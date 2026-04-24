# com.nz.appmonitor Windows 支持 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 `com.nz.appmonitor` 包补充 Windows Standalone/Editor 平台实现,让 `AppMonitor.Instance.GetCurrentApp()` 在 Windows 返回真实前台应用信息,对外契约与调用方零改动。

**Architecture:** 在 `Runtime/` 目录新增 `WindowsAppMonitor.cs`,通过纯 C# P/Invoke 调用 Windows `user32`/`kernel32`/`shell32`/`gdi32`/`version` 系统 DLL,不引入任何原生 DLL 或构建工具链。`AppMonitor.cs` 工厂分支追加 `UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN` → `WindowsAppMonitorImpl`。测试侧将既有"非 macOS 降级"断言条件收窄,新增 Windows 正向断言。

**Tech Stack:** Unity 6000.0.25f1、C# P/Invoke、Win32 API (`GetForegroundWindow`/`QueryFullProcessImageNameW`/`SHGetFileInfoW`/`GetDIBits`/`GetFileVersionInfoW`)。

**参考 Spec:** `docs/superpowers/specs/2026-04-24-appmonitor-windows-support-design.md`

**实现工程约定(重要):**
- **TDD 顺序反转**:项目 CLAUDE.md/用户偏好要求"先写实现再写测试"——Unity 下"先让测试失败"会触发全工程 CS0234 编译失败。本计划所有任务均遵循"实现 → 切换 Windows target 验证编译 → 补测试"顺序。
- **验证环境**:开发机为 macOS。编译正确性通过"切换 Unity Build Target 为 StandaloneWindows64 → `read_console` 查错"验证。Windows Player 运行时行为需在 Windows 物理机由用户执行(本计划提供命令,但不要求在 Mac 上完成)。
- **Meta 文件**:所有新增 `.cs` 的 `.cs.meta` 由 Unity 自动生成,**不手写 GUID**(记忆条目:feedback_unity_meta_guid)。

---

## File Structure

**新增文件**
- `localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs` — Windows 实现主文件,包含所有 P/Invoke 签名、结构体、`WindowsAppMonitorImpl` 类。单文件边界:所有 Windows 专属代码集中于此,不污染其他文件。

**修改文件**
- `localpackage/com.nz.appmonitor/Runtime/AppMonitor.cs` — 工厂分支追加 Windows 条件编译。
- `localpackage/com.nz.appmonitor/Tests/Runtime/AppMonitorPlayerTest.cs` — 测试条件编译块调整。
- `localpackage/com.nz.appmonitor/package.json` — 元数据更新。
- `localpackage/com.nz.appmonitor/README.md` — 文档更新。

**不动**
- `Runtime/IAppMonitor.cs`、`Runtime/AppMonitorData.cs`、`Runtime/MacOSAppMonitor.cs`、`Runtime/UnsupportedAppMonitorImpl.cs`、`Runtime/NZ.AppMonitor.Runtime.asmdef`、`Plugins/macOS/*`、`Assets/Scripts/APP/Network/System/ActiveAppSystem.cs`。

---

## Task 1: 创建 Windows 实现骨架并打通工厂

**Files:**
- Create: `localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs`
- Modify: `localpackage/com.nz.appmonitor/Runtime/AppMonitor.cs`(约第 13-17 行的 `#if` 块)

**目标:** 让 Windows target 下能编译通过、能命中 `WindowsAppMonitorImpl`,但所有方法先返回占位值。后续任务逐步填充真实逻辑。

- [ ] **Step 1: 创建 WindowsAppMonitor.cs 骨架**

写入 `localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs`:

```csharp
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
```

- [ ] **Step 2: 修改 AppMonitor.cs 工厂分支**

读取 `localpackage/com.nz.appmonitor/Runtime/AppMonitor.cs`,把:

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            _impl = new MacOSAppMonitorImpl();
#else
            _impl = new UnsupportedAppMonitorImpl();
#endif
```

改为:

```csharp
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            _impl = new MacOSAppMonitorImpl();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            _impl = new WindowsAppMonitorImpl();
#else
            _impl = new UnsupportedAppMonitorImpl();
