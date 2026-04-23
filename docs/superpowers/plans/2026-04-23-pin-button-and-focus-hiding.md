# Pin 按钮 + 失焦隐藏 + PlayerCardModel 重构 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给主面板番茄钟与每张远端玩家卡片加"图钉"按钮，实现"失焦 && 未 pin → 隐藏自己"；顺带把 `IPlayerCardPositionModel` 重构为 `IPlayerCardModel` + `IPlayerCard` 实例容器；新增 Editor 调试窗口手动注入 Model 值。

**Architecture:** QFramework 分层。新增 `IGameModel.IsAppFocused`（全局）；`IPomodoroModel` 追加 `IsPinned`；重构 `IPlayerCardPositionModel` 为 `IPlayerCardModel` + `IPlayerCard`（每个 `IPlayerCard` 持 `Position` / `IsPinned` 两个 Bindable，运行时实例随玩家在线/离线生灭；持久化仓库独立驻留 Model 内部）。View 层每个 UI 订阅 `(IsAppFocused, 自身 IsPinned)` → 本地 `EnableInClassList("xxx-hidden")`。pin 按钮点击 → `Cmd_SetXxxPinned` → Model 写入 → Bindable 推送 → View 刷新。

**Tech Stack:** Unity 6000.0.25f1、UI Toolkit、QFramework v1.0、PlayerPrefs JSON 持久化、UnityEditor IMGUI（调试窗口）、NUnit（EditMode 测试）。

**Unity 约束：** 本项目里"测试先于实现"的 TDD 会触发全工程 CS0234 编译失败（测试引用尚未存在的类型）。因此每个任务**先写实现、编译通过、commit，再写/改测试**。Spec §8.1 明确约定。

**Spec：** `docs/superpowers/specs/2026-04-23-pin-button-and-focus-hiding-design.md`

---

## 文件结构

### 新建

| 路径 | 职责 |
|---|---|
| `Assets/Scripts/APP/Pomodoro/Model/IGameModel.cs` | 全局 Model 接口 |
| `Assets/Scripts/APP/Pomodoro/Model/GameModel.cs` | 实现 |
| `Assets/Scripts/APP/Pomodoro/Model/IPlayerCard.cs` | 单卡片实例接口 |
| `Assets/Scripts/APP/Pomodoro/Model/IPlayerCardModel.cs` | 卡片容器 + 持久化接口 |
| `Assets/Scripts/APP/Pomodoro/Model/PlayerCardModel.cs` | 实现（含内部 `PlayerCard` 类与持久化仓库） |
| `Assets/Scripts/APP/Pomodoro/Event/PlayerCardEvents.cs` | `E_PlayerCardAdded` / `E_PlayerCardRemoved` |
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPinned.cs` | 写 `IPomodoroModel.IsPinned` |
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPinned.cs` | 写 `IPlayerCard.IsPinned` |
| `Assets/Scripts/APP/Pomodoro/Queries/Q_ListPlayerCards.cs` | 列出所有在线卡片 |
| `Assets/Scripts/Editor/ModelDebugWindow.cs` | Tools/Model 调试器 |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardModelTests.cs` | 新 Model 单测 |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/GameModelTests.cs` | GameModel 单测 |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/PinCommandTests.cs` | Command 行为测试 |

### 修改

| 路径 | 改动要点 |
|---|---|
| `Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs` | 增 `BindableProperty<bool> IsPinned` |
| `Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs` | 实现 `IsPinned`；`PomodoroPersistence` 存取 `IsPinned` |
| `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPosition.cs` | 改走 `IPlayerCardModel.Find(id).Position` |
| `Assets/Scripts/APP/Pomodoro/GameApp.cs` | 注册 `IGameModel` 与 `IPlayerCardModel`，移除旧 `IPlayerCardPositionModel` |
| `Assets/Scripts/APP/Network/System/NetworkSystem.cs` | Join/Leave/Snapshot/Clear 路径同步 `IPlayerCardModel.AddOrGet/Remove` |
| `Assets/UI_V2/Controller/PomodoroPanelView.cs` | 绑定 `pp-pin-btn`、订阅 `IsAppFocused` + `IsPinned` |
| `Assets/UI_V2/Controller/PlayerCardController.cs` | 升格为 `IController`，新增 `Bind/Dispose`，绑定 `pc-pin-btn` |
| `Assets/UI_V2/Controller/PlayerCardManager.cs` | 数据源切到 `E_PlayerCardAdded/Removed`；Card 创建时调 `Bind` |
| `Assets/UI_V2/Controller/DeskWindowController.cs` | 删除 `_pomodoroPanelView.SetVisible(true)` 一行 |
| `Assets/UI_V2/Styles/PlayerCard.uss` | 末尾追加 `.pc-hidden { display: none; }` |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardManagerTests.cs` | 引用 `IPlayerCardModel`，API 走 `Find/AddOrGet` |

### 删除

| 路径 | 原因 |
|---|---|
| `Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs` | 被 `IPlayerCardModel` 替代 |
| `Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs` | 同上 |
| `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs` | 测试对象已删 |

---

## Task 1：新增 IGameModel 与 GameModel

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Model/IGameModel.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Model/GameModel.cs`

- [ ] **Step 1：写 `IGameModel.cs`**

```csharp
using QFramework;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 全局 Model：承载跨领域的运行时状态。
    /// 当前仅含 IsAppFocused；失焦真实数据源留待后续会话接入，
    /// 本次由 Editor 调试窗口（Tools/Model 调试器）手动赋值。
    /// </summary>
    public interface IGameModel : IModel
    {
        BindableProperty<bool> IsAppFocused { get; }
    }
}
```

- [ ] **Step 2：写 `GameModel.cs`**

```csharp
using QFramework;

namespace APP.Pomodoro.Model
{
    public sealed class GameModel : AbstractModel, IGameModel
    {
        public BindableProperty<bool> IsAppFocused { get; } = new BindableProperty<bool>(true);

        protected override void OnInit() { }
    }
}
```

- [ ] **Step 3：MCP `read_console` filter=Error 检查编译**

期望：0 error。如出现 "The type or namespace name ..." 检查 using 路径。

- [ ] **Step 4：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Model/IGameModel.cs \
        Assets/Scripts/APP/Pomodoro/Model/GameModel.cs
git commit -m "feat(model): 新增 IGameModel.IsAppFocused 全局聚焦态

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

*（不生成 `.meta` 文件——Unity 首次打开会自己补 GUID。不要手编 GUID。）*

---

## Task 2：扩展 IPomodoroModel.IsPinned + 持久化

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs`
- Modify: `Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs`

- [ ] **Step 1：在 `IPomodoroModel` 接口中追加字段**

在 `IsTopmost` 属性之后（文件约第 29 行），插入：

```csharp
        /// <summary>番茄钟面板是否被 pin（不因失焦隐藏）。默认 false。</summary>
        BindableProperty<bool> IsPinned { get; }
```

- [ ] **Step 2：在 `PomodoroModel` 类中加默认值实现**

