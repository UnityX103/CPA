using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZ.VisualTest.Editor
{
    public static class AppMonitorTestSceneSetup
    {
        private const string UxmlPath = "Packages/com.nz.visualtest/Editor/Windows/Components/AppMonitor/AppMonitorSection.uxml";
        private const string ScenePath = "Assets/Scenes/AppMonitorTestScene.unity";
        private const string PrefabPath = "Assets/Prefabs/AppMonitorUI.prefab";
        private const string McpSequencePath = "Assets/McpCallSequence.json";

        [MenuItem("NZ VisualTest/配置 AppMonitor 测试场景")]
        public static void SetupScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);

            var uiRoot = GameObject.Find("AppMonitorUI");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("AppMonitorUI");
            }

            var uiDocument = uiRoot.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = uiRoot.AddComponent<UIDocument>();
            }

            var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxmlAsset != null)
            {
                uiDocument.visualTreeAsset = uxmlAsset;
                Debug.Log($"[AppMonitorTestSceneSetup] 成功设置 UXML: {UxmlPath}");
            }
            else
            {
                Debug.LogError($"[AppMonitorTestSceneSetup] 无法加载 UXML 文件: {UxmlPath}");
            }

            SetupCamera();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AppMonitorTestSceneSetup] 场景已保存: {ScenePath}");

            CreatePrefab(uiRoot);
        }

        private static void SetupCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObj = GameObject.Find("Main Camera");
                if (cameraObj != null)
                {
                    camera = cameraObj.GetComponent<Camera>();
                }
            }

            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 0, -10);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                Debug.Log("[AppMonitorTestSceneSetup] 相机已配置");
            }
            else
            {
                Debug.LogWarning("[AppMonitorTestSceneSetup] 未找到相机");
            }
        }

        private static void CreatePrefab(GameObject uiRoot)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(uiRoot, PrefabPath, out bool success);
            if (success)
            {
                Debug.Log($"[AppMonitorTestSceneSetup] 预制体已创建: {PrefabPath}");
            }
            else
            {
                Debug.LogError($"[AppMonitorTestSceneSetup] 预制体创建失败: {PrefabPath}");
            }
        }

        [MenuItem("NZ VisualTest/创建 MCP 调用序列")]
        public static void CreateMcpCallSequence()
        {
            var lines = new string[]
            {
                "{",
                "    \"description\": \"AppMonitorTestScene MCP 调用序列\"," ,
                "    \"steps\": [",
                "        { \"step\": 1, \"tool\": \"manage_scene\", \"action\": \"create\", \"params\": { \"name\": \"AppMonitorTestScene\", \"path\": \"Assets/Scenes/AppMonitorTestScene.unity\" } }",
                "        { \"step\": 2, \"tool\": \"manage_gameobject\", \"action\": \"create\", \"params\": { \"name\": \"AppMonitorUI\", \"position\": [0, 0, 0] } }",
                "        { \"step\": 3, \"tool\": \"manage_components\", \"action\": \"add_component\", \"params\": { \"target\": \"AppMonitorUI\", \"component_type\": \"UnityEngine.UIElements.UIDocument\" } }",
                "        { \"step\": 4, \"tool\": \"manage_components\", \"action\": \"set_property\", \"params\": { \"target\": \"AppMonitorUI\", \"component_type\": \"UIDocument\", \"property\": \"sourceAsset\", \"value\": \"Packages/com.nz.visualtest/Editor/Windows/Components/AppMonitor/AppMonitorSection.uxml\" } }",
                "        { \"step\": 5, \"tool\": \"manage_gameobject\", \"action\": \"modify\", \"params\": { \"target\": \"Main Camera\", \"position\": [0, 0, -10] } }",
                "        { \"step\": 6, \"tool\": \"manage_components\", \"action\": \"set_property\", \"params\": { \"target\": \"Main Camera\", \"component_type\": \"Camera\", \"property\": \"clearFlags\", \"value\": 2 } }",
                "        { \"step\": 7, \"tool\": \"manage_components\", \"action\": \"set_property\", \"params\": { \"target\": \"Main Camera\", \"component_type\": \"Camera\", \"property\": \"backgroundColor\", \"value\": [0.15, 0.15, 0.15, 1.0] } }",
                "        { \"step\": 8, \"tool\": \"manage_asset\", \"action\": \"create_prefab\", \"params\": { \"source\": \"AppMonitorUI\", \"path\": \"Assets/Prefabs/AppMonitorUI.prefab\" } }",
                "        { \"step\": 9, \"tool\": \"manage_scene\", \"action\": \"save\" }",
                "    ]",
                "}"
            };
            
            System.IO.File.WriteAllLines(McpSequencePath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"[AppMonitorTestSceneSetup] MCP 调用序列已保存: {McpSequencePath}");
        }
    }
}
