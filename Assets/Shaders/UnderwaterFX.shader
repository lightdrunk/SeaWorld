Shader "Custom/UnderwaterFX"
{
    Properties
    {
        _TintColor ("水下色调", Color) = (0.02, 0.14, 0.22, 0.55)
        _WaveStrength ("画面晃动强度", Range(0.0, 0.05)) = 0.012
        _CausticScale ("焦散密度", Range(1.0, 20.0)) = 8.0
        _CausticSpeed ("焦散速度", Range(0.0, 3.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+200"
        }

        Pass
        {
            Name "UnderwaterOverlay"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _WaveStrength;
                float _CausticScale;
                float _CausticSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float CausticLayer(float2 uv, float t)
            {
                float v = 0.0;
                v += sin(uv.x * 21.0 + t * 1.7) * sin(uv.y * 17.0 - t * 1.3);
                v += sin((uv.x + uv.y) * 13.0 - t * 2.1) * sin((uv.x - uv.y) * 19.0 + t * 1.1);
                return v;
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // 网格内随机亮点，模拟水中悬浮颗粒：小、稀、闪烁、缓漂
            float Sparkles(float2 uv, float t, float density)
            {
                float2 g = uv * float2(density, density * 0.56) + float2(t * 0.2, -t * 0.06);
                float2 id = floor(g);
                float2 f = frac(g) - 0.5;
                float rnd = Hash21(id);
                float2 off = (frac(float2(rnd * 127.1, rnd * 311.7)) - 0.5) * 0.6;
                float d = length(f - off);
                float twinkle = 0.35 + 0.65 * sin(t * 2.0 + rnd * 31.4);
                return smoothstep(0.045, 0.0, d) * step(0.88, rnd) * saturate(twinkle);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _CausticSpeed;

                // UV 轻微波动，模拟水下折射晃动
                float2 uv = IN.uv;
                uv += float2(sin(uv.y * 12.0 + t * 1.6), cos(uv.x * 10.0 - t * 1.2)) * _WaveStrength;

                float caustic = CausticLayer(uv * _CausticScale * 0.1, t) * 0.5 + 0.5;
                caustic = pow(saturate(caustic), 3.0);

                // 边缘变暗，营造水体包裹感
                float2 d = IN.uv - 0.5;
                float vignette = saturate(1.0 - dot(d, d) * 2.2);

                // 悬浮颗粒：两层视差 + 闪烁，整体减淡避免像脏点
                float particles = (Sparkles(uv, t * 0.5, 70.0) + Sparkles(uv, t * 0.3, 34.0) * 0.6) * 0.5;

                float3 col = _TintColor.rgb + caustic * 0.08 + particles * 0.55;
                float alpha = _TintColor.a * (0.55 + 0.45 * vignette) + caustic * 0.06 + particles * 0.3;
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}