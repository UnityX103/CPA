using System;
using APP.Network.System;
using CPA.Monitoring;
using NUnit.Framework;
using UnityEngine;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class ActiveAppSystemTests
    {
        private sealed class FakeAppMonitor : IAppMonitor
        {
            public AppInfo NextAppInfo;
            public bool IsPermissionGranted => true;
            public void RequestPermission() { }
            public AppInfo GetCurrentApp() => NextAppInfo;
            public Texture2D GetAppIcon() => NextAppInfo?.Icon;
        }

        private sealed class CountingAppMonitor : IAppMonitor
        {
            private readonly IAppMonitor _inner;
            private readonly Action _onCall;
            public CountingAppMonitor(IAppMonitor inner, Action onCall) { _inner = inner; _onCall = onCall; }
            public bool IsPermissionGranted => _inner.IsPermissionGranted;
            public void RequestPermission() => _inner.RequestPermission();
            public AppInfo GetCurrentApp() { _onCall(); return _inner.GetCurrentApp(); }
            public Texture2D GetAppIcon() => _inner.GetAppIcon();
        }

        private static byte[] DummyPng => new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        [Test]
        public void Tick_FirstTimeWithNewBundleId_EmitsChanged()
        {
            var fake = new FakeAppMonitor
            {
                NextAppInfo = new AppInfo
                {
                    AppName = "Safari",
                    BundleId = "com.apple.Safari",
                    IsSuccess = true,
                }
            };
            var sys = new ActiveAppSystem(fake, () => DummyPng);

            int changedCount = 0;
            sys.Changed += _ => changedCount++;

            sys.Tick(1.5f);
            sys.Tick(1.6f);

            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(sys.Current.BundleId, Is.EqualTo("com.apple.Safari"));
            Assert.That(sys.Current.IconPngBytes, Is.EqualTo(DummyPng));
        }

        [Test]
        public void Tick_SameBundleIdTwice_EmitsOnce()
        {
            var fake = new FakeAppMonitor
            {
                NextAppInfo = new AppInfo { AppName = "Safari", BundleId = "com.apple.Safari", IsSuccess = true }
            };
            var sys = new ActiveAppSystem(fake, () => DummyPng);

            int changedCount = 0;
            sys.Changed += _ => changedCount++;

            sys.Tick(3.1f);
            sys.Tick(3.1f);

            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void Tick_WhenPermissionDenied_EmptyBundleId()
        {
            var fake = new FakeAppMonitor
            {
                NextAppInfo = new AppInfo { IsSuccess = false, ErrorCode = AppMonitorResultCode.AccessibilityDenied }
            };
            var sys = new ActiveAppSystem(fake, () => null);

            sys.Tick(3.1f);

            Assert.That(sys.Current.BundleId, Is.Empty);
        }

        [Test]
        public void Tick_Under1Second_DoesNotSample()
        {
            var fake = new FakeAppMonitor { NextAppInfo = new AppInfo { BundleId = "x", IsSuccess = true } };
            int getCalls = 0;
            var wrapped = new CountingAppMonitor(fake, () => getCalls++);
            var sys = new ActiveAppSystem(wrapped, () => DummyPng);

            sys.Tick(0.3f);
            sys.Tick(0.4f);

            Assert.That(getCalls, Is.EqualTo(0));
        }

        [Test]
        public void Tick_ReleasesTransientIconTexture()
        {
            var icon = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var fake = new FakeAppMonitor
            {
                NextAppInfo = new AppInfo
                {
                    AppName = "Safari",
                    BundleId = "com.apple.Safari",
                    Icon = icon,
                    IsSuccess = true,
                }
            };
            var sys = new ActiveAppSystem(fake);

            sys.Tick(3.1f);

            Assert.That(icon == null, Is.True);
        }
    }
}
