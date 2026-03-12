using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class BuildScript
{
    [MenuItem("Build/Build and Package macOS App")]
    public static void BuildAndPackageMacOS()
    {
        string buildPath = "Builds/macOS/AppMonitor.app";
        
        Debug.Log("[BuildScript] 开始构建 macOS 应用...");
        
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/AppMonitorTestScene.unity" },
            locationPathName = buildPath,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] ✓ 构建成功: {summary.totalSize / 1024 / 1024} MB");
            Debug.Log($"[BuildScript] ✓ 输出路径: {summary.outputPath}");
            
            SignApplication(buildPath);
            VerifyBuild(buildPath);
        }
        else
        {
            Debug.LogError($"[BuildScript] ✗ 构建失败: {summary.result}");
            Debug.LogError($"[BuildScript]   错误数: {summary.totalErrors}");
            Debug.LogError($"[BuildScript]   警告数: {summary.totalWarnings}");
            EditorApplication.Exit(1);
        }
    }
    
    
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
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            if (process.ExitCode == 0)
            {
                Debug.Log("[BuildScript] ✓ 签名验证成功");
                
                if (output.Contains("com.apple.security.automation.apple-events"))
                {
                    Debug.Log("[BuildScript] ✓ Apple Events 权限已配置");
                }
                if (output.Contains("com.apple.security.cs.allow-jit"))
                {
                    Debug.Log("[BuildScript] ✓ JIT 权限已配置");
                }
            }
            else
            {
                Debug.LogWarning($"[BuildScript] ⚠ 签名验证警告 (退出码: {process.ExitCode})");
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
