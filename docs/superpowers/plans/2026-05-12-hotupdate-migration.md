# 2026-05-12 业务代码迁入 App.Hotfix 迁移计划

## 背景

`feat(hotupdate)` 系列 commit 引入了 HybridCLR + Addressables 热更新基础设施：
- AOT 入口程序集：`App.Bootstrap`（`Assets/HotUpdate/Bootstrap/`）
- 热更新程序集：`App.Hotfix`（`Assets/HotUpdate/Hotfix/`）
- 加载链路：`BootstrapEntry` MonoBehaviour → `LoadHotfixSystem.RunAsync` → Addressables 拉 `App.Hotfix.dll` → 反射调 `App.Hotfix.HotfixEntry.Start()`

当前 `App.Hotfix` 只有一个空 `HotfixEntry`。本文档定义把现有 `APP.Runtime` (119 个 cs) 与 `APP.UI_V2` 中的业务代码迁入 `App.Hotfix` 的策略与步骤。

迁移目标：**功能等效**——所有现有 UI、Pomodoro、Settings、Network、SessionMemory 行为在迁移后表现与迁移前完全一致，仅入口路径变成 AOT Bootstrap 加载 Hotfix DLL。

## 分类约定

### 必须留在 AOT（不进 Hotfix）

| 类别 | 例子 | 原因 |
|------|------|------|
| 入口 MonoBehaviour | `BootstrapEntry` | 场景启动时 Native 反序列化加载 |
| 场景直接引用的 MonoBehaviour | MainV2 场景里 8 个挂脚本 | Scene YAML 用 GUID 引用类型，热更 DLL 重载时 GUID 失效 |
| 原生互操作 | `[DllImport("__Internal")]`、`[MonoPInvokeCallback]` 类 | HybridCLR 解释器不能调原生函数指针 |
| 包内 Editor 脚本 | 一切 `#if UNITY_EDITOR` 代码 | 不进 Player 也就不存在"热更"问题 |
| 公开给 Hotfix 反射调用的 façade | 一个 `App.Bootstrap.HotfixApi` 静态类（待新增） | 让 Hotfix 拿到原生句柄、Editor 工具等 |

### 优先进 Hotfix

| 类别 | 例子 | 原因 |
|------|------|------|
| Commands / Queries | `FeedPetCommand`、`GetHappinessQuery` | 无状态、由 Architecture 实例化 |
| Models | `PetModel`、`PomodoroModel`、`BindingKeyModel` | 数据，由 Architecture 注册 |
| Systems | `TimerSystem`、`BindingKeyCounterSystem` | 纯逻辑，由 Architecture 注册 |
| Events (struct) | `PetMoodChangedEvent` | struct，两边都能引用 |
| 非场景挂载的工具类 | 序列化器、转换器、扩展方法 | 只通过代码 new |
| Architecture 注册器 | `GameApp` | 由 HotfixEntry.Start() 触发 |

### 模糊地带：MonoBehaviour Controller

`APP.UI_V2/Controller/` 下的 `DeskWindowController`、`UnifiedSettingsPanelController` 等是 MonoBehaviour，挂在 MainV2 场景里。直接搬到 Hotfix 会导致场景里的脚本 ref 变红。

两种方案：

**方案 A（推荐起步阶段）— 保留 Controller 在 AOT，方法主体下沉到 Hotfix**

Controller 留在 `APP.UI_V2`，但每个事件回调都通过反射调到 Hotfix 中的一个伙伴类 (`HotfixDeskWindowController`)：

```csharp
// AOT 侧 (APP.UI_V2/Controller/DeskWindowController.cs)
public class DeskWindowController : MonoBehaviour {
    private static Action<DeskWindowController> s_onAwake;
    public static void Bind(Action<DeskWindowController> onAwake) => s_onAwake = onAwake;
    private void Awake() => s_onAwake?.Invoke(this);
}

// Hotfix 侧 (App.Hotfix/Controllers/HotfixDeskWindowController.cs)
public static class HotfixControllerBindings {
    public static void Register() {
        DeskWindowController.Bind(c => new HotfixDeskWindowController(c).Init());
    }
}
```

只搬 1 次 Controller 类，之后修改 `HotfixDeskWindowController` 全程热更。

**方案 B（中期推进）— HybridCLR MonoBehaviour-in-Hotfix**

HybridCLR 支持 MonoBehaviour 子类放进热更新程序集，但需要：
- `HybridCLRSettings.preserveHotUpdateAssemblies` 列上 `App.Hotfix`
- 生成 link.xml 保留 MonoScript GUID
- 场景里 `m_Script` 的 GUID 引用 Hotfix 类型，运行时 HybridCLR 拦截解析

