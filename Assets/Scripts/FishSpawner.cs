using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 程序化鱼群（9 个物种，外观按用户参考图）：
/// - 沙丁鱼：高处快速银色大群（10~20）；斑鰶：银色黄带中水层；蓝雀鲷：电光蓝小群（2~5）
/// - 小丑鱼：橙白三带，守在海葵附近（导入 Ocellaris_Unity.fbx 高模）；天竺鲷：红身黑带大眼，珊瑚丛小鱼群
/// - 蓝带裂唇鱼：淡紫蓝+贯穿黑纵带，绕礁石（导入 BluestreakCleanerWrasse_Adult_Unity.fbx 高模）；小型隆头鱼：亮黄，绕礁石
/// - 虾虎鱼：沙底红斑，底栖窜游；海马：黄褐环节，海草床竖姿慢漂
/// 体色花纹由顶点色绘制（Custom/Fish 乘算），摆尾由 shader 按 uv.x 弯曲。
/// </summary>
[ExecuteAlways]
public class FishSpawner : MonoBehaviour
{
    public int seed = 77;

    private enum Behavior { School, HoverAnemone, HoverCoral, RockRoam, BottomDart, GrassDrift }

    private enum TailShape { Forked, Rounded }          // 叉尾（雀鲷/鲱类）/ 圆扇尾（小丑鱼/隆头鱼等）
    private enum DorsalShape { Single, Rounded, Twin, Ribbon }  // 单棘背鳍 / 圆弧 / 双背鳍（虾虎鱼等）/ 长带状（裂唇鱼）

    private struct Fish
    {
        public Transform t;
        public Behavior behavior;
        public Vector3 anchor;      // 巡游锚点 / 栖息点
        public float angle;         // 轨道角
        public float radius;        // 轨道半径
        public float speed;
        public float height;        // 巡游高度（School）
        public float bobPhase;
        public bool upright;        // 海马：竖直姿态
        public float yawSeed;       // 海马慢转初始角
        public float burstPhase;    // 间歇摆尾/变速游动的独立相位（与 shader _BurstPhase 同源）
        public float roll;          // 当前侧倾角（平滑值）——转弯时向圆心内侧倾
        // 全身行波摆尾：相位按真实瞬时速度积分，经材质块传给 shader（与游动严格同步）
        public float wavePhase;
        public float swingSpeed;    // 物种摆尾频率（取自材质 _SwimSpeed）
        public Renderer[] renderers; // 本条鱼的渲染器（高模为多部件）
        public MaterialPropertyBlock block;
        // 底栖窜游
        public bool darting;
        public float dartTimer;
        public Vector3 dartTarget;
        public float dartSpeed;
        // 蒙皮导入模型（自带骨骼动画）：摆尾由 Animator 驱动，播放速度随瞬时游速缩放
        public Animator animator;
        public float animatorBase;
        // 程序化尾摆增强：FBX 自带 idle 摆尾幅度偏小，在 OnAnimatorMove（动画写完骨骼之后）叠加正弦偏转
        public FishTailBoost tailBoost;
        public float clear;         // 海床防穿富余量（鱼体半高 + 安全间距），任何位置不得低于海床+此值
    }

    private readonly List<Fish> _fish = new List<Fish>();

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        // 场景恢复/Inspector 变动期间 OnValidate 内禁止 DestroyImmediate —— 延迟到帧外重建
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= DelayedRebuild;
        UnityEditor.EditorApplication.delayCall += DelayedRebuild;
#else
        Rebuild();
#endif
    }

#if UNITY_EDITOR
    private void DelayedRebuild()
    {
        if (this != null)
        {
            Rebuild();
        }
    }
