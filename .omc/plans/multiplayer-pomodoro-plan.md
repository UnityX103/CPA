# 桌宠多人同步系统 - 实施方案（v2 修订版）

> **修订说明**：本版根据 Architect + Critic 联合评审结果修订。核心变更：
> 1. **Phase 3 (AppMonitor) 降级到 v2**，v1 仅同步番茄钟状态
> 2. **锁定 WebSocket 主线程调度实现**（ConcurrentQueue + MonoBehaviour Drain）
> 3. **锁定 StateSyncSystem tick 源**（复用 DeskWindowController.Update）
> 4. **删除 Cmd_SyncLocalState**，StateSyncSystem 直接调 NetworkSystem.Send
> 5. **协议补全** room_snapshot、iconId 预留、protocolVersion
> 6. **明确 PlayerCard 挂载策略**与 DeskWindowController 事件解耦
> 7. **新增 Phase 5.5 集成测试**
> 8. **加入 BaaS 替代方案拒绝理由**

---

## 需求摘要

为桌面宠物番茄钟项目添加多人同步能力（v1）：
1. **Node.js WebSocket 后端** - 房间码加入机制
2. **番茄钟状态同步** - 其他玩家可看到但不可编辑
3. **玩家面板** - 独立浮动卡片，可拖拽，展示宠物占位 + 番茄钟状态
4. **单元测试 + 集成测试** - 通过 Unity MCP 和 Node.js test runner 验证

> **v2 延期**：macOS 聚焦软件检测（AppMonitor 原生插件重建）。v1 协议预留字段供未来接入。

## 为什么选择自建 Node.js 而非 BaaS

Critic 提出"是否考虑过 Firebase/Supabase Realtime？"的合理质疑。本方案明确选择自建的理由：

| 维度 | 自建 Node.js + ws | Firebase/Supabase Realtime |
|------|-------------------|----------------------------|
| **协议可控** | 完全自定义消息格式 | 绑定厂商 SDK 格式 |
| **房间生命周期** | 自定义 30 秒空置销毁 | 需在客户端模拟，逻辑分散 |
| **延迟** | 本地/自建 VPS，可控 | 地区依赖，国内访问不稳定 |
| **未来互动功能** | 易加 `targetPlayerId` 定向消息 | 需额外 Firestore 规则或 Cloud Functions |
| **离线开发** | `localhost:8765` 零配置 | 需网络 + 账号 |
| **依赖风险** | 零第三方锁定 | 厂商迁移成本高 |
| **学习价值** | 完整掌握协议栈 | 仅调用 SDK |
| **成本** | 自建 VPS（可选），本地零成本 | 免费额度受限 |

**决策**：桌宠项目规模小、房间数少（预计 ≤100 并发），自建 Node.js 单进程足够；且未来要加"互相投食""战吃瓜子"等定向互动时，自建协议扩展成本更低。BaaS 的实时订阅模型对"点对点指令"并不擅长。

## 验收标准（v1）

- [ ] AC-1: 用户可创建房间并获得 6 位房间码（排除易混淆字符 `0/O/1/I/L`）
- [ ] AC-2: 其他用户输入房间码可加入房间；**网络端到端延迟 ≤ 500ms**
  - 测量方法：客户端 A 点击"开始"到客户端 B 的 PlayerCard `IsRunning` 变为 true 的 Unity 帧时间戳差
  - 测试脚本：`Server/test/latency.test.js` 使用两个 ws 客户端模拟
- [ ] AC-3: 房间内所有成员实时看到彼此的番茄钟状态（阶段、剩余时间、轮次），**更新频率 1 Hz**
  - 测量方法：客户端 B 订阅 `E_RemoteStateUpdated` 次数 ÷ 观察时长
- [ ] AC-4: 其他玩家的番茄钟面板为只读（无按钮、无交互），与本地面板视觉区分（配色/尺寸）
- [ ] AC-5: 新玩家加入时立即收到 `room_snapshot` 包含所有在场玩家的最新状态快照（**冷启动时间 ≤ 1 秒**）
- [ ] AC-6: 每个远程玩家显示为独立浮动卡片，包含：昵称、宠物占位（32×32 空块）、番茄钟阶段/剩余时间/轮次
- [ ] AC-7: 所有玩家卡片可自由拖拽，位置在当次会话内保持（**不持久化**）；拖拽不会触发番茄钟面板收纳
- [ ] AC-8: 玩家离开房间或断线超时（30 秒）时其卡片自动移除
- [ ] AC-9: 断线重连后收到 `room_snapshot` 恢复所有玩家状态，总恢复时间 ≤ 3 秒
- [ ] AC-10: 所有核心逻辑有单元测试 + 集成测试，通过 Unity MCP `run_tests`（EditMode）和 `node --test`（服务器）验证

**v2 延期验收（本次不做）**：
- ~~AC-5: 聚焦软件检测同步~~
- ~~AC-6: 软件名称/图标字段~~（卡片保留占位区域供 v2 填充）

## 架构总览

```
┌─────────────────────────────────────────────────┐
│                  Unity Client                    │
│                                                  │
│  ┌──────────┐                ┌───────────────┐  │
│  │Pomodoro  │                │  Networking   │  │
│  │(existing)│                │    (new)      │  │
│  └────┬─────┘                └───────┬───────┘  │
│       │                              │          │
│  ┌────┴──────────────────────────────┴───────┐  │
│  │              GameApp (QFramework)           │  │
│  │  +IRoomModel  +INetworkSystem              │  │
│  │  +IStateSyncSystem  (v2: +IAppMonitor*)    │  │
│  └──┬────────────────────┬────────────────────┘  │
│     │                    │                        │
│     │ Events             │ ConcurrentQueue<Action>│
│     ▼                    │ (main thread dispatch) │
│  PlayerCardManager       │                        │
│  (UI Toolkit)            ▼                        │
│                  NetworkDispatcherBehaviour        │
│                  (MonoBehaviour, Update drain)     │
│                          │                        │
│                          │ .NET ClientWebSocket   │
└──────────────────────────┼────────────────────────┘
                           │
                ┌──────────┴────────────┐
                │  Node.js Server       │
                │  (ws + RoomManager)   │
                │                       │
                │  Room Snapshot Cache  │
                │  State Broadcast      │
                └───────────────────────┘
```

## 实施步骤

---

### Phase 1: 后端 WebSocket 服务器

**目标**: 建立 Node.js 服务器，支持房间创建/加入/离开、状态广播、快照恢复

#### Step 1.1: 项目初始化
- **位置**: `Server/` (项目根目录下新建)
- **操作**:
  - `npm init` 创建 `package.json`（type: "module"）
  - 依赖: `ws@^8`, `nanoid@^5`（生成房间码）
  - 入口: `Server/src/index.js`
  - 测试: 使用 Node.js 内置 `node --test`（避免引入 jest 依赖）

