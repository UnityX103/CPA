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
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

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
                    Icon = TryExtractIcon(exePath)
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


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFOW
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
        }

        /// <summary>
        /// 从 exe 路径提取大图标转为 Texture2D。失败返回 null(不抛异常,不回退)。
        /// 仅处理 32-bit BGRA 图标(Windows Vista+ Shell 默认);其他位深放弃。
        /// </summary>
        private static Texture2D TryExtractIcon(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                return null;
            }

            SHFILEINFOW sfi = default;
            IntPtr shResult;
            IntPtr hIcon = IntPtr.Zero;
            ICONINFO iconInfo = default;
            bool hasIconInfo = false;
            IntPtr hdc = IntPtr.Zero;

            try
            {
                shResult = SHGetFileInfoW(
                    exePath,
                    0,
                    ref sfi,
                    (uint)Marshal.SizeOf<SHFILEINFOW>(),
                    SHGFI_ICON | SHGFI_LARGEICON);

                if (shResult == IntPtr.Zero || sfi.hIcon == IntPtr.Zero)
                {
                    return null;
                }
                hIcon = sfi.hIcon;

                if (!GetIconInfo(hIcon, out iconInfo))
                {
                    return null;
                }
                hasIconInfo = true;

                if (iconInfo.hbmColor == IntPtr.Zero)
                {
                    return null;
                }

                if (GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), out BITMAP bm) == 0 ||
                    bm.bmWidth <= 0 || bm.bmHeight <= 0 || bm.bmBitsPixel != 32)
                {
                    return null;
                }

                int w = bm.bmWidth;
                int h = bm.bmHeight;
                byte[] buf = new byte[w * h * 4];

                BITMAPINFO bmi = default;
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
                bmi.bmiHeader.biWidth = w;
                bmi.bmiHeader.biHeight = -h;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = BI_RGB;

                hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                {
                    return null;
                }

                int scanlines = GetDIBits(hdc, iconInfo.hbmColor, 0, (uint)h, buf, ref bmi, DIB_RGB_COLORS);
                if (scanlines == 0)
                {
                    return null;
                }

                byte[] rgba = new byte[buf.Length];
                int rowBytes = w * 4;
                for (int y = 0; y < h; y++)
                {
                    int srcRow = y * rowBytes;
                    int dstRow = (h - 1 - y) * rowBytes;
                    for (int x = 0; x < w; x++)
                    {
                        int s = srcRow + x * 4;
                        int d = dstRow + x * 4;
                        rgba[d + 0] = buf[s + 2];
                        rgba[d + 1] = buf[s + 1];
                        rgba[d + 2] = buf[s + 0];
                        rgba[d + 3] = buf[s + 3];
                    }
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "AppIcon"
                };
                tex.LoadRawTextureData(rgba);
                tex.Apply();
                return tex;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WindowsAppMonitorImpl] 图标提取异常: {ex.Message}");
                return null;
            }
            finally
            {
                if (hdc != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
                if (hasIconInfo)
                {
                    if (iconInfo.hbmColor != IntPtr.Zero)
                    {
                        DeleteObject(iconInfo.hbmColor);
                    }
                    if (iconInfo.hbmMask != IntPtr.Zero)
                    {
                        DeleteObject(iconInfo.hbmMask);
                    }
                }
                if (hIcon != IntPtr.Zero)
                {
                    DestroyIcon(hIcon);
                }
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

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
        private static extern IntPtr SHGetFileInfoW(string path, uint attrs, ref SHFILEINFOW info, uint size, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr h, int size, out BITMAP bm);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFO bmi, uint usage);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr h);
    }
}
#endif
