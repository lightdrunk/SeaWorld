#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Select the supplied FBX, then run Tools > Wrasse > Set Up FBX Materials.
// Uses Standard in Built-in RP and Lit in URP. No runtime package is required.
public static class WrasseFbxMaterials
{
    [MenuItem("Tools/Wrasse/Set Up FBX Materials")]
    public static void SetUp()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!path.EndsWith("BluestreakCleanerWrasse_Adult_Unity.fbx", System.StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Wrasse FBX", "先在 Project 窗口中选择 BluestreakCleanerWrasse_Adult_Unity.fbx。", "OK");
            return;
        }
        string folder = Path.GetDirectoryName(path).Replace('\\', '/');
        string materialFolder = folder + "/Materials";
        if (!AssetDatabase.IsValidFolder(materialFolder)) AssetDatabase.CreateFolder(folder, "Materials");
        string tex = folder + "/Textures/";
        ImportTexture(tex + "Wrasse_BaseColor_2K.png", true, false);
        ImportTexture(tex + "Wrasse_Normal_OpenGL_2K.png", false, true);
        ImportTexture(tex + "Wrasse_Unity_MetallicSmoothness_2K.png", false, false);
        Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Wrasse_BaseColor_2K.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Wrasse_Normal_OpenGL_2K.png");
        Texture2D metalSmooth = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Wrasse_Unity_MetallicSmoothness_2K.png");
        if (color == null || normal == null || metalSmooth == null) throw new System.IO.FileNotFoundException("请将 Textures 文件夹与 FBX 一同导入。");
        bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
            && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) throw new System.InvalidOperationException("此脚本支持 Built-in Standard 或 URP Lit。");
        string[] names = { "AdultSkin", "FinMembraneAndRays", "Eye" };
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
        foreach (string name in names)
        {
            string materialPath = materialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader;
            material.SetTexture(urp ? "_BaseMap" : "_MainTex", color);
            material.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.55f);
            material.SetTexture("_MetallicGlossMap", metalSmooth);
            material.SetFloat("_Metallic", 1f); // The map's red channel is zero.
            material.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 1f);
            if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword(urp ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (urp && material.HasProperty("_Cull")) material.SetFloat("_Cull", name == "FinMembraneAndRays" ? 0f : 2f);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        }
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.globalScale = 1f;
        // Each FBX take is a separate clip. The source takes are exported at 30 fps.
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            if (clip.takeName.Contains("Idle_Swim")) clip.name = "Idle_Swim";
            if (clip.takeName.Contains("Jaw_Demonstration")) clip.name = "Jaw_Demonstration";
            clip.loopTime = true;
        }
        if (clips.Length > 0) importer.clipAnimations = clips;
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        Debug.Log("Wrasse FBX: 已设置 PBR 材质、通用骨骼及动画片段。", Selection.activeObject);
    }

    private static void ImportTexture(string path, bool srgb, bool normal)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }
}
#endif
