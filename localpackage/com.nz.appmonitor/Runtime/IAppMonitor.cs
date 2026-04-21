using UnityEngine;

namespace CPA.Monitoring
{
    public interface IAppMonitor
    {
        bool IsPermissionGranted { get; }
        void RequestPermission();
        AppInfo GetCurrentApp();
        Texture2D GetAppIcon();
    }
}
