Shader "Custom/Reef"
{
    Properties
    {
        _CausticStrength ("焦散强度", Range(0.0, 2.0)) = 0.8
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
            Name "ReefForward"
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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float _CausticStrength;
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
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();

                half ndl = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                float3 col = IN.color.rgb * (mainLight.color * ndl * 0.9 + 0.35);

                // 与水面/海床同款焦散光网
                float t = _Time.y;
                float n1 = FoamPattern(IN.positionWS.xz * 0.18, t * 0.8);
                float n2 = FoamPattern(IN.positionWS.xz * 0.45 + 17.3, t * 1.1);
                float web = (1.0 - abs(n1 * 2.0 - 1.0)) * 0.65 + (1.0 - abs(n2 * 2.0 - 1.0)) * 0.35;
                col += mainLight.color * pow(saturate(web), 6.0) * _CausticStrength;

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}