在 `PomodoroModel.cs` 第 17 行 `IsTopmost` 之后插入：

```csharp
        public BindableProperty<bool> IsPinned { get; } = new BindableProperty<bool>(false);
```

- [ ] **Step 3：扩展 `PomodoroPersistentState`（约第 44 行）**

在 `IsTopmost` 字段之后追加：

```csharp
        public bool IsPinned;
```

- [ ] **Step 4：在 `PomodoroPersistence.Save`（约第 121 行）state 初始化块中追加**

在 `IsTopmost = model.IsTopmost.Value,` 之后追加：

```csharp
                IsPinned = model.IsPinned.Value,
```

- [ ] **Step 5：在 `PomodoroPersistence.ApplyState`（约第 164 行）中追加**

在 `model.IsTopmost.Value = state.IsTopmost;` 之后追加：

```csharp
            model.IsPinned.Value = state.IsPinned;
```

- [ ] **Step 6：扩展 `DeskWindowController.RegisterPersistenceCallbacks` 让 IsPinned 变化触发 SaveState**

在 `DeskWindowController.cs` 约第 183 行（`_model.IsTopmost.Register(...)` 之后）追加：

```csharp
            _model.IsPinned.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
```

- [ ] **Step 7：MCP `read_console` filter=Error 检查**

期望：0 error。

- [ ] **Step 8：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs \
        Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs \
        Assets/UI_V2/Controller/DeskWindowController.cs
git commit -m "feat(pomodoro): 新增 IPomodoroModel.IsPinned 并接入持久化

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3：新建 IPlayerCard + IPlayerCardModel 骨架（不替换旧 Model，先并存编译通过）

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Model/IPlayerCard.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Model/IPlayerCardModel.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Model/PlayerCardModel.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Event/PlayerCardEvents.cs`

本任务只新增文件、不动旧 `IPlayerCardPositionModel`。这保证本次 commit 后全工程仍能编译通过。

- [ ] **Step 1：写 `IPlayerCard.cs`**

```csharp
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 单个远端玩家卡片的运行时状态实例。
    /// PlayerId 终身不变；Position 与 IsPinned 以 BindableProperty 向外提供订阅。
    /// </summary>
    public interface IPlayerCard
    {
        string PlayerId { get; }
        BindableProperty<Vector2> Position { get; }
        BindableProperty<bool> IsPinned { get; }
    }
}
```

- [ ] **Step 2：写 `IPlayerCardModel.cs`**

```csharp
using System.Collections.Generic;

using QFramework;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 远端玩家卡片容器 + 持久化仓库。
    /// Cards 仅含当前在线玩家的实例（X 语义）；仓库记录独立驻留 Model 内部，离线后保留供再次 Join 时恢复。
    /// 持久化 key: "CPA.PlayerCards"。
    /// </summary>
    public interface IPlayerCardModel : IModel
    {
        IReadOnlyList<IPlayerCard> Cards { get; }
        IPlayerCard Find(string playerId);
        IPlayerCard AddOrGet(string playerId);
        void Remove(string playerId);
    }
}
```

- [ ] **Step 3：写 `PlayerCardEvents.cs`**

```csharp
namespace APP.Pomodoro.Event
{
    public readonly struct E_PlayerCardAdded
    {
        public readonly string PlayerId;
        public E_PlayerCardAdded(string playerId) => PlayerId = playerId;
    }

    public readonly struct E_PlayerCardRemoved
    {
        public readonly string PlayerId;
        public E_PlayerCardRemoved(string playerId) => PlayerId = playerId;
    }
}
```

- [ ] **Step 4：写 `PlayerCardModel.cs`（完整实现）**

```csharp
using System;
using System.Collections.Generic;
using APP.Pomodoro.Event;
using APP.Utility;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    public sealed class PlayerCardModel : AbstractModel, IPlayerCardModel
    {
        private const string StorageKey = "CPA.PlayerCards";

        // 持久化仓库（所有曾经出现过的玩家，保留 Position/IsPinned 最近一次值）
        private readonly Dictionary<string, PersistedData> _store = new Dictionary<string, PersistedData>();

        // 当前在线实例表
        private readonly Dictionary<string, PlayerCardEntry> _entries = new Dictionary<string, PlayerCardEntry>();

        // 对外暴露的只读视图（每次变化重建一次；Cards 列表数量小，代价可忽略）
        private IReadOnlyList<IPlayerCard> _cardsView = Array.Empty<IPlayerCard>();

        public IReadOnlyList<IPlayerCard> Cards => _cardsView;

        protected override void OnInit()
        {
            var storage = this.GetUtility<IStorageUtility>();
            string json = storage?.LoadString(StorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                var env = JsonUtility.FromJson<Envelope>(json);
                if (env?.entries == null) return;
                for (int i = 0; i < env.entries.Length; i++)
                {
                    var e = env.entries[i];
                    if (string.IsNullOrEmpty(e.id)) continue;
                    _store[e.id] = new PersistedData
                    {
                        Position = new Vector2(e.x, e.y),
                        IsPinned = e.pinned,
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerCardModel] 解析持久化数据失败：{ex.Message}");
            }
        }

        public IPlayerCard Find(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            return _entries.TryGetValue(playerId, out var entry) ? entry.Card : null;
        }

        public IPlayerCard AddOrGet(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return null;
            if (_entries.TryGetValue(playerId, out var existing))
            {
                return existing.Card;
            }

            Vector2 pos = Vector2.zero;
            bool pinned = false;
            if (_store.TryGetValue(playerId, out var saved))
            {
                pos = saved.Position;
                pinned = saved.IsPinned;
            }

            var card = new PlayerCard(playerId, pos, pinned);

            var entry = new PlayerCardEntry { Card = card };
            entry.PositionUnRegister = card.Position.Register(v =>
            {
                _store[playerId] = new PersistedData { Position = v, IsPinned = card.IsPinned.Value };
                Persist();
            });
            entry.PinnedUnRegister = card.IsPinned.Register(v =>
            {
                _store[playerId] = new PersistedData { Position = card.Position.Value, IsPinned = v };
                Persist();
            });

            _entries[playerId] = entry;
            RebuildView();

            this.SendEvent(new E_PlayerCardAdded(playerId));
            return card;
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (!_entries.TryGetValue(playerId, out var entry)) return;

            entry.PositionUnRegister?.UnRegister();
            entry.PinnedUnRegister?.UnRegister();

            // 落盘实例当前值（Register 回调已实时写入，这里是兜底）
            _store[playerId] = new PersistedData
            {
                Position = entry.Card.Position.Value,
                IsPinned = entry.Card.IsPinned.Value,
            };
            Persist();

            _entries.Remove(playerId);
            RebuildView();

            this.SendEvent(new E_PlayerCardRemoved(playerId));
        }

        private void RebuildView()
        {
            var list = new List<IPlayerCard>(_entries.Count);
            foreach (var kv in _entries) list.Add(kv.Value.Card);
            _cardsView = list;
        }

        private void Persist()
        {
            var storage = this.GetUtility<IStorageUtility>();
            if (storage == null) return;

            var env = new Envelope { entries = new Entry[_store.Count] };
            int idx = 0;
            foreach (var kv in _store)
            {
                env.entries[idx++] = new Entry
                {
                    id = kv.Key,
                    x = kv.Value.Position.x,
                    y = kv.Value.Position.y,
                    pinned = kv.Value.IsPinned,
                };
            }
            storage.SaveString(StorageKey, JsonUtility.ToJson(env));
            storage.Flush();
        }

        // ─── 内部类型 ────────────────────────────────────────────

        private struct PersistedData
        {
            public Vector2 Position;
            public bool IsPinned;
        }

        private sealed class PlayerCardEntry
        {
            public PlayerCard Card;
            public IUnRegister PositionUnRegister;
            public IUnRegister PinnedUnRegister;
        }

        private sealed class PlayerCard : IPlayerCard
        {
            public string PlayerId { get; }
            public BindableProperty<Vector2> Position { get; }
            public BindableProperty<bool> IsPinned { get; }

            public PlayerCard(string playerId, Vector2 pos, bool pinned)
            {
                PlayerId = playerId;
                Position = new BindableProperty<Vector2>(pos);
                IsPinned = new BindableProperty<bool>(pinned);
            }
        }

        [Serializable]
        private struct Entry
        {
            public string id;
            public float x;
            public float y;
            public bool pinned;
        }

        [Serializable]
        private sealed class Envelope
        {
            public Entry[] entries = Array.Empty<Entry>();
        }
    }
}
```

- [ ] **Step 5：MCP `read_console` filter=Error**

期望：0 error。注意 `IUnRegister`、`IStorageUtility`、`IModel` 来自 QFramework / `APP.Utility`。

- [ ] **Step 6：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Model/IPlayerCard.cs \
        Assets/Scripts/APP/Pomodoro/Model/IPlayerCardModel.cs \
        Assets/Scripts/APP/Pomodoro/Model/PlayerCardModel.cs \
        Assets/Scripts/APP/Pomodoro/Event/PlayerCardEvents.cs
git commit -m "feat(model): 新增 IPlayerCardModel + IPlayerCard 容器与生命周期事件

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4：迁移 — 注册切换 + 所有引用点切换 + 旧 Model 删除

本任务是整个重构的关键迁移步骤。**一次 commit** 内完成所有切换，避免中间编译状态不一致。

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/GameApp.cs`
- Modify: `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPosition.cs`
- Modify: `Assets/UI_V2/Controller/PlayerCardManager.cs`
- Modify: `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardManagerTests.cs`
- Delete: `Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs`
- Delete: `Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs.meta`
- Delete: `Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs`
- Delete: `Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs.meta`
- Delete: `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs`
- Delete: `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs.meta`