#### Step 1.2: 房间管理器
- **文件**: `Server/src/RoomManager.js`
- **数据结构**:
  ```js
  Room = {
    code: "ABC234",                    // 6 位，字符集: ABCDEFGHJKMNPQRSTUVWXYZ23456789
    createdAt: timestamp,
    destroyTimer: null,                // 空房间 30s 销毁定时器
    players: Map<playerId, Player>     // 最大 8 人
  }
  Player = {
    id: uuid,
    name: string,                      // 1-16 字符
    ws: WebSocket,
    joinedAt: timestamp,
    lastSeenAt: timestamp,             // 心跳更新
    latestState: RemoteState | null    // 最新状态快照（用于 room_snapshot）
  }
  RemoteState = {
    pomodoro: { phase, remainingSeconds, currentRound, totalRounds, isRunning },
    activeApp: null                    // v2 预留
  }
  ```
- **API**:
  - `createRoom(hostId, hostName, ws)` → Room
  - `joinRoom(code, playerId, playerName, ws)` → Room | Error
  - `leaveRoom(code, playerId)` → void
  - `updatePlayerState(code, playerId, state)` → void（更新 latestState）
  - `getRoomSnapshot(code)` → Player[] with latest state
  - 空房间延迟销毁：最后一人离开后 30 秒销毁；期间新加入取消销毁

#### Step 1.3: 消息协议（含 protocolVersion）
- **文件**: `Server/src/protocol.js`
- **通用字段**: 所有消息带 `v: 1`（协议版本号）

```json
// === 客户端 → 服务器 ===
{ "v": 1, "type": "create_room", "playerName": "小明" }
{ "v": 1, "type": "join_room", "code": "ABC234", "playerName": "小红" }
{ "v": 1, "type": "leave_room" }
{ "v": 1, "type": "sync_state", "data": {
    "pomodoro": { "phase": 0, "remainingSeconds": 1200, "currentRound": 1, "totalRounds": 4, "isRunning": true }
  }
}
{ "v": 1, "type": "ping" }

// === 服务器 → 客户端 ===
{ "v": 1, "type": "room_created", "code": "ABC234", "playerId": "uuid", "snapshot": [...] }
{ "v": 1, "type": "room_joined", "code": "ABC234", "playerId": "uuid", "snapshot": [...] }
{ "v": 1, "type": "room_snapshot", "snapshot": [
    { "playerId": "uuid", "playerName": "小明", "state": { ... RemoteState ... } },
    ...
  ]
}
{ "v": 1, "type": "player_joined", "playerId": "uuid", "playerName": "小红", "state": null }
{ "v": 1, "type": "player_left", "playerId": "uuid" }
{ "v": 1, "type": "state_update", "playerId": "uuid", "state": { ... RemoteState ... } }
{ "v": 1, "type": "error", "code": "ROOM_NOT_FOUND|ROOM_FULL|INVALID_VERSION|...", "message": "..." }
{ "v": 1, "type": "pong" }
```

- **关键约定**:
  - `room_created`/`room_joined` 的 `snapshot` 字段总是包含当前房间所有玩家的最新状态（含自己），用于客户端冷启动
  - `state_update` 只在有变化时广播；服务端保存 latestState 供新人冷启动
  - **protocolVersion 不匹配**时返回 `error { code: "INVALID_VERSION" }` 并断开

#### Step 1.4: WebSocket 服务器
- **文件**: `Server/src/index.js`
- **职责**:
  - 监听 `ws://0.0.0.0:8765`（本地开发）；生产部署时 nginx 反代 wss
  - 收到新连接等待首条合法消息（`create_room` 或 `join_room`），否则 10 秒后断开
  - 心跳：服务端每 20 秒发 `ping`，客户端 `pong`；60 秒未收到任何消息视为超时断开
  - **转发节流**：同一玩家的 `sync_state` **最多 10 次/秒（滑动窗口）**作为 DoS 防御；10Hz 窗口远高于客户端正常 1Hz 流量，足以吸收阶段切换等突发事件，**无需特殊 priority 字段**——服务端按玩家 ID 维护上次状态指纹，指纹变化的消息即时广播
  - 广播策略：`state_update` 广播给同房间其他成员（不包括发送者）
  - 空房间销毁：最后一人离开后 30 秒销毁

#### Step 1.5: 服务器测试
- **文件**: `Server/test/room.test.js`, `Server/test/protocol.test.js`, `Server/test/latency.test.js`
- **覆盖**:
  - `RoomManager` CRUD、空房销毁防抖、满员（8人）拒绝、无效房间码、状态更新、快照生成
  - 协议版本校验、消息格式校验、非法 JSON 拒绝
  - **集成/延迟测试**：启动真实 ws 服务端 + 2 个 ws 客户端，验证 join → state_update 的端到端延迟 ≤ 500ms
- **运行**: `npm test`（调用 `node --test test/`）

---

### Phase 2: Unity 网络层（QFramework 集成）

**目标**: 建立 WebSocket 客户端，融入 QFramework 架构，安全处理主线程调度

#### Step 2.1: 网络 Model

- **文件**: `Assets/Scripts/APP/Network/Model/IRoomModel.cs`
  ```csharp
  public interface IRoomModel : IModel
  {
      BindableProperty<string> RoomCode { get; }
      BindableProperty<bool> IsConnected { get; }
      BindableProperty<bool> IsInRoom { get; }
      BindableProperty<string> LocalPlayerName { get; }
      BindableProperty<string> LocalPlayerId { get; }
      BindableProperty<ConnectionStatus> Status { get; }  // Disconnected/Connecting/Connected/Reconnecting/Failed

      // 只读远程玩家访问（供 PlayerCardManager 初始化快照读取）
      IReadOnlyDictionary<string, RemotePlayerData> RemotePlayers { get; }

      // 以下由内部调用（通过 internal 或 friend pattern）
      void AddOrUpdateRemotePlayer(RemotePlayerData data);
      void RemoveRemotePlayer(string playerId);
      void ClearRemotePlayers();
  }
  ```

- **文件**: `Assets/Scripts/APP/Network/Model/RemotePlayerData.cs`
  ```csharp
  public sealed class RemotePlayerData
  {
      public string PlayerId;
      public string PlayerName;

      // 番茄钟字段
      public PomodoroPhase Phase;
      public int RemainingSeconds;
      public int CurrentRound;
      public int TotalRounds;
      public bool IsRunning;

      // v2 预留（v1 为 null/空）
      public string ActiveAppName;   // v2
      public string ActiveAppBundleId;  // v2
      public byte[] ActiveAppIcon;   // v2
  }
  ```

- **文件**: `Assets/Scripts/APP/Network/Model/RoomModel.cs`
  - 内部 `Dictionary<string, RemotePlayerData>` 通过 `IReadOnlyDictionary` 暴露
  - `AddOrUpdateRemotePlayer` 写入后由调用方（NetworkSystem 主线程路径）发 `E_RemoteStateUpdated`

#### Step 2.2: INetworkSystem（锁定主线程调度）

- **文件**: `Assets/Scripts/APP/Network/System/INetworkSystem.cs`
  ```csharp
  public interface INetworkSystem : ISystem
  {
      void Connect(string serverUrl, string playerName);
      void Disconnect();
      void Send(object message);    // 序列化为 JSON 并通过 ws 发送
      void DrainMainThreadQueue();  // 由 MonoBehaviour Update 调用
  }
  ```

