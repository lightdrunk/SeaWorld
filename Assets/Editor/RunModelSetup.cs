using UnityEditor;
using UnityEngine;

// 临时脚本：代替手动选择资产后运行模型说明文档要求的菜单工具
// （Tools > Wrasse > Set Up FBX Materials 要求 Project 窗口先选中 FBX）
public static class RunModelSetup
{
    [MenuItem("Tools/TempModelSetup/Run")]
    public static void Run()
    {
        Object wrasse = AssetDatabase.LoadAssetAtPath<Object>(
            "Assets/Model/蓝带裂唇鱼/BluestreakCleanerWrasse_UnityFBX/BluestreakCleanerWrasse_Adult_Unity.fbx");
        if (wrasse == null)
        {
            Debug.LogError("[RunModelSetup] 未找到 BluestreakCleanerWrasse_Adult_Unity.fbx");
            return;
        }
        Selection.activeObject = wrasse;
        WrasseFbxMaterials.SetUp();
        OcellarisUnitySetup.CreatePrefab();
        Debug.Log("[RunModelSetup] 裂唇鱼材质与小丑鱼 Prefab 配置完成");
    }
}
