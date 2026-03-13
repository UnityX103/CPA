#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CPA.Monitoring
{
    internal sealed class MacOSAppMonitorImpl : IAppMonitor
    {
        private const string DllName = "AppMonitor";
        private const int MaxAppNameLength = 256;
        private const int MaxWindowTitleLength = 1024;

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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void RequestAccessibilityPermission();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int IsAccessibilityGranted();

        internal MacOSAppMonitorImpl()
        {
            Debug.Log($"[MacOSAppMonitorImpl] 构造函数: platform={Application.platform}, isEditor={Application.isEditor}");
            try
            {
                RequestAccessibilityPermission();
                Debug.Log("[MacOSAppMonitorImpl] 已触发辅助功能权限请求");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MacOSAppMonitorImpl] 触发权限请求失败: {ex.Message}");
            }
        }

        public bool IsPermissionGranted
        {
            get
            {
                try
                {
                    return IsAccessibilityGranted() != 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void RequestPermission()
        {
            try
            {
                RequestAccessibilityPermission();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MacOSAppMonitorImpl] RequestPermission 失败: {ex.Message}");
            }
        }

        public AppInfo GetCurrentApp()
        {
            Debug.Log("[MacOSAppMonitorImpl] GetCurrentApp: 开始调用原生插件");
            var appNameBuilder = new StringBuilder(MaxAppNameLength);
            var windowTitleBuilder = new StringBuilder(MaxWindowTitleLength);

            int result;
            try
            {
                result = GetFrontmostAppInfo(
                    appNameBuilder,
                    MaxAppNameLength,
                    windowTitleBuilder,
                    MaxWindowTitleLength,
                    out IntPtr iconDataTemp,
                    out int iconLenTemp);
                Debug.Log($"[MacOSAppMonitorImpl] GetFrontmostAppInfo 返回码: {result} ({(AppMonitorResultCode)result})");

                AppMonitorResultCode resultCode = (AppMonitorResultCode)result;

                if (resultCode == AppMonitorResultCode.AccessibilityDenied)
                {
                    Debug.LogWarning("[MacOSAppMonitorImpl] Accessibility 权限被拒绝，使用 Fallback");
                    return CreateFallbackAppInfo(
                        "无法获取应用信息：请在系统偏好设置 > 安全性与隐私 > 辅助功能中授予本应用权限。");
                }

                if (resultCode != AppMonitorResultCode.Success)
                {
                    Debug.LogError($"[MacOSAppMonitorImpl] 获取失败，错误码: {resultCode}，消息: {GetErrorMessage(resultCode)}");
                    return new AppInfo
                    {
                        IsSuccess = false,
                        ErrorCode = resultCode,
                        ErrorMessage = GetErrorMessage(resultCode)
                    };
                }

                string appName = appNameBuilder.ToString();
                string windowTitle = windowTitleBuilder.ToString();
                Debug.Log($"[MacOSAppMonitorImpl] 获取成功: AppName='{appName}', WindowTitle='{windowTitle}', iconLen={iconLenTemp}");

                var appInfo = new AppInfo
                {
                    AppName = appName,
                    WindowTitle = windowTitle,
                    IsSuccess = true
                };

                if (iconDataTemp != IntPtr.Zero && iconLenTemp > 0)
                {
                    try
                    {
                        byte[] pngBytes = new byte[iconLenTemp];
                        Marshal.Copy(iconDataTemp, pngBytes, 0, iconLenTemp);
                        appInfo.Icon = PngBytesToTexture2D(pngBytes);
                        Debug.Log($"[MacOSAppMonitorImpl] 图标解析: {(appInfo.Icon != null ? "成功" : "失败")}，原始字节数={iconLenTemp}");
                    }
                    finally
                    {
                        FreeIconData(iconDataTemp);
                    }
                }
                else
                {
                    Debug.Log("[MacOSAppMonitorImpl] 无图标数据");
                }

                return appInfo;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MacOSAppMonitorImpl] 原生调用异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return new AppInfo
                {
                    IsSuccess = false,
                    ErrorMessage = $"原生调用异常: {ex.Message}"
                };
            }
        }

        public Texture2D GetAppIcon()
        {
            Debug.Log("[MacOSAppMonitorImpl] GetAppIcon 调用");
            AppInfo appInfo = GetCurrentApp();
            Debug.Log($"[MacOSAppMonitorImpl] GetAppIcon 结果: {(appInfo?.Icon != null ? "有图标" : "无图标")}");
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
#endif
