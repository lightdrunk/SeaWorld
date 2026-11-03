using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景清理工具：删除泄漏到场景根部的临时图元对象（名为 Sphere 等）。
/// </summary>
public static class SceneCleaner
{
    [MenuItem("Tools/清理泄漏的Sphere对象")]
    public static void DeleteLeakedSpheres()
    {
        int count = 0;
        GameObject[] all = Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in all)
        {
            if (go.name == "Sphere" && go.transform.parent == null)
            {
                Object.DestroyImmediate(go);
                count++;
            }
        }
        Debug.Log($"[SceneCleaner] 已删除 {count} 个泄漏的 Sphere 对象");
    }
}