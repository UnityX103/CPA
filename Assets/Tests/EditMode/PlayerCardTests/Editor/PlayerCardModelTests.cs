using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Tests.PlayerCardTests
{
    public sealed class PlayerCardModelTests
    {
        private sealed class TestArchitecture : Architecture<TestArchitecture>
        {
            protected override void Init()
            {
                // 使用 InMemoryStorageUtility，避免污染 PlayerPrefs
                RegisterUtility<IStorageUtility>(new InMemoryStorageUtility());
                RegisterModel<IPlayerCardModel>(new PlayerCardModel());
            }
        }

        // 每个测试用同一个架构实例（SetUp 重建，TearDown 销毁）
        private IArchitecture _arch;
        private IPlayerCardModel _model;

        [SetUp]
        public void SetUp()
        {
            // 先清除上一次残留的静态实例（防止 TearDown 未能清理）
            var existing = typeof(TestArchitecture)
                .GetField("mArchitecture",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IArchitecture;
            existing?.Deinit();

            // 重新初始化
            _arch = TestArchitecture.Interface;
            _model = _arch.GetModel<IPlayerCardModel>();
        }

        [TearDown]
        public void TearDown()
        {
            _arch?.Deinit();
            _arch = null;
            _model = null;
        }

        [Test]
        public void AddOrGet_NewPlayer_ReturnsDefaultInstance()
        {
            var card = _model.AddOrGet("p1");

            Assert.IsNotNull(card);
            Assert.AreEqual("p1", card.PlayerId);
            Assert.AreEqual(Vector2.zero, card.Position.Value);
            Assert.IsFalse(card.IsPinned.Value);
        }

        [Test]
        public void AddOrGet_SamePlayerTwice_ReturnsSameInstance()
        {
            var a = _model.AddOrGet("p1");
            var b = _model.AddOrGet("p1");

            Assert.AreSame(a, b);
            Assert.AreEqual(1, _model.Cards.Count);
        }

        [Test]
        public void Remove_InstanceDropsFromCards()
        {
            _model.AddOrGet("p1");

            _model.Remove("p1");

            Assert.AreEqual(0, _model.Cards.Count);
            Assert.IsNull(_model.Find("p1"));
        }

        [Test]
        public void Remove_ThenAddOrGet_RestoresLastValues()
        {
            var card = _model.AddOrGet("p1");
            card.Position.Value = new Vector2(123f, 456f);
            card.IsPinned.Value = true;
            _model.Remove("p1");

            var restored = _model.AddOrGet("p1");

            Assert.AreEqual(new Vector2(123f, 456f), restored.Position.Value);
            Assert.IsTrue(restored.IsPinned.Value);
        }

        [Test]
        public void BindableChange_WritesStoreImmediately()
        {
            var card = _model.AddOrGet("p1");

            card.IsPinned.Value = true;
            _model.Remove("p1");

            var restored = _model.AddOrGet("p1");
            Assert.IsTrue(restored.IsPinned.Value);
        }

        [Test]
        public void AddOrGet_RaisesPlayerCardAdded()
        {
            // Architecture.SendEvent 走架构内部 TypeEventSystem，不走 TypeEventSystem.Global
            // 必须通过架构实例的 RegisterEvent 订阅
            string received = null;
            _arch.RegisterEvent<E_PlayerCardAdded>(e => received = e.PlayerId);

            _model.AddOrGet("p1");

            Assert.AreEqual("p1", received);
        }

        [Test]
        public void Remove_RaisesPlayerCardRemoved()
        {
            // Architecture.SendEvent 走架构内部 TypeEventSystem，不走 TypeEventSystem.Global
            // 必须通过架构实例的 RegisterEvent 订阅
            _model.AddOrGet("p1");
            string received = null;
            _arch.RegisterEvent<E_PlayerCardRemoved>(e => received = e.PlayerId);

            _model.Remove("p1");

            Assert.AreEqual("p1", received);
        }
    }
}