- **文件**: `Assets/Scripts/APP/Network/System/NetworkSystem.cs`
  - **实现锁定**：
    ```csharp
    private ClientWebSocket _ws;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    private CancellationTokenSource _cts;

    public void Connect(string url, string playerName)
    {
        _cts = new CancellationTokenSource();
        _ws = new ClientWebSocket();
        _ = RunAsync(url, playerName, _cts.Token);
    }

    private async Task RunAsync(string url, string playerName, CancellationToken ct)
    {
        try
        {
            await _ws.ConnectAsync(new Uri(url), ct);
            EnqueueMainThread(() => this.SendEvent(new E_ConnectionStateChanged { IsConnected = true }));

            var buffer = new byte[8192];
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                string json = Encoding.UTF8.GetString(ms.ToArray());
                // 解析发生在非主线程（JSON 解析是线程安全的）
                var msg = JsonUtility.FromJson<InboundMessage>(json);
                // ❗ 关键：所有上层事件派发必须 Enqueue 到主线程
                EnqueueMainThread(() => DispatchInbound(msg));
            }
        }
        catch (OperationCanceledException) { /* expected */ }
        catch (Exception ex)
        {
            EnqueueMainThread(() => this.SendEvent(new E_NetworkError { Message = ex.Message }));
            EnqueueMainThread(() => TryReconnect(url, playerName));
        }
    }

    private void EnqueueMainThread(Action action) => _mainThreadQueue.Enqueue(action);

    public void DrainMainThreadQueue()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try { action(); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    public void Send(object message)
    {
        if (_ws?.State != WebSocketState.Open) return;
        string json = JsonUtility.ToJson(message);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        // 发送可在主线程调用，ClientWebSocket.SendAsync 是线程安全的
        _ = _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            if (_ws?.State == WebSocketState.Open)
            {
                // 不等待 CloseAsync，避免主线程阻塞；后台 Task 会在 Cancel 后自然退出
                _ = _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disconnect", CancellationToken.None);
            }
        }
        catch (Exception ex) { Debug.LogException(ex); }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _ws?.Dispose();
            _ws = null;
            // 清空未处理的主线程队列，避免跨连接串消息
            while (_mainThreadQueue.TryDequeue(out _)) { }
        }
    }
    ```
  - **关键规则**：
    1. 所有 `this.SendEvent(...)` 必须在 `DrainMainThreadQueue` 中执行
    2. `DispatchInbound` 内部调用 `_roomModel.AddOrUpdateRemotePlayer` 和 `this.SendEvent`
    3. 重连：首次失败后 2s/4s/8s/16s/32s 指数退避，最多 5 次；第 5 次失败发送 `E_ConnectionStateChanged { Status = Failed }` 并停止

- **JSON 库选择**：使用 `UnityEngine.JsonUtility`（内置，零依赖，性能够用）
- **DTO 设计**（`Assets/Scripts/APP/Network/DTO/InboundMessage.cs`）：

  ```csharp
  // 扁平化 InboundMessage —— 所有可能字段都列出，按 type 分发
  [Serializable]
  public class InboundMessage
  {
      public int v;
      public string type;
      public string code;          // room_created, room_joined
      public string playerId;      // room_created, room_joined, player_joined, player_left, state_update
      public string playerName;    // player_joined
      public RemoteState state;    // state_update, player_joined (initial)
      public List<SnapshotEntry> snapshot;  // room_created, room_joined, room_snapshot
      public string errorCode;     // error
      public string message;       // error
  }

  [Serializable]
  public class SnapshotEntry
  {
      public string playerId;
      public string playerName;
      public RemoteState state;    // 允许为 null（新加入玩家还没发过状态）
  }

  [Serializable]
  public class RemoteState
  {
      public PomodoroStateDto pomodoro;
      public ActiveAppDto activeApp;  // v1 固定为 null

      public static bool EqualsLogical(RemoteState a, RemoteState b)
      {
          if (a == null || b == null) return a == b;
          return PomodoroStateDto.EqualsLogical(a.pomodoro, b.pomodoro);
          // v1 不比较 activeApp，v2 扩展
      }
  }

  [Serializable]
  public class PomodoroStateDto
  {
      public int phase;
      public int remainingSeconds;
      public int currentRound;
      public int totalRounds;
      public bool isRunning;

      public static bool EqualsLogical(PomodoroStateDto a, PomodoroStateDto b)
      {
          if (a == null || b == null) return a == b;
          return a.phase == b.phase
              && a.remainingSeconds == b.remainingSeconds
              && a.currentRound == b.currentRound
              && a.totalRounds == b.totalRounds
              && a.isRunning == b.isRunning;
      }
  }

  [Serializable]
  public class ActiveAppDto  // v2 填充
  {
      public string name;
      public string bundleId;
      public string iconId;
  }
  ```

- **OutboundMessage DTO**（`Assets/Scripts/APP/Network/DTO/OutboundMessage.cs`）：对 `create_room / join_room / leave_room / sync_state / ping` 分别定义 `[Serializable]` 类，避免 JsonUtility 对顶层多态的不支持

#### Step 2.3: NetworkDispatcherBehaviour（仅网络 IO 主线程驱动）

- **文件**: `Assets/Scripts/APP/Network/NetworkDispatcherBehaviour.cs`
  ```csharp
  [DefaultExecutionOrder(-1000)]  // 在 DeskWindowController 之前执行
  public sealed class NetworkDispatcherBehaviour : MonoBehaviour, IController
  {
      IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

      private void Update()
      {
          // 仅负责网络 IO：把非主线程 Enqueue 的消息派发回主线程
          // ❗ 不负责 StateSyncSystem.Tick —— 见 Step 2.4 说明
          this.GetSystem<INetworkSystem>().DrainMainThreadQueue();
      }

      private void OnDestroy()
      {
          this.GetSystem<INetworkSystem>().Disconnect();
      }
  }
  ```
- **挂载点（锁定）**: **与 DeskWindowController 相同的 GameObject**（场景 `MainV2.unity` 中的 `DeskWindow` GameObject）
- **职责范围**：仅 `DrainMainThreadQueue` + OnDestroy 清理
- **为什么不在此处调用 StateSyncSystem.Tick**：若在这里调用，`DefaultExecutionOrder(-1000)` 会让 Tick **早于** DeskWindowController.Update 中的 `Cmd_PomodoroTick` 执行，导致 StateSyncSystem 读取的是**上一帧末**的 PomodoroModel 状态，同步数据慢一帧。故 Tick 放到 DeskWindowController.Update 末尾（见 Step 2.4）

#### Step 2.4: IStateSyncSystem（无 Command 版，由 DeskWindowController 驱动 Tick）

- **文件**: `Assets/Scripts/APP/Network/System/IStateSyncSystem.cs`
  ```csharp
  public interface IStateSyncSystem : ISystem
  {
      void Tick(float deltaTime);
      void ForceSyncNow();  // 供阶段切换等事件立即触发
  }
  ```

- **Tick 调用点（锁定）**：**由 `DeskWindowController.Update()` 的末尾调用**，位于现有 `Cmd_PomodoroTick` 之后。这样确保 StateSyncSystem 读取的是**本帧已更新**的 PomodoroModel，延迟最小。

  ```csharp
  // DeskWindowController.cs:104 修改后
  private void Update()
  {
      this.SendCommand(new Cmd_PomodoroTick(Time.deltaTime));
      // 新增：Tick 状态同步（番茄钟状态已在本帧更新，读取最新值）
      this.GetSystem<IStateSyncSystem>().Tick(Time.unscaledDeltaTime);
  }
  ```

