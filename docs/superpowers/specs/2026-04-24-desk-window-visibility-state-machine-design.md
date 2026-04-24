# 桌面窗口可见性状态机 · 设计稿

日期：2026-04-24
分支：feat/desk-window-refactor
状态：待用户 review

## 与前置 spec 的关系

本设计是 [`2026-04-23-pin-button-and-focus-hiding-design.md`](2026-04-23-pin-button-and-focus-hiding-design.md) 的后继/演进。该 spec 明确把以下三点列为"非目标"，本 spec **反转这三条**：

| 前置 spec（2026-04-23） | 本 spec（2026-04-24） |
|---|---|
| 不接 `OnApplicationFocus` 真实源；Editor 手动注入 `IsAppFocused` | **接上真实源**：新增 `AppFocusBridge : MonoBehaviour` + `Cmd_SetAppFocused`。Editor 调试窗口仍保留，作为覆盖入口 |
| 不改 `UniWindowController.isTopmost` 语义 | **让 `isTopmost` 由 `AnyPinned` 派生**：`PomodoroModel.IsTopmost` 字段去意义化 |
| 不引入集中决策器（`VisibilitySystem` 等） | **新增 `IWindowVisibilityCoordinatorSystem`** 负责聚合 `AnyPinned` 并驱动 `isTopmost` |

**公式变更**（非等价）：

- 旧：`visible = IsAppFocused ∥ thisPinned`
- 新：`hidden = !thisPinned ∧ !IsAppFocused ∧ AnyPinned`

两者只在 `AnyPinned=false ∧ IsAppFocused=false` 时不同：旧公式让所有非 pinned UI 隐藏，新公式让所有 UI 保持可见（因为此时整窗口已经 `isTopmost=false`，视觉沉底由 macOS 原生完成，不需要 CSS 级的隐藏）。这正是 S0 状态的新语义。

保持不变（前置 spec 已落地，本 spec 复用）：
- `IGameModel.IsAppFocused` 字段定义
- `IPomodoroModel.IsPinned` / `IPlayerCard.IsPinned` 及其持久化
- `Cmd_SetPomodoroPinned` / `Cmd_SetPlayerCardPinned`
- `E_PlayerCardAdded` / `E_PlayerCardRemoved` 事件
- Editor `Tools/Model 调试器` 窗口（可继续用于人工覆盖 `IsAppFocused`）

## 背景

桌宠小游戏当前的窗口层级与 UI 可见性由多处零散逻辑驱动：

- `UniWindowController.isTopmost` 由用户偏好 `PomodoroModel.IsTopmost` 显式写入（`Cmd_PomodoroSetTopmost`）
- 各张 UI（番茄钟面板、PlayerCard）各自订阅"Unity 失焦"事件来隐藏自身
- `PlayerCard.IsPinned` / `PomodoroModel.IsPinned` 标记"这张 UI 是否 pin"

存在的漏洞：**当用户把所有 UI 都取消 pin 后，一旦 Unity 窗口失焦，所有 UI 都会被隐藏，而透明点击穿透会让用户无法点中任何 Unity 区域来重新聚焦——UI 永久失联**。

同时 `PomodoroModel.IsTopmost` 与 `IsPinned` 语义重叠，偏好与运行时状态混在一起。

## 目标

用一个**统一、自洽的状态机**替换分散的失焦隐藏逻辑：

1. 整窗口的 `isTopmost` 由"是否存在任意一个 pinned UI"自动派生
2. 每个 UI 的可见性由一条公式决定（pin 状态 × 应用焦点 × 全局 AnyPinned）
3. 不引入额外的"唤回通道"——由于 pinned UI 的存在是进入"隐藏态"的前提，pinned UI 自身必然可见，必然担任唤回入口
4. 结构符合 QFramework 四层分层规范

## 状态机

### 输入

| 变量 | 类型 | 来源 |
|---|---|---|
| `AnyPinned` | 派生 bool | `PomodoroModel.IsPinned` ∥ 任一 `PlayerCard.IsPinned` |
| `GameModel.IsAppFocused` | `BindableProperty<bool>` | Unity `OnApplicationFocus(bool)` 回调 |

