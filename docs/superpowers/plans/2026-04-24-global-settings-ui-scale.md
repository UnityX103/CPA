# 全局设置面板 · UI 缩放倍率 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给 Unified Settings 面板新增一个"全局设置"Tab，提供 `0.5×–2.0×`（步长 0.1）UI 缩放滑块；点"应用"后改两个 `PanelSettings.scale`，弹出居中倒计时对话框（`5s` 未保留则自动回滚）。顺带把"未保存更改"对话框与番茄钟 Apply 按钮抽成可复用组件。

**Architecture:** QFramework 四层分工——`ISettingsModel` 暴露 `PreviewUiScale`（不持久化）+ `UiScale`（持久化）两个 `BindableProperty<float>`；一个 MonoBehaviour `PanelScaleApplier` 订阅 `PreviewUiScale` 写两个 `PanelSettings.scale`；三个 `Cmd_*` 统一写入入口；`ConfirmDialogController` 通用对话框（可选倒计时）服务"未保存更改"与"缩放倒计时"两场景，旧 `UnsavedChangesDialogController` 改为薄适配保持测试兼容。

**Tech Stack:** Unity 6000.0.25f1 · QFramework v1.0（`Assets/Scripts/QFramework.cs`）· UI Toolkit（UIDocument / PanelSettings / Slider）· NUnit EditMode 测试 · PlayerPrefs 持久化 · Pencil MCP 设计稿

**参考 Spec：** `docs/superpowers/specs/2026-04-24-global-settings-ui-scale-design.md`

---

## 文件结构

### 新建

| Path | 责任 |
|---|---|
| `Assets/Scripts/APP/Settings/Model/ISettingsModel.cs` | 接口：`PreviewUiScale` + `UiScale` |
| `Assets/Scripts/APP/Settings/Model/SettingsModel.cs` | 实现：默认值、Clamp、持久化注册 |
| `Assets/Scripts/APP/Settings/Command/Cmd_SetPreviewUiScale.cs` | 写 `PreviewUiScale`（带 Clamp） |
| `Assets/Scripts/APP/Settings/Command/Cmd_CommitUiScale.cs` | `UiScale = PreviewUiScale`（触发持久化） |
| `Assets/Scripts/APP/Settings/Command/Cmd_RevertUiScale.cs` | `PreviewUiScale = UiScale`（回滚） |
| `Assets/UI_V2/Controller/PanelScaleApplier.cs` | MonoBehaviour：订阅 `PreviewUiScale` 写两个 `PanelSettings.scale` |
| `Assets/UI_V2/Controller/ConfirmDialogController.cs` | 通用确认对话框 + 可选倒计时 |
| `Assets/UI_V2/Controller/GlobalSettingsPanelController.cs` | 全局设置面板行为 |
| `Assets/UI_V2/Documents/ConfirmDialog.uxml` | 通用对话框模板（带 `dlg-countdown` 行） |
| `Assets/UI_V2/Styles/ConfirmDialog.uss` | 对话框样式（backdrop + card + countdown） |
| `Assets/UI_V2/Documents/GlobalSettingsPanel.uxml` | 全局面板模板 |
| `Assets/UI_V2/Styles/GlobalSettingsPanel.uss` | 全局面板样式 |
| `Assets/Tests/EditMode/SettingsTests/Editor/APP.Settings.Tests.asmdef` | 测试 asmdef |
| `Assets/Tests/EditMode/SettingsTests/Editor/SettingsModelTests.cs` | Model 单测 |
| `Assets/Tests/EditMode/SettingsTests/Editor/UiScaleCommandTests.cs` | 三个 Command 单测 |
| `Assets/Tests/EditMode/SettingsTests/Editor/ConfirmDialogControllerTests.cs` | 对话框单测（无 schedule 依赖部分） |
| `Assets/Tests/EditMode/SettingsTests/Editor/GlobalSettingsPanelControllerTests.cs` | 全局面板行为单测 |

### 修改

| Path | 改动点 |
|---|---|
| `Assets/Scripts/APP/Pomodoro/GameApp.cs` | 注册 `ISettingsModel` |
| `Assets/UI_V2/Controller/UnsavedChangesDialogController.cs` | 改造为 `ConfirmDialogController` 的薄适配 |
| `Assets/UI_V2/Controller/UnifiedSettingsPanelController.cs` | 新 `_tabGlobal` / `_globalSettings` / `_scaleDialogHost`；`Init` 新参数；`SelectTab`/`DoSelectTab`/`EnsureTabContent` 加 `"global"` 分支 |
| `Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs` | 加 `_globalSettingsTemplate`；原 `_unsavedChangesDialogTemplate` 打 `[FormerlySerializedAs]` 保持引用、重绑到 `ConfirmDialog.uxml` |
| `Assets/UI_V2/Controller/PomodoroSettingsPanelView.cs` | 查询节点 `psp-apply-btn` → `apply-btn`；class `psp-apply-btn--hidden` → `apply-btn--hidden` |
| `Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml` | 侧栏追加 `tab-global`；overlay 追加 `scale-dialog-host`；Style 列表删 `UnsavedChangesDialog.uss`、加 `ConfirmDialog.uss` + `GlobalSettingsPanel.uss` |
| `Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml` | `psp-apply-btn` → `apply-btn`；CSS class 同步 |
| `Assets/UI_V2/Styles/PomodoroSettingsPanel.uss` | `.psp-apply-btn` / `.psp-apply-btn--hidden` → `.apply-btn` / `.apply-btn--hidden`（或保留语义，仅换名） |

### 删除

| Path |
|---|
| `Assets/UI_V2/Documents/UnsavedChangesDialog.uxml` + `.meta` |
| `Assets/UI_V2/Styles/UnsavedChangesDialog.uss` + `.meta` |

### Pencil 手工（用户）

- 新建三个 reusable：`ConfirmDialog`、`SettingsApplyRow`、`Global Settings Panel`
- 把 `ikREg` 改为 `ConfirmDialog` 实例（overrides 文案）
- 把 `gs1Tv/PBXgQ` 改为 `SettingsApplyRow` 实例
- `vnYnS` 侧栏 `Cz9E3 tab-pet` 之后追加 `tab-global`
- `settings-overlay` 下 `unsaved-dialog-host` 之后追加 `scale-dialog-host`
- **⌘S 落盘**（Pencil 不自动保存）

---

## 实施约束（项目特定）

- **实现先于测试**：按用户 memory `feedback_unity_tdd_order.md`，Unity 下"先让测试失败"会引发全工程 `CS0234`。每个功能先写实现 → 再写测试，跑测试时第一次就期望通过（或合理断言失败）。
- **.meta 自动生成**：新建 `.cs` / `.uxml` / `.uss` 后交由 Unity 编辑器在下一次 `AssetDatabase.Refresh` 生成 `.meta`；**不要手写 GUID**。
- **Pencil 优先**：任何 UI 变更先在 Pencil 调好并 ⌘S 落盘，再同步 UXML/USS/C#。
- **编译检查**：每个"实现 step"后用 `read_console` 过滤 `Error` 检查；`validate_script` 做静态分析。
- **Commit 节奏**：每个阶段收尾一次 commit（6 个阶段，6 个 commit）。commit message 用中文主题 + Conventional Commits 前缀，Co-Author 签 Claude。

---

## Stage A · Pencil 设计落盘（用户手工）

### Task 1：Pencil 三个 reusable + vnYnS 侧栏与 host 扩展

**Files (Pencil document `/Users/xpy/Desktop/NanZhai/CPA/AUI/PUI.pen`)**
- 新增 reusable：`ConfirmDialog`、`SettingsApplyRow`、`Global Settings Panel`
- 修改实例：`ikREg`（→ ConfirmDialog 实例）、`PBXgQ`（→ SettingsApplyRow 实例）
- 修改 `vnYnS` 侧栏 `qvEam`、body 底部 host

- [ ] **Step 1.1：新建 `ConfirmDialog` reusable**

结构（参考 spec §4.1）。在 Pencil 里复制 `ikREg` 全结构，重命名为 `ConfirmDialog`，然后在 `dlg-header` 和 `dlg-body` 之间插入一行：

```
dlg-countdown (row, justifyContent:center, display:none)
└─ dlg-countdown-text (label "剩余 5s 后自动还原", #D97706 / 13 / 700)
```

关键：`dlg-countdown` 默认 `display:none`，代码通过 `style.display` 切换显隐。`ConfirmDialog` 自身 `reusable:true`。

- [ ] **Step 1.2：新建 `SettingsApplyRow` reusable**

```
SettingsApplyRow (frame, reusable, width:fill_container, justifyContent:end, padding:0)
└─ apply-btn (ref:Za5wE Button/Primary, width:120, label "应用")
```

- [ ] **Step 1.3：新建 `Global Settings Panel` reusable**

```
Global Settings Panel (frame, reusable, fill:#FFFFFF00, gap:16, layout:vertical, width:fill_container(572))
├─ gspApply (ref:SettingsApplyRow)
└─ gspScale (card, cornerRadius:16, fill:#F6F7F8, padding:16, gap:10, layout:vertical)
    ├─ gsp-scale-label (label "界面缩放", #9CA3AF / 12 / 600)
    └─ gsp-scale-row (row, alignItems:center, gap:12)
        ├─ gsp-scale-slider (frame 占位 "Slider", flex:1, height:24, cornerRadius:12, fill:#FFFFFFCC, stroke:#E5E7EB)
        └─ gsp-scale-value (label "1.0×", #1A1A1A / 14 / 700, minWidth:48, textAlign:right)
```

（Slider 真实控件在 UXML 里用 `<ui:Slider>`，Pencil 只需放一个视觉占位帮助设计对齐）

- [ ] **Step 1.4：把 `ikREg` 改为 `ConfirmDialog` 实例**

