# 多人联机功能 —— 设计文档

- 日期：2026-04-21
- 作者：xpy + Claude
- 状态：已定稿，待进入实施计划

## 背景与目标

项目是 Unity 6 桌面宠物番茄钟，已有单机完整功能（QFramework 架构、Pomodoro 倒计时、透明窗口、PlayerCard UI、Pencil 设计稿）。

本次目标：**让多个用户通过房间码聚到同一个"房间"，互相**被动观看**对方当前的番茄钟阶段 / 剩余时间 / 正在用的前台 App（名字 + 图标）。不含互动、不含强同步。

- 玩法定位：**被动观战**（Q1=A）
- 本次做：① 协议对齐、② 创建房间 UI、③ 自动联网 & 用户名持久化、④ 历史房间、⑤ ActiveApp 同步、⑥ PlayMode 集成测试（Q2）
- 部署：**本地/同局域网**，`ws://127.0.0.1:8765` 硬编码（Q3=A）
- 自动联网：**启动时自动重进上次房间**，失败落回加入卡片（Q4=A）
- ActiveApp：**1Hz 采样 + 服务端缓存图标 base64**（Q5=A+iii）
- 历史房间上限：**5**（Q6a）
- 协议对齐方向：**改客户端**（Q7a），服务端只改端口与加 icon 协议
- 重连策略：**沿用指数退避 + UI 显示重连状态**（Q8c）

## 架构总览

```
┌────────────────────────────────────────────────────────────────┐
│  Unity 客户端（QFramework 四层）                                │
│                                                                 │
│  Controller:                                                    │
│    OnlineSettingsPanelController  创建/加入/离开/复制房号       │
│    PlayerCardManager              远端卡片 CRUD                │
│    DeskWindowController           每帧 StateSync.Tick          │
│    NetworkDispatcherBehaviour     Drain 主线程队列             │
│                                                                 │
│  Command: Cmd_CreateRoom / Cmd_JoinRoom / Cmd_LeaveRoom        │
│           Cmd_AutoReconnectOnStartup                           │
│                                                                 │
│  System:  INetworkSystem     WS 收发、重连、主线程回调         │
│           IStateSyncSystem   1Hz 采样 + 阶段变更立即推         │
│           IActiveAppSystem   1Hz 轮询 AppMonitor（新）        │
│           IIconCacheSystem   bundleId → Texture2D 缓存（新）  │
│                                                                 │
│  Model:   IRoomModel         房间/连接状态（已存在）           │
│           ISessionMemoryModel 用户名/历史房间/自动联网（新）   │
│                                                                 │
│  Utility: IStorageUtility    PlayerPrefs 封装（新）            │
│                                                                 │
└────────────────────────┬───────────────────────────────────────┘
                         │ WebSocket (ws://127.0.0.1:8765)
┌────────────────────────▼───────────────────────────────────────┐
│  Node.js 服务端（已有，本次只改端口 + 加 icon 协议）           │
│                                                                 │
│  RoomManager  房间/玩家（已完成）                              │
│  IconCache    bundleId → iconBase64 LRU（新，100 条 / ≤1MB）  │
│  Protocol     加 icon_upload/icon_broadcast/icon_need/...     │
└────────────────────────────────────────────────────────────────┘
```

**职责边界：**

- 网络 IO 封闭在 `NetworkSystem`，上层只通过 `Cmd_*` 发、通过 `E_*` 事件订阅。
- ActiveApp 采样与图标缓存和网络解耦：`ActiveAppSystem` 轮询、`IconCacheSystem` 存图、`StateSyncSystem` 打包 state 发送。
- 会话记忆（用户名、历史房间、上次房间）独立成 `SessionMemoryModel`，通过 `IStorageUtility` 持久化到 PlayerPrefs。

## 1. 协议对齐（客户端改，服务端只动端口 + 加图标）

### 1.1 默认端口：8765

服务端 `Server/src/index.js`：
```js
const DEFAULT_PORT = Number.parseInt(process.env.PORT ?? '8765', 10);
```

### 1.2 客户端 DTO 字段重命名

