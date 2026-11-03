Shader "Custom/FishTextured"
{
    Properties
    {
        _BaseMap ("贴图", 2D) = "white" {}
        _Gloss ("光泽度", Range(0.0, 1.0)) = 0.52
        _SwimSpeed ("摆尾速度", Range(0.0, 20.0)) = 7.0
        _SwimAmp ("摆尾幅度", Range(0.0, 1.0)) = 0.15
        _SwingAxis ("摆动轴 0=局部Y(天竺鲷) 1=局部Z(Y-up高模)", Float) = 0
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
            Name "FishTexturedForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

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
                float3 positionWS : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Gloss;
                float _SwimSpeed;
                float _SwimAmp;
                float _WavePhase;
                float _SwingNorm;
                float _SwingAxis;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 摆尾：顶点色 R 通道存头尾坐标（0=头部 → 1=尾梢）
                // 天竺鲷为 Z-up 网格（局部 +X 头、+Z 背）→ 侧向为局部 Y，沿 Y 摆（默认）
                // 小丑鱼/裂唇鱼为 Y-up 网格（局部 +X 头、+Y 背）→ 侧向为局部 Z，沿 Z 摆（_SwingAxis=1）
                // 全身连续行波（carangiform 泳姿）：头部 6% 微幅 → 尾梢 100%，沿 t² 平滑连续增长
                // 相位/摆幅由 CPU 按每条鱼的瞬时游速积分传入（_WavePhase/_SwingNorm），游快摆快、滑行微摆，严格同步
                // _WavePhase 为 0 时回退到时间驱动（编辑器预览/未接入 CPU 的情形）
                float3 posOS = IN.positionOS.xyz;
                float t = IN.color.r;
                float phase = _WavePhase != 0.0 ? _WavePhase : _Time.y * _SwimSpeed;
                float norm = _SwingNorm > 0.0 ? _SwingNorm : 0.6;
                float amp = _SwimAmp * (0.15 + 0.85 * norm);
                float w = 0.06 + 0.94 * t * t;
                float bend = sin(phase - t * 5.0);
                posOS += bend * amp * w * (_SwingAxis > 0.5 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0));

                OUT.positionHCS = TransformObjectToHClip(posOS);
                OUT.positionWS = TransformObjectToWorld(posOS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();

                // 模型自带 2K PBR 贴图（含鳞片细节），不再叠加程序化纹路
                half3 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;

                half3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                half ndl = saturate(dot(normalWS, mainLight.direction));
                col *= mainLight.color * ndl * 0.6 + 0.42;

                // 鱼皮光泽：镜面高光 + 菲涅尔，强度按导入说明分区光泽度（鱼身 0.52 / 眼 0.85 / 鳍 0.42）
                float gloss = saturate(_Gloss);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), lerp(24.0, 64.0, gloss));
                col += mainLight.color * spec * (0.25 + 0.55 * gloss);
                float fres = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 3.0);
                col += mainLight.color * fres * (0.1 + 0.25 * gloss);

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
