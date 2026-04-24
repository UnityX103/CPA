#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using UnityEngine;

namespace CPA.Monitoring
{
    /// <summary>
    /// Windows 前台应用监控实现。通过 P/Invoke 调用 Win32 API 获取前台窗口信息,
    /// 无需特殊权限,不依赖任何原生 DLL。
    /// </summary>
    internal sealed class WindowsAppMonitorImpl : IAppMonitor
    {
        public bool IsPermissionGranted => true;

        public void RequestPermission() { }

        public AppInfo GetCurrentApp()
        {
            return new AppInfo
            {
                IsSuccess = false,
                ErrorMessage = "Windows 实现占位(Task 1)"
            };
        }

        public Texture2D GetAppIcon()
        {
            AppInfo appInfo = GetCurrentApp();
            return appInfo?.Icon;
        }
    }
}
#endif