| 文件 | 改动 |
|---|---|
| `InboundMessage.cs` | `code` → `roomCode`；新增 `List<SnapshotEntry> players`；去掉 `snapshot`；错误改为读 `error` 字符串 |
| `OutboundJoinRoom.cs` | `code` → `roomCode` |
| `NetworkSystem.DispatchInbound` | `"state_update"` → `"player_state_broadcast"`；`HandleRoomCreated/Joined` 不再读 `inbound.snapshot`（等单独的 `room_snapshot` 消息补齐）；`HandlePlayerJoined` 从 `inbound.players[0]` 读 |
| `NetworkSystem.HandleNetworkError` | 读 `inbound.error`（string），code 与 message 都用这个值 |
| `Cmd_CreateRoom / Cmd_JoinRoom` | 默认 URL 保持 `ws://localhost:8765`（与端口对齐） |

### 1.3 新增图标协议

**客户端 → 服务端**

- `player_state_update { state: { pomodoro, activeApp: { name, bundleId } } }` — `activeApp` 只带 name + bundleId，不含图标
- `icon_upload { bundleId, iconBase64 }` — 在收到 `icon_need` 后上传
- `icon_request { bundleIds: [...] }` — 新加入房间时，对比本地缓存补齐缺失图标

**服务端 → 客户端**

- `icon_need { bundleId }` — 仅回给发送者；收到 `player_state_update` 但缓存没有此 bundleId 时触发
- `icon_broadcast { bundleId, iconBase64 }` — 全房间广播（`icon_upload` 触发）或单点响应（`icon_request` 触发）

**图标流程总图**

```
A 切到 Safari (bundleId=com.apple.Safari)
  → state_update { activeApp: { name, bundleId } }

服务端：
  缓存有？
    - 是：正常广播 player_state_broadcast（bundleId only）
    - 否：① 正常广播 ② 回 A 一条 icon_need { bundleId }

A 收 icon_need：
  从 ActiveAppSystem.Current.IconPngBytes 拿 PNG → base64
  → icon_upload { bundleId, iconBase64 }

服务端收 icon_upload：
  校验 base64 长度 ≤ 1MB → 加入 IconCache（LRU 100）
  → icon_broadcast 全房间（含 A）

B/C 收 icon_broadcast：
  IIconCacheSystem.StoreFromBase64 → 发 E_IconUpdated
  → PlayerCardManager 刷新 BundleId==X 的卡片

新加入者 N：
  room_snapshot 含各玩家 activeApp.bundleId（无图）
  N 对比本地 IconCache → 缺的批量 icon_request { bundleIds }
  服务端逐个 icon_broadcast 回 N
```

**服务端 IconCache 规格**

- `Map<bundleId, { iconBase64, lastUsedAt }>`，LRU，上限 **100 条**
- 单条 iconBase64 上限 **1 MB**（字符串长度），超出返 `error: "ICON_TOO_LARGE"`
- 全局共享（不按房间隔离，Safari 图标全项目复用）
- 服务端重启后丢失，客户端会被重新 `icon_need`

**Unity 端 IconCache**

- `IIconCacheSystem` 持 `Dictionary<string, Texture2D>`，非持久化
- 卡片 UI 通过 `bundleId` 查 Texture2D，没有就显示首字母块占位

## 2. 新增 UI

> UI 改动**强制工作流**：先在 Pencil 设计稿里做（`mcp__pencil__batch_design`），再同步到 Unity UXML/USS。`AUI/PUI.pen` 是 UI 的 source of truth，UXML 是对它的实现。

### 2.1 Pencil 设计稿改动

**`OnlineSettingsPanel` 组件（Pencil id = 8Le5R）**

- `osp-join-card` 内新增「创建新房间」按钮 `osp-create-btn`，secondary 按钮样式，放在 `osp-join-btn` 正下方
- `osp-room-card` 房号区：`osp-room-name` 旁加 icon 按钮 `osp-copy-btn`（复制图标）
- `osp-room-card` 顶部加 reconnect banner `osp-reconnect-banner`（默认隐藏；重连中暖色底显示文案）
- `osp-hist-card` 内容：动态列表，每项含房号、上次用户名、相对时间、快捷加入按钮、删除按钮

**`PlayerCard` 组件**

- 如果当前还没有 activeApp 图标/名字区域，加 `pc-active-app-icon`（方形图标位）+ `pc-active-app-name`（一行文字），在卡片下部

