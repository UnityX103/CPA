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
    private const string BuildPath = "Builds/macOS/CTClock.app";
    private const string ProfilerBuildPath = "Builds/macOS/CTClock_Profiler.app";
    /// <summary>运行验证等待秒数（等应用写出日志再读取）</summary>
    private const int VerifyRunSeconds = 8;

    // ─── 菜单项 ──────────────────────────────────────────────────

    /// <summary>构建、签名，然后启动应用并打印启动日志</summary>
    [MenuItem("Build/Build, Run and Verify macOS App")]
    public static void BuildRunAndVerifyMacOS()
    {
        DoBuild();
    }

    /// <summary>构建 Development Build，启用 Profiler 自动连接，用于内存/性能分析</summary>
    [MenuItem("Build/Build macOS Development (Profiler)")]
    public static void BuildMacOSDevelopmentProfiler()
    {
        DoDevelopmentProfilerBuild();
    }

    // ─── 核心构建逻辑 ────────────────────────────────────────────

    private static void DoBuild()
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

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript] ✗ 构建失败: {summary.result}");
            Debug.LogError($"[BuildScript]   错误数: {summary.totalErrors}，警告数: {summary.totalWarnings}");
            return; // 不退出编辑器，仅记录错误
        }

        Debug.Log($"[BuildScript] ✓ 构建成功: {summary.totalSize / 1024 / 1024} MB");
        Debug.Log($"[BuildScript] ✓ 输出路径: {summary.outputPath}");

        SignApplication(BuildPath);
        VerifyBuild(BuildPath);
        RunAndCaptureLogs(BuildPath);
    }

    /// <summary>
    /// 构建 Development Player 并启用 Profiler 自动连接。
    /// 构建完成后自动启动，Unity Profiler 会自动连接到该进程。
    /// </summary>
    private static void DoDevelopmentProfilerBuild()
    {
        string[] scenes = GetBuildScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[BuildScript] ✗ 没有可用的场景，请在 Build Settings 中至少启用一个场景。");
            return;
        }

        Debug.Log("[BuildScript] ════ 开始 Development Build（Profiler）════");
        foreach (string s in scenes)
        {
            Debug.Log($"[BuildScript]   场景: {s}");
        }

        BuildOptions devOptions =
            BuildOptions.Development |
            BuildOptions.ConnectWithProfiler |
            BuildOptions.AllowDebugging;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ProfilerBuildPath,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = devOptions,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript] ✗ Development Build 失败: {summary.result}");
            Debug.LogError($"[BuildScript]   错误数: {summary.totalErrors}，警告数: {summary.totalWarnings}");
            return;
        }

        Debug.Log($"[BuildScript] ✓ Development Build 成功: {summary.totalSize / 1024 / 1024} MB");
        Debug.Log($"[BuildScript] ✓ 输出路径: {Path.GetFullPath(ProfilerBuildPath)}");

        SignApplication(ProfilerBuildPath);
        LaunchProfilerBuild(ProfilerBuildPath);
    }

    /// <summary>启动 Profiler 构建并提示用户连接</summary>
    private static void LaunchProfilerBuild(string appPath)
    {
        string fullAppPath = Path.GetFullPath(appPath);
        string appName = Path.GetFileNameWithoutExtension(appPath);
        string executablePath = Path.Combine(fullAppPath, "Contents", "MacOS", appName);

        if (!File.Exists(executablePath))
        {
            Debug.LogWarning($"[BuildScript] ⚠ 找不到可执行文件：{executablePath}");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        try
        {
            Process proc = Process.Start(startInfo);
            if (proc == null)
            {
                Debug.LogError("[BuildScript] ✗ 无法启动 Development Build。");
                return;
            }

            Debug.Log($"[BuildScript] ▶ Development Build 已启动 (PID: {proc.Id})");
            Debug.Log("[BuildScript] ════ Profiler 使用指南 ════");
            Debug.Log("[BuildScript] 1. 打开 Window → Analysis → Profiler");
            Debug.Log("[BuildScript] 2. Profiler 应已自动连接到该进程");
            Debug.Log("[BuildScript] 3. 切换到 Memory 模块，点击 [Take Sample] 拍摄内存快照");
            Debug.Log("[BuildScript] 4. 查看 Simple / Detailed 视图中各类资源占用");
            Debug.Log("[BuildScript] 5. 重点关注：Texture2D、GC Alloc、Native Memory");
            Debug.Log("[BuildScript] ════════════════════════════");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BuildScript] ✗ 启动失败: {ex.Message}");
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

        string logFile = Path.Combine(Path.GetTempPath(), "CPA_launch_verify.log");
        if (File.Exists(logFile))
        {
            File.Delete(logFile);
        }

        Debug.Log($"[BuildScript] ▶ 启动应用（{VerifyRunSeconds} 秒后读取启动日志）...");

        // 桌面宠物需要图形渲染，不加 -batchmode；通过 -logFile 指定日志路径
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"-logFile \"{logFile}\"",
            UseShellExecute = false,
            CreateNoWindow = false,
        };

        try
        {
            Process proc = Process.Start(startInfo);
            if (proc == null)
            {
                Debug.LogError("[BuildScript] ✗ 无法启动应用进程。");
                return;
            }

            Debug.Log($"[BuildScript]   PID: {proc.Id}，日志: {logFile}");

            // 等待应用启动并稳定（会短暂阻塞编辑器 UI，属预期行为）
            Thread.Sleep(VerifyRunSeconds * 1000);

            PrintPlayerLog(logFile);

            Debug.Log("[BuildScript] ✓ 应用已启动，保持运行中。");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BuildScript] ✗ 启动失败: {ex.Message}");
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

    // ─── 签名与验证（保持不变）────────────────────────────────────

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
            FileName = "/usr/bin/codesign",
            Arguments = $"--force --deep --sign - --entitlements \"{entitlementsPath}\" \"{fullPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
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
            FileName = "/usr/bin/codesign",
            Arguments = $"-dv --entitlements - \"{fullPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
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
                if (combined.Contains("com.apple.security.cs.allow-jit"))
                {
                    Debug.Log("[BuildScript] ✓ JIT 权限已配置");
                }
                if (combined.Contains("com.apple.security.cs.allow-unsigned-executable-memory"))
                {
                    Debug.Log("[BuildScript] ✓ Unsigned Executable Memory 权限已配置");
                }
                if (combined.Contains("com.apple.security.automation.apple-events"))
                {
                    Debug.LogWarning("[BuildScript] ⚠ 检测到额外 Apple Events 权限，请确认是否必须。");
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
                Debug.LogWarning("[BuildScript] ⚠ 检测到额外 Apple Events 权限描述，请确认是否必须。");
            }
        }

        Debug.Log("[BuildScript] ========================================");
        Debug.Log("[BuildScript] 构建完成！应用已准备好发布。");
        Debug.Log($"[BuildScript] 输出路径: {fullPath}");
        Debug.Log("[BuildScript] ========================================");
    }
}