- [ ] **Step 1：改 `GameApp.cs`（约 20-21 行 Model 注册区）**

定位：

```csharp
RegisterModel<IPlayerCardPositionModel>(new PlayerCardPositionModel());
```

替换为（同时加 IGameModel 注册）：

```csharp
RegisterModel<IGameModel>(new GameModel());
RegisterModel<IPlayerCardModel>(new PlayerCardModel());
```

确保在现有的 `RegisterModel<ISessionMemoryModel>(...)` 之前/之后都可；保持和其它 `RegisterModel` 并列即可。

- [ ] **Step 2：改 `Cmd_SetPlayerCardPosition.cs`**

整体替换为：

```csharp
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    public sealed class Cmd_SetPlayerCardPosition : AbstractCommand
    {
        private readonly string _playerId;
        private readonly Vector2 _position;

        public Cmd_SetPlayerCardPosition(string playerId, Vector2 position)
        {
            _playerId = playerId;
            _position = position;
        }

        protected override void OnExecute()
        {
            var card = this.GetModel<IPlayerCardModel>().Find(_playerId);
            if (card == null)
            {
                Debug.LogWarning($"[Cmd_SetPlayerCardPosition] 未找到 playerId={_playerId}");
                return;
            }
            card.Position.Value = _position;
        }
    }
}
```

- [ ] **Step 3：改 `PlayerCardManager.cs` 两处引用**

**第一处**：`AddOrUpdate` 内约第 145 行

定位：

```csharp
            var posModel = this.GetModel<IPlayerCardPositionModel>();
            if (posModel != null && !posModel.TryGet(data.PlayerId, out _))
            {
                this.SendCommand(new Cmd_SetPlayerCardPosition(data.PlayerId, pos));
            }
```

替换为：

```csharp
            var cardModel = this.GetModel<IPlayerCardModel>();
            var card = cardModel?.AddOrGet(data.PlayerId);
            // 首次出现（仓库无位置记录）：NextSlot 结果写回
            if (card != null && card.Position.Value == Vector2.zero)
            {
                this.SendCommand(new Cmd_SetPlayerCardPosition(data.PlayerId, pos));
            }
```

**第二处**：`ResolveInitialPosition` 约第 200 行

定位：

```csharp
        private Vector2 ResolveInitialPosition(string playerId)
        {
            var posModel = this.GetModel<IPlayerCardPositionModel>();
            if (posModel != null && posModel.TryGet(playerId, out Vector2 saved))
            {
                return saved;
            }
            return NextSlot();
        }
```

替换为：

```csharp
        private Vector2 ResolveInitialPosition(string playerId)
        {
            var cardModel = this.GetModel<IPlayerCardModel>();
            var card = cardModel?.Find(playerId);
            if (card != null && card.Position.Value != Vector2.zero)
            {
                return card.Position.Value;
            }
            return NextSlot();
        }
```

> 注：`Vector2.zero` 在这里承担 "无持久化记录" 的 sentinel——新玩家默认 `Position = (0,0)`，而布局算法绝不产出 `(0,0)`（`FirstAnchor = (40,40)`），所以 `!= Vector2.zero` 判断安全。后续如果要区分"未设置"与"设置为 (0,0)"，可把默认改为 `Vector2.negativeInfinity`，但当前无需求。

- [ ] **Step 4：改 `PlayerCardManagerTests.cs` 里所有 `IPlayerCardPositionModel` 引用**

定位文件（整文件扫 3 处）：

```csharp
var posModel = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
```

替换为：

```csharp
var cardModel = GameApp.Interface.GetModel<IPlayerCardModel>();
```

并同步把 `posModel.Set(id, pos)` / `posModel.TryGet(id, out pos)` / `posModel.Remove(id)` 调用改为：

```csharp
// Set：
var card = cardModel.AddOrGet(id);
card.Position.Value = pos;

// TryGet：
var card = cardModel.Find(id);
bool exists = card != null;
Vector2 pos = card?.Position.Value ?? default;

// Remove：
cardModel.Remove(id);
```

*（具体替换处位置见 `PlayerCardManagerTests.cs` 里包含 `IPlayerCardPositionModel` 的行，一行一行机械改。）*

- [ ] **Step 5：删除旧文件**

