using System.Collections.Generic;

namespace APP.Utility
{
    public sealed class InMemoryStorageUtility : IStorageUtility
    {
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();

        public string LoadString(string key, string fallback = "")
            => _strings.TryGetValue(key, out var v) ? v : fallback;

        public void SaveString(string key, string value) => _strings[key] = value ?? string.Empty;

        public int LoadInt(string key, int fallback = 0)
            => _ints.TryGetValue(key, out var v) ? v : fallback;

        public void SaveInt(string key, int value) => _ints[key] = value;

        public float LoadFloat(string key, float fallback = 0f)
            => _floats.TryGetValue(key, out var v) ? v : fallback;

        public void SaveFloat(string key, float value) => _floats[key] = value;

        public void DeleteKey(string key)
        {
            _strings.Remove(key);
            _ints.Remove(key);
            _floats.Remove(key);
        }

        public void Flush() { }

        /// <summary>测试专用：重置所有已存储的键值。</summary>
        public void Clear()
        {
            _strings.Clear();
            _ints.Clear();
            _floats.Clear();
        }
    }
}
