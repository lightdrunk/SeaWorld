using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导入模型一次性引导（幂等）：蒙皮高模（霓虹雀鲷 / 三斑海马）的资源配置。
/// MCP 桥接与包内 Setup 脚本依赖固定目录结构（Assets/NeonDamselfish），与本项目的
/// Assets/Model/&lt;包名&gt;/ 嵌套结构不符，故由本引导完成等效配置：
/// 1. 法线贴图导入类型设为 NormalMap；
/// 2. 为海马构建 URP/Lit 图集材质（包内未提供 .mat）；
/// 3. 将两个 FBX 的待机动画 clip 设为 Loop Time（否则播一遍就停）。
/// </summary>
[InitializeOnLoad]
public static class FishModelBootstrap
{
    private const string DamselFbx = "Assets/Model/NeonDamselfish_URP/Assets/NeonDamselfish/NeonDamselfish_Rigged.fbx";
    private const string SeahorseFbx = "Assets/Model/Trimaculatus_URP/Assets/Trimaculatus/Hippocampus_trimaculatus_Rigged.fbx";
    private const string SeahorseTexDir = "Assets/Model/Trimaculatus_URP/Assets/Trimaculatus/Textures";
    private const string SeahorseNormal = SeahorseTexDir + "/HT_Normal_OpenGL_2K.png";
    private const string SeahorseMatDir = "Assets/Model/Trimaculatus_URP/Assets/Trimaculatus/Materials";
    private const string SeahorseMat = SeahorseMatDir + "/HT_Atlas_URP.mat";

    static FishModelBootstrap()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Tools/FishModelBootstrap/Run Ensure")]
    private static void RunEnsure()
    {
        Ensure();
    }

