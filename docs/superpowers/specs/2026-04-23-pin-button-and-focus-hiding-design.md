# Pin 按钮 + 失焦隐藏 设计文档

- 日期：2026-04-23
- 相关分支：`feat/desk-window-refactor`
- 领域：UI 层 + Pomodoro 领域 Model 重构

## 1. 背景与目标

主界面的番茄钟面板（`pp-pin-btn`）和每张远端玩家卡片（`pc-pin-btn`）右上角已经有"图钉"按钮的 UXML/USS 定义，但**尚未绑定行为**。本次要让这两个按钮成为"pin 开关"，同时引入一个全局的"应用是否聚焦"信号：

- 每个 UI 独立判断，**失焦 && 未 pin → 自身 `display:none`**；失焦但已 pin → 保留可见。
- pin 按钮有两种视觉态（pin / pin-off），通过 `.xxx-pin-btn--unpinned` 修饰 class 切换。
- "失焦"信号由一个新 `IGameModel.IsAppFocused : bool` 管理。**本次会话不接真实数据源**（谁把 Unity/OS 的焦点状态写进 Model），改用一个 Editor 调试窗口手动赋值 Model 字段。

同时，借这次重构把"玩家卡片数据"整合：

- `IPlayerCardPositionModel`（当前只存 `Dictionary<playerId, Vector2>`）升级为 `IPlayerCardModel` + `IPlayerCard` 实例容器。
- 每个 `IPlayerCard` 实例持有 `Position` 和 `IsPinned` 两个 Bindable。
- 持久化按 `playerId` 走 `PlayerPrefs`，本次**直接丢弃旧 JSON 数据**（开发阶段，不做 schema 兼容）。

### 1.1 `PlayerId` 语义 · 已知风险

客户端当前协议里：

- 加入房间只发 `playerName + roomCode`（`OutboundJoinRoom`），**不发任何客户端身份标识**。
- `playerId` 完全由**服务端**在 `join_room_ack` 里下发（`InboundMessage.playerId`）。
- 客户端没有"账号 / 设备 UUID / token"任何一种本地稳定锚点。

因此本次持久化 key 使用的 `playerId` 只是**服务端分配的会话级身份**。具体稳定性完全取决于服务端实现：如果服务端按 `(roomCode, playerName)` 做键稳定下发同一个 id，则用户换名或切房间时记录会丢；如果每次 connect 发新 UUID，则重连即丢。

**本次不解决这个问题**。Spec 记录为已知风险，留待独立议题（需要客户端持久化 `ClientIdentityId` + 服务端协议配合）。

## 2. 非目标 / 不做的事

- **不**接失焦变量的真实数据源（Unity `OnApplicationFocus`、`UniWindowController` 事件、`IActiveAppSystem` 等）。留 Editor 窗口做手动注入，后续会话再接。
- **不**做 `PlayerId` 稳定化或账号体系。
- **不**做旧 JSON schema 兼容——使用新 PlayerPrefs key `"CPA.PlayerCards"`，旧数据直接作废。
- **不**改动 `UniWindowController.isTopmost` 或 `IPomodoroModel.IsTopmost` 的语义（它们是 OS 窗口层级概念，和本次 pin 无关）。
- **不**引入 `VisibilitySystem`（集中决策器）——当前规则仅 `focused || pinned`，一个布尔或，本地订阅更简单。

## 3. 架构与数据流

### 3.1 层级映射

```
 ┌────────────────────────────────────────────────────────────────┐
 │ View (MonoBehaviour / IController)                             │
 │  PomodoroPanelView     pp-pin-btn click ──► Cmd_SetPomodoroPinned
 │                        订阅 IsAppFocused + IsPinned ──► pp-hidden
 │  PlayerCardController  pc-pin-btn click ──► Cmd_SetPlayerCardPinned
 │                        订阅 IsAppFocused + card.IsPinned ──► pc-hidden
 │  PlayerCardManager     订阅 E_PlayerCardAdded/Removed ──► 增删 VisualElement
 ├────────────────────────────────────────────────────────────────┤
 │ Command                                                        │
 │  Cmd_SetPomodoroPinned(bool)                                   │
 │  Cmd_SetPlayerCardPinned(playerId, bool)                       │
 │  Cmd_SetPlayerCardPosition(playerId, Vector2)   [改写]         │
 ├────────────────────────────────────────────────────────────────┤
 │ Query                                                          │
 │  Q_ListPlayerCards() : IReadOnlyList<IPlayerCard>              │
 ├────────────────────────────────────────────────────────────────┤
 │ System                                                         │
 │  NetworkSystem  (在玩家 Join/Leave 时直写 IPlayerCardModel)    │
 ├────────────────────────────────────────────────────────────────┤
 │ Model                                                          │
 │  IGameModel.IsAppFocused : BindableProperty<bool>              │
 │  IPomodoroModel.IsPinned : BindableProperty<bool>    [新增]    │
 │  IPlayerCardModel { Cards, Find, AddOrGet, Remove }  [重构]    │
 │    └─ IPlayerCard { PlayerId, Position, IsPinned }             │
 └────────────────────────────────────────────────────────────────┘
```