#endif
```

- [ ] **Step 3: 切换 Unity Build Target 为 StandaloneWindows64 验证编译**

使用 MCP 工具切换构建平台并刷新编译:

```
mcp__UnityMCP__manage_editor(action="set_build_target", build_target="StandaloneWindows64")
mcp__UnityMCP__refresh_unity()
```

等待编译完成(轮询 `mcp__UnityMCP__manage_editor(action="get_state")` 的 `isCompiling` 字段直到 `false`),然后:

```
mcp__UnityMCP__read_console(types=["Error"], count=50)
```

**Expected:** 无 error。若有未定义符号、未引用命名空间等错误,修正后重试。

- [ ] **Step 4: 提交**

```bash
git add localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs \
        localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs.meta \
        localpackage/com.nz.appmonitor/Runtime/AppMonitor.cs
git commit -m "feat(appmonitor): add Windows impl skeleton and factory wire-up"
```

---

## Task 2: 实现 GetCurrentApp 核心流程(前台窗口 + 进程路径)

**Files:**
- Modify: `localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs`

**目标:** 填充真实的 `GetCurrentApp()` 主流程:获取前台 HWND、窗口标题、PID、exe 完整路径。AppName 临时用 exe 文件名(Task 3 再优化),图标仍返回 null(Task 4)。

- [ ] **Step 1: 覆写 WindowsAppMonitor.cs 添加 P/Invoke 基础设施和 GetCurrentApp 核心实现**

完整重写 `localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs` 为:

```csharp
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
                string appName = fileNameNoExt;
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
    }
}
#endif
```

- [ ] **Step 2: 验证 Windows target 编译**

(当前 Build Target 仍为 StandaloneWindows64,无需切换)

```
mcp__UnityMCP__refresh_unity()
```

轮询 `isCompiling=false` 后:

```
mcp__UnityMCP__read_console(types=["Error"], count=50)
```

**Expected:** 无 error。

- [ ] **Step 3: 提交**

```bash
git add localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs
git commit -m "feat(appmonitor): implement Windows GetCurrentApp core (foreground window + process path)"
```

---

## Task 3: AppName 优先使用 FileDescription(版本资源)

**Files:**
- Modify: `localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs`

**目标:** 让 `AppName` 更友好——优先从 exe 的版本资源 `FileDescription` 读取(如 `chrome.exe` → `Google Chrome`),失败则回退到文件名。

- [ ] **Step 1: 在 WindowsAppMonitor.cs 中添加 FileDescription 读取逻辑**

找到 `GetCurrentApp()` 中的:

```csharp
                string fileNameNoExt = Path.GetFileNameWithoutExtension(exePath) ?? string.Empty;
                string appName = fileNameNoExt;
                string bundleId = fileNameNoExt.ToLowerInvariant();
```

改为:

```csharp
                string fileNameNoExt = Path.GetFileNameWithoutExtension(exePath) ?? string.Empty;
                string appName = TryGetFileDescription(exePath);
                if (string.IsNullOrEmpty(appName))
                {
                    appName = fileNameNoExt;
                }
                string bundleId = fileNameNoExt.ToLowerInvariant();
```

在 `TryGetProcessExePath` 方法下方、P/Invoke 区之前,插入辅助方法:

```csharp
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

                // 读取语言/码页(Translation 是 DWORD 数组:低字=langId, 高字=codepage)
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
```

在 P/Invoke 区末尾(现有 `CloseHandle` 之后)追加 `version.dll` 导入:

```csharp
        [DllImport("version.dll", CharSet = CharSet.Unicode, EntryPoint = "GetFileVersionInfoSizeW")]
        private static extern int GetFileVersionInfoSizeW(string path, out int handleIgnored);

        [DllImport("version.dll", CharSet = CharSet.Unicode, EntryPoint = "GetFileVersionInfoW")]
        private static extern bool GetFileVersionInfoW(string path, int handleIgnored, int len, byte[] data);

        [DllImport("version.dll", CharSet = CharSet.Unicode, EntryPoint = "VerQueryValueW")]
        private static extern bool VerQueryValueW(byte[] data, string subBlock, out IntPtr buf, out uint len);
