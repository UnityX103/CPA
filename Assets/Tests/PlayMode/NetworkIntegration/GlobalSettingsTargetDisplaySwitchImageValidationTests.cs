using System.Collections;
using System.IO;
using APP.Pomodoro;
using APP.Pomodoro.System;
using Kirurobo;
using NZ.VisualTest;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace APP.NetworkIntegration.Tests
{
    /// <summary>
    /// 切换目标显示器后的窗口几何回归测试。
    ///
    /// 关注的 bug：UniWindowController 上 `_shouldFitMonitor=true` 时，
    /// 任何 SetWindowSize 都会触发 ObserveWindowStyleChanged → 0.5 秒后
    /// ForceZoomed 协程强制 SetZoomed(true)，把窗口最大化覆盖整张显示器，
    /// 吃掉我们自定义的"顶栏 FixedWindowHeight"几何，UI 视觉错乱。
    ///
    /// 修复双保险：
    ///   1. MainV2.unity 把 _shouldFitMonitor prefab override 改 0
    ///   2. WindowPositionSystem.Initialize 再次确认关闭，防 scene 漂移
    ///
    /// 测试做的事：
    ///   - 加载 MainV2，等系统初始化完成
    ///   - 断言 _uwc.shouldFitMonitor == false（修复 invariant）
    ///   - 截屏 before-switch
    ///   - 调 IWindowPositionSystem.PreviewMoveToMonitor 触发同一条 ApplyMonitorRect
    ///   - 等 0.8s（已超过 ForceZoomed 的 0.5s 阈值）
    ///   - 截屏 after-switch
    ///   - 再次断言 shouldFitMonitor 仍 false（没被任何路径偷偷打开）
    /// </summary>
    [TestFixture]
    public sealed class GlobalSettingsTargetDisplaySwitchImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";

        [UnityTest]
        public IEnumerator TargetDisplaySwitch_WindowDoesNotGetForceMaximized()
        {
#if !UNITY_EDITOR
            Assert.Ignore("PlayMode 视觉测试仅在 Editor 内运行。");
            yield break;
#else
            // 网络/认证类偶发日志不应打断
            LogAssert.ignoreFailingMessages = true;

            yield return EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

            yield return WaitForFrames(15);

            UniWindowController uwc = UnityEngine.Object.FindFirstObjectByType<UniWindowController>();
            Assert.That(uwc, Is.Not.Null, "MainV2 必须挂 UniWindowController。");

            // ── 修复 invariant：WindowPositionSystem.Initialize 必须已关闭 shouldFitMonitor。
            //    若此断言失败，要么 scene override 又被打开，要么 Initialize 里的防御逻辑被改坏。
            Assert.That(uwc.shouldFitMonitor, Is.False,
                "WindowPositionSystem.Initialize 应在启动时把 shouldFitMonitor 关闭，否则 " +
                "UniWindowController 的 ForceZoomed 协程会把窗口最大化覆盖整张显示器。");

            yield return WaitForFrames(20);

            // ── before：切换前快照
            yield return CaptureScreenStep(
                "before-display-switch",
                null,
                "before / shouldFitMonitor=false / 顶栏几何");

            // ── 触发显示器切换（PreviewMoveToMonitor 与用户在面板上下拉切换走同一路径）
            IWindowPositionSystem wps = GameApp.Interface.GetSystem<IWindowPositionSystem>();
            int monitorCount = UniWindowController.GetMonitorCount();
            // 单显示器环境下也跑：仍会走完整 ApplyMonitorRect 路径，验证不会被强制 zoom
            int targetIndex = monitorCount > 1 ? 1 : 0;
            wps.PreviewMoveToMonitor(targetIndex);

            // ── 等待已超过 ForceZoomed 协程的 0.5s 阈值；如果 bug 在，此时窗口已被 SetZoomed(true)
            yield return new WaitForSeconds(0.8f);
            // 再让 WindowPositionSystem 的 RefitAfterMonitorBound 协程跑完
            yield return WaitForFrames(5);

            // ── after：切换后快照
            yield return CaptureScreenStep(
                "after-display-switch",
                null,
                $"after / monitorCount={monitorCount} / targetIndex={targetIndex} / 等待 0.8s 已超 ForceZoomed 0.5s 阈值");

            // ── 修复仍然成立的硬断言
            Assert.That(uwc.shouldFitMonitor, Is.False,
                "切换后 shouldFitMonitor 仍应为 false（任何一条路径都不能把它再打开）。");

            // ── manifest 完整性
            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(2),
                "应登记 before / after 两张截图。");
            Assert.That(File.Exists(
                Path.Combine(CurrentRunDirectory, "01-before-display-switch-actual.png")), Is.True);
            Assert.That(File.Exists(
                Path.Combine(CurrentRunDirectory, "02-after-display-switch-actual.png")), Is.True);
#endif
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }
        }
    }
}
