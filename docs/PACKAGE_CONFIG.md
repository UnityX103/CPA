# macOS 应用监控包名配置规范

## 包标识

| 字段 | 值 |
|---|---|
| 包名（name） | `com.nz.appmonitor` |
| 显示名 | `NZ.AppMonitor` |
| 初始版本 | `1.0.0` |
| 最低 Unity 版本 | `6000.0` |
| 支持平台 | macOS（StandaloneOSX） |

---

## package.json 完整示例

将此文件放置于包根目录（例如 `localpackage/com.nz.appmonitor/package.json`）：

```json
{
  "name": "com.nz.appmonitor",
  "displayName": "NZ.AppMonitor",
  "version": "1.0.0",
  "unity": "6000.0",
  "description": "macOS 应用监控插件，通过无障碍 API 获取前台应用名称、窗口标题和图标。仅支持 macOS 平台。",
  "keywords": [
    "macos",
    "accessibility",
    "appmonitor",
    "native"
  ],
  "author": {
    "name": "NZ"
  },
  "dependencies": {},
  "repository": {
    "type": "git",
    "url": "https://github.com/your-org/nz-appmonitor.git"
  }
}
```

---

## 目录结构规范

作为 UPM 包分发时，目录结构如下：

```
com.nz.appmonitor/
├── package.json                          # 包描述文件（必须）
├── README.md                             # 简要说明
├── CHANGELOG.md                          # 版本变更记录
├── Runtime/
│   ├── MacOSAppMonitor.cs                # C# API 单例
│   ├── AppMonitorPanel.cs                # UI 面板（可选）
│   └── CPA.AppMonitor.Runtime.asmdef     # 程序集定义
├── Editor/
│   ├── BuildScript.cs                    # 构建菜单扩展
│   ├── MacOSBuildPostProcessor.cs        # 构建后处理
│   └── CPA.AppMonitor.Editor.asmdef      # 编辑器程序集定义
├── Plugins/
│   └── macOS/
│       └── AppMonitor/
│           ├── AppMonitor.m              # Objective-C 原生插件
│           ├── AppMonitor.entitlements   # Entitlements 配置
│           └── Info.plist                # Info.plist 合并片段
└── Tests/
    └── Runtime/
        └── Player/
            ├── MacOSAppMonitorPlayerTest.cs
            └── NZ.AppMonitor.Tests.asmdef
```

---

## 程序集定义文件

### Runtime 程序集（`CPA.AppMonitor.Runtime.asmdef`）

```json
{
    "name": "CPA.AppMonitor.Runtime",
    "rootNamespace": "CPA.Monitoring",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Editor 程序集（`CPA.AppMonitor.Editor.asmdef`）

```json
{
    "name": "CPA.AppMonitor.Editor",
    "rootNamespace": "CPA.Monitoring.Editor",
    "references": [
        "CPA.AppMonitor.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### 测试程序集（`NZ.AppMonitor.Tests.asmdef`）

```json
{
    "name": "NZ.AppMonitor.Tests",
    "rootNamespace": "NZ.AppMonitor.Tests",
    "references": [
        "CPA.AppMonitor.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

---

## 安装到主项目

### 方式一：本地路径引用（推荐开发阶段）

在主项目的 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.nz.appmonitor": "file:../../localpackage/com.nz.appmonitor",
    "com.nz.visualtest": "file:../../localpackage/com.nz.visualtest"
  }
}
```

### 方式二：Git URL 引用（推荐集成阶段）

```json
{
  "dependencies": {
    "com.nz.appmonitor": "https://github.com/your-org/nz-appmonitor.git#v1.0.0"
  }
}
```

### 方式三：复制到主项目 Assets

直接将包内容复制到 `Assets/Plugins/NZ/AppMonitor/`，适用于不使用 UPM 的项目。

---

## 版本号约定

遵循语义化版本（SemVer）：

| 版本 | 含义 |
|---|---|
| `1.0.0` | 初始稳定版本 |
| `1.0.x` | 仅 Bug 修复，无 API 变更 |
| `1.x.0` | 新增功能，向后兼容 |
| `x.0.0` | 破坏性变更（API 不兼容） |

---

## 依赖关系

`com.nz.appmonitor` 无外部 UPM 包依赖，仅依赖 Unity 内置模块：

- `com.unity.modules.unitywebrequest`（可选，图标网络加载）
- `com.unity.ugui` 或 UI Toolkit（仅 `AppMonitorPanel.cs` 需要）

若主项目使用 `com.nz.visualtest`，可结合测试框架验证集成效果。