成熟但配置复杂，先用方案 A 验证主链路，再切方案 B。

## ⚠️ 关键约束：依赖方向 = AOT ← Hotfix（单向）

迁移过程中**绝不能让 AOT 程序集 (APP.Runtime, APP.UI_V2) 在 .asmdef 引用 App.Hotfix**——否则热更新失去意义（修 Hotfix 必须重打 AOT）。

实测依赖扫描（grep `APP.SessionMemory` 跨模块引用）：
- `Assets/Scripts/APP/Network/System/NetworkSystem.cs`
- `Assets/Scripts/APP/Network/Command/Cmd_AutoReconnectOnStartup.cs`
- `Assets/Scripts/APP/Network/Command/Cmd_LeaveRoom.cs`
- `Assets/Scripts/APP/Pomodoro/GameApp.cs`（Architecture 入口）
- `Assets/UI_V2/Controller/OnlineSettingsPanelController.cs`

**结论**：单独迁 SessionMemory 会导致 5 个 AOT 文件 `CS0246: namespace 'APP.SessionMemory' not found`。
APP.Runtime 内部模块（Pomodoro/Network/Settings/SessionMemory/Utility）紧密耦合，
互相引用率超过 50%。任何"挑一个模块迁"都会触发跨模块编译错误。

## 迁移策略：单次大爆炸 (Big Bang)

唯一可行的路线是 **APP.Runtime 全部业务代码一次性平移到 App.Hotfix**：

1. **保留在 AOT (APP.Runtime / APP.UI_V2)**：
   - 所有 MonoBehaviour 类（场景/Prefab 引用 GUID 必须稳定）：约 8 个挂场景 + 若干 Controllers
   - QFramework.cs（基础框架）
   - 任何 `EditorOnlyDisableUniWindow.cs` 之类 `#if UNITY_EDITOR` 的代码
   - DllImport / MonoPInvokeCallback 类
   - 一个新增的 `App.Bootstrap.HotfixApi` 静态门面，给 Hotfix 反向调 AOT 用

2. **迁入 App.Hotfix**（一次性）：
   - `Assets/Scripts/APP/SessionMemory/**` (6 files)
   - `Assets/Scripts/APP/Settings/**` (~25 files)
   - `Assets/Scripts/APP/Pomodoro/{Command,Query,Model,System,Event,Config}/**`（绕开 View MonoBehaviour）
   - `Assets/Scripts/APP/Network/**`
   - `Assets/Scripts/APP/Utility/**`
   - `Assets/Scripts/APP/Pomodoro/GameApp.cs`（Architecture 入口）

3. **MonoBehaviour 桥接（方案 A）** — 单独一步：
   - 每个挂场景的 MonoBehaviour 保留空壳类在 `APP.UI_V2`
   - 实际方法实现搬到 `App.Hotfix` 的 `HotfixXxxController`
   - 通过 `App.Bootstrap.HotfixApi` 注册回调

## 落地步骤

每完成下面任一步立刻跑 `Smoke Test Hotfix Loader (Edit Mode)`，再跑全部 EditMode 测试。

### Step 1：扩 App.Hotfix.asmdef references

```jsonc
{
  "name": "App.Hotfix",
  "references": [
    "Unity.Addressables",
    "Unity.ResourceManagement",
    "APP.Runtime"   // 加这个让 hotfix 能直接 using QFramework / APP.Utility 等
  ]
}
```

注意：APP.Runtime ←┘ App.Hotfix 是**单向**——AOT 永远不引用 Hotfix。

### Step 2：物理移动文件 + 修 namespace（推荐用 Unity 的 Move，保留 .meta GUID）

```
Assets/Scripts/APP/SessionMemory/  →  Assets/HotUpdate/Hotfix/SessionMemory/
Assets/Scripts/APP/Settings/        →  Assets/HotUpdate/Hotfix/Settings/
Assets/Scripts/APP/Pomodoro/*       →  Assets/HotUpdate/Hotfix/Pomodoro/*     (除 View)
Assets/Scripts/APP/Network/         →  Assets/HotUpdate/Hotfix/Network/
Assets/Scripts/APP/Utility/         →  Assets/HotUpdate/Hotfix/Utility/
Assets/Scripts/APP/Pomodoro/GameApp.cs → Assets/HotUpdate/Hotfix/GameApp.cs
```

