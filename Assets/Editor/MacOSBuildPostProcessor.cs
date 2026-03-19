using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class MacOSBuildPostProcessor
{
    [PostProcessBuild(1)]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.StandaloneOSX)
            return;

        Debug.Log("[MacOSBuildPostProcessor] 开始配置 macOS 权限...");

        string appName = Path.GetFileNameWithoutExtension(pathToBuiltProject);
        string contentsPath = Path.Combine(pathToBuiltProject, "Contents");
        
        // 1. 修改 Info.plist
        ConfigureInfoPlist(contentsPath);
        
        // 2. 配置 Entitlements
        ConfigureEntitlements(pathToBuiltProject, appName);
        
        Debug.Log("[MacOSBuildPostProcessor] ✓ macOS 权限配置完成");
    }
    
    private static void ConfigureInfoPlist(string contentsPath)
    {
        string plistPath = Path.Combine(contentsPath, "Info.plist");
        
        if (!File.Exists(plistPath))
        {
            Debug.LogWarning($"[MacOSBuildPostProcessor] 未找到 Info.plist: {plistPath}");
            return;
        }
        
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        
        PlistElementDict rootDict = plist.root;
        
        // 添加 Accessibility 权限描述
        rootDict.SetString("NSAccessibilityUsageDescription", 
            "此应用需要辅助功能权限来检测当前活动窗口，以便提供更好的使用体验。");
        
        plist.WriteToFile(plistPath);
        
        Debug.Log($"[MacOSBuildPostProcessor] ✓ Info.plist 已更新: {plistPath}");
    }
    
    private static void ConfigureEntitlements(string buildPath, string appName)
    {
        // 查找 entitlements：仅使用 Assets 路径
        string[] candidatePaths =
        {
            Path.Combine(Application.dataPath, "Plugins/macOS/AppMonitor.entitlements"),
        };

        string sourceEntitlements = System.Array.Find(candidatePaths, File.Exists);

        if (sourceEntitlements == null)
        {
            Debug.LogWarning("[MacOSBuildPostProcessor] 未找到 Entitlements 文件，已搜索路径：\n  "
                + string.Join("\n  ", candidatePaths));
            return;
        }
        
        // 复制到构建目录
        string destEntitlements = Path.Combine(buildPath, "Contents", $"{appName}.entitlements");
        File.Copy(sourceEntitlements, destEntitlements, true);
        
        Debug.Log($"[MacOSBuildPostProcessor] ✓ Entitlements 已复制: {destEntitlements}");
        Debug.Log("[MacOSBuildPostProcessor] ⚠️  注意: 需要手动签名应用以应用 Entitlements");
        Debug.Log($"[MacOSBuildPostProcessor]   命令: codesign --force --deep --sign - --entitlements \"{destEntitlements}\" \"{buildPath}\"");
    }
}
