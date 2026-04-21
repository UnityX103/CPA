using System;
using System.Collections.Generic;
using UnityEngine;

namespace APP.SessionMemory.Model
{
    [Serializable]
    internal sealed class HistoryRoomListWrapper
    {
        public List<HistoryRoomEntry> Entries;
    }

    public static class HistoryRoomSerializer
    {
        public static string Serialize(IList<HistoryRoomEntry> entries)
        {
            var wrapper = new HistoryRoomListWrapper { Entries = new List<HistoryRoomEntry>(entries ?? new List<HistoryRoomEntry>()) };
            return JsonUtility.ToJson(wrapper);
        }

        public static List<HistoryRoomEntry> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<HistoryRoomEntry>();
            try
            {
                HistoryRoomListWrapper wrapper = JsonUtility.FromJson<HistoryRoomListWrapper>(json);
                return wrapper?.Entries ?? new List<HistoryRoomEntry>();
            }
            catch
            {
                return new List<HistoryRoomEntry>();
            }
        }
    }
}