```

- [ ] **Step 2: 验证编译**

```
mcp__UnityMCP__refresh_unity()
```

等 `isCompiling=false` 后:

```
mcp__UnityMCP__read_console(types=["Error"], count=50)
```

**Expected:** 无 error。

- [ ] **Step 3: 提交**

```bash
git add localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs
git commit -m "feat(appmonitor): use exe FileDescription for Windows AppName, fallback to filename"
```

---

## Task 4: 图标提取管线(HICON → Texture2D)

**Files:**
- Modify: `localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs`

**目标:** 从 exe 路径提取图标,填入 `AppInfo.Icon`。失败 → null,不抛异常,不回退占位图。

- [ ] **Step 1: 在 WindowsAppMonitor.cs 中添加图标常量、结构体、方法**

在类顶部常量区(`ProcessQueryLimitedInformation` 常量下方),追加:

```csharp
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;
```

在 `WindowsAppMonitorImpl` 类内,P/Invoke 区之前,追加 Win32 结构体声明:

```csharp
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
            // 32-bit BI_RGB 无需 bmiColors 数组
        }
```

把 `GetCurrentApp` 中对 Icon 的赋值从 `Icon = null` 改为:

```csharp
                    Icon = TryExtractIcon(exePath)
```

在辅助方法区(`TryGetFileDescription` 下方)追加图标提取方法:

```csharp
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
            IntPtr shResult = IntPtr.Zero;
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
                    return null; // 仅有 mask 的老式单色图标,放弃
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
                bmi.bmiHeader.biHeight = -h; // 负值:自顶向下
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

                // BGRA 自顶向下 → RGBA 自底向上(Unity 坐标系)
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
                        rgba[d + 0] = buf[s + 2]; // R <- B
                        rgba[d + 1] = buf[s + 1]; // G
                        rgba[d + 2] = buf[s + 0]; // B <- R
                        rgba[d + 3] = buf[s + 3]; // A
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
```

在 P/Invoke 区末尾追加:

```csharp
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
```

- [ ] **Step 2: 验证编译**

```
mcp__UnityMCP__refresh_unity()
```

等 `isCompiling=false`,然后:

```
mcp__UnityMCP__read_console(types=["Error"], count=50)
```

**Expected:** 无 error。

- [ ] **Step 3: 提交**

```bash
git add localpackage/com.nz.appmonitor/Runtime/WindowsAppMonitor.cs
git commit -m "feat(appmonitor): extract Windows app icon via SHGetFileInfo + GetDIBits"
```

---

## Task 5: 收窄既有非 macOS 测试的条件编译

**Files:**
- Modify: `localpackage/com.nz.appmonitor/Tests/Runtime/AppMonitorPlayerTest.cs`(约第 59-66 行、第 140-149 行、第 178-185 行)

**目标:** 把三处 `#if !UNITY_STANDALONE_OSX` 块(声明"非 macOS 降级")改为 `#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN`,并把方法名从 `_NonMacOS_` 改为 `_UnsupportedPlatform_`。

- [ ] **Step 1: 修改权限测试分支(第 59-66 行附近)**

找到:

```csharp
#if !UNITY_STANDALONE_OSX
        [Test]
        public void IsPermissionGranted_NonMacOS_ReturnsFalse()
        {
            Assert.IsFalse(CPA.Monitoring.AppMonitor.Instance.IsPermissionGranted,
                "非 macOS 平台 IsPermissionGranted 应始终返回 false");
        }
#endif
```

改为:

```csharp
#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN
        [Test]
        public void IsPermissionGranted_UnsupportedPlatform_ReturnsFalse()
        {
            Assert.IsFalse(CPA.Monitoring.AppMonitor.Instance.IsPermissionGranted,
                "非支持平台(macOS/Windows 之外)IsPermissionGranted 应始终返回 false");
        }
#endif
```

