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
    /// 远端玩家按键计数同步链路视觉回归。
    ///
    /// 复刻"本地服务器"工作流：用 PlayerCardManager.AddOrUpdate 直接以 RemotePlayerData
    /// 喂卡片管理器，等价于 NetworkSimulatorWindow 的"本地事件注入"模式，
    /// 也等价于真实 WebSocket 链路 NetworkSystem.HandleStateUpdated 收到 player_state_broadcast 后调到的位置。
    /// 验证 PlayerCardController.ApplyBindingKey 在 BindingKeyLabel / BindingPressCount 变化时正确切换 KeyCounterPill：
    ///   Step 1：带 Space/47 按键加入 → pill 可见 + count="47"
    ///   Step 2：按下次数到 100 → count Label = "99+"（>99 收敛防止溢出 pill 边界）
    ///   Step 3：清空按键 → pill 加 .pc-key-counter-pill--hidden 类（display:none）
    ///   Step 4：恢复 keyLabel="F" + pressCount=0 → pill 重新可见、count="0"（codex D 点：隐藏恢复路径 + pressCount=0 边界）
    ///   Step 5：keyLabel="鼠标左键" → keyBadge 加 .comp-key-counter-pill__badge--mouse-left class（codex D 点：鼠标键图标分支）
    /// 用户反馈"远端玩家面板看不到按键计数"以此回归。
    /// </summary>
    [TestFixture]
    public sealed class RemotePlayerBindingKeyImageValidationTests : VisualImageTestBase
    {
        private const string ScenePath = "Assets/Scenes/MainV2.unity";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string PlayerId = "binding-key-sync-test-player";
        private const string PillHiddenClass = "pc-key-counter-pill--hidden";

        [UnityTest]
        public IEnumerator RemoteBindingKey_ShouldDisplayAndUpdateAndHide()
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
            Assert.That(uiDocument, Is.Not.Null);

            VisualElement root = uiDocument.rootVisualElement;
            yield return WaitUntilReady(root, "pp-root", 60);
            yield return WaitForFrames(10);

            PlayerCardManager manager = GetPlayerCardManager(desk);
            Assert.That(manager, Is.Not.Null);

            // ─ Step 1：带 Space/47 按键加入 ─
            yield return AddOrRefresh(manager, "远端玩家", "Space", 47);
            yield return WaitUntilCardReady(manager, PlayerId, 120);
            VisualElement cardRoot = manager.Cards[PlayerId].Root;
            VisualElement pill = cardRoot.Q<VisualElement>("pc-key-counter-pill");
            Label keyLabel = cardRoot.Q<Label>("key-counter-pill-key");
            Label countLabel = cardRoot.Q<Label>("key-counter-pill-count");
            Assert.That(pill, Is.Not.Null, "PlayerCard 必须含 pc-key-counter-pill。");
            Assert.That(keyLabel, Is.Not.Null, "PlayerCard 必须含 key-counter-pill-key Label。");
            Assert.That(countLabel, Is.Not.Null, "PlayerCard 必须含 key-counter-pill-count Label。");

            yield return WaitForFrames(20);
            Assert.That(pill.ClassListContains(PillHiddenClass), Is.False,
                "Step1: 有 BindingKeyLabel 时 pill 不应带 hidden 类。");
            Assert.That(keyLabel.text, Is.EqualTo("Space"),
                "Step1: keyLabel 应为 'Space'，实测 '" + keyLabel.text + "'");
            Assert.That(countLabel.text, Is.EqualTo("47"),
                "Step1: countLabel 应为 '47'，实测 '" + countLabel.text + "'");
            yield return CaptureScreenStep(
                "remote-binding-key-space-47",
                null,
                "full-screen; 远端玩家 PlayerCard 含 KeyCounterPill='Space / 47'");

            // ─ Step 2：按下次数变成 100 → 视觉收敛到 "99"（"99+" 实测仍溢出卡片，参见 PlayerCardController.FormatPressCount 注释）─
            yield return AddOrRefresh(manager, "远端玩家", "Space", 100);
            yield return WaitForFrames(20);
            Assert.That(countLabel.text, Is.EqualTo("99"),
                "Step2: pressCount>=100 应收敛为 '99' 防止 pill 被卡片右边界裁切，实测 '" + countLabel.text + "'");
            Assert.That(countLabel.tooltip, Is.EqualTo("100"),
                "Step2: 视觉收敛后 tooltip 仍应保留真实计数 '100'，实测 '" + countLabel.tooltip + "'");
            Assert.That(pill.ClassListContains(PillHiddenClass), Is.False,
                "Step2: 仅 pressCount 变化 pill 仍应可见。");
            yield return CaptureScreenStep(
                "remote-binding-key-space-cap99",
                null,
                "full-screen; pressCount 100 → 视觉 '99'（tooltip='100'）");

            // ─ Step 3：清空按键 → pill 隐藏 ─
            yield return AddOrRefresh(manager, "远端玩家", "", 0);
            yield return WaitForFrames(20);
            Assert.That(pill.ClassListContains(PillHiddenClass), Is.True,
                "Step3: BindingKeyLabel 为空时 pill 必须带 hidden 类（USS display:none）。");
            yield return CaptureScreenStep(
                "remote-binding-key-cleared",
                null,
                "full-screen; 远端清空按键 → pill 隐藏");

            // ─ Step 4：恢复路径 + pressCount=0 边界 ─
            yield return AddOrRefresh(manager, "远端玩家", "F", 0);
            yield return WaitForFrames(20);
            Assert.That(pill.ClassListContains(PillHiddenClass), Is.False,
                "Step4: 隐藏后再切回 keyLabel 非空，pill 必须重新显示。");
            Assert.That(keyLabel.text, Is.EqualTo("F"),
                "Step4: keyLabel='F' 应渲染。");
            Assert.That(countLabel.text, Is.EqualTo("0"),
                "Step4: pressCount=0 时 count Label 仍应显示 '0'，不能隐藏。");
            yield return CaptureScreenStep(
                "remote-binding-key-restored-zero",
                null,
                "full-screen; 隐藏→恢复后 keyLabel=F count=0 都可见");

            // ─ Step 5：鼠标左键 → badge 切到 mouse-left 图标 class ─
            yield return AddOrRefresh(manager, "远端玩家", "鼠标左键", 8);
            yield return WaitForFrames(20);
            VisualElement badge = cardRoot.Q<VisualElement>("key-counter-pill-badge");
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.ClassListContains("comp-key-counter-pill__badge--mouse"), Is.True,
                "Step5: 鼠标键应启用 --mouse 修饰类。");
            Assert.That(badge.ClassListContains("comp-key-counter-pill__badge--mouse-left"), Is.True,
                "Step5: '鼠标左键' 应启用 --mouse-left 修饰类，让 USS 切到 mouse-left.png 背景图。");
            yield return CaptureScreenStep(
                "remote-binding-key-mouse-left",
                null,
                "full-screen; 鼠标左键 → keyBadge 切到 mouse-left 图标");

            // 清理
            manager.Remove(PlayerId);

            // 产物完整性
            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(5));
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "01-remote-binding-key-space-47-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "02-remote-binding-key-space-cap99-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "03-remote-binding-key-cleared-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "04-remote-binding-key-restored-zero-actual.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "05-remote-binding-key-mouse-left-actual.png")), Is.True);
#endif
        }

