using System.Reflection;
using APP.Pomodoro.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardPositionModelTests
    {
        private const string TestKey = "CPA.PlayerCardPositions";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(TestKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(TestKey);
        }

        [Test]
        public void TryGet_UnknownPlayer_ReturnsFalse()
        {
            var model = CreateModel();
            Assert.That(model.TryGet("nobody", out _), Is.False);
        }

        [Test]
        public void Set_ThenTryGet_ReturnsStoredPosition()
        {
            var model = CreateModel();
            model.Set("p1", new Vector2(40f, 40f));

            Assert.That(model.TryGet("p1", out Vector2 pos), Is.True);
            Assert.That(pos, Is.EqualTo(new Vector2(40f, 40f)));
        }

        [Test]
        public void Remove_RemovesEntry()
        {
            var model = CreateModel();
            model.Set("p1", new Vector2(1f, 2f));
            model.Remove("p1");
            Assert.That(model.TryGet("p1", out _), Is.False);
        }

        [Test]
        public void Set_Persists_AcrossModelInstances()
        {
            var m1 = CreateModel();
            m1.Set("alice", new Vector2(100f, 200f));
            PlayerPrefs.Save();

            var m2 = CreateModel();
            Assert.That(m2.TryGet("alice", out Vector2 pos), Is.True);
            Assert.That(pos, Is.EqualTo(new Vector2(100f, 200f)));
        }

        [Test]
        public void GameApp_RegistersModel()
        {
            var model = APP.Pomodoro.GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            Assert.That(model, Is.Not.Null, "GameApp 应注册 IPlayerCardPositionModel");
        }

        [Test]
        public void Cmd_SetPlayerCardPosition_WritesModel()
        {
            var model = APP.Pomodoro.GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            APP.Pomodoro.GameApp.Interface.SendCommand(
                new APP.Pomodoro.Command.Cmd_SetPlayerCardPosition("px", new Vector2(11f, 22f)));
            Assert.That(model.TryGet("px", out Vector2 got), Is.True);
            Assert.That(got, Is.EqualTo(new Vector2(11f, 22f)));
            model.Remove("px"); // 清场
        }

        // 为每个用例建独立 Architecture（否则 QFramework 单例模式会污染）
        private static IPlayerCardPositionModel CreateModel()
        {
            TestArch.ResetForTests();
            var arch = TestArch.Interface;
            return arch.GetModel<IPlayerCardPositionModel>();
        }

        // 测试专用 Architecture；注册 Utility + Model，并暴露 reset
        private sealed class TestArch : Architecture<TestArch>
        {
            protected override void Init()
            {
                RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());
                RegisterModel<IPlayerCardPositionModel>(new PlayerCardPositionModel());
            }

            // QFramework 的 Architecture 是静态单例，需要每个用例重置
            public static void ResetForTests()
            {
                // 反射置空 mArchitecture 以触发 InitArchitecture
                var fld = typeof(Architecture<TestArch>)
                    .GetField("mArchitecture", BindingFlags.NonPublic | BindingFlags.Static);
                fld?.SetValue(null, null);
            }
        }
    }
}