- [ ] **Step 2: 修改 GetCurrentApp 降级测试(第 140-149 行附近)**

找到:

```csharp
#else
        [Test]
        public void GetCurrentApp_NonMacOS_ReturnsPlatformUnsupported()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsFalse(result.IsSuccess, "非 macOS 平台 GetCurrentApp() 应返回失败");
            Assert.AreEqual("当前平台不支持", result.ErrorMessage,
                "非 macOS 平台错误消息应为 '当前平台不支持'");
        }
#endif
```

注意上下文:这是嵌在 `#if UNITY_STANDALONE_OSX` / `#else` / `#endif` 块里。整个块的前后结构要调整。找到完整块:

```csharp
#if UNITY_STANDALONE_OSX
        [Test]
        public void GetCurrentApp_macOS_ReturnsAppName()
        {
            // ... 保持不变
        }

        [Test]
        public void GetCurrentApp_macOS_WindowTitle_IsString()
        {
            // ... 保持不变
        }

        [Test]
        public void GetCurrentApp_macOS_ErrorMessage_NotUnsupportedPlatform()
        {
            // ... 保持不变
        }
#else
        [Test]
        public void GetCurrentApp_NonMacOS_ReturnsPlatformUnsupported()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsFalse(result.IsSuccess, "非 macOS 平台 GetCurrentApp() 应返回失败");
            Assert.AreEqual("当前平台不支持", result.ErrorMessage,
                "非 macOS 平台错误消息应为 '当前平台不支持'");
        }
#endif
```

改为(把 `#else` 替换为独立条件块,后续 Task 6 会再插 Windows 分支):

```csharp
#if UNITY_STANDALONE_OSX
        [Test]
        public void GetCurrentApp_macOS_ReturnsAppName()
        {
            // ... 保持不变
        }

        [Test]
        public void GetCurrentApp_macOS_WindowTitle_IsString()
        {
            // ... 保持不变
        }

        [Test]
        public void GetCurrentApp_macOS_ErrorMessage_NotUnsupportedPlatform()
        {
            // ... 保持不变
        }
#endif

#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN
        [Test]
        public void GetCurrentApp_UnsupportedPlatform_ReturnsPlatformUnsupported()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsFalse(result.IsSuccess, "非支持平台 GetCurrentApp() 应返回失败");
            Assert.AreEqual("当前平台不支持", result.ErrorMessage,
                "非支持平台错误消息应为 '当前平台不支持'");
        }
#endif
```

- [ ] **Step 3: 修改 GetAppIcon 降级测试(第 178-185 行附近)**

找到:

```csharp
#if UNITY_STANDALONE_OSX
        [Test]
        public void GetAppIcon_macOS_ReturnsTexture()
        {
            // ... 保持不变
        }
#else
        [Test]
        public void GetAppIcon_NonMacOS_ReturnsNull()
        {
            Texture2D icon = CPA.Monitoring.AppMonitor.Instance.GetAppIcon();
            Assert.IsNull(icon, "非 macOS 平台 GetAppIcon() 应返回 null");
        }
#endif
```

改为:

```csharp
#if UNITY_STANDALONE_OSX
        [Test]
        public void GetAppIcon_macOS_ReturnsTexture()
        {
            // ... 保持不变
        }
#endif

#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN
        [Test]
        public void GetAppIcon_UnsupportedPlatform_ReturnsNull()
        {
            Texture2D icon = CPA.Monitoring.AppMonitor.Instance.GetAppIcon();
            Assert.IsNull(icon, "非支持平台 GetAppIcon() 应返回 null");
        }
#endif
```

- [ ] **Step 4: 验证编译(当前 Build Target 仍是 Windows)**

```
mcp__UnityMCP__refresh_unity()
```

等 `isCompiling=false` 后:

```
mcp__UnityMCP__read_console(types=["Error"], count=50)
```

**Expected:** 无 error。测试会出现在 TestRunner 中但因测试程序集受 `UNITY_INCLUDE_TESTS` 守卫,实际执行需等 Task 8 完成后在真实 Windows 机器上跑。