### 2.2 UXML / USS 同步

- `Assets/UI_V2/Documents/OnlineSettingsPanel.uxml`
- `Assets/UI_V2/Documents/PlayerCard.uxml`
- 对应 `.uss` 文件（复制提示、reconnect banner、active app 区、history item 样式）

### 2.3 Controller 接线

**`OnlineSettingsPanelController`**

- `OnCreateClicked()` → 校验用户名 → `SendCommand(new Cmd_CreateRoom(username))`
- `OnCopyClicked()` → `GUIUtility.systemCopyBuffer = _roomModel.RoomCode.Value`；按钮短暂显示"已复制"
- 订阅 `E_ConnectionStateChanged`：
  - `Reconnecting` → 显示 reconnect banner
  - `Connected` / `InRoom` → 隐藏
  - `Error` → banner 变红显示"重连失败"
- 用户名字段首次加载时从 `ISessionMemoryModel.LastPlayerName` 回填
- `osp-auto-toggle` 绑定 `ISessionMemoryModel.AutoReconnectEnabled`（双向同步）
- `RefreshHistoryList()` 消费 `ISessionMemoryModel.RecentRooms` 重建 `osp-hist-list`
- 订阅 `E_RecentRoomsChanged` 自动刷新历史列表

**`PlayerCardView`**

- 读 `RemotePlayerData.ActiveAppBundleId`
- 通过 `IIconCacheSystem.GetTexture(bundleId)` 贴图
- 拿不到显示首字母块
- 订阅 `E_IconUpdated` 时，如果 `bundleId` 匹配就重新取图贴

## 3. 会话记忆（用户名 / 上次房间 / 历史房间）

### 3.1 Utility — `IStorageUtility`

```csharp
public interface IStorageUtility : IUtility
{
    string LoadString(string key, string fallback = "");
    void SaveString(string key, string value);
    int LoadInt(string key, int fallback = 0);
    void SaveInt(string key, int value);
    void DeleteKey(string key);
    void Flush();
}
```

`PlayerPrefsStorageUtility` 为默认实现；测试用 `InMemoryStorageUtility`。`GameApp.Init` 中**先于**所有 Model 注册。

### 3.2 Model — `ISessionMemoryModel`

```csharp
public interface ISessionMemoryModel : IModel
{
    BindableProperty<string> LastPlayerName { get; }
    BindableProperty<string> LastRoomCode { get; }
    BindableProperty<bool>   AutoReconnectEnabled { get; }
    IReadOnlyList<HistoryRoomEntry> RecentRooms { get; }

    void RememberJoin(string playerName, string roomCode);
    void ForgetLastRoom();
    void SetAutoReconnectEnabled(bool enabled);
    void RemoveHistoryEntry(string roomCode);
}

public sealed class HistoryRoomEntry
{
    public string RoomCode;
    public string LastPlayerName;
    public long LastJoinedAtUnixMs;
}
```

**PlayerPrefs key**

- `net.lastPlayerName` → string
- `net.lastRoomCode` → string（空 = 无）
- `net.autoReconnect` → int 0/1
- `net.recentRooms` → JSON 字符串 `[{"code":"ABC123","name":"小明","t":1700000000000}, ...]`，按 `t` 倒序，最多 5 条

**事件**

- 任何写入（`RememberJoin` / `RemoveHistoryEntry`）后发 `E_RecentRoomsChanged`

### 3.3 事件钩子 — 谁在什么时机更新 SessionMemory

| 时机 | 触发点 | 动作 |
|---|---|---|
| 点创建/加入 | `Cmd_CreateRoom` / `Cmd_JoinRoom` 执行 | `SessionMemory.LastPlayerName = _playerName` |
| 成功进房 | `NetworkSystem.HandleRoomCreated` / `HandleRoomJoined` | `SessionMemory.RememberJoin(name, code)` → 更新 `LastRoomCode` + 插入 `RecentRooms`（去重：若已存在则移到最前） |
| 主动离开 | `Cmd_LeaveRoom` 执行 | `SessionMemory.ForgetLastRoom()` — 只清 `LastRoomCode`，保留 `RecentRooms` |
| 切换开关 | `osp-auto-toggle` | `SessionMemory.SetAutoReconnectEnabled(v)` |
| 点"删除" | `osp-hist-del-btn` | `SessionMemory.RemoveHistoryEntry(code)` |

