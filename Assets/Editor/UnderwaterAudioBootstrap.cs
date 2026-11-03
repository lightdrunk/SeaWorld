using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 一次性引导：确保 Main Camera 挂有 UnderwaterAudio，且背景音（索引 0）自动填入项目内置水下环境音。
/// 两个步骤都幂等：已挂载/已填入则跳过。
/// </summary>
[InitializeOnLoad]
public static class UnderwaterAudioBootstrap
{
    private const string AmbientPath = "Assets/Audio/Underwater_Ambient.wav";

    static UnderwaterAudioBootstrap()
    {
        EditorApplication.delayCall += Ensure;
    }

    private static void Ensure()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        UnderwaterAudio player = cam.GetComponent<UnderwaterAudio>();
        if (player == null)
        {
            player = cam.gameObject.AddComponent<UnderwaterAudio>();
            Debug.Log("[UnderwaterAudioBootstrap] 已为 Main Camera 挂载 UnderwaterAudio", cam);
        }

        if (player.audioClips == null || player.audioClips.Length == 0)
        {
            AudioClip ambient = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientPath);
            if (ambient != null)
            {
                player.audioClips = new AudioClip[] { ambient };
                EditorUtility.SetDirty(player);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                Debug.Log("[UnderwaterAudioBootstrap] 已自动填入水下环境音: " + ambient.name, cam);
            }
        }
    }
}