#endif

    private void Rebuild()
    {
        // 先确认 shader 可用，再清空子物体，避免缺 shader 时清空后无法重建
        Shader shader = Shader.Find("Custom/Fish");
        if (shader == null)
        {
            Debug.LogError("找不到 Custom/Fish shader");
            return;
        }

        ClearChildren();
        _fish.Clear();

        System.Random rng = new System.Random(seed);
        _seabed = FindFirstObjectByType<SeabedTerrain>();

        // 导入高模：天竺鲷（分区贴图）、小丑鱼与蓝带裂唇鱼（各模型说明文档的单张 BaseColor 贴图），其余物种为程序化网格
#if UNITY_EDITOR
        GameObject cardinalfishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/天竺鲷/Cardinalfish_Unity_Colored.fbx");
        if (cardinalfishPrefab == null) Debug.LogWarning("未找到 Cardinalfish_Unity_Colored.fbx，天竺鲷回退为程序化网格");
        GameObject clownfishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/小丑/Assets/Clownfish_Unity/Ocellaris_Unity.fbx");
        if (clownfishPrefab == null) Debug.LogWarning("未找到 Ocellaris_Unity.fbx，小丑鱼回退为程序化网格");
        GameObject wrassePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/蓝带裂唇鱼/BluestreakCleanerWrasse_UnityFBX/BluestreakCleanerWrasse_Adult_Unity.fbx");
        if (wrassePrefab == null) Debug.LogWarning("未找到 BluestreakCleanerWrasse_Adult_Unity.fbx，蓝带裂唇鱼回退为程序化网格");
        Texture2D clownfishTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/小丑/Assets/Clownfish_Unity/Textures/Ocellaris_BaseColor_2K.png");
        Texture2D wrasseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/蓝带裂唇鱼/BluestreakCleanerWrasse_UnityFBX/Textures/Wrasse_BaseColor_2K.png");
        GameObject damselfishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/NeonDamselfish_URP/Assets/NeonDamselfish/NeonDamselfish_Rigged.fbx");
        if (damselfishPrefab == null) Debug.LogWarning("未找到 NeonDamselfish_Rigged.fbx，蓝雀鲷回退为程序化网格");
        Material damselfishMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model/NeonDamselfish_URP/Assets/NeonDamselfish/Materials/PC_Atlas_URP.mat");
        GameObject seahorsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Trimaculatus_URP/Assets/Trimaculatus/Hippocampus_trimaculatus_Rigged.fbx");
        if (seahorsePrefab == null) Debug.LogWarning("未找到 Hippocampus_trimaculatus_Rigged.fbx，海马回退为程序化网格");
        Material seahorseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model/Trimaculatus_URP/Assets/Trimaculatus/Materials/HT_Atlas_URP.mat");
#else
        GameObject cardinalfishPrefab = null;
        GameObject clownfishPrefab = null;
        GameObject wrassePrefab = null;
        Texture2D clownfishTex = null;
        Texture2D wrasseTex = null;
        GameObject damselfishPrefab = null;
        Material damselfishMat = null;
        GameObject seahorsePrefab = null;
        Material seahorseMat = null;
#endif

        // ---- 物种定义（外观对应参考图）：沙丁鱼 10~20 成群，天竺鲷小鱼群，蓝雀鲷 2~5，其余 1~2 ----
        // 尾鳍长度约为体长 6~7 成（真鱼比例），鳍膜偏白与身体区分
        SpawnSpecies(rng, shader, "小丑鱼", 2, Behavior.HoverAnemone, BuildFish(0.38f, 0.2f, 0.13f, 0.26f, 0.16f, true, Clownfish, TailShape.Rounded, DorsalShape.Rounded),
            clownfishPrefab, 0.38f, clownfishTex, orbit: 0.8f, speed: 0.5f, matAmp: 0.045f, swingZ: true);
        SpawnSpecies(rng, shader, "蓝带裂唇鱼", 1, Behavior.RockRoam, BuildFish(0.4f, 0.11f, 0.08f, 0.24f, 0.1f, true, CleanerWrasse, TailShape.Forked, DorsalShape.Ribbon, analFin: true),
            wrassePrefab, 0.4f, wrasseTex, orbit: 1.2f, speed: 0.35f, matAmp: 0.05f, swingZ: true);
        SpawnSpecies(rng, shader, "蓝雀鲷", 3, Behavior.School, BuildFish(0.34f, 0.15f, 0.09f, 0.24f, 0.14f, true, BlueDamsel),
            damselfishPrefab, 0.18f, null, schoolAnchorRadius: 10f, heightMin: -6.5f, heightMax: -5.5f, orbit: 0.8f, speed: 0.32f, matAmp: 0.06f, matSpeed: 9.5f,
            importedMaterial: damselfishMat, importedAnimClip: "IdleSwim_2s", importedAnimSpeed: 0.9f,
            importedTailBone: "TailFin");
        SpawnSpecies(rng, shader, "天竺鲷", 5, Behavior.HoverCoral, BuildFish(0.28f, 0.115f, 0.055f, 0.13f, 0.07f, true, Cardinalfish, TailShape.Rounded, DorsalShape.Twin, eyeScale: 1.65f, dorsalScale: 0.6f, finFade: 0.35f),
            cardinalfishPrefab, 0.28f, null, orbit: 0.5f, speed: 0.35f, matAmp: 0.04f);
        SpawnSpecies(rng, shader, "小型隆头鱼", 2, Behavior.RockRoam, BuildFish(0.48f, 0.16f, 0.1f, 0.3f, 0.14f, true, Wrasse, TailShape.Forked, DorsalShape.Ribbon, forkDepth: 0.35f, eyeScale: 1.3f),
            orbit: 1.5f, speed: 0.6f, matAmp: 0.05f);
        SpawnSpecies(rng, shader, "虾虎鱼", 2, Behavior.BottomDart, BuildFish(0.24f, 0.12f, 0.09f, 0.16f, 0.08f, true, Goby, TailShape.Rounded, DorsalShape.Twin),
            matAmp: 0.03f);
        SpawnSpecies(rng, shader, "海马", 2, Behavior.GrassDrift, BuildSeahorse(0.34f, Seahorse),
            seahorsePrefab, 0.24f, null, orbit: 0.25f, speed: 0.12f, matAmp: 0.02f, upright: true,
            importedMaterial: seahorseMat, importedAnimClip: "Idle_FinPropulsion_2s", importedAnimSpeed: 0.75f, importedVertical: true);
        SpawnSpecies(rng, shader, "斑鰶", 2, Behavior.School, BuildFish(0.38f, 0.17f, 0.09f, 0.26f, 0.16f, true, GizzardShad, eyeScale: 1.25f),
            schoolAnchorRadius: 14f, heightMin: -6f, heightMax: -3f, orbit: 1.5f, speed: 0.7f, matAmp: 0.06f, matSpeed: 8.5f);
        SpawnSpecies(rng, shader, "沙丁鱼", 15, Behavior.School, BuildFish(0.42f, 0.11f, 0.08f, 0.3f, 0.14f, false, Sardine),
            schoolAnchorRadius: 13f, heightMin: -4f, heightMax: -2f, orbit: 2.5f, speed: 1.1f, matAmp: 0.07f, matSpeed: 10f);
    }

    // ---------- 物种生成 ----------

    private void SpawnSpecies(System.Random rng, Shader shader, string name, int count, Behavior behavior, Mesh mesh,
        GameObject importedPrefab = null, float importedLen = 0.28f, Texture2D baseTex = null, bool swingZ = false,
        float schoolAnchorRadius = 0f, float heightMin = 0f, float heightMax = 0f,
        float orbit = 1f, float speed = 0.5f, float matAmp = 0.3f, float matSpeed = 7f, bool upright = false,
        Material importedMaterial = null, string importedAnimClip = null,
        float importedAnimSpeed = 1f, bool importedVertical = false,
        string importedTailBone = null)
    {
        Transform group = EnsureGroup(name);

        // 行为锚点来源
        List<Vector3> spots = null;
        if (behavior == Behavior.HoverAnemone) spots = CollectCache("海葵");
        else if (behavior == Behavior.HoverCoral) spots = CollectCache("珊瑚");
        else if (behavior == Behavior.RockRoam) spots = CollectCache("礁石");
        else if (behavior == Behavior.GrassDrift) spots = CollectCache("海草");

        Vector2 schoolCenter = RandomInCircle(rng, schoolAnchorRadius);
        float schoolBaseY = (heightMin + heightMax) * 0.5f;

        // 同种聚群：整群共享一个小范围锚点（珊瑚/海草/礁石/海底任选其一），便于对照观察同类游动行为
        Vector3 speciesSpot = new Vector3((float)rng.NextDouble() * 20f - 10f, -10f, (float)rng.NextDouble() * 20f - 10f);
        if (behavior == Behavior.BottomDart)
        {
            Vector2 bo = RandomInCircle(rng, 25f);
            speciesSpot = new Vector3(bo.x, _seabed != null ? _seabed.GetWorldSeabedHeight(bo.x, bo.y) : -15f, bo.y);
        }
        else if (spots != null && spots.Count > 0)
        {
            speciesSpot = spots[rng.Next(spots.Count)];
        }

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"{name}_{i}");
            go.transform.SetParent(group, false);
            Animator fishAnimator = null;   // 蒙皮导入模型才有（骨骼动画驱动）
            FishTailBoost fishTailBoost = null; // 程序化尾摆增强组件（按物种指定尾骨骼）

            if (importedPrefab != null)
            {
                // 导入高模：空根节点承担游动逻辑，模型实例挂在子层级（自带 2K PBR 材质）
                GameObject model = new GameObject("Model");
                model.transform.SetParent(go.transform, false);
                GameObject fix = new GameObject("Fix");
                fix.transform.SetParent(model.transform, false);
                fix.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);   // 头部 +X → 前方 +Z
                GameObject inst = (GameObject)Object.Instantiate(importedPrefab, fix.transform);
                Bounds b = LocalMeshBounds(inst);                               // 网格本地包围盒（蒙皮 renderer.bounds 当帧未更新，不可靠）
                float s = importedLen / Mathf.Max(0.0001f, importedVertical ? b.size.y : b.size.x);  // 海马直立姿态按身高归一化，其余按头尾体长
                fix.transform.localScale = Vector3.one * s;
                Vector3 centerFix = fix.transform.InverseTransformPoint(inst.transform.TransformPoint(b.center));
                inst.transform.localPosition = -centerFix;                      // 中心对齐根原点

                // 蒙皮模型（自带骨骼动画）：统一赋共享 PBR 图集材质 + Animator 循环播放待机动画；
                // 静态高模：刷顶点色摆尾 + 贴图材质
                bool hasSkinned = false;
                Renderer[] instRenderers = inst.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in instRenderers)
                {
                    if (r is SkinnedMeshRenderer) { hasSkinned = true; break; }
                }

                if (hasSkinned && importedMaterial != null)
                {
                    foreach (Renderer r in instRenderers)
                    {
                        Material[] mats = new Material[r.sharedMaterials.Length];
                        for (int m = 0; m < mats.Length; m++) mats[m] = importedMaterial;
                        r.sharedMaterials = mats;
                        // 骨骼动画会让鱼游出绑定姿态的静态包围盒——贴近时视锥剔除误判导致"近了反而消失"
                        if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                    }
#if UNITY_EDITOR
                    fishAnimator = SetupImportedAnimator(inst, importedAnimClip, importedPrefab);
#endif
                    if (!string.IsNullOrEmpty(importedTailBone))
                    {
                        foreach (Transform tr in inst.GetComponentsInChildren<Transform>(true))
                        {
                            if (tr.name == importedTailBone)
                            {
                                fishTailBoost = inst.AddComponent<FishTailBoost>();
                                fishTailBoost.speed = matSpeed; // 摆动频率与程序化鱼一致
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ApplySwingAndTexture(inst, matAmp, matSpeed, baseTex, swingZ);  // 顶点色摆尾 + 贴图材质（baseTex 指定时统一使用）
                }
            }
            else
            {
                Material mat = new Material(shader);
                mat.SetColor("_Color", Color.white);
                mat.SetColor("_BellyColor", Color.white);
                mat.SetFloat("_SwimAmp", matAmp);
                mat.SetFloat("_SwimSpeed", matSpeed);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }

            float sc = 0.75f + (float)rng.NextDouble() * 0.5f;
            go.transform.localScale = new Vector3(sc, sc, sc);

            // 每条鱼独立的材质块：行波相位/摆幅逐帧更新，与游速严格同步
            float burstPhase = (float)rng.NextDouble() * 100f;
            Renderer[] fishRenderers = go.GetComponentsInChildren<Renderer>();
            MaterialPropertyBlock mpb = null;
            if (fishRenderers.Length > 0)
            {
                mpb = new MaterialPropertyBlock();
                foreach (Renderer r in fishRenderers) r.SetPropertyBlock(mpb);
            }

            Vector3 anchor;
            switch (behavior)
            {
                case Behavior.School:
                    anchor = new Vector3(schoolCenter.x + (float)rng.NextDouble() * 2f - 1f,
                                         schoolBaseY + ((float)rng.NextDouble() - 0.5f) * (heightMax - heightMin),
                                         schoolCenter.y + (float)rng.NextDouble() * 2f - 1f);
                    break;
                case Behavior.HoverAnemone:
                case Behavior.HoverCoral:
                case Behavior.RockRoam:
                case Behavior.GrassDrift:
                    anchor = speciesSpot + new Vector3((float)rng.NextDouble() - 0.5f,
                                                       behavior == Behavior.RockRoam ? 1.2f + (float)rng.NextDouble() * 1.5f : 0.35f + (float)rng.NextDouble() * 0.5f,
                                                       (float)rng.NextDouble() - 0.5f);
                    break;
                case Behavior.BottomDart:
                default:
                    Vector2 o = new Vector2(speciesSpot.x + (float)rng.NextDouble() * 2f - 1f,
                                            speciesSpot.z + (float)rng.NextDouble() * 2f - 1f);
                    float gy = _seabed != null ? _seabed.GetWorldSeabedHeight(o.x, o.y) : speciesSpot.y;
                    anchor = new Vector3(o.x, gy + 0.14f, o.y);
                    break;
            }

            Fish fish = new Fish
            {
                t = go.transform,
                behavior = behavior,
                anchor = anchor,
                angle = (float)rng.NextDouble() * Mathf.PI * 2f,
                radius = orbit * (0.6f + (float)rng.NextDouble() * 0.8f),
                speed = speed * (0.85f + (float)rng.NextDouble() * 0.3f),
                height = anchor.y,
                bobPhase = (float)rng.NextDouble() * Mathf.PI * 2f,
                upright = upright,
                yawSeed = (float)rng.NextDouble() * Mathf.PI * 2f,
                burstPhase = burstPhase,
                roll = 0f,
                wavePhase = 0.5f + (float)rng.NextDouble() * 6f,   // 非 0 初始值，避免 shader 走时间回退分支
                swingSpeed = matSpeed,
                renderers = mpb != null ? fishRenderers : null,
                block = mpb,
                darting = false,
                dartTimer = 0.5f + (float)rng.NextDouble() * 2f,
                dartSpeed = 0.8f + (float)rng.NextDouble() * 0.7f,
                dartTarget = anchor,
                clear = 0.25f,
                animator = fishAnimator,
                animatorBase = fishAnimator != null ? importedAnimSpeed : 0f,
                tailBoost = fishTailBoost,
            };

            PoseFish(fish, 0f);
            _fish.Add(fish);
        }
    }

    private SeabedTerrain _seabed;

    /// <summary>按初始状态摆放一条鱼（编辑模式静态分布 + Play 模式初帧）。</summary>
    private void PoseFish(Fish fish, float time)
    {
        Vector3 pos;
        Quaternion rot = Quaternion.identity;

        switch (fish.behavior)
        {
            case Behavior.BottomDart:
                pos = fish.anchor;
                rot = Quaternion.Euler(0f, fish.yawSeed * Mathf.Rad2Deg, 0f);
                break;
            case Behavior.GrassDrift:
                pos = OrbitPos(fish, time, 0.25f);
                rot = fish.upright
                    ? Quaternion.Euler(0f, fish.yawSeed * Mathf.Rad2Deg, 0f)
                    : Quaternion.LookRotation(OrbitTangent(fish), Vector3.up);
                break;
            default:
                pos = OrbitPos(fish, time, 0.4f);
                Vector3 tangent = OrbitTangent(fish);
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    rot = Quaternion.LookRotation(tangent, Vector3.up);
                }
                break;
        }

        fish.t.position = pos;
        fish.t.rotation = rot;
        // 静态摆放同样避开海床（编辑模式直接可见，不埋进沙丘）
        if (_seabed != null)
        {
            float minY = _seabed.GetWorldSeabedHeight(pos.x, pos.z) + fish.clear;
            if (pos.y < minY)
            {
                fish.t.position = new Vector3(pos.x, minY, pos.z);
            }
        }
        fish.t.position += Repel(fish) * 0.55f;   // 静态摆放同样避开礁石/珊瑚占地
    }

