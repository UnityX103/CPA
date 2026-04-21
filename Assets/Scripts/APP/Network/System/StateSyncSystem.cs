using APP.Network.DTO;
using APP.Network.Model;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Network.System
{
    public sealed class StateSyncSystem : AbstractSystem, IStateSyncSystem
    {
        private float _accumulator;
        private float _timeSinceLastSent;
        private RemoteState _lastSent;
        private IUnRegister _phaseChangedUnregister;

        protected override void OnInit()
        {
            _accumulator = 0f;
            _timeSinceLastSent = 0f;
            _lastSent = null;
            _phaseChangedUnregister = this.RegisterEvent<E_PomodoroPhaseChanged>(_ => ForceSyncNow());
        }

        protected override void OnDeinit()
        {
            _phaseChangedUnregister?.UnRegister();
            _phaseChangedUnregister = null;
        }

        public void Tick(float deltaTime)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            if (!room.IsConnected.Value || !room.IsInRoom.Value)
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

            if (changed || _timeSinceLastSent >= 5f)
            {
                SendState(current);
            }
        }

        public void ForceSyncNow()
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            if (!room.IsConnected.Value || !room.IsInRoom.Value)
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
            };
        }
    }
}
