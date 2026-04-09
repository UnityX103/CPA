# Subtask 2: Unity 网络层（QFramework 集成）

**所属方案**: `/Users/xpy/Desktop/NanZhai/CPA/.omc/plans/multiplayer-pomodoro-plan.md` 的 **Phase 2 + Phase 5 网络测试部分**
**相关方案章节**: Phase 2（Step 2.1 - 2.6），Phase 5（Step 5.1, 5.3, 5.4）

## 文件所有权（严格独占）

你**只能**写入这些路径：

**新建（15 个）**
- `Assets/Scripts/APP/Network/Model/IRoomModel.cs`
- `Assets/Scripts/APP/Network/Model/RoomModel.cs`
- `Assets/Scripts/APP/Network/Model/RemotePlayerData.cs`
- `Assets/Scripts/APP/Network/Model/ConnectionStatus.cs`
- `Assets/Scripts/APP/Network/System/INetworkSystem.cs`
- `Assets/Scripts/APP/Network/System/NetworkSystem.cs`
- `Assets/Scripts/APP/Network/System/IStateSyncSystem.cs`
- `Assets/Scripts/APP/Network/System/StateSyncSystem.cs`
- `Assets/Scripts/APP/Network/NetworkDispatcherBehaviour.cs`
- `Assets/Scripts/APP/Network/Event/NetworkEvents.cs`
- `Assets/Scripts/APP/Network/DTO/InboundMessage.cs`
- `Assets/Scripts/APP/Network/DTO/OutboundMessage.cs`
- `Assets/Scripts/APP/Network/Command/Cmd_CreateRoom.cs`
- `Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs`
- `Assets/Scripts/APP/Network/Command/Cmd_LeaveRoom.cs`

**测试（4 个）**
- `Assets/Tests/EditMode/NetworkTests/RoomModelTests.cs`
- `Assets/Tests/EditMode/NetworkTests/MessageSerializationTests.cs`
- `Assets/Tests/EditMode/NetworkTests/StateSyncTests.cs`
- `Assets/Tests/EditMode/NetworkTests/MainThreadDispatcherTests.cs`
- 如需要，创建 `Assets/Tests/EditMode/NetworkTests/NetworkTests.asmdef`

**修改（1 个）**
- `Assets/Scripts/APP/Pomodoro/GameApp.cs` — 新增 `RegisterModel<IRoomModel>` 和 `RegisterSystem<INetworkSystem>` / `RegisterSystem<IStateSyncSystem>`

**严禁**写入 `Server/**`、`Assets/UI_V2/**`、`Assets/Scenes/**` 或修改 DeskWindowController.cs。

## QFramework 规范（严格执行）

参考项目 CLAUDE.md：
- Command 必须无状态（通过构造函数传参）
- System 不能持有 Controller 引用
- 下层 → 上层只能用 Event
- Model 通过 BindableProperty
- Architecture 注册顺序：Model → System

## 实施要点（方案 Step 2.1 - 2.6）

1. **Step 2.1 Model 层**：
   - `IRoomModel` 暴露 `BindableProperty<ConnectionStatus>`, `BindableProperty<string> RoomCode`, `BindableProperty<string> LocalPlayerId`, `IList<RemotePlayerData> RemotePlayers`
   - `ConnectionStatus` 枚举：Disconnected, Connecting, Connected, InRoom, Reconnecting, Error
   - `RemotePlayerData` 为普通 C# 类，含 id/name/pomodoro 快照字段
2. **Step 2.2 NetworkSystem + DTO**（方案这部分最详细，严格照抄）：
   - `.NET System.Net.WebSockets.ClientWebSocket` 使用
   - `ConcurrentQueue<Action> _mainThreadQueue` + `DrainMainThreadQueue()` 模式
   - [Serializable] DTO：`InboundMessage`, `SnapshotEntry`, `RemoteState`, `PomodoroStateDto`, `ActiveAppDto`, 以及 `OutboundMessage` 等
   - 在 `RemoteState` 和 `PomodoroStateDto` 上定义 **静态 EqualsLogical 方法**
   - **Disconnect()** 严格按照 Implementation Notes #1 的 async-safe 写法（先置空 `_cts=null; _ws=null;`，然后 cancel/close，最后 dispose + 清空队列）
   - **SendAsync** 加 `SemaphoreSlim(1,1)` 并发锁（Implementation Notes #2）
3. **Step 2.3 NetworkDispatcherBehaviour**：
   - 只负责 `Update` 里 `_networkSystem.DrainMainThreadQueue()`
   - 挂载点为 DeskWindow GameObject（Worker 3 或 scene 层负责挂载，你只写脚本）
   - **不在 NetworkDispatcherBehaviour 里调用 StateSyncSystem.Tick**（N-2 修订项）
4. **Step 2.4 StateSyncSystem**：
   - 累加器模式（参照 PomodoroTimerSystem Lines 56-75）
   - `Tick(float deltaTime)` 每 1 秒调用 `INetworkSystem.Send(buildLocalStateMessage())`
   - `ForceSyncNow()` 开头加 `IsInRoom && IsConnected` 守卫（N-1 修订项）
   - System → System 水平调用 `INetworkSystem.Send`（不用 Command）
5. **Step 2.5 Command**：
   - `Cmd_CreateRoom(string playerName)`, `Cmd_JoinRoom(string code, string playerName)`, `Cmd_LeaveRoom()`
   - 构造函数传参，OnExecute 里调 `this.GetSystem<INetworkSystem>().Send(...)`
6. **Step 2.6 GameApp 注册**：
   ```csharp
   RegisterModel<IRoomModel>(new RoomModel());
   RegisterSystem<INetworkSystem>(new NetworkSystem());
   RegisterSystem<IStateSyncSystem>(new StateSyncSystem());
   ```
   顺序必须在现有 PomodoroModel 之后、现有 System 之前/之后均可（不影响 Init）

## 测试要求

使用 Unity Test Runner (EditMode + NUnit)。测试不依赖真实 WebSocket（用 fake/spy）：

- `RoomModelTests.cs` — BindableProperty 变更、RemotePlayers 增删
- `MessageSerializationTests.cs` — 所有 DTO 的 JsonUtility 序列化/反序列化；EqualsLogical 正确性；空值/缺失字段兼容
- `StateSyncTests.cs` — 累加器节流、ForceSyncNow 守卫、phase 切换立即同步
- `MainThreadDispatcherTests.cs` — ConcurrentQueue → DrainMainThreadQueue 顺序保证、异常不破坏主线程循环

## 验收标准

- [ ] 所有新建文件通过 Unity 编译（无 CS 错误）
- [ ] EditMode 测试全部通过（通过 `mcp__plugin_oh-my-claudecode_t__lsp_diagnostics_directory` 或 unity-agent 触发 `run_tests`）
- [ ] QFramework 规范 0 违反（code review 自检）
- [ ] `GameApp.cs` 修改最小化，只在 `Init()` 中添加注册调用

## 完成后的输出

在子任务 transition 到 done 之前：
1. 运行 `mcp__plugin_oh-my-claudecode_t__lsp_diagnostics_directory` 对 `Assets/Scripts/APP/Network/` 做诊断，确认 0 错误
2. 在 mailbox 发送摘要给 leader-fixed：`{"files_created": N, "qframework_violations": 0, "compile_errors": 0}`
