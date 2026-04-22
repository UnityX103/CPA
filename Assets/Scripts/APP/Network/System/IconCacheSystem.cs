using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public sealed class IconCacheSystem : AbstractSystem, IIconCacheSystem
    {
        /// <summary>
        /// LRU 缓存节点。先落 pngBytes 作为存在性标记，GetTexture 首次访问时再懒构造 Texture2D。
        /// 这样 StoreFromBase64 可以在 EditMode（Texture2D.LoadImage 不一定可用）里被单测覆盖。
        /// </summary>
        private sealed class Entry
        {
            public LinkedListNode<string> Node;
            public byte[] PngBytes;
            public Texture2D Texture;
        }

        private readonly int _maxEntries;
        private readonly LinkedList<string> _order = new LinkedList<string>();
        private readonly Dictionary<string, Entry> _map = new Dictionary<string, Entry>();

        public IconCacheSystem(int maxEntries = 100)
        {
            _maxEntries = Math.Max(1, maxEntries);
        }

        protected override void OnInit() { }

        public bool HasIconFor(string bundleId)
            => !string.IsNullOrEmpty(bundleId) && _map.ContainsKey(bundleId);

        public Texture2D GetTexture(string bundleId)
        {
            if (string.IsNullOrEmpty(bundleId) || !_map.TryGetValue(bundleId, out Entry entry))
                return null;

            _order.Remove(entry.Node);
            entry.Node = _order.AddLast(bundleId);

            if (entry.Texture == null && entry.PngBytes != null)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = $"AppIcon:{bundleId}" };
                if (tex.LoadImage(entry.PngBytes))
                {
                    tex.Apply();
                    entry.Texture = tex;
                }
                else
                {
                    DestroySafe(tex);
                }
            }

            return entry.Texture;
        }

        public void StoreFromBase64(string bundleId, string base64)
        {
            if (string.IsNullOrEmpty(bundleId) || string.IsNullOrEmpty(base64)) return;

            byte[] png;
            try { png = Convert.FromBase64String(base64); }
            catch { return; }
            if (png.Length == 0) return;

            if (_map.TryGetValue(bundleId, out Entry existing))
            {
                _order.Remove(existing.Node);
                DestroySafe(existing.Texture);
                existing.Texture = null;
                existing.PngBytes = png;
                existing.Node = _order.AddLast(bundleId);
            }
            else
            {
                var entry = new Entry
                {
                    PngBytes = png,
                    Texture = null,
                    Node = _order.AddLast(bundleId),
                };
                _map[bundleId] = entry;
            }

            while (_map.Count > _maxEntries)
            {
                string oldestKey = _order.First.Value;
                _order.RemoveFirst();
                if (_map.TryGetValue(oldestKey, out Entry oldest))
                {
                    DestroySafe(oldest.Texture);
                    _map.Remove(oldestKey);
                }
            }
        }

        public string EncodeBase64FromPngBytes(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) return string.Empty;
            return Convert.ToBase64String(pngBytes);
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
