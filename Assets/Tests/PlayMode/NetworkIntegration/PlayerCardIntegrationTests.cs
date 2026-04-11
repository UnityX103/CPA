using System.Collections;
using System.Collections.Generic;
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
    /// 验证 PlayerCardManager 正确创建/更新/移除 PlayerCard 预制体实例。
    /// 不需要真实 WebSocket 连接，通过手动调用 AddOrUpdate/Remove 驱动。
    /// </summary>
    [TestFixture]
    public sealed class PlayerCardIntegrationTests
    {
        private PlayerCardManager _manager;
        private GameObject _prefab;
        private GameObject _parent;

        [SetUp]
        public void SetUp()
        {
            // 创建测试预制体（含 UIDocument + PlayerCardController）
            _prefab = new GameObject("TestCardPrefab");
            _prefab.AddComponent<UIDocument>();
            _prefab.AddComponent<PlayerCardController>();
            _prefab.SetActive(false);

            _parent = new GameObject("TestCardParent");

            _manager = new PlayerCardManager();
            _manager.InitializeForTests(_prefab, _parent.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Clear();
            if (_prefab != null) Object.Destroy(_prefab);
            if (_parent != null) Object.Destroy(_parent);
        }

        // ─── 场景 1：远程玩家加入，预制体被实例化 ────────────

        [UnityTest]
        public IEnumerator RemotePlayerJoin_CreatesPlayerCard()
        {
            var player = CreatePlayer("remote-1", "小明", PomodoroPhase.Focus, 1500, 1, 4, true);
            _manager.AddOrUpdate(player);

            yield return null;

            Assert.That(_manager.Cards.ContainsKey("remote-1"), Is.True, "应创建玩家卡片");
            Assert.That(_manager.Cards.Count, Is.EqualTo(1));
            Assert.That(_parent.transform.childCount, Is.EqualTo(1));
        }

        // ─── 场景 2：多个玩家同时加入 ────────────────────────

        [UnityTest]
        public IEnumerator MultiplePlayersJoin_CreatesMultipleCards()
        {
            _manager.AddOrUpdate(CreatePlayer("r1", "小明", PomodoroPhase.Focus, 1200, 1, 4, true));
            _manager.AddOrUpdate(CreatePlayer("r2", "小红", PomodoroPhase.Break, 300, 2, 4, true));
            _manager.AddOrUpdate(CreatePlayer("r3", "小刚", PomodoroPhase.Completed, 0, 4, 4, false));

            yield return null;

            Assert.That(_manager.Cards.Count, Is.EqualTo(3), "应创建 3 个卡片实例");
            Assert.That(_parent.transform.childCount, Is.EqualTo(3));
            Assert.That(_manager.Cards.ContainsKey("r1"), Is.True);
            Assert.That(_manager.Cards.ContainsKey("r2"), Is.True);
            Assert.That(_manager.Cards.ContainsKey("r3"), Is.True);
        }

        // ─── 场景 3：状态变化，卡片数据刷新（不新建） ────────

        [UnityTest]
        public IEnumerator StateUpdate_RefreshesExistingCard()
        {
            _manager.AddOrUpdate(CreatePlayer("remote-1", "小明", PomodoroPhase.Focus, 1500, 1, 4, true));
            yield return null;

            _manager.AddOrUpdate(CreatePlayer("remote-1", "小明", PomodoroPhase.Focus, 1200, 1, 4, true));
            yield return null;

            Assert.That(_manager.Cards.Count, Is.EqualTo(1), "应保持 1 个实例（更新而非新建）");
            Assert.That(_parent.transform.childCount, Is.EqualTo(1));
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

        // ─── 场景 5：玩家离开，实例被销毁 ────────────────────

        [UnityTest]
        public IEnumerator PlayerLeave_RemovesCard()
        {
            _manager.AddOrUpdate(CreatePlayer("r1", "小明", PomodoroPhase.Focus, 1500, 1, 4, true));
            _manager.AddOrUpdate(CreatePlayer("r2", "小红", PomodoroPhase.Break, 300, 2, 4, true));
            yield return null;

            Assert.That(_manager.Cards.Count, Is.EqualTo(2));

            _manager.Remove("r1");
            yield return null;

            Assert.That(_manager.Cards.Count, Is.EqualTo(1));
            Assert.That(_manager.Cards.ContainsKey("r1"), Is.False, "小明的卡片应被移除");
            Assert.That(_manager.Cards.ContainsKey("r2"), Is.True, "小红的卡片应保留");
        }

        // ─── 场景 6：全部离开后清空 ─────────────────────────

        [UnityTest]
        public IEnumerator ClearAll_RemovesAllCards()
        {
            _manager.AddOrUpdate(CreatePlayer("r1", "A", PomodoroPhase.Focus, 1500, 1, 4, true));
            _manager.AddOrUpdate(CreatePlayer("r2", "B", PomodoroPhase.Break, 300, 2, 4, true));
            _manager.AddOrUpdate(CreatePlayer("r3", "C", PomodoroPhase.Completed, 0, 4, 4, false));
            yield return null;

            Assert.That(_manager.Cards.Count, Is.EqualTo(3));

            _manager.Clear();
            yield return null;

            Assert.That(_manager.Cards.Count, Is.EqualTo(0));
        }

        // ─── 场景 7：完整生命周期 ───────────────────────────

        [UnityTest]
        public IEnumerator FullLifecycle_JoinUpdateLeave()
        {
            // 1. 加入
            _manager.AddOrUpdate(CreatePlayer("remote-1", "小明", PomodoroPhase.Focus, 1500, 1, 4, true));
            yield return null;
            Assert.That(_manager.Cards.ContainsKey("remote-1"), Is.True);

            // 2. 多次状态更新
            for (int remaining = 1499; remaining >= 1495; remaining--)
            {
                _manager.AddOrUpdate(CreatePlayer("remote-1", "小明", PomodoroPhase.Focus, remaining, 1, 4, true));
            }
            yield return null;
            Assert.That(_manager.Cards.Count, Is.EqualTo(1), "多次更新不应创建新实例");

            // 3. 阶段切换
            _manager.AddOrUpdate(CreatePlayer("remote-1", "小明", PomodoroPhase.Break, 300, 1, 4, true));
            yield return null;
            Assert.That(_manager.Cards.Count, Is.EqualTo(1));

            // 4. 完成
            _manager.AddOrUpdate(CreatePlayer("remote-1", "小明", PomodoroPhase.Completed, 0, 4, 4, false));
            yield return null;

            // 5. 离开
            _manager.Remove("remote-1");
            yield return null;
            Assert.That(_manager.Cards.Count, Is.EqualTo(0));
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