在 Pencil 里：右键 `ikREg` → 转为 `ConfirmDialog` 的实例（保留原 node id `ikREg`）。overrides：
- `dlg-title` = "有未保存的更改"
- `dlg-subtitle` = "请先应用或取消后再继续"
- `dlg-body` = "你修改了番茄钟设置但尚未应用。离开此面板将丢失这些改动，是否先保存并继续？"
- `dlg-confirm` 按钮 label = "保存并继续"
- `dlg-cancel` 按钮 label = "取消"
- `dlg-countdown` = hidden

- [ ] **Step 1.5：把 `gs1Tv/PBXgQ` 改为 `SettingsApplyRow` 实例**

保留 node id `PBXgQ`。

- [ ] **Step 1.6：`vnYnS` 侧栏与 host 扩展**

- `qvEam` 侧栏 `Cz9E3 tab-pet` 之后追加：
  ```
  tab-global (frame, padding:[10,14], width:fill_container, fill:none)
  └─ label "全局" (#9E8E80 / 13 / 500)
  ```
- 把 `Global Settings Panel` 的实例 ref 挂在 `2RdBk contentArea` 下（作为与 `gs1Tv`/`8Le5R`/`v2ZgA` 并列的一个备用 panel——Pencil 侧设计即可，UXML 侧会通过 Controller 动态切换）
- `settings-overlay` 下 `unsaved-dialog-host` 之后追加：
  ```
  scale-dialog-host (frame, name="scale-dialog-host", picking-mode:Ignore)
  ```

- [ ] **Step 1.7：落盘到 `.pen`**

两种方式任选其一：

- **自动**：`bash .claude/skills/pencil-autosave/save.sh` —— 由 `pencil-autosave` skill 切焦点到 Pencil、发 ⌘S、切回。**前置**：macOS 已给运行 Claude Code 的终端/进程授予辅助功能权限（首次运行会弹授权对话框）
- **手动**：切到 Pencil 按 `Cmd+S`

不落盘后续 UXML/代码查节点时会对不上 Pencil 里的 name，**必须**在继续下一任务前完成。

- [ ] **Step 1.8：Commit（Pencil 文件变更）**

```bash
git add AUI/PUI.pen
git commit -m "$(cat <<'EOF'
design(settings): Pencil 新增全局设置面板与通用对话框/应用按钮组件

- 新建 reusable: ConfirmDialog (带可选倒计时行)
- 新建 reusable: SettingsApplyRow (顶部靠右 Primary 按钮)
- 新建 reusable: Global Settings Panel (界面缩放滑块卡片)
- ikREg 改为 ConfirmDialog 实例 (overrides 文案)
- gs1Tv/PBXgQ 改为 SettingsApplyRow 实例
- vnYnS 侧栏追加 tab-global、overlay 追加 scale-dialog-host

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Stage B · Settings Model + Commands + 测试

### Task 2：`ISettingsModel` + `SettingsModel`

**Files:**
- Create: `Assets/Scripts/APP/Settings/Model/ISettingsModel.cs`
- Create: `Assets/Scripts/APP/Settings/Model/SettingsModel.cs`

- [ ] **Step 2.1：创建 Model 目录与接口**

文件：`Assets/Scripts/APP/Settings/Model/ISettingsModel.cs`

```csharp
using QFramework;

namespace APP.Settings.Model
{
    /// <summary>
    /// 全局设置 Model。
    /// UiScale 为已保留/持久化的正式值；PreviewUiScale 为当前正在生效/预览的值，
    /// 订阅者（PanelScaleApplier）据此写 PanelSettings.scale。
    /// 仅 UiScale 会被持久化到 PlayerPrefs。
    /// </summary>
    public interface ISettingsModel : IModel
    {
        /// <summary>当前正在生效/预览中的缩放倍率。不持久化。</summary>
        BindableProperty<float> PreviewUiScale { get; }

        /// <summary>已保留/持久化的缩放倍率。自动写入 PlayerPrefs。</summary>
        BindableProperty<float> UiScale { get; }
    }
}
```

- [ ] **Step 2.2：创建 Model 实现**

文件：`Assets/Scripts/APP/Settings/Model/SettingsModel.cs`

```csharp
using APP.Utility;
using QFramework;
using UnityEngine;

namespace APP.Settings.Model
{
    public sealed class SettingsModel : AbstractModel, ISettingsModel
    {
        public const float MinScale     = 0.5f;
        public const float MaxScale     = 2.0f;
        public const float DefaultScale = 1.0f;

        private const string UiScaleKey = "Settings.UiScale";

        public BindableProperty<float> PreviewUiScale { get; } = new BindableProperty<float>(DefaultScale);
        public BindableProperty<float> UiScale        { get; } = new BindableProperty<float>(DefaultScale);

        protected override void OnInit()
        {
            var storage = this.GetUtility<IStorageUtility>();
            var loaded  = Mathf.Clamp(
                storage.Load(UiScaleKey, DefaultScale),
                MinScale, MaxScale);

            UiScale.SetValueWithoutEvent(loaded);
            PreviewUiScale.SetValueWithoutEvent(loaded);

            // 只有正式值自动持久化；预览值不写 PlayerPrefs
            UiScale.Register(v => storage.Save(UiScaleKey, v));
        }
    }
}
```

- [ ] **Step 2.3：Unity 刷新 + 编译检查**

在 Unity Editor 中菜单 `Assets → Refresh`（或等待 Editor 自动刷新）。然后：

```
MCP tool: read_console
filter types: error, warning
```

**期望**：无 error。若有 `CS0234: The type or namespace name 'Utility' does not exist`，检查 `using APP.Utility;` 与实际 `IStorageUtility` 命名空间是否一致（可用 `mcp__UnityMCP__find_in_file` 定位）。

---

### Task 3：三个 Command

**Files:**
- Create: `Assets/Scripts/APP/Settings/Command/Cmd_SetPreviewUiScale.cs`
- Create: `Assets/Scripts/APP/Settings/Command/Cmd_CommitUiScale.cs`
- Create: `Assets/Scripts/APP/Settings/Command/Cmd_RevertUiScale.cs`

- [ ] **Step 3.1：`Cmd_SetPreviewUiScale`**

```csharp
using APP.Settings.Model;
using QFramework;
using UnityEngine;

namespace APP.Settings.Command
{
    /// <summary>
    /// 写入 PreviewUiScale（驱动 PanelScaleApplier 应用到 PanelSettings.scale）。
    /// 对非法值（NaN/Infinity）做保护——直接丢弃；合法值 Clamp 到 [MinScale, MaxScale]。
    /// </summary>
    public sealed class Cmd_SetPreviewUiScale : AbstractCommand
    {
        private readonly float _scale;
        public Cmd_SetPreviewUiScale(float scale) => _scale = scale;

        protected override void OnExecute()
        {
            if (!float.IsFinite(_scale)) return;
            this.GetModel<ISettingsModel>().PreviewUiScale.Value =
                Mathf.Clamp(_scale, SettingsModel.MinScale, SettingsModel.MaxScale);
        }
    }
}
```

- [ ] **Step 3.2：`Cmd_CommitUiScale`**

```csharp
using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>
    /// 把当前预览值提交为正式值（触发 UiScale.Register 持久化）。
    /// </summary>
    public sealed class Cmd_CommitUiScale : AbstractCommand
    {
        protected override void OnExecute()
        {
            var m = this.GetModel<ISettingsModel>();
            m.UiScale.Value = m.PreviewUiScale.Value;
        }
    }
}
```

- [ ] **Step 3.3：`Cmd_RevertUiScale`**

```csharp
using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>
    /// 把预览值刷回正式值（PanelScaleApplier 把 PanelSettings.scale 回滚）。
    /// </summary>
    public sealed class Cmd_RevertUiScale : AbstractCommand
    {
        protected override void OnExecute()
        {
            var m = this.GetModel<ISettingsModel>();
            m.PreviewUiScale.Value = m.UiScale.Value;
        }
    }
}
```

- [ ] **Step 3.4：编译检查**

```
MCP tool: read_console
filter types: error
```

**期望**：无 error。

---

### Task 4：`GameApp.cs` 注册 SettingsModel

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/GameApp.cs`

- [ ] **Step 4.1：在 Utility 之后、`PomodoroModel` 之前插入注册**

文件：`Assets/Scripts/APP/Pomodoro/GameApp.cs`

在 `RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());` 之后、`RegisterModel<IPomodoroModel>(new PomodoroModel());` 之前加：

```csharp
RegisterModel<ISettingsModel>(new SettingsModel());
```

顶部 using 加：

```csharp
using APP.Settings.Model;
```

完整上下文（插入后的片段）：

```csharp
using APP.Network.Model;
using APP.Network.System;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using APP.SessionMemory.Model;
using APP.Settings.Model;
using APP.Utility;
using QFramework;

namespace APP.Pomodoro
{
    public sealed class GameApp : Architecture<GameApp>
    {
        protected override void Init()
        {
            RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());

            RegisterModel<ISettingsModel>(new SettingsModel());

            RegisterModel<IPomodoroModel>(new PomodoroModel());
            // ...其余保持不变
```

- [ ] **Step 4.2：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。

---

### Task 5：Settings 测试 asmdef + 单测

**Files:**
- Create: `Assets/Tests/EditMode/SettingsTests/Editor/APP.Settings.Tests.asmdef`
- Create: `Assets/Tests/EditMode/SettingsTests/Editor/SettingsModelTests.cs`
- Create: `Assets/Tests/EditMode/SettingsTests/Editor/UiScaleCommandTests.cs`

- [ ] **Step 5.1：创建测试 asmdef**

文件：`Assets/Tests/EditMode/SettingsTests/Editor/APP.Settings.Tests.asmdef`

