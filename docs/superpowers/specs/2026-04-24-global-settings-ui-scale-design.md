# 全局设置面板 · UI 缩放倍率 设计 Spec

- **日期**：2026-04-24
- **入口**：`Unified Settings Panel`（Pencil node `vnYnS`）侧栏新增"全局"tab
- **功能**：新增"全局设置"子面板，提供 UI 缩放倍率滑块；支持"预览-倒计时-确认/回滚"模型；复用出通用确认对话框组件与通用应用按钮组件。

---

## 1. 目标与非目标

### 目标

1. 在统一设置面板侧栏新增第四个 tab **"全局"**，内容面板提供**界面缩放**滑块（`0.5× ~ 2.0×`，步长 `0.1`，默认 `1.0×`）。
2. **拖动滑块不改变任何实际显示**；点击面板顶部"应用"按钮才真正把缩放写入两个 `PanelSettings` asset 的 `Scale Mode Parameters → Scale` 字段。
3. 应用瞬间弹出**居中倒计时对话框**："保留新缩放吗？剩余 N 秒后自动还原" + `[还原]` `[保留]`。**5 秒内未响应自动还原**；用户点"保留"则持久化到 `PlayerPrefs`。
4. 倒计时对话框显示期间，设置面板内所有输入一律不响应（modal 遮罩）。
5. 把现有"未保存更改"对话框（Pencil `ikREg`）和番茄钟面板顶部的"应用"按钮抽成**可复用的 Pencil 组件**和对应的**可复用 C# Controller**，两处共用。

### 非目标

- 缩放不影响宠物渲染、桌面坐标或任何非 UIDocument 元素（只改 `PanelSettings.scale`）。
- 不引入自定义 Slider 组件；使用 UI Toolkit 自带 `<ui:Slider>`。
- 不做迁移脚本处理旧存档——`PlayerPrefs` 如无历史值按默认 `1.0×`。

---

## 2. 现有结构快照

- Pencil `vnYnS`：水平 body = 侧栏 `qvEam`（三个 tab：番茄钟/联机/宠物）+ 内容区 `2RdBk`。
- 侧栏 tab 激活样式：active 时 `fill:#FFFFFF` + label `#D15F3D/700`；inactive 无 fill + label `#9E8E80/500`。
- 番茄钟面板 `gs1Tv` 顶部 `PBXgQ` 是 "应用" 按钮行（ref `Za5wE` Button/Primary，label "应用"）。
- 对话框 `ikREg` 是 `reusable:true` 的"未保存更改"组件，白卡 + 标题/副标题 + 正文 + `[取消]` `[保存并继续]`。
- 项目 `PanelSettings` asset 有两个：`Assets/UI_V2/PanelSettings_Settings.asset`、`Assets/UI_V2/PomodoroPanelSettings.asset`。两者 `m_ScaleMode: 0`（ConstantPixelSize），`m_Scale: 1`。

---

## 3. 架构总览 & 文件清单

### QFramework 分层

| 层 | 职责 | 新增/修改 |
|---|---|---|
| Model | `ISettingsModel.PreviewUiScale` / `UiScale`（两个 `BindableProperty<float>`） | **新增** `Assets/Scripts/APP/Settings/Model/ISettingsModel.cs`、`SettingsModel.cs`；**修改** `GameApp.cs` 注册 |
| Command | 统一写入入口 | **新增** `Assets/Scripts/APP/Settings/Command/SetPreviewUiScaleCommand.cs`、`CommitUiScaleCommand.cs`、`RevertUiScaleCommand.cs` |
| Controller | 全局设置面板 UI + `PanelSettings` 同步器 | **新增** `Assets/UI_V2/Controller/GlobalSettingsPanelController.cs`、`PanelScaleApplier.cs`（MonoBehaviour） |
| Dialog | 通用确认对话框 | **新增** `Assets/UI_V2/Controller/ConfirmDialogController.cs`；**改造** `UnsavedChangesDialogController.cs` 为薄适配层（对外 API 不变） |

### Pencil 新增 3 个 reusable

