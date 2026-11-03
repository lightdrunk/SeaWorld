using System.IO;
using UnityEditor;
using UnityEngine;

namespace OceanTools.Editor
{
    /// <summary>
    /// MCP 桥接自恢复：每 0.5 秒轮询 Assets/Editor/McpRestart.trigger，
    /// 发现触发文件就调用 McpUnityServer.RestartServer() 重启 WebSocket 服务端，
    /// Node 客户端随后会自动重连。Agent 在桥接失联时写入该文件即可恢复，无需人工干预。
    /// </summary>
    [InitializeOnLoad]
    public static class McpBridgeKeeper
    {
        private const string TriggerPath = "Assets/Editor/McpRestart.trigger";
        private static double _nextCheck;

        static McpBridgeKeeper()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;

            try
            {
                if (!File.Exists(TriggerPath)) return;
                File.Delete(TriggerPath);

                var server = McpUnity.Unity.McpUnityServer.Instance;
                if (server == null)
                {
                    Debug.LogError("[McpBridgeKeeper] MCP 服务不可用（batch mode？）。");
                    return;
                }

                Debug.Log($"[McpBridgeKeeper] 检测到触发文件，重启 MCP 服务（IsListening={server.IsListening}）...");
                server.RestartServer();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[McpBridgeKeeper] 重启失败: {ex.Message}");
            }
        }

        [MenuItem("Tools/MCP Unity/重启桥接")]
        private static void RestartNow()
        {
            File.WriteAllText(TriggerPath, System.DateTime.Now.ToString("O"));
            Debug.Log("[McpBridgeKeeper] 已写入触发文件，将在下一次轮询时重启 MCP 服务。");
        }
    }
}