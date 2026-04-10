using System.Collections.Generic;
using QFramework;

namespace APP.Network.Model
{
    public sealed class RoomModel : AbstractModel, IRoomModel
    {
        private readonly List<RemotePlayerData> _remotePlayers = new List<RemotePlayerData>();

        public BindableProperty<string> RoomCode { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<bool> IsConnected { get; } = new BindableProperty<bool>(false);
        public BindableProperty<bool> IsInRoom { get; } = new BindableProperty<bool>(false);
        public BindableProperty<string> LocalPlayerName { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<string> LocalPlayerId { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<ConnectionStatus> Status { get; } =
            new BindableProperty<ConnectionStatus>(ConnectionStatus.Disconnected);
        public IReadOnlyList<RemotePlayerData> RemotePlayers => _remotePlayers;

        protected override void OnInit()
        {
        }

        public void SetRoomCode(string roomCode)
        {
            RoomCode.Value = roomCode ?? string.Empty;
        }

        public void SetConnectionFlags(bool isConnected, bool isInRoom)
        {
            IsConnected.Value = isConnected;
            IsInRoom.Value = isInRoom;
        }

        public void SetLocalPlayerName(string playerName)
        {
            LocalPlayerName.Value = playerName ?? string.Empty;
        }

        public void SetLocalPlayerId(string playerId)
        {
            LocalPlayerId.Value = playerId ?? string.Empty;
        }

        public void SetStatus(ConnectionStatus status)
        {
            Status.Value = status;
        }

        public void ApplySnapshot(IList<RemotePlayerData> players)
        {
            _remotePlayers.Clear();

            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                RemotePlayerData player = players[i];
                if (player == null)
                {
                    continue;
                }

                _remotePlayers.Add(player.Clone());
            }
        }

        public void AddOrUpdateRemotePlayer(RemotePlayerData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.PlayerId))
            {
                return;
            }

            for (int i = 0; i < _remotePlayers.Count; i++)
            {
                if (_remotePlayers[i].PlayerId != data.PlayerId)
                {
                    continue;
                }

                _remotePlayers[i] = data.Clone();
                return;
            }

            _remotePlayers.Add(data.Clone());
        }

        public void RemoveRemotePlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            for (int i = _remotePlayers.Count - 1; i >= 0; i--)
            {
                if (_remotePlayers[i].PlayerId == playerId)
                {
                    _remotePlayers.RemoveAt(i);
                    return;
                }
            }
        }

        public void ClearRemotePlayers()
        {
            _remotePlayers.Clear();
        }

        public void ResetRoomState()
        {
            SetRoomCode(string.Empty);
            SetLocalPlayerId(string.Empty);
            SetConnectionFlags(IsConnected.Value, false);
            ClearRemotePlayers();
        }
    }
}