1. `ConfirmDialog`：通用确认对话框（含可选 countdown 行）。`ikREg` 改为它的一个实例。
2. `SettingsApplyRow`：顶部靠右 Primary 按钮行。番茄钟面板 `PBXgQ` 改为它的实例，新全局面板顶部也挂它。
3. `Global Settings Panel`：新的子面板本体。

### UXML / USS 新增 / 修改

- **新增**：`Assets/UI_V2/Documents/GlobalSettingsPanel.uxml` + `Assets/UI_V2/Styles/GlobalSettingsPanel.uss`
- **新增**：`Assets/UI_V2/Documents/ConfirmDialog.uxml` + `Assets/UI_V2/Styles/ConfirmDialog.uss`
- **删除**：`Assets/UI_V2/Documents/UnsavedChangesDialog.uxml` + `Assets/UI_V2/Styles/UnsavedChangesDialog.uss`
- **修改**：`Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml`（侧栏新增 `tab-global`、overlay 新增 `scale-dialog-host`、Style 列表删 `UnsavedChangesDialog.uss`、加 `ConfirmDialog.uss` + `GlobalSettingsPanel.uss`）
- **修改**：`UnifiedSettingsPanelController.cs` 新增 global tab 分支与 scale dialog host 转发；`UnifiedSettingsPanelDriver.cs` 新增 `_globalTemplate` 与 `_confirmDialogTemplate` 序列化字段
- **修改**：`PomodoroSettingsPanelController.cs` 里应用按钮 `name` 对齐新 `SettingsApplyRow` 约定

---

## 4. Pencil 组件设计

### 4.1 `ConfirmDialog`（新 reusable）

**Pencil 组件本身 = 白色卡片**（等价于现有 `ikREg` 结构，追加 countdown 行）：

```
ConfirmDialog (frame, reusable, width:420, padding:24, gap:16, cornerRadius:20, fill:#FFFFFF,
               effect:{blur:32, color:#00000033, offset:{x:0,y:8}, shadowType:outer})
├─ dlg-header (row, justifyContent:space_between)
│   ├─ dlg-title-wrap (col, gap:4)
│   │   ├─ dlg-title     (label, #1A1A1A / 16 / 700)
│   │   └─ dlg-subtitle  (label, #6B7280 / 12 / 500)
│   └─ dlg-close (ref:UAbZg)
├─ dlg-countdown (row, justifyContent:center, display:none 默认)
│   └─ dlg-countdown-text (label, #D97706 / 13 / 700)
├─ dlg-body (label, #4B5563 / 14 / 500, lineHeight:1.5, textGrowth:fixed-width, width:fill_container)
└─ dlg-actions (row, justifyContent:end, gap:12)
    ├─ dlg-cancel  (ref:RPLJq, label 从 overrides 注入)
    └─ dlg-confirm (ref:Za5wE, label 从 overrides 注入)
```

`ikREg` 替换为本组件的实例（overrides：title="有未保存的更改"、subtitle="请先应用或取消后再继续"、body=原文、confirm="保存并继续"、cancel="取消"、countdown 行保持隐藏）。

**UXML 外层 backdrop 结构**（与现有 `UnsavedChangesDialog.uxml` 一致）：

```xml
<ui:VisualElement name="dlg-root" class="dlg-root" style="display: none;">
    <!-- 这里是 Pencil 白卡片导出的内容：dlg-header / dlg-countdown / dlg-body / dlg-actions -->
    <ui:VisualElement class="dlg-card">
        ...
    </ui:VisualElement>
</ui:VisualElement>
```

- `name="dlg-root"` 是 **backdrop**（Pencil 里没有对应节点，仅在 UXML 中手写）；CSS 必须 `position:absolute; inset:0;` + 半透明背景 + `picking-mode=Position`，才能在 `display:flex` 时吃掉下层面板所有输入。
- Controller 用 `host.Q<VisualElement>("dlg-root")` 拿到 backdrop，切换 `display` 来显隐。
- 卡片内部子节点的 `name`（`dlg-title` / `dlg-subtitle` / `dlg-body` / `dlg-countdown` / `dlg-countdown-text` / `dlg-close` / `dlg-confirm` / `dlg-cancel`）必须与 Pencil 组件内节点 name 对应，才能被 Controller 查询到。