```json
{
    "name": "APP.Settings.Tests",
    "rootNamespace": "APP.Settings.Tests",
    "references": [
        "APP.Runtime"
    ],
    "includePlatforms": [
        "Editor"
    ],
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

- [ ] **Step 5.2：`SettingsModelTests.cs`**

```csharp
using APP.Settings.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Settings.Tests
{
    public sealed class SettingsModelTests
    {
        private const string UiScaleKey = "Settings.UiScale";

        [SetUp]
        public void ClearKey() => PlayerPrefs.DeleteKey(UiScaleKey);

        [TearDown]
        public void CleanupKey() => PlayerPrefs.DeleteKey(UiScaleKey);

        [Test]
        public void OnInit_NoSavedValue_DefaultsTo1()
        {
            var model = new SettingsModel();
            ((IModel)model).Init();

            Assert.That(model.UiScale.Value, Is.EqualTo(SettingsModel.DefaultScale));
            Assert.That(model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.DefaultScale));
        }

        [Test]
        public void OnInit_LoadsSavedValue_IntoBothFields()
        {
            PlayerPrefs.SetFloat(UiScaleKey, 1.5f);
            var model = new SettingsModel();
            ((IModel)model).Init();

            Assert.That(model.UiScale.Value, Is.EqualTo(1.5f));
            Assert.That(model.PreviewUiScale.Value, Is.EqualTo(1.5f));
        }

        [Test]
        public void OnInit_ClampsOutOfRange_ToBounds()
        {
            PlayerPrefs.SetFloat(UiScaleKey, 10f);  // 超上限
            var model = new SettingsModel();
            ((IModel)model).Init();

            Assert.That(model.UiScale.Value, Is.EqualTo(SettingsModel.MaxScale));
            Assert.That(model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.MaxScale));
        }

        [Test]
        public void UiScaleChange_PersistsToPlayerPrefs()
        {
            var model = new SettingsModel();
            ((IModel)model).Init();

            model.UiScale.Value = 1.3f;

            Assert.That(PlayerPrefs.GetFloat(UiScaleKey, -1f), Is.EqualTo(1.3f));
        }

        [Test]
        public void PreviewUiScaleChange_DoesNotPersist()
        {
            var model = new SettingsModel();
            ((IModel)model).Init();
            PlayerPrefs.DeleteKey(UiScaleKey);  // 清除 OnInit 可能写入的默认值

            model.PreviewUiScale.Value = 1.8f;

            // 预览变化不触发持久化：PlayerPrefs 中应不存在该 key（或仍是旧值）
            Assert.That(PlayerPrefs.HasKey(UiScaleKey), Is.False);
        }
    }
}
```

- [ ] **Step 5.3：`UiScaleCommandTests.cs`**

```csharp
using APP.Pomodoro;  // GameApp
using APP.Settings.Command;
using APP.Settings.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Settings.Tests
{
    public sealed class UiScaleCommandTests
    {
        private ISettingsModel Model => GameApp.Interface.GetModel<ISettingsModel>();

        [SetUp]
        public void ResetModel()
        {
            // 让 Architecture 初始化
            _ = GameApp.Interface;
            Model.UiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
            Model.PreviewUiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
        }

        [Test]
        public void SetPreview_ClampsToBounds()
        {
            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(10f));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.MaxScale));

            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(-1f));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.MinScale));
        }

        [Test]
        public void SetPreview_InvalidNumber_DoesNotWrite()
        {
            Model.PreviewUiScale.SetValueWithoutEvent(1.2f);

            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(float.NaN));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.2f));

            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(float.PositiveInfinity));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.2f));
        }

        [Test]
        public void SetPreview_DoesNotChangeUiScale()
        {
            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(1.7f));

            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.7f));
            Assert.That(Model.UiScale.Value, Is.EqualTo(SettingsModel.DefaultScale));
        }

        [Test]
        public void Commit_CopiesPreviewToUiScale()
        {
            Model.PreviewUiScale.SetValueWithoutEvent(1.4f);

            GameApp.Interface.SendCommand(new Cmd_CommitUiScale());

            Assert.That(Model.UiScale.Value, Is.EqualTo(1.4f));
        }

        [Test]
        public void Revert_CopiesUiScaleToPreview()
        {
            Model.UiScale.SetValueWithoutEvent(1.0f);
            Model.PreviewUiScale.SetValueWithoutEvent(1.9f);

            GameApp.Interface.SendCommand(new Cmd_RevertUiScale());

            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.0f));
        }
    }
}
```

- [ ] **Step 5.4：运行测试**

```
MCP tool: run_tests
testMode: EditMode
testNames: [
  "APP.Settings.Tests.SettingsModelTests",
  "APP.Settings.Tests.UiScaleCommandTests"
]
```

轮询 `get_test_job` 直到完成。**期望**：所有测试通过。

若 `SettingsModelTests.OnInit_NoSavedValue_DefaultsTo1` 失败是因为 OnInit 注册了 Register 在初始化时把默认值写入 PlayerPrefs —— 实际不会，因为 `SetValueWithoutEvent` 不触发 Register。若仍失败，改写测试为允许 key 存在但值为 1.0。

---

### Task 6：Commit Stage B

- [ ] **Step 6.1：Stage & Commit**

```bash
git add Assets/Scripts/APP/Settings/ \
        Assets/Scripts/APP/Pomodoro/GameApp.cs \
        Assets/Tests/EditMode/SettingsTests/

git commit -m "$(cat <<'EOF'
feat(settings): 新增 ISettingsModel 与 UI 缩放相关 Commands

- ISettingsModel/SettingsModel: PreviewUiScale (不持久化) + UiScale (持久化)
  启动时从 PlayerPrefs 读取并 Clamp 到 [0.5, 2.0]，默认 1.0
- Cmd_SetPreviewUiScale: 带 NaN/Infinity 保护与 Clamp
- Cmd_CommitUiScale: Preview → UiScale，触发持久化
- Cmd_RevertUiScale: UiScale → Preview，回滚
- GameApp 注册 SettingsModel
- SettingsModelTests + UiScaleCommandTests 覆盖核心路径

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Stage C · PanelScaleApplier 与场景布线

### Task 7：`PanelScaleApplier` MonoBehaviour

**Files:**
- Create: `Assets/UI_V2/Controller/PanelScaleApplier.cs`

- [ ] **Step 7.1：创建文件**

```csharp
using APP.Pomodoro;
using APP.Settings.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 订阅 ISettingsModel.PreviewUiScale，把值同步写入所有关联 PanelSettings 的 scale。
    /// 挂在 DeskWindow 场景常驻 GameObject 上，Inspector 里把两个 PanelSettings 资源拖入数组。
    /// </summary>
    public sealed class PanelScaleApplier : MonoBehaviour, IController
    {
        [SerializeField]
        [Tooltip("拖入所有需要同步缩放的 PanelSettings 资源（PanelSettings_Settings、PomodoroPanelSettings）")]
        private PanelSettings[] _panelSettings;

        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private void Start()
        {
            _ = GameApp.Interface;  // 确保 Architecture 初始化

            this.GetModel<ISettingsModel>().PreviewUiScale
                .RegisterWithInitValue(ApplyToAll)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void ApplyToAll(float s)
        {
            if (_panelSettings == null || _panelSettings.Length == 0)
            {
                Debug.LogWarning("[PanelScaleApplier] _panelSettings 数组为空，UI 缩放不会生效。");
                return;
            }
            foreach (var ps in _panelSettings)
            {
                if (ps != null) ps.scale = s;
            }
        }
    }
}
```

- [ ] **Step 7.2：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。

---

### Task 8：场景布线 + Play Mode 冒烟

- [ ] **Step 8.1：在 DeskWindow 场景挂 `PanelScaleApplier`**

用户手动（或通过 Unity MCP `manage_gameobject`）：
1. 打开 DeskWindow 场景（或项目主场景；用 `manage_scene` 查询）
2. 选择 `DeskWindow` 根 GameObject（或找一个常驻的空物体；不要挂到某个 UIDocument 子物体上以免场景切换时被销毁）
3. Add Component → `PanelScaleApplier`
4. 把 `Assets/UI_V2/PanelSettings_Settings.asset` 和 `Assets/UI_V2/PomodoroPanelSettings.asset` 拖入 `_panelSettings` Size=2 的数组
5. 保存场景

> 若用 MCP 自动化：
> ```
> mcp__UnityMCP__manage_gameobject (action: add_component, component_type: "APP.Pomodoro.Controller.PanelScaleApplier")
> mcp__UnityMCP__manage_components (set SerializeField _panelSettings)
> ```
> 但 `PanelSettings` 数组字段的手动 Inspector 拖拽通常更可靠，建议手工。

- [ ] **Step 8.2：Play Mode 冒烟验证**

1. 进入 Play Mode
2. 打开设置面板（此时"全局"tab 还没接入，不需要测它）
3. 在 Hierarchy 选中挂了 `PanelScaleApplier` 的 GameObject
4. Inspector 中看不到运行时日志即可；`read_console` 确认无 warning `_panelSettings 数组为空`
5. 用 Unity 的 Console 或 `read_console` 观察有没有 `NullReferenceException`
6. 退出 Play Mode

此步仅验证组件本身初始化流程，不验证缩放可见性——那个等 Global 面板接入后手工验证。

- [ ] **Step 8.3：Commit Stage C**

```bash
git add Assets/UI_V2/Controller/PanelScaleApplier.cs \
        Assets/Scenes/  # 场景文件变更

git commit -m "$(cat <<'EOF'
feat(settings): 新增 PanelScaleApplier 订阅 PreviewUiScale 同步 PanelSettings

MonoBehaviour 挂在 DeskWindow 场景常驻 GameObject，Inspector 里
绑定 PanelSettings_Settings 与 PomodoroPanelSettings 两个资源。
PreviewUiScale 变化时遍历写入 PanelSettings.scale，实现桌面窗
与番茄钟窗同步缩放。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Stage D · 通用 ConfirmDialog 组件

### Task 9：`ConfirmDialog.uss`

**Files:**
- Create: `Assets/UI_V2/Styles/ConfirmDialog.uss`

- [ ] **Step 9.1：创建样式文件**

基于现有 `UnsavedChangesDialog.uss` 的样式，追加 `dlg-countdown` 行。先读现有样式作为参考：

```
Read: Assets/UI_V2/Styles/UnsavedChangesDialog.uss
```

然后新建 `ConfirmDialog.uss`（若现有不存在，从零写）。核心规则（基于 spec §4.1 + 项目 CSS token）：

```css
/* 外层 backdrop：absolute 全覆盖 overlay，picking 吃输入 */
.dlg-root {
    position: absolute;
    left: 0; right: 0; top: 0; bottom: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background-color: rgba(0, 0, 0, 0.35);
}

