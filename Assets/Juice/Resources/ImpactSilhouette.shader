// ============================================================================
//  IMPACT SILHOUETTE (sprite shader)
//
//  Draws a sprite as one flat colour, keeping only its outline (alpha).
//  JuiceImpactFrame swaps the fighters onto this for a split second on heavy
//  hits, so every art style (pixel, photo, hand drawn) turns into the same
//  clean white shape. It also draws the black background quad and the sparks.
//
//  _SrcBlend picks how it is drawn (set from code, see JuiceImpactFrame):
//    One              = normal: flat _Color over whatever is behind
//    OneMinusDstColor = invert: flips what is behind (black <-> white), so
//                       sparks show up on the black background AND on the
//                       white fighters
//
//  It lives in a Resources folder so JuiceImpactFrame can load it by name and
//  so Unity always includes it in builds.
// ============================================================================
Shader "Hidden/LD/ImpactSilhouette"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend [_SrcBlend] OneMinusSrcAlpha   // the colour below is premultiplied, so this works for both modes
        ZWrite Off Cull Off   // Cull Off: fighters facing left are flipped with a negative scale

        Pass
        {
            Name "Silhouette"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);      // set by the SpriteRenderer, or copied from the particle material
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;         // set by JuiceImpactFrame from JuiceProfile
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Only the shape is kept: the sprite's alpha (and the renderer's alpha), never its colours.
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a * _Color.a;
                return half4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