### 3.2 可见性判定

每个 View 独立订阅两个信号源：

```
visible = IsAppFocused || thisPinned
display = visible ? Flex : None   （通过 EnableInClassList("xxx-hidden", !visible)）
```

### 3.3 Pin 视觉态

按钮 UXML 默认 class = "已 pin"。首次启动 `IsPinned=false`，订阅回调会自动把 `.xxx-pin-btn--unpinned` 加上，视觉变成未 pin。**UXML 不需要改默认态**。

### 3.4 PlayerCard 实例生命周期（X 语义）

- `IPlayerCardModel.Cards` 只含**当前在线**玩家的实例。
- `AddOrGet(playerId)`：存在即返回；不存在则新建实例，并从持久化仓库读初值（位置/pin）。
- `Remove(playerId)`：把当前实例的 Bindable 值落盘到仓库，从 Cards 移除实例，解除订阅。仓库记录保留供再次 Join 时恢复。
- `NetworkSystem` 在 Join 时 `AddOrGet`，在 Leave / Clear / Reset 时 `Remove`。先写 `RoomModel` 再写 `PlayerCardModel`，保证 Manager 订阅 `E_PlayerCardAdded` 时 `RemotePlayerData` 已可查。

## 4. Model 层

### 4.1 `IGameModel`（新增）

`Assets/Scripts/APP/Pomodoro/Model/IGameModel.cs`：

```csharp
public interface IGameModel : IModel
{
    /// <summary>应用是否处于聚焦态。默认 true。</summary>
    /// <remarks>
    /// 本次会话不接真实数据源，由 Editor 调试窗口手动赋值。
    /// 未来接入时仍写此字段。
    /// </remarks>
    BindableProperty<bool> IsAppFocused { get; }
}
```

`GameModel`：单字段实现，`OnInit` 空实现，不持久化。

### 4.2 `IPomodoroModel.IsPinned`（新增字段）

在现有 `IPomodoroModel` 接口里追加：

```csharp
/// <summary>番茄钟面板是否被 pin（不因失焦隐藏）。默认 false。</summary>
BindableProperty<bool> IsPinned { get; }
```

`PomodoroModel` 实现：`new BindableProperty<bool>(false)`。

持久化：在 `PomodoroPersistence` 的读/写列表中追加 `IsPinned` 字段，和 `IsTopmost` 并列（二者独立字段，不合并，不互相影响）。

### 4.3 `IPlayerCardPositionModel` → `IPlayerCardModel` + `IPlayerCard`（重构）

**`IPlayerCard`** —— 单个玩家卡片的运行时实例：

```csharp
public interface IPlayerCard
{
    string PlayerId { get; }                 // 终身不变
    BindableProperty<Vector2> Position { get; }
    BindableProperty<bool>    IsPinned { get; }
}
```

**`IPlayerCardModel`** —— 容器 + 持久化仓库：

```csharp
public interface IPlayerCardModel : IModel
{
    IReadOnlyList<IPlayerCard> Cards { get; }

    /// <summary>按 id 查当前在线的实例，找不到返回 null。</summary>
    IPlayerCard Find(string playerId);

    /// <summary>
    /// 确保指定玩家的卡片存在于 Cards。若已存在直接返回；
    /// 若不存在则新建实例，并从持久化仓库恢复 Position/IsPinned（没有记录则用默认）。
    /// 会发出 E_PlayerCardAdded。
    /// </summary>
    IPlayerCard AddOrGet(string playerId);

    /// <summary>
    /// 把当前实例值落盘到持久化仓库，然后从 Cards 移除实例。
    /// 仓库记录保留。会发出 E_PlayerCardRemoved。
    /// </summary>
    void Remove(string playerId);
}
```

