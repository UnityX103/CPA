# 桌面窗口可见性状态机 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用 `AnyPinned × IsAppFocused` 双维驱动的状态机替换当前分散的"失焦隐藏"逻辑。`UniWindowController.isTopmost` 从 `PomodoroModel.IsTopmost` 静态偏好改为由 `AnyPinned` 实时派生；每个 UI 的隐藏公式改为 `hidden = !thisPinned ∧ !IsAppFocused ∧ AnyPinned`；`Cmd_PomodoroSetTopmost / RevertTopmost / JumpToScreenTop` 一并删除。

**Architecture:** 新增 `IWindowVisibilityCoordinatorSystem` 聚合 `PomodoroModel.IsPinned` 与所有 `PlayerCard.IsPinned` 计算 `AnyPinned`，并在其变化时调用 `IWindowPositionSystem.SetTopmost`。新增 `Cmd_SetAppFocused(bool)` 承接 Unity `OnApplicationFocus`。`IPomodoroModel.IsTopmost` 去意义化删除（持久化字段一并移除，开发期不做兼容）。

**Tech Stack:** Unity 6000.0.25f1、QFramework v1.0、UniWindowController、UI Toolkit、NUnit EditMode。

**Unity 约束（重要）：** 项目里"测试先于实现"的 TDD 红绿灯会触发全工程 CS0234 编译失败（测试文件引用尚未定义的类型会让整个 Assembly-CSharp 编译失败，连现有测试都跑不起来）。因此每个任务**先写实现 → 编译通过 → 写/改测试 → 测试通过 → commit**。对应的 meta 文件交给 Unity 首次打开时自动生成（不要手编 GUID，可能出现非 hex 字符导致 Unity 崩溃）。

**Spec:** `docs/superpowers/specs/2026-04-24-desk-window-visibility-state-machine-design.md`

**与 Spec 的偏离：**

| 偏离点 | Spec 要求 | Plan 实施 | 原因 |
|---|---|---|---|
| AppFocus 兼容层 | 新增 `AppFocusBridge : MonoBehaviour` 独立挂载 | 复用 `DeskWindowController.OnApplicationFocus`，一行转发 `Cmd_SetAppFocused` | DeskWindowController 本身就是 MonoBehaviour 且生命周期与整个桌宠程序一致；额外 Bridge 是 YAGNI。如未来要换精度更高的源（NSWorkspace），再抽 |
| Coordinator 启动时序 | Spec 含糊："立即 `Recalculate()` 保证冷启动 isTopmost 正确" | `Coordinator.OnInit` 订阅 + 计算 `AnyPinned`，但 `_uwc` 此时为 null；**由 `Cmd_PomodoroInitialize` 在 `wps.Initialize()` 之后显式调 `wps.SetTopmost(coordinator.AnyPinned.Value)`** 完成首次应用 | `WindowPositionSystem._uwc` 是在 `Cmd_PomodoroInitialize` 里才注入的，Architecture.Init 阶段 SetTopmost 对原生窗口无效；在初始化命令里 apply 最干净 |

---

## 文件结构

### 新建

| 路径 | 职责 |
|---|---|
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetAppFocused.cs` | 写 `IGameModel.IsAppFocused` |
| `Assets/Scripts/APP/Pomodoro/Queries/Q_IsAnyPinned.cs` | 一次性读取当前 AnyPinned 的快照 |
| `Assets/Scripts/APP/Pomodoro/System/IWindowVisibilityCoordinatorSystem.cs` | Coordinator 接口（暴露 `AnyPinned: IReadonlyBindableProperty<bool>`） |
| `Assets/Scripts/APP/Pomodoro/System/WindowVisibilityCoordinatorSystem.cs` | 实现：聚合 IsPinned 信号、驱动 WindowPositionSystem.SetTopmost |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/WindowVisibilityCoordinatorSystemTests.cs` | Coordinator 单测 |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/QIsAnyPinnedTests.cs` | Query 单测 |

### 修改

| 路径 | 改动要点 |
|---|---|
| `Assets/Scripts/APP/Pomodoro/GameApp.cs` | 注册 `IWindowVisibilityCoordinatorSystem` |
| `Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs` | 删除 `BindableProperty<bool> IsTopmost` |
| `Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs` | 删除 `IsTopmost` 字段、`PomodoroPersistentState.IsTopmost`、持久化读写中对 `IsTopmost` 的处理 |
| `Assets/Scripts/APP/Pomodoro/System/IWindowPositionSystem.cs` | 删除 `JumpToScreenTop()` / `RevertTopmost()` 成员 |
| `Assets/Scripts/APP/Pomodoro/System/WindowPositionSystem.cs` | 删除 `JumpToScreenTop()` / `RevertTopmost()` 实现；`SetTopmost` 里不再写 Model |
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroInitialize.cs` | 末尾 `wps.SetTopmost(model.IsTopmost.Value)` 改为 `wps.SetTopmost(coordinator.AnyPinned.Value)` |
| `Assets/UI_V2/Controller/PomodoroPanelView.cs` | `RefreshVisibility` 切换到新公式；订阅 `coordinator.AnyPinned` |
| `Assets/UI_V2/Controller/PlayerCardController.cs` | `RefreshVisibility` 切换到新公式；`Bind` 增订阅 `coordinator.AnyPinned` |
| `Assets/UI_V2/Controller/DeskWindowController.cs` | `OnApplicationFocus` 改为发 `Cmd_SetAppFocused`；`RegisterPersistenceCallbacks` 删除 `_model.IsTopmost.Register` 一行 |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/PinCommandTests.cs` | 加 `Cmd_SetAppFocused` 的三个断言（默认 true / 写 false 后 true → Model 反映） |

### 删除

| 路径 | 原因 |
|---|---|
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroSetTopmost.cs` | `IsTopmost` 去意义化后无调用方 |
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroRevertTopmost.cs` | 同上 |
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroJumpToScreenTop.cs` | 同上（`JumpToScreenTop` 语义在新状态机下无意义） |