- **文件**: `Assets/Scripts/APP/Network/System/StateSyncSystem.cs`
  - **实现要点**（参照 `PomodoroTimerSystem.cs:56-75` 累加器模式）：
    ```csharp
    private float _accumulator;
    private RemoteState _lastSent;  // 初始为 null
    private float _timeSinceLastSent;

    protected override void OnInit()
    {
        // 订阅关键事件：阶段切换时强制同步，绕过 1Hz 采样
        this.RegisterEvent<E_PomodoroPhaseChanged>(_ => ForceSyncNow());
    }

    public void Tick(float dt)
    {
        var room = this.GetModel<IRoomModel>();
        if (!room.IsConnected.Value || !room.IsInRoom.Value) return;

        _accumulator += dt;
        _timeSinceLastSent += dt;

        // 1Hz 采样点
        if (_accumulator < 1f) return;
        _accumulator = 0f;

        var current = CollectLocalState();
        bool changed = _lastSent == null || !RemoteState.EqualsLogical(current, _lastSent);

        // 有变化 → 发送；无变化 → 每 5 秒发一次 keepalive
        if (changed || _timeSinceLastSent >= 5f)
        {
            SendState(current);
        }
    }

    public void ForceSyncNow()
    {
        // ❗ 守卫：未加入房间时不发送（与 Tick 守卫对齐）
        var room = this.GetModel<IRoomModel>();
        if (!room.IsConnected.Value || !room.IsInRoom.Value) return;

        var current = CollectLocalState();
        SendState(current);
    }

    private void SendState(RemoteState state)
    {
        // ❗ 关键：StateSyncSystem → INetworkSystem 水平 System 调用，不经 Command
        this.GetSystem<INetworkSystem>().Send(new OutboundSyncState { v = 1, type = "sync_state", data = state });
        _lastSent = state;
        _timeSinceLastSent = 0f;
    }

    private RemoteState CollectLocalState()
    {
        var pomo = this.GetModel<IPomodoroModel>();
        return new RemoteState
        {
            pomodoro = new PomodoroStateDto
            {
                phase = (int)pomo.CurrentPhase.Value,
                remainingSeconds = pomo.RemainingSeconds.Value,
                currentRound = pomo.CurrentRound.Value,
                totalRounds = pomo.TotalRounds.Value,
                isRunning = pomo.IsRunning.Value
            },
            activeApp = null  // v2 填充
        };
    }
    ```
  - **主线程保证**：`E_PomodoroPhaseChanged` 由 `Cmd_PomodoroTick → PomodoroTimerSystem.AdvancePhase → SendEvent` 触发，事件链完全在 Unity 主线程内执行（Cmd_PomodoroTick 由 DeskWindowController.Update 发起），因此 `ForceSyncNow` 保证在主线程运行，可安全调用 `INetworkSystem.Send`（`ClientWebSocket.SendAsync` 线程安全）
  - **Command 边界**：**不**引入 `Cmd_SyncLocalState`。StateSyncSystem → INetworkSystem 是 System→System 水平调用，符合 QFramework 规范（`CLAUDE.md` 层级规则只要求 Controller→写操作必须走 Command，System→System 水平调用无限制）
  - **`EqualsLogical` 定义**：静态方法 `RemoteState.EqualsLogical(a, b)` 比较 pomodoro 子对象的 5 个字段（phase/remainingSeconds/currentRound/totalRounds/isRunning）+ activeApp（v1 都是 null，自动相等）

#### Step 2.5: 网络 Commands（仅用户发起的写操作）

- **文件**: `Assets/Scripts/APP/Network/Command/Cmd_CreateRoom.cs`
- **文件**: `Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs`
- **文件**: `Assets/Scripts/APP/Network/Command/Cmd_LeaveRoom.cs`
- 每个 Command 职责：更新 RoomModel 的 LocalPlayerName，调用 `INetworkSystem.Connect()` 和 `Send()`，本身无状态

#### Step 2.6: 网络 Events
- **文件**: `Assets/Scripts/APP/Network/Event/NetworkEvents.cs`
  ```csharp
  public struct E_RoomCreated { public string Code; }
  public struct E_RoomJoined { public string Code; public List<RemotePlayerData> InitialPlayers; }
  public struct E_PlayerJoined { public RemotePlayerData Player; }
  public struct E_PlayerLeft { public string PlayerId; }
  public struct E_RemoteStateUpdated { public string PlayerId; }  // 订阅者从 RoomModel.RemotePlayers[id] 读
  public struct E_ConnectionStateChanged { public ConnectionStatus Status; }
  public struct E_NetworkError { public string Code; public string Message; }
  public struct E_RoomSnapshot { public List<RemotePlayerData> Players; }
  ```

#### Step 2.7: GameApp 注册扩展
- **修改**: `Assets/Scripts/APP/Pomodoro/GameApp.cs`
  ```csharp
  RegisterModel<IPomodoroModel>(new PomodoroModel());
  RegisterModel<IRoomModel>(new RoomModel());           // 新增
  RegisterSystem<IPomodoroTimerSystem>(new PomodoroTimerSystem());
  RegisterSystem<IWindowPositionSystem>(new WindowPositionSystem());
  RegisterSystem<INetworkSystem>(new NetworkSystem());  // 新增
  RegisterSystem<IStateSyncSystem>(new StateSyncSystem()); // 新增
  ```

---

### Phase 3: ~~AppMonitor 重建~~ **（v2 延期，本次不做）**

**延期理由**（综合 Critic/Architect 分析）：

1. **权限复杂度**：`CGWindowListCopyWindowInfo` 获取窗口标题需要 macOS Screen Recording 权限，首次运行需用户授权，体验差
2. **plist 不匹配**：现有 `NSAccessibilityUsageDescription` 对应无障碍 API，与 `NSWorkspace` API 不符
3. **跨平台问题**：Windows/Linux 用户无对应实现，需额外降级 UI
4. **原生插件构建**：`.bundle` 需 Xcode 编译 + codesign 纳入 `build_macos.sh`，涉及构建管线改造
5. **对核心价值贡献小**：多人番茄钟的核心是"一起专注"，看到对方在用什么软件是 nice-to-have 且有隐私顾虑

**v2 接入预留**：
- 协议的 `RemoteState.activeApp` 字段在 v1 就定义为 `null`
- `RemotePlayerData` 保留 `ActiveAppName/BundleId/Icon` 字段
- `PlayerCard.uxml` 保留 `app-info` 区域（v1 显示"—"占位）
- v2 实施时新增 AppMonitor Model/System，StateSyncSystem 在 `CollectLocalState` 里填充 `activeApp` 即可

---

### Phase 4: 玩家面板 UI（UI Toolkit）

**目标**: 为每个远程玩家创建独立浮动可拖拽卡片，**不破坏现有 DeskWindowController 的事件路由**