```bash
rm Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs \
   Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs.meta \
   Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs \
   Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs.meta \
   Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs \
   Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs.meta
```

- [ ] **Step 6：MCP `read_console` filter=Error**

期望：0 error。如果报错，常见原因：
- `PlayerCardManagerTests.cs` 里残留 `posModel` 调用 → 继续找
- `NetworkSystem.cs` 等其它文件意外引用了旧 Model → grep 全仓 `IPlayerCardPositionModel` 确认 0 匹配
- `GameApp.cs` 未 `using APP.Pomodoro.Model` / `using APP.Utility` → 检查 using

- [ ] **Step 7：MCP `run_tests` → `PlayerCardTests`**

期望：已保留的测试用例全绿（`PlayerCardManagerTests` 的引用被改，断言语义不变）。

- [ ] **Step 8：commit**

```bash
git add -A Assets/Scripts/APP/Pomodoro/GameApp.cs \
          Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPosition.cs \
          Assets/UI_V2/Controller/PlayerCardManager.cs \
          Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardManagerTests.cs \
          Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs \
          Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs.meta \
          Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs \
          Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs.meta \
          Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs \
          Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs.meta
git commit -m "refactor(model): 迁移 IPlayerCardPositionModel → IPlayerCardModel，注册 IGameModel

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5：新增 pin Commands（Pomodoro + PlayerCard）

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPinned.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPinned.cs`

- [ ] **Step 1：写 `Cmd_SetPomodoroPinned.cs`**

```csharp
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 切换番茄钟面板的 pin 状态（不因失焦隐藏）。
    /// </summary>
    public sealed class Cmd_SetPomodoroPinned : AbstractCommand
    {
        private readonly bool _pinned;
        public Cmd_SetPomodoroPinned(bool pinned) => _pinned = pinned;

        protected override void OnExecute() =>
            this.GetModel<IPomodoroModel>().IsPinned.Value = _pinned;
    }
}
```

- [ ] **Step 2：写 `Cmd_SetPlayerCardPinned.cs`**

```csharp
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 切换指定玩家卡片的 pin 状态。
    /// 玩家离线时（Find 返回 null）静默 Warn 不抛异常。
    /// </summary>
    public sealed class Cmd_SetPlayerCardPinned : AbstractCommand
    {
        private readonly string _playerId;
        private readonly bool _pinned;

        public Cmd_SetPlayerCardPinned(string playerId, bool pinned)
        {
            _playerId = playerId;
            _pinned = pinned;
        }

        protected override void OnExecute()
        {
            var card = this.GetModel<IPlayerCardModel>().Find(_playerId);
            if (card == null)
            {
                Debug.LogWarning($"[Cmd_SetPlayerCardPinned] 未找到 playerId={_playerId}");
                return;
            }
            card.IsPinned.Value = _pinned;
        }
    }
}
```

- [ ] **Step 3：MCP `read_console` filter=Error**

- [ ] **Step 4：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPinned.cs \
        Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPinned.cs
git commit -m "feat(command): 新增 Cmd_SetPomodoroPinned / Cmd_SetPlayerCardPinned

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6：新增 Q_ListPlayerCards

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Queries/Q_ListPlayerCards.cs`

- [ ] **Step 1：确认 Queries 目录是否已存在**

```bash
ls Assets/Scripts/APP/Pomodoro/Queries 2>/dev/null || echo "未建"
```

若未建，创建：

```bash
mkdir -p Assets/Scripts/APP/Pomodoro/Queries
```

- [ ] **Step 2：写 `Q_ListPlayerCards.cs`**

```csharp
using System.Collections.Generic;
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.Queries
{
    /// <summary>
    /// 列出当前在线的所有玩家卡片实例（只读）。
    /// 供 Editor 调试窗口等不在 Architecture 内的消费方使用。
    /// </summary>
    public sealed class Q_ListPlayerCards : AbstractQuery<IReadOnlyList<IPlayerCard>>
    {
        protected override IReadOnlyList<IPlayerCard> OnDo() =>
            this.GetModel<IPlayerCardModel>().Cards;
    }
}
```

- [ ] **Step 3：MCP `read_console` filter=Error**

- [ ] **Step 4：commit**

```bash
git add Assets/Scripts/APP/Pomodoro/Queries/Q_ListPlayerCards.cs
git commit -m "feat(query): 新增 Q_ListPlayerCards

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 7：NetworkSystem 接入 IPlayerCardModel 生命周期

**Files:**
- Modify: `Assets/Scripts/APP/Network/System/NetworkSystem.cs`

**目标：** 让远端玩家的 `AddOrGet/Remove` 和 `RoomModel` 的 `AddOrUpdateRemotePlayer/RemoveRemotePlayer` 同步发生。先写 RoomModel，再写 PlayerCardModel，保证 Manager 收到 `E_PlayerCardAdded` 时 `RemotePlayerData` 已可查。

- [ ] **Step 1：在文件顶部 using 区追加**

```csharp
using APP.Pomodoro.Model;
```

（若已有，跳过。）

- [ ] **Step 2：改 `HandleSnapshot`（约第 430 行，`BuildRemotePlayers` 之后的 `room.ApplySnapshot` 区域）**

在 `room.ApplySnapshot(players);` 之后追加：

```csharp
            IPlayerCardModel cardModel = this.GetModel<IPlayerCardModel>();
            // 先登记本次 snapshot 里的所有玩家
            for (int i = 0; i < players.Count; i++)
            {
                cardModel.AddOrGet(players[i].PlayerId);
            }
            // 删除已不在 snapshot 里的旧在线实例（离线）
            PruneOfflineCards(cardModel, players);
```

*（`PruneOfflineCards` 辅助方法在 Step 5 加入。）*

- [ ] **Step 3：改 `HandlePlayerJoined`（约第 439 行）**

在 `room.AddOrUpdateRemotePlayer(player);` 之后追加：

```csharp
            this.GetModel<IPlayerCardModel>().AddOrGet(player.PlayerId);
```

- [ ] **Step 4：改 `HandlePlayerLeft`（约第 459 行）**

在 `room.RemoveRemotePlayer(inbound.playerId);` 之后、`SendEvent(new E_PlayerLeft(...))` 之前追加：

```csharp
            this.GetModel<IPlayerCardModel>().Remove(inbound.playerId);
```

- [ ] **Step 5：在 `NetworkSystem` 类内部新增辅助方法**

在类末尾（任何 `private` 辅助函数之间）追加：

```csharp
        private static void PruneOfflineCards(IPlayerCardModel cardModel, List<RemotePlayerData> snapshot)
        {
            if (cardModel == null || snapshot == null) return;

            var liveIds = new HashSet<string>();
            for (int i = 0; i < snapshot.Count; i++)
            {
                liveIds.Add(snapshot[i].PlayerId);
            }

            // 先收集要移除的 id（避免遍历时修改集合）
            var toRemove = new List<string>();
            var cards = cardModel.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!liveIds.Contains(cards[i].PlayerId))
                {
                    toRemove.Add(cards[i].PlayerId);
                }
            }
            for (int i = 0; i < toRemove.Count; i++) cardModel.Remove(toRemove[i]);
        }
```

