using System;
using System.Collections.Generic;
using APP.Utility;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    public sealed class PlayerCardPositionModel : AbstractModel, IPlayerCardPositionModel
    {
        private const string StorageKey = "CPA.PlayerCardPositions";

        private readonly Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();

        [Serializable]
        private struct Entry
        {
            public string id;
            public float  x;
            public float  y;
        }

        [Serializable]
        private sealed class Envelope
        {
            public Entry[] entries = Array.Empty<Entry>();
        }

        protected override void OnInit()
        {
            var storage = this.GetUtility<IStorageUtility>();
            string json = storage?.LoadString(StorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                Envelope env = JsonUtility.FromJson<Envelope>(json);
                if (env?.entries == null) return;
                for (int i = 0; i < env.entries.Length; i++)
                {
                    var e = env.entries[i];
                    if (!string.IsNullOrEmpty(e.id))
                        _positions[e.id] = new Vector2(e.x, e.y);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerCardPositionModel] 解析持久化数据失败：{ex.Message}");
            }
        }

        public bool TryGet(string playerId, out Vector2 position)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                position = Vector2.zero;
                return false;
            }
            return _positions.TryGetValue(playerId, out position);
        }

        public void Set(string playerId, Vector2 position)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _positions[playerId] = position;
            Persist();
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_positions.Remove(playerId)) Persist();
        }

        private void Persist()
        {
            var storage = this.GetUtility<IStorageUtility>();
            if (storage == null) return;

            var env = new Envelope { entries = new Entry[_positions.Count] };
            int idx = 0;
            foreach (var kv in _positions)
            {
                env.entries[idx++] = new Entry { id = kv.Key, x = kv.Value.x, y = kv.Value.y };
            }
            storage.SaveString(StorageKey, JsonUtility.ToJson(env));
            storage.Flush();
        }
    }
}