**实现要点**（`PlayerCardModel`）：

- 内部 `Dictionary<string, PersistedData>` 作为持久化仓库（`PersistedData = { Vector2 Position, bool IsPinned }`）。
- 内部 `Dictionary<string, PlayerCard>` 作为当前在线实例表（驱动 `Cards` 只读视图）。
- `OnInit`：读 PlayerPrefs key `"CPA.PlayerCards"` → 解析 JSON → 填仓库。
- `AddOrGet`：
  1. 如已在实例表 → 返回
  2. 否则：从仓库读初值（缺省 `Position = Vector2.zero`、`IsPinned = false`）→ `new PlayerCard(id, pos, pinned)`
  3. 订阅实例的 `Position` / `IsPinned`：每次变化 → 更新仓库 → `Persist()`
  4. 加入实例表 → `SendEvent(new E_PlayerCardAdded(id))`
- `Remove`：
  1. 从实例表找到实例；没有则直接 return
  2. 解除步骤 3 的订阅（保存 `IUnRegister`）
  3. 把实例当前 `Position.Value`/`IsPinned.Value` 写仓库 → `Persist()`
  4. 从实例表删除 → `SendEvent(new E_PlayerCardRemoved(id))`
- `Persist()`：整表序列化 JSON 写 PlayerPrefs，和现有 `PlayerCardPositionModel.Persist` 同风格。

**新 JSON schema**（key = `"CPA.PlayerCards"`）：

```csharp
[Serializable] struct Entry
{
    public string id;
    public float  x;
    public float  y;
    public bool   pinned;
}
[Serializable] sealed class Envelope { public Entry[] entries; }
```

旧 key `"CPA.PlayerCardPositions"` 不读不写。开发阶段不做迁移。

### 4.4 GameApp 注册

`GameApp.Init()` 调整：

```csharp
RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());

RegisterModel<IGameModel>(new GameModel());                      // 新增
RegisterModel<IPomodoroModel>(new PomodoroModel());
RegisterModel<IRoomModel>(new RoomModel());
RegisterModel<ISessionMemoryModel>(new SessionMemoryModel());
RegisterModel<IPlayerCardModel>(new PlayerCardModel());          // 替换旧类型

// Systems 不变
```

## 5. Command / Query / Event 层

### 5.1 新增 Command

`Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPinned.cs`：

```csharp
public sealed class Cmd_SetPomodoroPinned : AbstractCommand
{
    private readonly bool _pinned;
    public Cmd_SetPomodoroPinned(bool pinned) => _pinned = pinned;
    protected override void OnExecute() =>
        this.GetModel<IPomodoroModel>().IsPinned.Value = _pinned;
}
```

`Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPinned.cs`：

```csharp
public sealed class Cmd_SetPlayerCardPinned : AbstractCommand
{
    private readonly string _playerId;
    private readonly bool   _pinned;
    public Cmd_SetPlayerCardPinned(string playerId, bool pinned)
    {
        _playerId = playerId;
        _pinned   = pinned;
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
```

### 5.2 改写已有 Command

`Cmd_SetPlayerCardPosition`：

```csharp
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
```

### 5.3 新增 Query

`Assets/Scripts/APP/Pomodoro/Queries/Q_ListPlayerCards.cs`：

```csharp
public sealed class Q_ListPlayerCards : AbstractQuery<IReadOnlyList<IPlayerCard>>
{
    protected override IReadOnlyList<IPlayerCard> OnDo() =>
        this.GetModel<IPlayerCardModel>().Cards;
}
```

> 读单个 `IPlayerCard` 不加 Query——`PlayerCardManager` 等 `IController` 直接 `GetModel<IPlayerCardModel>().Find(id)`。只有 Editor 窗口等"不在 Architecture 内"的消费方需要 Query；Editor 窗口用 `GameApp.Interface.GetModel<...>` 也能直接拿，所以 Query 仅提供 `Q_ListPlayerCards` 一条便捷接口。

### 5.4 新增 Event

`Assets/Scripts/APP/Pomodoro/Event/PlayerCardEvents.cs`：

```csharp
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
```

发出点：`PlayerCardModel.AddOrGet`（新建路径）、`PlayerCardModel.Remove`（完成后）。

### 5.5 不新增

