using System.IO;
using APP.Pomodoro.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Editor
{
    /// <summary>
    /// 编辑器工具：一键创建番茄钟所需资产（PanelSettings + PomodoroConfig.asset）
    /// 菜单：APP → 番茄钟 → 初始化资产
    /// </summary>
    public static class PomodoroSetupEditor
    {
        private const string PanelSettingsPath = "Assets/UI/PomodoroPanelSettings.asset";
        private const string ConfigPath = "Assets/Settings/PomodoroConfig.asset";

        [MenuItem("APP/番茄钟/初始化资产")]
        public static void InitializeAssets()
        {
            EnsureDirectory("Assets/UI");
            EnsureDirectory("Assets/Settings");

            CreatePanelSettings();
            CreatePomodoroConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "番茄钟资产初始化",
                $"已创建：\n• {PanelSettingsPath}\n• {ConfigPath}\n\n请将 PomodoroPanelSettings 赋给场景中 UIDocument 组件的 Panel Settings 槽位。",
                "OK");
        }

        private static void CreatePanelSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath) != null)
            {
                return;
            }

            PanelSettings ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 120);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
        }

        private static void CreatePomodoroConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<PomodoroConfig>(ConfigPath) != null)
            {
                return;
            }

            PomodoroConfig cfg = ScriptableObject.CreateInstance<PomodoroConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path);
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
