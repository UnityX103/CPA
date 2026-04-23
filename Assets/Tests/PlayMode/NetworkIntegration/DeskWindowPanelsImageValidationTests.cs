using System;
using System.Collections;
using System.IO;
using System.Reflection;
using APP.Network.Model;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
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
    /// <summary>
    /// 对 DeskWindow 中的两张面板做视觉快照：
    ///   - 番茄钟主面板 #pomodoro-panel（来自 PomodoroPanel.uxml）
    ///   - 玩家卡片 pc-root（PlayerCard.uxml，通过 PlayerCardManager.AddOrUpdate 注入测试玩家）
    /// 对应 Pencil 节点：YRqeB (pomodoroPanel) / drqFB (PlayerCard)
    /// </summary>
    [TestFixture]
    public sealed class DeskWindowPanelsImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator DeskWindowPanels_ShouldCaptureExpectedStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            // PackageManager / 网络认证偶发错误日志不应打断视觉测试
            LogAssert.ignoreFailingMessages = true;

            yield return EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

            yield return WaitForFrames(10);

            DeskWindowController desk =
                UnityEngine.Object.FindFirstObjectByType<DeskWindowController>();
            Assert.That(desk, Is.Not.Null, "MainV2 场景中必须存在 DeskWindowController。");

            UIDocument uiDocument = desk.GetComponent<UIDocument>();
            Assert.That(uiDocument, Is.Not.Null, "DeskWindowController GameObject 必须挂 UIDocument。");

            VisualElement root = uiDocument.rootVisualElement;

            // 1) 番茄钟主面板：pp-root 走 position:absolute，TemplateContainer bound=0，
            //    必须截内层 pp-root 才有实际宽高。
            yield return WaitUntilReady(root, "pp-root", 60);
            VisualElement pomodoroRoot = root.Q<VisualElement>("pp-root");
            Assert.That(pomodoroRoot, Is.Not.Null, "pp-root 必须存在。");

            // 等 TextMeshPro/SDF 字体完成加载 + 多帧渲染 pass 完成
            yield return WaitForFrames(30);

            yield return CaptureScaled(
                "pomodoro-panel",
                pomodoroRoot,
                $"{BaselineDirectory}/pomodoro-panel-baseline.png",
                "YRqeB pomodoroPanel");

            // 2) 玩家卡片 —— 注入一个测试玩家，等 PlayerCardManager 落出 pc-root
            PlayerCardManager manager = GetPlayerCardManager(desk);
            Assert.That(manager, Is.Not.Null, "PlayerCardManager 必须已初始化。");

            RemotePlayerData player = new RemotePlayerData
            {
                PlayerId = "visual-test-player",
                PlayerName = "远端玩家",
                Phase = PomodoroPhase.Focus,
                RemainingSeconds = 1458,
                CurrentRound = 3,
                TotalRounds = 4,
                IsRunning = true,
                ActiveAppBundleId = "com.microsoft.VSCode",
            };
            manager.AddOrUpdate(player);

            yield return WaitUntilCardReady(manager, player.PlayerId, 120);
            VisualElement cardRoot = manager.Cards[player.PlayerId].Root;
            Assert.That(cardRoot, Is.Not.Null, "pc-root 应已被 PlayerCardManager 创建。");

            // 等 SDF 字体/图标异步加载 + 多帧渲染 pass 完成
            yield return WaitForFrames(30);

            yield return CaptureScaled(
                "player-card",
                cardRoot,
                $"{BaselineDirectory}/player-card-baseline.png",
                "drqFB PlayerCard");

            // 清理：移除测试玩家，避免污染后续测试状态
            manager.Remove(player.PlayerId);

            // 3) 产物完整性检查
            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(2), "应登记两个 capture step。");
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "01-pomodoro-panel-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "02-player-card-actual.png")), Is.True);
            string manifestPath = Path.Combine(CurrentRunDirectory, "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True);

            string manifestJson = File.ReadAllText(manifestPath);
            VisualImageTestRunManifest manifest = JsonUtility.FromJson<VisualImageTestRunManifest>(manifestJson);

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.steps, Is.Not.Null);
            Assert.That(manifest.steps.Count, Is.EqualTo(2));
            AssertManifestStep(
                manifest.steps[0],
                "pomodoro-panel",
                "01-pomodoro-panel-actual.png",
                $"{BaselineDirectory}/pomodoro-panel-baseline.png");
            AssertManifestStep(
                manifest.steps[1],
                "player-card",
                "02-player-card-actual.png",
                $"{BaselineDirectory}/player-card-baseline.png");