#### Step 4.1: 玩家卡片 UXML
- **文件**: `Assets/UI_V2/Documents/PlayerCard.uxml`
- **结构**:
  ```xml
  <ui:UXML>
    <Style src="../Styles/PlayerCard.uss"/>
    <ui:VisualElement class="pc-root">
      <ui:VisualElement class="pc-header">
        <ui:VisualElement class="pc-pet-placeholder"/>   <!-- 32×32 宠物占位 -->
        <ui:Label class="pc-name" text="玩家"/>
      </ui:VisualElement>
      <ui:VisualElement class="pc-pomodoro-row">
        <ui:Label class="pc-phase" text="待机"/>
        <ui:Label class="pc-time" text="--:--"/>
        <ui:Label class="pc-rounds" text="0/0"/>
      </ui:VisualElement>
      <ui:VisualElement class="pc-app-row" name="app-info">
        <ui:Label class="pc-app-placeholder" text="—"/>  <!-- v2 填充 -->
      </ui:VisualElement>
    </ui:VisualElement>
  </ui:UXML>
  ```
- **尺寸**: 220×120px，圆角 12px，半透明背景 `rgba(0,0,0,0.6)`
- **USS 类名前缀**: `pc-*`（避免与现有 `dw-*/pp-*` 冲突）

#### Step 4.2: 玩家卡片 USS
- **文件**: `Assets/UI_V2/Styles/PlayerCard.uss`
- **要点**:
  - `.pc-root { position: absolute; width: 220px; height: 120px; }`
  - 主题色复用 `Variables.uss` 中的 `--color-focus / --color-rest / --color-paused`
  - 不同阶段动态切换类名 `pc-phase-focus/pc-phase-rest/pc-phase-paused/pc-phase-idle`

#### Step 4.3: 拖拽系统（事件解耦）
- **文件**: `Assets/UI_V2/Controller/DraggableElement.cs`
  ```csharp
  public static class DraggableElement
  {
      public static void MakeDraggable(VisualElement target, VisualElement dragHandle = null)
      {
          var handle = dragHandle ?? target;
          Vector2 pointerStart = default;
          Vector2 elementStart = default;
          bool dragging = false;

          handle.RegisterCallback<PointerDownEvent>(evt =>
          {
              pointerStart = evt.position;
              elementStart = new Vector2(target.resolvedStyle.left, target.resolvedStyle.top);
              dragging = true;
              handle.CapturePointer(evt.pointerId);
              evt.StopPropagation();  // ❗ 阻止冒泡到 root PointerDown（防止收纳番茄钟面板）
          });

          handle.RegisterCallback<PointerMoveEvent>(evt =>
          {
              if (!dragging) return;
              try
              {
                  var delta = (Vector2)evt.position - pointerStart;
                  // ❗ 使用父容器尺寸做边界约束（而非 Screen.width/height，后者在 UI Toolkit runtime panel 下坐标系不一致）
                  var parent = target.parent;
                  float parentWidth = parent?.resolvedStyle.width ?? Screen.width;
                  float parentHeight = parent?.resolvedStyle.height ?? Screen.height;
                  float newLeft = Mathf.Clamp(elementStart.x + delta.x, 0, parentWidth - target.resolvedStyle.width);
                  float newTop = Mathf.Clamp(elementStart.y + delta.y, 0, parentHeight - target.resolvedStyle.height);
                  target.style.left = newLeft;
                  target.style.top = newTop;
              }
              catch (Exception ex) { Debug.LogException(ex); }
              finally { evt.StopPropagation(); }
          });

          handle.RegisterCallback<PointerUpEvent>(evt =>
          {
              dragging = false;
              if (handle.HasPointerCapture(evt.pointerId))
                  handle.ReleasePointer(evt.pointerId);
              evt.StopPropagation();
          });

          // ❗ 异常保险：PointerCapture 丢失或事件取消时清理状态
          handle.RegisterCallback<PointerCaptureOutEvent>(evt =>
          {
              dragging = false;
          });
      }
  }
  ```

#### Step 4.4: 玩家卡片控制器
- **文件**: `Assets/UI_V2/Controller/PlayerCardView.cs`
- **职责**:
  - 构造函数：`new PlayerCardView(RemotePlayerData data, VisualTreeAsset uxml)`
  - 从 UXML 实例化 `VisualElement Root`
  - `Refresh(RemotePlayerData)` 更新显示（格式化 MM:SS、中文阶段标签、阶段色调）
  - 只读：无按钮、无可点击元素

#### Step 4.5: 玩家面板管理器（与 DeskWindow 解耦）
- **文件**: `Assets/UI_V2/Controller/PlayerCardManager.cs`
- **挂载策略**：**方案 A — 同一 UIDocument，独立 player-card-layer**
  ```csharp
  public sealed class PlayerCardManager : IController
  {
      IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

      private VisualElement _cardLayer;
      private readonly Dictionary<string, PlayerCardView> _cards = new();
      private VisualTreeAsset _cardTemplate;

      public void Initialize(VisualElement root, VisualTreeAsset cardTemplate)
      {
          _cardTemplate = cardTemplate;

          // ❗ 创建独立 layer，绝对定位，不参与 dw-wrap 的 flex
          _cardLayer = new VisualElement
          {
              name = "player-card-layer",
              pickingMode = PickingMode.Ignore  // 空白区域透明点击穿透
          };
          _cardLayer.style.position = Position.Absolute;
          _cardLayer.style.left = 0;
          _cardLayer.style.top = 0;
          _cardLayer.style.right = 0;
          _cardLayer.style.bottom = 0;
          root.Add(_cardLayer);  // 直接加到 root，脱离 dw-wrap

          this.RegisterEvent<E_PlayerJoined>(OnPlayerJoined);
          this.RegisterEvent<E_PlayerLeft>(OnPlayerLeft);
          this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated);
          this.RegisterEvent<E_RoomSnapshot>(OnSnapshot);
      }

      private void OnPlayerJoined(E_PlayerJoined e)
      {
          var card = new PlayerCardView(e.Player, _cardTemplate);
          card.Root.style.left = Random.Range(100, 400);  // 随机初始位置避免重叠
          card.Root.style.top = Random.Range(100, 300);
          card.Root.pickingMode = PickingMode.Position;  // 卡片本身可交互
          DraggableElement.MakeDraggable(card.Root);     // 整张卡片可拖
          _cardLayer.Add(card.Root);
          _cards[e.Player.PlayerId] = card;
      }
      // ... OnPlayerLeft/OnStateUpdated/OnSnapshot
  }
  ```

#### Step 4.6: DeskWindowController 事件路由修正（M-5 完整修复）

**三重防御策略**：

**(1) 事件注册阶段改回 NoTrickleDown（冒泡阶段）**

现有代码 `DeskWindowController.cs:164-171` 使用 `TrickleDown.TrickleDown`（capture 阶段，在 target 之前执行），这会让卡片内 `StopPropagation` 无效。必须改为 `TrickleDown.NoTrickleDown`（默认，冒泡阶段在 target 之后执行），让拖拽的 `StopPropagation` 能阻止事件冒泡到 root。

**(2) 循环向上查找祖先（而非 `GetFirstAncestorOfType` 只查一级）**

`VisualElement.GetFirstAncestorOfType<T>()` 只返回**第一个直接祖先**，不递归。必须自己写循环。