- [ ] **Step 6：处理 `ClearRemotePlayers` / `ResetRoomState` 路径**

定位（约第 67-68 行、第 671 行）：

```csharp
            roomModel.ClearRemotePlayers();
```

和

```csharp
            room.ResetRoomState();
```

在这两处后都要清空 `IPlayerCardModel.Cards`。最简洁的做法是：在每处 `ClearRemotePlayers()` / `ResetRoomState()` 之后追加：

```csharp
            ClearAllPlayerCards();
```

并在类内加辅助方法：

```csharp
        private void ClearAllPlayerCards()
        {
            var cardModel = this.GetModel<IPlayerCardModel>();
            if (cardModel == null) return;
            var cards = cardModel.Cards;
            if (cards.Count == 0) return;

            var ids = new List<string>(cards.Count);
            for (int i = 0; i < cards.Count; i++) ids.Add(cards[i].PlayerId);
            for (int i = 0; i < ids.Count; i++) cardModel.Remove(ids[i]);
        }
```

*检查：`ClearRemotePlayers` 在 `Connect` 分支里直接对 `roomModel` 调；用 `this.GetModel` 获取 `IPlayerCardModel` 在 NetworkSystem（AbstractSystem）里合法。*

- [ ] **Step 7：MCP `read_console` filter=Error**

常见编译错误：
- `HashSet<>` / `List<>` 需 `using System.Collections.Generic;`（文件顶部应已有）

- [ ] **Step 8：MCP `run_tests` → `NetworkTests`**

期望：`RoomModelTests` 等既有测试全绿。

- [ ] **Step 9：commit**

```bash
git add Assets/Scripts/APP/Network/System/NetworkSystem.cs
git commit -m "feat(network): Join/Leave/Snapshot/Clear 同步 IPlayerCardModel

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8：PlayerCardController 升格为 IController + 绑定 pc-pin-btn

**Files:**
- Modify: `Assets/UI_V2/Controller/PlayerCardController.cs`

- [ ] **Step 1：文件顶部 using 区追加**

```csharp
using System.Collections.Generic;
using APP.Pomodoro;
using APP.Pomodoro.Command;
using QFramework;
```

- [ ] **Step 2：类声明改为实现 IController**

定位：

```csharp
    public sealed class PlayerCardController
```

替换为：

```csharp
    public sealed class PlayerCardController : IController
```

- [ ] **Step 3：实现 `GetArchitecture`（在类顶部、字段之前）**

```csharp
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;
```

- [ ] **Step 4：在字段区追加**

```csharp
        private IPlayerCard _card;
        private VisualElement _pinBtn;
        private readonly List<IUnRegister> _unRegisters = new List<IUnRegister>();
```

- [ ] **Step 5：改 `BindUI()` 末尾追加 pin 按钮绑定**

在 `BindUI()` 方法末尾（`_appIcon = _root.Q<VisualElement>("pc-active-app-icon");` 之后）追加：

```csharp
            _pinBtn = _root.Q<VisualElement>("pc-pin-btn");
            if (_pinBtn != null)
            {
                _pinBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                _pinBtn.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (_card == null) return;
                    this.SendCommand(new Cmd_SetPlayerCardPinned(_card.PlayerId, !_card.IsPinned.Value));
                });
            }
```

- [ ] **Step 6：新增 `Bind` 与 `Dispose` 方法**

在类末尾追加：

```csharp
        /// <summary>
        /// 绑定卡片 Model 实例。由 PlayerCardManager 在创建后调用。
        /// 订阅 IsPinned 与 GameModel.IsAppFocused；解除由 Dispose 负责。
        /// </summary>
        public void Bind(IPlayerCard card)
        {
            _card = card;
            if (_card == null) return;

            _unRegisters.Add(_card.IsPinned.RegisterWithInitValue(OnPinnedChanged));
            _unRegisters.Add(GameApp.Interface.GetModel<IGameModel>()
                .IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility()));
        }

        /// <summary>
        /// 由 PlayerCardManager 在移除卡片前调用，解除 Bindable 订阅，防止泄漏。
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _unRegisters.Count; i++) _unRegisters[i].UnRegister();
            _unRegisters.Clear();
            _card = null;
        }

        private void OnPinnedChanged(bool pinned)
        {
            _pinBtn?.EnableInClassList("pc-pin-btn--unpinned", !pinned);
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (_root == null || _card == null) return;
            bool focused = GameApp.Interface.GetModel<IGameModel>().IsAppFocused.Value;
            bool visible = focused || _card.IsPinned.Value;
            _root.EnableInClassList("pc-hidden", !visible);
        }
```

- [ ] **Step 7：顶部 using 里追加 Model 引用（若未加）**

```csharp
using APP.Pomodoro.Model;
```

- [ ] **Step 8：MCP `read_console` filter=Error**

- [ ] **Step 9：commit**

```bash
git add Assets/UI_V2/Controller/PlayerCardController.cs
git commit -m "feat(ui): PlayerCardController 升格为 IController，绑定 pc-pin-btn 与可见性订阅

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 9：PlayerCardManager 切换事件驱动 + Bind/Dispose

**Files:**
- Modify: `Assets/UI_V2/Controller/PlayerCardManager.cs`

**目标：** 从订阅 `E_PlayerJoined/Left/Snapshot` 改为订阅 `E_PlayerCardAdded/Removed`；创建 `PlayerCardController` 后调 `Bind(card)`，移除时调 `Dispose()`。`RemotePlayerData` 仍从 `RoomModel` 查（不变）。

- [ ] **Step 1：文件顶部 using 区追加**

```csharp
using APP.Pomodoro.Event; // 新 events
```

- [ ] **Step 2：改 `Initialize` 方法里事件订阅（约第 49-64 行）**

把原本两组注册（分别对应 `lifecycleOwner != null` 的两个分支）中的 `E_PlayerJoined` / `E_PlayerLeft` / `E_RoomSnapshot` / `E_RoomJoined` 替换为 `E_PlayerCardAdded` / `E_PlayerCardRemoved`。**保留** `E_RemoteStateUpdated` 和 `E_IconUpdated`（这两个仍然用于刷新卡片内容）。

新的 `lifecycleOwner != null` 分支：

```csharp
                this.RegisterEvent<E_PlayerCardAdded>(OnCardAdded).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_PlayerCardRemoved>(OnCardRemoved).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_IconUpdated>(OnIconUpdated).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
```

无 `lifecycleOwner` 分支对应同一替换（无 `.UnRegisterWhen...` 调用）。

**删除** `OnPlayerJoined` / `OnPlayerLeft` / `OnRoomJoined` / `OnSnapshot` 回调方法，以及相关 `RebuildFromSnapshot` 方法（如它不再被引用）。若 `RebuildFromSnapshot` 被别处调用则保留。

