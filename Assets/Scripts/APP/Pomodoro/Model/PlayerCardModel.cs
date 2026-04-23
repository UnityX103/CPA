using System;
using System.Collections.Generic;
using APP.Pomodoro.Event;
using APP.Utility;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    public sealed class PlayerCardModel : AbstractModel, IPlayerCardModel
    {
        private const string StorageKey = "CPA.PlayerCards";

        // 持久化仓库（所有曾经出现过的玩家，保留 Position/IsPinned 最近一次值）
        private readonly Dictionary<string, PersistedData> _store = new Dictionary<string, PersistedData>();

        // 当前在线实例表
        private readonly Dictionary<string, PlayerCardEntry> _entries = new Dictionary<string, PlayerCardEntry>();

        // 对外暴露的只读视图（每次变化重建一次；Cards 列表数量小，代价可忽略）
        private IReadOnlyList<IPlayerCard> _cardsView = Array.Empty<IPlayerCard>();

        public IReadOnlyList<IPlayerCard> Cards => _cardsView;

        protected override void OnInit()
        {
            var storage = this.GetUtility<IStorageUtility>();
            string json = storage?.LoadString(StorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                var env = JsonUtility.FromJson<Envelope>(json);
                if (env?.entries == null) return;
                for (int i = 0; i < env.entries.Length; i++)
                {
                    var e = env.entries[i];
                    if (string.IsNullOrEmpty(e.id)) continue;
                    _store[e.id] = new PersistedData
                    {
                        Position = new Vector2(e.x, e.y),
                        IsPinned = e.pinned,
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerCardModel] 解析持久化数据失败：{ex.Message}");
            }
        }

        public IPlayerCard Find(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            return _entries.TryGetValue(playerId, out var entry) ? entry.Card : null;
        }

        public IPlayerCard AddOrGet(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            if (_entries.TryGetValue(playerId, out var existing))
            {
                return existing.Card;
            }

            Vector2 pos = Vector2.zero;
            bool pinned = false;
            if (_store.TryGetValue(playerId, out var saved))
            {
                pos = saved.Position;
                pinned = saved.IsPinned;
            }

            var card = new PlayerCard(playerId, pos, pinned);

            var entry = new PlayerCardEntry { Card = card };
            entry.PositionUnRegister = card.Position.Register(v =>
            {
                _store[playerId] = new PersistedData { Position = v, IsPinned = card.IsPinned.Value };
                Persist();
            });
            entry.PinnedUnRegister = card.IsPinned.Register(v =>
            {
                _store[playerId] = new PersistedData { Position = card.Position.Value, IsPinned = v };
                Persist();
            });

            _entries[playerId] = entry;
            RebuildView();

            this.SendEvent(new E_PlayerCardAdded(playerId));
            return card;
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_entries.TryGetValue(playerId, out var entry)) return;

            entry.PositionUnRegister?.UnRegister();
            entry.PinnedUnRegister?.UnRegister();

            // 落盘实例当前值（Register 回调已实时写入，这里是兜底）
            _store[playerId] = new PersistedData
            {
                Position = entry.Card.Position.Value,
                IsPinned = entry.Card.IsPinned.Value,
            };
            Persist();

            _entries.Remove(playerId);
            RebuildView();

            this.SendEvent(new E_PlayerCardRemoved(playerId));
        }

        private void RebuildView()
        {
            var list = new List<IPlayerCard>(_entries.Count);
            foreach (var kv in _entries) list.Add(kv.Value.Card);
            _cardsView = list;
        }

        private void Persist()
        {
            var storage = this.GetUtility<IStorageUtility>();
            if (storage == null) return;

            var env = new Envelope { entries = new Entry[_store.Count] };
            int idx = 0;
            foreach (var kv in _store)
            {
                env.entries[idx++] = new Entry
                {
                    id = kv.Key,
                    x = kv.Value.Position.x,
                    y = kv.Value.Position.y,
                    pinned = kv.Value.IsPinned,
                };
            }
            storage.SaveString(StorageKey, JsonUtility.ToJson(env));
            storage.Flush();
        }

        // ─── 内部类型 ────────────────────────────────────────────

        private struct PersistedData
        {
            public Vector2 Position;
            public bool IsPinned;
        }

        private sealed class PlayerCardEntry
        {
            public PlayerCard Card;
            public IUnRegister PositionUnRegister;
            public IUnRegister PinnedUnRegister;
        }

        private sealed class PlayerCard : IPlayerCard
        {
            public string PlayerId { get; }
            public BindableProperty<Vector2> Position { get; }
            public BindableProperty<bool> IsPinned { get; }

            public PlayerCard(string playerId, Vector2 pos, bool pinned)
            {
                PlayerId = playerId;
                Position = new BindableProperty<Vector2>(pos);
                IsPinned = new BindableProperty<bool>(pinned);
            }
        }

        [Serializable]
        private struct Entry
        {
            public string id;
            public float x;
            public float y;
            public bool pinned;
        }

        [Serializable]
        private sealed class Envelope
        {
            public Entry[] entries = Array.Empty<Entry>();
        }
    }
}