### 4.2 `SettingsApplyRow`（新 reusable）

```
SettingsApplyRow (frame, reusable, width:fill_container, justifyContent:end, padding:0)
└─ apply-btn (ref:Za5wE, width:120, label "应用")
```

番茄钟 `gs1Tv` 的 `PBXgQ` 改为本组件的实例。新的 `Global Settings Panel` 顶部也挂一个。代码侧统一按 `name="apply-btn"` 查询。

### 4.3 `Global Settings Panel`（新 reusable）

```
Global Settings Panel (frame, reusable, fill:#FFFFFF00, gap:16, layout:vertical, width:fill_container(572))
├─ gspApply (ref:SettingsApplyRow)
└─ gspScale (card, cornerRadius:16, fill:#F6F7F8, padding:16, gap:10, layout:vertical)
    ├─ gsp-scale-label (label "界面缩放", #9CA3AF / 12 / 600)
    └─ gsp-scale-row (row, alignItems:center, gap:12)
        ├─ gsp-scale-slider (Slider, low:0.5, high:2.0, step:0.1, value:1.0, flex:1)
        └─ gsp-scale-value  (label "1.0×", #1A1A1A / 14 / 700, minWidth:48, textAlign:right)
```

### 4.4 `vnYnS` 侧栏新增第四个 tab

在 `Cz9E3 tab-pet` 之后追加：

```
tab-global (frame, padding:[10,14], width:fill_container, fill:none)
└─ label "全局" (#9E8E80 / 13 / 500)
```

激活样式切换完全沿用现有三个 tab 的规则（active → `fill:#FFFFFF` + label `#D15F3D/700`）。侧栏 `qvEam` 高度保持 `318`（实测四个 tab 占用 152，剩余 150 余量）。

---

## 5. 数据层

### 5.1 `ISettingsModel`

```csharp
namespace APP.Settings.Model
{
    public interface ISettingsModel : IModel
    {
        BindableProperty<float> PreviewUiScale { get; }  // 不持久化；订阅者写入 PanelSettings
        BindableProperty<float> UiScale { get; }         // 已保留的正式值；自动持久化
    }
}
```

### 5.2 `SettingsModel`

```csharp
public sealed class SettingsModel : AbstractModel, ISettingsModel
{
    public const float MinScale     = 0.5f;
    public const float MaxScale     = 2.0f;
    public const float DefaultScale = 1.0f;
    private const string UiScaleKey = "Settings.UiScale";

    public BindableProperty<float> PreviewUiScale { get; } = new BindableProperty<float>(DefaultScale);
    public BindableProperty<float> UiScale { get; }        = new BindableProperty<float>(DefaultScale);

    protected override void OnInit()
    {
        var storage = this.GetUtility<IStorageUtility>();
        var loaded  = Mathf.Clamp(storage.Load(UiScaleKey, DefaultScale), MinScale, MaxScale);

        UiScale.SetValueWithoutEvent(loaded);
        PreviewUiScale.SetValueWithoutEvent(loaded);

        UiScale.Register(v => storage.Save(UiScaleKey, v));  // 只有正式值持久化
    }
}
```

### 5.3 Commands

```csharp
// 滑块值 → 预览（触发 PanelScaleApplier 写 PanelSettings.scale）
public sealed class SetPreviewUiScaleCommand : AbstractCommand
{
    private readonly float _scale;
    public SetPreviewUiScaleCommand(float scale) => _scale = scale;
    protected override void OnExecute() =>
        this.GetModel<ISettingsModel>().PreviewUiScale.Value =
            Mathf.Clamp(_scale, SettingsModel.MinScale, SettingsModel.MaxScale);
}

// 点"保留"：预览 → 正式 → 持久化
public sealed class CommitUiScaleCommand : AbstractCommand
{
    protected override void OnExecute()
    {
        var m = this.GetModel<ISettingsModel>();
        m.UiScale.Value = m.PreviewUiScale.Value;
    }
}

// 点"还原" / 倒计时超时：正式 → 预览（PanelSettings 回滚）
public sealed class RevertUiScaleCommand : AbstractCommand
{
    protected override void OnExecute()
    {
        var m = this.GetModel<ISettingsModel>();
        m.PreviewUiScale.Value = m.UiScale.Value;
    }
}
```

