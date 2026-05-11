using System.Collections.Generic;
using APP.Network.DTO;
using APP.Network.Event;
using APP.Network.Model;
using APP.Network.System;
using APP.Pomodoro;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using APP.Settings.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;

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
            // SetUp 走 SetConnectionFlags / SetStatus 不发 E_ConnectionStateChanged 事件，
            // 因此 StateSyncSystem.OnConnectionStateChanged 不会被触发——这里仍然 Clear 一下
            // 防御未来某些测试自己 SendEvent 出现脏状态。
            _fakeNetworkSystem.SentMessages.Clear();
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

        [Test]
        public void SyncedKeyId_Set_TriggersImmediateSyncWithBindingDto()
        {
            var binding = GameApp.Interface.GetModel<IBindingKeyModel>();
            string id = binding.AddEntry();
            binding.TryUpdateEntryKey(id, (int)KeyCode.Space, "Space");

            binding.SyncedKeyId.Value = id;

            Assert.That(_fakeNetworkSystem.SentMessages, Has.Count.EqualTo(1), "SyncedKeyId 切换应立即推送一次。");
            var msg = (OutboundSyncState)_fakeNetworkSystem.SentMessages[0];
            Assert.That(msg.state.bindingKey, Is.Not.Null);
            Assert.That(msg.state.bindingKey.keyLabel, Is.EqualTo("Space"));
        }

        [Test]
        public void SyncedKeyId_ClearOnRemove_ImmediatelyPushesNullBinding()
        {
            var binding = GameApp.Interface.GetModel<IBindingKeyModel>();
            string id = binding.AddEntry();
            binding.TryUpdateEntryKey(id, (int)KeyCode.F, "F");
            binding.SyncedKeyId.Value = id;
            _fakeNetworkSystem.SentMessages.Clear();

            // RemoveEntry 内部把 SyncedKeyId 清空 → 触发 StateSyncSystem 立即重发
            binding.RemoveEntry(id);

            Assert.That(_fakeNetworkSystem.SentMessages, Has.Count.EqualTo(1));
            var msg = (OutboundSyncState)_fakeNetworkSystem.SentMessages[0];
            Assert.That(msg.state.bindingKey, Is.Null, "Synced entry 删除后 bindingKey 必须为 null，让远端 pill 隐藏。");
        }

        [Test]
        public void PressCountOnly_FirstTickWithin2s_DoesNotResend()
        {
            var binding = GameApp.Interface.GetModel<IBindingKeyModel>();
            string id = binding.AddEntry();
            binding.TryUpdateEntryKey(id, (int)KeyCode.Space, "Space");
            binding.SyncedKeyId.Value = id;
            int baseline = _fakeNetworkSystem.SentMessages.Count;
            Assert.That(baseline, Is.GreaterThan(0));

            binding.IncrementEntry(id);
            binding.IncrementEntry(id);

            // 1s Tick：pressCount-only 走 2s 中速通道，1s 还不够 → 不发
            _stateSyncSystem.Tick(1.0f);
            Assert.That(_fakeNetworkSystem.SentMessages.Count, Is.EqualTo(baseline),
                "PressCount-only 变化在 2s 中速间隔内不应触发立即重发，避免每秒 spam。");
        }

        [Test]
        public void PressCountOnly_After2sInterval_SendsLatestCountViaMidChannel()
        {
            var binding = GameApp.Interface.GetModel<IBindingKeyModel>();
            string id = binding.AddEntry();
            binding.TryUpdateEntryKey(id, (int)KeyCode.Space, "Space");
            binding.SyncedKeyId.Value = id;
            int baseline = _fakeNetworkSystem.SentMessages.Count;

            binding.IncrementEntry(id);
            binding.IncrementEntry(id);
            binding.IncrementEntry(id);

            // 跨过 1s 的 Tick 门槛 + 累计 ≥2s → pressCount-diff 中速通道触发
            _stateSyncSystem.Tick(1.0f);
            _stateSyncSystem.Tick(1.2f);  // 累计 2.2s
            Assert.That(_fakeNetworkSystem.SentMessages.Count, Is.EqualTo(baseline + 1),
                "累计 ≥2s 时 pressCount-diff 中速通道应推一次，比 5s heartbeat 灵敏。");
            var latest = (OutboundSyncState)_fakeNetworkSystem.SentMessages[baseline];
            Assert.That(latest.state.bindingKey, Is.Not.Null);
            Assert.That(latest.state.bindingKey.pressCount, Is.EqualTo(3),
                "推送内容必须带最新的 PressCount。");
        }

        [Test]
        public void NoChange_5sHeartbeat_SendsKeepAlive()
        {
            // 第一次 Tick：_lastSent=null → 算 changed → 发送，并把 _timeSinceLastSent 重置为 0
            _stateSyncSystem.Tick(1.0f);
            int afterFirst = _fakeNetworkSystem.SentMessages.Count;
            Assert.That(afterFirst, Is.EqualTo(1), "首发应当成功。");

            // 接下来不再修改任何状态，只测 5s heartbeat：
            // 从首发后 _timeSinceLastSent=0 起算，<5s 不应再发
            _stateSyncSystem.Tick(2.0f);            // _timeSinceLastSent ≈ 2s
            _stateSyncSystem.Tick(2.0f);            // ≈ 4s
            Assert.That(_fakeNetworkSystem.SentMessages.Count, Is.EqualTo(afterFirst),
                "完全无变化时 <5s 不应触发 heartbeat。");

            _stateSyncSystem.Tick(1.5f);            // ≈ 5.5s
            Assert.That(_fakeNetworkSystem.SentMessages.Count, Is.GreaterThan(afterFirst),
                "完全无变化时 ≥5s heartbeat 应触发一次。");
        }

        [Test]
        public void DuringReconnectBeforeRoomJoinedAck_GuardBlocksAllSends()
        {
            var binding = GameApp.Interface.GetModel<IBindingKeyModel>();
            string id = binding.AddEntry();
            binding.TryUpdateEntryKey(id, (int)KeyCode.Space, "Space");
            binding.SyncedKeyId.Value = id;
            _fakeNetworkSystem.SentMessages.Clear();

            // 模拟：socket 刚连上、room_joined ack 还没回来 → Status=Connected（IsConnected/IsInRoom 由用户意图保留）
            _roomModel.SetStatus(ConnectionStatus.Connected);

            // 这期间用户改了同步键 / 番茄状态 / 触发 ForceSyncNow，都不能发出去
            binding.SyncedKeyId.Value = string.Empty;        // 触发 ForceSyncNow
            binding.SyncedKeyId.Value = id;                  // 再触发一次
            _stateSyncSystem.ForceSyncNow();
            _stateSyncSystem.Tick(2.0f);
            _stateSyncSystem.Tick(2.0f);
            _stateSyncSystem.Tick(2.0f);                     // 累计 6s 远超 5s heartbeat

            Assert.That(_fakeNetworkSystem.SentMessages, Is.Empty,
                "Status!=InRoom（ack 未到）时三重守卫必须挡住所有 send，避免服务端丢弃 player_state_update。");
        }

        [Test]
        public void ConnectionStateChangedToInRoom_TriggersImmediateSync()
        {
            var binding = GameApp.Interface.GetModel<IBindingKeyModel>();
            string id = binding.AddEntry();
            binding.TryUpdateEntryKey(id, (int)KeyCode.Space, "Space");
            binding.SyncedKeyId.Value = id;
            _fakeNetworkSystem.SentMessages.Clear();

            // 模拟 NetworkSystem 在 HandleRoomCreated / HandleRoomJoined 发出的事件
            GameApp.Interface.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.InRoom));

            Assert.That(_fakeNetworkSystem.SentMessages, Has.Count.EqualTo(1),
                "进入房间事件应立即补发一次快照。");
            var msg = (OutboundSyncState)_fakeNetworkSystem.SentMessages[0];
            Assert.That(msg.state.bindingKey, Is.Not.Null);
            Assert.That(msg.state.bindingKey.keyLabel, Is.EqualTo("Space"));
        }

        [Test]
        public void ReconnectAfterOfflineChanges_FlushesPendingPressCount()
        {
            var binding = GameApp.Interface.GetModel<IBindingKeyModel>();
            string id = binding.AddEntry();
            binding.TryUpdateEntryKey(id, (int)KeyCode.Space, "Space");
            binding.SyncedKeyId.Value = id;
            _fakeNetworkSystem.SentMessages.Clear();

            // 重连真实路径：IsInRoom 保持 true，IsConnected false→true，最后 SetStatus(InRoom) + 发事件
            _roomModel.SetConnectionFlags(false, true);
            binding.IncrementEntry(id);
            binding.IncrementEntry(id);
            _stateSyncSystem.ForceSyncNow();
            Assert.That(_fakeNetworkSystem.SentMessages, Is.Empty,
                "断线期间 ForceSyncNow 被守卫挡掉，不应发送。");

            _roomModel.SetConnectionFlags(true, true);
            _roomModel.SetStatus(ConnectionStatus.InRoom);
            GameApp.Interface.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.InRoom));

            Assert.That(_fakeNetworkSystem.SentMessages, Has.Count.GreaterThanOrEqualTo(1),
                "重连完成后必须立即把断线期间累加的 PressCount 推出去。");
            var msg = (OutboundSyncState)_fakeNetworkSystem.SentMessages[_fakeNetworkSystem.SentMessages.Count - 1];
            Assert.That(msg.state.bindingKey, Is.Not.Null);
            Assert.That(msg.state.bindingKey.pressCount, Is.EqualTo(2));
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
