// ============================================================================
//  CRT FILTER (full screen shader)
//
//  Drawn over the whole screen by CRTRenderPass after everything else.
//  It has no Properties block on purpose: every value is a "global" value
//  sent by CRTSettings.cs each frame, so there is no material to edit.
//
//  It lives in a Resources folder so CRTSettings can load it by name and
//  so Unity always includes it in builds.
// ============================================================================
Shader "Hidden/LD/CRT"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "CRT"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Gives us Vert (a full screen triangle) and _BlitTexture (the picture so far).
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Set by CRTSettings.ApplySettings()
            float _DefeatGrey;
            float _Intensity;
            float _InnerIntensity;
            float _OuterIntensity;
            float _EdgeFalloff;
            float4 _TintColor;
            float _TintStrength;
            float _ColorSplit;
            float _WaveAmount;
            float _WaveFrequency;
            float _WaveSpeed;
            float _ScanlineScrollSpeed;
            float _ScanlineCount;
            float _ScanlineDarkness;
            float _Curvature;
            float _VignetteStrength;

            half3 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float t = _Time.y;
                half3 original = SampleScreen(uv);

                // How strong the effect is here: _InnerIntensity in the middle,
                // _OuterIntensity at the corners, _EdgeFalloff shapes the blend.
                float2 centered = uv * 2.0 - 1.0;                    // -1..1, 0 at the middle
                float edge = pow(saturate(length(centered) / 1.4142), _EdgeFalloff);
                float strength = _Intensity * lerp(_InnerIntensity, _OuterIntensity, edge);

                // Bulge the picture like a curved tube screen.
                // Dividing by (1 + bend) zooms in just enough that the corners land
                // exactly on the picture's corners, so no black shows at the edges.
                float bend = _Curvature * strength;
                float2 curved = centered * (1.0 + bend * centered.yx * centered.yx) / (1.0 + bend);
                float2 crtUV = curved * 0.5 + 0.5;

                // Anything pushed off the screen by the curve is black.
                if (crtUV.x < 0.0 || crtUV.x > 1.0 || crtUV.y < 0.0 || crtUV.y > 1.0)
                    return half4(0, 0, 0, 1);

                // Wobble each row sideways a little.
                crtUV.x += sin(crtUV.y * _WaveFrequency + t * _WaveSpeed) * _WaveAmount * strength;

                // Split red and blue apart sideways.
                float split = _ColorSplit * strength;
                half3 col;
                col.r = SampleScreen(crtUV + float2(split, 0)).r;
                col.g = SampleScreen(crtUV).g;
                col.b = SampleScreen(crtUV - float2(split, 0)).b;

                // Scanlines that slowly scroll.
                float scan = 0.5 + 0.5 * sin((crtUV.y * _ScanlineCount + t * _ScanlineScrollSpeed) * 3.14159265);
                col *= lerp(1.0, 1.0 - _ScanlineDarkness, scan);

                // Tint towards the screen's phosphor color.
                col = lerp(col, col * _TintColor.rgb, _TintStrength);

                // Dark corners.
                float vignette = 1.0 - _VignetteStrength * smoothstep(0.5, 1.5, length(curved));
                col *= vignette;

                half3 result = lerp(original, col, saturate(strength));
                half grey = dot(result, half3(0.2126, 0.7152, 0.0722));
                return half4(lerp(result, grey.xxx, saturate(_DefeatGrey)), 1);
            }
            ENDHLSL
        }
    }
}