> **关于 `Assets/Scripts/APP/Pomodoro/Controller/_Archived/PomodoroPanelController.cs`**：该文件已归档、不参与编译（确认方式：看同目录 asmdef 或搜 `#if` 守卫）。引用本次删除的三个 Command 和 `IsTopmost` 的老代码全在此文件内。**Task 15 会验证它确实不参与编译**；如参与编译，则额外删除该归档文件以保证 Green。

---

## Task 1：新增 `Cmd_SetAppFocused`

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetAppFocused.cs`

- [ ] **Step 1：写 `Cmd_SetAppFocused.cs`**

```csharp
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 写入 IGameModel.IsAppFocused。
    /// 由 DeskWindowController.OnApplicationFocus 调用，
    /// 将 Unity 的应用焦点状态统一汇入 QFramework Model。
    /// </summary>
    public sealed class Cmd_SetAppFocused : AbstractCommand
    {
        private readonly bool _isFocused;

        public Cmd_SetAppFocused(bool isFocused) => _isFocused = isFocused;

        protected override void OnExecute() =>
            this.GetModel<IGameModel>().IsAppFocused.Value = _isFocused;
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Run: `mcp__UnityMCP__read_console` with filter types=Error
Expected: 0 error

- [ ] **Step 3：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Command/Cmd_SetAppFocused.cs
git commit -m "$(cat <<'EOF'
feat(command): 新增 Cmd_SetAppFocused 转发 Unity 应用焦点到 IGameModel

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

*（.meta 文件交给 Unity 首次打开自动补 GUID，不要手编。）*

---

## Task 2：新增 `IWindowVisibilityCoordinatorSystem` 接口

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/System/IWindowVisibilityCoordinatorSystem.cs`

- [ ] **Step 1：写接口**

```csharp
using QFramework;

namespace APP.Pomodoro.System
{
    /// <summary>
    /// 窗口可见性协调器：聚合 IPomodoroModel.IsPinned 与所有在线 IPlayerCard.IsPinned，
    /// 派生出 AnyPinned 标志，并在其变化时驱动 IWindowPositionSystem.SetTopmost。
    /// 对外只暴露只读快照，不允许外部直接写入。
    /// </summary>
    public interface IWindowVisibilityCoordinatorSystem : ISystem
    {
        /// <summary>
        /// 当前是否存在任何 pinned 的 UI。
        /// 由 PomodoroModel.IsPinned ∥ 任一 PlayerCard.IsPinned 派生。
        /// </summary>
        IReadonlyBindableProperty<bool> AnyPinned { get; }
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 3：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/System/IWindowVisibilityCoordinatorSystem.cs
git commit -m "$(cat <<'EOF'
feat(system): 定义 IWindowVisibilityCoordinatorSystem 接口

只暴露 AnyPinned 只读 BindableProperty；内部聚合 PomodoroModel 与 PlayerCards 的 pin 状态。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3：实现 `WindowVisibilityCoordinatorSystem`

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/System/WindowVisibilityCoordinatorSystem.cs`

- [ ] **Step 1：写实现**

```csharp
using System.Collections.Generic;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.System
{
    public sealed class WindowVisibilityCoordinatorSystem
        : AbstractSystem, IWindowVisibilityCoordinatorSystem
    {
        private readonly BindableProperty<bool> _anyPinned = new BindableProperty<bool>(false);
        private readonly Dictionary<string, IUnRegister> _cardSubs = new Dictionary<string, IUnRegister>();

        public IReadonlyBindableProperty<bool> AnyPinned => _anyPinned;

        protected override void OnInit()
        {
            IPomodoroModel pomodoro = this.GetModel<IPomodoroModel>();
            IPlayerCardModel cards = this.GetModel<IPlayerCardModel>();

            // 1) 订阅番茄钟 IsPinned
            pomodoro.IsPinned.Register(_ => Recalculate());

            // 2) 订阅 PlayerCard 动态集合（加/删）
            this.RegisterEvent<E_PlayerCardAdded>(e => OnCardAdded(e.PlayerId));
            this.RegisterEvent<E_PlayerCardRemoved>(e => OnCardRemoved(e.PlayerId));

            // 3) 对已存在的 Card（冷启动时从持久化恢复）订阅
            foreach (IPlayerCard card in cards.Cards)
            {
                SubscribeCard(card);
            }

            // 4) 写初值
            Recalculate();

            // 5) AnyPinned 变化 → 驱动原生窗口层级
            _anyPinned.Register(v => this.GetSystem<IWindowPositionSystem>().SetTopmost(v));
        }

        private void OnCardAdded(string playerId)
        {
            IPlayerCard card = this.GetModel<IPlayerCardModel>().Find(playerId);
            if (card == null) return;
            SubscribeCard(card);
            Recalculate();
        }

        private void OnCardRemoved(string playerId)
        {
            if (_cardSubs.TryGetValue(playerId, out IUnRegister handle))
            {
                handle.UnRegister();
                _cardSubs.Remove(playerId);
            }
            Recalculate();
        }

        private void SubscribeCard(IPlayerCard card)
        {
            if (_cardSubs.ContainsKey(card.PlayerId)) return;
            _cardSubs[card.PlayerId] = card.IsPinned.Register(_ => Recalculate());
        }

        private void Recalculate()
        {
            IPomodoroModel pomodoro = this.GetModel<IPomodoroModel>();
            IPlayerCardModel cards = this.GetModel<IPlayerCardModel>();

            bool any = pomodoro.IsPinned.Value;
            if (!any)
            {
                for (int i = 0; i < cards.Cards.Count; i++)
                {
                    if (cards.Cards[i].IsPinned.Value)
                    {
                        any = true;
                        break;
                    }
                }
            }
            _anyPinned.Value = any;
        }
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error. 若出现 `IReadonlyBindableProperty` 找不到，检查 QFramework.cs 内是否存在该接口（Search: `grep -n "IReadonlyBindableProperty" Assets/Scripts/QFramework.cs`）。注意 QFramework 的命名是 `IReadonlyBindableProperty`（小写 o、单 n），不是 `IReadOnly...`。

- [ ] **Step 3：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/System/WindowVisibilityCoordinatorSystem.cs
git commit -m "$(cat <<'EOF'
feat(system): 实现 WindowVisibilityCoordinatorSystem

- 订阅 PomodoroModel.IsPinned + 每张 PlayerCard.IsPinned（动态增删通过 E_PlayerCardAdded/Removed）
- AnyPinned 变化驱动 IWindowPositionSystem.SetTopmost
- OnInit 内立即 Recalculate 写入冷启动正确初值

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4：`GameApp` 注册 Coordinator

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/GameApp.cs`

- [ ] **Step 1：读当前 GameApp.cs**

Run: `Read /Users/xpy/Desktop/NanZhai/CPA/Assets/Scripts/APP/Pomodoro/GameApp.cs`

- [ ] **Step 2：在 `RegisterSystem<IWindowPositionSystem>` 之后追加一行**

在 `RegisterSystem<IWindowPositionSystem>(new WindowPositionSystem());` 下方插入：

```csharp
            RegisterSystem<IWindowVisibilityCoordinatorSystem>(new WindowVisibilityCoordinatorSystem());
```

（注意：必须在 `IWindowPositionSystem` 之后，因为 Coordinator 在 OnInit 里会立即触发 `SetTopmost`。虽然 WindowPositionSystem._uwc 此时仍为 null、无副作用，但依赖顺序仍保持显式。）

- [ ] **Step 3：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 4：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/GameApp.cs
git commit -m "$(cat <<'EOF'
feat(app): GameApp 注册 WindowVisibilityCoordinatorSystem

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5：新增 `Q_IsAnyPinned`

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Queries/Q_IsAnyPinned.cs`

- [ ] **Step 1：写 Query**

```csharp
using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro.Queries
{
    /// <summary>
    /// 读取当前 AnyPinned 的快照（PomodoroModel.IsPinned ∥ 任一 PlayerCard.IsPinned）。
    /// 用于 Editor 调试、启动时初值读取、单元测试断言。
    /// 运行期订阅应使用 IWindowVisibilityCoordinatorSystem.AnyPinned。
    /// </summary>
    public sealed class Q_IsAnyPinned : AbstractQuery<bool>
    {
        protected override bool OnDo() =>
            this.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned.Value;
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 3：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Queries/Q_IsAnyPinned.cs
git commit -m "$(cat <<'EOF'
feat(query): 新增 Q_IsAnyPinned 读取 AnyPinned 快照

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6：`Cmd_PomodoroInitialize` 迁移到 Coordinator

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroInitialize.cs`

- [ ] **Step 1：替换末尾两行**

当前 `OnExecute` 末尾（约第 60-61 行）：

```csharp
            // 按恢复后的偏好应用显示器/置顶
            wps.MoveToMonitor(model.TargetMonitorIndex.Value);
            wps.SetTopmost(model.IsTopmost.Value);
```

替换为：

```csharp
            // 按恢复后的偏好应用显示器
            wps.MoveToMonitor(model.TargetMonitorIndex.Value);

            // 冷启动首次应用 isTopmost：由 coordinator 基于 AnyPinned 派生
            var coordinator = this.GetSystem<IWindowVisibilityCoordinatorSystem>();
            wps.SetTopmost(coordinator.AnyPinned.Value);
```

以及顶部 using 区补一行（若已有可跳过）：

```csharp
using APP.Pomodoro.System;
```

（该 using 实际上已存在——文件顶部 `using APP.Pomodoro.System;` 已用于 `IWindowPositionSystem`。所以只加代码。）

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error. 如果报 `IWindowVisibilityCoordinatorSystem` 找不到，检查上一个任务的 commit 是否包含 System 类。

- [ ] **Step 3：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroInitialize.cs
git commit -m "$(cat <<'EOF'
refactor(command): Cmd_PomodoroInitialize 从 coordinator 读 AnyPinned 作为 isTopmost 初值

不再读 Model.IsTopmost；为后续 IsTopmost 字段删除铺路。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7：`PomodoroPanelView` 切换新隐藏公式

**Files:**
- Modify: `Assets/UI_V2/Controller/PomodoroPanelView.cs`

- [ ] **Step 1：改 `SubscribeModel` —— 增加 AnyPinned 订阅**

找到 `SubscribeModel` 方法末尾，在 `IsAppFocused` 订阅行之后追加一行 `AnyPinned` 订阅：

原来（约第 200-204 行）：

```csharp
            _model.IsPinned.RegisterWithInitValue(OnPomodoroPinnedChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.GetModel<IGameModel>().IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility())
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }
```

改为：

```csharp
            _model.IsPinned.RegisterWithInitValue(OnPomodoroPinnedChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.GetModel<IGameModel>().IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility())
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned
                .RegisterWithInitValue(_ => RefreshVisibility())
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }
```

- [ ] **Step 2：改 `RefreshVisibility` 公式**

原来（约第 369-375 行）：

```csharp
        private void RefreshVisibility()
        {
            if (_ppRoot == null || _model == null) return;
            bool focused = this.GetModel<IGameModel>().IsAppFocused.Value;
            bool visible = focused || _model.IsPinned.Value;
            _ppRoot.EnableInClassList("pp-hidden", !visible);
        }
```

改为：

```csharp
        private void RefreshVisibility()
        {
            if (_ppRoot == null || _model == null) return;
            bool focused = this.GetModel<IGameModel>().IsAppFocused.Value;
            bool anyPinned = this.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned.Value;
            bool thisPinned = _model.IsPinned.Value;
            // S2 隐藏条件：整窗口置顶(AnyPinned) 且失焦 且本 UI 非 pinned
            bool hidden = !thisPinned && !focused && anyPinned;
            _ppRoot.EnableInClassList("pp-hidden", hidden);
        }
```

- [ ] **Step 3：补 using**

文件顶部 using 区若尚无 `using APP.Pomodoro.System;` 则追加：

```csharp
using APP.Pomodoro.System;
```

（`IWindowVisibilityCoordinatorSystem` 位于该命名空间。）

- [ ] **Step 4：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 5：commit**

```bash
git add Assets/UI_V2/Controller/PomodoroPanelView.cs
git commit -m "$(cat <<'EOF'
refactor(ui): PomodoroPanelView 切到新隐藏公式 hidden = !pinned && !focused && anyPinned

订阅 coordinator.AnyPinned；替换旧 visible = focused || pinned 语义。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8：`PlayerCardController` 切换新隐藏公式

**Files:**
- Modify: `Assets/UI_V2/Controller/PlayerCardController.cs`

- [ ] **Step 1：补 using**

文件顶部 using 区追加：

```csharp
using APP.Pomodoro.System;
```

- [ ] **Step 2：改 `Bind` 增加 AnyPinned 订阅**

原来（约第 187-195 行）：

```csharp
        public void Bind(IPlayerCard card)
        {
            _card = card;
            if (_card == null) return;

            _unRegisters.Add(_card.IsPinned.RegisterWithInitValue(OnPinnedChanged));
            _unRegisters.Add(GameApp.Interface.GetModel<IGameModel>()
                .IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility()));
        }
```

改为：

```csharp
        public void Bind(IPlayerCard card)
        {
            _card = card;
            if (_card == null) return;

            _unRegisters.Add(_card.IsPinned.RegisterWithInitValue(OnPinnedChanged));
            _unRegisters.Add(GameApp.Interface.GetModel<IGameModel>()
                .IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility()));
            _unRegisters.Add(GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>()
                .AnyPinned.RegisterWithInitValue(_ => RefreshVisibility()));
        }
```

- [ ] **Step 3：改 `RefreshVisibility` 公式**

原来（约第 213-219 行）：

```csharp
        private void RefreshVisibility()
        {
            if (_root == null || _card == null) return;
            bool focused = GameApp.Interface.GetModel<IGameModel>().IsAppFocused.Value;
            bool visible = focused || _card.IsPinned.Value;
            _root.EnableInClassList("pc-hidden", !visible);
        }
```

改为：

```csharp
        private void RefreshVisibility()
        {
            if (_root == null || _card == null) return;
            bool focused = GameApp.Interface.GetModel<IGameModel>().IsAppFocused.Value;
            bool anyPinned = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned.Value;
            bool thisPinned = _card.IsPinned.Value;
            // S2 隐藏条件：整窗口置顶(AnyPinned) 且失焦 且本卡非 pinned
            bool hidden = !thisPinned && !focused && anyPinned;
            _root.EnableInClassList("pc-hidden", hidden);
        }
```

- [ ] **Step 4：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 5：commit**

```bash
git add Assets/UI_V2/Controller/PlayerCardController.cs
git commit -m "$(cat <<'EOF'
refactor(ui): PlayerCardController 切到新隐藏公式 hidden = !pinned && !focused && anyPinned

订阅 coordinator.AnyPinned；替换旧 visible = focused || pinned 语义。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9：`DeskWindowController.OnApplicationFocus` 改为发 `Cmd_SetAppFocused`

**Files:**
- Modify: `Assets/UI_V2/Controller/DeskWindowController.cs`

- [ ] **Step 1：改 `OnApplicationFocus`**

原来（约第 95-101 行）：

```csharp
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                this.SendCommand(new Cmd_PomodoroRevertTopmost());
            }
        }
```

改为：

```csharp
        private void OnApplicationFocus(bool hasFocus)
        {
            // 将 Unity 的应用焦点状态统一汇入 IGameModel.IsAppFocused，
            // 由 WindowVisibilityCoordinatorSystem + View 层公式驱动可见性。
            this.SendCommand(new Cmd_SetAppFocused(hasFocus));
        }
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error. `Cmd_PomodoroRevertTopmost` 的唯一调用方已移除——后续 Task 11 删除该 Command 类时就不会报"unused"。

- [ ] **Step 3：commit**

```bash
git add Assets/UI_V2/Controller/DeskWindowController.cs
git commit -m "$(cat <<'EOF'
refactor(ui): DeskWindowController.OnApplicationFocus 统一走 Cmd_SetAppFocused

不再分支处理 hasFocus=true——失焦/得焦两侧都需要写 IsAppFocused。
Cmd_PomodoroRevertTopmost 的调用方至此清零。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10：删除 `IPomodoroModel.IsTopmost`

> **提示：** 从此任务开始进入"去意义化"清理阶段。Task 10-13 删改合并在一个逻辑单元——每个 commit 后编译会有短暂的不对称状态，但由于每个 Task 都是自洽的（接口改了对应实现也改了、持久化字段删了对应读写也删了），每个 commit 后整工程可编译。

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs`
- Modify: `Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs`

- [ ] **Step 1：`IPomodoroModel.cs` 删掉 IsTopmost 行**

原来（约第 29-30 行）：

```csharp
        /// <summary>窗口是否置顶（isTopmost）</summary>
        BindableProperty<bool> IsTopmost { get; }

```

删除这两行（包括其上的注释行和下面的空行，保持与其它 field 间的一致空行节奏）。

- [ ] **Step 2：`PomodoroModel.cs` 删除 `IsTopmost` 字段**

原来（约第 17 行）：

```csharp
        public BindableProperty<bool> IsTopmost { get; } = new BindableProperty<bool>(false);
```

删除这一行。

- [ ] **Step 3：`PomodoroModel.cs` 的 `PomodoroPersistentState` 删 `IsTopmost` 字段**

原来（约第 45 行）：

```csharp
        public bool IsTopmost;
```

删除这一行。

- [ ] **Step 4：`PomodoroModel.cs` 的 `Save()` 删读 IsTopmost**

原来（约第 123 行，`new PomodoroPersistentState { ... }` 初始化列表内）：

```csharp
                IsTopmost = model.IsTopmost.Value,
```

删除这一行。

- [ ] **Step 5：`PomodoroModel.cs` 的 `ApplyState()` 删写 IsTopmost**

原来（约第 167 行）：

```csharp
            model.IsTopmost.Value = state.IsTopmost;
```

删除这一行。

- [ ] **Step 6：MCP `read_console` filter=Error 检查编译**

Expected: 编译会失败，因为：
- `Cmd_PomodoroSetTopmost.cs` 里 `wps.SetTopmost(_isTopmost)` 不需要 Model，但 `WindowPositionSystem.SetTopmost` 里 `model.IsTopmost.Value = isTopmost` 行会报 `IsTopmost` 不存在
- `DeskWindowController.cs` 第 181 行 `_model.IsTopmost.Register(...)` 会失败

**这些编译错会在 Task 11-13 修复。为了避免 Task 10 单独断崖，本 Task 不要求绿编译。** 标记：下一个 Task 完成后重新验证。

如果你在执行 Task 10 时希望保持每个 commit 都绿编译，可以把 Task 10-13 合并成一个大 commit；但 plan 按"每任务一 commit"呈现。

- [ ] **Step 7：commit（接受临时红编译）**

```bash
git add Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs \
        Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs
git commit -m "$(cat <<'EOF'
refactor(model)!: 删除 IPomodoroModel.IsTopmost（去意义化）

isTopmost 行为由 WindowVisibilityCoordinatorSystem 从 AnyPinned 派生，
PomodoroModel 中的独立偏好字段及其持久化字段一并移除。

后续 Task 11-13 会移除 WindowPositionSystem / DeskWindowController /
Cmd_PomodoroSetTopmost|RevertTopmost|JumpToScreenTop 中的调用方。
本 commit 到 Task 13 之间编译处于红状态，Task 13 后恢复绿。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 11：清理 `WindowPositionSystem` 中 IsTopmost 相关代码

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/System/IWindowPositionSystem.cs`
- Modify: `Assets/Scripts/APP/Pomodoro/System/WindowPositionSystem.cs`

- [ ] **Step 1：`IWindowPositionSystem.cs` 删 `JumpToScreenTop / RevertTopmost` 两个成员**

原来：

```csharp
        void SetTopmost(bool isTopmost);


        /// <summary>临时置顶窗口（不改变 Model 偏好），用于阶段切换提醒</summary>
        void JumpToScreenTop();

        /// <summary>将 isTopmost 恢复为 Model 中用户的偏好值</summary>
        void RevertTopmost();
    }
}
```

改为：

```csharp
        void SetTopmost(bool isTopmost);
    }
}
```

- [ ] **Step 2：`WindowPositionSystem.cs` 的 `SetTopmost` 不再写 Model**

原来（约第 63-73 行）：

```csharp
        public void SetTopmost(bool isTopmost)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.IsTopmost.Value = isTopmost;

            if (_uwc != null)
            {
                _uwc.isTopmost = isTopmost;
                Debug.Log($"[WindowPositionSystem] SetTopmost({isTopmost})");
            }
        }