### 5.4 `GameApp.cs` 注册

在 `Init()` 里 Utility 之后、其他 Model 之前：

```csharp
RegisterModel<ISettingsModel>(new SettingsModel());
```

### 5.5 `PanelScaleApplier`（MonoBehaviour）

位置：`Assets/UI_V2/Controller/PanelScaleApplier.cs`，命名空间 `APP.Pomodoro.Controller`（与其他 UI_V2/Controller 文件一致）。

```csharp
public sealed class PanelScaleApplier : MonoBehaviour, IController
{
    [SerializeField] private PanelSettings[] _panelSettings;  // Inspector 拖入两个 asset

    IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

    private void Start()
    {
        this.GetModel<ISettingsModel>().PreviewUiScale
            .RegisterWithInitValue(ApplyToAll)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    private void ApplyToAll(float s)
    {
        if (_panelSettings == null) return;
        foreach (var ps in _panelSettings)
        {
            if (ps != null) ps.scale = s;
        }
    }
}
```

**场景布线**：挂在 DeskWindow 场景一个常驻 GameObject（可与 DeskWindowController 同一 GameObject），`_panelSettings` 拖入 `PanelSettings_Settings.asset` + `PomodoroPanelSettings.asset`。

### 5.6 数据流总览

| 动作 | 写入 | 订阅端反应 |
|---|---|---|
| 启动 | `OnInit` 从 Storage 读 → `UiScale` + `PreviewUiScale` 同步写入（不触发 Register） | 屏幕初始缩放正确 |
| 拖动滑块 | 只改面板 local `_pendingScale` 和 label | 屏幕不变 |
| 点"应用" | `SetPreviewUiScaleCommand(local)` → `PreviewUiScale` 变 | `PanelScaleApplier` 写两个 `PanelSettings.scale` → 屏幕变；倒计时弹窗弹出 |
| 点"保留" | `CommitUiScaleCommand` → `UiScale` 变 → Register 自动 `Save` | 持久化；对话框关闭 |
| 点"还原" / 超时 | `RevertUiScaleCommand` → `PreviewUiScale` 写回 `UiScale.Value` | `PanelScaleApplier` 写回旧值 → 屏幕回滚；对话框关闭 |

---

## 6. `ConfirmDialogController` 通用化

### 6.1 新 `ConfirmDialogController`

位置：`Assets/UI_V2/Controller/ConfirmDialogController.cs`。

```csharp
public sealed class ConfirmDialogController
{
    private VisualElement _root, _countdownRow, _closeBtn;
    private Label _title, _subtitle, _body, _countdownText;
    private Button _confirmBtn, _cancelBtn;

    private IVisualElementScheduledItem _tickItem;
    private float _remainingSeconds;

    private Action _onConfirm, _onCancel;

    public bool IsVisible => _root != null && _root.style.display != DisplayStyle.None;

    public void Init(VisualElement host, VisualTreeAsset template);
    public void Show(
        string title, string subtitle, string body,
        string confirmText, string cancelText,
        Action onConfirm, Action onCancel,
        float countdownSeconds = 0f);
    public void Hide();

    private void HandleConfirm();
    private void HandleCancel();
    private void StartCountdown(float seconds);   // schedule.Execute(Tick).Every(500).StartingIn(0)
    private void Tick(TimerState _);              // 每 0.5s 扣秒、刷新文字、到 0 触发 HandleCancel
}
```