- **修改**: `Assets/UI_V2/Controller/DeskWindowController.cs:164-171`
  ```csharp
  // 修改前：TrickleDown.TrickleDown
  // 修改后：NoTrickleDown + 循环祖先查找白名单
  root.RegisterCallback<PointerDownEvent>(evt =>
  {
      // 白名单 1：点击在 dw-wrap 内部 → 不收纳（原有逻辑）
      bool clickInDwWrap = _dwWrap != null && _dwWrap.worldBound.Contains(evt.position);

      // 白名单 2：点击在任何 player-card-layer / PlayerCard 内部 → 不收纳
      bool clickInCardLayer = false;
      if (evt.target is VisualElement target)
      {
          var node = target;
          while (node != null)
          {
              if (node.name == "player-card-layer" || node.ClassListContains("pc-root"))
              {
                  clickInCardLayer = true;
                  break;
              }
              node = node.parent;
          }
      }

      if (!clickInDwWrap && !clickInCardLayer)
      {
          _pomodoroPanelView?.Collapse();
      }
  });  // 不传 TrickleDown 参数 → 默认 NoTrickleDown（冒泡阶段）
  ```

**(3) DraggableElement 在 PointerDown/Move/Up 中 StopPropagation**

已在 Step 4.3 实现。因为 root 监听器现在是冒泡阶段，拖拽的 `StopPropagation` 会有效阻止事件上冒到 root，即使白名单逻辑失效也能兜底。

**回归验证**：
- `TrickleDown.TrickleDown` 改为 `NoTrickleDown` 对原"点击 dw-wrap 外部收纳面板"行为的影响：冒泡阶段依然会触发 root 的回调（只要事件没被 StopPropagation），且判断依旧基于 `evt.position` 是否在 `_dwWrap.worldBound` 内——**功能等价**。
- UI Toolkit 的 `Button.RegisterCallback<PointerUpEvent>` 默认不 StopPropagation，所以菜单按钮的点击依然会冒泡到 root；但菜单按钮都在 `dw-wrap` 内部，命中白名单 1，不会触发收纳——**功能等价**。

**同时**在 `Start()` 末尾初始化 `PlayerCardManager`，注入 card template 资产（见 Step 4.8）

#### Step 4.7: 联机设置面板重写
- **修改**: `Assets/UI_V2/Documents/OnlineSettingsPanel.uxml`（**完全重写**，现有内容是占位）
- **修改**: `Assets/UI_V2/Styles/OnlineSettingsPanel.uss`（**完全重写**）
- **新结构**:
  - 昵称输入 `TextField`
  - 服务器地址输入 `TextField`（默认 `ws://localhost:8765`）
  - "创建房间"按钮
  - 房间码输入 `TextField` + "加入"按钮
  - 当前状态区：连接状态、当前房间码（可点击复制）、成员列表
  - "离开房间"按钮
  - 错误提示 Label

#### Step 4.8: DeskWindow 集成入口
- **修改**: `Assets/UI_V2/Controller/DeskWindowController.cs`
  - 添加 `[SerializeField] VisualTreeAsset _playerCardUxml;`
  - `Start()` 末尾：
    ```csharp
    var cardManager = new PlayerCardManager();
    cardManager.Initialize(_uiDocument.rootVisualElement, _playerCardUxml);
    BindOnlineSettingsPanelEvents();  // 新增方法：连接 UI 控件到 Cmd_CreateRoom/Cmd_JoinRoom/Cmd_LeaveRoom
    ```
  - 场景中 DeskWindow GameObject 挂 `NetworkDispatcherBehaviour`

---

### Phase 5: 测试

**目标**: 通过 Unity MCP `run_tests` + Node.js `node --test` 验证

#### Step 5.1: 服务器单元测试（Phase 1.5 已定义）
- `Server/test/room.test.js` — 房间 CRUD、防抖销毁、满员
- `Server/test/protocol.test.js` — 协议解析、版本校验、非法消息

#### Step 5.2: 服务器集成测试
- `Server/test/integration.test.js`
  - 启动真实 ws 服务端
  - 创建 2 个 ws 客户端模拟 A/B
  - A 创建房间 → B 加入 → 验证 snapshot
  - A 发 sync_state → B 收到 state_update → 测量端到端延迟
  - B 断开 → 验证 A 收到 player_left
  - B 重连 → 验证收到 room_snapshot
- **断言**: 端到端延迟 ≤ 500ms（AC-2）、快照恢复 ≤ 3s（AC-9）

#### Step 5.3: Unity 网络层 EditMode 测试
- **文件**: `Assets/Tests/EditMode/NetworkTests/`
- **覆盖**:
  - `RoomModelTests.cs` — 远程玩家增删、快照导入
  - `MessageSerializationTests.cs` — JsonUtility 序列化往返
  - `StateSyncTests.cs` — 1Hz 节拍、keepalive 5s、阶段切换 ForceSyncNow（用 mock INetworkSystem 验证 Send 调用次数）
  - `MainThreadDispatcherTests.cs` — ConcurrentQueue 入队/出队、异常隔离

#### Step 5.4: 玩家面板 EditMode 测试
- **文件**: `Assets/Tests/EditMode/PlayerCardTests/`
- **覆盖**:
  - `PlayerCardManagerTests.cs` — 玩家加入创建卡片、离开销毁、snapshot 批量建卡
  - `DraggableElementTests.cs` — 拖拽位置计算、边界约束（用 mock PointerEvent）
  - `PlayerCardViewTests.cs` — 状态字段映射（phase → 中文标签、秒数 → MM:SS）

#### Step 5.5: Unity-Server PlayMode 集成测试（可选，需启动本地服务器）
- **文件**: `Assets/Tests/PlayMode/NetworkIntegrationTests.cs`
- **前置**: 测试脚本启动本地 Node.js 服务器子进程
- **覆盖**: Unity ClientWebSocket 连接、创建房间、发送 sync_state、接收广播
- **注意**: 若 CI 环境无 Node.js，此套件标注 `[Explicit]` 跳过

#### Step 5.6: 运行方式
- 服务器：`cd Server && npm test`
- Unity EditMode: Unity MCP `run_tests` with `testMode: EditMode`
- Unity PlayMode: Unity MCP `run_tests` with `testMode: PlayMode`（手动触发）

---

