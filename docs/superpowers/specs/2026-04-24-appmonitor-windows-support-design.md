# com.nz.appmonitor — Windows 支持

**日期**: 2026-04-24
**目标包**: `localpackage/com.nz.appmonitor`
**范围**: 为现有 macOS-only 的 AppMonitor 包补充 Windows 平台实现,对外 API 契约与调用方代码不变。

---

## 1. 背景

当前 `com.nz.appmonitor` 仅通过 `MacOSAppMonitorImpl`(P/Invoke 到 Obj-C `AppMonitor.bundle`)支持 macOS。非 macOS 平台统一降级为 `UnsupportedAppMonitorImpl`(返回 `IsSuccess=false, ErrorMessage="当前平台不支持"`)。

现需让 Windows Standalone/Editor 同样能返回真实的前台应用信息,满足桌面宠物在 Windows 上的 `ActiveAppSystem` 监控需求。

## 2. 设计约束

- 对外接口 `IAppMonitor` 与数据类型 `AppInfo`/`AppMonitorResultCode`/`PermissionDeniedException` **完全不变**。
- 现有调用方(`Assets/Scripts/APP/Network/System/ActiveAppSystem.cs`)零改动。
- Windows 实现**不引入原生 DLL 依赖**——纯 C# P/Invoke 调用系统 `user32`/`kernel32`/`shell32`/`gdi32`,避免 MSVC 工具链与 `.dll` 制品入库。
- macOS 分支代码与行为保持原样。

## 3. 实现方案

### 3.1 文件改动清单

**新增**
- `Runtime/WindowsAppMonitor.cs` — `WindowsAppMonitorImpl` 实现,`#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN` 守卫。
- `Runtime/WindowsAppMonitor.cs.meta` — Unity 自动生成,不手写 GUID。

**修改**
- `Runtime/AppMonitor.cs` — 工厂分支追加 `#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN` → `new WindowsAppMonitorImpl()`。
- `Tests/Runtime/AppMonitorPlayerTest.cs` — 非 macOS 降级断言条件收窄为 `!UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN`,新增 `#if UNITY_STANDALONE_WIN` 正向断言块。
- `package.json` — keywords 加 `windows`,description 去掉"仅 macOS"措辞。
- `README.md` — 功能表新增 Windows 行、新增"Windows 支持"章节、更新"已知限制"。

**不动**
- `Runtime/IAppMonitor.cs`、`Runtime/AppMonitorData.cs`、`Runtime/MacOSAppMonitor.cs`、`Runtime/UnsupportedAppMonitorImpl.cs`
- `Runtime/NZ.AppMonitor.Runtime.asmdef`(`includePlatforms=[]` 已覆盖所有平台)
- `Plugins/macOS/*`
- 上层调用方代码

### 3.2 `WindowsAppMonitorImpl` 核心流程

```text
GetCurrentApp():
  hwnd = GetForegroundWindow()
  若 hwnd == IntPtr.Zero → AppInfo{ IsSuccess=false, ErrorCode=NoFrontmostApp, ErrorMessage="未找到前台应用" }

  title = GetWindowTextW(hwnd)             // 允许为空字符串(某些无标题窗口)
  GetWindowThreadProcessId(hwnd, out pid)
  hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, pid)
  若 hProc == IntPtr.Zero → AppInfo{ IsSuccess=false, ErrorCode=NoFrontmostApp, ErrorMessage="无法打开前台进程(可能受保护/Elevated)" }

  exePath = QueryFullProcessImageNameW(hProc)
  CloseHandle(hProc)

  appName  = TryGetFileDescription(exePath) ?? Path.GetFileNameWithoutExtension(exePath)
  bundleId = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant()
  icon     = TryExtractIconAsTexture2D(exePath)   // 失败返回 null,不阻塞主流程

  return AppInfo{ IsSuccess=true, AppName, BundleId, WindowTitle=title, Icon=icon, ErrorCode=null, ErrorMessage=null }

IsPermissionGranted => true   // Windows 无 Accessibility 权限等价概念
RequestPermission()  => { }   // no-op
GetAppIcon()         => GetCurrentApp()?.Icon
```

### 3.3 AppName 解析策略

- 优先 `GetFileVersionInfoW` → `VerQueryValueW(\StringFileInfo\{langId}\FileDescription)`(例如 Chrome 的 exe 叫 `chrome.exe` 但 FileDescription 是 `Google Chrome`),更贴近 macOS `localizedName` 的显示名语义。
- 任一失败回退到 `Path.GetFileNameWithoutExtension(exePath)`(例如 `chrome`)。
- 两条路径都失败则返回空字符串——测试断言 `AppName` 非 null 但不强制非空。

### 3.4 BundleId 映射

