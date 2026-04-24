using APP.Pomodoro;
using APP.Pomodoro.Model;
using APP.Pomodoro.Queries;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Tests.PlayerCardTests
{
    public sealed class QIsAnyPinnedTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetArchitecture();
            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [TearDown]
        public void TearDown()
        {
            ResetArchitecture();
            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        private static void ResetArchitecture()
        {
            var current = typeof(GameApp).BaseType
                ?.GetField("mArchitecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IArchitecture;
            current?.Deinit();
        }

        [Test]
        public void Query_Returns_False_When_Nothing_Pinned()
        {
            bool result = GameApp.Interface.SendQuery(new Q_IsAnyPinned());
            Assert.IsFalse(result);
        }

        [Test]
        public void Query_Returns_True_When_Pomodoro_Pinned()
        {
            GameApp.Interface.GetModel<IPomodoroModel>().IsPinned.Value = true;
            bool result = GameApp.Interface.SendQuery(new Q_IsAnyPinned());
            Assert.IsTrue(result);
        }

        [Test]
        public void Query_Returns_True_When_Any_Card_Pinned()
        {
            var c = GameApp.Interface.GetModel<IPlayerCardModel>().AddOrGet("p1");
            c.IsPinned.Value = true;
            bool result = GameApp.Interface.SendQuery(new Q_IsAnyPinned());
            Assert.IsTrue(result);
        }
    }
}
