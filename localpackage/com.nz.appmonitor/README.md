# com.nz.appmonitor — NZ App Monitor

macOS 与 Windows 的前台应用监控包。macOS 通过 **Accessibility API**，Windows 通过 **Win32 P/Invoke**（无需特殊权限），实时获取前台应用名称、窗口标题和应用图标。

---

## 功能

| API | 说明 |
|-----|------|
| `AppMonitor.Instance.GetCurrentApp()` | 返回 `AppInfo`，包含 AppName、WindowTitle、Icon（Texture2D）|
| `AppMonitor.Instance.GetAppIcon()` | 直接返回前台应用的 `Texture2D` 图标 |
| `AppMonitor.Instance.IsPermissionGranted` | 检查 Accessibility 权限是否已授予（不弹窗）|
| `AppMonitor.Instance.RequestPermission()` | 触发系统权限弹窗（仅在未授权时弹出）|

**支持平台:** macOS（Accessibility API）、Windows（Win32 P/Invoke，无原生 DLL 依赖）。其他平台（Linux、非 OSX/WIN 编辑器）自动降级为无操作实现，`GetCurrentApp()` 返回 `IsSuccess = false`，不会崩溃。

---

## 安装

### 1. 将包添加到 manifest.json

在 `Packages/manifest.json` 的 `dependencies` 中加入：

```json
{
  "dependencies": {
    "com.nz.appmonitor": "file:../localpackage/com.nz.appmonitor"
  }
}
```

如果需要运行包内的测试，同时在 `testables` 中加入：

```json
{
  "testables": [
    "com.nz.appmonitor"
  ]
}
```

### 2. 验证导入

打开 Unity，等待编译完成。
在 **Window → Package Manager** 中可以看到 **NZ App Monitor 1.1.0**。

---

## 权限配置（必须）

包内已附带权限模板文件，但必须将以下内容**合并**到项目的 Info.plist 和 Entitlements 中。

### Info.plist — 权限描述字符串

```xml
<!-- 辅助功能：用于获取前台应用名称和窗口标题 -->
<key>NSAccessibilityUsageDescription</key>
<string>本应用需要辅助功能权限，以便获取当前前台应用的名称和窗口标题。</string>

<!-- Apple Events：用于与前台应用通信 -->
<key>NSAppleEventsUsageDescription</key>
<string>本应用需要 Apple Events 权限，以便与前台应用进行通信。</string>
```

参考文件：`Plugins/macOS/Info.plist`

### Entitlements — Hardened Runtime 权限

```xml
<key>com.apple.security.cs.allow-jit</key>
<true/>
<key>com.apple.security.cs.allow-unsigned-executable-memory</key>
<true/>
<key>com.apple.security.cs.allow-dyld-environment-variables</key>
<true/>
<key>com.apple.security.cs.disable-library-validation</key>
<true/>
<key>com.apple.security.automation.apple-events</key>
<true/>
```

参考文件：`Plugins/macOS/AppMonitor.entitlements`

> **重要：** 若不正确配置 Entitlements，应用在 Hardened Runtime 模式下会被系统拒绝加载原生库。
> Unity 默认会在构建后处理阶段（`IPostprocessBuildWithReport`）注入这些权限，参考项目中的 `MacOSBuildPostProcessor.cs`。

---

## Windows 支持

Windows 实现采用纯 C# P/Invoke，**无需任何原生 DLL**，直接调用系统 `user32`/`kernel32`/`shell32`/`gdi32`/`version` DLL。克隆即用，不依赖 MSVC 工具链。

### 行为差异（与 macOS 对比）

| 字段/API | macOS | Windows |
|---|---|---|
| `IsPermissionGranted` | 取决于 Accessibility 授权 | **恒为 `true`**（Win32 API 不需要特殊权限） |
| `RequestPermission()` | 触发系统授权弹窗 | **no-op**（无弹窗） |
| `AppName` | `NSRunningApplication.localizedName` | exe 的 `FileDescription`（版本资源），失败回退到文件名 |
| `BundleId` | 反向 DNS（如 `com.apple.finder`） | **exe 文件名小写**（如 `notepad`、`chrome`、`explorer`）|
| `WindowTitle` | `AXFocusedWindow.AXTitle` | `GetWindowTextW(GetForegroundWindow())` |
| `Icon` | `NSRunningApplication.icon` PNG | `SHGetFileInfoW` + `GetDIBits` 提取 32-bit BGRA |