- `Cmd_EnsurePlayerCard` / `Cmd_RemovePlayerCard`：系统 `NetworkSystem` 直写 Model（QFramework 允许 System → Model）。
- `Cmd_SetAppFocused`：业务 Controller 不该写此字段，Editor 直写即可。
- `E_PomodoroPinnedChanged` / `E_AppFocusChanged`：用 Bindable 订阅即可，不再多一层事件。

## 6. View 层

### 6.1 `PomodoroPanelView`

`BindElements` 内新增：

```csharp
private VisualElement _ppPinBtn;

_ppPinBtn = pomodoroTemplateContainer.Q<VisualElement>("pp-pin-btn");
BindPinButton();
```

`SubscribeModel` 末尾追加：

```csharp
_model.IsPinned.RegisterWithInitValue(OnPomodoroPinnedChanged)
    .UnRegisterWhenGameObjectDestroyed(gameObject);

this.GetModel<IGameModel>().IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility())
    .UnRegisterWhenGameObjectDestroyed(gameObject);
```

新方法：

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

**`DeskWindowController.Start()` 里的 `_pomodoroPanelView.SetVisible(true)` 这行删除**——可见性完全由订阅驱动，避免两条路径冲突。

### 6.2 `PlayerCardController` 升格

当前 `PlayerCardController` 既非 `MonoBehaviour` 也非 `IController`。改为实现 `IController`，增加 `Bind(IPlayerCard)` / `Dispose()`：

```csharp
public sealed class PlayerCardController : IController
{
    IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

    private IPlayerCard _card;
    private VisualElement _pinBtn;
    private readonly List<IUnRegister> _unRegisters = new();

    // BindUI 内追加
    private void BindUI()
    {
        // ... 现有 ...
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
    }

    public void Bind(IPlayerCard card)
    {
        _card = card;
        _unRegisters.Add(_card.IsPinned.RegisterWithInitValue(OnPinnedChanged));
        _unRegisters.Add(GameApp.Interface.GetModel<IGameModel>()
            .IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility()));
    }

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
}
```

### 6.3 `PlayerCardManager`

数据源从"按 `RoomModel.RemotePlayers` diff"改为订阅两个新事件：

```csharp
this.RegisterEvent<E_PlayerCardAdded>(e => OnCardAdded(e.PlayerId))
    .UnRegisterWhenGameObjectDestroyed(_hostGameObject);
this.RegisterEvent<E_PlayerCardRemoved>(e => OnCardRemoved(e.PlayerId))
    .UnRegisterWhenGameObjectDestroyed(_hostGameObject);

private void OnCardAdded(string playerId)
{
    var card = this.GetModel<IPlayerCardModel>().Find(playerId);
    if (card == null) return;
    var data = FindRemotePlayer(this.GetModel<IRoomModel>(), playerId);
    if (data == null) return;

    var tree = _template.Instantiate();
    var root = tree.Q<VisualElement>(className: "pc-root") ?? tree;
    _cardLayer.Add(tree);

    var controller = new PlayerCardController(root);
    controller.Setup(data);
    controller.Bind(card);

    // 初始位置 / 拖拽回调（引用 card.Position 而不是之前的 posModel）
    ApplyInitialPosition(root, card.Position.Value);
    WireDragHandlers(root, playerId);

    _cards[playerId] = (tree, controller);
    _joinOrder.Add(playerId);
}

private void OnCardRemoved(string playerId)
{
    if (_cards.TryGetValue(playerId, out var entry))
    {
        entry.controller.Dispose();
        _cardLayer.Remove(entry.tree);
        _cards.Remove(playerId);
        _joinOrder.Remove(playerId);
    }
}
```

（细节：`_cards` 的类型从 `Dictionary<string, VisualElement>` 改为 `Dictionary<string, (VisualElement tree, PlayerCardController controller)>`；初始位置算法沿用现有 `ResolveInitialPosition` 但读 `card.Position` 而非 `posModel.TryGet`。）

### 6.4 USS 新增

`Assets/UI_V2/Styles/PlayerCard.uss` 末尾：

```css
/* ── 通用隐藏（由 C# class 切换）────────────── */
.pc-hidden {
    display: none;
}
```

`PomodoroPanel.uss` 的 `.pp-hidden` 已存在，不改。

### 6.5 `NetworkSystem` 接入

在现有处理里新增对 `IPlayerCardModel` 的写入：