- [ ] **Step 3：新增 `OnCardAdded` / `OnCardRemoved` 方法**

在原 `OnPlayerJoined` / `OnPlayerLeft` 位置替换为：

```csharp
        private void OnCardAdded(E_PlayerCardAdded e)
        {
            if (string.IsNullOrEmpty(e.PlayerId)) return;
            var room = this.GetModel<IRoomModel>();
            var data = FindRemotePlayer(room, e.PlayerId);
            if (data == null) return; // RoomModel 尚未同步（理论上 NetworkSystem 先写 RoomModel 再写 PlayerCardModel，不会发生）
            AddOrUpdate(data);
        }

        private void OnCardRemoved(E_PlayerCardRemoved e)
        {
            Remove(e.PlayerId);
        }
```

- [ ] **Step 4：改 `AddOrUpdate` 内把 `ctrl.Setup(data)` 之后追加 `Bind`**

在 `AddOrUpdate` 方法里（约第 140-141 行）：

```csharp
            var ctrl = new PlayerCardController(pcRoot);
            ctrl.Setup(data);
            _cards[data.PlayerId] = ctrl;
            _joinOrder.Add(data.PlayerId);
```

的 `_joinOrder.Add` 之后插入：

```csharp
            // 订阅 Model 实例（pin 态 + 失焦可见性）
            var card = this.GetModel<IPlayerCardModel>().Find(data.PlayerId);
            if (card != null) ctrl.Bind(card);
```

- [ ] **Step 5：改 `Remove` 方法：销毁前调 `Dispose`**

替换原方法为：

```csharp
        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_cards.TryGetValue(playerId, out var card))
            {
                card.Dispose();
                card.Root.parent?.Remove(card.Root);
                _cards.Remove(playerId);
                _joinOrder.Remove(playerId);
            }
        }
```

- [ ] **Step 6：改 `Clear`（约第 171 行）**

```csharp
        public void Clear()
        {
            foreach (var kv in _cards) kv.Value.Dispose();
            _cardLayer?.Clear();
            _cards.Clear();
            _joinOrder.Clear();
        }
```

- [ ] **Step 7：MCP `read_console` filter=Error**

- [ ] **Step 8：MCP `run_tests` → `PlayerCardTests`**

期望：`PlayerCardManagerTests` 保持通过（断言走 `cardModel.Find(id)?.Position`；核心行为不变）。

- [ ] **Step 9：commit**

```bash
git add Assets/UI_V2/Controller/PlayerCardManager.cs
git commit -m "refactor(ui): PlayerCardManager 切换到 E_PlayerCardAdded/Removed 驱动，联动 Controller.Bind/Dispose

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 10：PomodoroPanelView 绑定 pp-pin-btn + 失焦可见性

**Files:**
- Modify: `Assets/UI_V2/Controller/PomodoroPanelView.cs`

- [ ] **Step 1：文件顶部 using 区无需新增（已有 `APP.Pomodoro.Model`、`QFramework`）**

- [ ] **Step 2：在字段区（约第 26 行 `_ppBtnSecondary` 之后）追加**

```csharp
        private VisualElement _ppPinBtn;
```

- [ ] **Step 3：在 `BindElements`（约第 106 行）末尾追加按钮查找与绑定**

在 `_ppBtnSecondary?.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());` 之后追加：

```csharp
            _ppPinBtn = pomodoroTemplateContainer.Q<VisualElement>("pp-pin-btn");
            BindPinButton();
```

- [ ] **Step 4：新增 `BindPinButton` 方法**

在 `SubscribeModel` 之前（约第 153 行）插入：

```csharp
        private void BindPinButton()
        {
            if (_ppPinBtn == null) return;
            _ppPinBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            _ppPinBtn.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                bool next = !_model.IsPinned.Value;
                this.SendCommand(new Cmd_SetPomodoroPinned(next));
            });
        }
```

- [ ] **Step 5：在 `SubscribeModel` 末尾追加订阅**

在 `_model.AutoStartBreak.RegisterWithInitValue(_ => RefreshClock())` 之后追加：

```csharp
            _model.IsPinned.RegisterWithInitValue(OnPomodoroPinnedChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.GetModel<IGameModel>().IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility())
                .UnRegisterWhenGameObjectDestroyed(gameObject);
```

- [ ] **Step 6：新增回调方法**

在类末尾追加：

```csharp
        private void OnPomodoroPinnedChanged(bool pinned)
        {
            _ppPinBtn?.EnableInClassList("pp-pin-btn--unpinned", !pinned);
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (_ppRoot == null || _model == null) return;
            bool focused = this.GetModel<IGameModel>().IsAppFocused.Value;
            bool visible = focused || _model.IsPinned.Value;
            _ppRoot.EnableInClassList("pp-hidden", !visible);
        }
```

- [ ] **Step 7：MCP `read_console` filter=Error**

- [ ] **Step 8：commit**

```bash
git add Assets/UI_V2/Controller/PomodoroPanelView.cs
git commit -m "feat(ui): PomodoroPanelView 绑定 pp-pin-btn，订阅失焦隐藏

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 11：PlayerCard.uss 新增 .pc-hidden；DeskWindowController 删除冗余 SetVisible

**Files:**
- Modify: `Assets/UI_V2/Styles/PlayerCard.uss`
- Modify: `Assets/UI_V2/Controller/DeskWindowController.cs`

- [ ] **Step 1：在 `PlayerCard.uss` 文件末尾追加**

```css

/* ── 通用隐藏（由 C# class 切换，不写 inline style）────────── */
.pc-hidden {
    display: none;
}
```

- [ ] **Step 2：改 `DeskWindowController.cs`：删除初始 SetVisible**

定位约第 79 行：

```csharp
            if (_pomodoroPanelView != null && _pomodoroPanelContainer != null)
            {
                _pomodoroPanelView.Init(_pomodoroPanelContainer);
                _pomodoroPanelView.SetVisible(true);
            }
```

替换为（仅删除 SetVisible 这一行）：

```csharp
            if (_pomodoroPanelView != null && _pomodoroPanelContainer != null)
            {
                _pomodoroPanelView.Init(_pomodoroPanelContainer);
            }
```

- [ ] **Step 3：MCP `read_console` filter=Error**

- [ ] **Step 4：commit**

```bash
git add Assets/UI_V2/Styles/PlayerCard.uss \
        Assets/UI_V2/Controller/DeskWindowController.cs
git commit -m "feat(ui): 加 .pc-hidden 样式；移除冗余 SetVisible 调用，可见性交订阅驱动

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 12：Editor Model 调试窗口

**Files:**
- Create: `Assets/Scripts/Editor/ModelDebugWindow.cs`

- [ ] **Step 1：写 `ModelDebugWindow.cs`**

```csharp
#if UNITY_EDITOR
using APP.Pomodoro;
using APP.Pomodoro.Model;
using UnityEditor;
using UnityEngine;

