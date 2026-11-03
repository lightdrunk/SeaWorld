Shader "Custom/Fish"
{
    Properties
    {
        _Color ("鱼身色", Color) = (1, 0.45, 0.1, 1)
        _BellyColor ("鱼肚色", Color) = (1, 0.9, 0.75, 1)
        _SwimSpeed ("摆尾速度", Range(0.0, 20.0)) = 7.0
        _SwimAmp ("摆尾幅度", Range(0.0, 1.0)) = 0.3
        [HideInInspector] _WavePhase ("行波相位", Float) = 0
        [HideInInspector] _SwingNorm ("摆幅归一", Range(0.0, 1.0)) = 0.6
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
            Name "FishForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

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
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half4 color : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BellyColor;
                float _SwimSpeed;
                float _SwimAmp;
                float _WavePhase;
                float _SwingNorm;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 全身连续行波（carangiform 泳姿）：头部 6% 微幅 → 尾梢 100%，沿 u² 平滑连续增长
                // 相位/摆幅由 CPU 按每条鱼的瞬时游速积分传入（_WavePhase/_SwingNorm），游快摆快、滑行微摆，严格同步
                // _WavePhase 为 0 时回退到时间驱动（编辑器预览/未接入 CPU 的情形）
                float3 posOS = IN.positionOS.xyz;
                float u = IN.uv.x;
                float phase = _WavePhase != 0.0 ? _WavePhase : _Time.y * _SwimSpeed;
                float norm = _SwingNorm > 0.0 ? _SwingNorm : 0.6;
                float amp = _SwimAmp * (0.15 + 0.85 * norm);
                float w = 0.06 + 0.94 * u * u;
                float bend = sin(phase - u * 5.0);
                posOS.x += bend * amp * w;

                OUT.positionHCS = TransformObjectToHClip(posOS);
                OUT.positionWS = TransformObjectToWorld(posOS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();

                float3 col = lerp(_BellyColor.rgb, _Color.rgb, saturate(IN.uv.y * 1.6));
                col *= IN.color.rgb;

                // 鳞片纹路：沿体侧细密的弧形交错纹（逐像素，不受网格密度限制）
                float scaleRows = sin(IN.uv.y * 52.0 + sin(IN.uv.x * 30.0) * 1.4);
                col *= 0.93 + 0.07 * scaleRows;

                half3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                half ndl = saturate(dot(normalWS, mainLight.direction));
                col *= mainLight.color * ndl * 0.6 + 0.42;

                // 鱼皮光泽：镜面高光 + 菲涅尔银光（银色鱼的关键观感）
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), 40.0);
                col += mainLight.color * spec * 0.55;
                float fres = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 3.0);
                col += mainLight.color * fres * 0.22;

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}