### 已知限制（Windows）

- **受保护进程**：SYSTEM 级服务、某些 UAC 提升窗口会导致 `OpenProcess` 失败，返回 `NoFrontmostApp`。
- **图标位深**：仅处理 32-bit BGRA（Vista+ Shell 默认）。1/4/8-bit 老式图标将返回 `Icon = null`。
- **batchmode**：无前台窗口场景下 `GetForegroundWindow` 返回 0，自然降级为 `NoFrontmostApp`。

### 不需要的配置

与 macOS 不同，Windows **无需**以下任何内容：
- Entitlements 文件
- 权限描述字符串
- 预编译 `.bundle` / `.dll`
- 代码签名额外条目

---

## 原生插件说明

包内提供预编译的 Universal Binary（`Plugins/macOS/AppMonitor.bundle`），同时支持 **arm64** 和 **x86_64**。

如需从源码重新编译：

```bash
cd Plugins/macOS/AppMonitor
chmod +x build_appmonitor.sh
./build_appmonitor.sh
```

### 编译依赖

- Xcode Command Line Tools（`xcode-select --install`）
- macOS SDK 13.0+
- 框架：`Foundation`、`AppKit`、`ApplicationServices`

### Plugin Import 设置

`AppMonitor.bundle` 需在 Unity Inspector 中配置如下（通常 Unity 会自动识别 `.bundle` 文件）：

| 设置 | 值 |
|------|----|
| Platform | macOS |
| CPU | Any CPU |
| Load Type | Default |

若导入后插件未生效，在 Project 窗口选中 `AppMonitor.bundle`，在 Inspector 中确认 **macOS** 勾选已启用，然后点击 **Apply**。

---

## 基本使用

```csharp
using CPA.Monitoring;
using UnityEngine;

public class MyMonitor : MonoBehaviour
{
    private void Start()
    {
        // 启动时请求权限（仅首次会弹窗）
        AppMonitor.Instance.RequestPermission();
    }

    private void Update()
    {
        AppInfo info = AppMonitor.Instance.GetCurrentApp();

        if (info.IsSuccess)
        {
            Debug.Log($"前台应用: {info.AppName}");
            Debug.Log($"窗口标题: {info.WindowTitle}");

            if (info.Icon != null)
            {
                // info.Icon 是 Texture2D，用完需手动 Destroy 防止内存泄漏
            }
        }
        else
        {
            Debug.LogWarning($"获取失败: {info.ErrorMessage}（ErrorCode: {info.ErrorCode}）");
        }
    }
}
```

### AppInfo 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `IsSuccess` | `bool` | 是否成功获取（权限被拒时也可能为 true，见 ErrorCode）|
| `AppName` | `string` | 前台应用名称 |
| `WindowTitle` | `string` | 当前窗口标题（某些应用无窗口时为空字符串）|
| `Icon` | `Texture2D` | 应用图标，**调用方负责 Destroy** |
| `ErrorCode` | `AppMonitorResultCode?` | 错误码，成功且无降级时为 null |
| `ErrorMessage` | `string` | 错误描述 |

### AppMonitorResultCode 枚举

| 值 | 说明 |
|----|------|
| `Success (0)` | 成功 |
| `InvalidArgument (-1)` | 参数非法（缓冲区长度不足等）|
| `AccessibilityDenied (-2)` | Accessibility 权限未授予 |
| `NoFrontmostApp (-3)` | 未找到前台应用 |
| `IconAllocationFailed (-4)` | 图标内存分配失败 |

### 权限被拒时的 Fallback 行为

当 `AccessibilityDenied` 时，`GetCurrentApp()` **不会返回失败**，而是返回一个 Fallback `AppInfo`：

- `IsSuccess = true`
- `AppName = <当前进程名>`
- `ErrorCode = AccessibilityDenied`
- `Icon = 32x32 蓝色占位图`

这允许 UI 在权限不足时仍能正常显示。

---

## 运行测试

### 在编辑器中运行（PlayMode）

打开 **Window → General → Test Runner**，切换到 **PlayMode** 标签，找到 `NZ.AppMonitor.Tests` 并点击 **Run All**。

### 构建 Player 测试并运行

