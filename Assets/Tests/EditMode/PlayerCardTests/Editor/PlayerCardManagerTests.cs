using System.Text.RegularExpressions;
using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardManagerTests
    {
        private VisualElement _cardLayer;
        private VisualTreeAsset _template;

        [SetUp]
        public void SetUp()
        {
            _cardLayer = new VisualElement { style = { width = 1920, height = 1080 } };
            _template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI_V2/Documents/PlayerCard.uxml");
            Assert.That(_template, Is.Not.Null, "PlayerCard.uxml 必须存在");

            // 清空持久化位置，避免跨用例污染
            PlayerPrefs.DeleteKey("CPA.PlayerCardPositions");
            var posModel = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            posModel.Remove("p1"); posModel.Remove("p2"); posModel.Remove("p3");
        }

        [TearDown]
        public void TearDown()
        {
            var posModel = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            posModel.Remove("p1"); posModel.Remove("p2"); posModel.Remove("p3");
            _cardLayer = null;
        }

        [Test]
        public void FirstCard_PlacedAtFixedAnchor_40_40()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            mgr.AddOrUpdate(NewPlayer("p1", "Alice"));

            var card = mgr.Cards["p1"].Root;
            Assert.That(card.style.left.value.value, Is.EqualTo(40f));
            Assert.That(card.style.top.value.value,  Is.EqualTo(40f));
        }

        [Test]
        public void SecondCard_PlacedToRightOfFirst_WithGap12()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            mgr.AddOrUpdate(NewPlayer("p1", "A"));
            mgr.AddOrUpdate(NewPlayer("p2", "B"));

            var card2 = mgr.Cards["p2"].Root;
            // 期望：x = 40 + 153 + 12 = 205，y = 40
            Assert.That(card2.style.left.value.value, Is.EqualTo(205f));
            Assert.That(card2.style.top.value.value,  Is.EqualTo(40f));
        }

        [Test]
        public void OverflowRightEdge_WrapsToNextLine()
        {
            var narrowLayer = new VisualElement { style = { width = 400, height = 800 } };
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, narrowLayer);
            // layerW=400, 右界=layerW-20=380, 每张 153+12 gap
            // p1 @ (40,40), p2 @ (205,40), p3 候选 x=370 → 370+153=523>380 → 换行 x=40, y=40+113+12=165
            mgr.AddOrUpdate(NewPlayer("p1", "A"));
            mgr.AddOrUpdate(NewPlayer("p2", "B"));
            mgr.AddOrUpdate(NewPlayer("p3", "C"));

            var card3 = mgr.Cards["p3"].Root;
            Assert.That(card3.style.left.value.value, Is.EqualTo(40f));
            Assert.That(card3.style.top.value.value,  Is.EqualTo(40f + 113f + 12f));
        }

        [Test]
        public void ReturningPlayer_RestoresPersistedPosition()
        {
            var posModel = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            posModel.Set("p1", new Vector2(333f, 444f));

            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            mgr.AddOrUpdate(NewPlayer("p1", "Alice"));

            var card = mgr.Cards["p1"].Root;
            Assert.That(card.style.left.value.value, Is.EqualTo(333f));
            Assert.That(card.style.top.value.value,  Is.EqualTo(444f));

            posModel.Remove("p1");
        }

        [Test]
        public void Remove_UnknownPlayer_DoesNotThrow()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            Assert.DoesNotThrow(() => mgr.Remove("unknown"));
        }

        [Test]
        public void AddOrUpdate_NullOrEmptyId_Ignored()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            mgr.AddOrUpdate(null);
            mgr.AddOrUpdate(new RemotePlayerData { PlayerId = null });
            mgr.AddOrUpdate(new RemotePlayerData { PlayerId = string.Empty });
            Assert.That(mgr.Cards, Is.Empty);
        }

        [Test]
        public void AddOrUpdate_WithoutTemplate_SkipsCreation()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(null, _cardLayer);

            LogAssert.Expect(LogType.Error, new Regex("未分配"));
            mgr.AddOrUpdate(NewPlayer("p1", "Alice"));

            Assert.That(mgr.Cards, Is.Empty);
        }

        private static RemotePlayerData NewPlayer(string id, string name) => new RemotePlayerData
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
