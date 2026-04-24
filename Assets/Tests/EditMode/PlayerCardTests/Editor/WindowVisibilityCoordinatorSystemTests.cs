using APP.Pomodoro;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Tests.PlayerCardTests
{
    public sealed class WindowVisibilityCoordinatorSystemTests
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
            // mArchitecture 声明在 Architecture<GameApp> 基类上，typeof(GameApp).GetField
            // 不跨继承查找私有字段；这里显式走 BaseType 拿到真实字段。
            var field = typeof(GameApp).BaseType?.GetField(
                "mArchitecture",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var current = field?.GetValue(null) as IArchitecture;
            current?.Deinit();
        }

        [Test]
        public void AnyPinned_Initially_False()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_TogglesWhenPomodoroIsPinnedChanges()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var pomodoro = GameApp.Interface.GetModel<IPomodoroModel>();

            pomodoro.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            pomodoro.IsPinned.Value = false;
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_TogglesWhenPlayerCardIsPinnedChanges()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();

            var c = cards.AddOrGet("p1");
            Assert.IsFalse(coord.AnyPinned.Value);

            c.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            c.IsPinned.Value = false;
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_StaysTrueWhenAnyOfMultipleSourcesIsPinned()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var pomodoro = GameApp.Interface.GetModel<IPomodoroModel>();
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();

            var p1 = cards.AddOrGet("p1");
            var p2 = cards.AddOrGet("p2");

            pomodoro.IsPinned.Value = true;
            p1.IsPinned.Value = true;
            p2.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            // 取消其中两个，仍为 true
            p1.IsPinned.Value = false;
            pomodoro.IsPinned.Value = false;
            Assert.IsTrue(coord.AnyPinned.Value);

            // 最后一个取消 → false
            p2.IsPinned.Value = false;
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_RemovingPinnedCard_DropsToFalse()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();

            var c = cards.AddOrGet("p1");
            c.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            cards.Remove("p1");
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_AddingAlreadyPinnedCard_PromotesToTrue()
        {
            // 场景：Card 持久化记录中 pinned=true，重新 AddOrGet 时 coordinator 要识别
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();
            var c = cards.AddOrGet("p1");
            c.IsPinned.Value = true;
            cards.Remove("p1"); // 落盘 pinned=true

            // 模拟"重新加入"：AddOrGet 会从持久化恢复 IsPinned=true
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var c2 = cards.AddOrGet("p1");
            Assert.IsTrue(c2.IsPinned.Value, "持久化应恢复 pinned=true");
            Assert.IsTrue(coord.AnyPinned.Value, "coordinator 应识别到 pinned 卡重新加入");
        }
    }
}
