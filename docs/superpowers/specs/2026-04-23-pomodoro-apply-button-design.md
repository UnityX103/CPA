# 番茄钟设置面板：隐藏启用开关 + 引入"应用"按钮

**日期**：2026-04-23
**分支**：`feat/desk-window-refactor`
**涉及层**：UI（UXML/USS）+ UI Controller（C#）+ 统一设置面板

---

## 背景与动机

当前 `PomodoroSettingsPanel` 的"专注时长 / 休息时长"通过 TextField 的
Blur/Enter 即时触发 `Cmd_PomodoroApplySettings`，但该 Command 只写
`FocusDurationSeconds`/`BreakDurationSeconds`，**不会**更新正在倒计时的
`RemainingSeconds`。结果用户必须"关掉番茄钟再重新打开"（触发
`ResetCycle`）才能看到新时长生效。

此外面板顶端的"启用番茄钟"开关在产品上永远开启、代码里 `OnEnabledToggleChanged`
为空实现、`RefreshFromModel` 也硬编码 `isEnabled = true`——已是无用元素。

## 目标

1. **隐藏并移除启用开关**：面板中不再呈现，模型始终视为已启用。
2. **引入"应用"按钮**：占用原开关行的视觉位，默认隐藏；当草稿值（focus / break / hint）与 Model 不一致时浮出。
3. **点击"应用"立即重置**：发送 `Cmd_PomodoroApplySettings(..., resetProgress: true)` + `Cmd_PomodoroApplyMetaSettings(...)`，让正在运行的计时器立刻按新时长重启。
4. **未保存拦截**：在统一设置面板执行关闭或切 tab 前，若番茄钟面板有未保存草稿，弹出确认对话框"保存并继续 / 取消"。

## 非目标

- 不动结束提示音（`psp-sound-label` 保持静态 Label，未来加选择器时自然纳入草稿体系）。
- 不在 Model 中引入任何"草稿"字段——草稿状态仅由 Controller 私有持有。
- 不增加"丢弃更改"入口（取消 = 留在面板；保存 = 提交并继续）。

## 范围与影响文件

| 类别 | 文件 | 变更 |
|---|---|---|
| 设计 | `AUI/PUI.pen` | 已改：`gs1Tv` 顶行替换为 `Button/Primary "应用"`；新增顶层 `Unsaved Changes Dialog`（id `ikREg`） |
| UXML | `Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml` | 删除启用开关 `.psp-row`，改为 `<ui:Button name="psp-apply-btn">应用</ui:Button>` 置顶 |
| UXML 新增 | `Assets/UI_V2/Documents/UnsavedChangesDialog.uxml` | 新对话框模板（标题 + 副标题 + 关闭按钮 + 正文 + 取消/保存并继续） |
| USS | `Assets/UI_V2/Styles/PomodoroSettingsPanel.uss` | 新增 `.psp-apply-btn` / `.psp-apply-btn--hidden`；删除 `.psp-row` 启用行相关非通用规则 |
| USS 新增 | `Assets/UI_V2/Styles/UnsavedChangesDialog.uss` | 对话框样式（遮罩 + 卡片 + 阴影 + 按钮行） |
| Controller | `Assets/UI_V2/Controller/PomodoroSettingsPanelView.cs` | 去掉 `psp-toggle` 绑定；新增 `ApplyButton` 引用、`SetApplyVisible(bool)`、`ForceCommitDrafts()`；保留 `OnHintToggleChanged` 但改为仅更新草稿显隐（不发 Command） |
| Controller | `Assets/UI_V2/Controller/PomodoroSettingsPanelController.cs` | 引入私有 `_draftFocusMin/_draftBreakMin/_draftHint`；Commit / Toggle 回调仅更新草稿并 `EvaluateDirty`；新增 `IsDirty`、`TryApply()`、`DiscardDrafts()`、`ForceCommitPendingEdits()` 公开方法 |
| Controller | `Assets/UI_V2/Controller/UnifiedSettingsPanelController.cs` | `Hide()` 与 `SelectTab()` 前调用 `_pomodoroSettings.ForceCommitPendingEdits()` + `IsDirty`；真为 dirty 则挂起动作、展示 `UnsavedChangesDialog`；关闭/取消/保存分别处理 |
| Controller 新增 | `Assets/UI_V2/Controller/UnsavedChangesDialogController.cs` | 纯 C# 类；Show(container, title, body, onConfirm) / Hide()；仅持有视图，不与 QFramework 通信 |
| Command | `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroApplySettings.cs` | 无改动（已支持 `resetProgress`） |
| 测试 | `Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroSettingsPanelPersistenceTests.cs` | 适配：Commit 不再立刻写 Model，需显式 `TryApply()`；新增 Dirty/Apply/Discard/ForceCommit 测试 |
| 测试新增 | `Assets/Tests/EditMode/PlayerCardTests/Editor/UnifiedSettingsPanelDirtyGuardTests.cs` | 验证 Hide/SelectTab 在 dirty 时暂停并显示对话框；确认后原动作执行；取消后面板留住 |

---

## 设计细节

### 1. 草稿状态机（Controller 私有）

Controller 内部维护三项草稿与 Model 同步值：

```csharp
// 草稿（用户当前输入/拨动的值）
private int  _draftFocusMin;
private int  _draftBreakMin;
private bool _draftHint;

// 基线（最近一次 Refresh/Apply 时的 Model 值）
private int  _baseFocusMin;
private int  _baseBreakMin;
private bool _baseHint;
```

`RefreshFromModel` 同时把 Model 值写入 draft 与 base，并隐藏 Apply 按钮。

`OnFocusMinutesChanged` / `OnBreakMinutesChanged` / `OnHintToggleChanged` 回调
仅更新 `_draftX`，调用 `EvaluateDirty()`：

