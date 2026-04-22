using System.Collections.Generic;
using APP.Network.DTO;
using APP.Network.Model;
using APP.Network.System;
using APP.Pomodoro;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class StateSyncTests
    {
        private FakeNetworkSystem _fakeNetworkSystem;
        private IRoomModel _roomModel;
        private IPomodoroModel _pomodoroModel;
        private IStateSyncSystem _stateSyncSystem;

        [SetUp]
        public void SetUp()
        {
            ResetGameApp();

            _fakeNetworkSystem = new FakeNetworkSystem();
            GameApp.OnRegisterPatch = architecture => architecture.RegisterSystem<INetworkSystem>(_fakeNetworkSystem);

            _ = GameApp.Interface;

            _roomModel = GameApp.Interface.GetModel<IRoomModel>();
            _pomodoroModel = GameApp.Interface.GetModel<IPomodoroModel>();
            _stateSyncSystem = GameApp.Interface.GetSystem<IStateSyncSystem>();

            _roomModel.SetConnectionFlags(true, true);
            _roomModel.SetStatus(ConnectionStatus.InRoom);
            _roomModel.SetRoomCode("ABC123");
            _roomModel.SetLocalPlayerId("local-player");
        }

        [TearDown]
        public void TearDown()
        {
            ResetGameApp();
        }

        [Test]
        public void Tick_WhenLessThanOneSecondElapsed_DoesNotSend()
        {
            _stateSyncSystem.Tick(0.4f);
            _stateSyncSystem.Tick(0.5f);

            Assert.That(_fakeNetworkSystem.SentMessages, Is.Empty);
        }

        [Test]
        public void Tick_WhenOneSecondElapsed_SendsLatestPomodoroState()
        {
            _pomodoroModel.CurrentPhase.Value = PomodoroPhase.Break;
            _pomodoroModel.RemainingSeconds.Value = 180;
            _pomodoroModel.CurrentRound.Value = 2;
            _pomodoroModel.TotalRounds.Value = 4;
            _pomodoroModel.IsRunning.Value = true;

            _stateSyncSystem.Tick(1.01f);

            Assert.That(_fakeNetworkSystem.SentMessages, Has.Count.EqualTo(1));
            Assert.That(_fakeNetworkSystem.SentMessages[0], Is.TypeOf<OutboundSyncState>());

            var message = (OutboundSyncState)_fakeNetworkSystem.SentMessages[0];
            Assert.That(message.state.pomodoro.phase, Is.EqualTo((int)PomodoroPhase.Break));
            Assert.That(message.state.pomodoro.remainingSeconds, Is.EqualTo(180));
            Assert.That(message.state.pomodoro.currentRound, Is.EqualTo(2));
            Assert.That(message.state.pomodoro.totalRounds, Is.EqualTo(4));
            Assert.That(message.state.pomodoro.isRunning, Is.True);
        }

        [Test]
        public void ForceSyncNow_WhenGuardFails_DoesNotSend()
        {
            _roomModel.SetConnectionFlags(false, true);
            _stateSyncSystem.ForceSyncNow();

            _roomModel.SetConnectionFlags(true, false);
            _stateSyncSystem.ForceSyncNow();

            Assert.That(_fakeNetworkSystem.SentMessages, Is.Empty);
        }

        [Test]
        public void PomodoroPhaseChangedEvent_WhenInRoom_TriggersImmediateSync()
        {
            GameApp.Interface.SendEvent(new E_PomodoroPhaseChanged(PomodoroPhase.Break, 2, 4));

            Assert.That(_fakeNetworkSystem.SentMessages, Has.Count.EqualTo(1));
        }

        private static void ResetGameApp()
        {
            GameApp.OnRegisterPatch = _ => { };

            try
            {
                GameApp.Interface.Deinit();
            }
            catch
            {
                // 测试初始化前 Architecture 可能尚未建立，忽略即可。
            }
        }

        private sealed class FakeNetworkSystem : AbstractSystem, INetworkSystem
        {
            public List<object> SentMessages { get; } = new List<object>();

            protected override void OnInit()
            {
            }

            public void Connect(string serverUrl, string playerName)
            {
            }

            public void Disconnect()
            {
            }

            public void Send(object message)
            {
                SentMessages.Add(message);
            }

            public void DrainMainThreadQueue()
            {
            }
        }
    }
}
