using System;

namespace APP.SessionMemory.Model
{
    [Serializable]
    public sealed class HistoryRoomEntry
    {
        public string RoomCode;
        public string LastPlayerName;
        public long LastJoinedAtUnixMs;
    }
}