### 3.4 自动重连 — `Cmd_AutoReconnectOnStartup`

**触发**：`DeskWindowController.Start()` 末尾 `this.SendCommand(new Cmd_AutoReconnectOnStartup())`。

**逻辑**：
```csharp
if (!SessionMemory.AutoReconnectEnabled.Value) return;
if (string.IsNullOrEmpty(SessionMemory.LastRoomCode.Value)) return;
if (string.IsNullOrEmpty(SessionMemory.LastPlayerName.Value)) return;

this.SendCommand(new Cmd_JoinRoom(
    SessionMemory.LastRoomCode.Value,
    SessionMemory.LastPlayerName.Value));
```

`ROOM_NOT_FOUND` 错误 → `E_NetworkError` → UI 展示，UI 落回加入卡片，并**清理 `LastRoomCode`** 避免下次继续失败。

## 4. ActiveApp 接入

### 4.1 恢复 AppMonitor 包

```bash
git checkout e74c3aa -- localpackage/com.nz.appmonitor
```

在 `Packages/manifest.json` 中加：
```json
"com.nz.appmonitor": "file:../localpackage/com.nz.appmonitor"
```

### 4.2 扩展 AppMonitor 加 bundleId

**`AppMonitor.m`** 新增出参：
```objc
int GetFrontmostAppInfoV2(
    char *appName, int nameLen,
    char *windowTitle, int titleLen,
    char *bundleId, int bundleIdLen,    // 新增
    unsigned char **iconData, int *iconLen);
```
- 从 `NSRunningApplication.bundleIdentifier` 取
- 旧 `GetFrontmostAppInfo` 可保留为 shim（内部调 V2 忽略 bundleId），或干脆替换（项目只有自己用）

**重新构建**：
```bash
cd localpackage/com.nz.appmonitor/Plugins/macOS/AppMonitor
./build_appmonitor.sh
```

**`AppMonitorData.cs`** 加字段：
```csharp
public class AppInfo
{
    public string AppName;
    public string BundleId;
    public string WindowTitle;
    public Texture2D Icon;
    public bool IsSuccess;
    public AppMonitorResultCode? ErrorCode;
    public string ErrorMessage;
}
```

`MacOSAppMonitor.cs` 更新 P/Invoke 签名，填 `BundleId`。

### 4.3 新 System — `IActiveAppSystem`

```csharp
public interface IActiveAppSystem : ISystem
{
    void Tick(float deltaTime);
    ActiveAppSnapshot Current { get; }
    event Action<ActiveAppSnapshot> Changed;
}

public readonly struct ActiveAppSnapshot
{
    public readonly string Name;
    public readonly string BundleId;
    public readonly byte[] IconPngBytes;  // 与 BundleId 一起刷新，保留到下次 BundleId 变化
}
```

**行为**

- 1Hz 调用 `IAppMonitor.GetCurrentApp()`
- bundleId 不变 → 不触发 Changed（`IconPngBytes` 原样保留，供 icon_need 随时读取）
- bundleId 变 → 更新 `Current` 的 Name / BundleId / IconPngBytes（都是新 PNG）→ 触发 Changed
- 权限未给或失败 → `Current.BundleId = ""`，`state.activeApp` 会序列化为 null

Tick 由 `DeskWindowController.Update` 驱动（已有每帧 `_stateSyncSystem.Tick(Time.unscaledDeltaTime)` 的位置，紧挨着加一行即可）。

### 4.4 `StateSyncSystem` 打包 state

```csharp
private RemoteState CollectLocalState()
{
    IPomodoroModel pomodoro = this.GetModel<IPomodoroModel>();
    IActiveAppSystem activeApp = this.GetSystem<IActiveAppSystem>();
    ActiveAppSnapshot snap = activeApp.Current;

    return new RemoteState
    {
        pomodoro = new PomodoroStateDto { ... },
        activeApp = string.IsNullOrEmpty(snap.BundleId) ? null : new ActiveAppDto
        {
            name = snap.Name,
            bundleId = snap.BundleId,
            // iconBase64 不在 state_update 中传；由 icon_need 触发单独上传
        },
    };
}
```