- 倒计时使用 `_root.schedule.Execute(Tick).Every(500)`；生命周期跟 `_root` 走，不会泄漏。
- `countdownSeconds <= 0` 时 countdown 行保持隐藏，不启动 scheduler。
- 倒计时文字显示 `Mathf.CeilToInt(_remainingSeconds)`（`5 → 4 → 3 → 2 → 1 → 0`）。
- 用户手动 Confirm/Cancel/X 时立即停 scheduler。

### 6.2 `UnsavedChangesDialogController` 改造为薄适配

对外 API（`Init` / `Show(onConfirm, onCancel)` / `Hide` / `IsVisible`）保持不变，内部委托：

```csharp
public sealed class UnsavedChangesDialogController
{
    private readonly ConfirmDialogController _inner = new ConfirmDialogController();
    public bool IsVisible => _inner.IsVisible;

    public void Init(VisualElement host, VisualTreeAsset confirmDialogTemplate)
        => _inner.Init(host, confirmDialogTemplate);

    public void Show(Action onConfirm, Action onCancel)
        => _inner.Show(
            title: "有未保存的更改",
            subtitle: "请先应用或取消后再继续",
            body: "你修改了番茄钟设置但尚未应用。离开此面板将丢失这些改动，是否先保存并继续？",
            confirmText: "保存并继续",
            cancelText: "取消",
            onConfirm: onConfirm,
            onCancel: onCancel,
            countdownSeconds: 0f);

    public void Hide() => _inner.Hide();
}
```

### 6.3 旧 UnsavedChangesDialog 资源处理

- 删除 `Assets/UI_V2/Documents/UnsavedChangesDialog.uxml`
- 删除 `Assets/UI_V2/Styles/UnsavedChangesDialog.uss`
- `UnifiedSettingsPanel.uxml` Style 列表中移除 `UnsavedChangesDialog.uss`，加入 `ConfirmDialog.uss`
- `UnifiedSettingsPanelDriver.cs` 序列化字段 `_unsavedDialogTemplate` → `_confirmDialogTemplate`，绑定到 `ConfirmDialog.uxml`

### 6.4 Scale 确认弹窗的独立 host

在 `settings-overlay` 内部 `unsaved-dialog-host` 之后追加：

```xml
<ui:VisualElement name="scale-dialog-host" class="unsaved-dialog-host" picking-mode="Ignore" />
```

两个 host 独立，对应两份 `ConfirmDialogController` 实例，互不影响。

---

## 7. `GlobalSettingsPanelController` 交互时序

位置：`Assets/UI_V2/Controller/GlobalSettingsPanelController.cs`，命名空间 `APP.Pomodoro.Controller`。

### 7.1 状态 & 字段

```csharp
public sealed class GlobalSettingsPanelController : IController
{
    IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

    private Slider _slider;
    private Label _valueLabel;
    private Button _applyBtn;

    private ConfirmDialogController _scaleDialog;
    private float _pendingScale;

    private const float CountdownSeconds = 5f;
}
```

### 7.2 Init

```csharp
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

    _slider.lowValue  = SettingsModel.MinScale;
    _slider.highValue = SettingsModel.MaxScale;

    _scaleDialog = new ConfirmDialogController();
    _scaleDialog.Init(dialogHost, confirmDialogTemplate);

    SyncSliderFromModel(model.UiScale.Value);

    _slider.RegisterValueChangedCallback(evt =>
    {
        _pendingScale    = SnapToStep(evt.newValue);
        _valueLabel.text = FormatScale(_pendingScale);
    });

    _applyBtn.clicked += OnApplyClicked;

    model.UiScale.Register(SyncSliderFromModel)
        .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
}

public void RefreshFromModel()
    => SyncSliderFromModel(this.GetModel<ISettingsModel>().UiScale.Value);

private void SyncSliderFromModel(float v)
{
    _pendingScale = v;
    _slider.SetValueWithoutNotify(v);
    _valueLabel.text = FormatScale(v);
}

private static float  SnapToStep(float v) => Mathf.Round(v * 10f) / 10f;
private static string FormatScale(float v) => $"{v:0.0}×";
```

### 7.3 点"应用"