#endif
        }

        /// <summary>
        /// worldBound 是面板内部坐标系（ReferenceResolution），而 ScreenCapture 按真实屏幕像素取图。
        /// 需要先按 panel→screen 比例缩放，再调 <see cref="VisualTestImageUtility.CaptureScreenRegionToFile"/>，
        /// 产出的尺寸才能跟 baseline（Pencil 导出，已是 2x panel 像素）对齐。
        /// </summary>
        private IEnumerator CaptureScaled(
            string stepName,
            VisualElement target,
            string baselinePath,
            string notes)
        {
            Assert.That(target, Is.Not.Null, "CaptureScaled: target 为空。");
            Assert.That(target.panel, Is.Not.Null, "CaptureScaled: target.panel 为空，元素未挂到 UI。");

            Rect panelRoot = target.panel.visualTree.worldBound;
            float scaleX = panelRoot.width > 0 ? Screen.width / panelRoot.width : 1f;
            float scaleY = panelRoot.height > 0 ? Screen.height / panelRoot.height : 1f;
            Rect wb = target.worldBound;
            Rect screenRect = new Rect(wb.x * scaleX, wb.y * scaleY, wb.width * scaleX, wb.height * scaleY);

            yield return new WaitForEndOfFrame();

            RectInt region = VisualTestImageUtility.CreateScreenRegionFromTopLeftRect(
                screenRect, Screen.width, Screen.height, 0);
            Assert.That(region.width, Is.GreaterThan(0), "截图区域宽度必须大于 0。");
            Assert.That(region.height, Is.GreaterThan(0), "截图区域高度必须大于 0。");

            // 借用 VisualImageTestBase 的私有 _stepIndex / _currentManifest 完成步骤登记
            FieldInfo stepIndexField = typeof(VisualImageTestBase).GetField(
                "_stepIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo manifestField = typeof(VisualImageTestBase).GetField(
                "_currentManifest", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo saveManifestMethod = typeof(VisualImageTestBase).GetMethod(
                "SaveManifest", BindingFlags.Instance | BindingFlags.NonPublic);

            int next = (int)stepIndexField.GetValue(this) + 1;
            stepIndexField.SetValue(this, next);

            string fileName = VisualTestImageUtility.BuildStepArtifactFileName(next, stepName, "actual");
            string outputPath = Path.Combine(CurrentRunDirectory, fileName);
            VisualTestImageUtility.CaptureScreenRegionToFile(outputPath, region);

            var manifest = manifestField.GetValue(this) as VisualImageTestRunManifest;
            manifest.steps.Add(new VisualImageTestStepManifest
            {
                index = next,
                name = stepName,
                actualImagePath = fileName,
                baselineImagePath = baselinePath,
                notes = notes,
            });
            saveManifestMethod.Invoke(this, Array.Empty<object>());
        }

        private static PlayerCardManager GetPlayerCardManager(DeskWindowController desk)
        {
            FieldInfo field = typeof(DeskWindowController).GetField("_playerCardManager", InstancePrivate);
            Assert.That(field, Is.Not.Null, "DeskWindowController._playerCardManager 字段缺失。");
            return field.GetValue(desk) as PlayerCardManager;
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

        private static IEnumerator WaitUntilReady(VisualElement root, string elementName, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement target = root.Q<VisualElement>(elementName);
                bool isReady = target != null
                    && target.resolvedStyle.display == DisplayStyle.Flex
                    && target.worldBound.width > 0f
                    && target.worldBound.height > 0f;

                if (isReady)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"等待元素就绪超时：{elementName}");
        }

        private static IEnumerator WaitUntilCardReady(
            PlayerCardManager manager, string playerId, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (manager.Cards.TryGetValue(playerId, out var ctrl) && ctrl.Root != null)
                {
                    VisualElement card = ctrl.Root;
                    bool isReady = card.resolvedStyle.display == DisplayStyle.Flex
                        && card.worldBound.width > 0f
                        && card.worldBound.height > 0f;

                    if (isReady)
                    {
                        yield break;
                    }
                }

                yield return null;
            }

            if (manager.Cards.TryGetValue(playerId, out var last))
            {
                VisualElement card = last.Root;
                Debug.LogError(
                    $"[VisualTest] pc-root 状态：display={card.resolvedStyle.display}, " +
                    $"worldBound={card.worldBound}, parent={card.parent?.name}");
                Assert.Fail($"等待 pc-root 就绪超时：display={card.resolvedStyle.display}, worldBound={card.worldBound}");
            }
            Assert.Fail("等待 pc-root 就绪超时：manager.Cards 未登记。");
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