/* 白色卡片 */
.dlg-card {
    width: 420px;
    padding: 24px;
    background-color: var(--color-bg-white);
    border-radius: var(--radius-2xl);
    /* 阴影用 box-shadow（UI Toolkit 不支持，降级为 border） */
    border-color: var(--color-border-light);
    border-width: 1px;
}

.dlg-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 16px;
}

.dlg-title-wrap { flex-direction: column; }
.dlg-title {
    color: var(--color-text-dark);
    font-size: 16px;
    -unity-font-style: bold;
}
.dlg-subtitle {
    color: var(--color-text-muted);
    font-size: 12px;
    margin-top: 4px;
}

.dlg-close {
    width: 24px; height: 24px;
    align-items: center; justify-content: center;
    border-radius: 999px;
}
.dlg-close-icon {
    color: var(--color-text-secondary);
    font-size: 14px;
}

/* 倒计时行（默认隐藏） */
.dlg-countdown {
    flex-direction: row;
    justify-content: center;
    margin-bottom: 12px;
    display: none;
}
.dlg-countdown-text {
    color: var(--color-text-amber);
    font-size: 13px;
    -unity-font-style: bold;
}

.dlg-body {
    color: rgb(75, 85, 99);
    font-size: 14px;
    white-space: normal;
    margin-bottom: 16px;
}

.dlg-actions {
    flex-direction: row;
    justify-content: flex-end;
}

.dlg-btn {
    border-radius: 999px;
    padding: 7px 12px;
    min-width: 88px;
    -unity-font-style: bold;
    font-size: 13px;
    margin-left: 12px;
}

.dlg-btn--secondary {
    background-color: var(--color-bg-white);
    border-color: var(--color-border-warm);
    border-width: 1px;
    color: var(--color-text-secondary);
}

.dlg-btn--primary {
    background-color: var(--color-accent-orange);
    border-width: 0;
    color: var(--color-text-white);
}
```

> 若原 `UnsavedChangesDialog.uss` 已存在样式更贴合 Pencil 视觉，直接复制过来并追加 `.dlg-countdown` / `.dlg-countdown-text`。

---

### Task 10：`ConfirmDialog.uxml`

**Files:**
- Create: `Assets/UI_V2/Documents/ConfirmDialog.uxml`

- [ ] **Step 10.1：创建 UXML 文件**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements"
         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         noNamespaceSchemaLocation="../../../UIElementsSchema/UIElements.xsd"
         editor-extension-mode="False">
    <ui:Style src="../Styles/Variables.uss"/>
    <ui:Style src="../Styles/ConfirmDialog.uss"/>

    <!-- Pencil: ConfirmDialog — 通用确认对话框（backdrop + 白卡片 + 可选倒计时行） -->
    <ui:VisualElement name="dlg-root" class="dlg-root" style="display: none;">
        <ui:VisualElement class="dlg-card">
            <ui:VisualElement class="dlg-header">
                <ui:VisualElement class="dlg-title-wrap">
                    <ui:Label name="dlg-title" text="" class="dlg-title" />
                    <ui:Label name="dlg-subtitle" text="" class="dlg-subtitle" />
                </ui:VisualElement>
                <ui:VisualElement name="dlg-close" class="dlg-close">
                    <ui:Label text="✕" class="dlg-close-icon" />
                </ui:VisualElement>
            </ui:VisualElement>

            <!-- 倒计时行（Controller 根据 countdownSeconds 切换显隐） -->
            <ui:VisualElement name="dlg-countdown" class="dlg-countdown">
                <ui:Label name="dlg-countdown-text" text="" class="dlg-countdown-text" />
            </ui:VisualElement>

            <ui:Label name="dlg-body" text="" class="dlg-body" />

            <ui:VisualElement class="dlg-actions">
                <ui:Button name="dlg-cancel" text="" class="dlg-btn dlg-btn--secondary" />
                <ui:Button name="dlg-confirm" text="" class="dlg-btn dlg-btn--primary" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

---

### Task 11：`ConfirmDialogController`

**Files:**
- Create: `Assets/UI_V2/Controller/ConfirmDialogController.cs`

- [ ] **Step 11.1：创建控制器**

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 通用确认对话框控制器（纯 C# 类）。
    /// 可选 countdown 模式：Show(countdownSeconds > 0) 时启用倒计时，到 0 自动触发 onCancel。
    /// 使用 VisualElement.schedule 跑 tick，不依赖 MonoBehaviour。
    /// </summary>
    public sealed class ConfirmDialogController
    {
        private VisualElement _root;
        private VisualElement _countdownRow;
        private VisualElement _closeBtn;
        private Label _title;
        private Label _subtitle;
        private Label _body;
        private Label _countdownText;
        private Button _confirmBtn;
        private Button _cancelBtn;

        private IVisualElementScheduledItem _tickItem;
        private float _remainingSeconds;
        private string _countdownTemplate = "剩余 {0}s 后自动还原";

        private Action _onConfirm;
        private Action _onCancel;

        public bool IsVisible => _root != null && _root.style.display != DisplayStyle.None;

        public void Init(VisualElement host, VisualTreeAsset template)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (template == null)
            {
                Debug.LogError("[ConfirmDialogController] template 为空，对话框不可用");
                return;
            }

            host.Clear();
            template.CloneTree(host);

            _root          = host.Q<VisualElement>("dlg-root");
            _countdownRow  = host.Q<VisualElement>("dlg-countdown");
            _closeBtn      = host.Q<VisualElement>("dlg-close");
            _title         = host.Q<Label>("dlg-title");
            _subtitle      = host.Q<Label>("dlg-subtitle");
            _body          = host.Q<Label>("dlg-body");
            _countdownText = host.Q<Label>("dlg-countdown-text");
            _confirmBtn    = host.Q<Button>("dlg-confirm");
            _cancelBtn     = host.Q<Button>("dlg-cancel");

            if (_confirmBtn != null) _confirmBtn.clicked += HandleConfirm;
            if (_cancelBtn  != null) _cancelBtn.clicked  += HandleCancel;
            _closeBtn?.RegisterCallback<PointerUpEvent>(_ => HandleCancel());

            // 阻止 backdrop/卡片点击穿透
            _root?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            _root?.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());

            Hide();
        }

        public void Show(
            string title,
            string subtitle,
            string body,
            string confirmText,
            string cancelText,
            Action onConfirm,
            Action onCancel,
            float countdownSeconds = 0f)
        {
            if (_root == null) return;

            if (_title    != null) _title.text    = title    ?? string.Empty;
            if (_subtitle != null) _subtitle.text = subtitle ?? string.Empty;
            if (_body     != null) _body.text     = body     ?? string.Empty;
            if (_confirmBtn != null) _confirmBtn.text = confirmText ?? string.Empty;
            if (_cancelBtn  != null) _cancelBtn.text  = cancelText  ?? string.Empty;

            _onConfirm = onConfirm;
            _onCancel  = onCancel;

            StopCountdown();

            if (countdownSeconds > 0f)
            {
                _remainingSeconds = countdownSeconds;
                SetCountdownRowVisible(true);
                RefreshCountdownLabel();
                StartCountdown();
            }
            else
            {
                SetCountdownRowVisible(false);
            }

            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root == null) return;
            StopCountdown();
            _root.style.display = DisplayStyle.None;
        }

        // ─── 测试钩子（internal，仅供 EditMode 测试绕过 UI 事件直接触发） ──
        internal void TriggerConfirmForTest() => HandleConfirm();
        internal void TriggerCancelForTest()  => HandleCancel();

        /// <summary>
        /// 内部：每 tick 扣减剩余秒数并刷新文字；到 0 触发 HandleCancel。
        /// 公开 internal visibility 以便 EditMode 测试绕过 scheduler 调用。
        /// </summary>
        internal void TickElapsed(float deltaSeconds)
        {
            if (!IsVisible || _tickItem == null) return;
            _remainingSeconds -= deltaSeconds;
            if (_remainingSeconds <= 0f)
            {
                HandleCancel();
                return;
            }
            RefreshCountdownLabel();
        }

        internal float RemainingSeconds => _remainingSeconds;

        private void HandleConfirm()
        {
            var cb = _onConfirm;
            _onConfirm = null;
            _onCancel  = null;
            Hide();
            cb?.Invoke();
        }

        private void HandleCancel()
        {
            var cb = _onCancel;
            _onConfirm = null;
            _onCancel  = null;
            Hide();
            cb?.Invoke();
        }

        private void StartCountdown()
        {
            if (_root == null) return;
            _tickItem = _root.schedule
                .Execute(() => TickElapsed(0.5f))
                .Every(500);
        }

        private void StopCountdown()
        {
            _tickItem?.Pause();
            _tickItem = null;
        }

        private void SetCountdownRowVisible(bool visible)
        {
            if (_countdownRow == null) return;
            _countdownRow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshCountdownLabel()
        {
            if (_countdownText == null) return;
            int secs = Mathf.Max(0, Mathf.CeilToInt(_remainingSeconds));
            _countdownText.text = string.Format(_countdownTemplate, secs);
        }
    }
}
```

- [ ] **Step 11.2：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。

---

### Task 12：`UnsavedChangesDialogController` 改为薄适配

**Files:**
- Modify: `Assets/UI_V2/Controller/UnsavedChangesDialogController.cs`

- [ ] **Step 12.1：整体替换文件内容**