### 4.5 新 System — `IIconCacheSystem`

```csharp
public interface IIconCacheSystem : ISystem
{
    bool HasIconFor(string bundleId);
    Texture2D GetTexture(string bundleId);
    void StoreFromBase64(string bundleId, string base64);
    string EncodeLocalPngBase64(byte[] pngBytes);
}
```

- 内存 `Dictionary<string, Texture2D>`，LRU 上限 100
- 非持久化，重启清空

### 4.6 图标协议 Unity 端实现

- `NetworkSystem.DispatchInbound` 新 case：
  - `"icon_need"` → 从 `IActiveAppSystem.Current.IconPngBytes` 取（或重新 `IAppMonitor.GetCurrentApp()` 一次）→ base64 → `icon_upload`
  - `"icon_broadcast"` → `IIconCacheSystem.StoreFromBase64` → 发 `E_IconUpdated`
- 新 Outbound DTO：`OutboundIconUpload` / `OutboundIconRequest`
- 新 Event：`E_IconUpdated { BundleId }`
- `NetworkSystem.HandleRoomSnapshot` 完成后，扫 `players[i].activeApp?.bundleId`，与 `IIconCacheSystem.HasIconFor` 对比，缺的批量 `icon_request`

### 4.7 服务端 IconCache 与新协议

- 新文件 `Server/src/IconCache.js` — LRU 类，100 条 / 单条 ≤ 1MB
- `protocol.js` 新增消息校验：`icon_upload { bundleId: string, iconBase64: string }`、`icon_request { bundleIds: string[] }`
- `index.js` 分派：
  - `player_state_update`：若 `state.activeApp?.bundleId` 且 `iconCache.has` 为假 → 回发送者 `icon_need { bundleId }`
  - `icon_upload`：校验 → `iconCache.set` → 全房间广播 `icon_broadcast`（含发送者自己，保证上传者本地卡片也有图）
  - `icon_request { bundleIds }`：对每个命中缓存的 bundleId，单发 `icon_broadcast` 给请求者

## 5. 历史房间列表

### 5.1 UI 结构（在 `osp-hist-card` 内）

```
osp-hist-card
├─ 标题 "历史房间"
└─ osp-hist-list
    └─ 每条 osp-hist-item
       ├─ osp-hist-code      房间码 "ABC123"
       ├─ osp-hist-name      上次用户名 "小明"
       ├─ osp-hist-time      相对时间 "3 小时前"
       ├─ osp-hist-join-btn  快捷加入
       └─ osp-hist-del-btn   删除
```

### 5.2 交互

| 行为 | 触发 | 结果 |
|---|---|---|
| 打开 online tab | `SelectTab("online")` | `RefreshHistoryList()` |
| 加入/创建成功 | `E_RoomJoined` / `E_RoomCreated` | `SessionMemory.RememberJoin` → 发 `E_RecentRoomsChanged` → `RefreshHistoryList` |
| 点快捷加入 | PointerUp | `SendCommand(new Cmd_JoinRoom(entry.RoomCode, _usernameField.value))` — 用**当前输入的用户名**，允许换名 |
| 点删除 | PointerUp | `SessionMemory.RemoveHistoryEntry(entry.RoomCode)` |
| 列表空 | — | 显示"暂无历史房间"占位 |
| 已在房间 | `IsInRoom == true` | 整张 `osp-hist-card` 隐藏（跟 join-card 同步） |

### 5.3 相对时间格式化

```csharp
static string FormatRelative(long unixMs)
{
    var delta = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
    if (delta.TotalSeconds < 60)  return "刚刚";
    if (delta.TotalMinutes < 60)  return $"{(int)delta.TotalMinutes} 分钟前";
    if (delta.TotalHours < 24)    return $"{(int)delta.TotalHours} 小时前";
    if (delta.TotalDays < 2)      return "昨天";
    if (delta.TotalDays < 7)      return $"{(int)delta.TotalDays} 天前";
    var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
    return $"{dt.Month}月{dt.Day}日";
}
```

## 6. 端到端 PlayMode 集成测试

### 6.1 服务端测试启动器

新文件 `Server/bin/test-server.js`：
```js
import { createPomodoroServer } from '../src/index.js';
const app = await createPomodoroServer({ port: 0 });
console.log(JSON.stringify({ port: app.port, url: app.url }));
```

