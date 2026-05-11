using System;
using CPA.Monitoring;
using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public sealed class ActiveAppSystem : AbstractSystem, IActiveAppSystem
    {
        private const float SampleIntervalSeconds = 3f;

        private readonly IAppMonitor _monitor;
        private readonly Func<byte[]> _captureIconPng;
        private float _sampleAccumulator;
        private ActiveAppSnapshot _current = ActiveAppSnapshot.Empty;

        public event Action<ActiveAppSnapshot> Changed;

        public ActiveAppSystem(IAppMonitor monitor = null, Func<byte[]> captureIconPng = null)
        {
            _monitor = monitor;
            _captureIconPng = captureIconPng;
        }

        public ActiveAppSnapshot Current => _current;

        protected override void OnInit() { }

        public void Tick(float deltaTime)
        {
            _sampleAccumulator += deltaTime;
            if (_sampleAccumulator < SampleIntervalSeconds)
            {
                return;
            }

            _sampleAccumulator = 0f;

            IAppMonitor monitor = _monitor ?? ResolveDefaultMonitor();
            AppInfo info = monitor?.GetCurrentApp();

            try
            {
                if (info == null || !info.IsSuccess || string.IsNullOrEmpty(info.BundleId))
                {
                    if (!string.IsNullOrEmpty(_current.BundleId))
                    {
                        _current = ActiveAppSnapshot.Empty;
                        Changed?.Invoke(_current);
                    }

                    return;
                }

                // 即便 bundleId 不变也要重新捕获图标字节——macOS 同一 App 换版 / 首次延迟拿到真实图标 /
                // 图标文件被更新但前台 App 没切换时，bundleId 不变但图标内容已经变了。原来 bundleId-only
                // 早 return 会让下游 ICP/PlayerCard 永远卡旧图。
                byte[] png = CaptureIconPng(info);
                bool bundleChanged = info.BundleId != _current.BundleId;
                bool iconChanged = !ByteArraysEqual(png, _current.IconPngBytes);
                if (!bundleChanged && !iconChanged)
                {
                    return;
                }

                _current = new ActiveAppSnapshot(info.AppName, info.BundleId, png);
                Changed?.Invoke(_current);
            }
            finally
            {
                ReleaseTransientIcon(info?.Icon);
            }
        }

        private byte[] CaptureIconPng(AppInfo info)
        {
            byte[] raw = _captureIconPng != null
                ? _captureIconPng()
                : (info?.Icon != null ? info.Icon.EncodeToPNG() : null);
            // 归一化：空数组语义上等同 "没有图标"，统一返回 null，避免下游 ByteArraysEqual 把
            // null↔byte[0] 反复看作"图标变了"，每 3s 触发一次伪 Changed 让 UI 重复重建 fallback。
            return raw == null || raw.Length == 0 ? null : raw;
        }

        private static void ReleaseTransientIcon(Texture2D icon)
        {
            if (icon == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(icon);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(icon);
            }
        }

        private static IAppMonitor ResolveDefaultMonitor()
        {
            // AppMonitor.Instance 是 CPA.Monitoring 包暴露的单例，已实现 IAppMonitor
            return AppMonitor.Instance;
        }

        /// <summary>
        /// 全量 byte[] 等值比较，用于检测同 bundleId 下图标内容是否变化。
        /// macOS 图标 PNG 通常 &lt;10KB，每 3 秒做一次扫描可接受；这里特意不用 hash 以避免碰撞静默 stale。
        /// </summary>
        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
