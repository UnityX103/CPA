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

                if (info.BundleId == _current.BundleId)
                {
                    return;
                }

                byte[] png = CaptureIconPng(info);
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
            if (_captureIconPng != null)
            {
                return _captureIconPng();
            }

            return info?.Icon != null ? info.Icon.EncodeToPNG() : null;
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
    }
}
