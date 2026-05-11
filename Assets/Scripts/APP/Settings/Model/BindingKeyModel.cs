using System;
using System.Collections.Generic;
using APP.Utility;
using QFramework;
using UnityEngine;

namespace APP.Settings.Model
{
    public sealed class BindingKeyModel : AbstractModel, IBindingKeyModel
    {
        // ─── 编码约定 ─────────────────────────────────────────
        public const int MouseLeft   = -1;
        public const int MouseRight  = -2;
        public const int MouseMiddle = -3;

        public const int    DefaultBoundKeyCode  = MouseLeft;
        public const string DefaultBoundKeyLabel = "鼠标左键";
        public const bool   DefaultEnabled       = false;

        // ─── PlayerPrefs key ─────────────────────────────────
        private const string EnabledKey      = "BindingKey.Enabled";
        private const string EntriesJsonKey  = "BindingKey.EntriesJson";
        private const string SyncedKeyIdKey  = "BindingKey.SyncedKeyId";

        // ─── 状态 ─────────────────────────────────────────────
        public BindableProperty<bool>   Enabled         { get; } = new BindableProperty<bool>(DefaultEnabled);
        public BindableProperty<int>    EntriesRevision { get; } = new BindableProperty<int>(0);
        public BindableProperty<string> SyncedKeyId     { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<string> ListeningKeyId  { get; } = new BindableProperty<string>(string.Empty);

        private readonly List<BindingKeyEntry> _entries = new List<BindingKeyEntry>();
        public IReadOnlyList<BindingKeyEntry> Entries => _entries;

        protected override void OnInit()
        {
            var storage = this.GetUtility<IStorageUtility>();

            Enabled.SetValueWithoutEvent(storage.LoadInt(EnabledKey, DefaultEnabled ? 1 : 0) != 0);
            SyncedKeyId.SetValueWithoutEvent(storage.LoadString(SyncedKeyIdKey, string.Empty));

            string json = storage.LoadString(EntriesJsonKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<EntriesWrapper>(json);
                    if (loaded?.Items != null) _entries.AddRange(loaded.Items);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BindingKeyModel] 解析 EntriesJson 失败：{e.Message}，使用空列表。");
                }
            }

            // 持久化
            Enabled.Register(v => storage.SaveInt(EnabledKey, v ? 1 : 0));
            SyncedKeyId.Register(v => storage.SaveString(SyncedKeyIdKey, v ?? string.Empty));
            // Entries 由 mutator 在每次修改后调用 PersistEntries
        }

        // ─── 列表方法 ──────────────────────────────────────────

        public string AddEntry()
        {
            string newId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _entries.Add(new BindingKeyEntry
            {
                Id         = newId,
                KeyCode    = DefaultBoundKeyCode,
                KeyLabel   = DefaultBoundKeyLabel,
                PressCount = 0,
                // 默认 true：UI 上没有逐项启用的 toggle，存在即计数；
                // 全局 BindingKeyModel.Enabled 仍负责整体开关。
                Enabled    = true,
            });
            PersistAndBump();
            return newId;
        }

        public bool RemoveEntry(string id)
        {
            int idx = IndexOf(id);
            if (idx < 0) return false;
            _entries.RemoveAt(idx);
            if (SyncedKeyId.Value == id) SyncedKeyId.Value = string.Empty;
            if (ListeningKeyId.Value == id) ListeningKeyId.Value = string.Empty;
            PersistAndBump();
            return true;
        }

        public bool TryUpdateEntryKey(string id, int keyCode, string keyLabel)
        {
            int idx = IndexOf(id);
            if (idx < 0) return false;
            var e = _entries[idx];
            e.KeyCode  = keyCode;
            e.KeyLabel = keyLabel ?? string.Empty;
            _entries[idx] = e;
            PersistAndBump();
            return true;
        }

        public bool IncrementEntry(string id)
        {
            int idx = IndexOf(id);
            if (idx < 0) return false;
            var e = _entries[idx];
            e.PressCount += 1;
            _entries[idx] = e;
            PersistAndBump();
            return true;
        }

        public bool ResetEntryCount(string id)
        {
            int idx = IndexOf(id);
            if (idx < 0) return false;
            var e = _entries[idx];
            e.PressCount = 0;
            _entries[idx] = e;
            PersistAndBump();
            return true;
        }

        public bool SetEntryEnabled(string id, bool enabled)
        {
            int idx = IndexOf(id);
            if (idx < 0) return false;
            var e = _entries[idx];
            e.Enabled = enabled;
            _entries[idx] = e;
            PersistAndBump();
            return true;
        }

        // ─── 内部 ─────────────────────────────────────────────

        private int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id) return i;
            }
            return -1;
        }

        private void PersistAndBump()
        {
            try
            {
                var storage = this.GetUtility<IStorageUtility>();
                var wrap = new EntriesWrapper { Items = _entries.ToArray() };
                storage.SaveString(EntriesJsonKey, JsonUtility.ToJson(wrap));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BindingKeyModel] 写 EntriesJson 失败：{e.Message}");
            }
            EntriesRevision.Value = EntriesRevision.Value + 1;
        }

        [Serializable]
        private sealed class EntriesWrapper
        {
            public BindingKeyEntry[] Items;
        }
    }
}