文件移动后 namespace 保持 `APP.SessionMemory.Model` 等不变（搬运不改命名空间，AOT consumer 不变）。
因为类型 namespace 与文件路径解耦，AOT 代码 `using APP.SessionMemory.Model;` 仍然能编译——
**只要 App.Hotfix 出现在 APP.Runtime 的程序集引用图里**。

但前面说过 APP.Runtime 不能 ref App.Hotfix。所以 AOT 那 5 个文件的 `using APP.SessionMemory.Model;`
怎么办？答案：把它们一起迁。GameApp、NetworkSystem、Cmd_AutoReconnectOnStartup、Cmd_LeaveRoom、
OnlineSettingsPanelController 这 5 个本来就该进 Hotfix（它们是 Command/Query/System/Controller-bridge）。

### Step 3：把 MonoBehaviour 上层 Controller 留壳，业务搬走

`OnlineSettingsPanelController` 留 AOT 壳：
```csharp
// AOT: Assets/UI_V2/Controller/OnlineSettingsPanelController.cs
public class OnlineSettingsPanelController : MonoBehaviour {
    private void Awake() => HotfixApi.OnOnlineSettingsAwake?.Invoke(this);
}
```

Hotfix 实现：
```csharp
// Hotfix: Assets/HotUpdate/Hotfix/UI/OnlineSettingsPanelHotfix.cs
public static class OnlineSettingsPanelHotfix {
    public static void Init() {
        HotfixApi.OnOnlineSettingsAwake = ctrl => new Impl(ctrl);
    }
    private class Impl { ...原本 OnlineSettingsPanelController 内部逻辑... }
}
```

`HotfixEntry.Start()` 里调一遍所有 `*Hotfix.Init()`。

### Step 4：HybridCLR 重生成 + 测试

- `HybridCLR/Generate/All` 重生 link.xml + AOTGenericReference
- 跑 Smoke Test + 全部 EditMode 单测
- 切 IL2CPP 出包：`Build/Build, Run and Verify macOS App`
- 启动 `Tools/AAServer/aa_server.py`，应用启动后验所有 UI 与现状一致

## 估时（粗略）

- Step 1（asmdef 改）：5 min
- Step 2（文件迁 + namespace 保持）：1.5 h（手工 + 自动）
- Step 3（MonoBehaviour 桥接）：3-4 h（每个 controller 单独处理）
- Step 4（HybridCLR Generate + 调试）：1-2 h
- 全套测试 + 回归：2 h

**单次大爆炸总估时：≈ 8-10 h，建议拆 2-3 个工作会话完成。**

## 验收

- [ ] Editor PlayMode 跑完 `Tools/CPA/HotUpdate/Smoke Test Hotfix Loader (Edit Mode)` 全绿
- [ ] `Build/Build, Run and Verify macOS App` 出 IL2CPP 包，启动 `Tools/AAServer/aa_server.py`，应用启动后 console 看到 `[Hotfix] HotfixEntry.Start()` 与所有原有功能 trace
- [ ] 用户主用例（Pomodoro 番茄钟、Settings 全局设置、按键计数器、网络对战）全部回归测试通过
- [ ] 修改 `App.Hotfix` 中任一文件后，**重新跑 `HybridCLR/CompileDll/ActiveBuildTarget` + `Addressables/Build/Build PlayerContent` 即可让现有 .app 看到新逻辑（不重打 IL2CPP）**——这才是"热更新生效"的真正定义

## 风险

- **Unity 6.3 + macOS 装 HybridCLR 默认会拖崩编辑器**：详见 memory `feedback_hybridclr_mac_unity6_install.md`。必须走 `Tools/CPA/HotUpdate/Install HybridCLR (Mac safe)` 菜单。
- **MonoBehaviour 场景引用断**：迁 Controller 前先备份 MainV2.unity；切方案 A 期间 m_Script GUID 不变。
- **AOT 泛型补元数据漏列**：迁完后 `HybridCLR/Generate/All` 会扫描 hotfix 用到的 AOT 泛型，注入 `patchAOTAssemblies`。如果运行时还报 `MissingMethodException`，按报错栈找到对应泛型 → 加 `patchAOTAssemblies` → 重 Generate。
- **PlayerSettings 必须先切 IL2CPP**：当前 Standalone scripting backend 大概率仍是 Mono。打包前 `PlayerSettings → Other Settings → Scripting Backend = IL2CPP, Api Compatibility Level = .NET Framework`。第一次切要等 5–15 min IL2CPP 全工程编译。
