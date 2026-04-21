using System.Collections.Generic;
using QFramework;

namespace APP.SessionMemory.Model
{
    public interface ISessionMemoryModel : IModel
    {
        BindableProperty<string> LastPlayerName { get; }
        BindableProperty<string> LastRoomCode { get; }
        BindableProperty<bool> AutoReconnectEnabled { get; }
        IReadOnlyList<HistoryRoomEntry> RecentRooms { get; }

        void RememberJoin(string playerName, string roomCode);
        void ForgetLastRoom();
        void SetAutoReconnectEnabled(bool enabled);
        void RemoveHistoryEntry(string roomCode);
    }
}