- 取值:`Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant()`,例如 `notepad`、`chrome`、`explorer`。
- 与 macOS 反向 DNS 风格(`com.apple.finder`)不一致,属于平台差异,调用方需知晓("BundleId 在 Windows 上是 exe 文件名小写")。
- README 新增一节说明此差异。

### 3.5 图标提取管线

```text
SHFILEINFOW sfi;
SHGetFileInfoW(exePath, 0, ref sfi, sizeof(SHFILEINFOW), SHGFI_ICON | SHGFI_LARGEICON)
hIcon = sfi.hIcon
若 hIcon == 0 → return null

ICONINFO info; GetIconInfo(hIcon, out info)
BITMAP bm;     GetObject(info.hbmColor, sizeof(BITMAP), out bm)
w = bm.bmWidth; h = bm.bmHeight

BITMAPINFO bmi;
bmi.biSize=40; biWidth=w; biHeight=-h (自顶向下); biPlanes=1; biBitCount=32; biCompression=BI_RGB
byte[] buf = new byte[w * h * 4]
hdc = GetDC(IntPtr.Zero)
GetDIBits(hdc, info.hbmColor, 0, (uint)h, buf, ref bmi, DIB_RGB_COLORS)
ReleaseDC(IntPtr.Zero, hdc)

// BGRA → RGBA 通道互换(B<->R)
// 自顶向下 → Unity 原点左下:按行翻转
byte[] rgba = FlipRowsAndSwapBR(buf, w, h)

Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain:false) { name = "AppIcon" }
tex.LoadRawTextureData(rgba); tex.Apply()

DeleteObject(info.hbmColor); DeleteObject(info.hbmMask); DestroyIcon(hIcon)
return tex
```

- 所有 GDI/USER 句柄在 `finally` 内释放,失败路径全部释放已获句柄。
- 提取失败(任何一步)→ 返回 null,不抛异常,不回退到蓝色占位图(与 macOS 的 `AccessibilityDenied` fallback 场景不同:Windows 没有权限被拒分支,null icon 就是最自然的失败表现)。
- **位深假设**:现代 Windows(Vista+)的 Shell 大图标均为 32-bit BGRA premultiplied alpha。若 `GetObject` 返回的 `BITMAP.bmBitsPixel != 32`,直接放弃该图标、返回 null(不处理 1/4/8-bit 的 mask 合成——属 YAGNI 场景,极少在 Unity 支持的 Windows 10+ 环境中遇到)。
- **Alpha 策略**:直接使用 DIB 返回的 BGRA 数据(premultiplied)。Unity UI Toolkit / Sprite 渲染对 premultiplied RGBA 兼容,无需二次处理。

### 3.6 错误码映射

| 场景 | `AppMonitorResultCode` | `IsSuccess` |
|---|---|---|
| `GetForegroundWindow() == 0` | `NoFrontmostApp` | false |
| `OpenProcess` 返回 0(Elevated/受保护进程) | `NoFrontmostApp` | false |
| 图标任一步失败 | 不改 ErrorCode,照常成功 | true,`Icon=null` |
| 整个 `GetCurrentApp` 抛出异常(P/Invoke 异常) | 不设 `ErrorCode`,`ErrorMessage="原生调用异常: ..."` | false |
| 正常路径 | `null` | true |

**不使用** `AccessibilityDenied`——Windows 上无对应语义。

### 3.7 P/Invoke 签名清单(预览)

```csharp
// user32
[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowTextW(IntPtr hWnd, StringBuilder buf, int max);
[DllImport("user32.dll")] static extern int GetWindowTextLengthW(IntPtr hWnd);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
[DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
[DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
[DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
[DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

// kernel32
[DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
[DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool QueryFullProcessImageNameW(IntPtr h, uint flags, StringBuilder buf, ref int size);
[DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

// shell32
[DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    static extern IntPtr SHGetFileInfoW(string path, uint attrs, ref SHFILEINFOW info, uint size, uint flags);

// gdi32
[DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, out BITMAP bm);
[DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFO bmi, uint usage);
[DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);

// version (AppName 优化)
[DllImport("version.dll", CharSet=CharSet.Unicode)] static extern int GetFileVersionInfoSizeW(string path, out int handle);
[DllImport("version.dll", CharSet=CharSet.Unicode)] static extern bool GetFileVersionInfoW(string path, int handle, int len, byte[] data);
[DllImport("version.dll", CharSet=CharSet.Unicode)] static extern bool VerQueryValueW(byte[] data, string subBlock, out IntPtr buf, out uint len);
```

常量:`PROCESS_QUERY_LIMITED_INFORMATION = 0x1000`、`SHGFI_ICON = 0x100`、`SHGFI_LARGEICON = 0x0`(默认,为可读性仍 OR 进去)、`BI_RGB = 0`、`DIB_RGB_COLORS = 0`、`LANG_EN_US_CP1200 = "040904B0"`(VerQueryValue 常用 FileDescription 子块,失败则枚举 `\VarFileInfo\Translation` 第一项)。