- [ ] **Step 5: 提交**

```bash
git add localpackage/com.nz.appmonitor/Tests/Runtime/AppMonitorPlayerTest.cs
git commit -m "test(appmonitor): narrow unsupported-platform assertions to exclude Windows"
```

---

## Task 6: 新增 Windows 正向测试

**Files:**
- Modify: `localpackage/com.nz.appmonitor/Tests/Runtime/AppMonitorPlayerTest.cs`

**目标:** 在 Task 5 新增的条件编译块之后,追加 `#if UNITY_STANDALONE_WIN` 的 Windows 专属断言。

- [ ] **Step 1: 在 AppMonitorPlayerTest.cs 中追加 Windows 测试块**

在第 4 节"平台分支验证"末尾(刚新增的 `#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN` 块之后),加入:

```csharp
#if UNITY_STANDALONE_WIN
        [Test]
        public void GetCurrentApp_Windows_AppName_NotNull()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsNotNull(result, "Windows 上 GetCurrentApp() 不应返回 null");

            // batchmode 可能拿不到 FG 窗口,允许 NoFrontmostApp
            if (result.IsSuccess)
            {
                Assert.IsNotNull(result.AppName, "Windows 成功状态下 AppName 不应为 null");
                Debug.Log($"[AppMonitorPlayerTest] Win AppName='{result.AppName}', BundleId='{result.BundleId}', WindowTitle='{result.WindowTitle}'");
            }
            else
            {
                Assert.AreEqual(AppMonitorResultCode.NoFrontmostApp, result.ErrorCode,
                    "Windows 失败时错误码应为 NoFrontmostApp(例如 batchmode 无前台窗口)");
            }
        }

        [Test]
        public void GetCurrentApp_Windows_BundleId_IsLower()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            if (result.IsSuccess && result.BundleId != null)
            {
                Assert.AreEqual(result.BundleId.ToLowerInvariant(), result.BundleId,
                    "Windows 上 BundleId 应为全小写(exe 文件名)");
            }
        }

        [Test]
        public void GetCurrentApp_Windows_ErrorMessage_NotUnsupportedPlatform()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            if (result.ErrorMessage != null)
            {
                StringAssert.DoesNotContain("当前平台不支持", result.ErrorMessage,
                    "Windows 上不应出现 UnsupportedAppMonitorImpl 的错误消息");
            }
        }
#endif
```

同时在第 2 节"权限 API"末尾追加:

```csharp
#if UNITY_STANDALONE_WIN
        [Test]
        public void IsPermissionGranted_Windows_ReturnsTrue()
        {
            Assert.IsTrue(CPA.Monitoring.AppMonitor.Instance.IsPermissionGranted,
                "Windows 上 IsPermissionGranted 应始终返回 true(无权限要求)");
        }
#endif
```

同时在第 5 节"GetAppIcon"末尾追加:

```csharp
#if UNITY_STANDALONE_WIN
        [Test]
        public void GetAppIcon_Windows_ReturnsTextureOrNull()
        {
            Texture2D icon = null;
            Assert.DoesNotThrow(() => icon = CPA.Monitoring.AppMonitor.Instance.GetAppIcon(),
                "Windows 上 GetAppIcon() 不应抛出异常");
            // 允许 null(batchmode 或受保护进程)或非 null(正常 GUI 应用)
            if (icon != null)
            {
                Assert.Greater(icon.width, 0, "图标宽度应 > 0");
                Assert.Greater(icon.height, 0, "图标高度应 > 0");
                UnityEngine.Object.Destroy(icon);
            }
        }
#endif
```

- [ ] **Step 2: 验证编译**

```
mcp__UnityMCP__refresh_unity()
```

等 `isCompiling=false` 后:

```
mcp__UnityMCP__read_console(types=["Error"], count=50)
```

**Expected:** 无 error。

- [ ] **Step 3: 提交**

```bash
git add localpackage/com.nz.appmonitor/Tests/Runtime/AppMonitorPlayerTest.cs
git commit -m "test(appmonitor): add Windows positive assertions for GetCurrentApp/Icon/Permission"
```

