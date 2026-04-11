# Phase 4 联机番茄钟审查报告（worker-1）

## 审查范围

- `Assets/UI_V2/Controller/PlayerCardView.cs`
- `Assets/UI_V2/Controller/PlayerCardManager.cs`
- `Assets/UI_V2/Controller/DeskWindowController.cs`

审查重点：

- QFramework 约束：Controller 只读 Model，写操作通过 Command
- UI Toolkit 事件与视图生命周期
- 事件解绑
- 空引用/模板缺失防御

## 结论

QFramework 读写边界整体是合规的：本次审查的 Controller 代码没有直接写 `IRoomModel` / `IPomodoroModel`，房间相关写操作都通过 `Cmd_CreateRoom`、`Cmd_JoinRoom`、`Cmd_LeaveRoom` 发起。主要风险不在“直接写 Model”，而在“本地状态重置后的 UI 刷新链不完整”、联机服务器输入未接线，以及卡片模板缺失时的容错不足。

## Findings

### 1. 高: 本地房间状态被清空后，成员列表和玩家卡片不会同步清理，UI 会残留旧房间数据

- `DeskWindowController` 只在 `E_PlayerJoined`、`E_PlayerLeft`、`E_RoomSnapshot`、`E_RoomJoined` 上刷新成员列表，没有监听“本地清房间状态”的任何信号：
  - `Assets/UI_V2/Controller/DeskWindowController.cs:353`
  - `Assets/UI_V2/Controller/DeskWindowController.cs:360`
  - `Assets/UI_V2/Controller/DeskWindowController.cs:451`
- `PlayerCardManager` 也是纯事件驱动，只在远端玩家加入/离开/快照/入房时重建卡片：
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:63`
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:80`
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:145`
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:212`
- 但本地“创建房间 / 加入房间 / 离开房间 / 断开连接”都会直接清空 `RoomModel.RemotePlayers`，且这些路径没有补发 `E_RoomSnapshot` / `E_PlayerLeft` 一类的 UI 刷新事件：
  - `Assets/Scripts/APP/Network/Command/Cmd_CreateRoom.cs:23`
  - `Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs:25`
  - `Assets/Scripts/APP/Network/Command/Cmd_LeaveRoom.cs:18`
  - `Assets/Scripts/APP/Network/System/NetworkSystem.cs:67`
  - `Assets/Scripts/APP/Network/System/NetworkSystem.cs:591`

影响：

- 点击“离开房间”后，`RoomModel` 已清空，但 `osp-members-list` 不会刷新，旧成员名仍然留在 UI 上。
- 远端玩家卡片也不会被 `PlayerCardManager.Clear()` 清掉，直到后续偶然收到新的快照事件。
- “重新建房/换房失败”时，旧房间残影会继续停留，和 Model 实际状态不一致。

建议：

- 明确设计一个“本地房间状态已重置”的刷新信号，例如在命令/系统层统一补发快照清空事件，或让 UI 绑定到可观察的房间状态变化而不是只依赖网络事件。

### 2. 中: 联机面板里的服务器地址输入框是死字段，用户修改后不会生效

- UI 上暴露了 `osp-server-url` 输入框：
  - `Assets/UI_V2/Documents/OnlineSettingsPanel.uxml:29`
- `DeskWindowController` 也确实拿到了这个字段引用：
  - `Assets/UI_V2/Controller/DeskWindowController.cs:324`
- 但创建房间和加入房间时，Controller 没有读取该值并传给 Command：
  - `Assets/UI_V2/Controller/DeskWindowController.cs:369`
  - `Assets/UI_V2/Controller/DeskWindowController.cs:382`
- 命令层反而把服务器地址硬编码成 `ws://localhost:8765`：
  - `Assets/Scripts/APP/Network/Command/Cmd_CreateRoom.cs:10`
  - `Assets/Scripts/APP/Network/Command/Cmd_CreateRoom.cs:26`
  - `Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs:10`
  - `Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs:28`

影响：

- Phase 4 UI 给了用户“可配置服务器”的假象，但任何输入都被忽略。
- 如果测试或联调环境不是本机 `localhost:8765`，当前 UI 无法工作。

建议：

- 要么删掉该输入框，避免误导；
- 要么把服务器地址纳入 Command 入参或配置源，保持 UI 和执行路径一致。

### 3. 中: 玩家卡片模板缺失时只有静默失败，没有 fallback，也缺少足够诊断

- `DeskWindowController` 初始化 `PlayerCardManager` 时不校验 `_playerCardUxml`：
  - `Assets/UI_V2/Controller/DeskWindowController.cs:127`
  - `Assets/UI_V2/Controller/DeskWindowController.cs:128`
- `PlayerCardManager.AddOrUpdate` 在 `_cardTemplate == null` 时直接返回，不打日志、不走兜底构造：
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:170`
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:172`
- `PlayerCardView` 本身也没有 null-template fallback，直接抛 `ArgumentNullException`：
  - `Assets/UI_V2/Controller/PlayerCardView.cs:38`
  - `Assets/UI_V2/Controller/PlayerCardView.cs:40`

影响：

- 只要 Inspector 漏绑 `PlayerCard.uxml`，多人卡片功能就会整体失效，但现场没有明确错误指引。
- 当前实现和“模板为空时 fallback 构造不崩溃”的预期不一致，后续测试补齐时大概率会暴露这个缺口。

建议：

- 至少在初始化或首次建卡失败时输出明确错误日志；
- 如果产品要求容错，应补一个最小可用的 fallback `VisualElement` 构造路径。

## Notes

### QFramework 约束检查

- 本次审查范围内未发现 Controller 直接写 Model：
  - `DeskWindowController` 的房间写操作经由 `SendCommand(...)` 发起：
    - `Assets/UI_V2/Controller/DeskWindowController.cs:379`
    - `Assets/UI_V2/Controller/DeskWindowController.cs:400`
    - `Assets/UI_V2/Controller/DeskWindowController.cs:406`
  - `PlayerCardManager` 只在 `OnStateUpdated` 中读取 `IRoomModel`：
    - `Assets/UI_V2/Controller/PlayerCardManager.cs:128`

### 事件解绑检查

- 当前生产调用路径上，QFramework 事件解绑基本正确：
  - `DeskWindowController` 的 Model/Event 订阅都用了 `UnRegisterWhenGameObjectDestroyed(gameObject)`：
    - `Assets/UI_V2/Controller/DeskWindowController.cs:275`
    - `Assets/UI_V2/Controller/DeskWindowController.cs:362`
  - `PlayerCardManager` 由 `DeskWindowController` 传入 `gameObject`，也会自动反注册：
    - `Assets/UI_V2/Controller/PlayerCardManager.cs:61`
    - `Assets/UI_V2/Controller/DeskWindowController.cs:128`
- 但 `PlayerCardManager.Initialize(..., lifecycleOwner: null)` 的分支会注册事件却不保存 `IUnRegister`，如果未来有其他调用方走到这条路径，会留下悬挂订阅：
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:74`
  - `Assets/UI_V2/Controller/PlayerCardManager.cs:80`
