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
            /// <summary>
            /// 若外部预设了共享 Utility，Init 会复用它；否则每次新建。
            /// 用于 BindableChange_PersistsThroughOnInit 测试中两个 Model 实例共享同一存储。
            /// </summary>
            public static IStorageUtility SharedStorage;

            protected override void Init()
            {
                // 若外部预设了共享 Utility，使用之；否则每次 Init 新建
                RegisterUtility<IStorageUtility>(SharedStorage ?? new InMemoryStorageUtility());
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
            // 清理共享 Utility，防止污染后续测试
            TestArchitecture.SharedStorage = null;
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
            int addedCount = 0;
            _arch.RegisterEvent<E_PlayerCardAdded>(_ => addedCount++);

            var a = _model.AddOrGet("p1");
            var b = _model.AddOrGet("p1");

            Assert.AreSame(a, b);
            Assert.AreEqual(1, _model.Cards.Count);
            Assert.AreEqual(1, addedCount, "重复 AddOrGet 同一 id 不应重发 E_PlayerCardAdded");
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
        public void BindableChange_PersistsThroughOnInit()
        {
            // Step 1: 用共享 Utility 让第一份 Model 写入，并触发持久化
            var storage = new InMemoryStorageUtility();
            TestArchitecture.SharedStorage = storage;

            // 当前 _arch 是在 SetUp 中用默认（非共享）Utility 初始化的，
            // 需要 Deinit 后重新 Init 以使用 SharedStorage
            _arch.Deinit();
            _arch = TestArchitecture.Interface;
            _model = _arch.GetModel<IPlayerCardModel>();

            var card = _model.AddOrGet("p1");
            card.Position.Value = new Vector2(77f, 88f);
            card.IsPinned.Value = true;

            // Step 2: Deinit 掉当前 Architecture，用同一个 storage 重新 Init 第二份 Model
            // SharedStorage 仍指向同一个实例，Init 会复用它，OnInit 走 LoadString → FromJson 路径
            (TestArchitecture.Interface as IArchitecture).Deinit();
            var m2 = TestArchitecture.Interface.GetModel<IPlayerCardModel>();
            var restored = m2.AddOrGet("p1");

            Assert.AreEqual(new Vector2(77f, 88f), restored.Position.Value);
            Assert.IsTrue(restored.IsPinned.Value);

            // 更新 _arch/_model 引用，使 TearDown 能正确 Deinit
            _arch = TestArchitecture.Interface;
            _model = m2;
            TestArchitecture.SharedStorage = null; // 提前清理
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
