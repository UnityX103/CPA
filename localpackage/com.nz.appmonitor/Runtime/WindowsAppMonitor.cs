#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CPA.Monitoring
{
    /// <summary>
    /// Windows 前台应用监控实现。通过 P/Invoke 调用 Win32 API 获取前台窗口信息,
    /// 无需特殊权限,不依赖任何原生 DLL。
    /// </summary>
    internal sealed class WindowsAppMonitorImpl : IAppMonitor
    {
        private const int MaxWindowTitleLength = 1024;
        private const int MaxPathLength = 1024;
        private const uint ProcessQueryLimitedInformation = 0x1000;

        public bool IsPermissionGranted => true;

        public void RequestPermission() { }

        public AppInfo GetCurrentApp()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    return new AppInfo
                    {
                        IsSuccess = false,
                        ErrorCode = AppMonitorResultCode.NoFrontmostApp,
                        ErrorMessage = "未找到前台窗口"
                    };
                }

                string windowTitle = GetWindowTitleSafe(hwnd);

                GetWindowThreadProcessId(hwnd, out uint pid);
                string exePath = TryGetProcessExePath(pid);
                if (string.IsNullOrEmpty(exePath))
                {
                    return new AppInfo
                    {
                        IsSuccess = false,
                        ErrorCode = AppMonitorResultCode.NoFrontmostApp,
                        ErrorMessage = "无法打开前台进程(可能受保护/Elevated)"
                    };
                }

                string fileNameNoExt = Path.GetFileNameWithoutExtension(exePath) ?? string.Empty;
                string appName = TryGetFileDescription(exePath);
                if (string.IsNullOrEmpty(appName))
                {
                    appName = fileNameNoExt;
                }
                string bundleId = fileNameNoExt.ToLowerInvariant();

                return new AppInfo
                {
                    IsSuccess = true,
                    AppName = appName,
                    BundleId = bundleId,
                    WindowTitle = windowTitle ?? string.Empty,
                    Icon = null
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WindowsAppMonitorImpl] 原生调用异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return new AppInfo
                {
                    IsSuccess = false,
                    ErrorMessage = $"原生调用异常: {ex.Message}"
                };
            }
        }

        public Texture2D GetAppIcon()
        {
            AppInfo appInfo = GetCurrentApp();
            return appInfo?.Icon;
        }

        // ──────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────

        private static string GetWindowTitleSafe(IntPtr hwnd)
        {
            int len = GetWindowTextLengthW(hwnd);
            if (len <= 0)
            {
                return string.Empty;
            }

            int bufSize = Math.Min(len + 1, MaxWindowTitleLength);
            var buf = new StringBuilder(bufSize);
            int copied = GetWindowTextW(hwnd, buf, bufSize);
            return copied > 0 ? buf.ToString() : string.Empty;
        }

        private static string TryGetProcessExePath(uint pid)
        {
            IntPtr hProc = OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (hProc == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var buf = new StringBuilder(MaxPathLength);
                int size = buf.Capacity;
                if (!QueryFullProcessImageNameW(hProc, 0, buf, ref size))
                {
                    return null;
                }
                return buf.ToString();
            }
            finally
            {
                CloseHandle(hProc);
            }
        }

        /// <summary>
        /// 尝试从 exe 的版本资源读取 FileDescription。失败返回 null。
        /// 流程:GetFileVersionInfoSizeW → GetFileVersionInfoW → VerQueryValueW(\VarFileInfo\Translation) → VerQueryValueW(\StringFileInfo\{lang}\FileDescription)
        /// </summary>
        private static string TryGetFileDescription(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                return null;
            }

            try
            {
                int size = GetFileVersionInfoSizeW(exePath, out _);
                if (size <= 0)
                {
                    return null;
                }

                byte[] data = new byte[size];
                if (!GetFileVersionInfoW(exePath, 0, size, data))
                {
                    return null;
                }

                if (!VerQueryValueW(data, @"\VarFileInfo\Translation", out IntPtr translationPtr, out uint translationLen) ||
                    translationPtr == IntPtr.Zero || translationLen < 4)
                {
                    return null;
                }

                ushort langId = (ushort)Marshal.ReadInt16(translationPtr);
                ushort codePage = (ushort)Marshal.ReadInt16(translationPtr, 2);
                string subBlock = $@"\StringFileInfo\{langId:X4}{codePage:X4}\FileDescription";

                if (!VerQueryValueW(data, subBlock, out IntPtr descPtr, out uint descLen) ||
                    descPtr == IntPtr.Zero || descLen == 0)
                {
                    return null;
                }

                string description = Marshal.PtrToStringUni(descPtr);
                return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            }
            catch
            {
                return null;
            }
        }

        // ──────────────────────────────────────────────
        // P/Invoke
        // ──────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder buf, int max);

        [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
        private static extern int GetWindowTextLengthW(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
        private static extern bool QueryFullProcessImageNameW(IntPtr h, uint flags, StringBuilder buf, ref int size);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);

        [DllImport("version.dll", CharSet = CharSet.Unicode, EntryPoint = "GetFileVersionInfoSizeW")]
        private static extern int GetFileVersionInfoSizeW(string path, out int handleIgnored);

        [DllImport("version.dll", CharSet = CharSet.Unicode, EntryPoint = "GetFileVersionInfoW")]
        private static extern bool GetFileVersionInfoW(string path, int handleIgnored, int len, byte[] data);

        [DllImport("version.dll", CharSet = CharSet.Unicode, EntryPoint = "VerQueryValueW")]
        private static extern bool VerQueryValueW(byte[] data, string subBlock, out IntPtr buf, out uint len);
    }
}
#endif
