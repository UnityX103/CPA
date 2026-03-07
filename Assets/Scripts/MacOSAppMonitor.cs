using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CPA.Monitoring
{
    public enum AppMonitorResultCode
    {
        Success = 0,
        InvalidArgument = -1,
        AccessibilityDenied = -2,
        NoFrontmostApp = -3,
        IconAllocationFailed = -4
    }

    public class PermissionDeniedException : Exception
    {
        public PermissionDeniedException(string message) : base(message) { }
        public PermissionDeniedException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class AppInfo
    {
        public string AppName;
        public string WindowTitle;
        public Texture2D Icon;
        public bool IsSuccess;
        public AppMonitorResultCode? ErrorCode;
        public string ErrorMessage;
    }

    public sealed class MacOSAppMonitor
    {
        private static MacOSAppMonitor _instance;
        private static readonly object _lock = new object();

        public static MacOSAppMonitor Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MacOSAppMonitor();
                        }
                    }
                }
                return _instance;
            }
        }

        private MacOSAppMonitor() { }

#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
        private const string DllName = "__Internal";
#elif UNITY_STANDALONE_OSX
        private const string DllName = "AppMonitor";
#else
        private const string DllName = "";
#endif

#if UNITY_STANDALONE_OSX
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetFrontmostAppInfo(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder appName,
            int nameLen,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder windowTitle,
            int titleLen,
            out IntPtr iconData,
            out int iconLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeIconData(IntPtr data);
#endif

        private const int MaxAppNameLength = 256;
        private const int MaxWindowTitleLength = 1024;

        public AppInfo GetCurrentApp()
        {
#if !UNITY_STANDALONE_OSX
            return new AppInfo
            {
                IsSuccess = false,
                ErrorMessage = "MacOSAppMonitor 仅在 macOS 平台可用"
            };
#else
            var appNameBuilder = new StringBuilder(MaxAppNameLength);
            var windowTitleBuilder = new StringBuilder(MaxWindowTitleLength);

            int result = GetFrontmostAppInfo(
                appNameBuilder,
                MaxAppNameLength,
                windowTitleBuilder,
                MaxWindowTitleLength,
                out IntPtr iconData,
                out int iconLen);

            AppMonitorResultCode resultCode = (AppMonitorResultCode)result;

            if (resultCode == AppMonitorResultCode.AccessibilityDenied)
            {
                return CreateFallbackAppInfo(
                    "无法获取应用信息：请在系统偏好设置 > 安全性与隐私 > 辅助功能中授予本应用权限。");
            }

            if (resultCode != AppMonitorResultCode.Success)
            {
                return new AppInfo
                {
                    IsSuccess = false,
                    ErrorCode = resultCode,
                    ErrorMessage = GetErrorMessage(resultCode)
                };
            }

            var appInfo = new AppInfo
            {
                AppName = appNameBuilder.ToString(),
                WindowTitle = windowTitleBuilder.ToString(),
                IsSuccess = true
            };

            if (iconData != IntPtr.Zero && iconLen > 0)
            {
                try
                {
                    byte[] pngBytes = new byte[iconLen];
                    Marshal.Copy(iconData, pngBytes, 0, iconLen);
                    appInfo.Icon = PngBytesToTexture2D(pngBytes);
                }
                finally
                {
                    FreeIconData(iconData);
                }
            }

            return appInfo;
#endif
        }

        public Texture2D GetAppIcon()
        {
            AppInfo appInfo = GetCurrentApp();
            return appInfo?.Icon;
        }

        private static Texture2D PngBytesToTexture2D(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            
            if (texture.LoadImage(pngBytes))
            {
                texture.name = "AppIcon";
                texture.Apply();
                return texture;
            }
            
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        private static string GetErrorMessage(AppMonitorResultCode code)
        {
            return code switch
            {
                AppMonitorResultCode.InvalidArgument => "参数非法",
                AppMonitorResultCode.AccessibilityDenied => "Accessibility 权限未授予",
                AppMonitorResultCode.NoFrontmostApp => "未找到前台应用",
                AppMonitorResultCode.IconAllocationFailed => "图标内存分配失败",
                _ => $"未知错误 (错误码: {(int)code})"
            };
        }

        private static AppInfo CreateFallbackAppInfo(string reason)
        {
            string processName = null;

            try
            {
                processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(processName))
            {
                processName = Application.isEditor ? "Unity Editor" : Application.productName;
            }

            return new AppInfo
            {
                AppName = processName,
                WindowTitle = string.Empty,
                Icon = CreateFallbackIcon(),
                IsSuccess = true,
                ErrorCode = AppMonitorResultCode.AccessibilityDenied,
                ErrorMessage = reason
            };
        }

        private static Texture2D CreateFallbackIcon()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "FallbackAppIcon"
            };

            var pixels = new Color[size * size];
            Color fallbackColor = new Color(0.24f, 0.56f, 0.96f, 1f);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = fallbackColor;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
