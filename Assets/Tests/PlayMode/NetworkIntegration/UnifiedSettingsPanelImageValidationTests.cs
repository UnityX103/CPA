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
    public sealed class UnifiedSettingsPanelImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator UnifiedSettingsPanel_ShouldCaptureExpectedStates()
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

            yield return CaptureOverlay(
                uiDocument.rootVisualElement,
                "pomodoro",
                $"{BaselineDirectory}/unified-settings-pomodoro.png");

            InvokePrivate(settingsPanel, "SelectTab", "online");
            yield return WaitUntilReady(
                uiDocument.rootVisualElement,
                "settings-overlay",
                "osp-root",
                60);

            yield return CaptureOverlay(
                uiDocument.rootVisualElement,
                "online",
                $"{BaselineDirectory}/unified-settings-online-not-joined.png");

            InvokePrivate(settingsPanel, "SelectTab", "pet");
            yield return WaitUntilReady(
                uiDocument.rootVisualElement,
                "settings-overlay",
                "pet-root",
                60);

            yield return CaptureOverlay(
                uiDocument.rootVisualElement,
                "pet",
                $"{BaselineDirectory}/unified-settings-pet.png");

            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3), "应登记三个 capture step。");
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "01-pomodoro-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "02-online-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "03-pet-actual.png")), Is.True);
            string manifestPath = Path.Combine(CurrentRunDirectory, "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True);

            string manifestJson = File.ReadAllText(manifestPath);
            VisualImageTestRunManifest manifest = JsonUtility.FromJson<VisualImageTestRunManifest>(manifestJson);

            Assert.That(manifest, Is.Not.Null, "manifest.json 应能反序列化为 VisualImageTestRunManifest。");
            Assert.That(manifest.steps, Is.Not.Null, "manifest.json 中 steps 不应为空。");
            Assert.That(manifest.steps.Count, Is.EqualTo(3), "manifest.json 中应登记三个 capture step。");

            AssertManifestStep(
                manifest.steps[0],
                "pomodoro",
                "01-pomodoro-actual.png",
                $"{BaselineDirectory}/unified-settings-pomodoro.png");
            AssertManifestStep(
                manifest.steps[1],
                "online",
                "02-online-actual.png",
                $"{BaselineDirectory}/unified-settings-online-not-joined.png");
            AssertManifestStep(
                manifest.steps[2],
                "pet",
                "03-pet-actual.png",
                $"{BaselineDirectory}/unified-settings-pet.png");
#endif
        }

        private IEnumerator CaptureOverlay(
            VisualElement root,
            string stepName,
            string baselinePath)
        {
            VisualElement overlay = root.Q<VisualElement>("settings-overlay");
            Assert.That(overlay, Is.Not.Null, "settings-overlay 必须存在。");

            yield return CaptureStep(stepName, overlay, baselinePath, "settings-overlay");
        }

        private static void AssertManifestStep(
            VisualImageTestStepManifest step,
            string expectedName,
            string expectedActualImagePath,
            string expectedBaselineImagePath)
        {
            Assert.That(step, Is.Not.Null, $"manifest step {expectedName} 不应为空。");
            Assert.That(step.name, Is.EqualTo(expectedName));
            Assert.That(step.actualImagePath, Is.EqualTo(expectedActualImagePath));
            Assert.That(step.baselineImagePath, Is.EqualTo(expectedBaselineImagePath));
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