```csharp
using System;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// "未保存更改"对话框薄适配层：保持原有 API（Init/Show/Hide/IsVisible）不变，
    /// 内部委托给 ConfirmDialogController，传入预置的标题/正文/按钮文案，countdownSeconds=0。
    /// 仅为兼容 PomodoroSettingsPanel 现有调用点，避免破坏测试。
    /// </summary>
    public sealed class UnsavedChangesDialogController
    {
        private readonly ConfirmDialogController _inner = new ConfirmDialogController();

        public bool IsVisible => _inner.IsVisible;

        /// <summary>
        /// 注意：template 现应传入 ConfirmDialog.uxml，而不是已删除的 UnsavedChangesDialog.uxml。
        /// UnifiedSettingsPanelDriver 的序列化字段需要重新指向新资源。
        /// </summary>
        public void Init(VisualElement host, VisualTreeAsset confirmDialogTemplate)
            => _inner.Init(host, confirmDialogTemplate);

        public void Show(Action onConfirm, Action onCancel)
            => _inner.Show(
                title:       "有未保存的更改",
                subtitle:    "请先应用或取消后再继续",
                body:        "你修改了番茄钟设置但尚未应用。离开此面板将丢失这些改动，是否先保存并继续？",
                confirmText: "保存并继续",
                cancelText:  "取消",
                onConfirm:   onConfirm,
                onCancel:    onCancel,
                countdownSeconds: 0f);

        public void Hide() => _inner.Hide();
    }
}
```

- [ ] **Step 12.2：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。

---

### Task 13：切换 UnifiedSettingsPanel 的 Style + 删除旧对话框文件

**Files:**
- Modify: `Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml`（Style 列表）
- Delete: `Assets/UI_V2/Documents/UnsavedChangesDialog.uxml` + `.meta`
- Delete: `Assets/UI_V2/Styles/UnsavedChangesDialog.uss` + `.meta`

- [ ] **Step 13.1：修改 UnifiedSettingsPanel.uxml 顶部 Style 列表**

文件：`Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml`

把第 10 行

```xml
<Style src="../Styles/UnsavedChangesDialog.uss"/>
```

替换为

```xml
<Style src="../Styles/ConfirmDialog.uss"/>
```

- [ ] **Step 13.2：删除旧文件**

```bash
git rm Assets/UI_V2/Documents/UnsavedChangesDialog.uxml \
       Assets/UI_V2/Documents/UnsavedChangesDialog.uxml.meta \
       Assets/UI_V2/Styles/UnsavedChangesDialog.uss \
       Assets/UI_V2/Styles/UnsavedChangesDialog.uss.meta
```

- [ ] **Step 13.3：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。若有 `Asset reference not found` 之类 warning 说明仍有 UXML 在引用被删的 uss，用 `grep -r "UnsavedChangesDialog.uss" Assets/` 确认并修掉。

---

### Task 14：`UnifiedSettingsPanelDriver` 字段改名 + Inspector 手工重绑

**Files:**
- Modify: `Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs`

- [ ] **Step 14.1：序列化字段改名（保留 Inspector 引用）**

文件：`Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs`

把

```csharp
[Header("未保存更改提示对话框模板")]
[SerializeField] private VisualTreeAsset _unsavedChangesDialogTemplate;
```

替换为

```csharp
[Header("通用确认对话框模板（用于未保存更改 + 缩放倒计时）")]
[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_unsavedChangesDialogTemplate")]
private VisualTreeAsset _confirmDialogTemplate;
```

并把 `Init` 调用里的 `_unsavedChangesDialogTemplate` 替换为 `_confirmDialogTemplate`：

```csharp
_controller.Init(
    _root,
    pomodoroModel,
    roomModel,
    _pomodoroSettingsTemplate,
    _onlineSettingsTemplate,
    _petSettingsTemplate,
    _confirmDialogTemplate,   // ← 改名
    gameObject);
```

> `[FormerlySerializedAs]` 让 Unity 自动把旧序列化数据迁移到新字段，Inspector 里**不丢引用**。

- [ ] **Step 14.2：Inspector 手工重新绑定模板到 `ConfirmDialog.uxml`**

用户手工：
1. 选中场景里的 `UnifiedSettingsPanel` GameObject
2. 在 Inspector 的 `UnifiedSettingsPanelDriver` 组件上找到 `_confirmDialogTemplate`
3. 把 `Assets/UI_V2/Documents/ConfirmDialog.uxml` 拖入（替换原来指向已删除的 `UnsavedChangesDialog.uxml` 的引用）
4. 保存场景

- [ ] **Step 14.3：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。

---

### Task 15：`ConfirmDialogControllerTests`

**Files:**
- Create: `Assets/Tests/EditMode/SettingsTests/Editor/ConfirmDialogControllerTests.cs`

- [ ] **Step 15.1：创建测试文件**

> 注意：EditMode 下 `VisualElement.schedule` 需要 panel context。测试只断言可同步调用的状态（`Show` 后 IsVisible、文案赋值、`TickElapsed` 手动推进）。真实 scheduler 由 PlayMode 手工验证。

```csharp
using APP.Pomodoro.Controller;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityEngine;

namespace APP.Settings.Tests
{
    public sealed class ConfirmDialogControllerTests
    {
        private VisualElement _host;
        private VisualTreeAsset _template;
        private ConfirmDialogController _ctrl;

        [SetUp]
        public void SetUp()
        {
            _host = new VisualElement();
            _template = Resources.Load<VisualTreeAsset>("ConfirmDialog");
            if (_template == null)
            {
                // 开发期从 AssetDatabase 加载（EditMode 下可用）
                #if UNITY_EDITOR
                _template = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/UI_V2/Documents/ConfirmDialog.uxml");
                #endif
            }
            Assert.That(_template, Is.Not.Null, "ConfirmDialog.uxml 未找到");

            _ctrl = new ConfirmDialogController();
            _ctrl.Init(_host, _template);
        }

        [Test]
        public void InitialState_IsHidden()
        {
            Assert.That(_ctrl.IsVisible, Is.False);
        }

        [Test]
        public void Show_NoCountdown_BecomesVisibleAndHidesCountdownRow()
        {
            _ctrl.Show("T", "S", "B", "Y", "N", null, null, countdownSeconds: 0f);

            Assert.That(_ctrl.IsVisible, Is.True);
            var row = _host.Q<VisualElement>("dlg-countdown");
            Assert.That(row.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void Show_WithCountdown_ShowsCountdownRowAndSetsInitialSeconds()
        {
            _ctrl.Show("T", "S", "B", "Y", "N", null, null, countdownSeconds: 5f);

            var row = _host.Q<VisualElement>("dlg-countdown");
            Assert.That(row.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_ctrl.RemainingSeconds, Is.EqualTo(5f));

            var txt = _host.Q<Label>("dlg-countdown-text");
            StringAssert.Contains("5", txt.text);
        }

        [Test]
        public void TickElapsed_DecrementsRemainingSecondsAndRefreshesLabel()
        {
            _ctrl.Show("T", "S", "B", "Y", "N", null, null, countdownSeconds: 5f);

            _ctrl.TickElapsed(0.5f);

            Assert.That(_ctrl.RemainingSeconds, Is.EqualTo(4.5f).Within(0.001f));
            var txt = _host.Q<Label>("dlg-countdown-text");
            StringAssert.Contains("5", txt.text);  // ceil(4.5) = 5
        }

        [Test]
        public void TickElapsed_ReachesZero_InvokesOnCancelAndHides()
        {
            int cancelCount = 0;
            int confirmCount = 0;
            _ctrl.Show("T", "S", "B", "Y", "N",
                onConfirm: () => confirmCount++,
                onCancel:  () => cancelCount++,
                countdownSeconds: 1f);

            _ctrl.TickElapsed(1.1f);

            Assert.That(_ctrl.IsVisible, Is.False);
            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(confirmCount, Is.EqualTo(0));
        }

        [Test]
        public void ManualConfirm_InvokesOnConfirmAndHides()
        {
            int confirmCount = 0;
            _ctrl.Show("T", "S", "B", "Y", "N",
                onConfirm: () => confirmCount++,
                onCancel:  null,
                countdownSeconds: 0f);

            _ctrl.TriggerConfirmForTest();

            Assert.That(_ctrl.IsVisible, Is.False);
            Assert.That(confirmCount, Is.EqualTo(1));
        }

        [Test]
        public void ManualCancel_InvokesOnCancelAndHides()
        {
            int cancelCount = 0;
            _ctrl.Show("T", "S", "B", "Y", "N",
                onConfirm: null,
                onCancel:  () => cancelCount++,
                countdownSeconds: 0f);

            _ctrl.TriggerCancelForTest();

            Assert.That(_ctrl.IsVisible, Is.False);
            Assert.That(cancelCount, Is.EqualTo(1));
        }

        [Test]
        public void TextsAreApplied()
        {
            _ctrl.Show("标题X", "副标题Y", "正文Z", "确认", "取消", null, null, 0f);

            Assert.That(_host.Q<Label>("dlg-title").text, Is.EqualTo("标题X"));
            Assert.That(_host.Q<Label>("dlg-subtitle").text, Is.EqualTo("副标题Y"));
            Assert.That(_host.Q<Label>("dlg-body").text, Is.EqualTo("正文Z"));
            Assert.That(_host.Q<Button>("dlg-confirm").text, Is.EqualTo("确认"));
            Assert.That(_host.Q<Button>("dlg-cancel").text, Is.EqualTo("取消"));
        }
    }
}
```

> Button.clicked 在 UI Toolkit 里是 `event Action`（外部不能 `.Invoke()`），也无法在 detached VisualElement 上可靠地 `SendEvent(ClickEvent)`。因此 ConfirmDialogController 在 Task 11 中暴露了 `TriggerConfirmForTest()` / `TriggerCancelForTest()` 两个 internal 钩子，测试直接调用来模拟按钮点击。

- [ ] **Step 15.2：测试 asmdef 加 UI_V2 引用**

文件：`Assets/Tests/EditMode/SettingsTests/Editor/APP.Settings.Tests.asmdef`