- `HandleJoinRoomAck` / `HandleRejoinAck` / `HandleSnapshot` 识别到每个远端 `playerId` 时：
  `this.GetModel<IPlayerCardModel>().AddOrGet(playerId);`（在 `room.AddOrUpdateRemotePlayer(...)` 之后）
- `HandlePlayerJoined`：同上，新玩家加入走 `AddOrGet`。
- `HandlePlayerLeft`：`room.RemoveRemotePlayer(playerId)` 之后调 `AddOrGet` 的反向 —— `this.GetModel<IPlayerCardModel>().Remove(playerId);`
- `ClearRemotePlayers` / `ResetRoomState` 路径：把当前 `Cards` 里每个 id 都调 `Remove`。

## 7. Editor 调试窗口

### 7.1 文件与菜单

- 路径：`Assets/Scripts/Editor/ModelDebugWindow.cs`
- 菜单：`Tools/Model 调试器`
- 整文件包 `#if UNITY_EDITOR`

### 7.2 UI 结构

IMGUI `EditorWindow`，和 `NetworkSimulatorWindow` 同风格：

- 非 Play Mode：`HelpBox("仅运行时可用", Info)` 后 return
- Play Mode：依次绘制三节
  - **GameModel**：`Toggle("IsAppFocused")`
  - **PomodoroModel**：`Toggle("IsPinned")`
  - **PlayerCardModel (Cards = N)**：遍历 `Cards`，对每个实例画一块：
    - `LabelField("playerId", card.PlayerId)`（只读）
    - `Vector2Field("Position", card.Position.Value)`
    - `Toggle("IsPinned", card.IsPinned.Value)`

### 7.3 写入路径

Editor 直接写 Model：`m.IsAppFocused.Value = next;`、`card.Position.Value = nextVec;` 等。不走 Command（Editor 调试工具非业务 Controller，和 `NetworkSimulatorWindow` 的直写 Model 风格一致）。

### 7.4 重绘

`OnEnable`: `EditorApplication.update += Repaint;`
`OnDisable`: `EditorApplication.update -= Repaint;`

每 Editor tick 重绘一次，自然反映外部变化和 Cards 列表的增删。不单独订阅 `E_PlayerCardAdded/Removed`。

### 7.5 Play Mode 进入与退出

- `!Application.isPlaying` 早退，**不**触达 `GameApp.Interface`（避免在非 Play Mode 触发 Architecture 静态初始化的副作用）。
- 进入 Play Mode 后窗口自动开始可用；退出时 `OnGUI` 又回到提示状态。

## 8. 测试计划

### 8.1 实现顺序（Unity TDD 约束：先实现再测试）

1. Model / Command / Query / Event 代码（编译通过）
2. View + PlayerCardManager + PlayerCardController 改动（编译通过）
3. Editor 调试窗口（编译通过）
4. EditMode 测试新增与改写

### 8.2 EditMode 单测清单

**新建 `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardModelTests.cs`**（替代旧 `PlayerCardPositionModelTests.cs`）：

| 用例 | 断言 |
|---|---|
| `AddOrGet_新玩家_返回默认实例` | `Position.Value == Vector2.zero` 且 `IsPinned.Value == false` |
| `AddOrGet_同一玩家调用两次_返回同一实例` | `ReferenceEquals(a, b)` |
| `Remove_实例从Cards移除` | `Cards.Count` 减 1 且 `Find(id) == null` |
| `Remove_再AddOrGet_从持久化恢复` | 值等于被 Remove 前的最后一次设置 |
| `实例Bindable变化_自动落盘` | 改 `Position.Value` 后新建一个 Model 实例读回 → 一致 |
| `AddOrGet_广播E_PlayerCardAdded` | TypeEventSystem 订阅收到 |
| `Remove_广播E_PlayerCardRemoved` | 同上 |

**新建 `GameModelTests.cs`**：

- `IsAppFocused_默认true`
- `IsAppFocused_写入后触发订阅`

**扩展现有 Pomodoro 持久化测试**：

- `IsPinned_首次启动默认false`
- `IsPinned_持久化往返`

**新建 Command 测试**：

- `Cmd_SetPomodoroPinned_写入Model`
- `Cmd_SetPlayerCardPinned_离线PlayerId_LogWarning_不抛异常`（`LogAssert.Expect`）

**删除 / 改写**：

- 删除 `PlayerCardPositionModelTests.cs`
- 改写 `PlayerCardManagerTests.cs`：引用类型改为 `IPlayerCardModel`，通过 `AddOrGet(id)` 构造实例，断言走 `card.Position`

