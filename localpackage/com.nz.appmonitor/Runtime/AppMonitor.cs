using UnityEngine;

namespace CPA.Monitoring
{
    public sealed class AppMonitor : IAppMonitor
    {
        public static AppMonitor Instance { get; } = new AppMonitor();

        private readonly IAppMonitor _impl;

        private AppMonitor()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            _impl = new MacOSAppMonitorImpl();
#else
            _impl = new UnsupportedAppMonitorImpl();
#endif
        }

        public bool IsPermissionGranted => _impl.IsPermissionGranted;

        public void RequestPermission() => _impl.RequestPermission();

        public AppInfo GetCurrentApp() => _impl.GetCurrentApp();

        public Texture2D GetAppIcon() => _impl.GetAppIcon();
    }
}