```csharp
private void OnApplyClicked()
{
    var model   = this.GetModel<ISettingsModel>();
    var current = model.UiScale.Value;
    var target  = SnapToStep(_pendingScale);

    if (Mathf.Approximately(current, target)) return;  // 无变化
    if (_scaleDialog.IsVisible)                 return; // 防重入

    this.SendCommand(new SetPreviewUiScaleCommand(target));

    _scaleDialog.Show(
        title:       "保留新缩放吗？",
        subtitle:    $"当前 {FormatScale(target)}，原 {FormatScale(current)}",
        body:        "如 5 秒内未保留，将自动还原到原缩放。",
        confirmText: "保留",
        cancelText:  "还原",
        onConfirm:   () => this.SendCommand(new CommitUiScaleCommand()),
        onCancel:    () => this.SendCommand(new RevertUiScaleCommand()),
        countdownSeconds: CountdownSeconds);
}
```

### 7.4 `UnifiedSettingsPanelController` 改动

- 新字段：`_tabGlobal`、`_globalRoot`、`_globalSettings`、`_globalTemplate`、`_scaleDialogHost`；把 `_unsavedDialogTemplate` 改名 `_confirmDialogTemplate`。
- `Init(...)` 多接 `_globalTemplate`；`_scaleDialogHost = root.Q("scale-dialog-host")`。
- `SelectTab` 添加 `"global"` 分支；`DoSelectTab` 在 `"global"` 分支调用 `_globalSettings?.RefreshFromModel()`。
- `EnsureTabContent` 添加 `"global"`：懒加载 `_globalRoot` + `new GlobalSettingsPanelController()` + `Init(_globalRoot, _scaleDialogHost, _confirmDialogTemplate, _lifecycleOwner)`。
- **关闭 / 切 tab 拦截逻辑无改动**：pomodoro dirty 检查保持；global tab 的倒计时对话框本身就是模态 backdrop，显示期间面板下方所有输入都吃不到，不需要在 Unified 层额外拦截。

### 7.5 `UnifiedSettingsPanelDriver.cs` 改动

- 新增序列化字段 `[SerializeField] private VisualTreeAsset _globalTemplate;`
- `_unsavedDialogTemplate` 改名 `_confirmDialogTemplate`（重新指向 `ConfirmDialog.uxml`）
- `Init` 调用改签：把 `_globalTemplate` 与 `_confirmDialogTemplate` 一并传入

### 7.6 `UnifiedSettingsPanel.uxml` 改动

- 侧栏 `qvEam` 中 `tab-pet` 之后追加 `tab-global`（label "全局"）
- `settings-overlay` 底部追加：
  ```xml
  <ui:VisualElement name="scale-dialog-host" class="unsaved-dialog-host" picking-mode="Ignore" />
  ```
- 顶部 Style 列表：删除 `<Style src="../Styles/UnsavedChangesDialog.uss"/>`，新增 `ConfirmDialog.uss` 与 `GlobalSettingsPanel.uss`

---

## 8. 错误处理与边界

| 情形 | 处理 |
|---|---|
| PlayerPrefs 读出非法值或超界 | `SettingsModel.OnInit` 做 `Mathf.Clamp([0.5, 2.0])` |
| `PanelScaleApplier._panelSettings` 元素为 null 或数组未赋值 | 遍历跳过 null；整个数组为空仅打 warning，不抛异常 |
| 用户在倒计时中关桌宠 / 域重载 | `IVisualElementScheduledItem` 跟 VisualElement 销毁；`UiScale` 未 commit，下次启动仍是旧值 |
| 连续点"应用" | `OnApplyClicked` 头部 `if (_scaleDialog.IsVisible) return;` |
| 目标值 == 当前正式值 | `Mathf.Approximately`，不弹窗 |
| 弹窗显示期间面板其它输入 | `.dlg-root` 使用 `position:absolute; inset:0;` + 半透明 backdrop + `picking-mode=Position`，全吃 |

---

## 9. 测试策略

### 9.1 EditMode 单测

新建 `Assets/Tests/EditMode/SettingsTests/Editor/`。

