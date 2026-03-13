using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class BuildScript
{
    private const string BuildPath = "Builds/macOS/AppMonitor.app";

    // ─── 菜单项 ──────────────────────────────────────────────────

    /// <summary>构建并签名（原有逻辑，兼容保留）</summary>
    [MenuItem("Build/Build and Package macOS App")]
    public static void BuildAndPackageMacOS()
    {
        DoBuild(runAfterBuild: false);
    }

    /// <summary>构建、签名，然后启动应用并打印启动日志</summary>
    [MenuItem("Build/Build, Run and Verify macOS App")]
    public static void BuildRunAndVerifyMacOS()
    {
        DoBuild(runAfterBuild: true);
    }

    // ─── 核心构建逻辑 ────────────────────────────────────────────

    private static void DoBuild(bool runAfterBuild)
    {
        string[] scenes = GetBuildScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[BuildScript] ✗ 没有可用的场景，请在 Build Settings 中至少启用一个场景。");
            return;
        }

        Debug.Log("[BuildScript] 开始构建 macOS 应用...");
        foreach (string s in scenes)
        {
            Debug.Log($"[BuildScript]   场景: {s}");
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] ✓ 构建成功: {summary.totalSize / 1024 / 1024} MB");
            Debug.Log($"[BuildScript] ✓ 输出路径: {summary.outputPath}");

            SignApplication(BuildPath);
            VerifyBuild(BuildPath);

            if (runAfterBuild)
            {
                RunAndCaptureLogs(BuildPath);
            }
        }
        else
        {
            Debug.LogError($"[BuildScript] ✗ 构建失败: {summary.result}");
            Debug.LogError($"[BuildScript]   错误数: {summary.totalErrors}，警告数: {summary.totalWarnings}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 获取要构建的场景列表：
    /// 优先使用 Build Settings 中已启用的场景；
    /// 若为空，回退到当前正在编辑的场景。
    /// </summary>
    private static string[] GetBuildScenes()
    {
        string[] enabled = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();

        if (enabled.Length > 0)
        {
            return enabled;
        }

        // 回退：使用当前打开的场景
        string currentPath = EditorSceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(currentPath))
        {
            Debug.LogWarning("[BuildScript] ⚠ Build Settings 中无已启用场景，使用当前打开的场景作为回退。");
            return new[] { currentPath };
        }

        return new string[0];
    }

    // ─── 启动验证 ────────────────────────────────────────────────

    /// <summary>
    /// 启动构建产物，等待几秒，捕获并打印 Player.log，随后终止进程。
    /// </summary>
    private static void RunAndCaptureLogs(string appPath)
    {
        string fullAppPath = Path.GetFullPath(appPath);
        string appName = Path.GetFileNameWithoutExtension(appPath);
        string executablePath = Path.Combine(fullAppPath, "Contents", "MacOS", appName);

        if (!File.Exists(executablePath))
        {
            Debug.LogWarning($"[BuildScript] ⚠ 找不到可执行文件：{executablePath}，跳过运行验证。");
            return;
        }

        // 将日志写到临时文件方便读取
        string logFile = Path.Combine(Path.GetTempPath(), "CPA_launch_verify.log");
        if (!string.IsNullOrEmpty(logFile) && File.Exists(logFile))
        {
            File.Delete(logFile);
        }

        Debug.Log($"[BuildScript] ▶ 启动应用（验证模式，{VerifyRunSeconds} 秒后自动终止）...");

        // 桌面宠物需要图形渲染，不加 -batchmode；通过 -logFile 指定日志路径
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"-logFile \"{logFile}\"",
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        Process proc = null;
        try
        {
            proc = Process.Start(startInfo);
            if (proc == null)
            {
                Debug.LogError("[BuildScript] ✗ 无法启动应用进程。");
                return;
            }

            Debug.Log($"[BuildScript]   PID: {proc.Id}，日志: {logFile}");

            // 等待应用启动并稳定（阻塞编辑器主线程，仅用于验证场景）
            Thread.Sleep(VerifyRunSeconds * 1000);

            PrintPlayerLog(logFile);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BuildScript] ✗ 启动失败: {ex.Message}");
        }
        finally
        {
            if (proc != null && !proc.HasExited)
            {
                proc.Kill();
                Debug.Log("[BuildScript] ■ 验证进程已终止。");
            }
            proc?.Dispose();
        }
    }

    /// <summary>读取 Player.log 并输出到 Unity Console</summary>
    private static void PrintPlayerLog(string logFile)
    {
        if (!File.Exists(logFile))
        {
            Debug.LogWarning("[BuildScript] ⚠ 未找到运行日志文件，应用可能未成功写入日志。");
            return;
        }

        string content = File.ReadAllText(logFile);
        if (string.IsNullOrWhiteSpace(content))
        {
            Debug.LogWarning("[BuildScript] ⚠ 运行日志为空。");
            return;
        }

        Debug.Log("[BuildScript] ════ Player.log 内容（截取前 80 行）════");
        string[] lines = content.Split('\n');
        int printCount = System.Math.Min(lines.Length, 80);
        for (int i = 0; i < printCount; i++)
        {
            string line = lines[i].TrimEnd();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.Contains("ERROR") || line.Contains("EXCEPTION") || line.Contains("error"))
            {
                Debug.LogError($"[Player] {line}");
            }
            else if (line.Contains("WARNING") || line.Contains("warning"))
            {
                Debug.LogWarning($"[Player] {line}");
            }
            else
            {
                Debug.Log($"[Player] {line}");
            }
        }

        if (lines.Length > 80)
        {
            Debug.Log($"[BuildScript]   … 共 {lines.Length} 行，已截取前 80 行。完整日志：{logFile}");
        }

        // 关键词检查
        bool hasException = content.Contains("Exception") || content.Contains("EXCEPTION");
        bool hasInitialized = content.Contains("Initialize") || content.Contains("initialized");

        Debug.Log("[BuildScript] ════ 快速健康检查 ════");
        Debug.Log(hasException
            ? "[BuildScript] ⚠ 日志中包含 Exception，请检查上方错误。"
            : "[BuildScript] ✓ 未检测到 Exception。");
        Debug.Log(hasInitialized
            ? "[BuildScript] ✓ 检测到初始化日志，应用已正常启动。"
            : "[BuildScript] ⚠ 未检测到初始化日志，应用可能未正常启动。");
    }

    // 运行验证等待秒数
    private const int VerifyRunSeconds = 5;
    
    
    private static void SignApplication(string appPath)
    {
        Debug.Log("[BuildScript] 开始签名应用...");
        
        string fullPath = Path.GetFullPath(appPath);
        string appName = Path.GetFileNameWithoutExtension(appPath);
        string entitlementsPath = Path.Combine(fullPath, "Contents", $"{appName}.entitlements");
        
        if (!File.Exists(entitlementsPath))
        {
            Debug.LogError($"[BuildScript] ✗ 未找到 Entitlements 文件: {entitlementsPath}");
            return;
        }
        
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "codesign",
            Arguments = $"--force --deep --sign - --entitlements \"{entitlementsPath}\" \"{fullPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            if (process.ExitCode == 0)
            {
                Debug.Log("[BuildScript] ✓ 应用签名成功");
                if (!string.IsNullOrEmpty(output))
                {
                    Debug.Log($"[BuildScript]   {output}");
                }
            }
            else
            {
                Debug.LogError($"[BuildScript] ✗ 签名失败 (退出码: {process.ExitCode})");
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[BuildScript]   {error}");
                }
            }
        }
    }
    
    private static void VerifyBuild(string appPath)
    {
        Debug.Log("[BuildScript] 验证构建...");
        
        string fullPath = Path.GetFullPath(appPath);
        
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "codesign",
            Arguments = $"-dv --entitlements - \"{fullPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // codesign -dv 将详细信息写入 stderr，合并两个流做关键词检查
            string combined = output + stderr;

            if (process.ExitCode == 0)
            {
                Debug.Log("[BuildScript] ✓ 签名验证成功");

                if (combined.Contains("com.apple.security.automation.apple-events"))
                {
                    Debug.Log("[BuildScript] ✓ Apple Events 权限已配置");
                }
                if (combined.Contains("com.apple.security.cs.allow-jit"))
                {
                    Debug.Log("[BuildScript] ✓ JIT 权限已配置");
                }
            }
            else
            {
                Debug.LogWarning($"[BuildScript] ⚠ 签名验证警告 (退出码: {process.ExitCode})");
                if (!string.IsNullOrEmpty(stderr))
                {
                    Debug.LogWarning($"[BuildScript]   {stderr}");
                }
            }
        }

        string infoPlistPath = Path.Combine(fullPath, "Contents", "Info.plist");
        if (File.Exists(infoPlistPath))
        {
            string plistContent = File.ReadAllText(infoPlistPath);
            if (plistContent.Contains("NSAccessibilityUsageDescription"))
            {
                Debug.Log("[BuildScript] ✓ Accessibility 权限描述已配置");
            }
            if (plistContent.Contains("NSAppleEventsUsageDescription"))
            {
                Debug.Log("[BuildScript] ✓ Apple Events 权限描述已配置");
            }
        }
        
        Debug.Log("[BuildScript] ========================================");
        Debug.Log("[BuildScript] 构建完成！应用已准备好发布。");
        Debug.Log($"[BuildScript] 输出路径: {fullPath}");
        Debug.Log("[BuildScript] ========================================");
    }
}
