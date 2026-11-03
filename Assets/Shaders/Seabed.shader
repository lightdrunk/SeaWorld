Shader "Custom/Seabed"
{
    Properties
    {
        _SandColor ("沙色亮部", Color) = (0.78, 0.70, 0.52, 1)
        _SandColor2 ("沙色暗部", Color) = (0.50, 0.44, 0.32, 1)
        _CausticStrength ("焦散强度", Range(0.0, 2.0)) = 0.7
        _PatternScale ("沙纹密度", Range(0.005, 0.5)) = 0.06
        _RimHaze ("边缘雾色", Color) = (0.36, 0.50, 0.60, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "SeabedForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SandColor;
                float4 _SandColor2;
                float _CausticStrength;
                float _PatternScale;
                float4 _RimHaze;
            CBUFFER_END

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float FoamPattern(float2 p, float t)
            {
                float n = ValueNoise(p + float2(t * 0.05, t * 0.03)) * 0.6;
                n += ValueNoise(p * 2.3 - float2(t * 0.08, t * 0.04)) * 0.4;
                return n;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                float t = _Time.y;

                // 沙地：双尺度噪声混出明暗沙纹
                float sandNoise = ValueNoise(IN.positionWS.xz * _PatternScale) * 0.6
                                + ValueNoise(IN.positionWS.xz * _PatternScale * 3.7) * 0.4;
                float3 sand = lerp(_SandColor2.rgb, _SandColor.rgb, sandNoise);

                half ndl = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                float3 col = sand * (mainLight.color * ndl * 0.9 + 0.35);

                // 焦散投影：与水面同款双尺度脊状光网
                float n1 = FoamPattern(IN.positionWS.xz * 0.18, t * 0.8);
                float n2 = FoamPattern(IN.positionWS.xz * 0.45 + 17.3, t * 1.1);
                float web = (1.0 - abs(n1 * 2.0 - 1.0)) * 0.65 + (1.0 - abs(n2 * 2.0 - 1.0)) * 0.35;
                col += mainLight.color * pow(saturate(web), 6.0) * _CausticStrength;

                // 边缘上翘区（世界 y > -3）渐隐到雾色：水下封住天空盒缝隙，水上呈远礁薄雾感
                float rimFade = saturate((IN.positionWS.y + 3.0) / 4.0);
                col = lerp(col, _RimHaze.rgb, rimFade);

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}