修改 `references` 为：

```json
"references": [
    "APP.Runtime",
    "APP.UI_V2"
]
```

- [ ] **Step 15.3：运行测试**

```
MCP tool: run_tests
testMode: EditMode
testNames: ["APP.Settings.Tests.ConfirmDialogControllerTests"]
```

**期望**：全部通过。若 `ManualConfirm` 失败，按 Step 15.1 备注的备选写法修改。

---

### Task 16：回归——跑现有 `PomodoroPanelPositionTests`

- [ ] **Step 16.1：跑现有番茄钟测试（确认 UnsavedChangesDialogController 改造未破坏回归）**

```
MCP tool: run_tests
testMode: EditMode
testNames: ["APP.Pomodoro.Tests.PomodoroPanelPositionTests"]
```

**期望**：全部通过（现有测试不直接依赖 Dialog 视觉，但改造若意外破坏 OnUnsavedChange 分支会在 PlayMode 手工验证阶段暴露）。

---

### Task 17：Commit Stage D

- [ ] **Step 17.1：Stage & Commit**

```bash
git add Assets/UI_V2/Controller/ConfirmDialogController.cs \
        Assets/UI_V2/Controller/UnsavedChangesDialogController.cs \
        Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs \
        Assets/UI_V2/Documents/ConfirmDialog.uxml \
        Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml \
        Assets/UI_V2/Styles/ConfirmDialog.uss \
        Assets/Tests/EditMode/SettingsTests/Editor/ConfirmDialogControllerTests.cs \
        Assets/Tests/EditMode/SettingsTests/Editor/APP.Settings.Tests.asmdef
# 被 git rm 的文件已在 index 中

git commit -m "$(cat <<'EOF'
feat(settings): 抽出通用 ConfirmDialog 组件 + 控制器

- 新建 ConfirmDialog.uxml/uss: 带可选 dlg-countdown 行的通用对话框
- 新建 ConfirmDialogController: 支持 countdownSeconds 参数 +
  基于 VisualElement.schedule 的 500ms tick 倒计时，到 0 自动 cancel
- UnsavedChangesDialogController 改为 ConfirmDialogController 的薄适配层
  (对外 API 不变，预置"未保存更改"文案 + countdownSeconds=0)
- 删除 UnsavedChangesDialog.uxml/uss (由 ConfirmDialog 取代)
- UnifiedSettingsPanel.uxml 切换 Style 引用
- UnifiedSettingsPanelDriver 字段改名 _confirmDialogTemplate
  (附 FormerlySerializedAs 保留 Inspector 引用)
- ConfirmDialogControllerTests 覆盖 show/tick/manual confirm/texts

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Stage E · Global Settings Panel

### Task 18：`GlobalSettingsPanel.uss` + `.uxml`

**Files:**
- Create: `Assets/UI_V2/Styles/GlobalSettingsPanel.uss`
- Create: `Assets/UI_V2/Documents/GlobalSettingsPanel.uxml`

- [ ] **Step 18.1：创建 uss**

```css
.gsp-root {
    flex-direction: column;
}

.gsp-apply-row {
    flex-direction: row;
    justify-content: flex-end;
    margin-bottom: 16px;
}

.apply-btn {
    border-radius: 999px;
    padding: 7px 12px;
    width: 120px;
    background-color: var(--color-accent-orange);
    color: var(--color-text-white);
    -unity-font-style: bold;
    font-size: 14px;
    border-width: 0;
}
.apply-btn--hidden { display: none; }

.gsp-scale-card {
    padding: 16px;
    background-color: var(--color-bg-surface);
    border-radius: var(--radius-xl);
    flex-direction: column;
}

.gsp-scale-label {
    color: rgb(156, 163, 175);
    font-size: 12px;
    -unity-font-style: bold;
    margin-bottom: 10px;
}

.gsp-scale-row {
    flex-direction: row;
    align-items: center;
}

.gsp-scale-slider {
    flex-grow: 1;
    margin-right: 12px;
}

.gsp-scale-value {
    color: var(--color-text-dark);
    font-size: 14px;
    -unity-font-style: bold;
    min-width: 48px;
    -unity-text-align: middle-right;
}
```

- [ ] **Step 18.2：创建 uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements"
         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         noNamespaceSchemaLocation="../../../UIElementsSchema/UIElements.xsd"
         editor-extension-mode="False">
    <ui:Style src="../Styles/Variables.uss"/>
    <ui:Style src="../Styles/GlobalSettingsPanel.uss"/>

    <ui:VisualElement name="gsp-root" class="gsp-root">
        <!-- Pencil: gspApply — 复用 SettingsApplyRow；按钮 name="apply-btn" -->
        <ui:VisualElement class="gsp-apply-row">
            <ui:Button name="apply-btn" text="应用" class="apply-btn" />
        </ui:VisualElement>

        <!-- Pencil: gspScale — 界面缩放卡片 -->
        <ui:VisualElement class="gsp-scale-card">
            <ui:Label text="界面缩放" class="gsp-scale-label" />
            <ui:VisualElement class="gsp-scale-row">
                <ui:Slider name="gsp-scale-slider" low-value="0.5" high-value="2.0"
                           value="1.0" class="gsp-scale-slider" />
                <ui:Label name="gsp-scale-value" text="1.0×" class="gsp-scale-value" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

---

### Task 19：`GlobalSettingsPanelController`

**Files:**
- Create: `Assets/UI_V2/Controller/GlobalSettingsPanelController.cs`

- [ ] **Step 19.1：创建控制器**

```csharp
using APP.Pomodoro;
using APP.Settings.Command;
using APP.Settings.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 全局设置面板控制器（纯 C# 类）。
    /// 滑块拖动只更新本地 pending，不影响 Model；点击"应用"发 Cmd_SetPreviewUiScale
    /// 让 PanelScaleApplier 写 PanelSettings.scale，同步弹出倒计时对话框。
    /// 用户点"保留" → Cmd_CommitUiScale；点"还原"或 5s 超时 → Cmd_RevertUiScale。
    /// </summary>
    public sealed class GlobalSettingsPanelController : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        public const float CountdownSeconds = 5f;

        private Slider _slider;
        private Label _valueLabel;
        private Button _applyBtn;
        private ConfirmDialogController _scaleDialog;

        private float _pendingScale;

        public bool IsScaleDialogVisible => _scaleDialog?.IsVisible == true;

        public void Init(
            VisualElement root,
            VisualElement dialogHost,
            VisualTreeAsset confirmDialogTemplate,
            GameObject lifecycleOwner)
        {
            var model = this.GetModel<ISettingsModel>();

            _slider     = root.Q<Slider>("gsp-scale-slider");
            _valueLabel = root.Q<Label>("gsp-scale-value");
            _applyBtn   = root.Q<Button>("apply-btn");

            if (_slider != null)
            {
                _slider.lowValue  = SettingsModel.MinScale;
                _slider.highValue = SettingsModel.MaxScale;
            }

            _scaleDialog = new ConfirmDialogController();
            if (dialogHost != null && confirmDialogTemplate != null)
            {
                _scaleDialog.Init(dialogHost, confirmDialogTemplate);
            }

            SyncSliderFromModel(model.UiScale.Value);

            _slider?.RegisterValueChangedCallback(OnSliderChanged);
            if (_applyBtn != null) _applyBtn.clicked += OnApplyClicked;

            model.UiScale.Register(SyncSliderFromModel)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
        }

        /// <summary>进入 global tab 时调用，把滑块刷回 UiScale（丢弃上次未应用的拖动残留）。</summary>
        public void RefreshFromModel()
            => SyncSliderFromModel(this.GetModel<ISettingsModel>().UiScale.Value);

        /// <summary>测试钩子（internal）：直接触发"应用"逻辑，绕过 UI 事件。</summary>
        internal void TriggerApplyForTest() => OnApplyClicked();

        // ─── 内部 ────────────────────────────────────────────────

        private void OnSliderChanged(ChangeEvent<float> evt)
        {
            _pendingScale    = SnapToStep(evt.newValue);
            if (_valueLabel != null) _valueLabel.text = FormatScale(_pendingScale);
        }

        private void OnApplyClicked()
        {
            var model   = this.GetModel<ISettingsModel>();
            var current = model.UiScale.Value;
            var target  = SnapToStep(_pendingScale);

            if (Mathf.Approximately(current, target)) return;
            if (_scaleDialog.IsVisible) return;

            this.SendCommand(new Cmd_SetPreviewUiScale(target));

            _scaleDialog.Show(
                title:       "保留新缩放吗？",
                subtitle:    $"当前 {FormatScale(target)}，原 {FormatScale(current)}",
                body:        "如 5 秒内未保留，将自动还原到原缩放。",
                confirmText: "保留",
                cancelText:  "还原",
                onConfirm:   () => this.SendCommand(new Cmd_CommitUiScale()),
                onCancel:    () => this.SendCommand(new Cmd_RevertUiScale()),
                countdownSeconds: CountdownSeconds);
        }

        private void SyncSliderFromModel(float v)
        {
            _pendingScale = v;
            _slider?.SetValueWithoutNotify(v);
            if (_valueLabel != null) _valueLabel.text = FormatScale(v);
        }

        internal static float  SnapToStep(float v) => Mathf.Round(v * 10f) / 10f;
        internal static string FormatScale(float v) => $"{v:0.0}×";
    }
}
```

- [ ] **Step 19.2：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。

---

### Task 20：`GlobalSettingsPanelControllerTests`

**Files:**
- Create: `Assets/Tests/EditMode/SettingsTests/Editor/GlobalSettingsPanelControllerTests.cs`

- [ ] **Step 20.1：创建测试**

```csharp
using APP.Pomodoro;
using APP.Pomodoro.Controller;
using APP.Settings.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Settings.Tests
{
    public sealed class GlobalSettingsPanelControllerTests
    {
        private VisualElement _panelRoot;
        private VisualElement _dialogHost;
        private VisualTreeAsset _panelTemplate;
        private VisualTreeAsset _dialogTemplate;
        private GlobalSettingsPanelController _ctrl;
        private GameObject _lifecycle;
        private ISettingsModel _model;

        [SetUp]
        public void SetUp()
        {
            _ = GameApp.Interface;
            _model = GameApp.Interface.GetModel<ISettingsModel>();
            _model.UiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
            _model.PreviewUiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);

            #if UNITY_EDITOR
            _panelTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI_V2/Documents/GlobalSettingsPanel.uxml");
            _dialogTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI_V2/Documents/ConfirmDialog.uxml");
            #endif
            Assert.That(_panelTemplate,  Is.Not.Null, "GlobalSettingsPanel.uxml 未找到");
            Assert.That(_dialogTemplate, Is.Not.Null, "ConfirmDialog.uxml 未找到");

            _panelRoot  = _panelTemplate.CloneTree();
            _dialogHost = new VisualElement();
            _lifecycle  = new GameObject("LifecycleOwner");

            _ctrl = new GlobalSettingsPanelController();
            _ctrl.Init(_panelRoot, _dialogHost, _dialogTemplate, _lifecycle);
        }

        [TearDown]
        public void TearDown()
        {
            if (_lifecycle != null) UnityEngine.Object.DestroyImmediate(_lifecycle);
        }

        [Test]
        public void Initial_SliderReflectsUiScale()
        {
            Assert.That(_panelRoot.Q<Slider>("gsp-scale-slider").value,
                Is.EqualTo(SettingsModel.DefaultScale));
            Assert.That(_panelRoot.Q<Label>("gsp-scale-value").text,
                Is.EqualTo("1.0×"));
        }

        [Test]
        public void SliderDrag_DoesNotChangeModel()
        {
            var slider = _panelRoot.Q<Slider>("gsp-scale-slider");
            slider.value = 1.5f;  // 触发 ChangeEvent

            Assert.That(_model.UiScale.Value, Is.EqualTo(1.0f));
            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.0f));
        }

        [Test]
        public void Apply_SameValue_DoesNothing()
        {
            // 滑块保持默认 1.0，直接点 Apply
            _ctrl.TriggerApplyForTest();

            Assert.That(_ctrl.IsScaleDialogVisible, Is.False);
            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.0f));
        }

        [Test]
        public void Apply_DifferentValue_WritesPreviewAndShowsDialog()
        {
            var slider = _panelRoot.Q<Slider>("gsp-scale-slider");
            slider.value = 1.5f;

            _ctrl.TriggerApplyForTest();

            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.5f));
            Assert.That(_model.UiScale.Value, Is.EqualTo(1.0f));
            Assert.That(_ctrl.IsScaleDialogVisible, Is.True);
        }

        [Test]
        public void Apply_DialogAlreadyVisible_DoesNotReenter()
        {
            var slider = _panelRoot.Q<Slider>("gsp-scale-slider");
            slider.value = 1.5f;
            _ctrl.TriggerApplyForTest();

            slider.value = 1.7f;
            _ctrl.TriggerApplyForTest();

            // PreviewUiScale 仍是第一次的 1.5
            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.5f));
        }

        [Test]
        public void RefreshFromModel_ResetsSliderToUiScale()
        {
            var slider = _panelRoot.Q<Slider>("gsp-scale-slider");
            slider.value = 1.8f;  // 未 apply 的残留
            Assert.That(_model.UiScale.Value, Is.EqualTo(1.0f));

            _ctrl.RefreshFromModel();

            Assert.That(slider.value, Is.EqualTo(1.0f));
        }

        [Test]
        public void UiScaleChange_SyncsSlider()
        {
            _model.UiScale.Value = 1.3f;

            Assert.That(_panelRoot.Q<Slider>("gsp-scale-slider").value, Is.EqualTo(1.3f));
            Assert.That(_panelRoot.Q<Label>("gsp-scale-value").text, Is.EqualTo("1.3×"));
        }
    }
}
```

- [ ] **Step 20.2：运行测试**

```
MCP tool: run_tests
testMode: EditMode
testNames: ["APP.Settings.Tests.GlobalSettingsPanelControllerTests"]
```

**期望**：全部通过。若 `Apply_DialogAlreadyVisible_DoesNotReenter` 失败（例如 clicked 重复调用实际没有 IsVisible 保护），回到 `GlobalSettingsPanelController.OnApplyClicked` 检查 `if (_scaleDialog.IsVisible) return;` 是否在 SetValue 之前。

---

### Task 21：Commit Stage E

- [ ] **Step 21.1：Stage & Commit**

```bash
git add Assets/UI_V2/Controller/GlobalSettingsPanelController.cs \
        Assets/UI_V2/Documents/GlobalSettingsPanel.uxml \
        Assets/UI_V2/Styles/GlobalSettingsPanel.uss \
        Assets/Tests/EditMode/SettingsTests/Editor/GlobalSettingsPanelControllerTests.cs

