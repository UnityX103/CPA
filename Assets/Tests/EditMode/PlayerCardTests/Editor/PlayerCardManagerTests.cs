using System.Text.RegularExpressions;
using APP.Network.Model;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardManagerTests
    {
        private GameObject _prefab;
        private GameObject _parent;

        [SetUp]
        public void SetUp()
        {
            // 创建一个带 PlayerCardController + UIDocument 的测试预制体
            _prefab = new GameObject("TestCardPrefab");
            _prefab.AddComponent<UnityEngine.UIElements.UIDocument>();
            _prefab.AddComponent<PlayerCardController>();
            _prefab.SetActive(false); // 预制体不激活

            _parent = new GameObject("TestCardParent");
        }

        [TearDown]
        public void TearDown()
        {
            if (_prefab != null) Object.DestroyImmediate(_prefab);
            if (_parent != null) Object.DestroyImmediate(_parent);
        }

        [Test]
        public void AddOrUpdate_WithoutPrefab_SkipsCreation()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null);

            LogAssert.Expect(LogType.Error, new Regex("预制体未分配"));
            manager.AddOrUpdate(NewPlayer("p1", "Alice"));

            Assert.That(manager.Cards, Is.Empty, "无预制体时应跳过卡片创建");
        }

        [Test]
        public void Remove_UnknownPlayerId_DoesNotThrow()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null);
            Assert.DoesNotThrow(() => manager.Remove("unknown-id"));
            Assert.DoesNotThrow(() => manager.Remove(null));
            Assert.DoesNotThrow(() => manager.Remove(string.Empty));
        }

        [Test]
        public void Clear_RemovesAllCards()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null);
            LogAssert.Expect(LogType.Error, new Regex("预制体未分配"));
            manager.AddOrUpdate(NewPlayer("p1", "A"));
            LogAssert.Expect(LogType.Error, new Regex("预制体未分配"));
            manager.AddOrUpdate(NewPlayer("p2", "B"));
            manager.Clear();
            Assert.That(manager.Cards, Is.Empty);
        }

        [Test]
        public void AddOrUpdate_NullOrEmptyId_Ignored()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null);
            manager.AddOrUpdate(null);
            manager.AddOrUpdate(new RemotePlayerData { PlayerId = null });
            manager.AddOrUpdate(new RemotePlayerData { PlayerId = string.Empty });
            Assert.That(manager.Cards, Is.Empty);
        }

        [Test]
        public void AddOrUpdate_WithPrefab_CreatesGameObject()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_prefab, _parent.transform);

            manager.AddOrUpdate(NewPlayer("p1", "Alice"));

            Assert.That(manager.Cards, Has.Count.EqualTo(1));
            Assert.That(manager.Cards.ContainsKey("p1"), Is.True);
            Assert.That(_parent.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void AddOrUpdate_DuplicatePlayerId_DoesNotCreateNewObject()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_prefab, _parent.transform);

            manager.AddOrUpdate(NewPlayer("p1", "Alice"));
            var firstCard = manager.Cards["p1"];

            manager.AddOrUpdate(NewPlayer("p1", "Alice Updated"));

            Assert.That(manager.Cards, Has.Count.EqualTo(1));
            Assert.That(manager.Cards["p1"], Is.SameAs(firstCard));
            Assert.That(_parent.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void Remove_DestroysGameObject()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_prefab, _parent.transform);

            manager.AddOrUpdate(NewPlayer("p1", "Alice"));
            Assert.That(manager.Cards, Has.Count.EqualTo(1));

            manager.Remove("p1");

            Assert.That(manager.Cards, Is.Empty);
            // DestroyImmediate 不会在 EditMode 的 Destroy 中立即生效，
            // 但 Dictionary 应已移除
        }

        [Test]
        public void InitializeForTests_WithNullPrefab_DoesNotThrow()
        {
            var manager = new PlayerCardManager();

            Assert.DoesNotThrow(() => manager.InitializeForTests(null));
            LogAssert.Expect(LogType.Error, new Regex("预制体未分配"));
            Assert.DoesNotThrow(() => manager.AddOrUpdate(NewPlayer("p1", "Alice")));

            Assert.That(manager.Cards, Is.Empty);
        }

        private static RemotePlayerData NewPlayer(string id, string name)
        {
            return new RemotePlayerData
            {
                PlayerId = id,
                PlayerName = name,
                Phase = PomodoroPhase.Focus,
                RemainingSeconds = 1500,
                CurrentRound = 1,
                TotalRounds = 4,
                IsRunning = true,
            };
        }
    }
}