### 6.2 Unity 测试工具

新文件 `Assets/Tests/PlayMode/NetworkIntegration/TestServerHarness.cs`：
- `Start()` 用 `System.Diagnostics.Process` 起 `node Server/bin/test-server.js`
- 读 stdout 第一行 JSON 拿 port → 构造 `ws://127.0.0.1:<port>`
- `Dispose()` 杀子进程
- 找不到 `node` 可执行时抛异常，测试层 `Assert.Ignore("需要 node")`（避免环境缺失时测试硬失败）

### 6.3 测试用例 — `NetworkE2ETests.cs`

**Case 1：`CreateRoom_ReceivesRoomCreatedAndSnapshot`**
- 起 server，客户端（真实 `NetworkSystem`）`SendCommand(Cmd_CreateRoom("Alice"))`
- 等 `E_RoomCreated` + `RoomModel.IsInRoom == true`（≤ 5s）
- 断言 RoomCode 非空、LocalPlayerId 非空

**Case 2：`TwoClients_JoinAndStateSync`**
- 真实客户端 A 创建房间 → 记 roomCode
- **裸 `ClientWebSocket` B** 模拟第二个玩家，发 `join_room`
- 等 A 触发 `E_PlayerJoined`、RemotePlayers 含 B
- B 发 `player_state_update` → A 触发 `E_RemoteStateUpdated`
- **不**开第二套 `GameApp`（Unity 单进程一套 Architecture）

**Case 3：`IconUploadAndBroadcast`**
- A 真实连；B 裸 WS 入房
- B 发 `player_state_update` 带 `activeApp: { bundleId: "test.app" }`
- 等服务器回 B 一条 `icon_need`
- B 发 `icon_upload { bundleId, iconBase64: "<1x1 PNG 的 base64>" }`
- 等 A 收 `icon_broadcast`，`IIconCacheSystem.HasIconFor("test.app") == true`

**Case 4：`ReconnectAfterServerDrop`**
- A 进房 → Status = InRoom
- 服务器端 `socket.terminate()` 强制断 A
- Status 进 Reconnecting → 重连成功 → Connected / InRoom
- 断言 RoomCode 未丢、玩家列表一致

### 6.4 CI / 运行约定

- 测试前需 `npm install --prefix Server --package-lock=false`
- 每测试 `[OneTimeSetUp]` 起 server、`[OneTimeTearDown]` 关闭
- 单测超时 15s（有子进程启动开销）
- 找不到 node → `Assert.Ignore`

## 7. 文件 / 目录清单