#if UNITY_EDITOR
        private static IEnumerator AddOrRefresh(
            PlayerCardManager manager, string playerName, string keyLabel, int pressCount)
        {
            manager.AddOrUpdate(new RemotePlayerData
            {
                PlayerId = PlayerId,
                PlayerName = playerName,
                Phase = PomodoroPhase.Focus,
                RemainingSeconds = 1500,
                CurrentRound = 1,
                TotalRounds = 4,
                IsRunning = true,
                ActiveAppBundleId = "com.microsoft.VSCode",
                ActiveAppName = "VS Code",
                BindingKeyLabel = keyLabel,
                BindingPressCount = pressCount,
            });
            yield return null;
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
                if (target != null
                    && target.resolvedStyle.display == DisplayStyle.Flex
                    && target.worldBound.width > 0f
                    && target.worldBound.height > 0f)
                {
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("等待元素就绪超时：" + elementName);
        }

        private static IEnumerator WaitUntilCardReady(
            PlayerCardManager manager, string playerId, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (manager.Cards.TryGetValue(playerId, out var ctrl) && ctrl.Root != null)
                {
                    VisualElement card = ctrl.Root;
                    if (card.resolvedStyle.display == DisplayStyle.Flex
                        && card.worldBound.width > 0f
                        && card.worldBound.height > 0f)
                    {
                        yield break;
                    }
                }
                yield return null;
            }
            Assert.Fail("等待 pc-root 就绪超时：playerId=" + playerId);
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }
        }
#endif
    }
}