git commit -m "$(cat <<'EOF'
feat(settings): 新增全局设置面板与 UI 缩放滑块控制器

- GlobalSettingsPanel.uxml/uss: 顶部 Apply 按钮 + 界面缩放滑块卡片
- GlobalSettingsPanelController: 滑块拖动只改本地 pending 不改 Model；
  点 Apply 且值变化时发 Cmd_SetPreviewUiScale，同步弹 ConfirmDialog 倒计时；
  onConfirm 发 Cmd_CommitUiScale，onCancel/超时 发 Cmd_RevertUiScale；
  对话框可见时 Apply 重入被阻止；UiScale 变化自动刷新滑块
- GlobalSettingsPanelControllerTests 覆盖初值/拖动/Apply 分支/重入/刷新

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Stage F · Unified 接入 + 侧栏 + apply btn 统一

### Task 22：`UnifiedSettingsPanel.uxml` 扩展

**Files:**
- Modify: `Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml`

- [ ] **Step 22.1：顶部 Style 列表加入全局面板样式**

在现有 `<Style src="../Styles/PetSettingsPanel.uss"/>` 之后加：

```xml
<Style src="../Styles/GlobalSettingsPanel.uss"/>
```

（ConfirmDialog.uss 已在 Task 13 中加入）

- [ ] **Step 22.2：侧栏追加 `tab-global`**

在现有 `<ui:VisualElement name="tab-pet" ...>...</ui:VisualElement>` 之后加：

```xml
<ui:VisualElement name="tab-global" class="sidebar-tab">
    <ui:Label text="全局" class="sidebar-tab-label" />
</ui:VisualElement>
```

- [ ] **Step 22.3：overlay 底部追加 `scale-dialog-host`**

在现有 `<ui:VisualElement name="unsaved-dialog-host" ... />` 之后加：

```xml
<ui:VisualElement name="scale-dialog-host" class="unsaved-dialog-host" picking-mode="Ignore" />
```

> 复用 `.unsaved-dialog-host` 的样式（absolute 全覆盖 overlay，默认 picking-mode=Ignore）—— ConfirmDialogController 显示时 `.dlg-root` 自己的 `picking-mode=Position` 接管交互。

---

### Task 23：`UnifiedSettingsPanelController` 加 global tab 分支

**Files:**
- Modify: `Assets/UI_V2/Controller/UnifiedSettingsPanelController.cs`

- [ ] **Step 23.1：新增字段**

在现有字段声明区（`_petSettings` 之类附近）加：

```csharp
private VisualElement _tabGlobal;
private VisualElement _scaleDialogHost;

private GlobalSettingsPanelController _globalSettings;
private VisualTreeAsset _globalTemplate;
private VisualTreeAsset _confirmDialogTemplate;  // 接收 ConfirmDialog.uxml 模板
private VisualElement _globalRoot;
```

（原有 `_unsavedDialogHost` 保留不动，它承载"未保存更改"对话框实例。）

- [ ] **Step 23.2：`Init` 方法签名扩展**

把 `Init` 的签名改为：

```csharp
public void Init(
    VisualElement root,
    IPomodoroModel model,
    IRoomModel roomModel,
    VisualTreeAsset pomodoroTemplate,
    VisualTreeAsset onlineTemplate,
    VisualTreeAsset petTemplate,
    VisualTreeAsset globalTemplate,              // ← 新增
    VisualTreeAsset confirmDialogTemplate,       // ← 原 unsavedDialogTemplate
    GameObject lifecycleOwner)
```

方法体补充：

```csharp
_globalTemplate        = globalTemplate;
_confirmDialogTemplate = confirmDialogTemplate;

_tabGlobal       = root.Q("tab-global");
_scaleDialogHost = root.Q("scale-dialog-host");
```

并替换现有对 unsaved 对话框的初始化行（`unsavedDialogTemplate` → `confirmDialogTemplate`）：

```csharp
_unsavedDialog = new UnsavedChangesDialogController();
if (_unsavedDialogHost != null && confirmDialogTemplate != null)
{
    _unsavedDialog.Init(_unsavedDialogHost, confirmDialogTemplate);
}
```

在 tab 点击注册处加一行：

```csharp
_tabGlobal?.RegisterCallback<PointerUpEvent>(_ => SelectTab("global"));
```

在现有 `_tabPet?.RegisterCallback<PointerDownEvent>(...)` 附近加：

```csharp
_tabGlobal?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
```

- [ ] **Step 23.3：`DoSelectTab` 新增 class 切换 + "global" 刷新**

找 `DoSelectTab`：

```csharp
_tabPomodoro?.EnableInClassList("sidebar-tab--active", tabName == "pomodoro");
_tabOnline?.EnableInClassList("sidebar-tab--active", tabName == "online");
_tabPet?.EnableInClassList("sidebar-tab--active", tabName == "pet");
```

后面追加：

```csharp
_tabGlobal?.EnableInClassList("sidebar-tab--active", tabName == "global");
```

`switch (tabName)` 的 refresh 分支追加：

```csharp
case "global":
    _globalSettings?.RefreshFromModel();
    break;
```

- [ ] **Step 23.4：`EnsureTabContent` 加 `"global"` 分支**

