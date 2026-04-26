using System.Collections;
using System.IO;
using System.Reflection;
using APP.Network.Model;
using APP.Pomodoro;
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
    /// 覆盖 OnlineSettingsPanel 里"房间号旁边的复制按钮"的渲染状态。
    /// 该按钮对应 Pencil 组件 Button/Copy (id j9CVE)，Unity 侧实现为
    /// Assets/UI_V2/Documents/Components/ButtonCopy.uxml +
    /// Assets/UI_V2/Styles/Components/ButtonCopy.uss。
    /// 因为复制按钮只出现在 osp-room-card（已加入房间时），
    /// 现有 UnifiedSettingsPanelImageValidationTests 只截了未加入状态，
    /// 本测试补上"已加入 + 复制按钮默认/已复制"两个关键状态。
    /// </summary>
    [TestFixture]
    public sealed class OnlineCopyButtonImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator OnlineCopyButton_ShouldCaptureJoinedStates()
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
            Assert.That(uiDocument, Is.Not.Null, "UnifiedSettingsPanelDriver GameObject 必须挂 UIDocument。");

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

            // 把 Model 切到"已加入房间"，让 osp-room-card 显示出复制按钮。
            // OnlineSettingsPanelController 订阅了 IsInRoom，会自动 RefreshCardState。
            IRoomModel roomModel = APP.Pomodoro.GameApp.Interface.GetModel<IRoomModel>();
            roomModel.SetRoomCode("ROOM-001");
            roomModel.SetConnectionFlags(true, true);
            roomModel.SetStatus(ConnectionStatus.InRoom);

            yield return WaitUntilRoomCardVisible(uiDocument.rootVisualElement, 60);

            // Step 1：默认"复制"文本态——验证 22×22 的 icon 容器被替换为
            // 合适的药丸按钮，"复制"两个汉字完整显示且不溢出。
            // 截当前打开的整个设置面板（sidebar + 当前 tab 内容），与
            // UnifiedSettingsPanelImageValidationTests 保持一致。
            yield return CaptureOverlay(
                uiDocument.rootVisualElement,
                "online-room-copy-default",
                $"{BaselineDirectory}/online-room-copy-default.png");

            // Step 2：切到"已复制"3 字态——直接改 Button.text 触发布局，
            // 避免触发真实的 GUIUtility.systemCopyBuffer 副作用。
            Button copyBtn = uiDocument.rootVisualElement.Q<Button>("osp-copy-btn");
            Assert.That(copyBtn, Is.Not.Null, "osp-copy-btn 必须存在。");
            copyBtn.text = "已复制";
            yield return WaitForFrames(2);

            yield return CaptureOverlay(
                uiDocument.rootVisualElement,
                "online-room-copy-copied",
                $"{BaselineDirectory}/online-room-copy-copied.png");

            // 清理：避免污染后续测试或下一次进入面板时保留"已加入"状态
            roomModel.ResetRoomState();
            roomModel.SetConnectionFlags(false, false);
            roomModel.SetStatus(ConnectionStatus.Disconnected);

            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(2), "应登记两个 capture step。");
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "01-online-room-copy-default-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "02-online-room-copy-copied-actual.png")), Is.True);

            string manifestPath = Path.Combine(CurrentRunDirectory, "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True, "manifest.json 必须生成。");

            string manifestJson = File.ReadAllText(manifestPath);
            VisualImageTestRunManifest manifest = JsonUtility.FromJson<VisualImageTestRunManifest>(manifestJson);
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.steps, Is.Not.Null);
            Assert.That(manifest.steps.Count, Is.EqualTo(2));

            AssertManifestStep(
                manifest.steps[0],
                "online-room-copy-default",
                "01-online-room-copy-default-actual.png",
                $"{BaselineDirectory}/online-room-copy-default.png");
            AssertManifestStep(
                manifest.steps[1],
                "online-room-copy-copied",
                "02-online-room-copy-copied-actual.png",
                $"{BaselineDirectory}/online-room-copy-copied.png");
#endif
        }

        private IEnumerator CaptureOverlay(
            VisualElement root,
            string stepName,
            string baselinePath)
        {
            VisualElement overlay = root.Q<VisualElement>("settings-overlay");
            Assert.That(overlay, Is.Not.Null, "settings-overlay 必须存在。");
            Assert.That(overlay.worldBound.width, Is.GreaterThan(0f));
            Assert.That(overlay.worldBound.height, Is.GreaterThan(0f));

            // 项目约定：所有视觉测试都用 full-screen 截图，确保上下文（侧栏 / 标题 / 同级控件）完整入画。
            yield return CaptureScreenStep(stepName, baselinePath, "full-screen");
        }

        private static IEnumerator WaitUntilRoomCardVisible(VisualElement root, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement roomCard = root.Q<VisualElement>("osp-room-card");
                VisualElement copyBtn = root.Q<VisualElement>("osp-copy-btn");

                bool visible = roomCard != null
                    && !roomCard.ClassListContains("osp-hidden")
                    && roomCard.resolvedStyle.display == DisplayStyle.Flex
                    && roomCard.worldBound.width > 0f
                    && roomCard.worldBound.height > 0f
                    && copyBtn != null
                    && copyBtn.worldBound.width > 0f;

                if (visible)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("等待 osp-room-card 显示超时。");
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

        private static void InvokePrivate(object target, string methodName, string argument)
        {
            // SelectTab 是 public，其它可能是 private；同时放宽两种可见性。
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            MethodInfo method = target.GetType().GetMethod(methodName, flags);
            Assert.That(method, Is.Not.Null, $"方法不存在：{methodName}");
            method.Invoke(target, new object[] { argument });
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
    }
}