## 4. 测试改动

```diff
// AppMonitorPlayerTest.cs

- #if !UNITY_STANDALONE_OSX
+ #if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN
  public void IsPermissionGranted_NonMacOS_ReturnsFalse()    // 改名:_UnsupportedPlatform_ReturnsFalse
  public void GetCurrentApp_NonMacOS_ReturnsPlatformUnsupported()  // 改名:_UnsupportedPlatform_
  public void GetAppIcon_NonMacOS_ReturnsNull()              // 改名:_UnsupportedPlatform_
  #endif
```

**新增 Windows 正向测试**(`#if UNITY_STANDALONE_WIN`):

- `IsPermissionGranted_Windows_ReturnsTrue` — `IsPermissionGranted == true`
- `GetCurrentApp_Windows_ReturnsAppName` — `result.IsSuccess==true`(一般情况),或 `NoFrontmostApp`(batchmode 无前台窗口);若成功则 `AppName` 非 null
- `GetCurrentApp_Windows_BundleId_IsLower` — 成功时 `BundleId == BundleId.ToLowerInvariant()`
- `GetAppIcon_Windows_ReturnsTextureOrNull` — 调用不抛异常,返回值可为 null(受保护进程/batchmode)

现有平台无关测试(`Instance_IsNotNull`、`GetCurrentApp_ReturnsNonNull`、`AppMonitorResultCode_Values_AreCorrect`、`AppInfo_DefaultConstruct_*`、`GetCurrentApp_MultipleCalls_Stable`、`GetCurrentApp_MemorySmoke_NoLeak` 等)照常在 Windows Player 上执行,自然覆盖 Windows 路径。

## 5. README 更新要点

- 顶部介绍从"macOS 前台应用监控"改为"macOS/Windows 前台应用监控"。
- 新增 `## Windows 支持` 一节,说明:
  - 无权限要求,`IsPermissionGranted` 恒为 `true`
  - `AppName` 优先 exe `FileDescription`,回退文件名
  - `BundleId = exe文件名小写`,与 macOS 反向 DNS 风格不同
  - 受保护进程(SYSTEM 级服务、某些 UAC 提升窗口)可能返回 `NoFrontmostApp`
  - 纯 C# P/Invoke,无需额外原生 DLL
- 更新项目结构树,加 `WindowsAppMonitor.cs` 行。
- "已知限制"节删除"仅支持 macOS"条,新增受保护进程拿不到信息的说明。

## 6. 不做的事(YAGNI)

- 不做 AppUserModelID / UWP Store 应用特殊处理(桌面 Unity 场景几乎都是 Win32 进程)
- 不做 Windows 独立原生 DLL,不引入任何构建脚本
- 不做 Windows 版 Info.plist/Entitlements 等价物(Windows 无此要求)
- 不改 `IAppMonitor` 接口、不改 `AppInfo` 字段结构
- 不改任何调用方代码
- 不做多监视器/多窗口的特殊处理(`GetForegroundWindow` 语义足够)

## 7. 风险与缓解

| 风险 | 缓解 |
|---|---|
| P/Invoke 在 Unity IL2CPP 下的 marshaling 差异 | 所有字符串使用 `CharSet.Unicode` + `StringBuilder`/`string`,与 Unity 常规 P/Invoke 一致 |
| `QueryFullProcessImageNameW` 对某些进程返回失败 | 捕获 `bool` 返回值,失败即降级为 `NoFrontmostApp` |
| GDI 句柄泄漏 | 所有句柄在 `try/finally` 中释放;`Texture2D` 由调用方 Destroy(与 macOS 契约一致) |
| `GetFileVersionInfo` 对无版本资源的 exe 失败 | 回退到文件名,不抛 |
| batchmode 下 `GetForegroundWindow==0` | 测试断言允许 `NoFrontmostApp`,不强制成功 |
| Unity Editor on Windows(`UNITY_EDITOR_WIN`)调用时 FG 窗口是编辑器自身 | 预期行为,测试允许 AppName=`Unity` 或任意值,只断言格式契约 |

## 8. 验收标准

- Windows Player/Editor 下 `AppMonitor.Instance.GetCurrentApp()` 返回 `IsSuccess=true` 且 `AppName` 非空字符串(前台有可见应用时)
- `BundleId` 为 exe 小写文件名
- 大多数 GUI 应用能拿到图标(`Icon != null`)
- 所有既有平台无关测试在 Windows 上通过
- macOS Player 下所有既有测试保持通过(无回归)
- 编译无警告(除 `#if` 分支自然的未引用 symbol 警告)
