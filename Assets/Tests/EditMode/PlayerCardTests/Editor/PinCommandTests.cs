using APP.Pomodoro;
using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.TestTools;

namespace APP.Tests.PlayerCardTests
{
    public sealed class PinCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            // 重置 Architecture 单例：Deinit 会把 mArchitecture 静态字段置空，
            // 下次访问 Interface 时重新 Init（见 QFramework.cs Architecture<T>.Deinit）
            var current = typeof(GameApp).BaseType
                ?.GetField("mArchitecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IArchitecture;
            current?.Deinit();

            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [TearDown]
        public void TearDown()
        {
            var current = typeof(GameApp).BaseType
                ?.GetField("mArchitecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IArchitecture;
            current?.Deinit();

            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [Test]
        public void Cmd_SetPomodoroPinned_WritesModel()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IPomodoroModel>();
            Assert.IsFalse(model.IsPinned.Value);

            arch.SendCommand(new Cmd_SetPomodoroPinned(true));

            Assert.IsTrue(model.IsPinned.Value);
        }

        [Test]
        public void Cmd_SetPlayerCardPinned_MissingPlayerId_LogsWarningWithoutThrow()
        {
            var arch = GameApp.Interface;
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("\\[Cmd_SetPlayerCardPinned\\] 未找到 playerId=ghost"));

            Assert.DoesNotThrow(() => arch.SendCommand(new Cmd_SetPlayerCardPinned("ghost", true)));
        }

        [Test]
        public void Cmd_SetPlayerCardPinned_OnlineCard_WritesModel()
        {
            var arch = GameApp.Interface;
            var cardModel = arch.GetModel<IPlayerCardModel>();
            var card = cardModel.AddOrGet("p1");
            Assert.IsFalse(card.IsPinned.Value);

            arch.SendCommand(new Cmd_SetPlayerCardPinned("p1", true));

            Assert.IsTrue(card.IsPinned.Value);
        }

        [Test]
        public void Cmd_SetAppFocused_WritesFalse()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IGameModel>();
            Assert.IsTrue(model.IsAppFocused.Value, "IsAppFocused 默认应为 true");

            arch.SendCommand(new Cmd_SetAppFocused(false));

            Assert.IsFalse(model.IsAppFocused.Value);
        }

        [Test]
        public void Cmd_SetAppFocused_WritesTrue()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IGameModel>();
            model.IsAppFocused.Value = false;

            arch.SendCommand(new Cmd_SetAppFocused(true));

            Assert.IsTrue(model.IsAppFocused.Value);
        }

        [Test]
        public void Cmd_SetAppFocused_TriggersSubscriber()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IGameModel>();
            bool? received = null;
            model.IsAppFocused.Register(v => received = v);

            arch.SendCommand(new Cmd_SetAppFocused(false));

            Assert.IsTrue(received.HasValue);
            Assert.IsFalse(received.Value);
        }
    }
}