---

## Task 7: 更新 package.json 与 README.md

**Files:**
- Modify: `localpackage/com.nz.appmonitor/package.json`
- Modify: `localpackage/com.nz.appmonitor/README.md`

**目标:** 元数据和文档反映新增的 Windows 支持。

- [ ] **Step 1: 修改 package.json**

读取当前 `localpackage/com.nz.appmonitor/package.json`,把:

```json
{
  "name": "com.nz.appmonitor",
  "version": "1.0.0",
  "displayName": "NZ App Monitor",
  "description": "macOS 前台应用监控:通过系统 Accessibility API 实时获取前台应用名称、窗口标题和应用图标。提供跨平台接口抽象,非 macOS 平台自动降级为无操作实现。",
```

改为:

```json
{
  "name": "com.nz.appmonitor",
  "version": "1.1.0",
  "displayName": "NZ App Monitor",
  "description": "macOS/Windows 前台应用监控:macOS 通过 Accessibility API,Windows 通过 Win32 P/Invoke,实时获取前台应用名称、窗口标题和应用图标。其他平台自动降级为无操作实现。",
```

keywords 数组追加 `"windows"`、`"win32"`:

```json
  "keywords": [
    "macos",
    "windows",
    "win32",
    "accessibility",
    "appmonitor",
    "foreground-app",
    "window-title"
  ],
```

- [ ] **Step 2: 修改 README.md 顶部简介和功能表**

把第 3 行 `macOS 前台应用监控包。通过系统 **Accessibility API** 实时获取前台应用名称、窗口标题和应用图标。` 改为:

```markdown
macOS 与 Windows 的前台应用监控包。macOS 通过 **Accessibility API**,Windows 通过 **Win32 P/Invoke**(无需特殊权限),实时获取前台应用名称、窗口标题和应用图标。
```

在"功能"表格后(第 16 行的"非 macOS 平台..."那段)把:

```markdown
非 macOS 平台(Windows、Linux、编辑器非 macOS 等)自动降级为无操作实现,`GetCurrentApp()` 返回 `IsSuccess = false`,不会崩溃。
```

改为:

```markdown
**支持平台:** macOS(Accessibility API)、Windows(Win32 P/Invoke,无原生 DLL 依赖)。其他平台(Linux、非 OSX/WIN 编辑器)自动降级为无操作实现,`GetCurrentApp()` 返回 `IsSuccess = false`,不会崩溃。
```

- [ ] **Step 3: 在 README.md "原生插件说明" 章节之前插入 Windows 支持章节**

在第 `## 原生插件说明` 行之前插入:

```markdown
---

## Windows 支持

Windows 实现采用纯 C# P/Invoke,**无需任何原生 DLL**,直接调用系统 `user32`/`kernel32`/`shell32`/`gdi32`/`version` DLL。克隆即用,不依赖 MSVC 工具链。

### 行为差异(与 macOS 对比)

| 字段/API | macOS | Windows |
|---|---|---|
| `IsPermissionGranted` | 取决于 Accessibility 授权 | **恒为 `true`**(Win32 API 不需要特殊权限) |
| `RequestPermission()` | 触发系统授权弹窗 | **no-op**(无弹窗) |
| `AppName` | `NSRunningApplication.localizedName` | exe 的 `FileDescription`(版本资源),失败回退到文件名 |
| `BundleId` | 反向 DNS(如 `com.apple.finder`) | **exe 文件名小写**(如 `notepad`、`chrome`、`explorer`)|
| `WindowTitle` | `AXFocusedWindow.AXTitle` | `GetWindowTextW(GetForegroundWindow())` |
| `Icon` | `NSRunningApplication.icon` PNG | `SHGetFileInfoW` + `GetDIBits` 提取 32-bit BGRA |

### 已知限制(Windows)

- **受保护进程**:SYSTEM 级服务、某些 UAC 提升窗口会导致 `OpenProcess` 失败,返回 `NoFrontmostApp`。
- **图标位深**:仅处理 32-bit BGRA(Vista+ Shell 默认)。1/4/8-bit 老式图标将返回 `Icon = null`。
- **batchmode**:无前台窗口场景下 `GetForegroundWindow` 返回 0,自然降级为 `NoFrontmostApp`。

### 不需要的配置

与 macOS 不同,Windows **无需**以下任何内容:
- Entitlements 文件
- 权限描述字符串
- 预编译 `.bundle` / `.dll`
- 代码签名额外条目

---
```