### 输出

| 输出 | 规则 |
|---|---|
| `UniWindowController.isTopmost` | 直接等于 `AnyPinned` |
| 每个 UI 的隐藏态 | `hidden = !thisIsPinned && !IsAppFocused && AnyPinned` |

### 三态真值表

| 状态 | AnyPinned | IsAppFocused | isTopmost | 非 pinned UI | pinned UI |
|---|---|---|---|---|---|
| **S0** | false | *（不参与） | **false** | 显示 | 显示 |
| **S1** | true | true | **true** | 显示 | 显示 |
| **S2** | true | false | **true** | **隐藏** | 显示 |

**每个状态的用户视觉效果：**

- **S0**：没有任何 pin 要求，整窗口不置顶，其他 app 可以正常盖在 Unity 之上——视觉"沉底"由 macOS 原生窗口管理自然达成。
- **S1**：有 pin 需求，窗口置顶，用户正在操作 Unity，所有 UI 都可见。
- **S2**：有 pin 需求，窗口置顶但用户已切到别的 app；视觉上看起来"别的 app 盖住了非 pinned UI"，实际是非 pinned UI 被 display:none；pinned UI 作为"始终浮在最上"的少数块保持可见，同时作为唤回入口。

### 状态迁移

```
S0 ──[任一 UI pin 切换为 true]──→ S1
S1 ──[最后一个 pin 被取消]────→ S0
S1 ──[Unity 失焦]─────────────→ S2
S2 ──[Unity 得焦]─────────────→ S1
S2 ──[最后一个 pin 被取消]────→ S0  (详见边界情况 #1)
```

## 组件职责（QFramework 映射）

### Model 层（保持现状）

已存在、保持不变：

- `IGameModel.IsAppFocused: BindableProperty<bool>`（默认 true）
- `IPomodoroModel.IsPinned: BindableProperty<bool>`
- `IPlayerCard.IsPinned: BindableProperty<bool>`

**待去意义化**：
- `IPomodoroModel.IsTopmost`：新状态机下 `isTopmost` 完全由 `AnyPinned` 派生，此字段不再独立有效。标为 `[Obsolete]`，由 implementation plan 决定迁移路径（包括持久化字段、`Cmd_PomodoroSetTopmost` / `Cmd_PomodoroRevertTopmost` / `Cmd_PomodoroJumpToScreenTop` 的重构或删除）。

### Query 层

新增：
- `Q_IsAnyPinned : AbstractQuery<bool>` —— 一次性读取当前 AnyPinned 的快照。用于启动时初值、Editor 调试窗口、测试断言等。运行期订阅请用 System 暴露的 BindableProperty。

### System 层

**新增 `IWindowVisibilityCoordinatorSystem`**（实现 `WindowVisibilityCoordinatorSystem : AbstractSystem`）：

- 对外暴露 `IReadonlyBindableProperty<bool> AnyPinned`
- `OnInit` 中：
  - 订阅 `PomodoroModel.IsPinned` 变化
  - 订阅现有的 `E_PlayerCardAdded` / `E_PlayerCardRemoved` 事件，动态维护每张 PlayerCard 的 `IsPinned` 订阅集合
  - 内部 `Recalculate()` 聚合 OR → 写 `AnyPinned.Value`
- `AnyPinned.Value` 变化 → 调 `IWindowPositionSystem.SetTopmost(AnyPinned.Value)` → 发 `E_AnyPinnedChanged { bool AnyPinned }`（供 View 订阅）
- 初始化后立即跑一次 `Recalculate()` 保证冷启动 isTopmost 正确

**现有 `IWindowPositionSystem`**：保持 `SetTopmost(bool)` 接口，但不再由 Controller 直接调用——只被新 coordinator 调用。`JumpToScreenTop` / `RevertTopmost` 的存废由 implementation plan 决定。

### 焦点兼容层（用户要求：QFramework 抽象 + 底层走 Unity `OnApplicationFocus`）

