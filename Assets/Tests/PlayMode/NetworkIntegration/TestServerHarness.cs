using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace APP.NetworkIntegration.Tests
{
    /// <summary>
    /// 启动 Server/bin/test-server.js 子进程。
    /// 通过 stdout 第一行 JSON 读取实际监听端口。
    /// </summary>
    public sealed class TestServerHarness : IDisposable
    {
        private Process _process;

        public string Url { get; private set; }
        public int Port { get; private set; }

        public static TestServerHarness Start()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string serverScript = Path.Combine(projectRoot, "Server", "bin", "test-server.js");

            if (!File.Exists(serverScript))
            {
                Assert.Ignore($"测试服务器脚本不存在: {serverScript}");
            }

            string nodeBin = Environment.GetEnvironmentVariable("NODE_BIN") ?? "node";

            var psi = new ProcessStartInfo
            {
                FileName = nodeBin,
                Arguments = $"\"{serverScript}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Process p;
            try { p = Process.Start(psi); }
            catch (Exception ex) { Assert.Ignore($"无法启动 node: {ex.Message}"); return null; }

            if (p == null) { Assert.Ignore("Process.Start 返回 null"); return null; }

            string firstLine = p.StandardOutput.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                string stderr = p.StandardError.ReadToEnd();
                try { p.Kill(); } catch { }
                Assert.Ignore($"test-server.js 未输出启动信息；stderr={stderr}");
                return null;
            }

            int port = ParsePort(firstLine);
            string url = ParseUrl(firstLine);

            return new TestServerHarness { _process = p, Port = port, Url = url };
        }

        private static int ParsePort(string line)
        {
            int idx = line.IndexOf("\"port\":");
            if (idx < 0) return 0;
            int start = idx + "\"port\":".Length;
            int end = line.IndexOfAny(new[] { ',', '}' }, start);
            if (end <= start) return 0;
            return int.TryParse(line.Substring(start, end - start).Trim(), out int p) ? p : 0;
        }

        private static string ParseUrl(string line)
        {
            int idx = line.IndexOf("\"url\":\"");
            if (idx < 0) return null;
            int start = idx + "\"url\":\"".Length;
            int end = line.IndexOf('"', start);
            return end > start ? line.Substring(start, end - start) : null;
        }

        public void Dispose()
        {
            if (_process == null || _process.HasExited)
            {
                _process?.Dispose();
                _process = null;
                return;
            }

            try { _process.Kill(); _process.WaitForExit(3000); }
            catch { /* ignore */ }
            _process.Dispose();
            _process = null;
        }
    }
}