## 风险与缓解（扩展版）

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| **R1**: WebSocket 回调线程不安全导致 UI Toolkit 崩溃 | 致命 | **锁定方案**：NetworkSystem 所有 SendEvent 必须通过 `_mainThreadQueue.Enqueue`；NetworkDispatcherBehaviour 在 Update 中 Drain；在 StateSyncTests/MainThreadDispatcherTests 断言无跨线程 UI 调用 |
| **R2**: StateSyncSystem 没有 tick 源 | 阻塞 | **锁定方案**：由 NetworkDispatcherBehaviour.Update 每帧调用 `IStateSyncSystem.Tick(unscaledDeltaTime)`，内部 1s 累加器触发发送 |
| **R3**: 番茄钟面板在拖卡片时意外收纳 | UX bug | **锁定方案**：DraggableElement 在 PointerDown 时 StopPropagation；DeskWindowController 的 root PointerDown 回调增加 `clickInCardLayer` 白名单判断 |
| **R4**: 新加入玩家看到空番茄钟面板 | AC-5 失败 | 服务端为每个房间维护 `latestState` 缓存；room_joined/room_snapshot 消息携带全部玩家当前状态 |
| **R5**: 断线重连期间本地状态漂移 | 数据不一致 | 番茄钟本地 tick 继续；重连后以本地状态为权威（每人的番茄钟是独立的），只同步状态，不做双向 reconcile |
| **R6**: 房间码暴力枚举 | 低概率安全 | 每 IP 限制 10 次/分钟的 join_room 调用；房间码字符集 22^6 ≈ 1.1亿，有限防护 |
| **R7**: JsonUtility 不支持多态/复杂嵌套 | 协议扩展性 | 用扁平 DTO（InboundMessage 顶层带所有可能字段，按 type 分发）；若后续需要复杂结构再引入 Newtonsoft.Json |
| **R8**: UniWindowController 透明窗口上 card-layer 点击穿透错误 | UX bug | `player-card-layer` `pickingMode=Ignore`（空白穿透），卡片本身 `pickingMode=Position`（可交互） |
| **R9**: 服务端节流丢包 | 阶段切换丢失 | 客户端 `ForceSyncNow` 不受 1Hz 节流；服务端节流 10Hz（高于正常流量，仅作 DoS 防御） |
| **R10**: 跨平台（Windows/Linux）Unity 客户端 | 功能降级 | `ClientWebSocket` 跨平台可用；Phase 3 AppMonitor 延期后 v1 无平台相关代码；v2 实施时再处理平台降级 |

## 验证步骤

1. **服务器独立验证**:
   ```bash
   cd Server
   npm install
   npm test              # 通过所有 unit + integration 测试
   node src/index.js     # 启动服务器
   ```
2. **Unity 单元测试**: Unity MCP `run_tests` → NetworkTests + PlayerCardTests 全部通过
3. **双实例手动验证**:
   - 启动服务器 + Unity 实例 A → 创建房间 → 获得房间码
   - 启动 Unity 实例 B（通过 UI Toolkit Scene 复制构建或第二个 Unity Editor 实例）→ 加入房间
   - A 点击开始番茄钟 → B 的玩家卡片实时显示专注状态
   - A 拖动 B 的玩家卡片到屏幕任意位置 → 验证番茄钟面板未被收纳
   - A 强制退出 → B 收到 player_left → 卡片自动移除
   - A 重新启动并加入 → B 收到 player_joined → 卡片重新出现
4. **延迟测量**: `Server/test/latency.test.js` 自动验证 AC-2（≤ 500ms）
5. **断线恢复**: 手动断网 5 秒 → 重连 → 验证 room_snapshot 恢复所有玩家状态（AC-9）

## 文件清单

### 新建文件

**Server (6)**
- `Server/package.json`
- `Server/src/index.js`
- `Server/src/RoomManager.js`
- `Server/src/protocol.js`
- `Server/test/room.test.js`
- `Server/test/protocol.test.js`
- `Server/test/integration.test.js`
- `Server/test/latency.test.js`

**Unity Network (11)**
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
- `Assets/Scripts/APP/Network/DTO/InboundMessage.cs` — JsonUtility 扁平 DTO
- `Assets/Scripts/APP/Network/DTO/OutboundMessage.cs`

**Unity Network Commands (3)**
- `Assets/Scripts/APP/Network/Command/Cmd_CreateRoom.cs`
- `Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs`
- `Assets/Scripts/APP/Network/Command/Cmd_LeaveRoom.cs`

**UI (5)**
- `Assets/UI_V2/Documents/PlayerCard.uxml`
- `Assets/UI_V2/Styles/PlayerCard.uss`
- `Assets/UI_V2/Controller/DraggableElement.cs`
- `Assets/UI_V2/Controller/PlayerCardView.cs`
- `Assets/UI_V2/Controller/PlayerCardManager.cs`

**Tests (6)**
- `Assets/Tests/EditMode/NetworkTests/RoomModelTests.cs`
- `Assets/Tests/EditMode/NetworkTests/MessageSerializationTests.cs`
- `Assets/Tests/EditMode/NetworkTests/StateSyncTests.cs`
- `Assets/Tests/EditMode/NetworkTests/MainThreadDispatcherTests.cs`
- `Assets/Tests/EditMode/PlayerCardTests/PlayerCardManagerTests.cs`
- `Assets/Tests/EditMode/PlayerCardTests/DraggableElementTests.cs`
- `Assets/Tests/EditMode/PlayerCardTests/PlayerCardViewTests.cs`

### 修改文件（6 个）
- `Assets/Scripts/APP/Pomodoro/GameApp.cs` — 新增 Model/System 注册
- `Assets/UI_V2/Documents/OnlineSettingsPanel.uxml` — **完全重写**
- `Assets/UI_V2/Styles/OnlineSettingsPanel.uss` — **完全重写**
- `Assets/UI_V2/Controller/DeskWindowController.cs` — 集成 PlayerCardManager、修正 PointerDown 事件路由、绑定 OnlineSettingsPanel 事件
- `Assets/Scenes/MainV2.unity` — DeskWindow GameObject 挂载 NetworkDispatcherBehaviour，分配 PlayerCard UXML 引用

---

## ADR (Architecture Decision Record)

### Decision
采用**自建 Node.js + ws 后端 + 自定义协议**实现多人番茄钟同步，**Phase 3 AppMonitor 降级到 v2**。

### Drivers
1. QFramework 分层规范必须被严格遵守（Command 无状态、下层→Event→上层）
2. Unity 主线程约束（UI Toolkit 不支持跨线程调用）
3. 项目当前零网络依赖，引入复杂度需最小化
4. macOS 权限模型复杂，新功能应尽量避免权限弹窗
5. v1 目标是验证"一起专注"的核心价值

### Alternatives Considered
| 选项 | 拒绝理由 |
|------|----------|
| **Unity Relay + Lobby (UGS)** | 绑定 Unity 云服务，未来互动指令的协议自由度低 |
| **Firebase / Supabase Realtime** | 协议绑定厂商 SDK；国内访问不稳定；定向消息扩展成本高 |
| **Photon PUN2 / Fusion** | 过度方案；为小体量项目引入大框架 |
| **Mirror Networking** | 面向权威服务器游戏场景，本项目是"广播型"无需权威仲裁 |
| **v1 包含 AppMonitor** | macOS Screen Recording 权限复杂、跨平台降级、对核心价值贡献小 |

### Why Chosen
- 自建 ws 给予完整协议控制，未来加"互动投食"等定向消息时扩展成本最低
- 切片 Phase 3 到 v2 消除了 macOS 权限/plist/cross-platform 三重风险
- QFramework 架构兼容性好：NetworkSystem 作为水平 System，StateSyncSystem 通过主线程 Tick 驱动，完全符合现有模式

### Consequences
- **好处**: v1 实施路径清晰无阻塞；主线程调度、事件路由、Command 边界全部锁定；可 2 周内交付
- **代价**: 需要维护自建 Node.js 服务器（部署/运维成本）；协议演化需自己管理版本兼容
- **债务**: v2 AppMonitor 需要补回；未来图像传输需考虑二进制帧优化