#if UNITY_EDITOR
    // 蒙皮导入模型：从 FBX 提取自带待机动画 clip，构建临时 AnimatorController 循环播放
    private static Animator SetupImportedAnimator(GameObject inst, string clipName, GameObject prefab)
    {
        if (string.IsNullOrEmpty(clipName)) return null;
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path)) return null;

        AnimationClip clip = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip c && !c.name.StartsWith("__preview"))
            {
                if (c.name == clipName) { clip = c; break; }
                if (clip == null) clip = c;
            }
        }
        if (clip == null)
        {
            Debug.LogWarning($"导入模型未找到动画 clip: {clipName}（{path}）");
            return null;
        }

        Animator animator = inst.GetComponent<Animator>();   // FBX 实例根节点自带 Animator（模型导入自动添加），直接复用
        if (animator == null) animator = inst.AddComponent<Animator>();
        var controller = new UnityEditor.Animations.AnimatorController();   // 单状态循环播放
        controller.AddLayer("Base");
        var state = controller.layers[0].stateMachine.AddState(clip.name);
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 鱼群远离相机也不冻结，回来看不穿帮
        return animator;
    }
#endif

    // 蒙皮鱼摆动相位器：借 OnAnimatorMove 回调逐帧积分相位（与动画同帧率推进）。
    // 注意：动画曲线写入骨骼发生在一切脚本回调之后，直接改尾骨骼会被覆写——
    // 因此尾鳍摆动交给动画本身，"左右摆动感"由鱼根的摆头（yaw sway）实现，在 Update 阶段叠加。
    private class FishTailBoost : MonoBehaviour
    {
        public float speed;     // 物种摆尾频率
        public float speedMul = 1f; // 瞬时游速倍率（UpdateSwing 每帧写入），相位随游速同步积分
        public float phase;

        private void OnAnimatorMove()
        {
            phase += speed * Mathf.Max(speedMul, 0.7f) * Time.deltaTime;
        }
    }

    // 摆尾与游动严格同步：波形相位按瞬时速度积分（游快→波传播快），摆幅按速度归一化（爆发全力摆、滑行微幅）
    // _WavePhase 初始非 0，逐帧累积；材质块写入后应用到本条鱼的所有渲染器（高模多部件共享一份相位）
    // 蒙皮导入模型走骨骼动画分支：播放速度按瞬时游速缩放（爆发快摆、滑行慢波）
    private static void UpdateSwing(ref Fish fish, float speedMul, float dt)
    {
        if (fish.animator != null)
        {
            float target = fish.animatorBase * Mathf.Clamp(speedMul, 0.7f, 2.2f);   // 下限 0.7：滑行段尾巴仍保持可见摆动，真鱼滑行时尾也不停摆
            if (!Mathf.Approximately(fish.animator.speed, target)) fish.animator.speed = target;
            if (fish.tailBoost != null) fish.tailBoost.speedMul = speedMul;
            return;
        }

        if (fish.block == null || fish.renderers == null)
        {
            return;
        }
        fish.wavePhase += fish.swingSpeed * speedMul * dt;
        fish.block.SetFloat("_WavePhase", fish.wavePhase);
        fish.block.SetFloat("_SwingNorm", Mathf.Clamp01((speedMul - 0.55f) / 1.15f));
        for (int r = 0; r < fish.renderers.Length; r++)
        {
            if (fish.renderers[r] != null)
            {
                fish.renderers[r].SetPropertyBlock(fish.block);
            }
        }
    }

    private static Vector3 OrbitPos(Fish fish, float time, float bobAmp)
    {
        // 水平轨迹：正圆叠加三种缓变——半径伸缩、椭圆变形、锚点游走，形成不规则玫瑰线而非机械正圆
        // 变形速率（0.07~0.21 rad/s）远低于转向速率，切向近似仍用圆切线，航向误差 < 5°，无需重算导数
        float rMod = 1f + Mathf.Sin(time * 0.13f + fish.bobPhase) * 0.22f;          // 轨道半径缓变（周期约 48s）
        float ax = 1f + Mathf.Sin(time * 0.07f + fish.burstPhase) * 0.3f;           // X 轴拉伸成椭圆（周期约 90s）
        float wanderAmp = fish.radius * 0.2f;                                       // 游走幅度随轨道缩放，悬停小鱼不漂远
        float wanderX = (Mathf.Sin(time * 0.09f + fish.bobPhase * 1.7f) * 0.7f
                       + Mathf.Sin(time * 0.21f + fish.burstPhase * 2.3f) * 0.3f) * wanderAmp;
        float wanderZ = (Mathf.Cos(time * 0.11f + fish.burstPhase * 1.3f) * 0.7f
                       + Mathf.Sin(time * 0.17f + fish.bobPhase) * 0.3f) * wanderAmp;
        Vector2 dir = new Vector2(Mathf.Cos(fish.angle), Mathf.Sin(fish.angle));
        return fish.anchor + new Vector3(dir.x * fish.radius * rMod * ax + wanderX,
                                         BobY(fish, time) * bobAmp,
                                         dir.y * fish.radius * rMod + wanderZ);
    }

    // 垂直漂移：双正弦叠加且每条鱼频率不同——周期 22~40 秒，缓慢罕见地换层，不做频繁上下
    private static float BobY(Fish fish, float time)
    {
        float f1 = 0.16f + (fish.burstPhase % 1f) * 0.14f;   // 主频：周期约 22~40 秒
        return Mathf.Sin(time * f1 + fish.bobPhase) * 0.85f
             + Mathf.Sin(time * f1 * 1.9f + fish.burstPhase * 3.1f) * 0.15f;
    }

    private static Vector3 OrbitTangent(Fish fish)
    {
        return new Vector3(-Mathf.Sin(fish.angle), 0f, Mathf.Cos(fish.angle));
    }

    // 障碍回避矢量（礁石/珊瑚，来自 ReefSpawner.Obstacles）：靠近时向外排斥，
    // 权重 w²——接触最强、检测边缘归零、矢量连续无跳变。
    // 悬停珊瑚的天竺鲷锚点就在珊瑚上：锚点 1.5m 内的障碍视为宿主，不回避。
    private static Vector3 Repel(Fish fish)
    {
        Vector3 repel = Vector3.zero;
        List<ReefSpawner.Obstacle> obstacles = ReefSpawner.Obstacles;
        if (obstacles.Count == 0)
        {
            return repel;
        }
        Vector3 pos = fish.t.position;
        for (int o = 0; o < obstacles.Count; o++)
        {
            // 宿主豁免：天竺鲷宿主珊瑚 / 海马宿主海草丛——锚点 1.5m 内的障碍不回避
            if (fish.behavior == Behavior.HoverCoral || fish.behavior == Behavior.GrassDrift)
            {
                float dx = obstacles[o].a.x - fish.anchor.x;
                float dz = obstacles[o].a.z - fish.anchor.z;
                if (dx * dx + dz * dz < 2.25f)
                {
                    continue;   // 宿主：不回避
                }
            }
            // 点到胶囊轴线的最近距离（礁石=点障碍 a==b，珊瑚枝条/海草叶=线段）
            Vector3 ab = obstacles[o].b - obstacles[o].a;
            float abLen = ab.sqrMagnitude;
            float kSeg = abLen > 0.0001f ? Mathf.Clamp01(Vector3.Dot(pos - obstacles[o].a, ab) / abLen) : 0f;
            Vector3 d = pos - (obstacles[o].a + ab * kSeg);
            d.y *= 0.4f;    // 障碍贴地，以水平回避为主
            float rr = obstacles[o].radius + 0.45f;
            float sq = d.sqrMagnitude;
            if (sq < rr * rr && sq > 0.0001f)
            {
                float dist = Mathf.Sqrt(sq);
                float w = 1f - dist / rr;
                repel += (d / dist) * (w * w);
            }
        }
        return repel;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_seabed == null)
        {
            _seabed = FindFirstObjectByType<SeabedTerrain>();
        }

        float dt = Time.deltaTime;
        float t = Time.time;
        for (int i = 0; i < _fish.Count; i++)
        {
            Fish fish = _fish[i];
            if (fish.t == null)
            {
                continue;
            }

            switch (fish.behavior)
            {
                case Behavior.BottomDart:
                    UpdateBottomDart(ref fish, dt);
                    break;
                case Behavior.GrassDrift:
                    fish.angle += fish.speed * dt * 0.6f;
                    fish.t.position = OrbitPos(fish, t, 0.12f) + Repel(fish) * 0.55f;   // 漂游途中同样避开礁石/珊瑚
                    fish.t.rotation = fish.upright
                        ? Quaternion.Euler(Mathf.Sin(t * 0.5f + fish.bobPhase) * 6f,
                                           fish.yawSeed * Mathf.Rad2Deg + Mathf.Sin(t * 0.25f + fish.bobPhase) * 30f, 0f)
                        : Quaternion.LookRotation(OrbitTangent(fish), Vector3.up);
                    UpdateSwing(ref fish, 0.6f, dt);
                    break;
                default:
                    // 间歇游动：与 shader 摆尾包络同形——爆发期游快+摆尾，滑行期减速+尾几乎停摆
                    // 再叠加慢速微调（±12%），游速在两个时间尺度上起伏，避免匀速绕圈
                    float cyc = Mathf.Repeat(t * 0.22f + fish.burstPhase, 1f);
                    float burst = Mathf.Clamp01(cyc * 4f) * Mathf.Clamp01((1f - cyc) * 2.2f);
                    float speedMul = (0.7f + 0.9f * burst) * (1f + Mathf.Sin(t * 0.31f + fish.bobPhase) * 0.12f);
                    fish.angle += fish.speed * dt * speedMul;
                    UpdateSwing(ref fish, speedMul, dt);   // 行波相位按瞬时速度积分——摆尾与游速严格同步
                    float bobAmp2 = fish.behavior == Behavior.School ? 0.4f : 0.15f;
                    fish.t.position = OrbitPos(fish, t, bobAmp2);
                    // 障碍回避：靠近礁石/珊瑚时位置被平滑推出（排斥矢量连续，不穿模）
                    Vector3 repel = Repel(fish);
                    fish.t.position += repel * 0.55f;
                    // 姿态先于路径：俯仰按"未来"的垂直速度计算（提前约 1/4 漂移周期）——
                    // 真鱼先抬头/低头，身体才沿新方向推进；到顶时已回平，不是边游边持续倾斜
                    Vector3 vel = OrbitTangent(fish) * fish.speed * fish.radius * speedMul;
                    float f1v = 0.16f + (fish.burstPhase % 1f) * 0.14f;
                    float tv = t + 1.6f;
                    vel.y = bobAmp2 * (0.85f * f1v * Mathf.Cos(tv * f1v + fish.bobPhase)
                                     + 0.15f * f1v * 1.9f * Mathf.Cos(tv * f1v * 1.9f + fish.burstPhase * 3.1f));
                    // 摆头：贴近障碍时朝向偏向离开方向（Slerp 上限 0.65，接触最强、边缘归零），俯仰/侧倾照常叠加
                    if (repel.sqrMagnitude > 0.0025f)
                    {
                        Vector3 away = new Vector3(repel.x, 0f, repel.z);
                        float mag = away.magnitude;
                        if (mag > 0.001f)
                        {
                            Vector3 blended = Vector3.Slerp(vel.normalized, away / mag, Mathf.Min(0.65f, mag));
                            vel = blended * vel.magnitude;
                        }
                    }
                    if (vel.sqrMagnitude > 0.000001f)
                    {
                        Vector3 dir = vel.normalized;
                        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg, -14f, 14f);
                        // 转弯侧倾：绕圈游的真鱼持续向圆心内侧倾，倾角正比于转向角速度（小轨道小鱼倾得多，大群沙丁鱼几乎水平）
                        // 轨道角速度 = speed/radius；angle 递增 = 向左绕圈 → 左倾（绕前向轴正旋转）
                        float targetRoll = Mathf.Clamp(fish.speed / Mathf.Max(fish.radius, 0.2f) * Mathf.Rad2Deg * 0.35f, 0f, 25f);
                        fish.roll = Mathf.Lerp(fish.roll, targetRoll, Mathf.Min(1f, dt * 2.5f));
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.0001f)
                        {
                            fish.t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(pitch, 0f, fish.roll);
                            // 蒙皮鱼头尾反相摆动：yaw 左右轻摆与尾动画同频反相（真鱼划水特征）。
                            // 动画曲线写入骨骼晚于一切脚本回调，尾骨骼叠加不可行——摆动感由鱼根转头承担，Update 阶段设置不会被覆写。
                            if (fish.tailBoost != null)
                            {
                                fish.t.rotation *= Quaternion.Euler(0f, -Mathf.Sin(fish.tailBoost.phase) * 7.5f, 0f);
                            }
                        }
                    }
                    break;
            }

            // 海床防穿：任何行为的位置低于沙丘安全高度时立即抬升——贴着地形滑过沙丘，绝不嵌入海床
            if (_seabed != null)
            {
                Vector3 p = fish.t.position;
                float minY = _seabed.GetWorldSeabedHeight(p.x, p.z) + fish.clear;
                if (p.y < minY)
                {
                    fish.t.position = new Vector3(p.x, minY, p.z);
                }
            }
            _fish[i] = fish;
        }

        UpdateWakeBubbles(dt, t);
    }

    private readonly List<Transform> _wakePool = new List<Transform>();
    private readonly List<float> _wakeLife = new List<float>();
    private Transform _wakeRoot;
    private float _wakeTimer;

    // 游动扰动：随机巡游鱼每隔 1~2 秒从尾部冒出一串 2~4 个小气泡（挂在"游动气泡"父物体下），上升并缩小消失
    private void UpdateWakeBubbles(float dt, float time)
    {
        Shader shader = Shader.Find("Custom/Bubble");
        if (shader == null || _fish.Count == 0) return;

        if (_wakeRoot == null)
        {
            _wakePool.Clear();      // Play/Edit 过渡后旧池引用已销毁，必须清空再重建，否则死引用刷 MissingReferenceException
            _wakeLife.Clear();
            _wakeRoot = new GameObject("游动气泡").transform;
            _wakeRoot.SetParent(transform, false);
            Material mat = new Material(shader);
            Mesh sphere = PrimitiveMeshes.UnitSphere;
            for (int i = 0; i < 24; i++)
            {
                GameObject go = new GameObject($"游动气泡_{i}");
                go.transform.SetParent(_wakeRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = sphere;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.SetActive(false);
                _wakePool.Add(go.transform);
                _wakeLife.Add(0f);
            }
        }

        _wakeTimer -= dt;
        if (_wakeTimer <= 0f)
        {
            _wakeTimer = 1f + Random.value * 1.2f;
            Fish f = _fish[Random.Range(0, _fish.Count)];
            if (f.t != null)
            {
                int n = 2 + Random.Range(0, 3);                 // 一串 2~4 个
                float delay = 0f;
                for (int k = 0; k < n; k++)
                {
                    for (int i = 0; i < _wakePool.Count; i++)
                    {
                        if (!_wakePool[i].gameObject.activeSelf && _wakeLife[i] <= 0f)
                        {
                            Vector3 off = Random.insideUnitSphere * 0.05f;
                            _wakePool[i].position = f.t.position - f.t.forward * 0.18f + Vector3.up * 0.02f + off;
                            _wakePool[i].localScale = Vector3.one * (0.015f + Random.value * 0.02f);
                            _wakeLife[i] = -delay;              // 负寿命 = 排队等待出场
                            delay += 0.07f;
                            break;
                        }
                    }
                }
            }
        }

        for (int i = 0; i < _wakePool.Count; i++)
        {
            _wakeLife[i] += dt;
            if (_wakeLife[i] < 0f) continue;                    // 还没轮到出场
            if (!_wakePool[i].gameObject.activeSelf) _wakePool[i].gameObject.SetActive(true);
            float life = _wakeLife[i];
            if (life > 2.2f)
            {
                _wakePool[i].gameObject.SetActive(false);
                _wakeLife[i] = 0f;
                continue;
            }
            Vector3 p = _wakePool[i].position + Vector3.up * (0.25f * dt);
            p.x += Mathf.Sin(time * 3f + i) * 0.02f * dt;
            _wakePool[i].position = p;
            float s = 1f - life / 2.2f;
            _wakePool[i].localScale = Vector3.one * (0.005f + 0.03f * s);
        }
    }

    private void UpdateBottomDart(ref Fish fish, float dt)
    {
        fish.dartTimer -= dt;

        if (fish.darting)
        {
            Vector3 toTarget = fish.dartTarget - fish.t.position;
            float step = fish.dartSpeed * dt;
            if (fish.dartTimer <= 0f || toTarget.magnitude <= step)
            {
                fish.darting = false;
                fish.dartTimer = 1.2f + (fish.bobPhase % 1f) * 3f;  // 用相位派生的伪随机休息时长
                fish.t.position = toTarget.magnitude <= step ? fish.dartTarget : fish.t.position;
            }
            else
            {
                Vector3 dir = toTarget.normalized;
                fish.t.position += dir * step;
                fish.t.position += Repel(fish) * 1.4f * dt;   // 窜游途中遇礁石/珊瑚被平滑推开绕行
                fish.t.rotation = Quaternion.LookRotation(dir, Vector3.up);
                UpdateSwing(ref fish, 1.2f, dt);    // 窜游：高频全力摆尾
            }
        }
        else if (fish.dartTimer <= 0f)
        {
            // 开始一次窜游：锚点附近随机新目标，贴着海床
            // 路径预检：直线路径若需爬越沙丘超过 0.35m（等于撞沙丘），换一个方向重新选目标（最多 5 次）
            float dist = 0.3f + (fish.radius % 1f) * 0.9f;
            Vector3 from = fish.t.position;
            Vector3 target = from;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float ang = fish.angle + fish.bobPhase + attempt * 2.4f;
                Vector2 o = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                target = fish.anchor + new Vector3(o.x, 0f, o.y);
                if (_seabed != null)
                {
                    target.y = _seabed.GetWorldSeabedHeight(target.x, target.z) + fish.clear;
                    bool blocked = false;
                    for (int s = 1; s <= 4; s++)
                    {
                        float k = s / 4f;
                        Vector3 mid = Vector3.LerpUnclamped(from, target, k);
                        float need = _seabed.GetWorldSeabedHeight(mid.x, mid.z) + fish.clear;
                        if (Mathf.Lerp(from.y, target.y, k) < need - 0.35f)
                        {
                            blocked = true;     // 该方向被沙丘挡住 → 回头重新设置新目标
                            break;
                        }
                    }
                    if (!blocked) break;
                }
                else
                {
                    break;
                }
            }
            fish.dartTarget = target;
            fish.darting = true;
            fish.dartTimer = 0.3f + (fish.speed % 1f) * 0.3f;
        }
        else
        {
            // 休息：贴地微颤
            Vector3 pos = fish.t.position;
            pos.y = fish.anchor.y + Mathf.Sin(Time.time * 6f + fish.bobPhase) * 0.015f;
            fish.t.position = pos + Repel(fish) * 1.4f * dt;   // 休息点若落在礁石/珊瑚上会被慢慢推出
            UpdateSwing(ref fish, 0.35f, dt);   // 休息时胸鳍/尾小幅慢摆
        }
    }

    // ---------- 场景栖息点 ----------

    private readonly Dictionary<string, List<Vector3>> _spotCache = new Dictionary<string, List<Vector3>>();

    private static readonly Dictionary<Mesh, Mesh> _swingMeshCache = new Dictionary<Mesh, Mesh>();
    // 彩色高模分区贴图（导入说明）：身体/鳍/尾/眼各一张 2K，按部件名分配
    private static readonly Dictionary<Texture2D, Material> _importedPartMats = new Dictionary<Texture2D, Material>();
    private static Texture2D _cBodyTex, _cFinsTex, _cTailTex, _cEyeTex;
    private static bool _coloredTexLoaded;

    private static void LoadColoredTextures()
    {
        if (_coloredTexLoaded) return;
        _coloredTexLoaded = true;
#if UNITY_EDITOR
        _cBodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/天竺鲷/Cardinalfish_Colored_Body_2K.png");
        _cFinsTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/天竺鲷/Cardinalfish_Colored_Fins_2K.png");
        _cTailTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/天竺鲷/Cardinalfish_Colored_Tail_2K.png");
        _cEyeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Model/天竺鲷/Cardinalfish_Colored_Eye_2K.png");
#endif
    }

    // 导入说明的分区规则：双背鳍/腹鳍/臀鳍 → Fins；尾鳍/胸鳍 → Tail；身体 → Body（第二材质槽位为眼睛 → Eye）
    private static Texture2D TexForPart(string partName)
    {
        string n = partName.ToLowerInvariant();
        if (n.Contains("body")) return _cBodyTex;
        if (n.Contains("dorsal") || n.Contains("pelvic") || n.Contains("anal")) return _cFinsTex;
        if (n.Contains("caudal") || n.Contains("pectoral")) return _cTailTex;
        if (n.Contains("eye")) return _cEyeTex;
        return null;
    }

    private static Material PartMaterial(Texture2D tex, Shader sh)
    {
        if (tex == null) return null;
        if (!_importedPartMats.TryGetValue(tex, out Material m))
        {
            m = new Material(sh);
            m.SetTexture("_BaseMap", tex);
            // 导入说明第 7 条的分区光泽度：鱼身 0.52 / 眼睛 0.85 / 鱼鳍（含尾鳍）0.42
            float gloss = 0.52f;
            if (tex == _cEyeTex) gloss = 0.85f;
            else if (tex == _cFinsTex || tex == _cTailTex) gloss = 0.42f;
            m.SetFloat("_Gloss", gloss);
            _importedPartMats[tex] = m;
        }
        return m;
    }

    // 给导入模型刷顶点色（R：头部 0 → 尾梢 1，沿模型局部 X 轴）并换用 Custom/FishTextured 材质，实现 shader 摆尾
    // baseTex 指定时（单材质高模：小丑鱼/蓝带裂唇鱼），所有部件统一用该贴图；否则按天竺鲷分区贴图规则分配
    // swingZ：Y-up 网格（小丑鱼/裂唇鱼）侧向为局部 Z，需切摆动轴；Z-up 网格（天竺鲷）默认沿局部 Y 摆
    private static void ApplySwingAndTexture(GameObject inst, float amp, float speed, Texture2D baseTex = null, bool swingZ = false)
    {
        Shader sh = Shader.Find("Custom/FishTextured");
        Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
        if (sh == null || renderers.Length == 0) return;    // 缺 shader 时保留原材质（仅无摆尾，不影响显示）

        LoadColoredTextures();

        var pairs = new List<(Renderer r, Mesh m)>();
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        foreach (Renderer r in renderers)
        {
            Mesh m = null;
            if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
            else { MeshFilter mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
            if (m == null) continue;
            pairs.Add((r, m));
            Bounds b = m.bounds;
            minX = Mathf.Min(minX, b.min.x);
            maxX = Mathf.Max(maxX, b.max.x);
        }
        if (pairs.Count == 0) return;
        float len = Mathf.Max(0.0001f, maxX - minX);

        foreach (var pair in pairs)
        {
            if (!_swingMeshCache.TryGetValue(pair.m, out Mesh clone))
            {
                clone = Object.Instantiate(pair.m);
                clone.name = pair.m.name + "_Swing";
                Vector3[] verts = clone.vertices;
                Color[] cols = new Color[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                    cols[i] = new Color(Mathf.Clamp01((maxX - verts[i].x) / len), 0f, 0f, 1f);
                clone.colors = cols;
                _swingMeshCache[pair.m] = clone;
            }

            if (pair.r is SkinnedMeshRenderer smr2) smr2.sharedMesh = clone;
            else
            {
                MeshFilter mf2 = pair.r.GetComponent<MeshFilter>();
                if (mf2 != null) mf2.sharedMesh = clone;
            }
        }

        // 按贴图分配材质：单材质高模直接用 baseTex；天竺鲷按导入说明分区指定（每部件一个 FishTextured 实例，共享缓存）；缺失时回退到原材质贴图
        var used = new HashSet<Material>();
        foreach (Renderer r in renderers)
        {
            Texture2D tex = baseTex != null ? baseTex : TexForPart(r.gameObject.name);
            Material mainMat = PartMaterial(tex, sh);
            if (mainMat == null && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null)
                mainMat = PartMaterial(r.sharedMaterial.mainTexture as Texture2D, sh);
            if (mainMat == null) continue;                  // 无贴图可用的部件保留原材质
            used.Add(mainMat);

            Material[] old = r.sharedMaterials;
            Material[] mats = new Material[old.Length];
            for (int s = 0; s < mats.Length; s++)
            {
                // 身体第二槽位是眼睛（虹膜/瞳孔贴图）；其余槽位跟随部件主贴图
                Texture2D slotTex = s == 0 ? tex : (tex == _cBodyTex ? _cEyeTex : tex);
                Material slotMat = s == 0 ? mainMat : PartMaterial(slotTex, sh);
                if (slotMat != null) used.Add(slotMat);
                mats[s] = slotMat != null ? slotMat : mainMat;
            }
            r.sharedMaterials = mats;
        }

        // 摆尾参数只写入本次实际用到的材质，避免覆盖其它物种已生成材质的参数
        foreach (Material m in used)
        {
            m.SetFloat("_SwimAmp", amp);
            m.SetFloat("_SwimSpeed", speed);
            m.SetFloat("_SwingAxis", swingZ ? 1f : 0f);
        }
    }

    // 合并导入模型所有部件网格的包围盒（部件可能带旋转/位移：按 mesh.bounds 的 8 个角变换到根节点本地空间再取并集）
    private static Bounds LocalMeshBounds(GameObject root)
    {
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        bool has = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh;
            if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                mesh = mf != null ? mf.sharedMesh : null;
            }
            if (mesh == null) continue;
            Bounds mb = mesh.bounds;
            Vector3 ext = mb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + new Vector3((i & 1) == 0 ? -ext.x : ext.x,
                                                         (i & 2) == 0 ? -ext.y : ext.y,
                                                         (i & 4) == 0 ? -ext.z : ext.z);
                Vector3 local = root.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!has) { b = new Bounds(local, Vector3.zero); has = true; }
                else b.Encapsulate(local);
            }
        }
        return b;
    }

    private List<Vector3> CollectCache(string key)
    {
        if (_spotCache.TryGetValue(key, out List<Vector3> list))
        {
            return list;
        }

        list = new List<Vector3>();
        string rootName = key == "海葵" || key == "海草" ? "海草群" : "礁石群";
        GameObject root = GameObject.Find(rootName);
        if (root != null)
        {
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith(key)) continue;
                foreach (Transform g in child)
                {
                    list.Add(g.position);
                }
            }
        }
        _spotCache[key] = list;
        return list;
    }

    // ---------- 网格构建 ----------

    /// <summary>通用鱼身：环状纺锤形网格 + 按物种定制的尾鳍（叉/圆扇）与背鳍（单/圆/双/带）+ 可选臀鳍 + 两侧黑色眼睛几何体。
    /// uv.x 头0→尾1 供 shader 摆尾，v 腹0→背1，顶点色由 colorFn(u, v) 绘制。</summary>
    private static Mesh BuildFish(float len, float hgt, float wid, float tailLen, float tailHgt, bool dorsalFin,
                                  System.Func<float, float, Color> colorFn,
                                  TailShape tailShape = TailShape.Forked,
                                  DorsalShape dorsalShape = DorsalShape.Single,
                                  bool analFin = false,
                                  float eyeScale = 1f,
                                  float forkDepth = 0.65f,
                                  float dorsalScale = 1f,
                                  float finFade = 0.55f)
    {
        const int rings = 32;   // 轴向环数（高模：轮廓平滑，细节可见）
        const int segs = 26;    // 每环段数

        List<Vector3> verts = new List<Vector3>(rings * (segs + 1) + 20);
        List<Vector2> uvs = new List<Vector2>(rings * (segs + 1) + 20);
        List<Color> cols = new List<Color>(rings * (segs + 1) + 20);
        List<int> tris = new List<int>(rings * segs * 6 + 48);

        // 纺锤形半径轮廓：t∈[-1,1]，-1 尾柄、+1 吻尖，最宽处偏前
        System.Func<float, float> profile = t =>
        {
            if (t >= 0f) return Mathf.Pow(Mathf.Cos(Mathf.Min(t, 1f) * 1.35f), 0.8f);  // 头部收窄成吻
            float s = -t;
            return 1f - 0.84f * s * s;                                                 // 平缓收窄入细尾柄至 16%
        };
        // 尾半段侧向额外压扁：真鱼尾柄是左右薄的竖叶片，而非圆管
        System.Func<float, float> widthTaper = t => t < 0f ? 1f - 0.6f * t * t : 1f;

        // 身体环
        for (int i = 0; i < rings; i++)
        {
            float tz = i / (float)(rings - 1);          // 0 尾 → 1 头
            float t = tz * 2f - 1f;
            float z = Mathf.Lerp(-len, len, tz);
            float r = profile(t);
            float wr = r * widthTaper(t);               // 宽度方向额外收扁
            for (int s = 0; s <= segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2f;
                float y = Mathf.Sin(a) * hgt * r;
                if (y < 0f) y *= 0.82f;                 // 腹部略平
                else y *= 1f + 0.06f * Mathf.Clamp01(t * 1.2f + 0.25f);   // 背部前段微微隆起
                float x = Mathf.Cos(a) * wid * wr;
                float u = 1f - tz;                      // 头0 → 尾1
                float v = Mathf.Clamp01(y / (hgt * 1.9f) + 0.5f);  // 腹0 → 背1
                verts.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(u, v));
                cols.Add(colorFn(u, v));
            }
        }
        for (int i = 0; i < rings - 1; i++)
        {
            for (int s = 0; s < segs; s++)
            {
                int a = i * (segs + 1) + s;
                int b = a + segs + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

        // 吻尖封口
        int nose = verts.Count;
        verts.Add(new Vector3(0f, 0f, len * 1.04f));
        uvs.Add(new Vector2(0f, 0.5f));
        cols.Add(colorFn(0f, 0.5f));
        int headRing = (rings - 1) * (segs + 1);
        for (int s = 0; s < segs; s++)
        {
            tris.Add(nose); tris.Add(headRing + s + 1); tris.Add(headRing + s);
        }

        // 尾柄封口
        int tailCap = verts.Count;
        verts.Add(new Vector3(0f, 0f, -len));
        uvs.Add(new Vector2(1f, 0.5f));
        cols.Add(colorFn(1f, 0.5f));
        for (int s = 0; s < segs; s++)
        {
            tris.Add(tailCap); tris.Add(s + 1); tris.Add(s);
        }

        // 尾鳍：身体网格的直接延伸——鳍面顶点直接焊接在尾柄末环上，同一张曲面、同一套摆动相位，
        // 结构上不存在接缝；纵向网格线即鳍条：根部汇聚密集、向外展开稀疏，
        // 全部鳍条尖端落在同一条平滑余弦弧上（中间短、上下长），外缘连续光滑无尖刺
        const int finRings = 12;
        float pedPr = profile(-1f);                                      // 尾柄末端轮廓（与身体末环完全一致）
        int finV0 = verts.Count;
        for (int j = 1; j <= finRings; j++)
        {
            float t = j / (float)finRings;
            float tE = t * t;                                            // 缓入：环距根部密、向外缘渐疏
            for (int s = 0; s <= segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2f;
                float ny = Mathf.Sin(a);
                float ca = Mathf.Cos(a);
                float lenK = tailShape == TailShape.Forked
                    ? (1f - forkDepth) + forkDepth * Mathf.Pow(Mathf.Abs(ny), 1.7f)   // 叉尾：中间短、上下叶长
                    : 1f - 0.2f * Mathf.Cos(ny * 1.5708f);               // 圆尾：中间鳍条短、上下长
                float y0 = ny * hgt * pedPr;                             // 根点：与身体末环重合
                if (y0 < 0f) y0 *= 0.82f;
                float x0 = ca * wid * pedPr * widthTaper(-1f);
                float y1 = ny * tailHgt * (ny < 0f ? 0.85f : 1f);        // 梢点：落在尾鳍外缘弧上
                float x = x0 * (1f - tE) * (1f - tE);                    // 宽度沿程收拢成薄刃
                float y = Mathf.Lerp(y0, y1, tE);
                float z = -len - tailLen * lenK * tE;                    // 各纵线按尾形弧长推进
                float u = 1f + 0.08f * tE;                               // 相位自身体末环(=1)连续延伸
                float v = Mathf.Clamp01(y / (hgt * 1.9f) + 0.5f);
                Color c = Color.Lerp(colorFn(0.97f, v), White, finFade * tE);   // 根部体色 → 梢部鳍色连续渐变
                if ((s & 1) == 1) c *= Mathf.Lerp(1f, 0.75f, tE);        // 鳍条明暗条纹向梢部渐显
                verts.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(u, v));
                cols.Add(c);
            }
        }
        // 环间焊接：首环直接复用身体末环顶点（索引 0..segs，z=-len），法线与摆动全连续
        for (int j = 0; j < finRings; j++)
        {
            int je = finV0 + j * (segs + 1);
            for (int s = 0; s < segs; s++)
            {
                int a0 = s;                                              // 身体末环
                int b0 = je + s;
                int pa0 = j == 0 ? a0 : je - (segs + 1) + s;             // 上一环
                tris.Add(pa0); tris.Add(pa0 + 1); tris.Add(b0);
                tris.Add(pa0 + 1); tris.Add(b0 + 1); tris.Add(b0);
            }
        }

        // 背鳍：按物种形态（单一棘鳍 / 圆弧 / 双背鳍 / 长带状），鳍条扇面 + 膜色
        if (dorsalFin)
        {
            float dz0, dz1, dH;
            System.Func<float, float> hProf;
            switch (dorsalShape)
            {
                case DorsalShape.Rounded:
                    dz0 = len * 0.34f; dz1 = -len * 0.4f; dH = hgt * 0.48f;
                    hProf = t => Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI), 0.55f);
                    break;
                case DorsalShape.Twin:
                    dz0 = len * 0.4f; dz1 = -len * 0.42f; dH = hgt * 0.62f;
                    hProf = t =>
                    {
                        if (t < 0.4f) return Mathf.Pow(Mathf.Sin(t / 0.4f * Mathf.PI), 0.7f);          // 圆弧峰
                        if (t > 0.6f) return Mathf.Pow(Mathf.Sin((t - 0.6f) / 0.4f * Mathf.PI), 0.7f) * 0.85f;
                        return 0f;
                    };
                    break;
                case DorsalShape.Ribbon:
                    dz0 = len * 0.36f; dz1 = -len * 0.58f; dH = hgt * 0.3f;
                    hProf = t => 0.65f + 0.35f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    break;
                default:  // Single
                    dz0 = len * 0.3f; dz1 = -len * 0.38f; dH = hgt * 0.5f;
                    hProf = t => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    break;
            }
            dH *= dorsalScale;   // 物种级背鳍高度缩放（天竺鲷小背鳍）

            const int dRays = 17;
            int dv0 = verts.Count;
            for (int k = 0; k < dRays; k++)
            {
                float tk = k / (float)(dRays - 1);
                float z = Mathf.Lerp(dz0, dz1, tk);
                float uAt = 1f - (z + len) / (2f * len);
                float baseY = hgt * profile(z / len) * 0.92f;
                Color finC = Color.Lerp(colorFn(uAt, 1f), White, 0.5f);   // 背鳍膜偏白
                if ((k & 1) == 1) finC *= 0.85f;
                verts.Add(new Vector3(0f, baseY, z));
                uvs.Add(new Vector2(uAt, 1f));
                cols.Add(finC);
                verts.Add(new Vector3(0f, baseY + dH * hProf(tk), z));
                uvs.Add(new Vector2(uAt, 1f));
                cols.Add(finC);
            }
            for (int k = 0; k < dRays - 1; k++)
            {
                int b0 = dv0 + k * 2;
                tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 1);
                tris.Add(b0 + 1); tris.Add(b0 + 2); tris.Add(b0 + 3);
            }
        }

        // 臀鳍：腹面带状鳍（裂唇鱼上下缘连续鳍边）
        if (analFin)
        {
            const int aRays = 6;
            float az0 = len * 0.12f, az1 = -len * 0.5f, aH = hgt * 0.24f;
            int av0 = verts.Count;
            for (int k = 0; k < aRays; k++)
            {
                float tk = k / (float)(aRays - 1);
                float z = Mathf.Lerp(az0, az1, tk);
                float uAt = 1f - (z + len) / (2f * len);
                float baseY = -hgt * profile(z / len) * 0.75f;
                Color finC = Color.Lerp(colorFn(uAt, 0f), White, 0.5f);   // 臀鳍膜偏白
                verts.Add(new Vector3(0f, baseY, z));
                uvs.Add(new Vector2(uAt, 0f));
                cols.Add(finC);
                verts.Add(new Vector3(0f, baseY - aH, z));
                uvs.Add(new Vector2(uAt, 0f));
                cols.Add(finC);
            }
            for (int k = 0; k < aRays - 1; k++)
            {
                int b0 = av0 + k * 2;
                tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 1);
                tris.Add(b0 + 1); tris.Add(b0 + 2); tris.Add(b0 + 3);
            }
        }

        // 两侧胸鳍：小片半透明浅色胸鳍，位于头后侧面向后下方展开
        {
            const int pRays = 3;
            float pz = len * 0.18f;
            float uP = 1f - (pz + len) / (2f * len);
            float prP = profile(pz / len);
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? 1f : -1f;
                int pv0 = verts.Count;
                for (int k = 0; k < pRays; k++)
                {
                    float tk = k / (float)(pRays - 1);
                    verts.Add(new Vector3(sx * wid * prP * 0.85f, Mathf.Lerp(hgt * prP * 0.3f, -hgt * prP * 0.15f, tk), pz));
                    uvs.Add(new Vector2(uP, 0.45f));
                    cols.Add(colorFn(uP, 0.45f));
                    verts.Add(new Vector3(sx * (wid * prP + hgt * 0.5f), -hgt * 0.45f - tk * hgt * 0.1f, pz - len * (0.16f + tk * 0.05f)));
                    uvs.Add(new Vector2(uP, 0.3f));
                    cols.Add(Color.Lerp(colorFn(uP, 0.35f), White, 0.75f));   // 浅色半透明鳍膜
                }
                tris.Add(pv0); tris.Add(pv0 + 2); tris.Add(pv0 + 1);
                tris.Add(pv0 + 2); tris.Add(pv0 + 3); tris.Add(pv0 + 1);
                tris.Add(pv0 + 2); tris.Add(pv0 + 4); tris.Add(pv0 + 3);
                tris.Add(pv0 + 4); tris.Add(pv0 + 5); tris.Add(pv0 + 3);
            }
        }

        // 两侧眼睛：体积感眼球（高密度小球体，圆润无棱角），半嵌入头部微凸出，位于头侧偏前
        float eyeR = Mathf.Max(hgt * 0.18f * eyeScale, 0.006f);
        float eyeZ = len * 0.66f;
        float eyeY = hgt * 0.28f;
        const int eRings = 12, eSegs = 20;
        for (int side = 0; side < 2; side++)
        {
            float sx = side == 0 ? 1f : -1f;
            float bodyR = wid * profile(eyeZ / len) * 0.82f;             // 该处头部半宽，球心嵌在表面
            Vector3 c = new Vector3(sx * bodyR, eyeY, eyeZ);
            int ev0 = verts.Count;
            for (int r = 0; r <= eRings; r++)
            {
                float phi = r / (float)eRings * Mathf.PI;
                for (int s = 0; s <= eSegs; s++)
                {
                    float th = s / (float)eSegs * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    verts.Add(c + n * eyeR);
                    uvs.Add(new Vector2(0.08f, 0.62f));
                    float outDot = n.x * sx;
                    cols.Add(outDot > 0.45f ? Black : new Color(0.85f, 0.85f, 0.85f));   // 黑瞳 + 银白巩膜环
                }
            }
            for (int r = 0; r < eRings; r++)
            {
                for (int s = 0; s < eSegs; s++)
                {
                    int a = ev0 + r * (eSegs + 1) + s;
                    int b = a + eSegs + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                }
            }
        }

        Mesh m = new Mesh { name = "Fish" };
        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.colors = cols.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>海马：竖直S形身体 + 管状吻部 + 卷尾，头朝上。</summary>
    private static Mesh BuildSeahorse(float len, System.Func<float, float, Color> colorFn)
    {
        Mesh src = PrimitiveMeshes.UnitSphere;
        Vector3[] sv = src.vertices;
        Vector2[] suv = src.uv;
        int[] st = src.triangles;

        List<Vector3> verts = new List<Vector3>(sv.Length + 16);
        List<Vector2> uvs = new List<Vector2>(sv.Length + 16);
        List<Color> cols = new List<Color>(sv.Length + 16);
        List<int> tris = new List<int>(st.Length + 12);

        for (int i = 0; i < sv.Length; i++)
        {
            Vector3 v = sv[i];
            float yn = v.y + 0.5f;  // 0 底 1 顶
            // S 形弯曲：身体沿 y 拉长，z 随 y 偏移
            Vector3 body = new Vector3(v.x * 0.16f, v.y * len * 0.55f, v.z * 0.2f + Mathf.Sin(yn * 3.6f) * 0.05f - len * 0.08f);
            float u = Mathf.Clamp01(1f - yn);          // 头(顶)=0 → 尾=1
            verts.Add(body);
            uvs.Add(new Vector2(u, suv[i].y));
            cols.Add(colorFn(u, suv[i].y));
        }
        tris.AddRange(st);

        // 吻部：小管从头顶向前
        int sIdx = verts.Count;
        Vector3 s0 = new Vector3(0f, len * 0.42f, len * 0.05f);
        Vector3 s1 = new Vector3(0f, len * 0.52f, len * 0.22f);
        AddTube(verts, uvs, cols, tris, s0, s1, 0.045f, 0.03f, colorFn);

        // 卷尾：底部小钩
        int cIdx = verts.Count;
        Vector3 c0 = new Vector3(0f, -len * 0.52f, -len * 0.1f);
        Vector3 c1 = new Vector3(0f, -len * 0.62f, -len * 0.02f);
        AddTube(verts, uvs, cols, tris, c0, c1, 0.05f, 0.015f, colorFn);

        Mesh m = new Mesh { name = "Seahorse" };
        m.vertices = verts.ToArray();
        m.uv = uvs.ToArray();
        m.colors = cols.ToArray();
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static void AddDoubleSidedTri(List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris,
                                          Vector3 a, Vector3 b, Vector3 c,
                                          System.Func<float, Vector2> uvFn, System.Func<float, float, Color> colorFn)
    {
        int s = verts.Count;
        verts.Add(a); uvs.Add(uvFn(0f)); cols.Add(colorFn(uvFn(0f).x, uvFn(0f).y));
        verts.Add(b); uvs.Add(uvFn(1f)); cols.Add(colorFn(uvFn(1f).x, uvFn(1f).y));
        verts.Add(c); uvs.Add(uvFn(0.5f)); cols.Add(colorFn(uvFn(0.5f).x, uvFn(0.5f).y));
        tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
        tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
    }

    private static void AddTube(List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris,
                                Vector3 a, Vector3 b, float r0, float r1, System.Func<float, float, Color> colorFn)
    {
        const int sides = 6;
        Vector3 axis = (b - a).normalized;
        Vector3 side = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(axis, side).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;

        int start = verts.Count;
        for (int s = 0; s <= sides; s++)
        {
            float ang = s / (float)sides * Mathf.PI * 2f;
            Vector3 ringDir = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
            float t = (float)s / sides;
            verts.Add(a + ringDir * r0);
            uvs.Add(new Vector2(0.05f, 0.5f + ringDir.y * 0.4f));
            cols.Add(colorFn(0.05f, 0.5f + ringDir.y * 0.4f));
            verts.Add(b + ringDir * r1);
            uvs.Add(new Vector2(0.15f, 0.5f + ringDir.y * 0.4f));
            cols.Add(colorFn(0.15f, 0.5f + ringDir.y * 0.4f));
        }
        for (int s = 0; s < sides; s++)
        {
            int i0 = start + s * 2;
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            tris.Add(i0 + 1); tris.Add(i0 + 3); tris.Add(i0 + 2);
        }
    }

    // ---------- 物种花纹（顶点色，u: 头0→尾1，v: 腹0→背1） ----------

    private static readonly Color White = new Color(0.98f, 0.98f, 0.95f);
    private static readonly Color Black = new Color(0.07f, 0.06f, 0.06f);

    private static Color BlueDamsel(float u, float v)
    {
        // 参考图：电光宝蓝，头部略深，叉尾
        Color c = Color.Lerp(new Color(0.1f, 0.2f, 0.88f), new Color(0.13f, 0.28f, 1f), v);
        if (u < 0.28f) c *= 0.82f;
        return c;
    }

    private static Color Sardine(float u, float v)
    {
        // 参考图：青灰蓝背 + 银白腹，背部一列深色斑点
        Color back = new Color(0.32f, 0.42f, 0.48f);
        Color belly = new Color(0.93f, 0.95f, 0.97f);
        Color c = Color.Lerp(belly, back, Mathf.SmoothStep(0.4f, 0.85f, v));
        if (v > 0.72f && Hash(Mathf.Floor(u * 26f), Mathf.Floor(v * 8f)) < 0.4f) c *= 0.55f;
        if (u > 0.92f) c = Color.Lerp(c, new Color(0.92f, 0.86f, 0.55f), 0.45f);  // 沙丁鱼淡黄尾鳍
        return c;
    }

    private static Color GizzardShad(float u, float v)
    {
        // 参考图（Konosirus punctatus）：银色体侧 + 浅银蓝背 + 鳃盖后明显黑色圆斑 + 体侧淡黄线
        Color back = new Color(0.5f, 0.56f, 0.6f);
        Color belly = new Color(0.95f, 0.96f, 0.97f);
        Color c = Color.Lerp(belly, back, Mathf.SmoothStep(0.55f, 1f, v));
        if (Mathf.Abs(v - 0.42f) < 0.06f && u > 0.25f && u < 0.92f)
        {
            c = Color.Lerp(c, new Color(0.95f, 0.82f, 0.4f), 0.4f);            // 体侧淡黄线
        }
        float du = (u - 0.17f) * 1.15f;
        float dv = v - 0.52f;
        if (du * du + dv * dv < 0.006f) c = new Color(0.06f, 0.05f, 0.05f);    // 鳃盖后黑圆斑（标志性）
        if (u > 0.92f) c = Color.Lerp(c, new Color(0.92f, 0.88f, 0.6f), 0.45f); // 淡黄尾鳍
        return c;
    }

    private static Color Clownfish(float u, float v)
    {
        // 参考图：深橙体色 + 三条窄白带黑边，鳍缘黑边
        Color c = new Color(0.95f, 0.38f, 0.05f);
        float[] bands = { 0.16f, 0.5f, 0.82f };
        foreach (float b in bands)
        {
            float d = Mathf.Abs(u - b);
            if (d < 0.05f) c = White;
            else if (d < 0.075f) c = Black;
        }
        if (u < 0.03f) c = new Color(0.8f, 0.26f, 0.04f);
        if (u > 0.985f) c *= 0.35f;                                               // 尾鳍末端黑缘
        return c;
    }

    private static Color Cardinalfish(float u, float v)
    {
        // 淡红粉体色（略提饱和度以穿透水下雾）：粉白腹 → 淡红体侧 → 红褐背，圆尾红褐
        Color c;
        if (v < 0.35f) c = new Color(0.92f, 0.58f, 0.5f);                  // 粉白腹部
        else c = Color.Lerp(new Color(0.82f, 0.36f, 0.26f), new Color(0.5f, 0.2f, 0.13f), Mathf.SmoothStep(0.35f, 0.9f, v));
        if (u < 0.25f) c = Color.Lerp(new Color(0.88f, 0.46f, 0.3f), c, u / 0.25f);   // 头部略深
        if (u > 0.9f) c = new Color(0.48f, 0.2f, 0.14f);                   // 红褐圆尾鳍
        return c;
    }

    private static Color CleanerWrasse(float u, float v)
    {
        // 参考图：淡紫蓝体色，黑纵带从吻贯穿到尾
        Color c = new Color(0.62f, 0.63f, 0.92f);
        c = Color.Lerp(c * 0.75f, c, v);
        if (Mathf.Abs(v - 0.52f) < 0.08f) c = new Color(0.06f, 0.05f, 0.1f);  // 贯穿头尾的黑纵带
        if (u > 0.93f) c = new Color(0.35f, 0.4f, 0.75f);
        return c;
    }

    private static Color Wrasse(float u, float v)
    {
        // 参考图：light yellow-green 体色 + 清晰分离的水平条纹（非波浪噪声）+ 尖吻
        Color c = Color.Lerp(new Color(0.84f, 0.89f, 0.62f), new Color(0.72f, 0.8f, 0.5f), v);
        float[] stripes = { 0.62f, 0.45f, 0.28f };
        foreach (float s in stripes)
        {
            if (Mathf.Abs(v - s) < 0.035f && u > 0.05f && u < 0.95f)
                c = new Color(0.33f, 0.43f, 0.29f);                        // 深绿窄条纹，硬边缘
        }
        if (u < 0.08f) c = new Color(0.72f, 0.78f, 0.54f);                 // 尖吻部略浅
        return c;
    }

    private static Color Goby(float u, float v)
    {
        // 参考图：沙色半透体 + 红褐色斑列
        Color c = Color.Lerp(new Color(0.88f, 0.83f, 0.74f), new Color(0.72f, 0.66f, 0.56f), v);
        float cx = Mathf.Floor(u * 9f);
        float cy = Mathf.Floor(v * 4.5f);
        if (Hash(cx, cy) < 0.55f)
        {
            Vector2 frac = new Vector2(u * 9f - cx - 0.5f, (v * 4.5f - cy - 0.5f) * 0.8f);
            if (frac.sqrMagnitude < 0.09f && v > 0.15f && v < 0.8f) c = new Color(0.6f, 0.28f, 0.2f);
        }
        return c;
    }

    private static Color Seahorse(float u, float v)
    {
        // 参考图：黄褐体色 + 浅色斑点 + 腹部偏黄，环节与卷尾
        Color c = new Color(0.78f, 0.6f, 0.28f);
        c = Color.Lerp(new Color(0.85f, 0.72f, 0.35f), c, v);
        float ring = Mathf.Repeat(u * 13f, 1f);
        if (ring < 0.15f) c *= 0.8f;                                       // 环节
        if (Hash(Mathf.Floor(u * 30f), Mathf.Floor(v * 12f)) > 0.72f)
        {
            c = Color.Lerp(c, White, 0.45f);                               // 浅色斑点
        }
        if (u < 0.04f) c = new Color(0.9f, 0.82f, 0.55f);                  // 吻部
        if (u > 0.93f) c = new Color(0.6f, 0.46f, 0.2f);                   // 卷尾
        return c;
    }

    private static float Hash(float x, float y)
    {
        return Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);
    }

    // ---------- 工具 ----------

    /// <summary>创建全新的命名子分组。不复用已有分组：Play 模式下旧分组可能已被 Destroy 标记（帧末销毁），挂进去的新鱼会被连带销毁。</summary>
    private Transform EnsureGroup(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private static Vector2 RandomInCircle(System.Random rng, float radius)
    {
        float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = radius * Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
    }
}
