using APP.Network.DTO;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using APP.Settings.Model;
using QFramework;

namespace APP.Network.System
{
    public sealed class StateSyncSystem : AbstractSystem, IStateSyncSystem
    {
        private float _accumulator;
        private float _timeSinceLastSent;
        private RemoteState _lastSent;
        private IUnRegister _phaseChangedUnregister;
        private IUnRegister _syncedKeyIdUnregister;
        private IUnRegister _connectionStateUnregister;

        protected override void OnInit()
        {
            _accumulator = 0f;
            _timeSinceLastSent = 0f;
            _lastSent = null;
            _phaseChangedUnregister = this.RegisterEvent<E_PomodoroPhaseChanged>(_ => ForceSyncNow());
            // 选中/清空 synced entry → 立即推送，避免 1s Tick 节流让 PlayerCard pill 延迟隐藏/出现。
            // 删除当前 synced entry 时 BindingKeyModel.RemoveEntry 会把 SyncedKeyId 清空 → 同样落到这里。
            _syncedKeyIdUnregister = this.GetModel<IBindingKeyModel>().SyncedKeyId.Register(_ => ForceSyncNow());
            // 入房 / 重连恢复瞬间补发：NetworkSystem 在 HandleRoomCreated/HandleRoomJoined 以及
            // 重连成功后会发 E_ConnectionStateChanged(InRoom)。监听这个单一事件能避开订阅
            // IRoomModel.IsConnected/IsInRoom 两个 BindableProperty 时 SetConnectionFlags 半完成态
            // 误触发的问题；同时清掉 _lastSent，避免旧快照压制 pressCount-only 更新。
            _connectionStateUnregister = this.RegisterEvent<E_ConnectionStateChanged>(OnConnectionStateChanged);
        }

        protected override void OnDeinit()
        {
            _phaseChangedUnregister?.UnRegister();
            _phaseChangedUnregister = null;
            _syncedKeyIdUnregister?.UnRegister();
            _syncedKeyIdUnregister = null;
            _connectionStateUnregister?.UnRegister();
            _connectionStateUnregister = null;
        }

        private void OnConnectionStateChanged(E_ConnectionStateChanged evt)
        {
            // 仅在确认进入房间时补发；Connecting/Reconnecting/Error/Disconnected 都不触发。
            if (evt.Status != ConnectionStatus.InRoom) return;
            _lastSent = null;
            ForceSyncNow();
        }

        // PressCount-only 更新的最短同步间隔：5s heartbeat 对一个会被高频点击的计数器太陈旧，
        // 2s 在节流和实时性之间取折中——按键连续按下时远端最多滞后 2s，而不是 5s。
        private const float PressCountDiffMinInterval = 2f;
        private const float HeartbeatInterval = 5f;

        public void Tick(float deltaTime)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            // 三重守卫：IsConnected + IsInRoom + Status==InRoom。
            // IsInRoom 是"用户意图"，Status 是"服务端确认"——重连刚成功时 IsInRoom 仍为 true 但 Status=Connected，
            // 此时 room_joined ack 还没回来，发 player_state_update 会被服务端丢弃，必须等 ack。
            if (!room.IsConnected.Value
                || !room.IsInRoom.Value
                || room.Status.Value != ConnectionStatus.InRoom)
            {
                return;
            }

            _accumulator += deltaTime;
            _timeSinceLastSent += deltaTime;

            if (_accumulator < 1f)
            {
                return;
            }

            _accumulator = 0f;

            RemoteState current = CollectLocalState();
            bool changed = _lastSent == null || !RemoteState.EqualsLogical(current, _lastSent);

            // PressCount 故意不进 EqualsLogical（否则每秒 spam）。但若它真的变了，用 2s 间隔的
            // 中速通道推一次——比 5s heartbeat 灵敏，又不至于每秒都发。
            bool pressCountDiff = !changed
                && current.bindingKey != null
                && _lastSent?.bindingKey != null
                && current.bindingKey.pressCount != _lastSent.bindingKey.pressCount;

            if (changed
                || _timeSinceLastSent >= HeartbeatInterval
                || (pressCountDiff && _timeSinceLastSent >= PressCountDiffMinInterval))
            {
                SendState(current);
            }
        }

        public void ForceSyncNow()
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            // 三重守卫：和 Tick 保持一致；Status!=InRoom 时（例如 socket 刚连上 ack 还没回）也挡掉。
            if (!room.IsConnected.Value
                || !room.IsInRoom.Value
                || room.Status.Value != ConnectionStatus.InRoom)
            {
                return;
            }

            SendState(CollectLocalState());
        }

        private void SendState(RemoteState state)
        {
            this.GetSystem<INetworkSystem>().Send(new OutboundSyncState
            {
                v = 1,
                type = "player_state_update",
                state = state,
            });

            _lastSent = state;
            _timeSinceLastSent = 0f;
        }

        private RemoteState CollectLocalState()
        {
            IPomodoroModel pomodoro = this.GetModel<IPomodoroModel>();
            IActiveAppSystem activeApp = this.GetSystem<IActiveAppSystem>();
            IBindingKeyModel binding = this.GetModel<IBindingKeyModel>();
            ActiveAppSnapshot snap = activeApp.Current;

            return new RemoteState
            {
                pomodoro = new PomodoroStateDto
                {
                    phase = (int)pomodoro.CurrentPhase.Value,
                    remainingSeconds = pomodoro.RemainingSeconds.Value,
                    currentRound = pomodoro.CurrentRound.Value,
                    totalRounds = pomodoro.TotalRounds.Value,
                    isRunning = pomodoro.IsRunning.Value,
                },
                activeApp = string.IsNullOrEmpty(snap.BundleId) ? null : new ActiveAppDto
                {
                    name = snap.Name,
                    bundleId = snap.BundleId,
                    iconId = null,
                },
                bindingKey = BuildSyncedBindingDto(binding),
            };
        }

        /// <summary>
        /// 把 BindingKey 设置里"标记同步"的那个 entry 打包成可序列化 DTO。
        /// SyncedKeyId 为空、或目标 entry 已被删除 → 返回 null，接收端据此隐藏 PlayerCard 的 pill。
        /// 不读全局 binding.Enabled：取消同步走 SyncedKeyId 清空那条路径，全局 Enabled 只影响本端 tick。
        /// </summary>
        private static BindingKeyDto BuildSyncedBindingDto(IBindingKeyModel binding)
        {
            if (binding == null) return null;
            string syncedId = binding.SyncedKeyId.Value;
            if (string.IsNullOrEmpty(syncedId)) return null;

            var entries = binding.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == syncedId)
                {
                    return new BindingKeyDto
                    {
                        keyLabel = entries[i].KeyLabel ?? string.Empty,
                        pressCount = entries[i].PressCount,
                    };
                }
            }
            return null;
        }
    }
}
