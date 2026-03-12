# macOS 应用监控功能使用规范

## 功能描述

`MacOSAppMonitor` 通过 macOS 无障碍 API（Accessibility API）获取当前前台应用信息，包括：

- **应用名称**（AppName）：前台运行应用的名称
- **窗口标题**（WindowTitle）：当前活动窗口的标题（需要 Accessibility 权限）
- **应用图标**（Icon）：前台应用的图标，以 `Texture2D` 形式返回

### 平台限制

| 平台 | 支持状态 | 说明 |
|---|---|---|
| macOS Player | ✓ 完整支持 | 原生插件全功能运行 |
| macOS Editor | ✓ 支持 | 通过 `AppMonitor.dylib` 调用 |
| Windows / Linux | ✗ 不支持 | 返回错误响应，不抛异常 |
| iOS / Android | ✗ 不支持 | 返回错误响应，不抛异常 |

---

## 快速开始

### 1. 安装依赖

将以下文件复制到目标 Unity 项目：

```
Assets/
├── Plugins/
│   └── macOS/
│       └── AppMonitor/
│           └── AppMonitor.m          # Objective-C 原生插件（自动编译为 .dylib）
├── Scripts/
│   ├── MacOSAppMonitor.cs            # C# API 层（单例）
│   └── AppMonitorPanel.cs            # （可选）UI 面板
```

### 2. 配置权限

在 `Assets/Plugins/macOS/Info.plist` 中配置使用描述（Unity 构建时自动合并）：

```xml
<key>NSAccessibilityUsageDescription</key>
<string>此应用需要辅助功能权限来检测当前活动窗口。</string>

<key>NSAppleEventsUsageDescription</key>
<string>此应用需要控制其他应用以执行自动化任务。</string>
```

### 3. 基本调用

```csharp
using CPA.Monitoring;

// 获取当前前台应用信息
AppInfo info = MacOSAppMonitor.Instance.GetCurrentApp();

if (info.IsSuccess)
{
    Debug.Log($"应用名: {info.AppName}");
    Debug.Log($"窗口标题: {info.WindowTitle}");

    if (info.Icon != null)
    {
        // 将图标赋给 UI Image 组件
        rawImage.texture = info.Icon;
    }
}
else
{
    Debug.LogWarning($"获取失败: {info.ErrorMessage}");
}
```

---

## API 参考

### `MacOSAppMonitor`

线程安全的懒加载单例。

```csharp
public sealed class MacOSAppMonitor
{
    public static MacOSAppMonitor Instance { get; }

    // 获取当前前台应用的完整信息（名称、窗口标题、图标）
    public AppInfo GetCurrentApp();

    // 仅获取前台应用图标（快捷方法）
    public Texture2D GetAppIcon();
}
```

**重要**：`GetCurrentApp()` 返回的 `AppInfo.Icon` 是 `Texture2D` 对象，调用方负责在不再需要时调用 `Object.Destroy(info.Icon)` 释放内存。

---

### `AppInfo`

```csharp
public class AppInfo
{
    // 前台应用名称（始终有值；权限被拒绝时为进程名回退值）
    public string AppName;

    // 当前活动窗口标题（需要 Accessibility 权限；无权限时为空字符串）
    public string WindowTitle;

    // 前台应用图标（Texture2D，需调用方手动 Destroy）
    public Texture2D Icon;

    // 调用是否成功（包含回退模式，权限拒绝时也可为 true）
    public bool IsSuccess;

    // 错误码（仅在非 Success 状态或回退模式下有值）
    public AppMonitorResultCode? ErrorCode;

    // 人类可读的错误描述
    public string ErrorMessage;
}
```

---

### `AppMonitorResultCode`

```csharp
public enum AppMonitorResultCode
{
    Success = 0,               // 成功
    InvalidArgument = -1,      // 参数非法
    AccessibilityDenied = -2,  // Accessibility 权限未授予（触发回退模式）
    NoFrontmostApp = -3,       // 未找到前台应用
    IconAllocationFailed = -4  // 图标内存分配失败
}
```

---

## 权限处理与回退机制

当 Accessibility 权限被拒绝时，`MacOSAppMonitor` 进入**回退模式**，而非直接失败：

| 字段 | 回退值 |
|---|---|
| `IsSuccess` | `true` |
| `AppName` | 当前进程名（`Process.GetCurrentProcess().ProcessName`） |
| `WindowTitle` | 空字符串 |
| `Icon` | 32×32 蓝色纯色占位图标 |
| `ErrorCode` | `AppMonitorResultCode.AccessibilityDenied` |

**检测是否处于回退模式：**

```csharp
AppInfo info = MacOSAppMonitor.Instance.GetCurrentApp();

if (info.ErrorCode == AppMonitorResultCode.AccessibilityDenied)
{
    // 引导用户授权：系统设置 > 隐私与安全性 > 辅助功能
    ShowPermissionGuide();
}
```

---

## 内存管理

原生层（Objective-C）分配的图标内存由 C# 层自动释放（通过 `FreeIconData()`）。
但 `Texture2D` 对象本身需要调用方管理：

```csharp
// 推荐：使用后立即销毁
AppInfo info = MacOSAppMonitor.Instance.GetCurrentApp();
// ... 使用 info.Icon ...
if (info.Icon != null)
{
    Object.Destroy(info.Icon);
}

// 推荐：长期缓存时使用 LRU 缓存（参考 AppMonitorPanel.cs 的实现）
```

---

## 使用 AppMonitorPanel（可选 UI 组件）

`AppMonitorPanel` 是基于 UI Toolkit 的可视化面板，提供：

- 每秒自动刷新前台应用信息
- 图标 LRU 缓存（上限 50 个）
- 权限拒绝时显示引导提示

```csharp
// 将 AppMonitorPanel.cs 挂载到场景中的 GameObject 即可自动运行
// 无需手动调用，MonoBehaviour 的 Start() 会自动初始化
```

---

## 首次运行授权步骤

1. 构建并运行 `AppMonitor.app`
2. 首次启动时，macOS 会弹出权限请求对话框
3. 如果跳过，前往：**系统设置 > 隐私与安全性 > 辅助功能**
4. 找到 `AppMonitor`，开启开关
5. 重启应用即可获得完整功能