```csharp
bool dirty = _draftFocusMin != _baseFocusMin
          || _draftBreakMin != _baseBreakMin
          || _draftHint     != _baseHint;
_view.SetApplyVisible(dirty);
```

`IsDirty` 属性公开给 `UnifiedSettingsPanelController`。

### 2. 应用按钮回调

```csharp
public void TryApply()
{
    if (!IsDirty) return;

    this.SendCommand(new Cmd_PomodoroApplySettings(
        _draftFocusMin, _draftBreakMin, _model.TotalRounds.Value,
        resetProgress: true));

    this.SendCommand(new Cmd_PomodoroApplyMetaSettings(
        _draftHint, _model.AutoStartBreak.Value, _model.CompletionClipIndex.Value));

    _baseFocusMin = _draftFocusMin;
    _baseBreakMin = _draftBreakMin;
    _baseHint     = _draftHint;
    _view.SetApplyVisible(false);
}
```

`resetProgress: true` 让 `PomodoroTimerSystem.ApplySettings` 调用
`ResetCycle()`（需额外把 `RemainingSeconds` 重置为新 focus 时长——
这部分在现有 `ResetCycle` 实现里验证是否已覆盖；若没有，在实施阶段补）。

### 3. 强制 Commit 未失焦的 TextField

`PomodoroSettingsPanelView.ForceCommitDrafts()` 顺序调用
`CommitFocusValue()` + `CommitBreakValue()`，即便 TextField 当前持有焦点也
能同步 Draft。`Controller.ForceCommitPendingEdits()` 包装这一步，供统一设置面板
在关闭/切 tab 前调用。

### 4. 未保存拦截流程

```csharp
// UnifiedSettingsPanelController
private void TryExecuteNavigation(Action pendingAction)
{
    _pomodoroSettings?.ForceCommitPendingEdits();
    if (_pomodoroSettings?.IsDirty == true)
    {
        _unsavedDialog.Show(
            onConfirm: () => { _pomodoroSettings.TryApply(); pendingAction(); },
            onCancel:  () => { /* 什么都不做，留在当前面板 */ });
        return;
    }
    pendingAction();
}

public void Hide()       => TryExecuteNavigation(DoHide);
private void SelectTab(string t) => TryExecuteNavigation(() => DoSelectTab(t));
```

`_unsavedDialog` 的容器放在 `settings-overlay` 内的一个绝对定位子节点
`unsaved-dialog-host`，只有 `Show` 时 `display: Flex`。

### 5. 对话框视觉与交互

与 Pencil `Unsaved Changes Dialog` 一致：

- 容器：白底 20px 圆角，阴影，居中于 overlay 上方。
- 头部横排：左标题"有未保存的更改"(16/700) + 副标题"请先应用或取消后再继续"(12/500)；右 `Button/Close`。
- 正文：`你修改了番茄钟设置但尚未应用。离开此面板将丢失这些改动，是否先保存并继续？`
- 底部右对齐按钮行：`Button/Secondary "取消"` + `Button/Primary "保存并继续"`。
- 关闭 X 等同于"取消"。

### 6. 图层与显隐

- Apply 按钮通过 USS `display: none`（未 dirty）/`display: flex`（dirty）控制。
- 对话框 host 默认 `display: none`。
- USS 动画（opacity 100ms 过渡）可后加，首版不做。

---

## 层级通信合规性

严格遵循 QFramework 四层规则：

- Controller 只通过 `SendCommand` 修改 Model（`Cmd_PomodoroApplySettings`、
  `Cmd_PomodoroApplyMetaSettings`）。
- 草稿状态属于 UI 临时态，不污染 Model。
- `UnsavedChangesDialogController` 无状态（纯视图）不走 Architecture。
- `UnifiedSettingsPanelController` 之前就是纯 C# 类，非 IController，不变。

## 测试计划

EditMode（沿用 `BuildPanel` 辅助）：

- `OnFocusChange_MutatesDraftOnly_DoesNotWriteModel`
- `OnFocusChange_DirtyMakesApplyButtonVisible`
- `TryApply_DirtyFocus_SendsCommandAndClearsDirty`
- `TryApply_AutoJumpChange_SendsMetaCommand`
- `DiscardDrafts_RestoresViewAndHidesApply`（未来若加"丢弃"入口保留钩子；首版只断言 `RefreshFromModel` 可起同样效果）
- `ForceCommitPendingEdits_CommitsUnfocusedFieldValues`
- `UnifiedSettingsPanel_Hide_Dirty_ShowsDialog`
- `UnifiedSettingsPanel_Hide_DialogConfirm_AppliesAndHides`
- `UnifiedSettingsPanel_Hide_DialogCancel_StaysOnPanel`
- `UnifiedSettingsPanel_SelectTab_Dirty_SameFlow`

PlayMode：运行 `UnifiedSettingsPanelImageValidationTests` 的 dirty/clean/dialog 三个视觉 baseline。

## 回滚策略

三个 Git commit：
1. UXML/USS + Pencil（视觉层）
2. Controller 草稿态 + Apply 按钮
3. 未保存拦截 + 对话框

任何一层发现问题可回滚单个 commit，不影响其它层。

## 未解决的问题（实施时再定）

- `PomodoroTimerSystem.ApplySettings(resetProgress: true)` 是否已把
  `RemainingSeconds` 重置到新 focus 时长？若否，需扩展 `ResetCycle` 或
  Command 逻辑，保证"应用"后用户立刻在时钟上看到新时长。
- 对话框是否需要点击遮罩关闭？默认不支持（强制明确选择）。
- 键盘快捷键（Enter=保存并继续，Esc=取消）——首版不做。