```

改为：

```csharp
        public void SetTopmost(bool isTopmost)
        {
            if (_uwc != null)
            {
                _uwc.isTopmost = isTopmost;
                Debug.Log($"[WindowPositionSystem] SetTopmost({isTopmost})");
            }
        }
```

- [ ] **Step 3：`WindowPositionSystem.cs` 删 `JumpToScreenTop()` 与 `RevertTopmost()` 整块**

原来（约第 75-104 行，包括两个方法的注释、方法体）：

```csharp
        /// <summary>
        /// 临时置顶窗口（不改变 Model.IsTopmost 偏好）。
        /// 用于阶段切换时吸引用户注意。
        /// </summary>
        public void JumpToScreenTop()
        {
            if (_uwc == null)
            {
                return;
            }

            _uwc.isTopmost = true;
            Debug.Log("[WindowPositionSystem] JumpToScreenTop: 临时置顶窗口");
        }

        /// <summary>
        /// 将 isTopmost 恢复为 Model 中用户的偏好值。
        /// 用于用户聚焦窗口后取消临时置顶。
        /// </summary>
        public void RevertTopmost()
        {
            if (_uwc == null)
            {
                return;
            }

            bool preferred = this.GetModel<IPomodoroModel>().IsTopmost.Value;
            _uwc.isTopmost = preferred;
            Debug.Log($"[WindowPositionSystem] RevertTopmost: 恢复置顶={preferred}");
        }
    }
}
```

改为（两个方法整块删除，只保留类闭合）：

```csharp
    }
}
```

- [ ] **Step 4：MCP `read_console` filter=Error 检查编译**

Expected: 剩余错误应仅来自 `Cmd_PomodoroSetTopmost` / `Cmd_PomodoroRevertTopmost` / `Cmd_PomodoroJumpToScreenTop` 三个命令类。Task 12 会删这三个。

- [ ] **Step 5：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/System/IWindowPositionSystem.cs \
        Assets/Scripts/APP/Pomodoro/System/WindowPositionSystem.cs
git commit -m "$(cat <<'EOF'
refactor(system)!: WindowPositionSystem 清理 IsTopmost 余留

- SetTopmost 不再写 Model.IsTopmost（字段已删）
- 删除 JumpToScreenTop / RevertTopmost 两个方法（新状态机下无意义）
- 接口同步精简

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 12：删除 `Cmd_PomodoroSetTopmost / RevertTopmost / JumpToScreenTop`

**Files:**
- Delete: `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroSetTopmost.cs`
- Delete: `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroRevertTopmost.cs`
- Delete: `Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroJumpToScreenTop.cs`

- [ ] **Step 1：删除三个命令源文件及其 .meta**

```bash
rm Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroSetTopmost.cs
rm Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroSetTopmost.cs.meta
rm Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroRevertTopmost.cs
rm Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroRevertTopmost.cs.meta
rm Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroJumpToScreenTop.cs
rm Assets/Scripts/APP/Pomodoro/Command/Cmd_PomodoroJumpToScreenTop.cs.meta
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 剩余错误应仅来自 `DeskWindowController.cs` 第 181 行 `_model.IsTopmost.Register`。Task 13 修复。

