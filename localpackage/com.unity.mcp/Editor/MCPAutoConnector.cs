using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor
{
    public static class MCPAutoConnector
    {
        private const string PREF_AUTO_CONNECT = "MCP_AutoConnect_OnStartup";
        private const int MCP_PORT = 8080;
        private const string CONFIG_FILE_NAME = "mcp.config.json";
        private static bool _hasAttemptedConnect = false;

        private static string GetConfigPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, CONFIG_FILE_NAME);
        }

        /// <summary>
        /// 读取项目根目录下的 mcp.config.json，若其中 autoConnect 为 false 则返回 false。
        /// 文件不存在时默认返回 true（自动连接）。
        /// </summary>
        public static bool GetAutoConnectConfigEnabled()
        {
            string configPath = GetConfigPath();

            if (!File.Exists(configPath))
                return true;

            try
            {
                string json = File.ReadAllText(configPath);
                // 简单解析，避免引入额外依赖
                // 匹配 "autoConnect": false
                if (System.Text.RegularExpressions.Regex.IsMatch(json,
                    @"""autoConnect""\s*:\s*false", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MCP AutoConnect] 读取 {CONFIG_FILE_NAME} 失败: {e.Message}");
            }

            return true;
        }

        public static void SetAutoConnectConfigEnabled(bool enabled)
        {
            string configPath = GetConfigPath();

            try
            {
                File.WriteAllText(configPath, $@"{{""autoConnect"": {enabled.ToString().ToLowerInvariant()}}}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MCP AutoConnect] 写入 {CONFIG_FILE_NAME} 失败: {e.Message}");
            }
        }

        private static bool IsAutoConnectEnabledByConfig()
        {
            return GetAutoConnectConfigEnabled();
        }
        
        public static void AutoConnect()
        {
            _ = ConnectAsync();
        }
        
        private static async Task ConnectAsync()
        {
            Debug.Log("[MCP AutoConnect] 开始连接MCP服务器...");
            
            var bridge = MCPServiceLocator.Bridge;
            
            if (bridge.IsRunning)
            {
                Debug.Log("[MCP AutoConnect] MCP已在运行，执行验证...");
                var verifyResult = await bridge.VerifyAsync();
                if (verifyResult.Success)
                {
                    Debug.Log($"[MCP AutoConnect] 连接已验证: {verifyResult.Message}");
                    return;
                }
                Debug.LogWarning($"[MCP AutoConnect] 验证失败: {verifyResult.Message}");
            }
            
            Debug.Log("[MCP AutoConnect] 启动MCP Bridge...");
            bool started = await bridge.StartAsync();
            
            if (started)
            {
                Debug.Log("[MCP AutoConnect] Bridge已启动，等待验证...");
                
                await Task.Delay(1000);
                
                var verifyResult = await bridge.VerifyAsync();
                if (verifyResult.Success)
                {
                    Debug.Log($"[MCP AutoConnect] 连接成功: {verifyResult.Message}");
                }
                else
                {
                    Debug.LogError($"[MCP AutoConnect] 连接验证失败: {verifyResult.Message}");
                }
            }
            else
            {
                Debug.LogError("[MCP AutoConnect] 启动Bridge失败");
            }
        }
        
        private static bool IsMCPPortAvailable()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("localhost", MCP_PORT, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(500);
                    return success && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }
        
        [InitializeOnLoadMethod]
        private static void OnEditorStartup()
        {
            EditorApplication.delayCall += () =>
            {
                CheckAndConnect();
            };
        }
        
        private static async void CheckAndConnect()
        {
            if (_hasAttemptedConnect)
            {
                return;
            }
            
            _hasAttemptedConnect = true;

            if (!IsAutoConnectEnabledByConfig())
            {
                Debug.Log($"[MCP AutoConnect] 项目根目录 {CONFIG_FILE_NAME} 中已禁用自动连接，跳过");
                return;
            }

            await Task.Delay(3000);

            var bridge = MCPServiceLocator.Bridge;
            
            if (bridge.IsRunning)
            {
                Debug.Log("[MCP AutoConnect] MCP已连接");
                return;
            }
            
            if (!IsMCPPortAvailable())
            {
                Debug.Log("[MCP AutoConnect] MCP服务器未启动，跳过自动连接");
                return;
            }
            
            Debug.Log("[MCP AutoConnect] 检测到MCP服务器，开始自动连接...");
            await ConnectAsync();
        }
        
        public static void ToggleAutoConnect()
        {
            bool current = EditorPrefs.GetBool(PREF_AUTO_CONNECT, false);
            EditorPrefs.SetBool(PREF_AUTO_CONNECT, !current);
            Debug.Log($"[MCP AutoConnect] 启动时自动连接已{(!current ? "启用" : "禁用")}");
        }
        
        [MenuItem("MCP/Enable Auto Connect on Startup", true)]
        public static bool ValidateToggleAutoConnect()
        {
            Menu.SetChecked("MCP/Enable Auto Connect on Startup", EditorPrefs.GetBool(PREF_AUTO_CONNECT, false));
            return true;
        }
    }
}