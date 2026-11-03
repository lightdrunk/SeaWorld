using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Trimaculatus.Education.Editor
{
    // Scoped to this exact FBX and its HT_ texture files.
    public sealed class TrimaculatusImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (Path.GetFileName(assetPath) != "Hippocampus_trimaculatus_Rigged.fbx") return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false; // Keep bones and annotation anchors accessible.
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessAnimation()
        {
            if (Path.GetFileName(assetPath) != "Hippocampus_trimaculatus_Rigged.fbx") return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.Contains("Idle_FinPropulsion");
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalOrientation = true;
            }
            if (clips.Length > 0) importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            string file = Path.GetFileName(assetPath);
            if (!file.StartsWith("HT_", StringComparison.Ordinal) || !file.EndsWith("_2K.png", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = file.Contains("Normal_");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = file.Contains("BaseColor");
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = file.Contains("MetallicSmoothness") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }

    public static class TrimaculatusSetup
    {
        [MenuItem("Tools/Seahorse/Build URP Teaching Prefab")]
        public static void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP/Lit was not found. Install Universal RP and assign a URP pipeline asset in Project Settings > Graphics first.");
                return;
            }
            string path = AssetDatabase.FindAssets("Hippocampus_trimaculatus_Rigged t:Model")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("Hippocampus_trimaculatus_Rigged.fbx", StringComparison.Ordinal));
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Import the supplied FBX before running setup.");
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            // Force scoped import settings even when the FBX arrived before this script compiled.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            foreach (string png in Directory.GetFiles(folder + "/Textures", "HT_*_2K.png"))
                AssetDatabase.ImportAsset(png.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);

            EnsureFolder(folder, "Generated");
            string generated = folder + "/Generated";
            string materialPath = generated + "/HT_URP_Lit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "HT_URP_Lit" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", LoadTexture(folder, "HT_BaseColor_2K.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", LoadTexture(folder, "HT_Normal_OpenGL_2K.png"));
            material.SetFloat("_BumpScale", 0.6f);
            material.SetTexture("_MetallicGlossMap", LoadTexture(folder, "HT_URP_MetallicSmoothness_2K.png"));
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f); // Map alpha already stores 1 - roughness.
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Cull", 2f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SPECULAR_SETUP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);

            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(imported);
            try
            {
                instance.name = "Hippocampus_trimaculatus_Male_URP";
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        skin.quality = SkinQuality.Bone2;
                        skin.updateWhenOffscreen = true;
                        var bounds = skin.localBounds;
                        bounds.Expand(0.08f); // Tail-curl demonstration can move beyond the rest bounds.
                        skin.localBounds = bounds;
                    }
                }
                var annotations = instance.GetComponent<TrimaculatusAnnotations>() ?? instance.AddComponent<TrimaculatusAnnotations>();
                annotations.Rebuild();
                var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                string controllerPath = generated + "/HT_Demonstrations.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                foreach (var state in machine.states) machine.RemoveState(state.state);
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"));
                foreach (var clip in clips)
                {
                    string stateName = clip.name.Contains("Idle_FinPropulsion") ? "IdleFinPropulsion" :
                        clip.name.Contains("TailCurl") ? "TailCurl" : "JawOpen";
                    var state = machine.AddState(stateName);
                    state.motion = clip;
                    if (stateName == "IdleFinPropulsion") machine.defaultState = state;
                }
                EditorUtility.SetDirty(controller);
                animator.runtimeAnimatorController = controller;
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, generated + "/Hippocampus_trimaculatus_URP.prefab");
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Created the URP teaching prefab, material, three animation states, and annotation component in " + generated);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static Texture2D LoadTexture(string folder, string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/" + name);
            if (texture == null) throw new FileNotFoundException("Missing texture: " + name);
            return texture;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