| 测试类 | 覆盖点 |
|---|---|
| `SettingsModelTests` | OnInit 从 PlayerPrefs 读值；非法值 Clamp；`UiScale.Register` 触发持久化；`PreviewUiScale` 初值等于 `UiScale` |
| `UiScaleCommandTests` | `SetPreviewUiScaleCommand` 仅改 Preview；`CommitUiScaleCommand` Preview → UiScale；`RevertUiScaleCommand` UiScale → Preview；Preview Command 做 Clamp |
| `GlobalSettingsPanelControllerTests` | 滑块拖动不改 Model；点 Apply 且值不同 → 发 Preview Command + 弹窗 IsVisible；点 Apply 且值相同 → 无 Command 无弹窗；弹窗 IsVisible 时再点 Apply 无反应；onConfirm → Commit Command；onCancel → Revert Command；`RefreshFromModel` 把滑块刷回 `UiScale` 值 |
| `ConfirmDialogControllerTests` | `Show(countdown:0)` 不启 scheduler；`Show(countdown:5)` 每 0.5s 扣秒、label 显示 ceil；到 0 触发 onCancel 并停 scheduler；手动 confirm/cancel 停 scheduler；重复 Show 不泄漏 scheduler |
| `UnsavedChangesDialogControllerTests`（保留原有） | 原有断言不变，验证薄适配行为等价 |

### 9.2 手工验证清单（Unity Play Mode）

- [ ] 打开设置 → "全局" tab → 滑块 `1.0×`，label `"1.0×"`
- [ ] 拖滑块到 `1.5` → 屏幕不变、label `"1.5×"`
- [ ] 点"应用" → 屏幕立即放大到 `1.5×`，对话框弹 `"保留新缩放吗？"`，倒计时从 5 逐秒递减
- [ ] 5s 不动 → 自动关闭，屏幕回到 `1.0×`
- [ ] 重试：应用 `1.5×` → 点"保留" → 对话框关闭，屏幕保持；重启 Unity 仍 `1.5×`
- [ ] 重试：应用 `2.0×` → 点"还原" → 屏幕立即回 `1.0×`
- [ ] 倒计时中点侧栏/X/滑块 → 无反应
- [ ] 番茄钟"未保存更改"对话框照常工作（回归）
- [ ] 两个 PanelSettings（桌面窗 + 番茄钟窗）同步缩放

---

## 10. 实施顺序（强制 Pencil 优先）

1. **Pencil 侧**（按 `feedback_ui_pencil_first.md`）：
   a. 新建 `ConfirmDialog` reusable；把 `ikREg` 改为它的实例（overrides）
   b. 新建 `SettingsApplyRow` reusable；把 `gs1Tv/PBXgQ` 改为它的实例
   c. 新建 `Global Settings Panel` reusable（含 `gspApply` + `gspScale`）
   d. `vnYnS` 侧栏追加 `tab-global`；body 底部追加 `scale-dialog-host` 容器
   e. **⌘S 落盘**（`feedback_pencil_manual_save.md`）
2. **UXML / USS** 对齐 Pencil：新增 `ConfirmDialog.uxml/.uss`、`GlobalSettingsPanel.uxml/.uss`；删旧 `UnsavedChangesDialog.uxml/.uss`；改 `UnifiedSettingsPanel.uxml`
3. **C# 代码**：`ISettingsModel` / `SettingsModel` → 3 个 Command → `PanelScaleApplier` → `ConfirmDialogController` → `UnsavedChangesDialogController` 薄适配改造 → `GlobalSettingsPanelController` → `UnifiedSettingsPanel{Controller,Driver}` 改动
4. **场景布线**：在 DeskWindow 场景挂 `PanelScaleApplier`，拖入两个 PanelSettings；`UnifiedSettingsPanelDriver` 上关联新的 `_globalTemplate` + `_confirmDialogTemplate`
5. **编译检查**（`read_console` error filter）
6. **EditMode 测试**（`run_tests`）
7. **手工 Play Mode 验证**（按 §9.2 清单）
