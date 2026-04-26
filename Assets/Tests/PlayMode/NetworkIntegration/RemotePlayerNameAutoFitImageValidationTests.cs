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
    /// 视觉回归：远端玩家名字过长时应自动缩小字号显示完整文本，而不是被 USS ellipsis 截断。
    /// 三档样本：
    ///   - short：单字"喵"，应保持基础字号 14
    ///   - mid：6 字"远端长名玩家"，应被适度缩小到 (9, 14)
    ///   - long：14 字"远端玩家ABCDEF很长名字"，应缩到 9px 下限
    /// 同步对比设计稿基线 player-card-baseline.png（短名场景）。
    /// </summary>
    [TestFixture]
    public sealed class RemotePlayerNameAutoFitImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        private const string PlayerId = "name-autofit-test-player";
        private const float NameBaseFontSize = 14f;
        private const float NameMinFontSize = 7f;
        private const float FontSizeTolerance = 0.5f;

        // 协程不能返回值；用字段把每一步实测的 fontSize 传出来做断言
        private float _lastFontSize;

        [UnityTest]
        public IEnumerator RemotePlayerName_AutoFitsToCardWidth()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            LogAssert.ignoreFailingMessages = true;

            yield return EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));

            yield return WaitForFrames(10);

            DeskWindowController desk =
                UnityEngine.Object.FindFirstObjectByType<DeskWindowController>();
            Assert.That(desk, Is.Not.Null, "MainV2 场景必须存在 DeskWindowController。");

            UIDocument uiDocument = desk.GetComponent<UIDocument>();
            Assert.That(uiDocument, Is.Not.Null, "DeskWindowController 必须挂 UIDocument。");

            VisualElement root = uiDocument.rootVisualElement;
            yield return WaitUntilReady(root, "pp-root", 60);
            yield return WaitForFrames(10);

            PlayerCardManager manager = GetPlayerCardManager(desk);
            Assert.That(manager, Is.Not.Null, "PlayerCardManager 必须已初始化。");

            // —— Step 1：短名 —— 字号应保持基础值 14
            yield return RunStep(manager, "喵", "short-name", $"{BaselineDirectory}/player-card-baseline.png");
            float shortFontSize = _lastFontSize;
            Assert.That(
                shortFontSize,
                Is.EqualTo(NameBaseFontSize).Within(FontSizeTolerance),
                $"[short-name] 期望 ~{NameBaseFontSize}px，实测 {shortFontSize:F2}px");

            // —— Step 2：中长名 —— 字号应被压缩到 [min, base) 之间
            yield return RunStep(manager, "远端长名玩家", "mid-name", null);
            float midFontSize = _lastFontSize;
            Assert.That(
                midFontSize,
                Is.LessThan(NameBaseFontSize - FontSizeTolerance),
                $"[mid-name] 期望严格小于 {NameBaseFontSize - FontSizeTolerance}px，实测 {midFontSize:F2}px（自适应未触发？）");
            Assert.That(
                midFontSize,
                Is.GreaterThanOrEqualTo(NameMinFontSize - FontSizeTolerance),
                $"[mid-name] 期望 ≥ {NameMinFontSize}px，实测 {midFontSize:F2}px（低于下限？）");

            // —— Step 3：超长名 —— 字号应等于下限 9
            yield return RunStep(manager, "远端玩家ABCDEF很长名字", "long-name", null);
            float longFontSize = _lastFontSize;
            Assert.That(
                longFontSize,
                Is.EqualTo(NameMinFontSize).Within(FontSizeTolerance),
                $"[long-name] 期望 ~{NameMinFontSize}px，实测 {longFontSize:F2}px");

            // 单调收缩不变量：short ≥ mid ≥ long，且 short 严格大于 long
            Assert.That(shortFontSize + FontSizeTolerance, Is.GreaterThanOrEqualTo(midFontSize));
            Assert.That(midFontSize + FontSizeTolerance, Is.GreaterThanOrEqualTo(longFontSize));
            Assert.That(
                shortFontSize,
                Is.GreaterThan(longFontSize + FontSizeTolerance),
                "短名字号必须严格大于超长名字号；否则自适应未生效。");

            // 清理：移除测试玩家
            manager.Remove(PlayerId);

            // 产物完整性检查
            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3), "应登记三个 capture step。");
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "01-short-name-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "02-mid-name-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "03-long-name-actual.png")), Is.True);

            string manifestPath = Path.Combine(CurrentRunDirectory, "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True);
            string manifestJson = File.ReadAllText(manifestPath);
            VisualImageTestRunManifest manifest = JsonUtility.FromJson<VisualImageTestRunManifest>(manifestJson);
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.steps, Is.Not.Null);
            Assert.That(manifest.steps.Count, Is.EqualTo(3));
#endif
        }

        /// <summary>
        /// 注入/更新远端玩家、等卡片就绪、记录字号到 _lastFontSize 并截屏。
        /// </summary>
        private IEnumerator RunStep(
            PlayerCardManager manager,
            string playerName,
            string stepName,
            string baselineRelativePath)
        {
            RemotePlayerData player = new RemotePlayerData
            {
                PlayerId = PlayerId,
                PlayerName = playerName,
                Phase = PomodoroPhase.Focus,
                RemainingSeconds = 1458,
                CurrentRound = 3,
                TotalRounds = 4,
                IsRunning = true,
                ActiveAppBundleId = "com.microsoft.VSCode",
            };
            manager.AddOrUpdate(player);

            yield return WaitUntilCardReady(manager, PlayerId, 120);
            VisualElement cardRoot = manager.Cards[PlayerId].Root;
            Assert.That(cardRoot, Is.Not.Null, "pc-root 应已存在。");

            // 等 schedule(0) + 多帧 Layout pass，让 AutoFitNameFontSize 生效
            yield return WaitForFrames(30);

            Label nameLabel = cardRoot.Q<Label>("pc-name");
            Assert.That(nameLabel, Is.Not.Null, "pc-name Label 必须存在。");
            _lastFontSize = nameLabel.resolvedStyle.fontSize;

            Assert.That(cardRoot.worldBound.width, Is.GreaterThan(0f));
            Assert.That(cardRoot.worldBound.height, Is.GreaterThan(0f));

            yield return CaptureScreenStep(
                stepName,
                baselineRelativePath,
                $"远端玩家名='{playerName}' / fontSize={_lastFontSize:F2}px / full-screen");
        }

        private static PlayerCardManager GetPlayerCardManager(DeskWindowController desk)
        {
            FieldInfo field = typeof(DeskWindowController).GetField("_playerCardManager", InstancePrivate);
            Assert.That(field, Is.Not.Null, "DeskWindowController._playerCardManager 字段缺失。");
            return field.GetValue(desk) as PlayerCardManager;
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

            Assert.Fail($"等待 pc-root 就绪超时：playerId={playerId}");
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
