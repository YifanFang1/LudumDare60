Shader "LD/DefeatBurn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Burn ("Burn", Range(0,1)) = 0
        _MinY ("Bottom", Float) = 0
        _Height ("Height", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _Burn, _MinY, _Height;
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float2 local : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv; o.color = v.color; o.local = v.positionOS.xy;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float noise = frac(sin(dot(floor(i.local * 45), float2(127.1,311.7))) * 43758.5453);
                float height = (i.local.y - _MinY) / _Height;
                float remaining = height + noise * 0.14 - lerp(-0.15, 1.2, _Burn);
                clip(remaining);
                clip(col.a - 0.01);
                float edge = 1.0 - smoothstep(0, 0.12, remaining);
                float charred = 1.0 - smoothstep(0.1, 0.24, remaining);
                col.rgb *= lerp(1, 0.12, charred * step(0.001, _Burn));
                col.rgb = lerp(col.rgb, lerp(half3(1,0.12,0), half3(1,0.85,0.15), edge), edge * step(0.001, _Burn));
                return col;
            }
            ENDHLSL
        }
    }
}