### Follow-ups（v2 规划）
1. AppMonitor macOS 原生插件（基于 `NSWorkspace.frontmostApplication`，不使用 `CGWindowListCopyWindowInfo` 以避免 Screen Recording 权限）
2. 只同步 `bundleId + name + icon`（砍掉窗口标题）
3. 图标传输改为 iconId（bundleId）缓存 + `icon_data` 独立消息
4. 宠物状态 Model + 互动消息通道（`pet_state` / `interaction`）
5. 跨平台降级（Windows/Linux 客户端隐藏 activeApp 区域）

---

## Implementation Notes（实施阶段 Checklist）

以下是第二/第三轮 Architect 复审识别的"实现细节级"注意事项，在方案主体之外作为实施者必读清单：

1. **Disconnect() async-safe 包装**
   方案 Step 2.2 的 `Disconnect()` 在 `CloseAsync` fire-and-forget 期间就 `_cts.Dispose()`，理论上会让后台 `RunAsync` 中的 `ReceiveAsync(buffer, ct)` 在访问已 Dispose 的 CancellationToken 时抛 `ObjectDisposedException`。实施时改为：
   ```csharp
   public async Task DisconnectAsync()
   {
       var cts = _cts; var ws = _ws;
       _cts = null; _ws = null;   // 先置空防重入
       try { cts?.Cancel(); if (ws?.State == WebSocketState.Open) await ws.CloseAsync(..., CancellationToken.None); }
       finally { ws?.Dispose(); cts?.Dispose(); while (_mainThreadQueue.TryDequeue(out _)) { } }
   }
   ```
   同时 `TryReconnect` 必须判断 `_cts != null && !_cts.IsCancellationRequested`，防止 Disconnect 后意外重连。

2. **ClientWebSocket.SendAsync 并发安全**
   `ClientWebSocket.SendAsync` 不支持并发（.NET 文档明确）。`NetworkSystem.Send` 是 fire-and-forget，前一个 SendAsync 未完成时下一个启动会碰撞。实施时加 `SemaphoreSlim`：
   ```csharp
   private readonly SemaphoreSlim _sendLock = new(1, 1);
   public void Send(object message) { /* ...序列化... */ _ = SendInternalAsync(bytes); }
   private async Task SendInternalAsync(byte[] bytes)
   {
       await _sendLock.WaitAsync();
       try { if (_ws?.State == WebSocketState.Open) await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token); }
       finally { _sendLock.Release(); }
   }
   ```

3. **`Time.unscaledDeltaTime` vs `Time.deltaTime` 差异（DeskWindowController.Update）**
   - `Cmd_PomodoroTick(Time.deltaTime)` — 番茄钟业务 tick，受 Unity timeScale 影响（若游戏暂停时间应随之暂停）
   - `StateSyncSystem.Tick(Time.unscaledDeltaTime)` — 网络同步 tick，**不受** timeScale 影响（多人同步不应因本地暂停而停止心跳）
   - 实施者必须保留这个差异，不要"统一"两者的时间源

4. **DraggableElement 单指针桌面场景假设**
   v1 拖拽系统仅支持单指针桌面输入。多点触控下的行为未定义（第二根手指的 PointerDown 会覆盖闭包变量 `pointerStart/elementStart`）。v2 如需支持多触控，需改为 per-pointer state `Dictionary<int, DragState>`。

5. **JsonUtility 对 null 嵌套对象的行为**
   JsonUtility 反序列化时，`RemoteState.activeApp` 即使服务端发 `null`，客户端也会实例化为空对象 `new ActiveAppDto { name=null, bundleId=null, iconId=null }`。v1 不读 activeApp 字段，无影响；v2 实施 AppMonitor 时必须用 `string.IsNullOrEmpty(activeApp.bundleId)` 判空而非 `activeApp == null`。

---

## Changelog

### v2 修订（第二轮 Architect 复审后）

| 评审项 | 修复 |
|--------|------|
| **N-1** ForceSyncNow 缺 IsInRoom 守卫 | Step 2.4 ForceSyncNow 开头加 IsInRoom/IsConnected 守卫 |
| **N-2** 双 tick 源 + 慢一帧 | StateSyncSystem.Tick 从 DeskWindowController.Update 末尾调用（Cmd_PomodoroTick 之后），NetworkDispatcherBehaviour 仅负责 DrainMainThreadQueue；锁定 NetworkDispatcherBehaviour 挂载点为 DeskWindow GameObject |
| **N-3** priority 字段协议缺口 | 删除 priority 承诺，改为服务端按玩家状态指纹自动判断 |
| **N-4** InboundMessage DTO 缺失 | Step 2.2 新增完整 InboundMessage/SnapshotEntry/RemoteState/PomodoroStateDto/ActiveAppDto 的 [Serializable] 定义 |
| **N-5** EqualsLogical 未定义 | 在 RemoteState 和 PomodoroStateDto 上定义静态 EqualsLogical 方法 |
| **M-5 PARTIAL** 白名单只查一级祖先 | Step 4.6 改为循环向上查找；TrickleDown.TrickleDown → NoTrickleDown（冒泡阶段）让 StopPropagation 真正生效 |
| **Disconnect 清理** | Step 2.2 补完整 Disconnect() 实现：Cancel + Dispose + 清空队列 |
| **DraggableElement 异常路径** | Step 4.3 补 try-catch、PointerCaptureOutEvent 兜底、改用 parent 尺寸做边界约束 |

### v1 修订（第一轮 Architect + Critic 评审后）

| 评审项 | 修复 |
|--------|------|
| **C-1** StateSyncSystem tick 源 | Step 2.3 新增 NetworkDispatcherBehaviour（后 v2 改为 DeskWindowController 驱动 Tick） |
| **C-2** WebSocket 主线程调度 | Step 2.2 展开完整 ConcurrentQueue + DrainMainThreadQueue 实现，附伪代码 |
| **C-3** AppMonitor 权限 | Phase 3 整体降级到 v2，v1 AC-5/6 移除 activeApp |
| **M-1** Cmd_SyncLocalState 违反无状态 | 删除 Cmd_SyncLocalState；StateSyncSystem 直接调 INetworkSystem.Send |
| **M-2** room_snapshot 缺失 | Step 1.3 新增 room_snapshot 消息；服务端维护 latestState 缓存 |
| **M-3** 节流冲突 | 服务端改为 10Hz 滑动窗口（DoS 防御）；阶段切换 ForceSyncNow bypass |
| **M-4** 图标 iconId 缺失 | v2 预留，v1 不传图标 |
| **M-5** PlayerCardManager 路由冲突 | 拖拽 StopPropagation + 独立 player-card-layer + DeskWindowController 白名单 |
| **M-6** OnlineSettingsPanel 推翻重写 | Step 4.7 明确"完全重写"；文件清单加入 `.uss` |
| **Gap-1** 无集成测试 | Phase 5 新增 Step 5.2 (Server integration) + Step 5.5 (Unity PlayMode) |
| **Gap-2** 无 BaaS 辩护 | 新增"为什么选择自建 Node.js"章节 + ADR |
| **Gap-3** AC 无测量方法 | AC-2/3/5/9 补具体测量脚本和判据 |
| **Gap-4** protocolVersion 缺失 | 协议所有消息加 `v: 1` 字段 |
| **Gap-5** playerId 策略 | Step 1.2 明确服务端 UUID 生成；`room_created/joined` 返回 playerId |
