using System.Text.RegularExpressions;
using APP.Network.Model;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardManagerTests
    {
        private VisualElement _container;

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            _container = null;
        }

        [Test]
        public void AddOrUpdate_WithoutTemplate_SkipsCreation()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null, _container);

            LogAssert.Expect(LogType.Error, new Regex("模板未分配|未分配"));
            manager.AddOrUpdate(NewPlayer("p1", "Alice"));

            Assert.That(manager.Cards, Is.Empty, "无模板时应跳过卡片创建");
        }

        [Test]
        public void Remove_UnknownPlayerId_DoesNotThrow()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null, _container);
            Assert.DoesNotThrow(() => manager.Remove("unknown-id"));
            Assert.DoesNotThrow(() => manager.Remove(null));
            Assert.DoesNotThrow(() => manager.Remove(string.Empty));
        }

        [Test]
        public void Clear_RemovesAllCards()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null, _container);
            LogAssert.Expect(LogType.Error, new Regex("模板未分配|未分配"));
            manager.AddOrUpdate(NewPlayer("p1", "A"));
            LogAssert.Expect(LogType.Error, new Regex("模板未分配|未分配"));
            manager.AddOrUpdate(NewPlayer("p2", "B"));
            manager.Clear();
            Assert.That(manager.Cards, Is.Empty);
        }

        [Test]
        public void AddOrUpdate_NullOrEmptyId_Ignored()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(null, _container);
            manager.AddOrUpdate(null);
            manager.AddOrUpdate(new RemotePlayerData { PlayerId = null });
            manager.AddOrUpdate(new RemotePlayerData { PlayerId = string.Empty });
            Assert.That(manager.Cards, Is.Empty);
        }

        [Test]
        public void InitializeForTests_WithNullTemplate_DoesNotThrow()
        {
            var manager = new PlayerCardManager();

            Assert.DoesNotThrow(() => manager.InitializeForTests(null, _container));
            LogAssert.Expect(LogType.Error, new Regex("模板未分配|未分配"));
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
