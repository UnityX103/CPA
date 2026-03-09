using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    [MenuItem("Build/Build macOS Player with Tests")]
    public static void BuildMacOSPlayerWithTests()
    {
        string buildPath = "Builds/macOS/DevTemplate.app";
        
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/AppMonitorTestScene.unity" },
            locationPathName = buildPath,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.Development | BuildOptions.IncludeTestAssemblies | BuildOptions.ConnectToHost | BuildOptions.ConnectToHost
        };

        PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 1);

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"✓ Build succeeded: {summary.totalSize / 1024 / 1024} MB");
            Debug.Log($"✓ Output: {summary.outputPath}");
        }
        else
        {
            Debug.LogError($"✗ Build failed: {summary.result}");
            Debug.LogError($"  Total errors: {summary.totalErrors}");
            Debug.LogError($"  Total warnings: {summary.totalWarnings}");
            EditorApplication.Exit(1);
        }
    }
}
