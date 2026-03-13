using UnityEngine;

namespace CPA.Monitoring
{
    internal sealed class UnsupportedAppMonitorImpl : IAppMonitor
    {
        public bool IsPermissionGranted => false;

        public void RequestPermission() { }

        public AppInfo GetCurrentApp()
        {
            return new AppInfo
            {
                IsSuccess = false,
                ErrorMessage = "当前平台不支持"
            };
        }

        public Texture2D GetAppIcon() => null;
    }
}
