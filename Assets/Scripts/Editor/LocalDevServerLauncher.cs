#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using APP.Network.Config;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace APP.Editor
{
    /// <summary>
    /// 本地开发服务器一键启停。菜单：
    ///   - Tools/CPA/切到本地开发并启动服务器：把 NetworkConfig 切到 Development，
    ///     并以 Server/src/index.js 在后台跑 Node 进程。
    ///   - Tools/CPA/启动本地开发服务器：仅启动 Node，不动 NetworkConfig。
    ///   - Tools/CPA/停止本地开发服务器：杀掉本编辑器追踪的 Node 进程。
    ///
    /// 进程通过 SessionState 跨域 reload 持久化 PID，避免 domain reload 后丢失句柄。
    /// 编辑器退出时自动清理。
    /// </summary>
    [InitializeOnLoad]
    public static class LocalDevServerLauncher
    {
        private const string ResourceAssetPath = "Assets/Resources/NetworkConfig.asset";
        private const string SessionKeyPid = "CPA.LocalDevServer.Pid";
        private const string SessionKeyPort = "CPA.LocalDevServer.Port";

        static LocalDevServerLauncher()
        {
            EditorApplication.quitting += StopServerInternal;
        }

        // ─── 菜单 ────────────────────────────────────────────────

        [MenuItem("Tools/CPA/切到本地开发并启动服务器")]
        private static void SwitchAndStart()
        {
            if (!SwitchToDevelopment())
            {
                return;
            }
            StartServer();
        }

        [MenuItem("Tools/CPA/启动本地开发服务器")]
        private static void StartServerMenu()
        {
            StartServer();
        }

        [MenuItem("Tools/CPA/停止本地开发服务器")]
        private static void StopServerMenu()
        {
            StopServerInternal();
        }

        // ─── 切环境 ──────────────────────────────────────────────

        private static bool SwitchToDevelopment()
        {
            NetworkConfig config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(ResourceAssetPath);
            if (config == null)
            {
                EditorUtility.DisplayDialog(
                    "NetworkConfig 缺失",
                    $"未在 {ResourceAssetPath} 找到配置。请先运行 Tools/CPA/Network Environment 创建。",
                    "好的");
                return false;
            }

            if (config.CurrentEnvironment != ServerEnvironment.Development)
            {
                Undo.RecordObject(config, "Switch To Development");
                config.CurrentEnvironment = ServerEnvironment.Development;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                Debug.Log($"[LocalDevServer] NetworkConfig 已切换到 Development（{config.ActiveServerUrl}）");
            }
            else
            {
                Debug.Log($"[LocalDevServer] NetworkConfig 已经是 Development（{config.ActiveServerUrl}）");
            }
            return true;
        }

        // ─── 启动 / 停止 ─────────────────────────────────────────

        private static void StartServer()
        {
            int existingPid = SessionState.GetInt(SessionKeyPid, 0);
            if (existingPid > 0 && IsProcessAlive(existingPid))
            {
                int existingPort = SessionState.GetInt(SessionKeyPort, 0);
                Debug.Log($"[LocalDevServer] 已有进程在跑（pid={existingPid} port={existingPort}），跳过启动。");
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string serverScript = Path.Combine(projectRoot, "Server", "src", "index.js");
            if (!File.Exists(serverScript))
            {
                EditorUtility.DisplayDialog(
                    "未找到服务器脚本",
                    $"{serverScript} 不存在，无法启动本地服务器。",
                    "好的");
                return;
            }

            string nodeBin = ResolveNodeBin();
            if (string.IsNullOrEmpty(nodeBin))
            {
                EditorUtility.DisplayDialog(
                    "未找到 node",
                    "请安装 Node.js（推荐 brew install node@20）或设置环境变量 NODE_BIN 指向 node 可执行文件。",
                    "好的");
                return;
            }

            int port = ParsePortFromUrl(NetworkConfig.Instance.ActiveServerUrl);
            var psi = new ProcessStartInfo
            {
                FileName = nodeBin,
                Arguments = $"\"{serverScript}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            // 让 Server/src/index.js 默认 PORT 与 NetworkConfig 对齐（脚本读 process.env.PORT）
            if (port > 0)
            {
                psi.EnvironmentVariables["PORT"] = port.ToString();
            }

            Process p;
            try
            {
                p = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalDevServer] 启动失败：{ex.Message}");
                return;
            }
            if (p == null)
            {
                Debug.LogError("[LocalDevServer] Process.Start 返回 null。");
                return;
            }

            // 异步把 stdout/stderr 接到 Unity Console，方便观察服务端日志
            p.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[LocalDevServer][stdout] {e.Data}");
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning($"[LocalDevServer][stderr] {e.Data}");
            };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            SessionState.SetInt(SessionKeyPid, p.Id);
            SessionState.SetInt(SessionKeyPort, port);

            Debug.Log($"[LocalDevServer] 已启动 Node 服务（pid={p.Id} port={port}）。" +
                      $"客户端将通过 {NetworkConfig.Instance.ActiveServerUrl} 连接。");
        }

        private static void StopServerInternal()
        {
            int pid = SessionState.GetInt(SessionKeyPid, 0);
            if (pid <= 0)
            {
                Debug.Log("[LocalDevServer] 当前未追踪到正在运行的服务进程。");
                return;
            }

            try
            {
                Process p = Process.GetProcessById(pid);
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(3000);
                }
                Debug.Log($"[LocalDevServer] 已停止 Node 服务（pid={pid}）。");
            }
            catch (ArgumentException)
            {
                // 进程已不存在
                Debug.Log($"[LocalDevServer] pid={pid} 进程已不存在，清理记录。");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalDevServer] 停止时异常：{ex.Message}");
            }
            finally
            {
                SessionState.EraseInt(SessionKeyPid);
                SessionState.EraseInt(SessionKeyPort);
            }
        }

        // ─── helpers ─────────────────────────────────────────────

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                Process p = Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveNodeBin()
        {
            string env = Environment.GetEnvironmentVariable("NODE_BIN");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

            string[] candidates =
            {
                "/opt/homebrew/bin/node",
                "/opt/homebrew/opt/node@22/bin/node",
                "/opt/homebrew/opt/node@20/bin/node",
                "/usr/local/bin/node",
                "/usr/local/opt/node@22/bin/node",
                "/usr/bin/node",
            };
            foreach (string path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return "node";
        }

        private static int ParsePortFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return 0;
            try
            {
                var uri = new Uri(url);
                return uri.Port;
            }
            catch
            {
                return 0;
            }
        }
    }
}
#endif
