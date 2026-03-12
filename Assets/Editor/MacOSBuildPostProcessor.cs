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
        
        // 添加 Apple Events 权限描述
        rootDict.SetString("NSAppleEventsUsageDescription", 
            "此应用需要控制其他应用以执行自动化任务。");
        
        plist.WriteToFile(plistPath);
        
        Debug.Log($"[MacOSBuildPostProcessor] ✓ Info.plist 已更新: {plistPath}");
    }
    
    private static void ConfigureEntitlements(string buildPath, string appName)
    {
        // 读取项目中的 entitlements 文件
        string sourceEntitlements = Path.Combine(Application.dataPath, 
            "Plugins/macOS/AppMonitor.entitlements");
        
        if (!File.Exists(sourceEntitlements))
        {
            Debug.LogWarning($"[MacOSBuildPostProcessor] 未找到 Entitlements 文件: {sourceEntitlements}");
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
