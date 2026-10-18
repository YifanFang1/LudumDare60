// ============================================================================
//  SPRITE OUTLINE (sprite shader)
//
//  Draws the sprite with any number of coloured outlines around its shape
//  (up to MAX_LAYERS). Layer 0 hugs the sprite, layer 1 goes around layer 0,
//  and so on. The outline is worked out from the sprite's alpha every frame,
//  so it follows animation frames, flipping and movement by itself.
//
//  Don't set this material's values by hand: the SpriteOutline component sends
//  them per renderer (colours, widths, where the sprite sits in its texture).
//  Without that component the sprite draws plainly, with no outline.
//
//  How a pixel decides what it is:
//    inside the sprite                -> the sprite
//    shape within width 0 of it       -> layer 0's colour
//    shape within width 0 + width 1   -> layer 1's colour
//    ...                              -> nothing
//  "Shape within r" is checked by sampling the sprite's alpha on rings of
//  radius r around the pixel (more samples on bigger rings, so wide outlines
//  stay smooth around thin parts).
//
//  The quad is grown by the total outline width in the vertex shader, so the
//  PNG needs no empty padding. It needs Mesh Type: Full Rect in the sprite's
//  import settings. A Tight mesh hugs the art, so SpriteOutline doesn't grow
//  it (that would cut the art off) and the outline is clipped instead.
//
//  It lives in a Resources folder so it is always included in builds.
// ============================================================================
Shader "LD/SpriteOutline"
{
    Properties
    {
        [MainTexture] [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "True" }
        Blend One OneMinusSrcAlpha   // premultiplied colour, so see-through outline colours blend correctly
        ZWrite Off Cull Off          // Cull Off: fighters facing left are flipped with a negative scale

        Pass
        {
            Name "Outline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_LAYERS 8      // must match SpriteOutline.MaxLayers
            #define MIN_DIRECTIONS 12 // samples per ring: bigger rings get more, up to MAX_DIRECTIONS
            #define MAX_DIRECTIONS 32 // more = smoother wide outlines, slower
            #define MAX_RINGS 4       // rings per layer at most: wide layers get several so thin parts (a sword tip) aren't missed

            TEXTURE2D(_MainTex);      // set by the SpriteRenderer
            SAMPLER(sampler_MainTex);

            // All set per renderer by SpriteOutline (MaterialPropertyBlock).
            float4 _SpriteLocalRect;  // the sprite's mesh in object space: xy = min corner, zw = max corner
            float4 _SpriteUVRect;     // the same corners in texture UVs (the frame's place in its sheet)
            float4 _WorldToUV;        // xy: UV distance per world unit
            float4 _TexSize;          // xy: texture size in pixels
            float4 _PadLocal;         // xy: how far to grow the quad, in object space
            float _OutlineCount;      // how many layers are in use
            float _OutlineRadii[MAX_LAYERS];     // each layer's OUTER edge, in world units (widths added up)
            float4 _OutlineColors[MAX_LAYERS];   // linear colour + alpha

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                float3 pos = input.positionOS;
                float2 uv = input.uv;

                // Grow the quad outwards so the outline has pixels to draw on, and stretch the UVs
                // to match (UVs past the frame's edge read as empty, see SampleAlpha).
                float2 size = _SpriteLocalRect.zw - _SpriteLocalRect.xy;
                if (size.x > 0 && size.y > 0)   // zero until SpriteOutline has sent its values
                {
                    float2 center = (_SpriteLocalRect.xy + _SpriteLocalRect.zw) * 0.5;
                    pos.xy += sign(pos.xy - center) * _PadLocal.xy;
                    uv = lerp(_SpriteUVRect.xy, _SpriteUVRect.zw, (pos.xy - _SpriteLocalRect.xy) / size);
                }

                Varyings output;
                output.positionCS = TransformObjectToHClip(pos);
                output.color = input.color;
                output.uv = uv;
                return output;
            }

            // 1 inside this frame's rectangle, 0 outside. In a sprite sheet this stops the outline
            // from picking up the neighbouring frame. Half a texel in from the edge, because texture
            // filtering blends each sample with the texel next to it.
            float InFrame(float2 uv)
            {
                float2 halfTexel = 0.5 / max(_TexSize.xy, 1);
                float2 inside = step(_SpriteUVRect.xy + halfTexel, uv) * step(uv, _SpriteUVRect.zw - halfTexel);
                return inside.x * inside.y;
            }

            // How much shape is at uv, for the outline. Doubled, so a tiny protrusion that is only a
            // faint 1-2 pixel line (a whisker, a tail) still gets a full-strength outline instead
            // of a see-through, broken one.
            float SampleAlpha(float2 uv)
            {
                return saturate(SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a * InFrame(uv) * 2);
            }

            // The most shape found on a circle of `radius` world units around uv. About one sample
            // every 1.5 texels around the circle. `twist` (0 or 0.5) turns odd rings half a step, so
            // the gaps of one ring are covered by the next.
            float RingAlpha(float2 uv, float radius, float texelsPerUnit, float twist)
            {
                float2 offset = radius * _WorldToUV.xy;
                int directions = clamp((int)ceil(TWO_PI * radius * texelsPerUnit / 1.5), MIN_DIRECTIONS, MAX_DIRECTIONS);
                float stepAngle = TWO_PI / directions;
                float2 dir;
                sincos(stepAngle * twist, dir.y, dir.x);
                float c = cos(stepAngle);
                float s = sin(stepAngle);
                float best = 0;
                [loop]
                for (int i = 0; i < MAX_DIRECTIONS; i++)
                {
                    if (i >= directions) break;
                    best = max(best, SampleAlpha(uv + dir * offset));
                    dir = float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);   // turn to the next direction
                }
                return best;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                tex.a *= InFrame(input.uv);
                float4 color = float4(tex.rgb * tex.a, tex.a);

                // Solid sprite pixels are covered anyway: skip the outline work.
                int count = (int)_OutlineCount;
                if (count <= 0 || color.a >= 0.999) return color;

                float texelsPerUnit = max(_WorldToUV.x * _TexSize.x, _WorldToUV.y * _TexSize.y);
                float covered = 0;    // how much shape is within the current radius (0..1, soft at the edges)
                float previous = 0;
                float inner = 0;
                float4 outline = 0;

                [loop]
                for (int k = 0; k < MAX_LAYERS; k++)
                {
                    if (k >= count) break;
                    float outer = _OutlineRadii[k];

                    // About one ring every 2 texels of layer width, at most MAX_RINGS.
                    int rings = clamp((int)ceil((outer - inner) * texelsPerUnit * 0.5), 1, MAX_RINGS);
                    [loop]
                    for (int r = 1; r <= MAX_RINGS; r++)
                    {
                        if (r > rings) break;
                        covered = max(covered, RingAlpha(input.uv, lerp(inner, outer, r / (float)rings), texelsPerUnit, (r & 1) * 0.5));
                    }

                    // This layer only gets the part that the layers inside it didn't already cover, so
                    // a see-through inner layer shows the background, not the layer around it.
                    float4 c = _OutlineColors[k];
                    outline += float4(c.rgb * c.a, c.a) * (covered - previous);
                    previous = covered;
                    inner = outer;
                    if (covered >= 0.999) break;   // fully covered: the layers further out can't show here
                }

                // The outline goes BEHIND the sprite, so soft sprite edges blend into it.
                return color + (1 - color.a) * outline * input.color.a;
            }
            ENDHLSL
        }
    }
}