```bash
# 1. 在 Unity 编辑器菜单构建含测试的 Player
#    Build → Build macOS Player with Tests

# 2. 运行测试
./Builds/macOS/AppMonitor.app/Contents/MacOS/DevTemplate \
  -batchmode \
  -testPlatform PlayMode \
  -testResults ./TestResults/AppMonitor_Player_Results.xml \
  -testFilter "NZ.AppMonitor.Tests"

# 3. 查看结果
cat ./TestResults/AppMonitor_Player_Results.xml
```

### 测试覆盖范围

| 测试 | 覆盖内容 |
|------|----------|
| `Instance_IsNotNull` | 单例不为 null |
| `Instance_IsSameObject` | 单例同一性 |
| `IsPermissionGranted_DoesNotThrow` | 权限查询不抛异常 |
| `RequestPermission_DoesNotThrow` | 权限请求不抛异常 |
| `GetCurrentApp_ReturnsNonNull` | 返回值不为 null |
| `GetCurrentApp_AppInfo_*` | AppInfo 字段一致性 |
| `GetCurrentApp_macOS_*` | macOS 上有效 AppName/WindowTitle |
| `GetCurrentApp_Windows_*` | Windows 上 AppName/BundleId/ErrorMessage 断言 |
| `GetCurrentApp_UnsupportedPlatform_*` | 非支持平台降级行为 |
| `GetAppIcon_*` | 图标返回契约(macOS/Windows 有值或 null,不支持平台 null) |
| `AppMonitorResultCode_Values_AreCorrect` | 枚举值正确性 |
| `PermissionDeniedException_*` | 异常类构造 |
| `AppInfo_DefaultConstruct_*` | 数据类默认值 |
| `GetCurrentApp_MultipleCalls_Stable` | 多次调用稳定性（5 次）|
| `GetCurrentApp_MemorySmoke_NoLeak` | 10 次调用内存烟雾测试（<5MB 增长）|

---

## 项目结构

```
com.nz.appmonitor/
├── package.json                     # 包元数据
├── README.md                        # 本文档
├── Runtime/
│   ├── NZ.AppMonitor.Runtime.asmdef # 程序集定义（autoReferenced=true）
│   ├── AppMonitor.cs                # 公共单例入口
│   ├── IAppMonitor.cs               # 平台无关接口
│   ├── AppMonitorData.cs            # AppInfo、AppMonitorResultCode、PermissionDeniedException
│   ├── MacOSAppMonitor.cs           # macOS 实现（#if UNITY_STANDALONE_OSX）
│   ├── WindowsAppMonitor.cs         # Windows 实现（#if UNITY_STANDALONE_WIN，纯 C# P/Invoke）
│   └── UnsupportedAppMonitorImpl.cs # 其他平台无操作实现
├── Plugins/
│   └── macOS/
│       ├── AppMonitor.bundle        # 预编译 Universal Binary（arm64 + x86_64）
│       ├── AppMonitor.entitlements  # Hardened Runtime 权限模板
│       ├── Info.plist               # 权限描述字符串模板
│       └── AppMonitor/
│           ├── AppMonitor.m         # Objective-C 原生实现源码
│           └── build_appmonitor.sh  # 原生编译脚本
└── Tests/
    └── Runtime/
        ├── NZ.AppMonitor.Tests.asmdef  # 测试程序集（支持 Player 构建）
        └── AppMonitorPlayerTest.cs     # 集成测试套件(PlayMode + Player,含 macOS/Windows/无支持平台分支)
```

---

## 已知限制

- **支持的平台**：macOS（Standalone/Editor）、Windows（Standalone/Editor）。其他平台自动降级为无操作实现。
- **macOS — Accessibility 权限**：首次运行时系统会弹窗请求授权。用户拒绝后 `IsPermissionGranted = false`，`GetCurrentApp()` 返回 Fallback AppInfo。
- **Windows — 受保护进程**：SYSTEM 级服务或 UAC 提升窗口可能导致 `OpenProcess` 失败，返回 `NoFrontmostApp`。
- **图标内存管理**：`AppInfo.Icon`（Texture2D）由调用方负责 `Object.Destroy()`，否则会造成显存泄漏。
- **窗口标题限制**：部分应用（如系统守护进程、托盘图标）没有可见窗口，`WindowTitle` 会返回空字符串。
- **BundleId 跨平台差异**：macOS 是反向 DNS，Windows 是 exe 文件名小写——调用方若需唯一应用标识，应为两个平台分别处理。