- [ ] **Step 4: 更新 README.md "项目结构" 中的文件树**

找到项目结构代码块,把:

```text
│   ├── MacOSAppMonitor.cs           # macOS 实现(#if UNITY_STANDALONE_OSX)
│   └── UnsupportedAppMonitorImpl.cs # 非 macOS 无操作实现
```

改为:

```text
│   ├── MacOSAppMonitor.cs           # macOS 实现(#if UNITY_STANDALONE_OSX)
│   ├── WindowsAppMonitor.cs         # Windows 实现(#if UNITY_STANDALONE_WIN,纯 C# P/Invoke)
│   └── UnsupportedAppMonitorImpl.cs # 其他平台无操作实现
```

- [ ] **Step 5: 更新 README.md "已知限制" 章节**

找到:

```markdown
## 已知限制

- **仅支持 macOS**:`#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX` 条件编译,其他平台自动降级。
- **需要 Accessibility 权限**:首次运行时系统会弹窗请求授权,之后记住选择。用户拒绝后 `IsPermissionGranted = false`,`GetCurrentApp()` 返回 Fallback。
- **图标内存管理**:`AppInfo.Icon`(Texture2D)由调用方负责 `Object.Destroy()`,否则会造成显存泄漏。
- **窗口标题限制**:部分应用(如系统守护进程)没有可见窗口,`WindowTitle` 会返回空字符串。
```

改为:

```markdown
## 已知限制