```
localpackage/com.nz.appmonitor/              (从 e74c3aa 恢复，+ 扩展 bundleId)
Packages/manifest.json                        (加依赖)

Assets/Scripts/APP/Network/
├── Model/
│   └── ... (已存在，无改动；IRoomModel/RoomModel 不动)
├── System/ (扩展，沿用现有单数目录名)
│   ├── INetworkSystem.cs          (已存在，补 icon 消息处理)
│   ├── NetworkSystem.cs           (改 DispatchInbound + DTO 字段)
│   ├── IStateSyncSystem.cs        (已存在)
│   ├── StateSyncSystem.cs         (打包 activeApp)
│   ├── IActiveAppSystem.cs        ← 新
│   ├── ActiveAppSystem.cs         ← 新
│   ├── IIconCacheSystem.cs        ← 新
│   └── IconCacheSystem.cs         ← 新
├── Command/ (扩展)
│   ├── Cmd_CreateRoom.cs          (已存在)
│   ├── Cmd_JoinRoom.cs            (已存在)
│   ├── Cmd_LeaveRoom.cs           (已存在)
│   └── Cmd_AutoReconnectOnStartup.cs ← 新
├── DTO/
│   ├── InboundMessage.cs          (字段改名 + 加 players)
│   └── OutboundMessage.cs         (加 OutboundIconUpload / OutboundIconRequest)
└── Event/
    └── NetworkEvents.cs           (加 E_IconUpdated)

Assets/Scripts/APP/SessionMemory/
├── Model/
│   ├── ISessionMemoryModel.cs     ← 新
│   ├── SessionMemoryModel.cs      ← 新
│   └── HistoryRoomEntry.cs        ← 新
└── Event/
    └── SessionMemoryEvents.cs     ← 新 (E_RecentRoomsChanged)

Assets/Scripts/APP/Utility/
├── IStorageUtility.cs              ← 新
└── PlayerPrefsStorageUtility.cs    ← 新

Assets/Scripts/APP/Pomodoro/
└── GameApp.cs                     (注册 Utility/SessionMemory/ActiveApp/IconCache)

Assets/UI_V2/
├── Documents/
│   ├── OnlineSettingsPanel.uxml   (同步 Pencil：+create 按钮 +copy 按钮 +reconnect banner +history items)
│   └── PlayerCard.uxml            (同步 Pencil：+active app 图标与名字)
├── Styles/
│   ├── OnlineSettingsPanel.uss    (新增样式)
│   └── PlayerCard.uss             (新增样式)
└── Controller/
    ├── OnlineSettingsPanelController.cs  (创建/复制/重连 banner/历史列表)
    ├── DeskWindowController.cs           (发 Cmd_AutoReconnectOnStartup、Tick ActiveAppSystem)
    ├── PlayerCardView.cs                 (贴 activeApp 图标)
    └── PlayerCardManager.cs              (订阅 E_IconUpdated)

AUI/PUI.pen                        (先改这里，再同步到 UXML/USS)

Server/
├── src/
│   ├── index.js                   (端口 8765；新 case: icon_upload/icon_request；state_update 时检查缓存)
│   ├── protocol.js                (加 icon_upload/icon_request 校验；加 createIconNeedMessage/createIconBroadcastMessage)
│   ├── RoomManager.js             (不动)
│   └── IconCache.js               ← 新
├── bin/
│   └── test-server.js             ← 新 (port=0 启动器)
└── test/
    └── icon.test.js               ← 新 (IconCache LRU / 协议 / 端到端图标流)

Assets/Tests/PlayMode/NetworkIntegration/
├── NetworkE2ETests.cs             ← 新 (4 个 Case)
└── TestServerHarness.cs           ← 新
```

## 8. 实施顺序

每个阶段独立可验证，建议按顺序推进。

### 阶段 0：基础设施

- [ ] 服务端端口改 8765
- [ ] `git checkout e74c3aa -- localpackage/com.nz.appmonitor`
- [ ] `Packages/manifest.json` 加依赖
- [ ] Unity 重新导入，确认无编译错误

**验收**：Unity 无编译错误；`npm start` 在 8765 监听。

### 阶段 1：协议对齐

- [ ] `InboundMessage` / `OutboundJoinRoom` 字段重命名
- [ ] `NetworkSystem.DispatchInbound` 改 case；`HandleRoomCreated/Joined/PlayerJoined/NetworkError` 按新协议读字段
- [ ] EditMode `MessageSerializationTests` 全绿

**验收**：手动跑 `npm start` + Unity 点"加入"，房号能正确进房间。

### 阶段 2：创建房间 + 会话记忆 + 自动重连

- [ ] `IStorageUtility` + `PlayerPrefsStorageUtility`
- [ ] `ISessionMemoryModel` + `SessionMemoryModel` + `E_RecentRoomsChanged`
- [ ] `GameApp.Init` 注册顺序：Utility → SessionMemoryModel → RoomModel → System
- [ ] **Pencil**：在 `OnlineSettingsPanel` 加 `osp-create-btn` / `osp-copy-btn` / `osp-reconnect-banner`
- [ ] UXML/USS 同步
- [ ] `OnlineSettingsPanelController`：create/copy 按钮、auto-toggle 绑定、reconnect banner
- [ ] `NetworkSystem.HandleRoomCreated/Joined`：写 `SessionMemory.RememberJoin`
- [ ] `Cmd_LeaveRoom`：写 `SessionMemory.ForgetLastRoom`
- [ ] 新 `Cmd_AutoReconnectOnStartup`，`DeskWindowController.Start` 末尾发

**验收**：EditMode 加 `SessionMemoryModelTests`；手动创建房间 → 复制；重启 Unity 自动回到房间。

### 阶段 3：历史房间列表