**`AppFocusBridge : MonoBehaviour`**（新增）：
- 挂在 GameApp 常驻 GameObject 上（与 Architecture 初始化同生命周期）
- 实现 `OnApplicationFocus(bool hasFocus) => this.SendCommand(new Cmd_SetAppFocused(hasFocus))`
- 职责单一，不包含任何业务判断
- 后续若要换成 `NSWorkspace.didActivateApplication` 精度更高的源，只需替换 Bridge 的实现，上层无感知

**`Cmd_SetAppFocused(bool) : AbstractCommand`**（新增）：
- `protected override void OnExecute() => this.GetModel<IGameModel>().IsAppFocused.Value = _hasFocus;`

### Controller / View 层

每个可隐藏 UI（`PomodoroPanelView`、`PlayerCardController`）：

- 订阅三项：本 UI 的 `IsPinned`（Model 上） + `GameModel.IsAppFocused` + 新 coordinator 的 `AnyPinned`
- 每次任意一项变化时重算：
  ```
  shouldHide = !thisIsPinned && !IsAppFocused && AnyPinned
  ```
- 根据结果 toggle `.pc-hidden`（PlayerCard）或 `.pp-hidden`（PomodoroPanel，命名由 implementation 阶段决定）
- **删除**当前"直接订阅 Unity 失焦隐藏"的分散逻辑，统一走此公式

## 边界情况

### #1 · S2 下 unpin 最后一张 pinned UI

典型场景：用户当前在 S2（Unity 失焦，只剩一张 pinned 卡片可见），点击那张卡的 pin 按钮想取消 pin。

时序：
1. 用户点 pin 按钮 —— 这是点击到 Unity 的非透明 UI 区域
2. 点击让 Unity 得焦：`OnApplicationFocus(true)` → `Cmd_SetAppFocused(true)` → `IsAppFocused=true`
3. 状态暂进 S1，所有 UI 显现
4. pin 按钮 Command 执行 → `AnyPinned` 重算为 false → 进 S0
5. `isTopmost=false`，UI 公式恒为 `shouldHide=false`，全部保持可见 ✅

用户不会丢失任何 UI。

### #2 · 通过外部路径（Editor 调试窗口、测试代码）直接置 `AnyPinned=false`

不经过聚焦动作，纯粹 Model 变化。

- `AnyPinned` false → 进 S0
- UI 可见性公式恒为 `shouldHide=false`（因为 AnyPinned 项为 false）
- 无 UI 卡在隐藏状态 ✅

### #3 · S2 下新增一张 IsPinned=false 的 PlayerCard

- `E_PlayerCardAdded`（IsPinned=false） → coordinator 订阅该 Card，重算 AnyPinned 不变（仍 true）
- 新卡的 View 用 `RegisterWithInitValue` 初始订阅 → 计算出 `shouldHide=true`（当前 S2）→ 直接带 `.hidden` class 呈现
- ✅ 不会闪现一帧

### #4 · S1 下新增一张 IsPinned=true 的 PlayerCard

- 先前已 AnyPinned=true，`isTopmost` 已经是 true
- 新卡加入 → 订阅其 IsPinned，AnyPinned 保持 true
- 新卡 View 初始订阅 → `shouldHide=false`（因 IsAppFocused=true）→ 正常显示 ✅

### #5 · 删除最后一张 pinned 卡（房间 clear、玩家离线等）

- `E_PlayerCardRemoved` → coordinator 解订阅 → 重算 AnyPinned
- 若只有该卡 pinned → AnyPinned true→false → `isTopmost=false`
- 其他卡（如有）的 View 公式重算 → `shouldHide=false` ✅

### #6 · 冷启动

- `GameModel.IsAppFocused` 默认 true
- `PomodoroModel.IsPinned` / `PlayerCard.IsPinned` 从持久化恢复（已实现）
- `WindowVisibilityCoordinatorSystem.OnInit` 跑一次 `Recalculate()` → 写 `AnyPinned.Value` → 调 `SetTopmost`
- Controller 用 `RegisterWithInitValue` 订阅，首帧应用公式得到正确初值 ✅

