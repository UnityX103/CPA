#if UNITY_EDITOR
using APP.Network.Config;
using UnityEditor;
using UnityEngine;

namespace APP.Editor
{
    /// <summary>
    /// 服务器环境切换窗口。菜单：Tools → CPA → Network Environment。
    /// 下拉选中的环境会写入 Assets/Resources/NetworkConfig.asset，
    /// 随下一次 Build 一起打包生效。
    /// </summary>
    public sealed class NetworkEnvironmentWindow : EditorWindow
    {
        private const string ResourceAssetPath = "Assets/Resources/NetworkConfig.asset";

        [MenuItem("Tools/CPA/Network Environment")]
        private static void Open()
        {
            GetWindow<NetworkEnvironmentWindow>("Network Environment").Show();
        }

        private void OnGUI()
        {
            NetworkConfig config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(ResourceAssetPath);

            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    $"未找到 {ResourceAssetPath}。点击下方按钮创建默认配置。",
                    MessageType.Warning);
                if (GUILayout.Button("创建默认 NetworkConfig"))
                {
                    const string resourcesDir = "Assets/Resources";
                    if (!AssetDatabase.IsValidFolder(resourcesDir))
                    {
                        AssetDatabase.CreateFolder("Assets", "Resources");
                    }

                    NetworkConfig created = CreateInstance<NetworkConfig>();
                    AssetDatabase.CreateAsset(created, ResourceAssetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    EditorGUIUtility.PingObject(created);
                    Selection.activeObject = created;
                }
                return;
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("当前服务器环境", EditorStyles.boldLabel);
            ServerEnvironment newEnv = (ServerEnvironment)EditorGUILayout.EnumPopup(
                "Environment", config.CurrentEnvironment);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("环境 URL", EditorStyles.boldLabel);
            string devUrl = EditorGUILayout.TextField("Development", config.DevelopmentServerUrl);
            string prodUrl = EditorGUILayout.TextField("Production", config.ProductionServerUrl);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Active URL", config.ActiveServerUrl);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, "Change Network Config");
                config.CurrentEnvironment = newEnv;
                config.DevelopmentServerUrl = devUrl;
                config.ProductionServerUrl = prodUrl;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "此配置位于 Resources/，当前选择会随下一次 Build 打包，决定客户端连接哪台服务器。",
                MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button("在 Project 面板中定位该配置"))
            {
                EditorGUIUtility.PingObject(config);
                Selection.activeObject = config;
            }
        }
    }
}
#endif
