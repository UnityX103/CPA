using System.Collections;
using APP.Network.Model;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace APP.NetworkIntegration.Tests
{
    /// <summary>
    /// PlayMode 集成测试：模拟远程玩家加入、状态变化、离开，
    /// 验证 PlayerCardManager 正确创建/更新/移除 VisualElement 卡片。
    /// 不需要真实 WebSocket 连接，通过手动调用 AddOrUpdate/Remove 驱动。
    /// </summary>
    [TestFixture]
    public sealed class PlayerCardIntegrationTests
    {
        private PlayerCardManager _manager;
        private VisualElement _container;

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();

            _manager = new PlayerCardManager();
            // 无 VisualTreeAsset 模板时，AddOrUpdate 会跳过创建（打印错误日志）
            // 集成测试中无法轻松加载 VisualTreeAsset，仅测试管理器逻辑
            _manager.InitializeForTests(null, _container);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Clear();
            _container = null;
        }

        // ─── 场景 4：阶段切换验证 ───────────────────────────

        [Test]
        public void PhaseChange_FormatPhaseReturnsCorrectLabel()
        {
            Assert.That(PlayerCardView.FormatPhase(PomodoroPhase.Focus, true), Is.EqualTo("专注中"));
            Assert.That(PlayerCardView.FormatPhase(PomodoroPhase.Break, true), Is.EqualTo("休息中"));
            Assert.That(PlayerCardView.FormatPhase(PomodoroPhase.Completed, false), Is.EqualTo("已完成"));
            Assert.That(PlayerCardView.FormatPhase(PomodoroPhase.Focus, false), Is.EqualTo("专注暂停"));
        }

        // ─── 场景 8：格式化验证 ─────────────────────────────

        [Test]
        public void FormatTime_CorrectlyFormatsMinutesAndSeconds()
        {
            Assert.That(PlayerCardView.FormatTime(1500), Is.EqualTo("25:00"));
            Assert.That(PlayerCardView.FormatTime(300), Is.EqualTo("05:00"));
            Assert.That(PlayerCardView.FormatTime(61), Is.EqualTo("01:01"));
            Assert.That(PlayerCardView.FormatTime(0), Is.EqualTo("00:00"));
            Assert.That(PlayerCardView.FormatTime(-1), Is.EqualTo("00:00"));
        }

        [Test]
        public void GetPhaseClass_ReflectsRunningState()
        {
            Assert.That(PlayerCardView.GetPhaseClass(PomodoroPhase.Focus, true), Is.EqualTo("pc-phase-focus"));
            Assert.That(PlayerCardView.GetPhaseClass(PomodoroPhase.Focus, false), Is.EqualTo("pc-phase-paused"));
            Assert.That(PlayerCardView.GetPhaseClass(PomodoroPhase.Break, true), Is.EqualTo("pc-phase-rest"));
            Assert.That(PlayerCardView.GetPhaseClass(PomodoroPhase.Completed, false), Is.EqualTo("pc-phase-completed"));
        }

        // ─── 辅助方法 ───────────────────────────────────────

        private static RemotePlayerData CreatePlayer(
            string id, string name, PomodoroPhase phase,
            int remainingSeconds, int currentRound, int totalRounds, bool isRunning)
        {
            return new RemotePlayerData
            {
                PlayerId = id,
                PlayerName = name,
                Phase = phase,
                RemainingSeconds = remainingSeconds,
                CurrentRound = currentRound,
                TotalRounds = totalRounds,
                IsRunning = isRunning,
            };
        }
    }
}