namespace APP.Editor
{
    /// <summary>
    /// Model 调试器：运行时直接读写 IGameModel / IPomodoroModel.IsPinned / IPlayerCardModel。
    /// 设计意图：给本次"失焦隐藏"功能提供手动注入入口，替代尚未接入的真实数据源。
    /// </summary>
    public sealed class ModelDebugWindow : EditorWindow
    {
        [MenuItem("Tools/Model 调试器")]
        private static void Open() => GetWindow<ModelDebugWindow>("Model 调试器").Show();

        private Vector2 _scroll;

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("仅运行时可用。进入 Play Mode 后展示 Model 字段。", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawGameModel();
            EditorGUILayout.Space();
            DrawPomodoroModel();
            EditorGUILayout.Space();
            DrawPlayerCardModel();
            EditorGUILayout.EndScrollView();
        }

        private void DrawGameModel()
        {
            EditorGUILayout.LabelField("GameModel", EditorStyles.boldLabel);
            var model = GameApp.Interface.GetModel<IGameModel>();
            if (model == null)
            {
                EditorGUILayout.HelpBox("未注册 IGameModel", MessageType.Warning);
                return;
            }

            bool next = EditorGUILayout.Toggle("IsAppFocused", model.IsAppFocused.Value);
            if (next != model.IsAppFocused.Value)
            {
                model.IsAppFocused.Value = next;
            }
        }

        private void DrawPomodoroModel()
        {
            EditorGUILayout.LabelField("PomodoroModel", EditorStyles.boldLabel);
            var model = GameApp.Interface.GetModel<IPomodoroModel>();
            if (model == null)
            {
                EditorGUILayout.HelpBox("未注册 IPomodoroModel", MessageType.Warning);
                return;
            }

            bool next = EditorGUILayout.Toggle("IsPinned", model.IsPinned.Value);
            if (next != model.IsPinned.Value)
            {
                model.IsPinned.Value = next;
            }
        }

        private void DrawPlayerCardModel()
        {
            var model = GameApp.Interface.GetModel<IPlayerCardModel>();
            int count = model?.Cards?.Count ?? 0;
            EditorGUILayout.LabelField($"PlayerCardModel (Cards = {count})", EditorStyles.boldLabel);
            if (model == null)
            {
                EditorGUILayout.HelpBox("未注册 IPlayerCardModel", MessageType.Warning);
                return;
            }

            var cards = model.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("playerId", card.PlayerId);

                Vector2 nextPos = EditorGUILayout.Vector2Field("Position", card.Position.Value);
                if (nextPos != card.Position.Value) card.Position.Value = nextPos;

                bool nextPin = EditorGUILayout.Toggle("IsPinned", card.IsPinned.Value);
                if (nextPin != card.IsPinned.Value) card.IsPinned.Value = nextPin;
                EditorGUILayout.EndVertical();
            }
        }
    }
}
#endif
```

- [ ] **Step 2：MCP `read_console` filter=Error**

- [ ] **Step 3：commit**

```bash
git add Assets/Scripts/Editor/ModelDebugWindow.cs
git commit -m "feat(editor): 新增 Tools/Model 调试器 EditorWindow

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 13：写 EditMode 测试 — PlayerCardModelTests

