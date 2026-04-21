using System.Collections.Generic;

namespace APP.Utility
{
    public sealed class InMemoryStorageUtility : IStorageUtility
    {
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();

        public string LoadString(string key, string fallback = "")
            => _strings.TryGetValue(key, out var v) ? v : fallback;

        public void SaveString(string key, string value) => _strings[key] = value ?? string.Empty;

        public int LoadInt(string key, int fallback = 0)
            => _ints.TryGetValue(key, out var v) ? v : fallback;

        public void SaveInt(string key, int value) => _ints[key] = value;

        public void DeleteKey(string key)
        {
            _strings.Remove(key);
            _ints.Remove(key);
        }

        public void Flush() { }

        /// <summary>测试专用：重置所有已存储的键值。</summary>
        public void Clear()
        {
            _strings.Clear();
            _ints.Clear();
        }
    }
}
