using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public sealed class IconCacheSystem : AbstractSystem, IIconCacheSystem
    {
        private readonly int _maxEntries;
        private readonly LinkedList<string> _order = new LinkedList<string>();
        private readonly Dictionary<string, (LinkedListNode<string> node, Texture2D tex)> _map
            = new Dictionary<string, (LinkedListNode<string>, Texture2D)>();

        public IconCacheSystem(int maxEntries = 100)
        {
            _maxEntries = Math.Max(1, maxEntries);
        }

        protected override void OnInit() { }

        public bool HasIconFor(string bundleId)
            => !string.IsNullOrEmpty(bundleId) && _map.ContainsKey(bundleId);

        public Texture2D GetTexture(string bundleId)
        {
            if (string.IsNullOrEmpty(bundleId) || !_map.TryGetValue(bundleId, out var entry)) return null;
            _order.Remove(entry.node);
            _order.AddLast(entry.node);
            return entry.tex;
        }

        public void StoreFromBase64(string bundleId, string base64)
        {
            if (string.IsNullOrEmpty(bundleId) || string.IsNullOrEmpty(base64)) return;

            byte[] png;
            try { png = Convert.FromBase64String(base64); }
            catch { return; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = $"AppIcon:{bundleId}" };
            if (!tex.LoadImage(png))
            {
                UnityEngine.Object.Destroy(tex);
                return;
            }
            tex.Apply();

            if (_map.TryGetValue(bundleId, out var old))
            {
                _order.Remove(old.node);
                if (old.tex != null) UnityEngine.Object.Destroy(old.tex);
                _map.Remove(bundleId);
            }

            var node = _order.AddLast(bundleId);
            _map[bundleId] = (node, tex);

            while (_map.Count > _maxEntries)
            {
                string oldestKey = _order.First.Value;
                _order.RemoveFirst();
                if (_map.TryGetValue(oldestKey, out var oldestEntry))
                {
                    if (oldestEntry.tex != null) UnityEngine.Object.Destroy(oldestEntry.tex);
                    _map.Remove(oldestKey);
                }
            }
        }

        public string EncodeBase64FromPngBytes(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) return string.Empty;
            return Convert.ToBase64String(pngBytes);
        }
    }
}