- [ ] **Pencil**：`osp-hist-card` 结构
- [ ] UXML/USS 同步
- [ ] `OnlineSettingsPanelController.RefreshHistoryList`
- [ ] 快捷加入 / 删除按钮
- [ ] 订阅 `E_RecentRoomsChanged` 自动刷新

**验收**：EditMode 测 `FormatRelative`；手动进 3 个房间后看列表。

### 阶段 4：ActiveApp 采集

- [ ] 扩展 `AppMonitor.m` 加 bundleId 导出
- [ ] `build_appmonitor.sh` 重新构建 bundle
- [ ] `IAppMonitor / AppInfo / MacOSAppMonitor` 加 BundleId 字段与 P/Invoke
- [ ] 新 `IActiveAppSystem` + `ActiveAppSystem`
- [ ] `GameApp` 注册
- [ ] `StateSyncSystem.CollectLocalState` 打包 activeApp
- [ ] `DeskWindowController.Update` 调 `ActiveAppSystem.Tick`

**验收**：Unity 运行切 App 时 Console 有 bundleId 日志；state 中 activeApp 字段非 null。

### 阶段 5：图标协议（双端）

- [ ] 服务端 `Server/src/IconCache.js` + 单测
- [ ] 服务端 `protocol.js` 加 icon 消息校验
- [ ] 服务端 `index.js` 分派：state_update / icon_upload / icon_request
- [ ] 服务端 `test/icon.test.js`
- [ ] 客户端 `IIconCacheSystem` + `IconCacheSystem`
- [ ] 客户端 `NetworkSystem` 新 case：`icon_need` / `icon_broadcast`
- [ ] 客户端 `OutboundIconUpload` / `OutboundIconRequest`
- [ ] `HandleRoomSnapshot` 后批量 `icon_request`
- [ ] **Pencil**：`PlayerCard` 的 active app 图标/名字区
- [ ] UXML/USS 同步
- [ ] `PlayerCardView` 贴图
- [ ] `PlayerCardManager` 订阅 `E_IconUpdated`

**验收**：两台 Mac 或 NetworkSimulator 跑通 icon 流；Console 看到 `icon_need → upload → broadcast` 日志。

### 阶段 6：端到端 PlayMode 测试

- [ ] `Server/bin/test-server.js`
- [ ] `TestServerHarness.cs`
- [ ] `NetworkE2ETests.cs` 4 个 case

**验收**：PlayMode 测试全绿。

## 9. 风险与脆弱点

| 风险 | 影响 | 缓解 |
|---|---|---|
| 恢复的 AppMonitor.bundle 是预编译二进制，跨 macOS/Xcode 版本或签名可能失效 | 插件加载失败 | 恢复后先跑 `build_appmonitor.sh` 重建；`MacOSAppMonitor` 已有 try-catch fallback |
| 扩展 `AppMonitor.m` C 函数签名会破坏旧测试 `AppMonitorPlayerTest.cs` | 旧测试红 | 直接改签名，同步更新旧测试（项目仅自用） |
| 无辅助功能权限时 bundleId 为空 | 服务端永远没 activeApp | `ActiveAppSystem` 已保证 BundleId 空 → `state.activeApp = null`；服务端不会误触 icon_need |
| PlayMode 测试依赖本机 node | 缺 node 的环境硬失败 | `TestServerHarness` 找不到 node → `Assert.Ignore` |
| JsonUtility 不支持顶层数组 | `icon_request.bundleIds` 用法 | 已经包在对象里（`{ type, bundleIds: [...] }`），OK |
| 1MB iconBase64 单帧 WebSocket 传输 | 客户端 / 服务端 send 异常 | Node `ws` 默认 100MB payload；.NET ClientWebSocket 无硬限制，OK |
| 服务端 LRU 100 × 1MB = 最坏 100MB 常驻内存 | 自用够，留意 | 后续按需降 cap 或加 TTL |
| 同时多客户端断线重连可能留幽灵玩家 | 列表多出过期玩家 | 服务端已有 30s 心跳超时 + leaveRoom；压力测试不在本次范围 |

## 10. 不在本次范围

- TLS / wss 跨网部署（本期本地/同局域网）
- 音视频互动 / 聊天 / 表情
- 房间密码 / 踢人 / 禁言
- 跨平台 AppMonitor（Windows / Linux）
- 服务端持久化 / 数据库
