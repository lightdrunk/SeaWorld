// Copy Assets/NeonDamselfish into a Unity 2022.3+ URP project.
// This editor-only importer configures this asset without changing project settings.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NeonDamselfishEducation.Editor
{
    [InitializeOnLoad]
    public sealed class NeonDamselfishUrpSetup : AssetPostprocessor
    {
        const string Root = "Assets/NeonDamselfish";
        const string ModelPath = Root + "/NeonDamselfish_Rigged.fbx";
        const string MaterialPath = Root + "/Materials/PC_Atlas_URP.mat";
        const string PrefabPath = Root + "/Prefabs/NeonDamselfish_URP.prefab";
        const string ControllerPath = Root + "/Animations/NeonDamselfish.controller";
        static bool queued;
        static bool building;

        static NeonDamselfishUrpSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ModelPath) &&
                    (!File.Exists(MaterialPath) || !File.Exists(PrefabPath))) QueueSetup();
            };
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "/Textures/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            ConfigureTexture(importer, assetPath);
        }

        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            ConfigureModel((ModelImporter)assetImporter);
        }

        void OnPreprocessAnimation()
        {
            if (assetPath != ModelPath) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = "IdleSwim_2s";
                clip.loopTime = true;
                clip.loopPose = false;
            }
            importer.clipAnimations = clips;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            if (building) return;
            if (imported.Any(p => p == ModelPath || p.StartsWith(Root + "/Textures/", StringComparison.Ordinal)))
                QueueSetup();
        }

        static void QueueSetup()
        {
            if (queued || building) return;
            queued = true;
            EditorApplication.delayCall += RunQueued;
        }

        static void RunQueued()
        {
            queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueSetup();
                return;
            }
            if (File.Exists(ModelPath)) Setup();
        }

        static void ConfigureTexture(TextureImporter importer, string path)
        {
            bool normal = path.Contains("_Normal_");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = path.Contains("_BaseColor_");
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }

        static void ConfigureModel(ModelImporter importer)
        {
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false; // Preserve labelled structures and bones.
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Texture2D Texture(string name)
        {
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");
            if (result == null) throw new InvalidOperationException("Missing texture: " + name);
            return result;
        }

        [MenuItem("Tools/Neon Damselfish/Setup URP Material and Prefab")]
        public static void Setup()
        {
            if (building) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("Neon Damselfish: URP/Lit was not found. Install URP, then run Tools > Neon Damselfish > Setup URP Material and Prefab.");
                return;
            }
            if (!File.Exists(ModelPath))
            {
                Debug.LogError("Copy the supplied Assets/NeonDamselfish folder to the same path in your Unity project.");
                return;
            }
            building = true;
            try
            {
                EnsureFolder(Root + "/Materials");
                EnsureFolder(Root + "/Prefabs");
                EnsureFolder(Root + "/Animations");
                foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png"))
                {
                    var ti = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                    if (ti == null) continue;
                    ConfigureTexture(ti, path);
                    ti.SaveAndReimport();
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = "PC_Atlas_URP" };
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }
                material.shader = shader;
                material.SetFloat("_WorkflowMode", 1f);
                material.SetTexture("_BaseMap", Texture("PC_BaseColor_2K"));
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", Texture("PC_Normal_2K"));
                material.SetFloat("_BumpScale", 0.7f);
                material.SetTexture("_MetallicGlossMap", Texture("PC_URP_MetallicSmoothness_2K"));
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_AlphaClip", 0f);
                material.SetFloat("_Cull", 2f);
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_SrcBlend", 1f);
                material.SetFloat("_DstBlend", 0f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.DisableKeyword("_SPECULAR_SETUP");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
                EditorUtility.SetDirty(material);

                var mi = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
                if (mi == null) throw new InvalidOperationException("FBX ModelImporter is unavailable.");
                ConfigureModel(mi);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "PC_Atlas_URP"), material);
                mi.SaveAndReimport();

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null && clip != null)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                    controller.AddMotion(clip);
                }
                if (!File.Exists(PrefabPath))
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    try
                    {
                        instance.name = "NeonDamselfish_URP";
                        instance.transform.localScale = Vector3.one;
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                        var animator = instance.GetComponent<Animator>();
                        if (animator == null) animator = instance.AddComponent<Animator>();
                        animator.applyRootMotion = false;
                        if (controller != null) animator.runtimeAnimatorController = controller;
                        // Retarget static annotation anchors to the nearest deforming bone.
                        var transforms = instance.GetComponentsInChildren<Transform>(true);
                        foreach (var anchor in transforms.Where(t => t.name.StartsWith("Label_", StringComparison.Ordinal)))
                        {
                            string n = anchor.name;
                            string bone = n.Contains("Caudal") ? "TailFin" :
                                n.Contains("DorsalSpines") ? "DorsalSpines" :
                                n.Contains("DorsalSoft") ? "DorsalSoft" :
                                n.Contains("AnalFin") ? "AnalFin" :
                                n.Contains("PectoralFin_L") ? "Pectoral.L" :
                                n.Contains("PectoralFin_R") ? "Pectoral.R" :
                                n.Contains("PelvicFin") ? "Pelvic.L" :
                                n.Contains("Eye") || n.Contains("Mouth") || n.Contains("Operculum") ? "Head" : "Spine_02";
                            var target = transforms.FirstOrDefault(t => t.name == bone);
                            if (target != null) anchor.SetParent(target, true);
                        }
                        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Neon Damselfish URP ready: " + PrefabPath);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { building = false; }
        }
    }
}