**Files:**
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardModelTests.cs`

- [ ] **Step 1：新建测试文件**

```csharp
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Tests.PlayerCardTests
{
    public sealed class PlayerCardModelTests
    {
        private sealed class TestArchitecture : Architecture<TestArchitecture>
        {
            protected override void Init()
            {
                // 使用现成的 APP.Utility.InMemoryStorageUtility，避免污染 PlayerPrefs
                RegisterUtility<IStorageUtility>(new InMemoryStorageUtility());
                RegisterModel<IPlayerCardModel>(new PlayerCardModel());
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Deinit 会把 Architecture<T>.mArchitecture 静态字段置空；下次 Interface 访问会重新 Init。
            if (TestArchitectureRef.Instance != null) TestArchitectureRef.Instance.Deinit();
        }

        private static IPlayerCardModel CreateModel()
        {
            // 每个测试进入时若上一个残留实例，先 Deinit 清除
            if (TestArchitectureRef.Instance != null) TestArchitectureRef.Instance.Deinit();
            return TestArchitecture.Interface.GetModel<IPlayerCardModel>();
        }

        // 辅助：封装 Architecture<T>.mArchitecture 的"当前实例"引用（通过 Interface 自动创建）
        private static class TestArchitectureRef
        {
            public static IArchitecture Instance =>
                typeof(TestArchitecture)
                    .GetField("mArchitecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(null) as IArchitecture;
        }

        [Test]
        public void AddOrGet_NewPlayer_ReturnsDefaultInstance()
        {
            var model = CreateModel();

            var card = model.AddOrGet("p1");

            Assert.IsNotNull(card);
            Assert.AreEqual("p1", card.PlayerId);
            Assert.AreEqual(Vector2.zero, card.Position.Value);
            Assert.IsFalse(card.IsPinned.Value);
        }

        [Test]
        public void AddOrGet_SamePlayerTwice_ReturnsSameInstance()
        {
            var model = CreateModel();

            var a = model.AddOrGet("p1");
            var b = model.AddOrGet("p1");

            Assert.AreSame(a, b);
            Assert.AreEqual(1, model.Cards.Count);
        }

        [Test]
        public void Remove_InstanceDropsFromCards()
        {
            var model = CreateModel();
            model.AddOrGet("p1");

            model.Remove("p1");

            Assert.AreEqual(0, model.Cards.Count);
            Assert.IsNull(model.Find("p1"));
        }

        [Test]
        public void Remove_ThenAddOrGet_RestoresLastValues()
        {
            var model = CreateModel();
            var card = model.AddOrGet("p1");
            card.Position.Value = new Vector2(123f, 456f);
            card.IsPinned.Value = true;
            model.Remove("p1");

            var restored = model.AddOrGet("p1");

            Assert.AreEqual(new Vector2(123f, 456f), restored.Position.Value);
            Assert.IsTrue(restored.IsPinned.Value);
        }

        [Test]
        public void BindableChange_WritesStoreImmediately()
        {
            var model = CreateModel();
            var card = model.AddOrGet("p1");

            card.IsPinned.Value = true;
            model.Remove("p1");

            var restored = model.AddOrGet("p1");
            Assert.IsTrue(restored.IsPinned.Value);
        }

        [Test]
        public void AddOrGet_RaisesPlayerCardAdded()
        {
            var model = CreateModel();
            string received = null;
            TypeEventSystem.Global.Register<E_PlayerCardAdded>(e => received = e.PlayerId);

            model.AddOrGet("p1");

            Assert.AreEqual("p1", received);
        }

        [Test]
        public void Remove_RaisesPlayerCardRemoved()
        {
            var model = CreateModel();
            model.AddOrGet("p1");
            string received = null;
            TypeEventSystem.Global.Register<E_PlayerCardRemoved>(e => received = e.PlayerId);

            model.Remove("p1");

            Assert.AreEqual("p1", received);
        }
    }
}
```

> 本测试复用项目已有的 `APP.Utility.InMemoryStorageUtility`（`Assets/Scripts/APP/Utility/InMemoryStorageUtility.cs`），不要在测试文件内重复定义。

- [ ] **Step 2：MCP `read_console` filter=Error**

- [ ] **Step 3：MCP `run_tests` → `PlayerCardModelTests`**

期望：全部 7 用例通过。

- [ ] **Step 4：commit**

```bash
git add Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardModelTests.cs
git commit -m "test(model): PlayerCardModel 单元测试（AddOrGet/Remove/持久化/事件）

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 14：写 EditMode 测试 — GameModelTests

**Files:**
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/GameModelTests.cs`

- [ ] **Step 1：新建测试文件**

```csharp
using APP.Pomodoro.Model;
using NUnit.Framework;

namespace APP.Tests.PlayerCardTests
{
    public sealed class GameModelTests
    {
        [Test]
        public void IsAppFocused_DefaultTrue()
        {
            IGameModel model = new GameModel();
            Assert.IsTrue(model.IsAppFocused.Value);
        }

        [Test]
        public void IsAppFocused_WriteTriggersSubscriber()
        {
            IGameModel model = new GameModel();
            bool? received = null;
            model.IsAppFocused.Register(v => received = v);

            model.IsAppFocused.Value = false;

            Assert.IsTrue(received.HasValue);
            Assert.IsFalse(received.Value);
        }
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error**

- [ ] **Step 3：MCP `run_tests` → `GameModelTests`**

期望：2 用例通过。

- [ ] **Step 4：commit**

```bash
git add Assets/Tests/EditMode/PlayerCardTests/Editor/GameModelTests.cs
git commit -m "test(model): GameModel 默认值与订阅行为

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 15：写 EditMode 测试 — PinCommandTests

**Files:**
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/PinCommandTests.cs`

- [ ] **Step 1：新建测试文件**

```csharp
using APP.Pomodoro;
using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.TestTools;

namespace APP.Tests.PlayerCardTests
{
    public sealed class PinCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            // 重置 Architecture 单例：Deinit 会把 mArchitecture 静态字段置空，
            // 下次访问 Interface 时重新 Init（见 QFramework.cs Architecture<T>.Deinit）
            var current = typeof(GameApp)
                .GetField("mArchitecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as IArchitecture;
            current?.Deinit();
        }

        [Test]
        public void Cmd_SetPomodoroPinned_WritesModel()
        {
            var arch = GameApp.Interface;
            var model = arch.GetModel<IPomodoroModel>();
            Assert.IsFalse(model.IsPinned.Value);

            arch.SendCommand(new Cmd_SetPomodoroPinned(true));

            Assert.IsTrue(model.IsPinned.Value);
        }

        [Test]
        public void Cmd_SetPlayerCardPinned_MissingPlayerId_LogsWarningWithoutThrow()
        {
            var arch = GameApp.Interface;
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("\\[Cmd_SetPlayerCardPinned\\] 未找到 playerId=ghost"));

            Assert.DoesNotThrow(() => arch.SendCommand(new Cmd_SetPlayerCardPinned("ghost", true)));
        }

        [Test]
        public void Cmd_SetPlayerCardPinned_OnlineCard_WritesModel()
        {
            var arch = GameApp.Interface;
            var cardModel = arch.GetModel<IPlayerCardModel>();
            var card = cardModel.AddOrGet("p1");
            Assert.IsFalse(card.IsPinned.Value);

            arch.SendCommand(new Cmd_SetPlayerCardPinned("p1", true));

            Assert.IsTrue(card.IsPinned.Value);
        }
    }
}
```

- [ ] **Step 2：MCP `read_console` filter=Error**

- [ ] **Step 3：MCP `run_tests` → `PinCommandTests`**

期望：3 用例通过。

- [ ] **Step 4：commit**

```bash
git add Assets/Tests/EditMode/PlayerCardTests/Editor/PinCommandTests.cs
git commit -m "test(command): Cmd_SetPomodoroPinned 与 Cmd_SetPlayerCardPinned 行为测试

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 16：扩展 Pomodoro 持久化测试 — IsPinned 往返

**Files:**
- Modify: `Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs`（或就近的 Pomodoro 持久化测试文件）

- [ ] **Step 1：确认目标测试文件**

```bash
find Assets/Tests/EditMode -name "Pomodoro*Tests*.cs"
```

若找到 `PomodoroPanelPositionTests.cs` 或类似，追加用例；否则新建 `PomodoroPersistenceTests.cs`。

- [ ] **Step 2：在目标文件中新增用例（示例基于 `PomodoroPanelPositionTests`）**

```csharp
        [Test]
        public void IsPinned_RoundTripsThroughPersistence()
        {
            IPomodoroModel save = new PomodoroModel();
            save.IsPinned.Value = true;
            PomodoroPersistence.Save(save, flushToDisk: true);

            IPomodoroModel load = new PomodoroModel();
            bool ok = PomodoroPersistence.TryLoad(load);

            Assert.IsTrue(ok);
            Assert.IsTrue(load.IsPinned.Value);
        }

        [Test]
        public void IsPinned_DefaultFalse_WhenNoSavedState()
        {
            IPomodoroModel model = new PomodoroModel();
            Assert.IsFalse(model.IsPinned.Value);
        }
```

若新建文件，顶部加相应 using / namespace。

- [ ] **Step 3：MCP `read_console` filter=Error**

- [ ] **Step 4：MCP `run_tests`**

期望：新用例通过；既有 Pomodoro 持久化测试继续通过。

- [ ] **Step 5：commit**

```bash
git add Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs
git commit -m "test(pomodoro): IsPinned 持久化往返与默认值

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 17：手工验收与记录

**Files:** 无代码改动；只做运行时验证。

- [ ] **Step 1：打开 Unity 进 Play Mode**

- [ ] **Step 2：`Tools → Model 调试器` → 窗口出现**

- [ ] **Step 3：逐条走 Spec §8.4 的 12 条验收**

参见 `docs/superpowers/specs/2026-04-23-pin-button-and-focus-hiding-design.md` §8.4。

- [ ] **Step 4：若全绿，更新 spec 勾验收栏；若有 bug，回到相应 Task 修复**

- [ ] **Step 5：无代码改动 → 无 commit**

---

## 完成标准

- [ ] 全部 EditMode 测试通过（PlayerCardModel / GameModel / PinCommand / Pomodoro 持久化）
- [ ] MCP `read_console` filter=Error 返回 0
- [ ] 手工验收 Spec §8.4 全 12 条通过
- [ ] `git log` 中 17 个提交，链路清晰

## 已知风险（从 Spec 转入）

- **PlayerId 会话级语义** —— 持久化 key 依赖服务端 id 稳定性，客户端没有账号锚点。独立议题。
- **失焦真实数据源未接** —— 本 Plan 只接 Editor 窗口注入；运行期自动失焦隐藏在下一会话接 `OnApplicationFocus` / `UniWindowController` / `IActiveAppSystem` 等源之一。