### #7 · `isTopmost` 切换瞬间的视觉

macOS 下 `NSWindow.level` 切换是瞬时，理论上不引入闪烁。如实测出现一帧异常，由 implementation plan 调整时序（先切 `isTopmost` 再应用可见性，或反之）。此处标为待实测项。

## 测试策略

### EditMode 单元测试（必做）

- `WindowVisibilityCoordinatorSystemTests`
  - 初始 AnyPinned 计算覆盖各种 Pomodoro × Cards 组合
  - `PomodoroModel.IsPinned` 变化触发 AnyPinned 更新
  - `PlayerCard.IsPinned` 变化触发 AnyPinned 更新
  - `E_PlayerCardAdded` / `E_PlayerCardRemoved` 后订阅管理正确（加入已 pinned 卡应提升 AnyPinned；移除最后 pinned 卡应降 AnyPinned）
  - AnyPinned 变化时会调用 `IWindowPositionSystem.SetTopmost`（用 mock system 验证）
- `Cmd_SetAppFocusedTests`：写 `GameModel.IsAppFocused` 正确
- `Q_IsAnyPinnedTests`：覆盖真值表每种组合
- 隐藏公式真值表可在 View 层提取为 `static bool ComputeShouldHide(bool thisIsPinned, bool isAppFocused, bool anyPinned)` 纯函数单测

### PlayMode 集成测试（可选）

- 触发 `AppFocusBridge.OnApplicationFocus(false)` 模拟失焦，验证非 pinned UI 的 `.hidden` class 切换
- Mock `UniWindowController` 验证 isTopmost 被调用

### 手动回归（macOS 真机）

- Cmd+Tab 切到其他 app → 非 pinned UI 应隐藏（S1→S2）
- Cmd+Tab 切回 → 恢复（S2→S1）
- Dock 点击 Unity 图标 → S2→S1
- 点透明区域穿透到其他 app → S1→S2
- 点 pinned UI → Unity 得焦 → S2→S1
- 全部 unpin → isTopmost 应变 false，其他 app 能正常盖过 Unity（S0）

## 已识别的风险与待处理项

1. **`PomodoroModel.IsTopmost` 去意义化**：独立属性失去意义，需处理 `Cmd_PomodoroSetTopmost` / `Cmd_PomodoroRevertTopmost` / `Cmd_PomodoroJumpToScreenTop` / 持久化字段 `IsTopmost` 的迁移路径。留给 implementation plan。
2. **`OnApplicationFocus` 在 UniWindowController 配置下的触发稳定性**：需在真机验证失焦/得焦是否稳定触发，尤其是点击穿透导致的焦点变化。若触发不稳定，退路是改用 `CPA.Monitoring.AppMonitor` 监听前台 app bundleId 变化作为替代源。
3. **AppFocusBridge 的挂载时机**：必须确保 Bridge 所在 GameObject 在 Architecture 初始化后、首次可能失焦前已存在。放在 GameApp 引导流程里最安全。
4. **`_Archived/PomodoroPanelController.cs`**：归档文件中仍有旧式 `OnApplicationFocus` → `Cmd_PomodoroRevertTopmost` 调用，重构时若原文件已归档可忽略；若被重新启用，需一并迁移到新状态机。
5. **PomodoroPanel 与 PlayerCard 的 CSS 类命名不统一**：当前 `.pc-hidden`（PlayerCard）已存在；PomodoroPanel 的隐藏实现需要对齐命名约定（`.pp-hidden` 或通用 `.hidden`），由 implementation plan 决定。

## 非目标

- 不新增菜单栏图标、全局快捷键、屏幕边缘热区等额外唤回通道（状态机已闭环）
- 不改变 `UniWindowController.hitTestType`（保持 Alpha-based 点击穿透）
- 不引入多进程、多窗口方案
- 不改变现有 `PomodoroPanel` / `PlayerCard` 的位置/锚点逻辑（`WindowPositionSystem.MoveToMonitor` 等保持不变）
