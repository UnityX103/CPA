# macOS 应用自动打包指南

## 快速开始

### 一键打包

```bash
./build_macos.sh
```

这个脚本会自动完成：
1. ✅ 清理旧的构建
2. ✅ 使用 Unity 构建 macOS 应用
3. ✅ 自动签名应用（应用 Entitlements）
4. ✅ 验证签名和权限配置
5. ✅ 生成构建报告

### 构建输出

- **应用路径**: `Builds/macOS/DevTemplate.app`
- **构建日志**: `build.log`
- **应用大小**: 约 99 MB

## 权限配置

应用已自动配置以下权限：

### Entitlements（已签名）
- ✅ `com.apple.security.automation.apple-events` - Apple Events 权限
- ✅ `com.apple.security.cs.allow-jit` - JIT 编译
- ✅ `com.apple.security.cs.allow-unsigned-executable-memory` - 动态内存
- ✅ `com.apple.security.cs.allow-dyld-environment-variables` - 动态库环境变量
- ✅ `com.apple.security.cs.disable-library-validation` - 禁用库验证

### Info.plist 权限描述
- ✅ `NSAccessibilityUsageDescription` - 辅助功能权限说明
- ✅ `NSAppleEventsUsageDescription` - Apple Events 权限说明

## 首次运行

1. 运行应用：
   ```bash
   open Builds/macOS/DevTemplate.app
   ```

2. 授予辅助功能权限：
   - 前往 **系统设置 > 隐私与安全 > 辅助功能**
   - 将 **DevTemplate** 添加到允许列表
   - 重启应用

## API 使用示例

```csharp
using CPA.Monitoring;

// 获取当前聚焦的应用信息
AppInfo info = MacOSAppMonitor.Instance.GetCurrentApp();

if (info.IsSuccess)
{
    Debug.Log($"应用名称: {info.AppName}");
    Debug.Log($"窗口标题: {info.WindowTitle}");
    Debug.Log($"图标: {info.Icon.width}x{info.Icon.height}");
}
else
{
    Debug.LogWarning($"获取失败: {info.ErrorMessage}");
}

// 仅获取图标
Texture2D icon = MacOSAppMonitor.Instance.GetAppIcon();
```

## 错误处理

### 权限被拒绝
如果用户未授予辅助功能权限，API 会返回：
- `IsSuccess = true`（优雅降级）
- `ErrorCode = AccessibilityDenied`
- `AppName` = 当前进程名称（回退值）
- `Icon` = 蓝色占位图标

### 常见问题

**Q: 构建失败，提示 "Another Unity instance is running"**  
A: 关闭 Unity Editor 后重试：
```bash
pkill -f "Unity.app"
./build_macos.sh
```

**Q: 应用无法获取其他应用信息**  
A: 确保已在系统设置中授予辅助功能权限

**Q: 如何重新签名应用？**  
A: 重新运行构建脚本，或手动签名：
```bash
codesign --force --deep --sign - \
  --entitlements "Builds/macOS/DevTemplate.app/Contents/DevTemplate.entitlements" \
  "Builds/macOS/DevTemplate.app"
```

## 在 Unity Editor 中构建

也可以通过 Unity Editor 菜单构建：

1. 打开 Unity Editor
2. 选择 **Build > Build and Package macOS App**
3. 等待构建完成

## 技术细节

### 构建配置
- **目标平台**: macOS Standalone
- **架构**: Universal (x86_64 + arm64)
- **Unity 版本**: 6000.0.25f1
- **签名类型**: Ad-hoc（开发签名）

### 文件结构
```
Builds/macOS/DevTemplate.app/
├── Contents/
│   ├── Info.plist              # 权限描述
│   ├── DevTemplate.entitlements # Entitlements 配置
│   ├── MacOS/
│   │   └── DevTemplate         # 可执行文件（已签名）
│   ├── Resources/
│   │   └── Data/
│   │       └── Managed/
│   │           ├── AppMonitor.dll
│   │           └── ...
│   └── Plugins/
│       └── AppMonitor.bundle   # 原生插件
```

### 原生插件
- **位置**: `Assets/Plugins/macOS/AppMonitor/AppMonitor.m`
- **编译**: `build_appmonitor.sh`
- **输出**: `AppMonitor.bundle`
- **API**: `GetFrontmostAppInfo()`, `FreeIconData()`

## 发布到 App Store（可选）

如果需要上架 App Store：

1. 使用 Apple Developer 证书签名：
   ```bash
   codesign --force --deep \
     --sign "Developer ID Application: Your Name" \
     --entitlements "..." \
     "DevTemplate.app"
   ```

2. 启用沙盒（修改 Entitlements）：
   ```xml
   <key>com.apple.security.app-sandbox</key>
   <true/>
   ```

3. 注意：沙盒会限制某些功能，需要评估是否适合

## 许可证

本项目使用的权限和配置仅用于开发和测试目的。
