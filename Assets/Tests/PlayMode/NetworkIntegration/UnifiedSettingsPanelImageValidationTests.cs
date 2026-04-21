using System;
using System.Collections;
using System.IO;
using System.Reflection;
using APP.Pomodoro.Controller;
using NZ.VisualTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace APP.NetworkIntegration.Tests
{
    [TestFixture]
    public sealed class UnifiedSettingsPanelImageValidationTests : VisualTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const byte PixelTolerance = 24;
        private const float MaxMismatchRatio = 0.12f;

        protected override bool UseDedicatedTestCamera => false;

        protected override bool RecordOnSetUp => false;

        [UnityTest]
        public IEnumerator UnifiedSettingsPanel_ShouldMatchPencilBaselines_WhenSwitchingTabs()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            yield return EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

            yield return WaitForFrames(10);

            DeskWindowController controller = UnityEngine.Object.FindFirstObjectByType<DeskWindowController>();
            Assert.That(controller, Is.Not.Null, "MainV2 场景中必须存在 DeskWindowController。");

            UIDocument uiDocument = controller.GetComponent<UIDocument>();
            Assert.That(uiDocument, Is.Not.Null, "DeskWindowController 所在对象必须挂有 UIDocument。");

            yield return WaitUntilFieldAssigned(controller, "_settingsPanel", 60);

            object settingsPanel = GetPrivateField(controller, "_settingsPanel");
            Assert.That(settingsPanel, Is.Not.Null, "_settingsPanel 应在 Start 之后完成初始化。");

            InvokePublic(settingsPanel, "Show");
            yield return WaitUntilReady(
                uiDocument.rootVisualElement,
                "settings-overlay",
                "psp-root",
                60);

            yield return CaptureAndCompareOverlay(
                uiDocument.rootVisualElement,
                "pomodoro",
                "unified-settings-pomodoro.png");

            InvokePrivate(settingsPanel, "SelectTab", "online");
            yield return WaitUntilReady(
                uiDocument.rootVisualElement,
                "settings-overlay",
                "osp-root",
                60);

            yield return CaptureAndCompareOverlay(
                uiDocument.rootVisualElement,
                "online",
                "unified-settings-online-not-joined.png");

            InvokePrivate(settingsPanel, "SelectTab", "pet");
            yield return WaitUntilReady(
                uiDocument.rootVisualElement,
                "settings-overlay",
                "pet-root",
                60);

            yield return CaptureAndCompareOverlay(
                uiDocument.rootVisualElement,
                "pet",
                "unified-settings-pet.png");
#endif
        }

        private IEnumerator CaptureAndCompareOverlay(
            VisualElement root,
            string artifactSuffix,
            string baselineFileName)
        {
            VisualElement overlay = root.Q<VisualElement>("settings-overlay");
            Assert.That(overlay, Is.Not.Null, "settings-overlay 必须存在。");

            string actualPath = null;
            yield return CaptureElementScreenshot(
                overlay,
                $"unified-settings-{artifactSuffix}-actual",
                path => actualPath = path);

            string baselinePath = Path.Combine(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                BaselineDirectory,
                baselineFileName);

            AssertScreenshotMatchesBaseline(
                actualPath,
                baselinePath,
                $"unified-settings-{artifactSuffix}-diff",
                PixelTolerance,
                MaxMismatchRatio);
        }

        private static IEnumerator WaitUntilReady(
            VisualElement root,
            string overlayName,
            string expectedPanelRootName,
            int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement overlay = root.Q<VisualElement>(overlayName);
                VisualElement expectedPanelRoot = root.Q<VisualElement>(expectedPanelRootName);
                VisualElement host = root.Q<VisualElement>("settings-content-host");

                bool isReady = overlay != null
                    && overlay.resolvedStyle.display == DisplayStyle.Flex
                    && overlay.worldBound.width > 0f
                    && overlay.worldBound.height > 0f
                    && host != null
                    && host.childCount == 1
                    && expectedPanelRoot != null;

                if (isReady)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"等待 UI 就绪超时：overlay={overlayName}, panelRoot={expectedPanelRootName}");
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntilFieldAssigned(MonoBehaviour target, string fieldName, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (GetPrivateField(target, fieldName) != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"等待字段赋值超时：{fieldName}");
        }

        private static object GetPrivateField(MonoBehaviour target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.That(field, Is.Not.Null, $"字段不存在：{fieldName}");
            return field.GetValue(target);
        }

        private static void InvokePublic(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"方法不存在：{methodName}");
            method.Invoke(target, Array.Empty<object>());
        }

        private static void InvokePrivate(object target, string methodName, string argument)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
            Assert.That(method, Is.Not.Null, $"方法不存在：{methodName}");
            method.Invoke(target, new object[] { argument });
        }
    }
}