    [MenuItem("Tools/FishModelBootstrap/Diagnose")]
    private static void Diagnose()
    {
        string root = "Assets/Model/Trimaculatus_URP/Assets/Trimaculatus";
        Debug.Log($"[诊断] IsValidFolder: root={AssetDatabase.IsValidFolder(root)} textures={AssetDatabase.IsValidFolder(root + "/Textures")}");
        string[] all = AssetDatabase.FindAssets("", new[] { root });
        Debug.Log($"[诊断] root 下收录资产数: {all.Length}");

        string texPath = root + "/Textures/HT_BaseColor_2K.png";
        string guid = AssetDatabase.AssetPathToGUID(texPath);
        Debug.Log($"[诊断] {texPath}\n  GUID={guid}\n  GUID实际指向={AssetDatabase.GUIDToAssetPath(guid)}");
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(texPath))
        {
            Debug.Log($"  LoadAll: type={o.GetType().FullName} name={o.name}");
        }
        Texture2D direct = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        Object generic = AssetDatabase.LoadMainAssetAtPath(texPath);
        Debug.Log($"[诊断] LoadAssetAtPath<Texture2D>={direct != null}  LoadMainAssetAtPath={generic != null} type={(generic != null ? generic.GetType().FullName : "null")}");
    }

    private static void Ensure()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);   // 强制全量重扫（新拷入的模型包可能未被资产库收录）
        string damselTex = "Assets/Model/NeonDamselfish_URP/Assets/NeonDamselfish/Textures";
        // 包内 PNG 的 meta 被导出工具写成了 Cubemap 形状（导入为 Cubemap 而非 Texture2D）——统一修正
        EnsureTexture2D(damselTex, "PC_BaseColor_2K");
        EnsureTexture2D(damselTex, "PC_URP_MetallicSmoothness_2K");
        EnsureNormalMap(damselTex, "PC_Normal_2K");
        EnsureTexture2D(SeahorseTexDir, "HT_BaseColor_2K");
        EnsureTexture2D(SeahorseTexDir, "HT_URP_MetallicSmoothness_2K");
        EnsureNormalMap(SeahorseTexDir, "HT_Normal_OpenGL_2K");
        EnsureSeahorseMaterial();
        EnsureClipLoop(DamselFbx, "IdleSwim_2s");
        EnsureClipLoop(SeahorseFbx, "Idle_FinPropulsion_2s");
    }

    // 确保贴图为 Texture2D 形状（包导出时 textureShape 误写为 Cube 会导致 LoadAssetAtPath<Texture2D> 返回 null）
    private static Texture2D EnsureTexture2D(string folder, string name)
    {
        Texture2D tex = FindTexture(folder, name);
        string path = tex != null ? AssetDatabase.GetAssetPath(tex) : $"{folder}/{name}.png";
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return tex;

        if (imp.textureShape != TextureImporterShape.Texture2D)
        {
            imp.textureShape = TextureImporterShape.Texture2D;
            imp.SaveAndReimport();
            Debug.Log("[FishModelBootstrap] 贴图形状已修正为 Texture2D: " + path);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return tex;
    }

    // 加载贴图：直路径 → 强制同步重导入 → 按名搜索兜底（新拷入包的路径映射偶发未入库）
    private static Texture2D FindTexture(string folder, string name)
    {
        string path = $"{folder}/{name}.png";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;

        if (System.IO.File.Exists(path))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
        }

        foreach (string g in AssetDatabase.FindAssets($"{name} t:Texture2D", new[] { "Assets/Model" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name)
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
        }
        return null;
    }

    private static void EnsureNormalMap(string folder, string name)
    {
        Texture2D tex = EnsureTexture2D(folder, name);
        string path = tex != null ? AssetDatabase.GetAssetPath(tex) : $"{folder}/{name}.png";
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null || imp.textureType == TextureImporterType.NormalMap)
        {
            return;
        }
        imp.textureType = TextureImporterType.NormalMap;
        imp.SaveAndReimport();
        Debug.Log("[FishModelBootstrap] 法线贴图导入类型已设置: " + path);
    }

    private static void EnsureSeahorseMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(SeahorseMat) != null)
        {
            return;
        }

        // URP/Lit 图集材质（README 规格）：BaseMap(sRGB) + 法线(强度0.6) + MetallicSmoothness 打包图，
        // Smoothness 来源 = Metallic Alpha，Smoothness = 1，金属度 = 0
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("[FishModelBootstrap] 找不到 URP/Lit shader，跳过海马材质生成");
            return;
        }

        Material mat = new Material(lit);
        Texture2D baseMap = EnsureTexture2D(SeahorseTexDir, "HT_BaseColor_2K");
        Texture2D normalMap = EnsureTexture2D(SeahorseTexDir, "HT_Normal_OpenGL_2K");
        Texture2D msMap = EnsureTexture2D(SeahorseTexDir, "HT_URP_MetallicSmoothness_2K");
        if (baseMap == null || normalMap == null || msMap == null)
        {
            Debug.LogError($"[FishModelBootstrap] 海马贴图加载失败 BaseMap={baseMap != null} Normal={normalMap != null} MS={msMap != null}");
            return;
        }
        mat.SetTexture("_BaseMap", baseMap);
        mat.SetTexture("_BumpMap", normalMap);
        mat.SetTexture("_MetallicGlossMap", msMap);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 1f);
        mat.SetFloat("_BumpScale", 0.6f);
        mat.SetFloat("_SmoothnessTextureChannel", 1f);   // 1 = Metallic Alpha

        System.IO.Directory.CreateDirectory(SeahorseMatDir);
        AssetDatabase.CreateAsset(mat, SeahorseMat);
        Debug.Log("[FishModelBootstrap] 已生成海马 URP/Lit 图集材质: " + SeahorseMat);
    }

    private static void EnsureClipLoop(string fbxPath, string clipName)
    {
        ModelImporter mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null)
        {
            return;
        }

        List<ModelImporterClipAnimation> list = mi.clipAnimations != null && mi.clipAnimations.Length > 0
            ? new List<ModelImporterClipAnimation>(mi.clipAnimations)
            : new List<ModelImporterClipAnimation>(mi.defaultClipAnimations);
        if (list.Count == 0)
        {
            return;
        }

        bool changed = false;
        foreach (ModelImporterClipAnimation cs in list)
        {
            if ((list.Count == 1 || cs.name == clipName) && !cs.loopTime)
            {
                cs.loopTime = true;
                changed = true;
            }
        }

        if (changed)
        {
            mi.clipAnimations = list.ToArray();
            mi.SaveAndReimport();
            Debug.Log($"[FishModelBootstrap] 动画 clip 已设为循环: {fbxPath} / {clipName}");
        }
    }
}
