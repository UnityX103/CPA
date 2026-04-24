using System.Collections;
using System.IO;
using System.Reflection;
using APP.Network.Event;
using APP.Pomodoro.Controller;
using NUnit.Framework;
using NZ.VisualTest;
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
    /// 覆盖联机设置面板的两个新增交互状态：
    ///   1. join/leave/create 过程中 osp-busy-overlay 蒙层挡住整面板
    ///   2. E_NetworkError 触发 ConfirmDialog 弹窗（替代原底部 osp-error Label）
    /// 通过反射调用 OnlineSettingsPanelController 的 private 方法直接驱动状态，
    /// 避免依赖真实 WebSocket 响应；只做截图 + manifest 登记，不做像素断言。
    /// </summary>
    [TestFixture]
    public sealed class OnlineSettingsBusyAndErrorImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags InstanceAny =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [UnityTest]
        public IEnumerator OnlineSettings_ShouldCaptureBusyAndErrorStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            yield return EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

            yield return WaitForFrames(10);

            UnifiedSettingsPanelDriver driver =
                UnityEngine.Object.FindFirstObjectByType<UnifiedSettingsPanelDriver>();
            Assert.That(driver, Is.Not.Null, "MainV2 场景中必须存在 UnifiedSettingsPanelDriver。");

            UIDocument uiDocument = driver.GetComponent<UIDocument>();
            Assert.That(uiDocument, Is.Not.Null, "UnifiedSettingsPanelDriver 必须挂 UIDocument。");

            yield return WaitUntilFieldAssigned(driver, "_controller", 60);

            object settingsPanel = GetPrivateField(driver, "_controller");
            Assert.That(settingsPanel, Is.Not.Null, "Driver._controller 应在 Start 之后完成初始化。");

            APP.Pomodoro.GameApp.Interface.SendCommand(
                new APP.Pomodoro.Command.Cmd_OpenUnifiedSettings());

            InvokePrivate(settingsPanel, "SelectTab", "online");
            yield return WaitUntilReady(
                uiDocument.rootVisualElement,
                "settings-overlay",
                "osp-root",
                60);

            object onlineSettings = GetPrivateField(settingsPanel, "_onlineSettings");
            Assert.That(onlineSettings, Is.Not.Null,
                "UnifiedSettingsPanelController._onlineSettings 应在切到 online tab 后被创建。");

            // Step 1：默认未加入态（全屏截图）
            yield return CaptureScreenStep("online-default", string.Empty, "full-screen");

            // Step 2：模拟"加入房间中"—— 直接调用 private EnterBusy，
            // 蒙层应浮出覆盖 osp-root，"正在加入房间…"居中显示
            InvokePrivate(onlineSettings, "EnterBusy", "正在加入房间…");
            yield return WaitUntilBusyOverlayVisible(uiDocument.rootVisualElement, 60);

            yield return CaptureScreenStep("online-busy-joining", string.Empty, "full-screen");

            // 结束 busy，回到基线状态
            InvokePrivateNoArg(onlineSettings, "ExitBusy");
            yield return WaitUntilBusyOverlayHidden(uiDocument.rootVisualElement, 60);

            // Step 3：模拟网络异常 —— 直接发 E_NetworkError 触发 OnNetworkError → ShowErrorDialog
            InvokeOnNetworkError(onlineSettings, "ROOM_FULL", "房间人数已满");
            yield return WaitUntilErrorDialogVisible(uiDocument.rootVisualElement, 60);

            yield return CaptureScreenStep("online-error-dialog", string.Empty, "full-screen");

            // manifest 应登记 3 个 step
            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3), "应登记三个 capture step。");
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "01-online-default-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "02-online-busy-joining-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "03-online-error-dialog-actual.png")), Is.True);

            string manifestPath = Path.Combine(CurrentRunDirectory, "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True, "manifest.json 必须生成。");
#endif
        }

        // ─── 截图 & 等待辅助 ────────────────────────────────

        private IEnumerator CaptureOverlay(
            VisualElement root,
            string stepName,
            string baselinePath)
        {
            VisualElement overlay = root.Q<VisualElement>("settings-overlay");
            Assert.That(overlay, Is.Not.Null, "settings-overlay 必须存在。");
            yield return CaptureStep(stepName, overlay, baselinePath, "settings-overlay");
        }

        private static IEnumerator WaitUntilBusyOverlayVisible(VisualElement root, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement busy = root.Q<VisualElement>("osp-busy-overlay");
                bool visible = busy != null
                    && !busy.ClassListContains("osp-hidden")
                    && busy.resolvedStyle.display == DisplayStyle.Flex
                    && busy.worldBound.width > 0f
                    && busy.worldBound.height > 0f;
                if (visible) yield break;
                yield return null;
            }
            Assert.Fail("等待 osp-busy-overlay 显示超时。");
        }

        private static IEnumerator WaitUntilBusyOverlayHidden(VisualElement root, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement busy = root.Q<VisualElement>("osp-busy-overlay");
                bool hidden = busy != null
                    && (busy.ClassListContains("osp-hidden")
                        || busy.resolvedStyle.display == DisplayStyle.None);
                if (hidden) yield break;
                yield return null;
            }
            Assert.Fail("等待 osp-busy-overlay 隐藏超时。");
        }

        private static IEnumerator WaitUntilErrorDialogVisible(VisualElement root, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement host = root.Q<VisualElement>("online-error-dialog-host");
                VisualElement dlg = host?.Q<VisualElement>("dlg-root");
                bool visible = dlg != null
                    && dlg.resolvedStyle.display == DisplayStyle.Flex
                    && dlg.worldBound.width > 0f
                    && dlg.worldBound.height > 0f;
                if (visible) yield break;
                yield return null;
            }
            Assert.Fail("等待 ConfirmDialog 弹窗显示超时。");
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

                if (isReady) yield break;
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
                if (GetPrivateField(target, fieldName) != null) yield break;
                yield return null;
            }
            Assert.Fail($"等待字段赋值超时：{fieldName}");
        }

        // ─── 反射辅助 ──────────────────────────────────────

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.That(field, Is.Not.Null, $"字段不存在：{fieldName}");
            return field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, string argument)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstanceAny);
            Assert.That(method, Is.Not.Null, $"方法不存在：{methodName}");
            method.Invoke(target, new object[] { argument });
        }

        private static void InvokePrivateNoArg(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstanceAny);
            Assert.That(method, Is.Not.Null, $"方法不存在：{methodName}");
            method.Invoke(target, System.Array.Empty<object>());
        }

        /// <summary>
        /// 调用 OnNetworkError(E_NetworkError)：该事件类型定义在 APP.Network.Event 命名空间。
        /// 反射拿到接受 E_NetworkError 的方法后构造参数传入。
        /// </summary>
        private static void InvokeOnNetworkError(object target, string code, string message)
        {
            MethodInfo method = null;
            foreach (MethodInfo m in target.GetType().GetMethods(InstanceAny))
            {
                if (m.Name != "OnNetworkError") continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(E_NetworkError))
                {
                    method = m;
                    break;
                }
            }
            Assert.That(method, Is.Not.Null, "方法不存在：OnNetworkError(E_NetworkError)");
            var evt = new E_NetworkError(code, message);
            method.Invoke(target, new object[] { evt });
        }
    }
}
