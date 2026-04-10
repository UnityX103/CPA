using System.Collections.Generic;
using QFramework;

namespace APP.Network.Model
{
    public interface IRoomModel : IModel
    {
        BindableProperty<string> RoomCode { get; }
        BindableProperty<bool> IsConnected { get; }
        BindableProperty<bool> IsInRoom { get; }
        BindableProperty<string> LocalPlayerName { get; }
        BindableProperty<string> LocalPlayerId { get; }
        BindableProperty<ConnectionStatus> Status { get; }
        IReadOnlyList<RemotePlayerData> RemotePlayers { get; }

        void SetRoomCode(string roomCode);
        void SetConnectionFlags(bool isConnected, bool isInRoom);
        void SetLocalPlayerName(string playerName);
        void SetLocalPlayerId(string playerId);
        void SetStatus(ConnectionStatus status);
        void ApplySnapshot(IList<RemotePlayerData> players);
        void AddOrUpdateRemotePlayer(RemotePlayerData data);
        void RemoveRemotePlayer(string playerId);
        void ClearRemotePlayers();
        void ResetRoomState();
    }
}