- **支持的平台**:macOS(Standalone/Editor)、Windows(Standalone/Editor)。其他平台自动降级为无操作实现。
- **macOS — Accessibility 权限**:首次运行时系统会弹窗请求授权。用户拒绝后 `IsPermissionGranted = false`,`GetCurrentApp()` 返回 Fallback AppInfo。
- **Windows — 受保护进程**:SYSTEM 级服务或 UAC 提升窗口可能导致 `OpenProcess` 失败,返回 `NoFrontmostApp`。
- **图标内存管理**:`AppInfo.Icon`(Texture2D)由调用方负责 `Object.Destroy()`,否则会造成显存泄漏。
- **窗口标题限制**:部分应用(如系统守护进程、托盘图标)没有可见窗口,`WindowTitle` 会返回空字符串。
- **BundleId 跨平台差异**:macOS 是反向 DNS,Windows 是 exe 文件名小写——调用方若需唯一应用标识,应为两个平台分别处理。
```

- [ ] **Step 6: 提交**

```bash
git add localpackage/com.nz.appmonitor/package.json localpackage/com.nz.appmonitor/README.md
git commit -m "docs(appmonitor): document Windows support in README and bump version to 1.1.0"
```

---

## Task 8: 最终编译验证与交叉平台确认

**Files:** (无改动,仅验证)

**目标:** 证实 Windows 与 macOS 两个 target 都能干净编译,无回归。

- [ ] **Step 1: Windows target 最终确认**

```
mcp__UnityMCP__manage_editor(action="get_state")
```

确认 `activeBuildTarget` 是 `StandaloneWindows64`。然后:

```
mcp__UnityMCP__refresh_unity()
```

等 `isCompiling=false`:

```
mcp__UnityMCP__read_console(types=["Error", "Warning"], count=100)
```

**Expected:** 无 error。Warning 可忽略(若是本次改动引入的新 warning,应修正)。

- [ ] **Step 2: 切回 macOS target,验证无 mac 侧回归**

```
mcp__UnityMCP__manage_editor(action="set_build_target", build_target="StandaloneOSX")
mcp__UnityMCP__refresh_unity()
```

等 `isCompiling=false`:

```
mcp__UnityMCP__read_console(types=["Error"], count=50)
```

**Expected:** 无 error。

- [ ] **Step 3: 在 macOS Editor 运行既有 PlayMode 测试确认无回归**

```
mcp__UnityMCP__run_tests(testMode="PlayMode", testFilter="NZ.AppMonitor.Tests")
```

然后:

```
mcp__UnityMCP__get_test_job(jobId="<返回的 id>")
```

轮询直到 `status=completed`。

**Expected:** 所有 mac 分支测试通过(`_macOS_*`);非 macOS 降级测试(`_UnsupportedPlatform_*`)**不应运行**(因为 `UNITY_STANDALONE_OSX` 分支覆盖);Windows 专属测试(`_Windows_*`)也**不应运行**。通过总数应与 Task 0 基线一致或增加(不应减少)。

- [ ] **Step 4: 文档记录 Windows Player 手工验证步骤**

Windows Player 运行时行为无法在 Mac 开发机验证。记录清单供 Windows 操作员执行:

1. 在 Mac 上 Build Windows Player with Tests(菜单 **Build → Build macOS App** 或 Unity **Build Settings** 选 Windows + Development Build + Run Tests)。
2. 拷贝生成的 `Builds/Windows/` 目录到 Windows 物理机。
3. 在 Windows 上运行:
   ```cmd
   DevTemplate.exe -batchmode -testPlatform PlayMode -testResults TestResults.xml -testFilter "NZ.AppMonitor.Tests"
   ```
4. 检查 `TestResults.xml`,所有 `_Windows_*` 和平台无关测试应通过。
5. 可选:非 batchmode 运行,打开记事本作为前台应用,观察 Debug 输出的 `AppName='Notepad'`、`BundleId='notepad'`、图标是否正确显示。

此清单不需要当场执行,作为交付标准放入 PR 描述即可。

- [ ] **Step 5: 最终检查 & 总结提交**

```bash
git log --oneline -10
git status
```

**Expected:** 本次 feature 共 7 次提交(Task 1-7 各一次),工作区干净。

如需合并整个工作流为一条总结性的 squash,使用:

```bash
# 可选:若团队要求单 commit 合并
git log --oneline main..HEAD
# 手动决定是否 squash
```

默认保留 7 个原子提交,便于 review。

---

## Self-Review 追溯(写入后自查)

- **Spec §3.1 文件改动清单** → Task 1(skeleton+factory)、Task 5/6(tests)、Task 7(meta)。✓
- **Spec §3.2 核心流程** → Task 2 覆盖。✓
- **Spec §3.3 AppName FileDescription** → Task 3 覆盖。✓
- **Spec §3.4 BundleId 小写** → Task 2 中 `ToLowerInvariant()`,Task 6 断言。✓
- **Spec §3.5 图标管线** → Task 4 覆盖,含 `bmBitsPixel != 32` 放弃逻辑。✓
- **Spec §3.6 错误码映射** → Task 2 覆盖 `NoFrontmostApp` 两种触发路径,Task 4 图标失败不改 ErrorCode。✓
- **Spec §3.7 P/Invoke 签名** → Task 2/3/4 分别引入对应 DLL 导入。✓
- **Spec §4 测试改动** → Task 5(收窄)+ Task 6(新增)。✓
- **Spec §5 README 更新** → Task 7 覆盖 4 处:顶部介绍、新 Windows 章节、项目结构树、已知限制。✓
- **Spec §8 验收标准** → Task 8 覆盖编译与测试回归;运行时验证留为 Windows 侧手工清单(§8 承认此限制)。✓

类型一致性:`WindowsAppMonitorImpl`、`TryGetProcessExePath`、`TryGetFileDescription`、`TryExtractIcon` 这些方法名在 Task 2/3/4 中被先定义后引用,所有引用都与定义匹配。`AppMonitorResultCode.NoFrontmostApp` 来自既有 `AppMonitorData.cs`,无新增枚举值。
