Shader "Custom/Water"
{
    Properties
    {
        _ShallowColor ("浅水颜色", Color) = (0.18, 0.48, 0.58, 1)
        _DeepColor ("深水颜色", Color) = (0.02, 0.07, 0.16, 1)
        _Smoothness ("光滑度", Range(0.0, 1.0)) = 0.92
        _WaveScale ("波浪密度", Range(0.05, 4.0)) = 0.2
        _WaveSpeed ("流动速度", Range(0.0, 3.0)) = 0.8
        _WaveHeight ("波浪起伏", Range(0.0, 8.0)) = 1.5
        _SpecPower ("高光锐度", Range(8.0, 512.0)) = 180
        _FresnelPower ("菲涅尔强度", Range(1.0, 12.0)) = 5
        _FoamColor ("浪尖白沫颜色", Color) = (0.94, 0.98, 1.0, 1)
        _FoamThreshold ("白沫阈值", Range(0.0, 1.0)) = 0.55
        _FoamNoise ("白沫碎裂度", Range(0.2, 4.0)) = 1.0
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
            Name "WaterForward"
            Cull Off
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _Smoothness;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveHeight;
                float _SpecPower;
                float _FresnelPower;
                float4 _FoamColor;
                float _FoamThreshold;
                float _FoamNoise;
            CBUFFER_END

            // 叠加一条正弦波：累加高度 h 与水平梯度 g
            void AddWave(float2 p, float2 dir, float k, float a, float speed, float t, inout float h, inout float2 g)
            {
                float phase = dot(p, dir) * k + t * speed;
                h += sin(phase) * a;
                g += dir * (cos(phase) * a * k);
            }

            // 几何长波：顶点位移与法线共用，保证光照与起伏一致
            float GeometryHeight(float2 p, float t, out float2 grad)
            {
                float h = 0.0;
                grad = float2(0.0, 0.0);
                AddWave(p, normalize(float2(1.0, 0.35)), 0.25 * _WaveScale, 1.00, 1.1 * _WaveSpeed, t, h, grad);
                AddWave(p, normalize(float2(-0.6, 1.0)), 0.45 * _WaveScale, 0.60, 1.7 * _WaveSpeed, t, h, grad);
                AddWave(p, normalize(float2(0.3, -0.9)), 0.90 * _WaveScale, 0.40, 2.3 * _WaveSpeed, t, h, grad);
                return h;
            }

            // 着色法线 = 几何法线 + 高频细节（制造细碎波光）
            float3 WaveNormal(float2 p, float t, out float height)
            {
                float2 grad;
                height = GeometryHeight(p, t, grad);

                float2 detailGrad = float2(0.0, 0.0);
                float detailHeight = 0.0;
                AddWave(p, normalize(float2(0.9, 0.6)), 2.6 * _WaveScale, 0.09, 3.1 * _WaveSpeed, t, detailHeight, detailGrad);
                AddWave(p, normalize(float2(-0.8, 0.2)), 4.2 * _WaveScale, 0.06, 4.3 * _WaveSpeed, t, detailHeight, detailGrad);

                float2 totalGrad = grad * _WaveHeight + detailGrad * 0.5;
                return normalize(float3(-totalGrad.x, 1.0, -totalGrad.y));
            }

            // 程序化值噪声：用于打碎白沫边缘
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
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                // 顶点真正沿世界 Y 轴起伏（世界 XZ 取样，海面尺度无关）
                float2 grad;
                float waveHeight = GeometryHeight(posInputs.positionWS.xz, _Time.y, grad) * _WaveHeight;
                float3 displacedWS = posInputs.positionWS + float3(0.0, waveHeight, 0.0);

                float4 clipPos = TransformWorldToHClip(displacedWS);
                OUT.positionHCS = clipPos;
                OUT.positionWS = displacedWS;
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(clipPos.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float waveHeight;
                float3 normalWS = WaveNormal(IN.positionWS.xz, t, waveHeight);

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                Light mainLight = GetMainLight();

                // 水色：波峰偏浅、波谷偏深
                float3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(waveHeight * 0.25 + 0.5));

                // 反射：采样反射探针 / 环境立方体贴图
                float3 reflectDir = reflect(-viewDirWS, normalWS);
                half perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(_Smoothness);
                float3 reflectionColor = GlossyEnvironmentReflection(reflectDir, perceptualRoughness, 1.0);

                // 菲涅尔：视线越掠射，反射越强
                float fresnel = pow(1.0 - saturate(max(dot(normalWS, viewDirWS), 0.0)), _FresnelPower);

                // 水上：菲涅尔混合反射 + 太阳高光
                float3 aboveColor = lerp(waterColor, reflectionColor, saturate(fresnel));
                float3 halfDir = normalize(viewDirWS + mainLight.direction);
                float spec = pow(saturate(dot(normalWS, halfDir)), _SpecPower);
                aboveColor += mainLight.color * spec * 1.2;

                // 水下：水色提亮提饱和，噪声焦散波纹，向太阳方向形成亮窗（斯涅尔窗效果）
                float sunGlow = pow(saturate(dot(-viewDirWS, mainLight.direction)), 6.0);
                // 双尺度脊状噪声干涉：亮脊交织成锐利的焦散光网
                float n1 = FoamPattern(IN.positionWS.xz * 0.18, t * 0.8);
                float n2 = FoamPattern(IN.positionWS.xz * 0.45 + 17.3, t * 1.1);
                float web = (1.0 - abs(n1 * 2.0 - 1.0)) * 0.65 + (1.0 - abs(n2 * 2.0 - 1.0)) * 0.35;
                float caustic = pow(saturate(web), 6.0);
                float3 underColor = waterColor * (mainLight.color * 0.6 + 0.8);
                underColor += mainLight.color * (sunGlow * 0.8 + caustic * (0.5 + 0.9 * sunGlow));

                // 上/下分支按"相机与该浪面点的高度关系"判定（相机在点下方 → 水下着色）。
                // 之前用浪面法线判向（dot(normalWS, viewDirWS)）：125m 长周期涌浪会把整片
                // 可见海面的法线一致推过零点，海面每隔几秒在"天空反射/水下焦散"两种外观间
                // 整体跳变。改按高度判定后分支稳定；法线只用于各分支内部的光照细节。
                // 相机在水下时不再采样天空反射，水下视角无白色地平线（硬约束保持）。
                float underBlend = saturate((IN.positionWS.y - _WorldSpaceCameraPos.y) * 2.0 + 0.5);
                float3 color = lerp(aboveColor, underColor, underBlend);

                // 轻微漫反射，让水色随主光变化
                color *= mainLight.color * 0.2 + 0.8;

                // 浪尖白沫：波峰过阈值时触发，用噪声打碎边缘
                float crestT = saturate(waveHeight * 0.5 + 0.5);
                float foamH = saturate((crestT - _FoamThreshold) / max(0.001, 1.0 - _FoamThreshold));
                float pattern = FoamPattern(IN.positionWS.xz * 0.35 * _FoamNoise, t);
                float foam = saturate(foamH * 2.2 - pattern * 1.1);
                foam = saturate(foam * 1.5) * _FoamColor.a * (1.0 - underBlend * 0.75);
                color = lerp(color, _FoamColor.rgb, foam);

                // 接入场景雾：水下时远处的灰色被雾染成雾色（青蓝）
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}