### 8.3 PlayMode 测试

不新增自动化测试。现有 `DeskWindowPanelsImageValidationTests` 覆盖基础截图，本次新增行为（失焦隐藏）走手工验收。

### 8.4 手工验收清单

| # | 步骤 | 期望 |
|---|---|---|
| 1 | 进 Play Mode，`Tools → Model 调试器` | 列出 GameModel / PomodoroModel / PlayerCardModel 三节 |
| 2 | 默认状态（`IsAppFocused=true`，全部未 pin） | Pomodoro 面板可见 |
| 3 | Toggle `IsAppFocused` → false | Pomodoro 面板立即 `display:none` |
| 4 | `IsAppFocused=false` 下点 `pp-pin-btn` → pinned | 面板重新出现，按钮样式从 `--unpinned` 回到默认 |
| 5 | 再 toggle `pp-pin-btn` 到 unpinned | 面板再次消失 |
| 6 | Toggle `IsAppFocused` → true | 所有 UI 重新可见 |
| 7 | 用 `NetworkSimulatorWindow` 加一个远端玩家 | `PlayerCardModel.Cards` 新增一条；卡片 VisualElement 出现 |
| 8 | Toggle 该卡片 `IsPinned` → 失焦时是否保留 | 与 pp 行为一致（只影响该卡） |
| 9 | Editor 编辑该卡片 `Position` | 卡片漂移到新位置 |
| 10 | 重启应用 | 卡片位置 + `IsPinned` 保持；`IsAppFocused` 重置为 `true` |
| 11 | 让远端玩家离开 | `Cards` 减 1；持久化记录保留（再加入时恢复） |
| 12 | `Cmd_SetPlayerCardPinned(不存在id, true)`（编辑器测试或 Console） | 出现 Warn，无异常 |

### 8.5 编译 / 回归

每一批改动后执行：

- MCP `read_console` filter=Error / Warning，要求 0 error
- MCP `run_tests`：
  - `PlayerCardTests`（EditMode）
  - `NetworkTests`（EditMode）——`ClearRemotePlayers / ResetRoomState` 路径有改动

## 9. 最终交付物清单

1. `IGameModel` + `GameModel`（新增）
2. `IPomodoroModel.IsPinned` + `PomodoroModel` 实现 + `PomodoroPersistence` 扩展
3. `IPlayerCard` + `IPlayerCardModel` + `PlayerCardModel`（重构替换 `IPlayerCardPositionModel`）
4. Commands：`Cmd_SetPomodoroPinned`、`Cmd_SetPlayerCardPinned`、重写 `Cmd_SetPlayerCardPosition`
5. Query：`Q_ListPlayerCards`
6. Events：`E_PlayerCardAdded`、`E_PlayerCardRemoved`
7. `PomodoroPanelView` 绑定 `pp-pin-btn` + 订阅可见性
8. `PlayerCardController` 升格为 `IController`，加 `Bind/Dispose`
9. `PlayerCardManager` 切换到事件驱动
10. `NetworkSystem` 在 Join/Leave/Clear 路径上同步 `IPlayerCardModel`
11. `PlayerCard.uss` 新增 `.pc-hidden`
12. `DeskWindowController` 删除初始 `SetVisible(true)` 调用
13. `Assets/Scripts/Editor/ModelDebugWindow.cs` 新增
14. EditMode 测试：`PlayerCardModelTests`、`GameModelTests`、`PomodoroModel` 持久化测试扩展、Command 测试；删除 `PlayerCardPositionModelTests`；改写 `PlayerCardManagerTests`
15. 记录已知风险：PlayerId 会话级语义

## 10. 已知风险与后续议题

- **PlayerId 不稳定**：见 §1.1。持久化 key 语义依赖服务端，一旦服务端策略变化或用户换名/切房间，历史卡片位置 / pin 状态可能丢失。独立议题。
- **失焦信号真实源未接**：Editor 窗口只能手动注入 `IsAppFocused`。真实生产路径（`OnApplicationFocus` / `UniWindowController.OnFocusChanged` / `IActiveAppSystem`）留待后续会话决定。
- **`PlayerCardPinned` 写入离线 id 的策略**：当前选择 "Warn + 静默"。若未来允许 UI 在卡片离线后继续操作 pin（不太可能，但值得留意），需改为延迟写仓库。

