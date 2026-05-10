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
    /// <summary>
    /// 验证 gs1Tv (Pomodoro Settings Panel) 中 Tp1bH (psp-end-action-dropdown)
    /// 与 dELwq (psp-video-path-dropdown) 在「展开态」下的视觉一致：
    /// 两者都用 Pencil Frjkw 组件（comp-input-dropdown* 一族 class），由 InputDropdownBinding 统一驱动。
    /// 通过直接切换 .comp-input-dropdown-menu--hidden 模拟菜单展开后截整屏，
    /// 与 GlobalSettingsDropdownImageValidationTests 走同样的"toggle class + capture"路径。
    /// </summary>
    [TestFixture]
    public sealed class PomodoroSettingsPanelDropdownExpandedImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const string MenuHiddenClass = "comp-input-dropdown-menu--hidden";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator EndActionDropdown_ShouldRenderExpandedMenu()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            IPomodoroModel model = null;
            PomodoroEndActionMode originalMode = PomodoroEndActionMode.TopWindow;

            try
            {
                yield return EditorSceneManager.LoadSceneInPlayMode(
                    ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return WaitForFrames(10);

                UnifiedSettingsPanelDriver driver =
                    UnityEngine.Object.FindFirstObjectByType<UnifiedSettingsPanelDriver>();
                Assert.That(driver, Is.Not.Null, "MainV2 场景必须存在 UnifiedSettingsPanelDriver。");

                UIDocument uiDocument = driver.GetComponent<UIDocument>();
                Assert.That(uiDocument, Is.Not.Null, "Driver 必须挂 UIDocument。");

                yield return WaitUntilFieldAssigned(driver, "_controller", 60);

                model = GameApp.Interface.GetModel<IPomodoroModel>();
                originalMode = model.EndActionMode.Value;

                GameApp.Interface.SendCommand(new Cmd_OpenUnifiedSettings());
                yield return WaitUntilReady(uiDocument.rootVisualElement, "settings-overlay", "psp-root", 60);

                model.EndActionMode.Value = PomodoroEndActionMode.TopWindow;
                yield return WaitUntilHasWidth(uiDocument.rootVisualElement, "psp-end-action-row", 30);

                VisualElement endActionMenu = uiDocument.rootVisualElement.Q<VisualElement>("psp-end-action-menu");
                Assert.That(endActionMenu, Is.Not.Null, "psp-end-action-menu 必须存在（共享 Frjkw 组件菜单）。");
                Assert.That(
                    endActionMenu.ClassListContains("comp-input-dropdown-menu"),
                    Is.True,
                    "psp-end-action-menu 必须挂 comp-input-dropdown-menu class（共享 Frjkw 组件，与 GSP 一致）。");

                int initialItemCount = endActionMenu.childCount;
                Assert.That(initialItemCount, Is.EqualTo(2),
                    "InputDropdownBinding 应已为 EndAction 构建 2 个菜单项（弹窗到顶部 / 播放视频）。");

                endActionMenu.EnableInClassList(MenuHiddenClass, false);
                yield return WaitUntilHasWidth(uiDocument.rootVisualElement, "psp-end-action-menu", 30);

                yield return CaptureScreenStep(
                    "pomodoro-end-action-dropdown-expanded",
                    $"{BaselineDirectory}/pomodoro-end-action-dropdown-expanded.png",
                    "Tp1bH 展开态：菜单显示两项可选");

                Assert.That(CurrentManifest.steps.Count, Is.EqualTo(1), "应登记 1 个 capture step。");
                Assert.That(
                    File.Exists(Path.Combine(CurrentRunDirectory, "01-pomodoro-end-action-dropdown-expanded-actual.png")),
                    Is.True,
                    "展开态截图必须存在。");
            }
            finally
            {
                if (model != null)
                {
                    model.EndActionMode.Value = originalMode;
                }
            }
#endif
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
                FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
                if (field != null && field.GetValue(target) != null)
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail($"等待字段赋值超时：{fieldName}");
        }
    }
}
