using APP.Pomodoro.Controller;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace APP.Pomodoro.Tests
{
    public sealed class DeskWindowControllerSceneReferencesTests
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";

        [Test]
        public void MainV2_DeskWindowController_ShouldReferenceRequiredTemplates()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            DeskWindowController controller = Object.FindFirstObjectByType<DeskWindowController>();
            Assert.That(controller, Is.Not.Null, "MainV2 场景中必须存在 DeskWindowController。");

            var serializedObject = new SerializedObject(controller);

            // 仅检查玩家卡片模板（设置面板模板已迁移到 UnifiedSettingsPanelDriver）
            AssertTemplateReference(
                serializedObject,
                "_playerCardTemplate",
                "Assets/UI_V2/Documents/PlayerCard.uxml");
        }

        private static void AssertTemplateReference(
            SerializedObject serializedObject,
            string propertyName,
            string expectedAssetPath)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"字段不存在：{propertyName}");
            Assert.That(property.objectReferenceValue, Is.Not.Null, $"字段 {propertyName} 不应为空。");
            Assert.That(
                AssetDatabase.GetAssetPath(property.objectReferenceValue),
                Is.EqualTo(expectedAssetPath),
                $"字段 {propertyName} 应指向正确的 UXML 模板。");
        }
    }
}