```csharp
case "global":
    if (_globalRoot == null)
    {
        _globalRoot = CloneTemplate(_globalTemplate);
        _globalSettings = new GlobalSettingsPanelController();
        _globalSettings.Init(
            _globalRoot,
            _scaleDialogHost,
            _confirmDialogTemplate,
            _lifecycleOwner);
    }
    return _globalRoot;
```

- [ ] **Step 23.5：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error（Driver 还没更新签名，可能报 CS1501 参数数量不匹配——下一任务修）。

---

### Task 24：`UnifiedSettingsPanelDriver` 新增 `_globalSettingsTemplate` + 传参

**Files:**
- Modify: `Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs`

- [ ] **Step 24.1：增加序列化字段**

在 `_petSettingsTemplate` 声明后加：

```csharp
[SerializeField] private VisualTreeAsset _globalSettingsTemplate;
```

- [ ] **Step 24.2：`Init` 调用更新**

```csharp
_controller.Init(
    _root,
    pomodoroModel,
    roomModel,
    _pomodoroSettingsTemplate,
    _onlineSettingsTemplate,
    _petSettingsTemplate,
    _globalSettingsTemplate,     // ← 新增
    _confirmDialogTemplate,      // 已在 Task 14 改名
    gameObject);
```

- [ ] **Step 24.3：Inspector 手工绑定**

用户手工：
1. 选中 `UnifiedSettingsPanel` GameObject
2. `UnifiedSettingsPanelDriver` 组件的 `_globalSettingsTemplate` 字段拖入 `Assets/UI_V2/Documents/GlobalSettingsPanel.uxml`
3. 确认 `_confirmDialogTemplate` 已指向 `Assets/UI_V2/Documents/ConfirmDialog.uxml`（Task 14.2）
4. 保存场景

- [ ] **Step 24.4：编译检查**

```
MCP tool: read_console (filter: error)
```

**期望**：无 error。

---

### Task 25：番茄钟 Apply 按钮 name 统一

**Files:**
- Modify: `Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml`
- Modify: `Assets/UI_V2/Controller/PomodoroSettingsPanelView.cs`
- Modify: `Assets/UI_V2/Styles/PomodoroSettingsPanel.uss`

- [ ] **Step 25.1：UXML 改 name**

文件：`Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml`

把

```xml
<ui:Button name="psp-apply-btn" text="应用" class="psp-apply-btn psp-apply-btn--hidden" />
```

替换为

```xml
<ui:Button name="apply-btn" text="应用" class="apply-btn apply-btn--hidden" />
```

- [ ] **Step 25.2：View 改查询节点 + class 名**

文件：`Assets/UI_V2/Controller/PomodoroSettingsPanelView.cs`

- 第 33 行注释改：`// name="apply-btn"`
- 第 57 行：`_applyBtn = panelRoot.Q<Button>("apply-btn");`
- 第 100 行：`_applyBtn.EnableInClassList("apply-btn--hidden", !visible);`

完整 diff（核心 3 行）：

```csharp
// 原
private readonly Button _applyBtn;       // name="psp-apply-btn"
// 改
private readonly Button _applyBtn;       // name="apply-btn"

// 原
_applyBtn     = panelRoot.Q<Button>("psp-apply-btn");
// 改
_applyBtn     = panelRoot.Q<Button>("apply-btn");

// 原
_applyBtn.EnableInClassList("psp-apply-btn--hidden", !visible);
// 改
_applyBtn.EnableInClassList("apply-btn--hidden", !visible);
```

- [ ] **Step 25.3：USS 改 class 名**

文件：`Assets/UI_V2/Styles/PomodoroSettingsPanel.uss`

找 `.psp-apply-btn` 与 `.psp-apply-btn--hidden` 两条规则，批量改为 `.apply-btn` 与 `.apply-btn--hidden`。

> ⚠️ 若 `GlobalSettingsPanel.uss` 已定义同名 `.apply-btn` 规则，两处会合并。当前 `GlobalSettingsPanel.uss` 定义的是按钮视觉（橙底白字 + 圆角）；番茄钟原 `.psp-apply-btn` 视觉若一致可直接删除番茄钟这条，不一致则留在 `PomodoroSettingsPanel.uss`（按 UXML 挂载顺序覆盖）。

实操：用 `grep -n "psp-apply-btn" Assets/UI_V2/Styles/PomodoroSettingsPanel.uss` 列出行号，逐条 sed 或 Edit。

- [ ] **Step 25.4：编译 + 跑番茄钟测试**

```
MCP tool: read_console (filter: error)
```

```
MCP tool: run_tests
testMode: EditMode
testNames: ["APP.Pomodoro.Tests.PomodoroPanelPositionTests"]
```

**期望**：全部通过。

---

### Task 26：Commit Stage F

- [ ] **Step 26.1：Stage & Commit**

```bash
git add Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml \
        Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml \
        Assets/UI_V2/Controller/UnifiedSettingsPanelController.cs \
        Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs \
        Assets/UI_V2/Controller/PomodoroSettingsPanelView.cs \
        Assets/UI_V2/Styles/PomodoroSettingsPanel.uss \
        Assets/Scenes/  # 场景文件变更（Inspector 绑定的 _globalSettingsTemplate）

git commit -m "$(cat <<'EOF'
feat(settings): 接入全局设置 tab 到统一设置面板

- UnifiedSettingsPanel.uxml: 侧栏追加 tab-global、overlay 追加 scale-dialog-host、
  Style 列表加 GlobalSettingsPanel.uss
- UnifiedSettingsPanelController.Init 签名扩展: 新增 globalTemplate +
  confirmDialogTemplate 参数；SelectTab/DoSelectTab/EnsureTabContent 加
  "global" 分支；DoSelectTab 在 "global" 分支调用 RefreshFromModel
- UnifiedSettingsPanelDriver: 新增 _globalSettingsTemplate 序列化字段，
  Init 调用扩展；Inspector 需重新绑定 ConfirmDialog.uxml 与 GlobalSettingsPanel.uxml
- 番茄钟 Apply 按钮 name 从 psp-apply-btn 统一为 apply-btn
  (UXML + View.cs + PomodoroSettingsPanel.uss 同步改名)，与
  GlobalSettingsPanel 共用 SettingsApplyRow 组件约定

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Stage G · 整体验收

### Task 27：跑所有 EditMode 测试

- [ ] **Step 27.1：执行**

```
MCP tool: run_tests
testMode: EditMode
```

轮询 `get_test_job`。

**期望**：所有现有测试 + 本计划新增测试全绿。

若某个测试失败：
- 先 `read_console` 看失败堆栈
- 检查是否 Inspector 绑定遗漏（Scene 里某个 SerializeField 为 null）
- 必要时回到对应 Stage 的 Step 修正

- [ ] **Step 27.2：`validate_script` 静态检查关键文件**

```
MCP tool: validate_script
level: standard
paths: [
  "Assets/Scripts/APP/Settings/Model/SettingsModel.cs",
  "Assets/Scripts/APP/Settings/Command/Cmd_SetPreviewUiScale.cs",
  "Assets/Scripts/APP/Settings/Command/Cmd_CommitUiScale.cs",
  "Assets/Scripts/APP/Settings/Command/Cmd_RevertUiScale.cs",
  "Assets/UI_V2/Controller/ConfirmDialogController.cs",
  "Assets/UI_V2/Controller/GlobalSettingsPanelController.cs",
  "Assets/UI_V2/Controller/PanelScaleApplier.cs"
]
```

**期望**：无严重告警。

---

### Task 28：PlayMode 手工验证清单

- [ ] **Step 28.1：按清单验证**

进入 PlayMode（或真实启动构建后），依次操作并勾选：

- [ ] 打开设置 → 点"全局" tab → 滑块值 `1.0`，右侧 label `"1.0×"`
- [ ] 拖动滑块到 `1.5` → 屏幕 UI **不变化**，label 显示 `"1.5×"`
- [ ] 点"应用" → 屏幕 UI 立即放大到 `1.5×`；弹窗："保留新缩放吗？"、"剩余 5s 后自动还原"；按钮 `[还原] [保留]`
- [ ] 5 秒不动 → 倒计时数字每秒跳 `5→4→3→2→1→0` → 对话框关闭 → 屏幕回到 `1.0×`
- [ ] 再试：应用 `1.5×` → 点"保留" → 对话框关闭，屏幕保持 `1.5×`
- [ ] 重启 Unity → 进入 PlayMode → 缩放仍是 `1.5×`（说明持久化生效）
- [ ] 再试：应用 `2.0×` → 点"还原" → 屏幕立即回 `1.5×`，对话框关闭
- [ ] 倒计时期间点侧栏 tab / X 关闭 / 滑块 → 无反应（backdrop 吃输入）
- [ ] 番茄钟设置 tab 修改时长 → 切换 tab → 弹出"未保存更改"对话框（回归）
- [ ] 桌面窗 + 番茄钟窗同步缩放（改 scale 后观察番茄钟面板也跟着缩放）

- [ ] **Step 28.2：若有失败项**

根据失败表现回到对应 Stage：
- 屏幕不变 → `PanelScaleApplier._panelSettings` Inspector 绑定检查
- 弹窗不出 → `UnifiedSettingsPanelDriver._confirmDialogTemplate` 绑定检查
- 持久化失效 → `SettingsModel.OnInit` 的 `UiScale.Register` 注册时机
- 番茄钟回归破坏 → `UnsavedChangesDialogController` 薄适配签名

- [ ] **Step 28.3：若有新改动则 Commit 修复**

```bash
git add <modified files>
git commit -m "fix(settings): <具体问题>

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## 完成标志

✅ `run_tests` EditMode 全绿
✅ `read_console` 无 error/warning
✅ PlayMode 手工清单全 10 项勾选通过
✅ 6 个 commit 干净落盘（Pencil / Model+Cmd / PanelScaleApplier / ConfirmDialog / GlobalPanel / Wiring）
