using System;
using System.Collections.Generic;
using APP.SessionMemory.Event;
using APP.Utility;
using QFramework;

namespace APP.SessionMemory.Model
{
    public sealed class SessionMemoryModel : AbstractModel, ISessionMemoryModel
    {
        private const int MaxRecentRooms = 5;
        private const string KeyLastPlayerName = "net.lastPlayerName";
        private const string KeyLastRoomCode = "net.lastRoomCode";
        private const string KeyAutoReconnect = "net.autoReconnect";
        private const string KeyRecentRooms = "net.recentRooms";

        private readonly List<HistoryRoomEntry> _recentRooms = new List<HistoryRoomEntry>();

        public BindableProperty<string> LastPlayerName { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<string> LastRoomCode { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<bool> AutoReconnectEnabled { get; } = new BindableProperty<bool>(false);
        public IReadOnlyList<HistoryRoomEntry> RecentRooms => _recentRooms;

        protected override void OnInit()
        {
            IStorageUtility storage = this.GetUtility<IStorageUtility>();
            LastPlayerName.SetValueWithoutEvent(storage.LoadString(KeyLastPlayerName, string.Empty));
            LastRoomCode.SetValueWithoutEvent(storage.LoadString(KeyLastRoomCode, string.Empty));
            AutoReconnectEnabled.SetValueWithoutEvent(storage.LoadInt(KeyAutoReconnect, 0) != 0);

            _recentRooms.Clear();
            _recentRooms.AddRange(HistoryRoomSerializer.Deserialize(storage.LoadString(KeyRecentRooms, string.Empty)));

            LastPlayerName.Register(v => storage.SaveString(KeyLastPlayerName, v));
            LastRoomCode.Register(v => storage.SaveString(KeyLastRoomCode, v));
            AutoReconnectEnabled.Register(v => storage.SaveInt(KeyAutoReconnect, v ? 1 : 0));
        }

        public void RememberJoin(string playerName, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return;

            string normalizedName = playerName ?? string.Empty;
            string normalizedCode = roomCode;

            LastPlayerName.Value = normalizedName;
            LastRoomCode.Value = normalizedCode;

            _recentRooms.RemoveAll(e => e.RoomCode == normalizedCode);
            _recentRooms.Insert(0, new HistoryRoomEntry
            {
                RoomCode = normalizedCode,
                LastPlayerName = normalizedName,
                LastJoinedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            if (_recentRooms.Count > MaxRecentRooms)
                _recentRooms.RemoveRange(MaxRecentRooms, _recentRooms.Count - MaxRecentRooms);

            PersistRecent();
            this.SendEvent<E_RecentRoomsChanged>();
        }

        public void ForgetLastRoom()
        {
            LastRoomCode.Value = string.Empty;
        }

        public void SetAutoReconnectEnabled(bool enabled)
        {
            AutoReconnectEnabled.Value = enabled;
        }

        public void RemoveHistoryEntry(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return;
            int removed = _recentRooms.RemoveAll(e => e.RoomCode == roomCode);
            if (removed > 0)
            {
                PersistRecent();
                this.SendEvent<E_RecentRoomsChanged>();
            }
        }

        private void PersistRecent()
        {
            this.GetUtility<IStorageUtility>().SaveString(KeyRecentRooms, HistoryRoomSerializer.Serialize(_recentRooms));
        }
    }
}
