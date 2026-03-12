# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 回答语言

所有回答内容必须使用**中文**。

## 项目概述

Unity 6（6000.0.25f1）macOS 应用监控项目。核心功能：通过 Objective-C 原生插件调用 macOS 无障碍 API，获取前台应用名称、窗口标题、图标。

**产物：** `Builds/macOS/AppMonitor.app`（~99 MB，ad-hoc 签名，arm64+x86_64）

## 常用命令

### 构建

```bash
# 命令行构建（自动签名 + 验证）
./build_macos.sh

# 查看构建日志
cat build.log
```

Unity 编辑器菜单：**Build → Build and Package macOS App**

### 编译检查

Unity 在资源刷新时自动编译，无需手动命令。通过以下方式查看错误：
- Unity Console 窗口
- MCP 工具：`get_console_errors`
- MCP 工具：`script_validation`（级别：Basic / Standard / Comprehensive / Strict）

### 测试

```bash
# 构建含测试的 Player
# 菜单：Build → Build macOS Player with Tests

# 运行已构建的 Player 测试
./Builds/macOS/AppMonitor.app/Contents/MacOS/DevTemplate \
  -batchmode \
  -testPlatform PlayMode \
  -testResults ./TestResults/macOS_Player_Results.xml \
  -testFilter "NZ.VisualTest.Tests.Player.MacOSAppMonitorPlayerTest"
```

通过 MCP 运行单个测试：`run_tests`，参数 `testNames: ["Full.Test.Name"]`

## 架构

### 核心层级

```
原生层   Assets/Plugins/macOS/AppMonitor/AppMonitor.m   Objective-C，调用 NSWorkspace + AX API
API 层   Assets/Scripts/MacOSAppMonitor.cs               C# 单例，DllImport 桥接原生插件
UI 层    Assets/Scripts/AppMonitorPanel.cs               MonoBehaviour，UI Toolkit 渲染，含 LRU 图标缓存
构建层   Assets/Editor/BuildScript.cs                    编辑器菜单，自动签名、验证
```

### 关键设计点

- `MacOSAppMonitor`：`sealed` 单例，`#if UNITY_STANDALONE_OSX` 条件编译，提供 `GetCurrentApp()` / `GetAppIcon()`
- `AppMonitorPanel`：图标 LRU 缓存（上限 50），协程每秒刷新，权限拒绝时优雅降级
- 原生内存：`FreeIconData()` 须在 C# 侧手动调用，防止内存泄漏
- 权限：`NSAccessibilityUsageDescription`（窗口标题）+ `NSAppleEventsUsageDescription`（Apple Events），见 `Assets/Plugins/macOS/AppMonitor.entitlements`

### 本地包

| 包 | 路径 | 用途 |
|---|---|---|
| `com.unity.mcp` | `localpackage/com.unity.mcp/` | MCP for Unity，AI 工具集成 |
| `com.nz.visualtest` | `localpackage/com.nz.visualtest/` | 可视化测试框架，含录屏 |

### 程序集组织

```
Runtime 代码  → *.Runtime.asmdef
Editor 代码   → *.Editor.asmdef（含 #if UNITY_EDITOR 守卫）
测试代码      → Tests/Runtime/（PlayMode）或 Tests/Editor/（EditMode）
命名空间      → 与文件夹结构完全对应（如 CPA.Monitoring、NZ.VisualTest.Runtime）
```

## 代码风格

- **缩进**：4 个空格，Allman 大括号风格
- **命名**：类/方法/属性 → PascalCase；私有字段 → `_camelCase`；接口 → `IFoo`
- **修饰符**：所有成员显式声明访问级别；非继承类用 `sealed`；不可变字段用 `readonly`
- **注释**：中文；代码标识符：英文
- **错误处理**：外部操作用 try-catch + 日志；预期失败返回错误响应，不抛异常

## MCP 集成

项目内置 MCP for Unity，Claude Code 可直接操控编辑器：

| 工具 | 用途 |
|---|---|
| `run_tests` | 执行指定测试 |
| `get_test_job` | 查询测试执行状态 |
| `get_console_errors` | 读取 Unity 控制台错误 |
| `script_validation` | 静态代码分析 |

## 版本控制

每次开始新任务前，先提交当前未提交的改动（`git commit`）。
