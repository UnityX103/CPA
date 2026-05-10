using System;
using System.Collections;
using System.IO;
using System.Reflection;
using APP.Pomodoro;
using APP.Pomodoro.Command;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NZ.VisualTest;
using NUnit.Framework;
using QFramework;
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
    public sealed class PomodoroSettingsPanelEndActionImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator EndActionRow_ShouldCaptureThreeStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            Func<string> originalVideoFilePicker = PomodoroSettingsPanelController.VideoFilePicker;
            IPomodoroModel model = null;
            PomodoroEndActionMode originalEndActionMode = PomodoroEndActionMode.TopWindow;
            string originalEndActionVideoPath = string.Empty;

            try
            {
                PomodoroSettingsPanelController.VideoFilePicker = () => "/tmp/funny_cat.mp4";

                yield return EditorSceneManager.LoadSceneInPlayMode(
                    ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));

                yield return WaitForFrames(10);

                UnifiedSettingsPanelDriver driver =
                    UnityEngine.Object.FindFirstObjectByType<UnifiedSettingsPanelDriver>();
                Assert.That(driver, Is.Not.Null, "MainV2 场景中必须存在 UnifiedSettingsPanelDriver。");

                UIDocument uiDocument = driver.GetComponent<UIDocument>();
                Assert.That(uiDocument, Is.Not.Null, "UnifiedSettingsPanelDriver GameObject 必须挂 UIDocument。");

                yield return WaitUntilFieldAssigned(driver, "_controller", 60);

                object settingsPanel = GetPrivateField(driver, "_controller");
                Assert.That(settingsPanel, Is.Not.Null, "Driver._controller 应在 Start 之后完成初始化。");

                model = GameApp.Interface.GetModel<IPomodoroModel>();
                Assert.That(model, Is.Not.Null, "必须能从 GameApp 获取 IPomodoroModel。");

                originalEndActionMode = model.EndActionMode.Value;
                originalEndActionVideoPath = model.EndActionVideoPath.Value ?? string.Empty;

                GameApp.Interface.SendCommand(new Cmd_OpenUnifiedSettings());
                yield return WaitUntilReady(
                    uiDocument.rootVisualElement,
                    "settings-overlay",
                    "psp-root",
                    60);

                model.EndActionMode.Value = PomodoroEndActionMode.TopWindow;
                model.EndActionVideoPath.Value = string.Empty;
                yield return null;

                AssertRendered(uiDocument.rootVisualElement, "psp-end-action-row");
                yield return CaptureScreenStep(
                    "pomodoro-end-action-default",
                    $"{BaselineDirectory}/pomodoro-end-action-default.png",
                    "TopWindow 默认状态");

                model.EndActionMode.Value = PomodoroEndActionMode.PlayVideo;
                model.EndActionVideoPath.Value = string.Empty;
                // is-video-mode class 切换 display: none → flex 后，UIToolkit 需要至少 1 个 layout pass
                // 才会重算 worldBound，单帧 yield 不够稳，poll 直到行被布局完成或超时
                yield return WaitUntilHasWidth(uiDocument.rootVisualElement, "psp-video-path-row", 30);

                AssertRendered(uiDocument.rootVisualElement, "psp-end-action-row");
                AssertRendered(uiDocument.rootVisualElement, "psp-video-path-row");
                yield return CaptureScreenStep(
                    "pomodoro-end-action-video-empty",
                    $"{BaselineDirectory}/pomodoro-end-action-video-empty.png",
                    "PlayVideo 模式 + 视频路径未选");

                model.EndActionVideoPath.Value = "/Users/test/funny_cat.mp4";
                yield return WaitUntilHasWidth(uiDocument.rootVisualElement, "psp-video-path-row", 30);

                AssertRendered(uiDocument.rootVisualElement, "psp-end-action-row");
                AssertRendered(uiDocument.rootVisualElement, "psp-video-path-row");
                yield return CaptureScreenStep(
                    "pomodoro-end-action-video-selected",
                    $"{BaselineDirectory}/pomodoro-end-action-video-selected.png",
                    "PlayVideo + 已选 funny_cat.mp4");

                Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3), "应登记三个 capture step。");
                Assert.That(CurrentManifest.outputDirectory, Is.EqualTo(CurrentRunDirectory));

                Assert.That(
                    File.Exists(Path.Combine(CurrentRunDirectory, "01-pomodoro-end-action-default-actual.png")),
                    Is.True);
                Assert.That(
                    File.Exists(Path.Combine(CurrentRunDirectory, "02-pomodoro-end-action-video-empty-actual.png")),
                    Is.True);
                Assert.That(
                    File.Exists(Path.Combine(CurrentRunDirectory, "03-pomodoro-end-action-video-selected-actual.png")),
                    Is.True);

                string manifestPath = Path.Combine(CurrentRunDirectory, "manifest.json");
                Assert.That(File.Exists(manifestPath), Is.True);

                string manifestJson = File.ReadAllText(manifestPath);
                VisualImageTestRunManifest manifest = JsonUtility.FromJson<VisualImageTestRunManifest>(manifestJson);

                Assert.That(manifest, Is.Not.Null, "manifest.json 应能反序列化为 VisualImageTestRunManifest。");
                Assert.That(manifest.outputDirectory, Is.EqualTo(CurrentRunDirectory));
                Assert.That(manifest.steps, Is.Not.Null, "manifest.json 中 steps 不应为空。");
                Assert.That(manifest.steps.Count, Is.EqualTo(3), "manifest.json 中应登记三个 capture step。");

                AssertManifestStep(
                    manifest.steps[0],
                    "pomodoro-end-action-default",
                    "01-pomodoro-end-action-default-actual.png",
                    $"{BaselineDirectory}/pomodoro-end-action-default.png");
                AssertManifestStep(
                    manifest.steps[1],
                    "pomodoro-end-action-video-empty",
                    "02-pomodoro-end-action-video-empty-actual.png",
                    $"{BaselineDirectory}/pomodoro-end-action-video-empty.png");
                AssertManifestStep(
                    manifest.steps[2],
                    "pomodoro-end-action-video-selected",
                    "03-pomodoro-end-action-video-selected-actual.png",
                    $"{BaselineDirectory}/pomodoro-end-action-video-selected.png");
            }
            finally
            {
                if (model != null)
                {
                    model.EndActionMode.Value = originalEndActionMode;
                    model.EndActionVideoPath.Value = originalEndActionVideoPath;
                }

                PomodoroSettingsPanelController.VideoFilePicker = originalVideoFilePicker;
            }
#endif
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

        private static VisualElement AssertRendered(VisualElement root, string elementName)
        {
            VisualElement element = root.Q<VisualElement>(elementName);
            Assert.That(element, Is.Not.Null, $"{elementName} 必须存在。");
            Assert.That(element.worldBound.width, Is.GreaterThan(0f), $"{elementName} 宽度必须大于 0。");
            Assert.That(element.worldBound.height, Is.GreaterThan(0f), $"{elementName} 高度必须大于 0。");
            return element;
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

        private static IEnumerator WaitUntilHasWidth(VisualElement root, string elementName, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement element = root.Q<VisualElement>(elementName);
                if (element != null && element.worldBound.width > 0f && element.worldBound.height > 0f)
                {
                    yield break;
                }

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
    }
}
