using APP.Network.Model;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardManagerTests
    {
        private VisualTreeAsset _template;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            _template = Resources.Load<VisualTreeAsset>("__pc_test_not_existing__");
            // 无法在 EditMode 下 Resources.Load 真实 UXML。
            // 但 PlayerCardManager 在 template/root 任一为空时也允许 InitializeForTests 继续运行。
            _root = new VisualElement { name = "test-root" };
        }

        [Test]
        public void AddOrUpdate_WithoutTemplate_SkipsCreation()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_root, null);

            var player = NewPlayer("p1", "Alice");
            manager.AddOrUpdate(player);

            Assert.That(manager.Cards, Is.Empty, "无 template 时应跳过卡片创建");
            Assert.That(manager.CardLayer, Is.Not.Null);
            Assert.That(manager.CardLayer.name, Is.EqualTo("player-card-layer"));
        }

        [Test]
        public void Remove_UnknownPlayerId_DoesNotThrow()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_root, null);
            Assert.DoesNotThrow(() => manager.Remove("unknown-id"));
            Assert.DoesNotThrow(() => manager.Remove(null));
            Assert.DoesNotThrow(() => manager.Remove(string.Empty));
        }

        [Test]
        public void Clear_RemovesAllCards()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_root, null);
            manager.AddOrUpdate(NewPlayer("p1", "A"));
            manager.AddOrUpdate(NewPlayer("p2", "B"));
            manager.Clear();
            Assert.That(manager.Cards, Is.Empty);
        }

        [Test]
        public void CardLayer_UsesIgnorePickingMode()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_root, null);
            Assert.That(manager.CardLayer, Is.Not.Null);
            Assert.That(manager.CardLayer.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        [Test]
        public void AddOrUpdate_NullOrEmptyId_Ignored()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_root, null);
            manager.AddOrUpdate(null);
            manager.AddOrUpdate(new RemotePlayerData { PlayerId = null });
            manager.AddOrUpdate(new RemotePlayerData { PlayerId = string.Empty });
            Assert.That(manager.Cards, Is.Empty);
        }

        [Test]
        public void AddOrUpdate_DuplicatePlayerId_DoesNotCreateNewCard()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_root, CreateEmptyTemplate());

            manager.AddOrUpdate(NewPlayer("p1", "Alice"));

            PlayerCardView firstCard = manager.Cards["p1"];
            int initialChildCount = manager.CardLayer.childCount;

            manager.AddOrUpdate(NewPlayer("p1", "Alice Updated"));

            Assert.That(manager.Cards, Has.Count.EqualTo(1));
            Assert.That(manager.Cards["p1"], Is.SameAs(firstCard));
            Assert.That(manager.CardLayer.childCount, Is.EqualTo(initialChildCount));
        }

        [Test]
        public void Clear_AfterAddOrUpdate_CanMountCardAgain()
        {
            var manager = new PlayerCardManager();
            manager.InitializeForTests(_root, CreateEmptyTemplate());

            manager.AddOrUpdate(NewPlayer("p1", "Alice"));
            manager.Clear();
            manager.AddOrUpdate(NewPlayer("p1", "Alice Again"));

            Assert.That(manager.Cards, Has.Count.EqualTo(1));
            Assert.That(manager.Cards.ContainsKey("p1"), Is.True);
            Assert.That(manager.CardLayer.childCount, Is.EqualTo(1));
            Assert.That(manager.Cards["p1"].Root.parent, Is.SameAs(manager.CardLayer));
        }

        [Test]
        public void InitializeForTests_WithNullTemplate_FallbackConstructDoesNotThrow()
        {
            var manager = new PlayerCardManager();

            Assert.DoesNotThrow(() => manager.InitializeForTests(_root, null));
            Assert.DoesNotThrow(() => manager.AddOrUpdate(NewPlayer("p1", "Alice")));

            Assert.That(manager.CardLayer, Is.Not.Null);
            Assert.That(manager.Cards, Is.Empty);
            Assert.That(manager.CardLayer.childCount, Is.EqualTo(0));
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

        private static VisualTreeAsset CreateEmptyTemplate()
        {
            return ScriptableObject.CreateInstance<VisualTreeAsset>();
        }
    }
}
