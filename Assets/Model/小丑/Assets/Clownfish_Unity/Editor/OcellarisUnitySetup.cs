// Put the Clownfish_Unity folder under Assets, then run Tools > Clownfish > Create Unity Prefab.
// Uses editor APIs only. Does not add runtime dependencies.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class OcellarisAssetImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!Path.GetFileName(assetPath).StartsWith("Ocellaris_", StringComparison.Ordinal)) return;
        var ti = (TextureImporter)assetImporter;
        bool normal = assetPath.EndsWith("_Normal_2K.png", StringComparison.Ordinal);
        bool color = assetPath.EndsWith("_BaseColor_2K.png", StringComparison.Ordinal);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = color;
        ti.maxTextureSize = 2048;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.alphaIsTransparency = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
    void OnPreprocessModel()
    {
        if (!assetPath.EndsWith("/Ocellaris_Unity.fbx", StringComparison.Ordinal)) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1;
        mi.useFileScale = true;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.importAnimation = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.optimizeGameObjects = false; // Keep named bones and teaching anchors accessible.
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
    }
}

public static class OcellarisUnitySetup
{
    [MenuItem("Tools/Clownfish/Create Unity Prefab")]
    public static void CreatePrefab()
    {
        string fbx = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!fbx.EndsWith("/Ocellaris_Unity.fbx", StringComparison.Ordinal))
        {
            fbx = null;
            foreach (string guid in AssetDatabase.FindAssets("Ocellaris_Unity t:Model"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith("/Ocellaris_Unity.fbx", StringComparison.Ordinal)) continue;
                if (fbx != null) { EditorUtility.DisplayDialog("Clownfish", "Select the Ocellaris_Unity.fbx to configure in the Project window.", "OK"); return; }
                fbx = p;
            }
        }
        if (string.IsNullOrEmpty(fbx)) { EditorUtility.DisplayDialog("Clownfish", "Copy Clownfish_Unity into your project's Assets folder first.", "OK"); return; }
        string root = Path.GetDirectoryName(fbx).Replace('\\', '/');
        var pipeline = GraphicsSettings.currentRenderPipeline;
        string pipelineName = pipeline == null ? "" : pipeline.GetType().Name;
        bool hdrp = pipelineName.IndexOf("HDRender", StringComparison.OrdinalIgnoreCase) >= 0;
        bool urp = pipelineName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0;
        if (pipeline != null && !hdrp && !urp)
        {
            EditorUtility.DisplayDialog("Clownfish", "This custom render pipeline needs its own PBR material. Use the included base color, normal, and roughness maps.", "OK"); return;
        }
        Shader shader = Shader.Find(hdrp ? "HDRP/Lit" : urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) { Debug.LogError("Clownfish: required Lit shader was not found."); return; }
        if (!AssetDatabase.IsValidFolder(root + "/Materials")) AssetDatabase.CreateFolder(root, "Materials");
        string matPath = root + "/Materials/Ocellaris_Unity.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
        else mat.shader = shader;
        foreach (string textureGuid in AssetDatabase.FindAssets("t:Texture2D", new[] { root + "/Textures" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(textureGuid), ImportAssetOptions.ForceUpdate);
        Texture2D color = Load(root, "BaseColor");
        Texture2D normal = Load(root, "Normal");
        Texture2D packed = Load(root, hdrp ? "HDRPMask" : "UnityMetallicSmoothness");
        if (!color || !normal || !packed) { Debug.LogError("Clownfish: texture files are missing from " + root + "/Textures"); return; }
        SetTexture(mat, hdrp ? "_BaseColorMap" : urp ? "_BaseMap" : "_MainTex", color);
        SetColor(mat, hdrp || urp ? "_BaseColor" : "_Color", Color.white);
        SetTexture(mat, hdrp ? "_NormalMap" : "_BumpMap", normal);
        SetFloat(mat, hdrp ? "_NormalScale" : "_BumpScale", .30f);
        SetTexture(mat, hdrp ? "_MaskMap" : "_MetallicGlossMap", packed);
        SetFloat(mat, "_Metallic", 0);
        SetFloat(mat, "_Smoothness", 1);
        SetFloat(mat, "_Glossiness", 1);
        SetFloat(mat, "_GlossMapScale", 1);
        SetFloat(mat, "_SmoothnessTextureChannel", 0);
        SetFloat(mat, "_WorkflowMode", 1); // Metallic workflow in URP.
        mat.DisableKeyword("_SPECULAR_SETUP");
        mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        mat.EnableKeyword("_NORMALMAP");
        if (hdrp)
        {
            mat.EnableKeyword("_MASKMAP"); mat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            SetFloat(mat, "_NormalMapSpace", 0);
            SetFloat(mat, "_MetallicRemapMin", 0); SetFloat(mat, "_MetallicRemapMax", 1);
            SetFloat(mat, "_SmoothnessRemapMin", 0); SetFloat(mat, "_SmoothnessRemapMax", 1);
            SetFloat(mat, "_AORemapMin", 0); SetFloat(mat, "_AORemapMax", 1);
        }
        else mat.EnableKeyword(urp ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
        EditorUtility.SetDirty(mat);
        // Remap the FBX's single source material. Keep this mapping on the importer.
        var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Ocellaris_PBR"), mat);
        mi.SaveAndReimport();
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null) { Debug.LogError("Clownfish: FBX could not be instantiated."); return; }
        try
        {
            instance.name = "Amphiprion_ocellaris";
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                renderer.sharedMaterials = slots;
            }
            string path = root + "/Ocellaris_Unity.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("Clownfish prefab created: " + path + ". Total rest length: approximately 0.10 metres; Generic rig, 16 bones, 18 teaching anchors.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }
    static Texture2D Load(string root, string channel) { return AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/Textures/Ocellaris_" + channel + "_2K.png"); }
    static void SetTexture(Material m, string p, Texture t) { if (m.HasProperty(p)) m.SetTexture(p, t); }
    static void SetFloat(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
    static void SetColor(Material m, string p, Color v) { if (m.HasProperty(p)) m.SetColor(p, v); }
}