若出现非预期错误（例如 `_Archived/PomodoroPanelController.cs` 报错），说明归档文件参与了编译——回到 Task 13 的 Step 4 处理。

- [ ] **Step 3：commit**

```bash
git add -A Assets/Scripts/APP/Pomodoro/Command/
git commit -m "$(cat <<'EOF'
refactor(command)!: 删除 Cmd_PomodoroSetTopmost/RevertTopmost/JumpToScreenTop

IsTopmost 去意义化后，上述三个命令失去语义。由新状态机
(WindowVisibilityCoordinatorSystem + Cmd_SetAppFocused) 取代。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 13：清理 `DeskWindowController` 中 IsTopmost 订阅

**Files:**
- Modify: `Assets/UI_V2/Controller/DeskWindowController.cs`

- [ ] **Step 1：删 `RegisterPersistenceCallbacks` 中 IsTopmost 订阅行**

原来（约第 181-182 行）：

```csharp
            _model.IsTopmost.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
```

整块删除。

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error. 如果仍有 `IsTopmost` 相关错误，说明 Task 10-12 某步遗漏。

- [ ] **Step 3：验证 `_Archived/PomodoroPanelController.cs` 不参与编译**

Run: `Bash grep -l "PomodoroPanelController.cs" Assets/Scripts/APP/Pomodoro/*.asmdef Assets/Scripts/APP/*.asmdef 2>/dev/null`
Expected: 无输出（即没有 asmdef 显式引用）

或直接：
Run: `Bash grep -rn "\"PomodoroPanelController\"" Assets/Scripts/APP/Pomodoro/Controller/_Archived/ 2>/dev/null`
Expected: 无输出

如果 MCP `read_console` 能看到 `_Archived/PomodoroPanelController.cs` 相关错误，说明该文件参与编译——应额外执行：

```bash
rm -rf Assets/Scripts/APP/Pomodoro/Controller/_Archived
```

（或仅删除 `.cs` 与 `.meta`）。加入本 commit。

- [ ] **Step 4：commit**

```bash
git add Assets/UI_V2/Controller/DeskWindowController.cs
git commit -m "$(cat <<'EOF'
refactor(ui)!: DeskWindowController 移除 IsTopmost 持久化订阅

至此 IsTopmost 去意义化清理结束，整工程编译绿。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 5：整工程编译最终验证**

Run: `mcp__UnityMCP__read_console` filter=Error,Warning
Expected: 0 error。若有 Warning 可接受但记录下来。

---

## Task 14：`WindowVisibilityCoordinatorSystemTests` 单测

**Files:**
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/WindowVisibilityCoordinatorSystemTests.cs`

- [ ] **Step 1：写测试**

```csharp
using APP.Pomodoro;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Tests.PlayerCardTests
{
    public sealed class WindowVisibilityCoordinatorSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetArchitecture();
            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [TearDown]
        public void TearDown()
        {
            ResetArchitecture();
            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        private static void ResetArchitecture()
        {
            var current = typeof(GameApp)
                .GetField("mArchitecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IArchitecture;
            current?.Deinit();
        }

        [Test]
        public void AnyPinned_Initially_False()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_TogglesWhenPomodoroIsPinnedChanges()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var pomodoro = GameApp.Interface.GetModel<IPomodoroModel>();

            pomodoro.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            pomodoro.IsPinned.Value = false;
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_TogglesWhenPlayerCardIsPinnedChanges()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();

            var c = cards.AddOrGet("p1");
            Assert.IsFalse(coord.AnyPinned.Value);

            c.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            c.IsPinned.Value = false;
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_StaysTrueWhenAnyOfMultipleSourcesIsPinned()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var pomodoro = GameApp.Interface.GetModel<IPomodoroModel>();
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();

            var p1 = cards.AddOrGet("p1");
            var p2 = cards.AddOrGet("p2");

            pomodoro.IsPinned.Value = true;
            p1.IsPinned.Value = true;
            p2.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            // 取消其中两个，仍为 true
            p1.IsPinned.Value = false;
            pomodoro.IsPinned.Value = false;
            Assert.IsTrue(coord.AnyPinned.Value);

            // 最后一个取消 → false
            p2.IsPinned.Value = false;
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_RemovingPinnedCard_DropsToFalse()
        {
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();

            var c = cards.AddOrGet("p1");
            c.IsPinned.Value = true;
            Assert.IsTrue(coord.AnyPinned.Value);

            cards.Remove("p1");
            Assert.IsFalse(coord.AnyPinned.Value);
        }

        [Test]
        public void AnyPinned_AddingAlreadyPinnedCard_PromotesToTrue()
        {
            // 场景：Card 持久化记录中 pinned=true，重新 AddOrGet 时 coordinator 要识别
            var cards = GameApp.Interface.GetModel<IPlayerCardModel>();
            var c = cards.AddOrGet("p1");
            c.IsPinned.Value = true;
            cards.Remove("p1"); // 落盘 pinned=true

            // 模拟"重新加入"：AddOrGet 会从持久化恢复 IsPinned=true
            var coord = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>();
            var c2 = cards.AddOrGet("p1");
            Assert.IsTrue(c2.IsPinned.Value, "持久化应恢复 pinned=true");
            Assert.IsTrue(coord.AnyPinned.Value, "coordinator 应识别到 pinned 卡重新加入");
        }
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 3：MCP `run_tests` 跑 PlayerCardTests**

Run: `mcp__UnityMCP__run_tests` 参数：
```json
{ "testMode": "EditMode", "testNames": ["APP.Tests.PlayerCardTests.WindowVisibilityCoordinatorSystemTests"] }
```

Expected: 全部 PASS。若 `AnyPinned_AddingAlreadyPinnedCard_PromotesToTrue` 失败，检查 `WindowVisibilityCoordinatorSystem.OnCardAdded` 是否先 `SubscribeCard` 再 `Recalculate`（顺序很重要，因为持久化恢复的 IsPinned 初值是通过 `Register(_ => Recalculate())` 的首次回调之外的路径回填的）。

- [ ] **Step 4：commit**

```bash
git add Assets/Tests/EditMode/PlayerCardTests/Editor/WindowVisibilityCoordinatorSystemTests.cs
git commit -m "$(cat <<'EOF'
test(system): WindowVisibilityCoordinatorSystem 订阅聚合与动态增删

覆盖：
- 初值 false
- Pomodoro.IsPinned 切换联动
- PlayerCard.IsPinned 切换联动
- 多源同时 pinned
- Remove pinned 卡回落 false
- 重新 AddOrGet 持久化 pinned 卡回弹 true

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 15：`QIsAnyPinnedTests`

**Files:**
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/QIsAnyPinnedTests.cs`

- [ ] **Step 1：写测试**

```csharp
using APP.Pomodoro;
using APP.Pomodoro.Model;
using APP.Pomodoro.Queries;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Tests.PlayerCardTests
{
    public sealed class QIsAnyPinnedTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetArchitecture();
            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [TearDown]
        public void TearDown()
        {
            ResetArchitecture();
            PlayerPrefs.DeleteKey("CPA.PlayerCards");
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        private static void ResetArchitecture()
        {
            var current = typeof(GameApp)
                .GetField("mArchitecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IArchitecture;
            current?.Deinit();
        }

        [Test]
        public void Query_Returns_False_When_Nothing_Pinned()
        {
            bool result = GameApp.Interface.SendQuery(new Q_IsAnyPinned());
            Assert.IsFalse(result);
        }

        [Test]
        public void Query_Returns_True_When_Pomodoro_Pinned()
        {
            GameApp.Interface.GetModel<IPomodoroModel>().IsPinned.Value = true;
            bool result = GameApp.Interface.SendQuery(new Q_IsAnyPinned());
            Assert.IsTrue(result);
        }

        [Test]
        public void Query_Returns_True_When_Any_Card_Pinned()
        {
            var c = GameApp.Interface.GetModel<IPlayerCardModel>().AddOrGet("p1");
            c.IsPinned.Value = true;
            bool result = GameApp.Interface.SendQuery(new Q_IsAnyPinned());
            Assert.IsTrue(result);
        }
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 3：MCP `run_tests` 跑 QIsAnyPinnedTests**

Run: `mcp__UnityMCP__run_tests` 参数：
```json
{ "testMode": "EditMode", "testNames": ["APP.Tests.PlayerCardTests.QIsAnyPinnedTests"] }
```

Expected: 全部 PASS

- [ ] **Step 4：commit**

```bash
git add Assets/Tests/EditMode/PlayerCardTests/Editor/QIsAnyPinnedTests.cs
git commit -m "$(cat <<'EOF'
test(query): Q_IsAnyPinned 覆盖三种 pin 源组合

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 16：扩展 `PinCommandTests` 覆盖 `Cmd_SetAppFocused`

**Files:**
- Modify: `Assets/Tests/EditMode/PlayerCardTests/Editor/PinCommandTests.cs`

- [ ] **Step 1：在现有 `PinCommandTests` 类末尾追加三个测试**

在类的闭合 `}` 前插入：

```csharp
        [Test]
        public void Cmd_SetAppFocused_WritesFalse()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IGameModel>();
            Assert.IsTrue(model.IsAppFocused.Value, "IsAppFocused 默认应为 true");

            arch.SendCommand(new Cmd_SetAppFocused(false));

            Assert.IsFalse(model.IsAppFocused.Value);
        }

        [Test]
        public void Cmd_SetAppFocused_WritesTrue()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IGameModel>();
            model.IsAppFocused.Value = false;

            arch.SendCommand(new Cmd_SetAppFocused(true));

            Assert.IsTrue(model.IsAppFocused.Value);
        }

        [Test]
        public void Cmd_SetAppFocused_TriggersSubscriber()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IGameModel>();
            bool? received = null;
            model.IsAppFocused.Register(v => received = v);

            arch.SendCommand(new Cmd_SetAppFocused(false));

            Assert.IsTrue(received.HasValue);
            Assert.IsFalse(received.Value);
        }
```

- [ ] **Step 2：MCP `read_console` filter=Error 检查编译**

Expected: 0 error

- [ ] **Step 3：MCP `run_tests` 跑 PinCommandTests**

Run: `mcp__UnityMCP__run_tests` 参数：
```json
{ "testMode": "EditMode", "testNames": ["APP.Tests.PlayerCardTests.PinCommandTests"] }
```

Expected: 旧三条 + 新三条共 6 条全 PASS

- [ ] **Step 4：commit**

```bash
git add Assets/Tests/EditMode/PlayerCardTests/Editor/PinCommandTests.cs
git commit -m "$(cat <<'EOF'
test(command): PinCommandTests 加 Cmd_SetAppFocused 行为断言

默认 true / 写 false / 写 true / 触发订阅。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 17：整套 EditMode 回归 + 手动回归清单

**Files:**
- 无新建/修改，仅验证

- [ ] **Step 1：整套 PlayerCardTests 一起跑**

Run: `mcp__UnityMCP__run_tests` 参数：
```json
{ "testMode": "EditMode", "testNames": ["APP.Tests.PlayerCardTests"] }
```

Expected: 所有旧测试 + 新增 3 套（WindowVisibilityCoordinatorSystemTests、QIsAnyPinnedTests、PinCommandTests 扩展）全 PASS

若有 failure：
- 涉及持久化 key 污染：确认 SetUp/TearDown 的 `PlayerPrefs.DeleteKey` 两个 key 都有
- 涉及 Architecture 单例污染：确认 SetUp/TearDown 的 `ResetArchitecture()`

- [ ] **Step 2：跑 NetworkTests（PlayerCardModel 被 NetworkSystem 写入，回归）**

Run: `mcp__UnityMCP__run_tests` 参数：
```json
{ "testMode": "EditMode", "testNames": ["APP.Tests.NetworkTests"] }
```

Expected: 全 PASS（本次改动不触及 NetworkSystem，但回归保险）

- [ ] **Step 3：手动回归（macOS 真机）**

依次执行并勾选：

- [ ] 冷启动（全部 unpinned）：`_uwc.isTopmost = false`；其他 app 可正常盖过 Unity
- [ ] Pin 番茄钟面板 → `_uwc.isTopmost = true`；Unity 窗口浮到最上
- [ ] 点穿透到其他 app（如 Finder 空白区）→ Unity 失焦；非 pinned 卡片消失；番茄钟面板（已 pin）保留
- [ ] Cmd+Tab 切回 Unity → 所有非 pinned 卡片回来
- [ ] 再次 unpin 番茄钟面板 → `_uwc.isTopmost = false`；所有 UI 保持可见（S0 语义）
- [ ] 加入远端玩家（用 `NetworkSimulatorWindow`） → 卡片出现；pin/unpin 联动 isTopmost
- [ ] S2 下 pin 唯一卡片的 pin 按钮取消 pin：点击 → Unity 得焦 → 进 S1 → pin cmd 执行 → 进 S0；所有 UI 保留可见（无 UI 丢失）
- [ ] 重启应用：番茄钟 IsPinned / Card IsPinned 从持久化恢复；`isTopmost` 同步更新

- [ ] **Step 4：MCP `read_console` filter=Error,Warning 最终检查**

Expected: 0 error。Warning 可接受（例如 Editor 独有告警）但需人工确认非新增。

- [ ] **Step 5：无代码改动则不 commit。若 Step 3 发现问题，另起 fix commit**

（本 Task 不产生 commit，只做验证。）

---

## Self-Review 摘要（plan 作者预填）

**Spec 覆盖：**
- ✅ 状态机（§状态机） → Task 3（coordinator 的 `Recalculate()` + Task 7/8 的公式）
- ✅ `AppFocus 兼容层` → Task 1 (Cmd_SetAppFocused) + Task 9 (DeskWindowController)
- ✅ `IWindowVisibilityCoordinatorSystem` → Task 2/3
- ✅ `Q_IsAnyPinned` → Task 5
- ✅ `PomodoroModel.IsTopmost` 去意义化 → Task 10
- ✅ 清理 Cmd_PomodoroSetTopmost / RevertTopmost / JumpToScreenTop → Task 12
- ✅ 清理 WindowPositionSystem.JumpToScreenTop / RevertTopmost → Task 11
- ✅ `PomodoroPanelView` / `PlayerCardController` 切换新公式 → Task 7/8
- ✅ 测试（Coordinator / Query / Cmd_SetAppFocused） → Task 14/15/16
- ✅ 手动回归清单 → Task 17 Step 3

**未在 plan 内的 spec 项：**
- Spec §非目标里的"菜单栏图标/快捷键"——非目标本来就不做，✓
- Spec §已识别风险 #2 "`OnApplicationFocus` 在 UniWindowController 配置下的触发稳定性"——留在 Task 17 手动回归中观察

**与 spec 的偏离项：** 已在本文件顶部"与 Spec 的偏离"章节记录，共 2 项（AppFocusBridge 不新增、coordinator 启动时序交给 Cmd_PomodoroInitialize）。
