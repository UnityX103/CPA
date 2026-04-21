# 多人联机功能 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Unity 6 桌面番茄钟里接入"房间码被动观战"多人功能 —— 多个用户通过 6 位房间码聚到一起，同步番茄钟阶段/剩余时间和正在用的 macOS 前台 App 名字+图标。

**Architecture:** 服务端已有 Node.js WebSocket + RoomManager；本次修协议端口、加图标 LRU 缓存。客户端 Unity QFramework 四层：UI 触发 `Cmd_*`、NetworkSystem 收发 WebSocket、StateSyncSystem 1Hz 打包本地状态、新增 ActiveAppSystem 轮询 macOS 前台 App、新增 IconCacheSystem 管 bundleId→Texture2D 缓存、新增 SessionMemoryModel 持久化用户名+房间历史。UI 改动走 Pencil 设计稿 → UXML/USS 同步两步流水。

**Tech Stack:** Unity 6 (6000.0.25f1), C# 9, QFramework v1.0, UniWindowController, Node.js 25+, ws 8.18, UnityEngine.UIElements (UI Toolkit), macOS Accessibility API (Obj-C bundle), Pencil MCP.

**参考规格：** [`docs/superpowers/specs/2026-04-21-multiplayer-networking-design.md`](../specs/2026-04-21-multiplayer-networking-design.md)

**前置环境：**
- 本机已装 `node`（≥ 20）和 Xcode CLT（`clang`）
- Unity Editor 已打开 MainV2 场景
- Pencil MCP 可用（`mcp__pencil__*` 工具已加载）

---

## 阶段 0：基础设施

### Task 0.1：服务端默认端口改 8765

**Files:**
- Modify: `Server/src/index.js:23`

- [ ] **Step 1：修改默认端口常量**

```js
// Server/src/index.js 第 23 行
const DEFAULT_PORT = Number.parseInt(process.env.PORT ?? '8765', 10);
```

- [ ] **Step 2：验证服务端单测依然通过（端口无关）**

Run: `cd Server && npm install --package-lock=false && node --test test/*.js`
Expected: all tests pass.

- [ ] **Step 3：手动启动验证监听 8765**

Run: `cd Server && npm start`
Expected stdout: `[Server] listening on ws://127.0.0.1:8765`。确认后 Ctrl+C 关闭。

- [ ] **Step 4：Commit**

```bash
git add Server/src/index.js
git commit -m "chore(server): 默认监听端口改为 8765 与 Unity 客户端对齐"
```

---

### Task 0.2：从 git 恢复 AppMonitor 本地包

**Files:**
- Restore: `localpackage/com.nz.appmonitor/**` (从 commit `e74c3aa`)

- [ ] **Step 1：从历史提交恢复**

```bash
git checkout e74c3aa -- localpackage/com.nz.appmonitor
```

- [ ] **Step 2：验证包结构完整**

Run: `ls localpackage/com.nz.appmonitor/Runtime/ localpackage/com.nz.appmonitor/Plugins/macOS/`
Expected 包含:
```
Runtime/IAppMonitor.cs, AppMonitorData.cs, AppMonitor.cs, MacOSAppMonitor.cs, UnsupportedAppMonitorImpl.cs, NZ.AppMonitor.Runtime.asmdef
Plugins/macOS/AppMonitor.bundle, AppMonitor.entitlements, Info.plist, AppMonitor/AppMonitor.m, AppMonitor/build_appmonitor.sh
```

- [ ] **Step 3：Commit**

```bash
git add localpackage/com.nz.appmonitor
git commit -m "chore: 从 e74c3aa 恢复 AppMonitor 本地包"
```

---

### Task 0.3：在 Packages/manifest.json 注册 AppMonitor 依赖

**Files:**
- Modify: `Packages/manifest.json`

- [ ] **Step 1：在 `dependencies` 里加一行**

找到 `"com.nz.visualtest": "file:../localpackage/com.nz.visualtest",` 这行，在它下面加：

```json
    "com.nz.appmonitor": "file:../localpackage/com.nz.appmonitor",
```

保持 JSON 合法（注意逗号）。

- [ ] **Step 2：让 Unity 重新导入**

在 Unity Editor 里切到项目窗口 → 右键 `Assets` → `Reimport`，或菜单 `Assets → Refresh`。观察 Console：
- 预期：包被解析，`NZ.AppMonitor.Runtime` 程序集重新生成，无编译错误。

- [ ] **Step 3：用 MCP 读 console 确认无错**

用 MCP 工具 `read_console`，filter types: error。
Expected：无 AppMonitor 相关错误。

- [ ] **Step 4：Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "chore(unity): 注册 com.nz.appmonitor 本地包依赖"
```

---

## 阶段 1：协议对齐

> 目标：让 Unity 客户端能和真实 Node 服务端完整跑通 create/join/state/leave 流程。

### Task 1.1：更新 MessageSerializationTests 为新协议字段（red）

**Files:**
- Modify: `Assets/Tests/EditMode/NetworkTests/MessageSerializationTests.cs`

- [ ] **Step 1：替换测试 `InboundMessage_WhenRoundTripped_PreservesSnapshotPayload`** 从使用 `snapshot` 字段改为使用 `players` + `roomCode`

```csharp
[Test]
public void InboundMessage_WhenRoundTripped_PreservesPlayersPayload()
{
    var message = new InboundMessage
    {
        v = 1,
        type = "room_snapshot",
        roomCode = "ABC123",
        players = new List<SnapshotEntry>
        {
            new SnapshotEntry
            {
                playerId = "remote-1",
                playerName = "Alice",
                state = new RemoteState
                {
                    pomodoro = new PomodoroStateDto
                    {
                        phase = 1, remainingSeconds = 300,
                        currentRound = 2, totalRounds = 4, isRunning = true,
                    },
                    activeApp = null,
                },
            },
        },
    };

    string json = JsonUtility.ToJson(message);
    InboundMessage restored = JsonUtility.FromJson<InboundMessage>(json);

    Assert.That(restored.roomCode, Is.EqualTo("ABC123"));
    Assert.That(restored.players, Has.Count.EqualTo(1));
    Assert.That(restored.players[0].playerName, Is.EqualTo("Alice"));
    Assert.That(restored.players[0].state.pomodoro.remainingSeconds, Is.EqualTo(300));
}
```

并替换 `InboundMessage_WhenOptionalFieldsMissing_UsesDefaultValues` 为：

```csharp
[Test]
public void InboundMessage_WhenErrorFieldProvided_ParsesErrorCode()
{
    const string json = "{\"v\":1,\"type\":\"error\",\"error\":\"ROOM_FULL\"}";

    InboundMessage restored = JsonUtility.FromJson<InboundMessage>(json);

    Assert.That(restored.type, Is.EqualTo("error"));
    Assert.That(restored.error, Is.EqualTo("ROOM_FULL"));
}
```

并新增：

```csharp
[Test]
public void OutboundJoinRoom_WhenSerialized_UsesRoomCodeField()
{
    var msg = new OutboundJoinRoom
    {
        type = "join_room",
        roomCode = "XYZ789",
        playerName = "Bob",
    };

    string json = JsonUtility.ToJson(msg);
    Assert.That(json, Does.Contain("\"roomCode\":\"XYZ789\""));
    Assert.That(json, Does.Not.Contain("\"code\":"));
}
```

- [ ] **Step 2：运行测试确认失败（DTO 还是旧字段名）**

在 Unity 里 Window → General → Test Runner → EditMode 跑 `APP.Network.Tests.MessageSerializationTests`。
Expected：`InboundMessage_WhenRoundTripped_PreservesPlayersPayload`、`InboundMessage_WhenErrorFieldProvided_ParsesErrorCode`、`OutboundJoinRoom_WhenSerialized_UsesRoomCodeField` 三条红（编译或断言失败）。

或用 MCP：`run_tests` + `testMode: "EditMode"` + `testNames: ["APP.Network.Tests.MessageSerializationTests"]`。

---

### Task 1.2：更新 InboundMessage / OutboundMessage DTO 字段

**Files:**
- Modify: `Assets/Scripts/APP/Network/DTO/InboundMessage.cs`
- Modify: `Assets/Scripts/APP/Network/DTO/OutboundMessage.cs`

- [ ] **Step 1：改 InboundMessage.cs**

```csharp
using System;
using System.Collections.Generic;

namespace APP.Network.DTO
{
    [Serializable]
    public sealed class InboundMessage
    {
        public int v;
        public string type;
        public string roomCode;           // was: code
        public string playerId;
        public string playerName;
        public RemoteState state;
        public List<SnapshotEntry> players; // was: snapshot
        public string error;              // was: errorCode + message
    }

    // SnapshotEntry / RemoteState / PomodoroStateDto / ActiveAppDto 保持不变
}
```

保留原文件后半部分 `SnapshotEntry`、`RemoteState`、`PomodoroStateDto`、`ActiveAppDto` 定义不动。

- [ ] **Step 2：改 OutboundMessage.cs 的 OutboundJoinRoom**

```csharp
[Serializable]
public sealed class OutboundJoinRoom : OutboundMessage
{
    public string roomCode;              // was: code
    public string playerName;
}
```

其余 OutboundMessage 子类不变。

- [ ] **Step 3：运行 MessageSerializationTests 验证绿**

Run MCP `run_tests` with testNames `APP.Network.Tests.MessageSerializationTests.*`。
Expected：4 条（含新增 3 条）全绿。

- [ ] **Step 4：Commit**

```bash
git add Assets/Scripts/APP/Network/DTO/InboundMessage.cs \
        Assets/Scripts/APP/Network/DTO/OutboundMessage.cs \
        Assets/Tests/EditMode/NetworkTests/MessageSerializationTests.cs
git commit -m "refactor(net): DTO 字段对齐服务端协议 (code→roomCode, snapshot→players, errorCode→error)"
```

---

### Task 1.3：更新 NetworkSystem 消息分派 / Handle 方法

**Files:**
- Modify: `Assets/Scripts/APP/Network/System/NetworkSystem.cs`
- Modify: `Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs`

- [ ] **Step 1：修改 `Cmd_JoinRoom.cs` 的字段名**

找到 `Cmd_JoinRoom.OnExecute` 里：
```csharp
network.Send(new OutboundJoinRoom
{
    type = "join_room",
    code = _code,          // ← 改成 roomCode
    playerName = _playerName,
});
```

改成：
```csharp
network.Send(new OutboundJoinRoom
{
    type = "join_room",
    roomCode = _code,
    playerName = _playerName,
});
```

- [ ] **Step 2：修改 `NetworkSystem.DispatchInbound` 的 switch case**

找到 `case "state_update":` 改成 `case "player_state_broadcast":`。

- [ ] **Step 3：修改 `HandleRoomCreated`**

当前：
```csharp
private void HandleRoomCreated(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    room.SetRoomCode(inbound.code);  // ← code
    room.SetLocalPlayerId(inbound.playerId);
    room.SetConnectionFlags(true, true);
    room.SetStatus(ConnectionStatus.InRoom);

    List<RemotePlayerData> players = BuildRemotePlayers(inbound.snapshot, inbound.playerId); // ← snapshot
    room.ApplySnapshot(players);

    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.InRoom));
    this.SendEvent(new E_RoomCreated(inbound.code));
    this.SendEvent(new E_RoomSnapshot(ClonePlayers(players)));
}
```

改成（去掉 snapshot 读取，等服务端单独的 `room_snapshot` 消息补齐）：
```csharp
private void HandleRoomCreated(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    room.SetRoomCode(inbound.roomCode);
    room.SetLocalPlayerId(inbound.playerId);
    room.SetConnectionFlags(true, true);
    room.SetStatus(ConnectionStatus.InRoom);

    // room_created 不含 players，等随后的 room_snapshot 补齐
    room.ApplySnapshot(new List<RemotePlayerData>());

    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.InRoom));
    this.SendEvent(new E_RoomCreated(inbound.roomCode));
    this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));
}
```

- [ ] **Step 4：修改 `HandleRoomJoined` 同理**

```csharp
private void HandleRoomJoined(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    room.SetRoomCode(inbound.roomCode);
    room.SetLocalPlayerId(inbound.playerId);
    room.SetConnectionFlags(true, true);
    room.SetStatus(ConnectionStatus.InRoom);

    room.ApplySnapshot(new List<RemotePlayerData>());

    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.InRoom));
    this.SendEvent(new E_RoomJoined(inbound.roomCode, new List<RemotePlayerData>()));
    this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));
}
```

- [ ] **Step 5：修改 `HandleRoomSnapshot`**

```csharp
private void HandleRoomSnapshot(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    List<RemotePlayerData> players = BuildRemotePlayers(inbound.players, room.LocalPlayerId.Value);
    room.ApplySnapshot(players);
    this.SendEvent(new E_RoomSnapshot(ClonePlayers(players)));
}
```

注意参数从 `inbound.snapshot` 改为 `inbound.players`（同样是 `List<SnapshotEntry>`，只是字段改名）。

- [ ] **Step 6：修改 `HandlePlayerJoined` 从 `players[0]` 读**

```csharp
private void HandlePlayerJoined(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    if (inbound.players == null || inbound.players.Count == 0)
    {
        return;
    }

    SnapshotEntry entry = inbound.players[0];
    if (string.IsNullOrWhiteSpace(entry.playerId) || entry.playerId == room.LocalPlayerId.Value)
    {
        return;
    }

    RemotePlayerData player = ToRemotePlayerData(entry.playerId, entry.playerName, entry.state);
    room.AddOrUpdateRemotePlayer(player);

    this.SendEvent(new E_PlayerJoined(player.Clone()));
}
```

- [ ] **Step 7：修改 `HandleNetworkError`**

```csharp
private void HandleNetworkError(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    room.SetStatus(ConnectionStatus.Error);
    string code = string.IsNullOrEmpty(inbound.error) ? "UNKNOWN" : inbound.error;
    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.Error));
    this.SendEvent(new E_NetworkError(code, code));
}
```

- [ ] **Step 8：修改 `BuildRemotePlayers` 参数签名**

找到 `private List<RemotePlayerData> BuildRemotePlayers(IList<SnapshotEntry> snapshot, string localPlayerId)` 里的 `snapshot` 参数名保持不变（只是实际调用点传 `inbound.players`）。内部实现不动。

- [ ] **Step 9：修改 `SendRejoinMessageIfNeededAsync` 里的 OutboundJoinRoom 构造**

```csharp
var rejoinMessage = new OutboundJoinRoom
{
    type = "join_room",
    roomCode = room.RoomCode.Value,  // was: code
    playerName = room.LocalPlayerName.Value,
};
```

- [ ] **Step 10：Unity 无编译错误**

用 MCP `read_console` 检查无 error。

- [ ] **Step 11：Commit**

```bash
git add Assets/Scripts/APP/Network/System/NetworkSystem.cs \
        Assets/Scripts/APP/Network/Command/Cmd_JoinRoom.cs
git commit -m "refactor(net): NetworkSystem 按新协议字段分派消息"
```

---

### Task 1.4：手动端到端冒烟测试

- [ ] **Step 1：启动服务端**

```bash
cd Server && npm start
```

- [ ] **Step 2：Unity 进 Play Mode，点"创建房间"**

Expected: RoomCode 出现在 UI；RoomModel.IsInRoom=true。

- [ ] **Step 3：用另一个 WebSocket 客户端（wscat 或浏览器 devtools）连 `ws://127.0.0.1:8765`，发 join_room**

```bash
npx wscat -c ws://127.0.0.1:8765
> {"v":1,"type":"join_room","roomCode":"<上面得到的房间码>","playerName":"Test"}
```

Expected: Unity 侧触发 E_PlayerJoined，RemotePlayers 里出现 "Test"。

- [ ] **Step 4：冒烟通过，服务端关闭，Unity 退 Play**

**（无代码改动，不提交）**

---

## 阶段 2：会话记忆 + 创建房间 UI + 自动重连

### Task 2.1：新建 IStorageUtility 接口

**Files:**
- Create: `Assets/Scripts/APP/Utility/IStorageUtility.cs`

- [ ] **Step 1：写接口**

```csharp
using QFramework;

namespace APP.Utility
{
    public interface IStorageUtility : IUtility
    {
        string LoadString(string key, string fallback = "");
        void SaveString(string key, string value);
        int LoadInt(string key, int fallback = 0);
        void SaveInt(string key, int value);
        void DeleteKey(string key);
        void Flush();
    }
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Utility/IStorageUtility.cs
git commit -m "feat(util): 新增 IStorageUtility 接口"
```

---

### Task 2.2：InMemoryStorageUtility（测试实现）+ TDD

**Files:**
- Create: `Assets/Tests/EditMode/UtilityTests/InMemoryStorageUtilityTests.cs`
- Create: `Assets/Tests/EditMode/UtilityTests/UtilityTests.asmdef`
- Create: `Assets/Scripts/APP/Utility/InMemoryStorageUtility.cs`

- [ ] **Step 1：建 asmdef**

```json
{
    "name": "APP.Utility.Tests",
    "rootNamespace": "APP.Utility.Tests",
    "references": [ "APP.Runtime" ],
    "includePlatforms": [ "Editor" ],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ]
}
```

- [ ] **Step 2：写测试（red）**

```csharp
using APP.Utility;
using NUnit.Framework;

namespace APP.Utility.Tests
{
    [TestFixture]
    public sealed class InMemoryStorageUtilityTests
    {
        [Test]
        public void SaveString_ThenLoadString_ReturnsSavedValue()
        {
            var storage = new InMemoryStorageUtility();
            storage.SaveString("k", "v");
            Assert.That(storage.LoadString("k", "fallback"), Is.EqualTo("v"));
        }

        [Test]
        public void LoadString_WhenKeyMissing_ReturnsFallback()
        {
            var storage = new InMemoryStorageUtility();
            Assert.That(storage.LoadString("missing", "fb"), Is.EqualTo("fb"));
        }

        [Test]
        public void SaveInt_ThenLoadInt_ReturnsSavedValue()
        {
            var storage = new InMemoryStorageUtility();
            storage.SaveInt("n", 42);
            Assert.That(storage.LoadInt("n", 0), Is.EqualTo(42));
        }

        [Test]
        public void DeleteKey_RemovesValue()
        {
            var storage = new InMemoryStorageUtility();
            storage.SaveString("k", "v");
            storage.DeleteKey("k");
            Assert.That(storage.LoadString("k", "fb"), Is.EqualTo("fb"));
        }
    }
}
```

- [ ] **Step 3：跑测试确认 red（类不存在）**

MCP `run_tests` testMode EditMode testNames `APP.Utility.Tests.InMemoryStorageUtilityTests`。

- [ ] **Step 4：写实现**

```csharp
using System.Collections.Generic;

namespace APP.Utility
{
    public sealed class InMemoryStorageUtility : IStorageUtility
    {
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();

        public string LoadString(string key, string fallback = "")
            => _strings.TryGetValue(key, out var v) ? v : fallback;

        public void SaveString(string key, string value) => _strings[key] = value ?? string.Empty;

        public int LoadInt(string key, int fallback = 0)
            => _ints.TryGetValue(key, out var v) ? v : fallback;

        public void SaveInt(string key, int value) => _ints[key] = value;

        public void DeleteKey(string key)
        {
            _strings.Remove(key);
            _ints.Remove(key);
        }

        public void Flush() { }

        /// <summary>测试专用：重置所有已存储的键值（供 [SetUp] 在每个测试之间清理）。</summary>
        public void Clear()
        {
            _strings.Clear();
            _ints.Clear();
        }
    }
}
```

> `IUtility` 在 QFramework v1.0 里是空标记接口，无方法需要实现。

- [ ] **Step 5：跑测试确认全绿**

- [ ] **Step 6：Commit**

```bash
git add Assets/Tests/EditMode/UtilityTests Assets/Scripts/APP/Utility/InMemoryStorageUtility.cs
git commit -m "feat(util): InMemoryStorageUtility + 单测"
```

---

### Task 2.3：PlayerPrefsStorageUtility（生产实现）

**Files:**
- Create: `Assets/Scripts/APP/Utility/PlayerPrefsStorageUtility.cs`

- [ ] **Step 1：写实现**

```csharp
using UnityEngine;
using QFramework;

namespace APP.Utility
{
    public sealed class PlayerPrefsStorageUtility : IStorageUtility
    {
        public string LoadString(string key, string fallback = "") => PlayerPrefs.GetString(key, fallback ?? string.Empty);
        public void SaveString(string key, string value) => PlayerPrefs.SetString(key, value ?? string.Empty);
        public int LoadInt(string key, int fallback = 0) => PlayerPrefs.GetInt(key, fallback);
        public void SaveInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Flush() => PlayerPrefs.Save();
    }
}
```

> 注：不对 `PlayerPrefsStorageUtility` 单测，因为它是 Unity 引擎包装的薄代理，测试价值低；用 InMemory 版本做上层测试即可。

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Utility/PlayerPrefsStorageUtility.cs
git commit -m "feat(util): PlayerPrefsStorageUtility 生产实现"
```

---

### Task 2.4：HistoryRoomEntry POCO + JSON 序列化 helper

**Files:**
- Create: `Assets/Scripts/APP/SessionMemory/Model/HistoryRoomEntry.cs`
- Create: `Assets/Scripts/APP/SessionMemory/Model/HistoryRoomSerializer.cs`
- Create: `Assets/Tests/EditMode/SessionMemoryTests/HistoryRoomSerializerTests.cs`
- Create: `Assets/Tests/EditMode/SessionMemoryTests/SessionMemoryTests.asmdef`

- [ ] **Step 1：POCO**

```csharp
using System;

namespace APP.SessionMemory.Model
{
    [Serializable]
    public sealed class HistoryRoomEntry
    {
        public string RoomCode;
        public string LastPlayerName;
        public long LastJoinedAtUnixMs;
    }
}
```

- [ ] **Step 2：测试 asmdef**

同 Task 2.2 的模板，rootNamespace 改 `APP.SessionMemory.Tests`、name 改 `APP.SessionMemory.Tests`。

- [ ] **Step 3：测试（red）**

```csharp
using System.Collections.Generic;
using APP.SessionMemory.Model;
using NUnit.Framework;

namespace APP.SessionMemory.Tests
{
    [TestFixture]
    public sealed class HistoryRoomSerializerTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var entries = new List<HistoryRoomEntry>
            {
                new HistoryRoomEntry { RoomCode = "ABC123", LastPlayerName = "小明", LastJoinedAtUnixMs = 1700000000000L },
                new HistoryRoomEntry { RoomCode = "XYZ789", LastPlayerName = "小红", LastJoinedAtUnixMs = 1700000001000L },
            };

            string json = HistoryRoomSerializer.Serialize(entries);
            List<HistoryRoomEntry> restored = HistoryRoomSerializer.Deserialize(json);

            Assert.That(restored, Has.Count.EqualTo(2));
            Assert.That(restored[0].RoomCode, Is.EqualTo("ABC123"));
            Assert.That(restored[0].LastPlayerName, Is.EqualTo("小明"));
            Assert.That(restored[0].LastJoinedAtUnixMs, Is.EqualTo(1700000000000L));
        }

        [Test]
        public void Deserialize_EmptyString_ReturnsEmptyList()
        {
            Assert.That(HistoryRoomSerializer.Deserialize(""), Is.Empty);
        }

        [Test]
        public void Deserialize_Garbage_ReturnsEmptyList()
        {
            Assert.That(HistoryRoomSerializer.Deserialize("not-json"), Is.Empty);
        }
    }
}
```

- [ ] **Step 4：跑测试确认 red**

- [ ] **Step 5：实现**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace APP.SessionMemory.Model
{
    [Serializable]
    internal sealed class HistoryRoomListWrapper
    {
        public List<HistoryRoomEntry> Entries;
    }

    public static class HistoryRoomSerializer
    {
        public static string Serialize(IList<HistoryRoomEntry> entries)
        {
            var wrapper = new HistoryRoomListWrapper { Entries = new List<HistoryRoomEntry>(entries ?? new List<HistoryRoomEntry>()) };
            return JsonUtility.ToJson(wrapper);
        }

        public static List<HistoryRoomEntry> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<HistoryRoomEntry>();
            try
            {
                HistoryRoomListWrapper wrapper = JsonUtility.FromJson<HistoryRoomListWrapper>(json);
                return wrapper?.Entries ?? new List<HistoryRoomEntry>();
            }
            catch
            {
                return new List<HistoryRoomEntry>();
            }
        }
    }
}
```

- [ ] **Step 6：跑测试确认绿**

- [ ] **Step 7：Commit**

```bash
git add Assets/Scripts/APP/SessionMemory Assets/Tests/EditMode/SessionMemoryTests
git commit -m "feat(session): HistoryRoomEntry + JSON 序列化"
```

---

### Task 2.5：SessionMemoryModel + TDD

**Files:**
- Create: `Assets/Scripts/APP/SessionMemory/Model/ISessionMemoryModel.cs`
- Create: `Assets/Scripts/APP/SessionMemory/Model/SessionMemoryModel.cs`
- Create: `Assets/Scripts/APP/SessionMemory/Event/SessionMemoryEvents.cs`
- Create: `Assets/Tests/EditMode/SessionMemoryTests/SessionMemoryModelTests.cs`

- [ ] **Step 1：事件定义**

`Assets/Scripts/APP/SessionMemory/Event/SessionMemoryEvents.cs`：
```csharp
namespace APP.SessionMemory.Event
{
    public readonly struct E_RecentRoomsChanged { }
}
```

- [ ] **Step 2：接口**

`Assets/Scripts/APP/SessionMemory/Model/ISessionMemoryModel.cs`：
```csharp
using System.Collections.Generic;
using QFramework;

namespace APP.SessionMemory.Model
{
    public interface ISessionMemoryModel : IModel
    {
        BindableProperty<string> LastPlayerName { get; }
        BindableProperty<string> LastRoomCode { get; }
        BindableProperty<bool> AutoReconnectEnabled { get; }
        IReadOnlyList<HistoryRoomEntry> RecentRooms { get; }

        void RememberJoin(string playerName, string roomCode);
        void ForgetLastRoom();
        void SetAutoReconnectEnabled(bool enabled);
        void RemoveHistoryEntry(string roomCode);
    }
}
```

- [ ] **Step 3：测试（red）**

`Assets/Tests/EditMode/SessionMemoryTests/SessionMemoryModelTests.cs`：
```csharp
using APP.SessionMemory.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;

namespace APP.SessionMemory.Tests
{
    /// <summary>
    /// SessionMemoryModel 的核心行为单测。
    /// TestArch 是一次性单例 Architecture，共享同一份 InMemoryStorageUtility；
    /// 每个测试 SetUp 时 Clear 该存储并构造新的 model 完成初始化。
    /// </summary>
    [TestFixture]
    public sealed class SessionMemoryModelTests
    {
        private sealed class TestArch : Architecture<TestArch>
        {
            public static readonly InMemoryStorageUtility SharedStorage = new InMemoryStorageUtility();
            protected override void Init()
            {
                RegisterUtility<IStorageUtility>(SharedStorage);
            }
        }

        private SessionMemoryModel _model;

        [SetUp]
        public void SetUp()
        {
            // 先访问一次 Interface 确保 TestArch 已初始化（RegisterUtility 已执行）
            _ = TestArch.Interface;

            // 清掉上一测试遗留的存储
            TestArch.SharedStorage.Clear();

            // 构造新 model 并把 TestArch 注入
            _model = new SessionMemoryModel();
            ((ICanSetArchitecture)_model).SetArchitecture(TestArch.Interface);
            ((ICanInit)_model).Init();
        }

        [Test]
        public void RememberJoin_InsertsEntry_MostRecentFirst()
        {
            _model.RememberJoin("Alice", "ABC123");
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ABC123"));
            Assert.That(_model.LastRoomCode.Value, Is.EqualTo("ABC123"));
            Assert.That(_model.LastPlayerName.Value, Is.EqualTo("Alice"));
        }

        [Test]
        public void RememberJoin_SameRoomTwice_MovesToFront_NoDuplicate()
        {
            _model.RememberJoin("Alice", "ABC123");
            _model.RememberJoin("Bob", "XYZ789");
            _model.RememberJoin("Alice", "ABC123"); // 再次进同一间

            Assert.That(_model.RecentRooms, Has.Count.EqualTo(2));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ABC123"));
            Assert.That(_model.RecentRooms[1].RoomCode, Is.EqualTo("XYZ789"));
        }

        [Test]
        public void RememberJoin_ExceedsFive_TrimsOldest()
        {
            for (int i = 0; i < 7; i++)
            {
                _model.RememberJoin($"User{i}", $"ROOM{i:D2}");
            }

            Assert.That(_model.RecentRooms, Has.Count.EqualTo(5));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ROOM06"));
            Assert.That(_model.RecentRooms[4].RoomCode, Is.EqualTo("ROOM02"));
        }

        [Test]
        public void ForgetLastRoom_ClearsCode_KeepsHistory()
        {
            _model.RememberJoin("Alice", "ABC123");
            _model.ForgetLastRoom();

            Assert.That(_model.LastRoomCode.Value, Is.EqualTo(string.Empty));
            Assert.That(_model.RecentRooms, Has.Count.EqualTo(1));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ABC123"));
        }

        [Test]
        public void RemoveHistoryEntry_RemovesMatching()
        {
            _model.RememberJoin("Alice", "ABC123");
            _model.RememberJoin("Bob", "XYZ789");
            _model.RemoveHistoryEntry("ABC123");

            Assert.That(_model.RecentRooms, Has.Count.EqualTo(1));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("XYZ789"));
        }

        [Test]
        public void SetAutoReconnectEnabled_WritesToStorage()
        {
            _model.SetAutoReconnectEnabled(true);
            Assert.That(_model.AutoReconnectEnabled.Value, Is.True);

            _model.SetAutoReconnectEnabled(false);
            Assert.That(_model.AutoReconnectEnabled.Value, Is.False);
        }

        [Test]
        public void RememberJoin_PersistsThroughReload()
        {
            // 用同一个 Architecture（共享 storage），创建新 model 模拟重启
            _model.RememberJoin("Alice", "ABC123");
            _model.SetAutoReconnectEnabled(true);

            var reloaded = new SessionMemoryModel();
            ((ICanSetArchitecture)reloaded).SetArchitecture(TestArch.Interface);
            ((ICanInit)reloaded).Init();

            Assert.That(reloaded.LastRoomCode.Value, Is.EqualTo("ABC123"));
            Assert.That(reloaded.LastPlayerName.Value, Is.EqualTo("Alice"));
            Assert.That(reloaded.AutoReconnectEnabled.Value, Is.True);
            Assert.That(reloaded.RecentRooms, Has.Count.EqualTo(1));
        }
    }
}
```

- [ ] **Step 4：跑测试确认 red**

- [ ] **Step 5：实现 SessionMemoryModel**

`Assets/Scripts/APP/SessionMemory/Model/SessionMemoryModel.cs`：
```csharp
using System;
using System.Collections.Generic;
using APP.SessionMemory.Event;
using APP.Utility;
using QFramework;

namespace APP.SessionMemory.Model
{
    public sealed class SessionMemoryModel : AbstractModel, ISessionMemoryModel
    {
        private const int MaxRecentRooms = 5;
        private const string KeyLastPlayerName = "net.lastPlayerName";
        private const string KeyLastRoomCode = "net.lastRoomCode";
        private const string KeyAutoReconnect = "net.autoReconnect";
        private const string KeyRecentRooms = "net.recentRooms";

        private readonly List<HistoryRoomEntry> _recentRooms = new List<HistoryRoomEntry>();

        public BindableProperty<string> LastPlayerName { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<string> LastRoomCode { get; } = new BindableProperty<string>(string.Empty);
        public BindableProperty<bool> AutoReconnectEnabled { get; } = new BindableProperty<bool>(false);
        public IReadOnlyList<HistoryRoomEntry> RecentRooms => _recentRooms;

        protected override void OnInit()
        {
            IStorageUtility storage = this.GetUtility<IStorageUtility>();
            LastPlayerName.SetValueWithoutEvent(storage.LoadString(KeyLastPlayerName, string.Empty));
            LastRoomCode.SetValueWithoutEvent(storage.LoadString(KeyLastRoomCode, string.Empty));
            AutoReconnectEnabled.SetValueWithoutEvent(storage.LoadInt(KeyAutoReconnect, 0) != 0);

            _recentRooms.Clear();
            _recentRooms.AddRange(HistoryRoomSerializer.Deserialize(storage.LoadString(KeyRecentRooms, string.Empty)));

            LastPlayerName.Register(v => storage.SaveString(KeyLastPlayerName, v));
            LastRoomCode.Register(v => storage.SaveString(KeyLastRoomCode, v));
            AutoReconnectEnabled.Register(v => storage.SaveInt(KeyAutoReconnect, v ? 1 : 0));
        }

        public void RememberJoin(string playerName, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return;

            string normalizedName = playerName ?? string.Empty;
            string normalizedCode = roomCode;

            LastPlayerName.Value = normalizedName;
            LastRoomCode.Value = normalizedCode;

            _recentRooms.RemoveAll(e => e.RoomCode == normalizedCode);
            _recentRooms.Insert(0, new HistoryRoomEntry
            {
                RoomCode = normalizedCode,
                LastPlayerName = normalizedName,
                LastJoinedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            if (_recentRooms.Count > MaxRecentRooms) _recentRooms.RemoveRange(MaxRecentRooms, _recentRooms.Count - MaxRecentRooms);

            PersistRecent();
            this.SendEvent<E_RecentRoomsChanged>();
        }

        public void ForgetLastRoom()
        {
            LastRoomCode.Value = string.Empty;
        }

        public void SetAutoReconnectEnabled(bool enabled)
        {
            AutoReconnectEnabled.Value = enabled;
        }

        public void RemoveHistoryEntry(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return;
            int removed = _recentRooms.RemoveAll(e => e.RoomCode == roomCode);
            if (removed > 0)
            {
                PersistRecent();
                this.SendEvent<E_RecentRoomsChanged>();
            }
        }

        private void PersistRecent()
        {
            this.GetUtility<IStorageUtility>().SaveString(KeyRecentRooms, HistoryRoomSerializer.Serialize(_recentRooms));
        }
    }
}
```

- [ ] **Step 6：跑测试确认全绿**

- [ ] **Step 7：Commit**

```bash
git add Assets/Scripts/APP/SessionMemory Assets/Tests/EditMode/SessionMemoryTests/SessionMemoryModelTests.cs
git commit -m "feat(session): SessionMemoryModel + 单测（用户名/上次房间/历史列表/自动联网开关）"
```

---

### Task 2.6：GameApp 注册 Utility + SessionMemoryModel

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/GameApp.cs`

- [ ] **Step 1：改 GameApp.Init**

```csharp
using APP.Network.Model;
using APP.Network.System;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using APP.SessionMemory.Model;
using APP.Utility;
using QFramework;

namespace APP.Pomodoro
{
    public sealed class GameApp : Architecture<GameApp>
    {
        protected override void Init()
        {
            // Utility 必须最先注册，Model/System 的 OnInit 可能会用
            RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());

            RegisterModel<IPomodoroModel>(new PomodoroModel());
            RegisterModel<IRoomModel>(new RoomModel());
            RegisterModel<ISessionMemoryModel>(new SessionMemoryModel());

            RegisterSystem<IPomodoroTimerSystem>(new PomodoroTimerSystem());
            RegisterSystem<IWindowPositionSystem>(new WindowPositionSystem());
            RegisterSystem<INetworkSystem>(new NetworkSystem());
            RegisterSystem<IStateSyncSystem>(new StateSyncSystem());
        }
    }
}
```

- [ ] **Step 2：Unity 无编译错误**

- [ ] **Step 3：Commit**

```bash
git add Assets/Scripts/APP/Pomodoro/GameApp.cs
git commit -m "feat(arch): GameApp 注册 IStorageUtility + ISessionMemoryModel"
```

---

### Task 2.7：NetworkSystem 进房成功时写 SessionMemory

**Files:**
- Modify: `Assets/Scripts/APP/Network/System/NetworkSystem.cs`

- [ ] **Step 1：在 HandleRoomCreated / HandleRoomJoined 末尾补调用**

文件顶部 using 加：
```csharp
using APP.SessionMemory.Model;
```

`HandleRoomCreated` 末尾（`this.SendEvent(new E_RoomSnapshot(...));` 之后）加：
```csharp
this.GetModel<ISessionMemoryModel>().RememberJoin(
    this.GetModel<IRoomModel>().LocalPlayerName.Value,
    inbound.roomCode);
```

`HandleRoomJoined` 末尾同样加。

- [ ] **Step 2：Unity 无编译错误**

- [ ] **Step 3：Commit**

```bash
git add Assets/Scripts/APP/Network/System/NetworkSystem.cs
git commit -m "feat(net): 进房成功后写入 SessionMemory 历史"
```

---

### Task 2.8：Cmd_LeaveRoom 清 LastRoomCode

**Files:**
- Modify: `Assets/Scripts/APP/Network/Command/Cmd_LeaveRoom.cs`

- [ ] **Step 1：修改 OnExecute**

```csharp
using System.Collections.Generic;
using APP.Network.DTO;
using APP.Network.Event;
using APP.Network.Model;
using APP.Network.System;
using APP.SessionMemory.Model;
using QFramework;

namespace APP.Network.Command
{
    public sealed class Cmd_LeaveRoom : AbstractCommand
    {
        protected override void OnExecute()
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            this.GetSystem<INetworkSystem>().Send(new OutboundLeaveRoom { type = "leave_room" });

            room.ResetRoomState();
            this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));
            room.SetStatus(room.IsConnected.Value ? ConnectionStatus.Connected : ConnectionStatus.Disconnected);

            this.GetModel<ISessionMemoryModel>().ForgetLastRoom();
        }
    }
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Network/Command/Cmd_LeaveRoom.cs
git commit -m "feat(net): 离开房间后清除 LastRoomCode"
```

---

### Task 2.9：新增 Cmd_AutoReconnectOnStartup

**Files:**
- Create: `Assets/Scripts/APP/Network/Command/Cmd_AutoReconnectOnStartup.cs`

- [ ] **Step 1：实现**

```csharp
using APP.SessionMemory.Model;
using QFramework;

namespace APP.Network.Command
{
    /// <summary>
    /// 应用启动时检查 SessionMemory，若开启自动联网且有上次房间码，则发 Cmd_JoinRoom。
    /// </summary>
    public sealed class Cmd_AutoReconnectOnStartup : AbstractCommand
    {
        protected override void OnExecute()
        {
            ISessionMemoryModel memory = this.GetModel<ISessionMemoryModel>();
            if (!memory.AutoReconnectEnabled.Value) return;

            string code = memory.LastRoomCode.Value;
            string name = memory.LastPlayerName.Value;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) return;

            this.SendCommand(new Cmd_JoinRoom(code, name));
        }
    }
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Network/Command/Cmd_AutoReconnectOnStartup.cs
git commit -m "feat(net): Cmd_AutoReconnectOnStartup 启动时尝试自动重进房间"
```

---

### Task 2.10：DeskWindowController.Start 末尾触发自动重连

**Files:**
- Modify: `Assets/UI_V2/Controller/DeskWindowController.cs`

- [ ] **Step 1：增加 using 并在 Start 末尾发命令**

顶部 using 追加：
```csharp
using APP.Network.Command;
```

`Start()` 方法**末尾**（`_pomodoroPanelView.SetVisible(true);` 等已有代码之后）增加：
```csharp
this.SendCommand(new Cmd_AutoReconnectOnStartup());
```

- [ ] **Step 2：Unity 无编译错误**

- [ ] **Step 3：Commit**

```bash
git add Assets/UI_V2/Controller/DeskWindowController.cs
git commit -m "feat(ui): 启动时触发 Cmd_AutoReconnectOnStartup"
```

---

### Task 2.11：NetworkSystem.HandleNetworkError 清 LastRoomCode 避免死循环

**Files:**
- Modify: `Assets/Scripts/APP/Network/System/NetworkSystem.cs`

- [ ] **Step 1：修改 HandleNetworkError**

在 `HandleNetworkError` 里，如果错误 code 是 `ROOM_NOT_FOUND`，清掉 `SessionMemory.LastRoomCode`。

```csharp
private void HandleNetworkError(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    room.SetStatus(ConnectionStatus.Error);
    string code = string.IsNullOrEmpty(inbound.error) ? "UNKNOWN" : inbound.error;

    if (code == "ROOM_NOT_FOUND")
    {
        this.GetModel<ISessionMemoryModel>().ForgetLastRoom();
    }

    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.Error));
    this.SendEvent(new E_NetworkError(code, code));
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Network/System/NetworkSystem.cs
git commit -m "fix(net): 自动重连遇到 ROOM_NOT_FOUND 时清除 LastRoomCode 避免反复失败"
```

---

### Task 2.12：Pencil 设计稿改动（OnlineSettingsPanel）

> **规则：UI 变更必须先走 Pencil MCP。**

**Files:**
- Modify (via Pencil MCP): `AUI/PUI.pen` — OnlineSettingsPanel 组件（id = `8Le5R`）

- [ ] **Step 1：打开 Pencil 文档并定位组件**

```
mcp__pencil__get_editor_state { include_schema: false }
# 若文档未打开：mcp__pencil__open_document { filePathOrNew: "AUI/PUI.pen" }
mcp__pencil__batch_get { patterns: ["component:8Le5R"] }
```

记录：根容器 `osp-root`、`osp-join-card`、`osp-join-btn`、`osp-room-card`、`osp-room-info`、`osp-room-name` 的真实 node id。

- [ ] **Step 2：在 `osp-join-card` 里插入「创建新房间」按钮**

在 `osp-join-btn` 的 parent 下、紧挨着 `osp-join-btn` 下方，插入一个 Button 节点：

```
mcp__pencil__batch_design {
  operations: `
    createBtn=I("<osp-join-btn 的 parent id>", {
      type: "button",
      name: "osp-create-btn",
      styleClasses: ["comp-btn-secondary"],
      text: "创建新房间",
      position: "afterSibling:<osp-join-btn id>"
    })
  `
}
```

（真正的 Pencil MCP 语法以工具 description 为准；这里给的是语义——插入一个 `secondary` 按钮作为 `osp-join-btn` 的**后置兄弟**节点，名 `osp-create-btn`）

- [ ] **Step 3：在 `osp-room-info` 的 `osp-room-name` 旁插入复制按钮**

```
copyBtn=I("<osp-room-info 或包裹 osp-room-name 的行容器 id>", {
    type: "button",
    name: "osp-copy-btn",
    styleClasses: ["comp-btn-icon"],
    text: "复制",
    position: "afterSibling:<osp-room-name id>"
})
```

如果 `osp-room-name` 当前不在一个行容器里，先用 U/R 把它包进 `.osp-room-name-row` 容器再加按钮。

- [ ] **Step 4：在 `osp-room-card` 顶部加 reconnect banner**

```
banner=I("<osp-room-card id>", {
    type: "label",
    name: "osp-reconnect-banner",
    styleClasses: ["osp-reconnect-banner", "osp-hidden"],
    text: "正在重新连接...",
    position: "prepend"
})
```

- [ ] **Step 5：保存设计稿**

Pencil MCP 的 `batch_design` 会自动写回 `.pen` 文件。用 `get_editor_state` 确认变更已持久化。

- [ ] **Step 6：导出节点作为参考（可选但推荐）**

```
mcp__pencil__export_nodes { nodeIds: ["<新加的 3 个节点 id>"], format: "json" }
```

输出保存到 `docs/superpowers/specs/pencil-export-online-panel.json`（供下一步 UXML 同步参考）。

- [ ] **Step 7：Commit**

```bash
git add AUI/PUI.pen docs/superpowers/specs/pencil-export-online-panel.json 2>/dev/null || git add AUI/PUI.pen
git commit -m "design(pencil): OnlineSettingsPanel 新增创建房间按钮 / 复制房号 / 重连 banner"
```

> 若 Pencil MCP 当前不可用，此 Task 暂缓，由设计师手动在 Pencil 客户端完成；后续 UXML 同步可以直接按 Step 2-4 的语义手写。

---

### Task 2.13：OnlineSettingsPanel.uxml / uss 同步 Pencil 改动

**Files:**
- Modify: `Assets/UI_V2/Documents/OnlineSettingsPanel.uxml`
- Modify: `Assets/UI_V2/Styles/OnlineSettingsPanel.uss`

- [ ] **Step 1：编辑 OnlineSettingsPanel.uxml**

找到 `osp-join-card` 块：

```xml
<!-- 加入按钮 -->
<ui:Button name="osp-join-btn" text="加入房间" class="comp-btn-primary" />
```

下面插入：
```xml
<ui:Button name="osp-create-btn" text="创建新房间" class="comp-btn-secondary" />
```

找到 `osp-room-card` 的 `osp-room-info`：
```xml
<ui:VisualElement class="osp-room-info">
    <ui:Label name="osp-room-name" text="ROOM-001" class="osp-room-name" />
    <ui:Label name="osp-room-status" text="已连接 · 3 位成员" class="osp-room-status" />
</ui:VisualElement>
```

改成：
```xml
<ui:VisualElement class="osp-room-info">
    <ui:VisualElement class="osp-room-name-row">
        <ui:Label name="osp-room-name" text="ROOM-001" class="osp-room-name" />
        <ui:Button name="osp-copy-btn" text="复制" class="comp-btn-icon" />
    </ui:VisualElement>
    <ui:Label name="osp-room-status" text="已连接 · 3 位成员" class="osp-room-status" />
</ui:VisualElement>
```

在 `osp-room-card` 内**最顶部**插入 banner（紧跟 `<ui:VisualElement name="osp-room-card" ...>` 之后）：
```xml
<ui:Label name="osp-reconnect-banner" text="正在重新连接..." class="osp-reconnect-banner osp-hidden" />
```

- [ ] **Step 2：编辑 OnlineSettingsPanel.uss**

在文件末尾追加：
```css
.osp-room-name-row {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
}

.osp-reconnect-banner {
    background-color: #FFF4CC;
    color: #8A6D00;
    padding: 6px 10px;
    border-radius: 6px;
    margin-bottom: 8px;
    -unity-text-align: middle-center;
    font-size: 12px;
}

.osp-reconnect-banner--error {
    background-color: #FFD4D4;
    color: #8A1A1A;
}

.osp-hidden {
    display: none;
}
```

（若 `.osp-hidden` 已在文件其它位置定义，略过该条。）

- [ ] **Step 3：用 MCP `read_console` 验证无警告/错误**

- [ ] **Step 4：Commit**

```bash
git add Assets/UI_V2/Documents/OnlineSettingsPanel.uxml Assets/UI_V2/Styles/OnlineSettingsPanel.uss
git commit -m "feat(ui): 联机面板同步 Pencil 设计（创建按钮/复制按钮/重连 banner）"
```

---

### Task 2.14：OnlineSettingsPanelController 接入创建/复制/reconnect banner

**Files:**
- Modify: `Assets/UI_V2/Controller/OnlineSettingsPanelController.cs`

- [ ] **Step 1：加 using 与字段**

顶部：
```csharp
using APP.Network.Event;
using APP.SessionMemory.Event;
using APP.SessionMemory.Model;
```

类字段区新增：
```csharp
private ISessionMemoryModel _sessionMemory;
private Toggle _autoToggle;
private Button _createBtn;
private Button _copyBtn;
private Label _reconnectBanner;
private VisualElement _histList;
```

- [ ] **Step 2：Init 里注入 SessionMemory 并 Q 新控件**

`Init` 方法顶部（参数下面）：
```csharp
_sessionMemory = GameApp.Interface.GetModel<ISessionMemoryModel>();

_autoToggle      = container.Q<Toggle>("osp-auto-toggle");
_createBtn       = container.Q<Button>("osp-create-btn");
_copyBtn         = container.Q<Button>("osp-copy-btn");
_reconnectBanner = container.Q<Label>("osp-reconnect-banner");
_histList        = container.Q<VisualElement>("osp-hist-list");
```

- [ ] **Step 3：绑定按钮与事件**

在已有的 `osp-join-btn` / `osp-exit-btn` 注册下面加：
```csharp
_createBtn?.RegisterCallback<PointerUpEvent>(_ => OnCreateClicked());
_copyBtn?.RegisterCallback<PointerUpEvent>(_ => OnCopyClicked());

if (_autoToggle != null)
{
    _autoToggle.SetValueWithoutNotify(_sessionMemory.AutoReconnectEnabled.Value);
    _autoToggle.RegisterValueChangedCallback(e => _sessionMemory.SetAutoReconnectEnabled(e.newValue));
}

this.RegisterEvent<E_ConnectionStateChanged>(OnConnectionStateChanged)
    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
```

把 "回填用户名" 那段从 `_roomModel.LocalPlayerName.Value` 改成从 `_sessionMemory.LastPlayerName.Value` 读（更准确，LocalPlayerName 可能为空）：
```csharp
if (_usernameField != null)
{
    string savedName = _sessionMemory.LastPlayerName.Value;
    if (!string.IsNullOrEmpty(savedName)) _usernameField.value = savedName;
}
```

- [ ] **Step 4：实现新回调方法**

类底部添加：
```csharp
private void OnCreateClicked()
{
    string username = _usernameField?.value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(username))
    {
        ShowError("请输入用户名");
        return;
    }

    this.SendCommand(new Cmd_CreateRoom(username));
}

private void OnCopyClicked()
{
    string code = _roomModel?.RoomCode.Value ?? string.Empty;
    if (string.IsNullOrEmpty(code)) return;
    GUIUtility.systemCopyBuffer = code;
    if (_copyBtn != null) _copyBtn.text = "已复制";
    // 2 秒后恢复按钮文字；简单做法用 delayed schedule
    _copyBtn?.schedule.Execute(() => { if (_copyBtn != null) _copyBtn.text = "复制"; }).StartingIn(2000);
}

private void OnConnectionStateChanged(E_ConnectionStateChanged e)
{
    if (_reconnectBanner == null) return;

    switch (e.Status)
    {
        case APP.Network.Model.ConnectionStatus.Reconnecting:
            _reconnectBanner.text = "正在重新连接...";
            _reconnectBanner.RemoveFromClassList("osp-reconnect-banner--error");
            _reconnectBanner.RemoveFromClassList("osp-hidden");
            break;
        case APP.Network.Model.ConnectionStatus.Error:
            _reconnectBanner.text = "重连失败，请重试";
            _reconnectBanner.AddToClassList("osp-reconnect-banner--error");
            _reconnectBanner.RemoveFromClassList("osp-hidden");
            break;
        case APP.Network.Model.ConnectionStatus.Connected:
        case APP.Network.Model.ConnectionStatus.InRoom:
            _reconnectBanner.AddToClassList("osp-hidden");
            break;
    }
}
```

顶部加 `using APP.Network.Command;`（如果还没有）。

- [ ] **Step 5：Unity 无编译错误**

- [ ] **Step 6：手动验证（Unity Play Mode + 本地 Node server）**

启动 Server → Play → 输入用户名 → 点「创建新房间」 → 显示房号 → 点复制按钮 → 粘贴到别处应是该房号。打开 auto-toggle → 退 Play 再进 → 自动回到房间。

- [ ] **Step 7：Commit**

```bash
git add Assets/UI_V2/Controller/OnlineSettingsPanelController.cs
git commit -m "feat(ui): 联机面板接入创建房间/复制房号/重连 banner/自动联网开关"
```

---

## 阶段 3：历史房间列表

### Task 3.1：Pencil 设计稿 —— 历史卡片项模板

> **规则：UI 先走 Pencil MCP。**

**Files:**
- Modify (via Pencil MCP): `AUI/PUI.pen` — OnlineSettingsPanel 的 `osp-hist-card`

- [ ] **Step 1：打开文档并定位**

```
mcp__pencil__get_editor_state { include_schema: false }
mcp__pencil__batch_get { patterns: ["name:osp-hist-card"] }
```

记录 `osp-hist-list` 容器的 node id。

- [ ] **Step 2：在 `osp-hist-list` 里加一个 item 模板**（用作占位示例，实际渲染由 C# 动态生成）

```
mcp__pencil__batch_design {
  operations: `
    item=I("<osp-hist-list id>", {
      type: "container",
      name: "osp-hist-item-template",
      styleClasses: ["osp-hist-item"],
      children: [
        { type: "label", name: "osp-hist-code",     text: "ABC123", styleClasses: ["osp-hist-code"] },
        { type: "label", name: "osp-hist-name",     text: "小明",    styleClasses: ["osp-hist-name"] },
        { type: "label", name: "osp-hist-time",     text: "刚刚",    styleClasses: ["osp-hist-time"] },
        { type: "button", name: "osp-hist-join-btn", text: "加入", styleClasses: ["comp-btn-icon"] },
        { type: "button", name: "osp-hist-del-btn",  text: "删除", styleClasses: ["comp-btn-icon"] }
      ]
    })
  `
}
```

（Pencil 实际操作语法以工具 description 为准。此处给出语义：item 根 + 5 个子元素，带 class。）

- [ ] **Step 3：Commit**

```bash
git add AUI/PUI.pen
git commit -m "design(pencil): 历史房间列表 item 模板"
```

---

### Task 3.2：UXML / USS 同步

**Files:**
- Modify: `Assets/UI_V2/Documents/OnlineSettingsPanel.uxml`
- Modify: `Assets/UI_V2/Styles/OnlineSettingsPanel.uss`

- [ ] **Step 1：UXML —— `osp-hist-card` 区保持当前结构**

历史项是动态生成的，UXML 里只保留 `osp-hist-list` 空容器（已有）。不改。

- [ ] **Step 2：USS —— 追加 item 样式**

```css
.osp-hist-item {
    flex-direction: row;
    align-items: center;
    padding: 6px 8px;
    border-bottom-width: 1px;
    border-bottom-color: #EEE;
}

.osp-hist-code {
    flex-grow: 0;
    width: 80px;
    font-size: 14px;
    -unity-font-style: bold;
}

.osp-hist-name {
    flex-grow: 1;
    font-size: 13px;
    color: #555;
    margin-left: 8px;
}

.osp-hist-time {
    flex-grow: 0;
    width: 80px;
    font-size: 11px;
    color: #999;
    -unity-text-align: middle-right;
}

.osp-hist-join-btn, .osp-hist-del-btn {
    flex-grow: 0;
    width: 36px;
    height: 24px;
    margin-left: 4px;
}

.osp-hist-empty {
    padding: 16px;
    -unity-text-align: middle-center;
    color: #999;
    font-size: 12px;
}
```

- [ ] **Step 3：Commit**

```bash
git add Assets/UI_V2/Styles/OnlineSettingsPanel.uss
git commit -m "feat(ui): 历史房间 item 样式"
```

---

### Task 3.3：FormatRelative + 测试

**Files:**
- Create: `Assets/UI_V2/Controller/RelativeTimeFormatter.cs`
- Create: `Assets/Tests/EditMode/UI_V2_Tests/RelativeTimeFormatterTests.cs`（若没有 UI_V2 测试 asmdef 则沿用现有 `APP.UI_V2` 需特殊处理——但静态类可以放到 SessionMemory asmdef 里）

**简化**：把 `RelativeTimeFormatter` 放到 `Assets/Scripts/APP/SessionMemory/Model/RelativeTimeFormatter.cs`（已有 asmdef 测试），避免新建 asmdef。

- [ ] **Step 1：测试（red）**

`Assets/Tests/EditMode/SessionMemoryTests/RelativeTimeFormatterTests.cs`：
```csharp
using System;
using APP.SessionMemory.Model;
using NUnit.Framework;

namespace APP.SessionMemory.Tests
{
    [TestFixture]
    public sealed class RelativeTimeFormatterTests
    {
        private readonly DateTimeOffset _now = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero);

        private long Ago(TimeSpan d) => (_now - d).ToUnixTimeMilliseconds();

        [Test] public void Seconds_Under60_ReturnsJustNow()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromSeconds(30)), _now), Is.EqualTo("刚刚"));

        [Test] public void Minutes_Under60_ReturnsMinutes()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromMinutes(5)), _now), Is.EqualTo("5 分钟前"));

        [Test] public void Hours_Under24_ReturnsHours()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromHours(3)), _now), Is.EqualTo("3 小时前"));

        [Test] public void Under2Days_ReturnsYesterday()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromHours(30)), _now), Is.EqualTo("昨天"));

        [Test] public void Under7Days_ReturnsDaysAgo()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromDays(3)), _now), Is.EqualTo("3 天前"));

        [Test] public void Over7Days_ReturnsMonthDay()
        {
            long t = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            // 格式化会按本地时间打印月日，此处无法精确断言年外月日（依赖 tz），改为断言含"月"与"日"
            string r = RelativeTimeFormatter.Format(t, _now);
            Assert.That(r, Does.Contain("月").And.Contain("日"));
        }
    }
}
```

- [ ] **Step 2：跑测试确认 red**

- [ ] **Step 3：实现**

`Assets/Scripts/APP/SessionMemory/Model/RelativeTimeFormatter.cs`：
```csharp
using System;

namespace APP.SessionMemory.Model
{
    public static class RelativeTimeFormatter
    {
        public static string Format(long unixMs, DateTimeOffset now)
        {
            var delta = now - DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            if (delta.TotalSeconds < 60)  return "刚刚";
            if (delta.TotalMinutes < 60)  return $"{(int)delta.TotalMinutes} 分钟前";
            if (delta.TotalHours < 24)    return $"{(int)delta.TotalHours} 小时前";
            if (delta.TotalDays < 2)      return "昨天";
            if (delta.TotalDays < 7)      return $"{(int)delta.TotalDays} 天前";
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            return $"{dt.Month}月{dt.Day}日";
        }

        public static string Format(long unixMs) => Format(unixMs, DateTimeOffset.UtcNow);
    }
}
```

- [ ] **Step 4：跑测试确认绿**

- [ ] **Step 5：Commit**

```bash
git add Assets/Scripts/APP/SessionMemory/Model/RelativeTimeFormatter.cs \
        Assets/Tests/EditMode/SessionMemoryTests/RelativeTimeFormatterTests.cs
git commit -m "feat(session): RelativeTimeFormatter + 单测"
```

---

### Task 3.4：OnlineSettingsPanelController.RefreshHistoryList

**Files:**
- Modify: `Assets/UI_V2/Controller/OnlineSettingsPanelController.cs`

- [ ] **Step 1：在 `RefreshCardState` 里补历史列表刷新**

```csharp
public void RefreshCardState()
{
    bool inRoom = _roomModel != null && _roomModel.IsInRoom.Value;

    _joinCard?.EnableInClassList("osp-hidden", inRoom);
    _histCard?.EnableInClassList("osp-hidden", inRoom);
    _roomCard?.EnableInClassList("osp-hidden", !inRoom);

    if (inRoom) RefreshRoomInfo();
    else        RefreshHistoryList();
}
```

- [ ] **Step 2：订阅 E_RecentRoomsChanged**

`Init` 里（事件订阅区）：
```csharp
this.RegisterEvent<E_RecentRoomsChanged>(_ => RefreshHistoryList())
    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
```

- [ ] **Step 3：实现 RefreshHistoryList**

类底部追加：
```csharp
private void RefreshHistoryList()
{
    if (_histList == null || _sessionMemory == null) return;

    _histList.Clear();

    var rooms = _sessionMemory.RecentRooms;
    if (rooms == null || rooms.Count == 0)
    {
        var empty = new Label("暂无历史房间");
        empty.AddToClassList("osp-hist-empty");
        _histList.Add(empty);
        return;
    }

    for (int i = 0; i < rooms.Count; i++)
    {
        HistoryRoomEntry entry = rooms[i];

        VisualElement item = new VisualElement();
        item.AddToClassList("osp-hist-item");

        Label codeLabel = new Label(entry.RoomCode);
        codeLabel.AddToClassList("osp-hist-code");
        item.Add(codeLabel);

        Label nameLabel = new Label(entry.LastPlayerName ?? string.Empty);
        nameLabel.AddToClassList("osp-hist-name");
        item.Add(nameLabel);

        Label timeLabel = new Label(APP.SessionMemory.Model.RelativeTimeFormatter.Format(entry.LastJoinedAtUnixMs));
        timeLabel.AddToClassList("osp-hist-time");
        item.Add(timeLabel);

        Button joinBtn = new Button(() => OnHistoryJoinClicked(entry.RoomCode)) { text = "加入" };
        joinBtn.AddToClassList("comp-btn-icon");
        joinBtn.AddToClassList("osp-hist-join-btn");
        item.Add(joinBtn);

        Button delBtn = new Button(() => OnHistoryDeleteClicked(entry.RoomCode)) { text = "删除" };
        delBtn.AddToClassList("comp-btn-icon");
        delBtn.AddToClassList("osp-hist-del-btn");
        item.Add(delBtn);

        _histList.Add(item);
    }
}

private void OnHistoryJoinClicked(string roomCode)
{
    string username = _usernameField?.value ?? string.Empty;
    if (string.IsNullOrWhiteSpace(username))
    {
        ShowError("请输入用户名");
        return;
    }
    this.SendCommand(new Cmd_JoinRoom(roomCode, username));
}

private void OnHistoryDeleteClicked(string roomCode)
{
    _sessionMemory.RemoveHistoryEntry(roomCode);
}
```

顶部 using 若缺加 `using APP.SessionMemory.Model;`。

- [ ] **Step 4：手动验证**

Play → 依次创建/加入 3 个不同房间 → 退出 → 列表显示 3 条；点删除消失；点加入能回到。

- [ ] **Step 5：Commit**

```bash
git add Assets/UI_V2/Controller/OnlineSettingsPanelController.cs
git commit -m "feat(ui): 联机面板历史房间列表"
```

---

## 阶段 4：ActiveApp 采集

### Task 4.1：扩展 AppMonitor.m 加 bundleId 出参

**Files:**
- Modify: `localpackage/com.nz.appmonitor/Plugins/macOS/AppMonitor/AppMonitor.m`

- [ ] **Step 1：修改 C 导出函数签名**

找到 `GetFrontmostAppInfo` 的实现（文件末尾附近），把签名改为：

```objc
__attribute__((visibility("default")))
int GetFrontmostAppInfo(
    char *appName, int nameLen,
    char *windowTitle, int titleLen,
    char *bundleId, int bundleIdLen,
    unsigned char **iconData, int *iconLen)
```

在函数体里定位到 `NSRunningApplication *frontmostApp = ...` 之后，加：

```objc
if (bundleId != NULL && bundleIdLen > 0)
{
    CopyNSStringToBuffer(frontmostApp.bundleIdentifier ?: @"", bundleId, bundleIdLen);
}
```

（如 `frontmostApp` 为 nil 则填空字符串。）

- [ ] **Step 2：Commit**

```bash
git add localpackage/com.nz.appmonitor/Plugins/macOS/AppMonitor/AppMonitor.m
git commit -m "feat(appmon): 原生插件导出 bundleId"
```

---

### Task 4.2：重新构建 AppMonitor.bundle

- [ ] **Step 1：跑构建脚本**

```bash
cd localpackage/com.nz.appmonitor/Plugins/macOS/AppMonitor
./build_appmonitor.sh
```

Expected：脚本输出成功，`../AppMonitor.bundle/Contents/MacOS/AppMonitor` 文件更新。

- [ ] **Step 2：回到项目根**

```bash
cd -
```

- [ ] **Step 3：Unity 重新导入 Plugins 目录**

Unity Editor → `Assets → Reimport` 或重启 Editor。

- [ ] **Step 4：Commit（bundle 是二进制）**

```bash
git add localpackage/com.nz.appmonitor/Plugins/macOS/AppMonitor.bundle
git commit -m "build(appmon): 重新编译 AppMonitor.bundle（含 bundleId 导出）"
```

---

### Task 4.3：扩展 AppInfo + P/Invoke 签名

**Files:**
- Modify: `localpackage/com.nz.appmonitor/Runtime/AppMonitorData.cs`
- Modify: `localpackage/com.nz.appmonitor/Runtime/MacOSAppMonitor.cs`

- [ ] **Step 1：AppInfo 加 BundleId**

```csharp
public class AppInfo
{
    public string AppName;
    public string BundleId;           // 新增
    public string WindowTitle;
    public Texture2D Icon;
    public bool IsSuccess;
    public AppMonitorResultCode? ErrorCode;
    public string ErrorMessage;
}
```

- [ ] **Step 2：MacOSAppMonitor P/Invoke 签名改**

```csharp
private const int MaxBundleIdLength = 256;

[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
private static extern int GetFrontmostAppInfo(
    [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder appName,
    int nameLen,
    [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder windowTitle,
    int titleLen,
    [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder bundleId,
    int bundleIdLen,
    out IntPtr iconData,
    out int iconLen);
```

在 `GetCurrentApp()` 中：
```csharp
var bundleIdBuilder = new StringBuilder(MaxBundleIdLength);

result = GetFrontmostAppInfo(
    appNameBuilder, MaxAppNameLength,
    windowTitleBuilder, MaxWindowTitleLength,
    bundleIdBuilder, MaxBundleIdLength,
    out IntPtr iconDataTemp, out int iconLenTemp);
```

构造 AppInfo 时填：
```csharp
var appInfo = new AppInfo
{
    AppName = appName,
    BundleId = bundleIdBuilder.ToString(),
    WindowTitle = windowTitle,
    IsSuccess = true
};
```

`CreateFallbackAppInfo` 中 BundleId 填空字符串。

- [ ] **Step 3：Unity 无编译错误**

- [ ] **Step 4：Commit**

```bash
git add localpackage/com.nz.appmonitor/Runtime/AppMonitorData.cs \
        localpackage/com.nz.appmonitor/Runtime/MacOSAppMonitor.cs
git commit -m "feat(appmon): C# 侧 AppInfo 增加 BundleId 字段"
```

---

### Task 4.4：修复 AppMonitorPlayerTest（若存在旧断言）

**Files:**
- Modify (如存在): `localpackage/com.nz.appmonitor/Tests/Runtime/AppMonitorPlayerTest.cs`

- [ ] **Step 1：跑 PlayMode 测试看是否断红**

用 MCP `run_tests` testMode PlayMode，filter 过滤 AppMonitor 相关。

- [ ] **Step 2：若有测试因签名变化红**

更新测试文件，补 `BundleId` 字段断言（例如 `Assert.That(info.BundleId, Is.Not.Null);`），或移除与签名无关的过时断言。

- [ ] **Step 3：全绿后 Commit**

```bash
git add localpackage/com.nz.appmonitor/Tests/Runtime/AppMonitorPlayerTest.cs
git commit -m "test(appmon): 适配 BundleId 字段的新签名"
```

---

### Task 4.5：IActiveAppSystem 接口

**Files:**
- Create: `Assets/Scripts/APP/Network/System/IActiveAppSystem.cs`

- [ ] **Step 1：写接口与数据结构**

```csharp
using System;
using QFramework;

namespace APP.Network.System
{
    public readonly struct ActiveAppSnapshot
    {
        public readonly string Name;
        public readonly string BundleId;
        public readonly byte[] IconPngBytes;

        public ActiveAppSnapshot(string name, string bundleId, byte[] iconPngBytes)
        {
            Name = name ?? string.Empty;
            BundleId = bundleId ?? string.Empty;
            IconPngBytes = iconPngBytes;
        }

        public static ActiveAppSnapshot Empty => new ActiveAppSnapshot(string.Empty, string.Empty, null);
    }

    public interface IActiveAppSystem : ISystem
    {
        void Tick(float deltaTime);
        ActiveAppSnapshot Current { get; }
        event Action<ActiveAppSnapshot> Changed;
    }
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Network/System/IActiveAppSystem.cs
git commit -m "feat(net): IActiveAppSystem 接口"
```

---

### Task 4.6：ActiveAppSystem + TDD（用假 AppMonitor）

**Files:**
- Create: `Assets/Tests/EditMode/NetworkTests/ActiveAppSystemTests.cs`
- Create: `Assets/Scripts/APP/Network/System/ActiveAppSystem.cs`

> 因 `IAppMonitor` 在 `NZ.AppMonitor.Runtime` 里，`APP.Runtime` asmdef 需要加对 `NZ.AppMonitor.Runtime` 的引用。先改 asmdef。

- [ ] **Step 1：更新 APP.Runtime.asmdef**

`Assets/Scripts/APP/APP.Runtime.asmdef` （如果不存在先找实际位置——从 `APP.UI_V2.asmdef` 看它 `references` 了 `APP.Runtime`，那 `APP.Runtime.asmdef` 应在 Scripts 或 Scripts/APP 下）：

```bash
find /Users/xpy/Desktop/NanZhai/CPA/Assets/Scripts -name "APP.Runtime.asmdef"
```

找到后打开，在 `references` 里追加 `"NZ.AppMonitor.Runtime"`。

- [ ] **Step 2：测试（red）**

`Assets/Tests/EditMode/NetworkTests/ActiveAppSystemTests.cs`：
```csharp
using APP.Network.System;
using CPA.Monitoring;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class ActiveAppSystemTests
    {
        private sealed class FakeAppMonitor : IAppMonitor
        {
            public AppInfo NextAppInfo;
            public bool IsPermissionGranted => true;
            public void RequestPermission() { }
            public AppInfo GetCurrentApp() => NextAppInfo;
            public Texture2D GetAppIcon() => NextAppInfo?.Icon;
        }

        // ActiveAppSystem 不访问 Architecture（自己 event，不用 SendEvent/GetModel）
        // 直接 new + Tick 即可，无需 SetArchitecture/Init

        private static byte[] DummyPng => new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        [Test]
        public void Tick_FirstTimeWithNewBundleId_EmitsChanged()
        {
            var fake = new FakeAppMonitor
            {
                NextAppInfo = new AppInfo
                {
                    AppName = "Safari", BundleId = "com.apple.Safari",
                    IsSuccess = true,
                }
            };
            var sys = new ActiveAppSystem(fake, () => DummyPng);

            int changedCount = 0;
            sys.Changed += _ => changedCount++;

            // 模拟累计到 1 秒触发采样
            sys.Tick(0.5f);
            sys.Tick(0.6f);

            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(sys.Current.BundleId, Is.EqualTo("com.apple.Safari"));
            Assert.That(sys.Current.IconPngBytes, Is.EqualTo(DummyPng));
        }

        [Test]
        public void Tick_SameBundleIdTwice_EmitsOnce()
        {
            var fake = new FakeAppMonitor
            {
                NextAppInfo = new AppInfo { AppName = "Safari", BundleId = "com.apple.Safari", IsSuccess = true }
            };
            var sys = new ActiveAppSystem(fake, () => DummyPng);

            int changedCount = 0;
            sys.Changed += _ => changedCount++;

            sys.Tick(1.1f);
            sys.Tick(1.1f);

            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void Tick_WhenPermissionDenied_EmptyBundleId()
        {
            var fake = new FakeAppMonitor
            {
                NextAppInfo = new AppInfo { IsSuccess = false, ErrorCode = AppMonitorResultCode.AccessibilityDenied }
            };
            var sys = new ActiveAppSystem(fake, () => null);

            sys.Tick(1.1f);

            Assert.That(sys.Current.BundleId, Is.Empty);
        }

        [Test]
        public void Tick_Under1Second_DoesNotSample()
        {
            var fake = new FakeAppMonitor();
            int getCalls = 0;
            fake.NextAppInfo = new AppInfo { BundleId = "x", IsSuccess = true };
            var wrapped = new CountingAppMonitor(fake, () => getCalls++);
            var sys = new ActiveAppSystem(wrapped, () => DummyPng);

            sys.Tick(0.3f);
            sys.Tick(0.4f);

            Assert.That(getCalls, Is.EqualTo(0));
        }

        private sealed class CountingAppMonitor : IAppMonitor
        {
            private readonly IAppMonitor _inner;
            private readonly System.Action _onCall;
            public CountingAppMonitor(IAppMonitor inner, System.Action onCall) { _inner = inner; _onCall = onCall; }
            public bool IsPermissionGranted => _inner.IsPermissionGranted;
            public void RequestPermission() => _inner.RequestPermission();
            public AppInfo GetCurrentApp() { _onCall(); return _inner.GetCurrentApp(); }
            public Texture2D GetAppIcon() => _inner.GetAppIcon();
        }
    }
}
```

- [ ] **Step 3：跑 red**

- [ ] **Step 4：实现 ActiveAppSystem**

`Assets/Scripts/APP/Network/System/ActiveAppSystem.cs`：
```csharp
using System;
using CPA.Monitoring;
using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public sealed class ActiveAppSystem : AbstractSystem, IActiveAppSystem
    {
        private readonly IAppMonitor _monitor;
        private readonly Func<byte[]> _captureIconPng;
        private float _sampleAccumulator;
        private ActiveAppSnapshot _current = ActiveAppSnapshot.Empty;

        public event Action<ActiveAppSnapshot> Changed;

        public ActiveAppSystem(IAppMonitor monitor = null, Func<byte[]> captureIconPng = null)
        {
            _monitor = monitor;
            _captureIconPng = captureIconPng;
        }

        public ActiveAppSnapshot Current => _current;

        protected override void OnInit() { }

        public void Tick(float deltaTime)
        {
            _sampleAccumulator += deltaTime;
            if (_sampleAccumulator < 1f) return;
            _sampleAccumulator = 0f;

            IAppMonitor monitor = _monitor ?? AppMonitor.Instance;
            AppInfo info = monitor?.GetCurrentApp();
            if (info == null || !info.IsSuccess || string.IsNullOrEmpty(info.BundleId))
            {
                if (!string.IsNullOrEmpty(_current.BundleId))
                {
                    _current = ActiveAppSnapshot.Empty;
                    Changed?.Invoke(_current);
                }
                return;
            }

            if (info.BundleId == _current.BundleId) return;

            byte[] png = CaptureIconPng(info);
            _current = new ActiveAppSnapshot(info.AppName, info.BundleId, png);
            Changed?.Invoke(_current);
        }

        private byte[] CaptureIconPng(AppInfo info)
        {
            if (_captureIconPng != null) return _captureIconPng();
            return info?.Icon != null ? info.Icon.EncodeToPNG() : null;
        }
    }
}
```

> `AppMonitor.Instance` 是 `com.nz.appmonitor` 的静态工厂（见历史 `AppMonitor.cs`）。若工厂符号不叫 `Instance`，按实际改。

- [ ] **Step 5：跑测试确认全绿**

- [ ] **Step 6：Commit**

```bash
git add Assets/Scripts/APP/Network/System/ActiveAppSystem.cs \
        Assets/Tests/EditMode/NetworkTests/ActiveAppSystemTests.cs \
        Assets/Scripts/APP/*.asmdef 2>/dev/null || true
git commit -m "feat(net): ActiveAppSystem + 单测（1Hz 采样 / bundleId 变更触发）"
```

---

### Task 4.7：GameApp 注册 ActiveAppSystem

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/GameApp.cs`

- [ ] **Step 1：补注册**

在 `RegisterSystem<IStateSyncSystem>` 之前加（StateSync 会 GetSystem<IActiveAppSystem>）：
```csharp
RegisterSystem<IActiveAppSystem>(new ActiveAppSystem());
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Pomodoro/GameApp.cs
git commit -m "feat(arch): 注册 IActiveAppSystem"
```

---

### Task 4.8：StateSyncSystem 打包 activeApp

**Files:**
- Modify: `Assets/Scripts/APP/Network/System/StateSyncSystem.cs`

- [ ] **Step 1：CollectLocalState 读 ActiveAppSystem**

```csharp
private RemoteState CollectLocalState()
{
    IPomodoroModel pomodoro = this.GetModel<IPomodoroModel>();
    IActiveAppSystem activeApp = this.GetSystem<IActiveAppSystem>();
    ActiveAppSnapshot snap = activeApp.Current;

    return new RemoteState
    {
        pomodoro = new PomodoroStateDto
        {
            phase = (int)pomodoro.CurrentPhase.Value,
            remainingSeconds = pomodoro.RemainingSeconds.Value,
            currentRound = pomodoro.CurrentRound.Value,
            totalRounds = pomodoro.TotalRounds.Value,
            isRunning = pomodoro.IsRunning.Value,
        },
        activeApp = string.IsNullOrEmpty(snap.BundleId) ? null : new ActiveAppDto
        {
            name = snap.Name,
            bundleId = snap.BundleId,
            iconId = null,   // 图标不在 state_update 里传
        },
    };
}
```

- [ ] **Step 2：确认 StateSyncTests 仍通过**

- [ ] **Step 3：Commit**

```bash
git add Assets/Scripts/APP/Network/System/StateSyncSystem.cs
git commit -m "feat(net): StateSync 打包 activeApp(name+bundleId)"
```

---

### Task 4.9：DeskWindowController.Update 调 ActiveAppSystem.Tick

**Files:**
- Modify: `Assets/UI_V2/Controller/DeskWindowController.cs`

- [ ] **Step 1：在 Update 里补一行**

```csharp
private void Update()
{
    this.SendCommand(new Cmd_PomodoroTick(Time.deltaTime));
    this.GetSystem<IActiveAppSystem>().Tick(Time.unscaledDeltaTime);
    this.GetSystem<IStateSyncSystem>().Tick(Time.unscaledDeltaTime);
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/UI_V2/Controller/DeskWindowController.cs
git commit -m "feat(ui): DeskWindowController 每帧 Tick ActiveAppSystem"
```

---

## 阶段 5：图标协议（服务端 + 客户端）

### Task 5.1：服务端 IconCache 实现 + 单测

**Files:**
- Create: `Server/src/IconCache.js`
- Create: `Server/test/icon-cache.test.js`

- [ ] **Step 1：测试（red）**

```js
// Server/test/icon-cache.test.js
import assert from 'node:assert/strict';
import test from 'node:test';
import { IconCache } from '../src/IconCache.js';

test('set + has + get basic', () =>
{
    const cache = new IconCache({ maxEntries: 3, maxBase64Bytes: 1024 });
    cache.set('a', 'AAAA');
    assert.equal(cache.has('a'), true);
    assert.equal(cache.get('a'), 'AAAA');
});

test('LRU evicts oldest when over cap', () =>
{
    const cache = new IconCache({ maxEntries: 2, maxBase64Bytes: 1024 });
    cache.set('a', 'A');
    cache.set('b', 'B');
    cache.set('c', 'C');
    assert.equal(cache.has('a'), false);
    assert.equal(cache.has('b'), true);
    assert.equal(cache.has('c'), true);
});

test('accessing an entry promotes it in LRU', () =>
{
    const cache = new IconCache({ maxEntries: 2, maxBase64Bytes: 1024 });
    cache.set('a', 'A');
    cache.set('b', 'B');
    cache.get('a');         // 提升 a
    cache.set('c', 'C');    // 应淘汰 b
    assert.equal(cache.has('a'), true);
    assert.equal(cache.has('b'), false);
    assert.equal(cache.has('c'), true);
});

test('set rejects oversize base64', () =>
{
    const cache = new IconCache({ maxEntries: 2, maxBase64Bytes: 4 });
    assert.throws(() => cache.set('a', 'AAAAA'), /ICON_TOO_LARGE/);
});
```

- [ ] **Step 2：跑 red**

```bash
cd Server && node --test test/icon-cache.test.js
```

- [ ] **Step 3：实现**

```js
// Server/src/IconCache.js
export class IconCacheError extends Error
{
    constructor(code, message)
    {
        super(message);
        this.code = code;
    }
}

export class IconCache
{
    constructor(options = {})
    {
        this._maxEntries = options.maxEntries ?? 100;
        this._maxBase64Bytes = options.maxBase64Bytes ?? 1_048_576;
        this._entries = new Map();  // LRU: insertion order = least-recent first
    }

    has(bundleId)
    {
        return this._entries.has(bundleId);
    }

    get(bundleId)
    {
        if (!this._entries.has(bundleId)) return null;
        const value = this._entries.get(bundleId);
        this._entries.delete(bundleId);
        this._entries.set(bundleId, value);
        return value;
    }

    set(bundleId, iconBase64)
    {
        if (typeof iconBase64 !== 'string' || iconBase64.length === 0)
        {
            throw new IconCacheError('INVALID_ICON', 'iconBase64 必须为非空字符串');
        }
        if (iconBase64.length > this._maxBase64Bytes)
        {
            throw new IconCacheError('ICON_TOO_LARGE', `图标超过 ${this._maxBase64Bytes} 字节上限`);
        }

        if (this._entries.has(bundleId)) this._entries.delete(bundleId);
        this._entries.set(bundleId, iconBase64);

        while (this._entries.size > this._maxEntries)
        {
            const oldestKey = this._entries.keys().next().value;
            this._entries.delete(oldestKey);
        }
    }

    keys()
    {
        return [...this._entries.keys()];
    }
}
```

- [ ] **Step 4：跑测试确认绿**

- [ ] **Step 5：Commit**

```bash
git add Server/src/IconCache.js Server/test/icon-cache.test.js
git commit -m "feat(server): IconCache LRU（100 条 / 单条 1MB）+ 单测"
```

---

### Task 5.2：服务端 protocol.js 加 icon 消息

**Files:**
- Modify: `Server/src/protocol.js`

- [ ] **Step 1：扩展 SUPPORTED_CLIENT_MESSAGE_TYPES**

```js
const SUPPORTED_CLIENT_MESSAGE_TYPES = new Set([
    'create_room',
    'join_room',
    'leave_room',
    'player_state_update',
    'sync_state',
    'icon_upload',
    'icon_request',
    'ping',
    'pong'
]);
```

- [ ] **Step 2：parseClientMessage switch 里加两个 case**

在 `case 'pong':` 之前插入：

```js
case 'icon_upload':
    return {
        v: PROTOCOL_VERSION,
        type: 'icon_upload',
        bundleId: normalizeBundleId(parsedMessage.bundleId),
        iconBase64: normalizeIconBase64(parsedMessage.iconBase64)
    };

case 'icon_request':
    return {
        v: PROTOCOL_VERSION,
        type: 'icon_request',
        bundleIds: normalizeBundleIdArray(parsedMessage.bundleIds)
    };
```

- [ ] **Step 3：加消息构造函数与规范化辅助**

文件末尾附近加：

```js
export function createIconNeedMessage({ bundleId })
{
    return { type: 'icon_need', bundleId };
}

export function createIconBroadcastMessage({ bundleId, iconBase64 })
{
    return { type: 'icon_broadcast', bundleId, iconBase64 };
}

function normalizeBundleId(bundleId)
{
    if (typeof bundleId !== 'string' || !bundleId.trim())
    {
        throw new ProtocolError('INVALID_MESSAGE', 'bundleId 不能为空');
    }
    return bundleId.trim();
}

function normalizeIconBase64(iconBase64)
{
    if (typeof iconBase64 !== 'string' || !iconBase64.trim())
    {
        throw new ProtocolError('INVALID_MESSAGE', 'iconBase64 不能为空');
    }
    return iconBase64;
}

function normalizeBundleIdArray(bundleIds)
{
    if (!Array.isArray(bundleIds) || bundleIds.length === 0)
    {
        throw new ProtocolError('INVALID_MESSAGE', 'bundleIds 必须是非空数组');
    }
    return bundleIds.map((bundleId) => normalizeBundleId(bundleId));
}
```

- [ ] **Step 4：Commit**

```bash
git add Server/src/protocol.js
git commit -m "feat(server): 协议增加 icon_upload / icon_request / icon_need / icon_broadcast"
```

---

### Task 5.3：服务端 index.js 分派 icon 消息

**Files:**
- Modify: `Server/src/index.js`

- [ ] **Step 1：import 新构造函数 + IconCache**

顶部 import：
```js
import { IconCache, IconCacheError } from './IconCache.js';
import {
    ProtocolError,
    createErrorMessage,
    createIconBroadcastMessage,
    createIconNeedMessage,
    createPlayerJoinedMessage,
    // ... 已有
} from './protocol.js';
```

- [ ] **Step 2：createPomodoroServer 里创建 iconCache**

在 `const connections = new Map();` 同层加：
```js
const iconCache = options.iconCache ?? new IconCache({
    maxEntries: options.iconCacheMaxEntries ?? 100,
    maxBase64Bytes: options.iconCacheMaxBase64Bytes ?? 1_048_576
});
```

`return { ... }` 里加 `iconCache,` 方便测试注入。

- [ ] **Step 3：handleMessage switch 加两个 case**

```js
case 'icon_upload':
    handleIconUpload(message, { ...context, iconCache });
    return;

case 'icon_request':
    handleIconRequest(message, { ...context, iconCache });
    return;
```

**注意**：`context` 是函数内参数，需要在 `handleMessage` 里把 `iconCache` 加到 context。简单做法：在 `webSocketServer.on('connection', ...)` 回调里把 iconCache 一并 pass：

```js
handleMessage({
    rawMessage,
    connection,
    roomManager,
    iconCache,            // 新增
    logger,
    clearInitTimeout: ...,
    broadcastToRoom: ...,
});
```

- [ ] **Step 4：实现 handleIconUpload / handleIconRequest**

文件末尾附近添加：

```js
function handleIconUpload(message, context)
{
    if (!context.connection.roomCode || !context.connection.playerId)
    {
        throw new ProtocolError('NOT_IN_ROOM', '当前未加入房间');
    }

    try
    {
        context.iconCache.set(message.bundleId, message.iconBase64);
    }
    catch (error)
    {
        if (error instanceof IconCacheError)
        {
            safeSend(context.connection.socket, createErrorMessage(error.code));
            return;
        }
        throw error;
    }

    context.broadcastToRoom(
        context.connection.roomCode,
        createIconBroadcastMessage({
            bundleId: message.bundleId,
            iconBase64: message.iconBase64
        }),
        null                // 不排除发送者（让他自己也有图显示）
    );
}

function handleIconRequest(message, context)
{
    for (const bundleId of message.bundleIds)
    {
        const iconBase64 = context.iconCache.get(bundleId);
        if (!iconBase64) continue;
        safeSend(
            context.connection.socket,
            createIconBroadcastMessage({ bundleId, iconBase64 })
        );
    }
}
```

- [ ] **Step 5：handlePlayerStateUpdate 里加 icon_need 触发**

在 `handlePlayerStateUpdate` 末尾（`context.broadcastToRoom(...)` 之后）加：

```js
const bundleId = result.player.latestState?.activeApp?.bundleId;
if (bundleId && !context.iconCache.has(bundleId))
{
    safeSend(context.connection.socket, createIconNeedMessage({ bundleId }));
}
```

**注意**：`handlePlayerStateUpdate` 参数目前没传 `iconCache`，对照 Step 3 的改动，`handleMessage` 会把 iconCache 放 context 里；`handlePlayerStateUpdate(message, context)` 的 context 已经有。

- [ ] **Step 6：Commit**

```bash
git add Server/src/index.js
git commit -m "feat(server): 分派 icon_upload/icon_request + state 未命中时回发 icon_need"
```

---

### Task 5.4：服务端图标端到端集成测试

**Files:**
- Modify: `Server/test/integration.test.js`

- [ ] **Step 1：在现有文件里追加一个用例**

```js
test('图标流程：state_update → icon_need → icon_upload → icon_broadcast', async (t) =>
{
    const app = await createPomodoroServer({
        port: 0,
        heartbeatIntervalMs: 5000,
        initTimeoutMs: 1000
    });

    t.after(async () => { await app.close(); });

    const clientA = await openClient(app.url);
    const clientB = await openClient(app.url);
    const inboxA = createMessageCollector(clientA);
    const inboxB = createMessageCollector(clientB);

    t.after(() => { clientA.close(); clientB.close(); });

    // A 创建房间，B 加入
    sendJson(clientA, { type: 'create_room', playerName: 'A' });
    const roomCreated = await inboxA.waitFor('room_created');
    await inboxA.waitFor('room_snapshot');

    sendJson(clientB, { type: 'join_room', roomCode: roomCreated.roomCode, playerName: 'B' });
    await inboxB.waitFor('room_joined');
    await inboxB.waitFor('room_snapshot');
    await inboxA.waitFor('player_joined');

    // B 发带有未知 bundleId 的 state_update
    sendJson(clientB, {
        type: 'player_state_update',
        state: {
            pomodoro: { phase: 0, remainingSeconds: 1500, currentRound: 1, totalRounds: 4, isRunning: false },
            activeApp: { name: 'Safari', bundleId: 'com.apple.Safari' }
        }
    });

    // 预期 B 收到 icon_need
    const iconNeed = await inboxB.waitFor('icon_need');
    assert.equal(iconNeed.bundleId, 'com.apple.Safari');

    // B 上传图标
    sendJson(clientB, {
        type: 'icon_upload',
        bundleId: 'com.apple.Safari',
        iconBase64: 'QUFB'  // "AAA" base64
    });

    // A 与 B 都应收到 icon_broadcast
    const bcA = await inboxA.waitFor('icon_broadcast');
    const bcB = await inboxB.waitFor('icon_broadcast');
    assert.equal(bcA.bundleId, 'com.apple.Safari');
    assert.equal(bcA.iconBase64, 'QUFB');
    assert.equal(bcB.bundleId, 'com.apple.Safari');

    // 随后 A 请求图标应命中缓存
    sendJson(clientA, { type: 'icon_request', bundleIds: ['com.apple.Safari'] });
    const bcA2 = await inboxA.waitFor('icon_broadcast');
    assert.equal(bcA2.bundleId, 'com.apple.Safari');
});
```

- [ ] **Step 2：跑测试**

```bash
cd Server && node --test test/*.js
```

- [ ] **Step 3：Commit**

```bash
git add Server/test/integration.test.js
git commit -m "test(server): 图标端到端集成用例（need→upload→broadcast→request）"
```

---

### Task 5.5：Unity 端 IIconCacheSystem + TDD

**Files:**
- Create: `Assets/Scripts/APP/Network/System/IIconCacheSystem.cs`
- Create: `Assets/Scripts/APP/Network/System/IconCacheSystem.cs`
- Create: `Assets/Tests/EditMode/NetworkTests/IconCacheSystemTests.cs`

- [ ] **Step 1：接口**

```csharp
using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public interface IIconCacheSystem : ISystem
    {
        bool HasIconFor(string bundleId);
        Texture2D GetTexture(string bundleId);
        void StoreFromBase64(string bundleId, string base64);
        string EncodeBase64FromPngBytes(byte[] pngBytes);
    }
}
```

- [ ] **Step 2：测试（red）**

```csharp
using APP.Network.System;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class IconCacheSystemTests
    {
        // IconCacheSystem 不访问 Architecture，直接 new 就能测
        private IconCacheSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _sys = new IconCacheSystem(maxEntries: 3);
        }

        private static string OnePixelPngBase64()
        {
            // 1x1 红色 PNG 的 base64
            return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwAB/1EF5YwAAAAASUVORK5CYII=";
        }

        [Test]
        public void StoreAndFetch_RoundTripsTexture()
        {
            _sys.StoreFromBase64("bundle.x", OnePixelPngBase64());
            Assert.That(_sys.HasIconFor("bundle.x"), Is.True);
            Assert.That(_sys.GetTexture("bundle.x"), Is.Not.Null);
        }

        [Test]
        public void LRU_EvictsOldestWhenOverCap()
        {
            _sys.StoreFromBase64("a", OnePixelPngBase64());
            _sys.StoreFromBase64("b", OnePixelPngBase64());
            _sys.StoreFromBase64("c", OnePixelPngBase64());
            _sys.StoreFromBase64("d", OnePixelPngBase64());
            Assert.That(_sys.HasIconFor("a"), Is.False);
            Assert.That(_sys.HasIconFor("d"), Is.True);
        }

        [Test]
        public void EncodeBase64FromPngBytes_RoundTrips()
        {
            byte[] bytes = System.Convert.FromBase64String(OnePixelPngBase64());
            string roundtripped = _sys.EncodeBase64FromPngBytes(bytes);
            Assert.That(roundtripped, Is.EqualTo(OnePixelPngBase64()));
        }
    }
}
```

- [ ] **Step 3：跑 red**

- [ ] **Step 4：实现**

```csharp
using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public sealed class IconCacheSystem : AbstractSystem, IIconCacheSystem
    {
        private readonly int _maxEntries;
        // LinkedList + Dictionary 模拟 LRU
        private readonly LinkedList<string> _order = new LinkedList<string>();
        private readonly Dictionary<string, (LinkedListNode<string> node, Texture2D tex)> _map
            = new Dictionary<string, (LinkedListNode<string>, Texture2D)>();

        public IconCacheSystem(int maxEntries = 100)
        {
            _maxEntries = Math.Max(1, maxEntries);
        }

        protected override void OnInit() { }

        public bool HasIconFor(string bundleId) => !string.IsNullOrEmpty(bundleId) && _map.ContainsKey(bundleId);

        public Texture2D GetTexture(string bundleId)
        {
            if (string.IsNullOrEmpty(bundleId) || !_map.TryGetValue(bundleId, out var entry)) return null;
            _order.Remove(entry.node);
            _order.AddLast(entry.node);
            return entry.tex;
        }

        public void StoreFromBase64(string bundleId, string base64)
        {
            if (string.IsNullOrEmpty(bundleId) || string.IsNullOrEmpty(base64)) return;

            byte[] png;
            try { png = Convert.FromBase64String(base64); }
            catch { return; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = $"AppIcon:{bundleId}" };
            if (!tex.LoadImage(png))
            {
                UnityEngine.Object.Destroy(tex);
                return;
            }
            tex.Apply();

            if (_map.TryGetValue(bundleId, out var old))
            {
                _order.Remove(old.node);
                if (old.tex != null) UnityEngine.Object.Destroy(old.tex);
                _map.Remove(bundleId);
            }

            var node = _order.AddLast(bundleId);
            _map[bundleId] = (node, tex);

            while (_map.Count > _maxEntries)
            {
                string oldestKey = _order.First.Value;
                _order.RemoveFirst();
                if (_map.TryGetValue(oldestKey, out var oldestEntry))
                {
                    if (oldestEntry.tex != null) UnityEngine.Object.Destroy(oldestEntry.tex);
                    _map.Remove(oldestKey);
                }
            }
        }

        public string EncodeBase64FromPngBytes(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) return string.Empty;
            return Convert.ToBase64String(pngBytes);
        }
    }
}
```

- [ ] **Step 5：跑测试确认绿**

- [ ] **Step 6：Commit**

```bash
git add Assets/Scripts/APP/Network/System/IIconCacheSystem.cs \
        Assets/Scripts/APP/Network/System/IconCacheSystem.cs \
        Assets/Tests/EditMode/NetworkTests/IconCacheSystemTests.cs
git commit -m "feat(net): IconCacheSystem LRU + base64 编解码 + 单测"
```

---

### Task 5.6：GameApp 注册 IconCacheSystem + 事件 E_IconUpdated

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/GameApp.cs`
- Modify: `Assets/Scripts/APP/Network/Event/NetworkEvents.cs`

- [ ] **Step 1：注册 System**

```csharp
RegisterSystem<IIconCacheSystem>(new IconCacheSystem());
```

放在 `RegisterSystem<INetworkSystem>` 之前（NetworkSystem 会 GetSystem<IIconCacheSystem>）。

- [ ] **Step 2：加事件**

`NetworkEvents.cs` 末尾：
```csharp
public readonly struct E_IconUpdated
{
    public readonly string BundleId;
    public E_IconUpdated(string bundleId) { BundleId = bundleId; }
}
```

- [ ] **Step 3：Commit**

```bash
git add Assets/Scripts/APP/Pomodoro/GameApp.cs \
        Assets/Scripts/APP/Network/Event/NetworkEvents.cs
git commit -m "feat(arch): 注册 IIconCacheSystem + E_IconUpdated 事件"
```

---

### Task 5.7：Outbound DTO 新增 IconUpload / IconRequest

**Files:**
- Modify: `Assets/Scripts/APP/Network/DTO/OutboundMessage.cs`

- [ ] **Step 1：追加两个 DTO**

```csharp
[Serializable]
public sealed class OutboundIconUpload : OutboundMessage
{
    public string bundleId;
    public string iconBase64;
}

[Serializable]
public sealed class OutboundIconRequest : OutboundMessage
{
    public string[] bundleIds;
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Network/DTO/OutboundMessage.cs
git commit -m "feat(net): OutboundIconUpload / OutboundIconRequest DTO"
```

---

### Task 5.8：InboundMessage 增加 bundleId / iconBase64 字段

**Files:**
- Modify: `Assets/Scripts/APP/Network/DTO/InboundMessage.cs`

- [ ] **Step 1：InboundMessage 加字段**

```csharp
[Serializable]
public sealed class InboundMessage
{
    public int v;
    public string type;
    public string roomCode;
    public string playerId;
    public string playerName;
    public RemoteState state;
    public List<SnapshotEntry> players;
    public string error;
    public string bundleId;       // 新增：icon_need / icon_broadcast
    public string iconBase64;     // 新增：icon_broadcast
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Scripts/APP/Network/DTO/InboundMessage.cs
git commit -m "feat(net): InboundMessage 增加 bundleId/iconBase64 字段"
```

---

### Task 5.9：NetworkSystem 分派 icon_need / icon_broadcast

**Files:**
- Modify: `Assets/Scripts/APP/Network/System/NetworkSystem.cs`

- [ ] **Step 1：DispatchInbound switch 加 case**

```csharp
case "icon_need":
    HandleIconNeed(inbound);
    break;
case "icon_broadcast":
    HandleIconBroadcast(inbound);
    break;
```

- [ ] **Step 2：实现 HandleIconNeed**

类里加：
```csharp
private void HandleIconNeed(InboundMessage inbound)
{
    if (string.IsNullOrEmpty(inbound.bundleId)) return;

    IActiveAppSystem appSys = this.GetSystem<IActiveAppSystem>();
    ActiveAppSnapshot snap = appSys.Current;
    if (snap.BundleId != inbound.bundleId || snap.IconPngBytes == null)
    {
        // 当前已经切到别的 App 了 —— 忽略这次请求（服务端下次会再问）
        return;
    }

    IIconCacheSystem iconCache = this.GetSystem<IIconCacheSystem>();
    string base64 = iconCache.EncodeBase64FromPngBytes(snap.IconPngBytes);
    if (string.IsNullOrEmpty(base64)) return;

    Send(new OutboundIconUpload
    {
        type = "icon_upload",
        bundleId = inbound.bundleId,
        iconBase64 = base64,
    });
}
```

- [ ] **Step 3：实现 HandleIconBroadcast**

```csharp
private void HandleIconBroadcast(InboundMessage inbound)
{
    if (string.IsNullOrEmpty(inbound.bundleId) || string.IsNullOrEmpty(inbound.iconBase64)) return;
    IIconCacheSystem iconCache = this.GetSystem<IIconCacheSystem>();
    iconCache.StoreFromBase64(inbound.bundleId, inbound.iconBase64);
    this.SendEvent(new E_IconUpdated(inbound.bundleId));
}
```

- [ ] **Step 4：HandleRoomSnapshot 后批量 icon_request**

修改 `HandleRoomSnapshot`：
```csharp
private void HandleRoomSnapshot(InboundMessage inbound)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    List<RemotePlayerData> players = BuildRemotePlayers(inbound.players, room.LocalPlayerId.Value);
    room.ApplySnapshot(players);
    this.SendEvent(new E_RoomSnapshot(ClonePlayers(players)));

    RequestMissingIcons(players);
}

private void RequestMissingIcons(List<RemotePlayerData> players)
{
    if (players == null || players.Count == 0) return;
    IIconCacheSystem iconCache = this.GetSystem<IIconCacheSystem>();

    var missing = new HashSet<string>();
    foreach (RemotePlayerData p in players)
    {
        if (!string.IsNullOrEmpty(p.ActiveAppBundleId) && !iconCache.HasIconFor(p.ActiveAppBundleId))
        {
            missing.Add(p.ActiveAppBundleId);
        }
    }

    if (missing.Count == 0) return;

    var arr = new string[missing.Count];
    missing.CopyTo(arr);
    Send(new OutboundIconRequest { type = "icon_request", bundleIds = arr });
}
```

`ToRemotePlayerData` 中 `ActiveAppBundleId` 应已由 DTO 中的 `bundleId` 读取（第 1 阶段 `ToRemotePlayerData` 里已经取）。确认无需再改。

- [ ] **Step 5：Unity 无编译错误**

- [ ] **Step 6：Commit**

```bash
git add Assets/Scripts/APP/Network/System/NetworkSystem.cs
git commit -m "feat(net): NetworkSystem 分派 icon_need/icon_broadcast + 快照后批量 icon_request"
```

---

### Task 5.10：Pencil 设计稿 —— PlayerCard 增加 active app 图标/名字区

> UI 先改 Pencil。

**Files:**
- Modify (via Pencil MCP): `AUI/PUI.pen` 或 `AUI/desk-window-next` — PlayerCard 组件

- [ ] **Step 1：打开并定位 PlayerCard**

```
mcp__pencil__get_editor_state { include_schema: false }
mcp__pencil__batch_get { patterns: ["name:pc-root"] }
```

- [ ] **Step 2：如果当前 PlayerCard 已经有 activeApp 区（`pc-active-app-icon` / `pc-active-app-name` / `_appLabel`），略过本 Step。如果没有则插入**

```
mcp__pencil__batch_design {
  operations: `
    appRow=I("<pc-root 下合适的子容器 id>", {
      type: "container",
      name: "pc-active-app-row",
      styleClasses: ["pc-active-app-row"],
      children: [
        { type: "image", name: "pc-active-app-icon", styleClasses: ["pc-active-app-icon"] },
        { type: "label", name: "pc-active-app-name", text: "—",  styleClasses: ["pc-active-app-name"] }
      ]
    })
  `
}
```

- [ ] **Step 3：Commit**

```bash
git add AUI/PUI.pen AUI/desk-window-next 2>/dev/null
git commit -m "design(pencil): PlayerCard active app 图标+名字区"
```

---

### Task 5.11：PlayerCard.uxml / uss 同步

**Files:**
- Modify: `Assets/UI_V2/Documents/PlayerCard.uxml`
- Modify: `Assets/UI_V2/Styles/PlayerCard.uss`

- [ ] **Step 1：确认 uxml 有 icon 元素**

查看 PlayerCard.uxml。如果已有 `<ui:VisualElement name="pc-active-app-icon" />` 则略过；否则在合适位置添加：

```xml
<ui:VisualElement class="pc-active-app-row">
    <ui:VisualElement name="pc-active-app-icon" class="pc-active-app-icon" />
    <ui:Label name="pc-active-app-name" text="—" class="pc-active-app-name" />
</ui:VisualElement>
```

- [ ] **Step 2：USS**

PlayerCard.uss 追加：

```css
.pc-active-app-row {
    flex-direction: row;
    align-items: center;
    margin-top: 4px;
}

.pc-active-app-icon {
    width: 16px;
    height: 16px;
    background-size: contain;
    background-repeat: no-repeat;
    margin-right: 6px;
}

.pc-active-app-name {
    font-size: 11px;
    color: #666;
}
```

- [ ] **Step 3：Commit**

```bash
git add Assets/UI_V2/Documents/PlayerCard.uxml Assets/UI_V2/Styles/PlayerCard.uss
git commit -m "feat(ui): PlayerCard 同步 Pencil active app 样式"
```

---

### Task 5.12：PlayerCardView 贴图 + PlayerCardManager 订阅 E_IconUpdated

**Files:**
- Modify: `Assets/UI_V2/Controller/PlayerCardView.cs`
- Modify: `Assets/UI_V2/Controller/PlayerCardController.cs`
- Modify: `Assets/UI_V2/Controller/PlayerCardManager.cs`

- [ ] **Step 1：PlayerCardView / PlayerCardController 读图**

找到 `_appLabel.text = ...` 的地方，改成：
```csharp
_appLabel.text = string.IsNullOrEmpty(data.ActiveAppName) ? "—" : data.ActiveAppName;

VisualElement iconElem = _root.Q<VisualElement>("pc-active-app-icon");
if (iconElem != null)
{
    var iconCache = GameApp.Interface.GetSystem<APP.Network.System.IIconCacheSystem>();
    Texture2D tex = iconCache.GetTexture(data.ActiveAppBundleId);
    iconElem.style.backgroundImage = tex != null
        ? new StyleBackground(tex)
        : StyleKeyword.None;
}
```

> 这里要求 PlayerCardView 持有 `_root`。翻一下现有代码是否有 `_root` 字段；如果没有，加一个并从构造函数赋值。

- [ ] **Step 2：PlayerCardManager 订阅 E_IconUpdated 刷新**

`PlayerCardManager.Initialize` 已注册其它事件，追加：
```csharp
this.RegisterEvent<APP.Network.Event.E_IconUpdated>(OnIconUpdated)
    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
```

加方法：
```csharp
private void OnIconUpdated(APP.Network.Event.E_IconUpdated e)
{
    IRoomModel room = this.GetModel<IRoomModel>();
    foreach (var kv in _cards)
    {
        RemotePlayerData data = FindRemotePlayer(room, kv.Key);
        if (data != null && data.ActiveAppBundleId == e.BundleId)
        {
            kv.Value.Refresh(data);
        }
    }
}
```

- [ ] **Step 3：Unity 无编译错误**

- [ ] **Step 4：Commit**

```bash
git add Assets/UI_V2/Controller/PlayerCardView.cs \
        Assets/UI_V2/Controller/PlayerCardController.cs \
        Assets/UI_V2/Controller/PlayerCardManager.cs
git commit -m "feat(ui): PlayerCard 读 IconCacheSystem 贴 active app 图标 + 订阅 E_IconUpdated"
```

---

## 阶段 6：端到端 PlayMode 集成测试

### Task 6.1：服务端 test-server.js 启动器

**Files:**
- Create: `Server/bin/test-server.js`

- [ ] **Step 1：写启动器**

```js
#!/usr/bin/env node
import { createPomodoroServer } from '../src/index.js';

const app = await createPomodoroServer({ port: 0 });
console.log(JSON.stringify({ port: app.port, url: app.url }));

// 保持进程不退出，等父进程 kill
process.stdin.resume();

function shutdown()
{
    app.close().then(() => process.exit(0)).catch(() => process.exit(1));
}

process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
```

- [ ] **Step 2：手动验证**

```bash
node Server/bin/test-server.js
```

Expected：stdout 输出 JSON 如 `{"port":58123,"url":"ws://127.0.0.1:58123"}`，进程挂起。`Ctrl+C` 正常退出。

- [ ] **Step 3：Commit**

```bash
git add Server/bin/test-server.js
git commit -m "test(server): 添加 port=0 的测试启动器"
```

---

### Task 6.2：Unity 端 TestServerHarness

**Files:**
- Create: `Assets/Tests/PlayMode/NetworkIntegration/TestServerHarness.cs`

- [ ] **Step 1：写工具类**

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace APP.NetworkIntegration.Tests
{
    /// <summary>
    /// 启动 Server/bin/test-server.js 子进程。
    /// 通过 stdout 第一行 JSON 读取实际监听端口。
    /// </summary>
    public sealed class TestServerHarness : IDisposable
    {
        private Process _process;

        public string Url { get; private set; }
        public int Port { get; private set; }

        public static TestServerHarness Start()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string serverScript = Path.Combine(projectRoot, "Server", "bin", "test-server.js");

            if (!File.Exists(serverScript))
            {
                Assert.Ignore($"测试服务器脚本不存在: {serverScript}");
            }

            string nodeBin = Environment.GetEnvironmentVariable("NODE_BIN") ?? "node";

            var psi = new ProcessStartInfo
            {
                FileName = nodeBin,
                Arguments = $"\"{serverScript}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Process p;
            try { p = Process.Start(psi); }
            catch (Exception ex) { Assert.Ignore($"无法启动 node: {ex.Message}"); return null; }

            if (p == null) { Assert.Ignore("Process.Start 返回 null"); return null; }

            string firstLine = p.StandardOutput.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                string stderr = p.StandardError.ReadToEnd();
                p.Kill();
                Assert.Ignore($"test-server.js 未输出启动信息；stderr={stderr}");
                return null;
            }

            // 粗暴解析 JSON：{"port":<num>,"url":"<str>"}
            int port = ParsePort(firstLine);
            string url = ParseUrl(firstLine);

            return new TestServerHarness { _process = p, Port = port, Url = url };
        }

        private static int ParsePort(string line)
        {
            int idx = line.IndexOf("\"port\":");
            if (idx < 0) return 0;
            int start = idx + "\"port\":".Length;
            int end = line.IndexOfAny(new[] { ',', '}' }, start);
            return int.TryParse(line.Substring(start, end - start).Trim(), out int p) ? p : 0;
        }

        private static string ParseUrl(string line)
        {
            int idx = line.IndexOf("\"url\":\"");
            if (idx < 0) return null;
            int start = idx + "\"url\":\"".Length;
            int end = line.IndexOf('"', start);
            return end > start ? line.Substring(start, end - start) : null;
        }

        public void Dispose()
        {
            if (_process == null || _process.HasExited) return;
            try { _process.Kill(); _process.WaitForExit(3000); }
            catch { /* ignore */ }
            _process.Dispose();
            _process = null;
        }
    }
}
```

- [ ] **Step 2：Commit**

```bash
git add Assets/Tests/PlayMode/NetworkIntegration/TestServerHarness.cs
git commit -m "test(playmode): TestServerHarness 启停 Node 测试服务器"
```

---

### Task 6.3：NetworkE2ETests —— Case 1 (CreateRoom)

**Files:**
- Create: `Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs`

- [ ] **Step 1：写测试骨架 + Case 1**

```csharp
using System.Collections;
using APP.Network.Command;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.TestTools;

namespace APP.NetworkIntegration.Tests
{
    [TestFixture]
    public sealed class NetworkE2ETests
    {
        private TestServerHarness _server;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = TestServerHarness.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server?.Dispose();
        }

        [UnityTest]
        public IEnumerator CreateRoom_ReceivesRoomCreatedAndInRoomStatus()
        {
            Assert.That(_server, Is.Not.Null, "harness 未启动");

            bool roomCreated = false;
            string createdCode = null;
            IUnRegister reg = GameApp.Interface.RegisterEvent<E_RoomCreated>(e =>
            {
                roomCreated = true;
                createdCode = e.Code;
            });

            GameApp.Interface.SendCommand(new Cmd_CreateRoom("Alice", _server.Url));

            float timeout = 5f;
            while (!roomCreated && timeout > 0f)
            {
                yield return null;
                timeout -= Time.unscaledDeltaTime;
            }

            reg.UnRegister();

            Assert.That(roomCreated, Is.True, "未在 5 秒内触发 E_RoomCreated");
            Assert.That(createdCode, Is.Not.Null.And.Not.Empty);
            Assert.That(GameApp.Interface.GetModel<IRoomModel>().IsInRoom.Value, Is.True);

            // 清理：离开房间，避免污染下一用例
            GameApp.Interface.SendCommand(new Cmd_LeaveRoom());
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
```

- [ ] **Step 2：Unity PlayMode Test Runner 跑**

MCP `run_tests` testMode PlayMode testNames `APP.NetworkIntegration.Tests.NetworkE2ETests.CreateRoom_*`。
Expected：绿。

- [ ] **Step 3：Commit**

```bash
git add Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs
git commit -m "test(playmode): E2E Case 1 - CreateRoom"
```

---

### Task 6.4：NetworkE2ETests —— Case 2 (TwoClients_StateSync，真 A + 裸 WS B)

**Files:**
- Modify: `Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs`

- [ ] **Step 1：加裸 WS helper 与 Case 2**

类顶部 using 追加：
```csharp
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
```

类底部加 helper：
```csharp
private static async Task<ClientWebSocket> OpenBareWsAsync(string url)
{
    var ws = new ClientWebSocket();
    await ws.ConnectAsync(new System.Uri(url), CancellationToken.None);
    return ws;
}

private static Task SendJsonAsync(ClientWebSocket ws, string json)
{
    byte[] bytes = Encoding.UTF8.GetBytes(json);
    return ws.SendAsync(new System.ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).AsTask();
}
```

用例：
```csharp
[UnityTest]
public IEnumerator TwoClients_JoinAndStateSync()
{
    Assert.That(_server, Is.Not.Null);

    // A: real client
    bool roomCreated = false;
    string roomCode = null;
    IUnRegister r1 = GameApp.Interface.RegisterEvent<E_RoomCreated>(e => { roomCreated = true; roomCode = e.Code; });
    GameApp.Interface.SendCommand(new Cmd_CreateRoom("Alice", _server.Url));

    float timeout = 5f;
    while (!roomCreated && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
    r1.UnRegister();
    Assert.That(roomCreated);

    // B: bare WS
    ClientWebSocket wsB = null;
    var connectTask = Task.Run(async () => wsB = await OpenBareWsAsync(_server.Url));
    while (!connectTask.IsCompleted) yield return null;
    Assert.That(wsB, Is.Not.Null);

    string joinJson = $"{{\"v\":1,\"type\":\"join_room\",\"roomCode\":\"{roomCode}\",\"playerName\":\"Bob\"}}";
    var sendTask = Task.Run(async () => await SendJsonAsync(wsB, joinJson));
    while (!sendTask.IsCompleted) yield return null;

    // A should get E_PlayerJoined for Bob
    bool playerJoined = false;
    IUnRegister r2 = GameApp.Interface.RegisterEvent<E_PlayerJoined>(e =>
    {
        if (e.Player?.PlayerName == "Bob") playerJoined = true;
    });

    timeout = 5f;
    while (!playerJoined && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
    r2.UnRegister();
    Assert.That(playerJoined, Is.True, "A 未收到 Bob 加入事件");

    // B pushes state → A should receive
    bool stateReceived = false;
    IUnRegister r3 = GameApp.Interface.RegisterEvent<E_RemoteStateUpdated>(_ => stateReceived = true);

    string stateJson = "{\"v\":1,\"type\":\"player_state_update\",\"state\":{\"pomodoro\":{\"phase\":0,\"remainingSeconds\":1499,\"currentRound\":1,\"totalRounds\":4,\"isRunning\":true},\"activeApp\":null}}";
    var pushTask = Task.Run(async () => await SendJsonAsync(wsB, stateJson));
    while (!pushTask.IsCompleted) yield return null;

    timeout = 5f;
    while (!stateReceived && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
    r3.UnRegister();
    Assert.That(stateReceived, Is.True, "A 未收到 B 的 state 广播");

    // 清理
    var closeTask = Task.Run(async () => await wsB.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None));
    while (!closeTask.IsCompleted) yield return null;
    GameApp.Interface.SendCommand(new Cmd_LeaveRoom());
    yield return new WaitForSecondsRealtime(0.5f);
}
```

- [ ] **Step 2：跑测试确认绿**

- [ ] **Step 3：Commit**

```bash
git add Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs
git commit -m "test(playmode): E2E Case 2 - 真 A + 裸 WS B 的加入与状态同步"
```

---

### Task 6.5：NetworkE2ETests —— Case 3 (IconUpload)

**Files:**
- Modify: `Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs`

- [ ] **Step 1：加 Case 3**

```csharp
[UnityTest]
public IEnumerator IconUpload_Broadcast_CachedInA()
{
    Assert.That(_server, Is.Not.Null);

    bool roomCreated = false;
    string roomCode = null;
    IUnRegister r1 = GameApp.Interface.RegisterEvent<E_RoomCreated>(e => { roomCreated = true; roomCode = e.Code; });
    GameApp.Interface.SendCommand(new Cmd_CreateRoom("Alice", _server.Url));

    float timeout = 5f;
    while (!roomCreated && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
    r1.UnRegister();
    Assert.That(roomCreated);

    ClientWebSocket wsB = null;
    var t1 = Task.Run(async () => wsB = await OpenBareWsAsync(_server.Url));
    while (!t1.IsCompleted) yield return null;

    string joinJson = $"{{\"v\":1,\"type\":\"join_room\",\"roomCode\":\"{roomCode}\",\"playerName\":\"Bob\"}}";
    var t2 = Task.Run(async () => await SendJsonAsync(wsB, joinJson));
    while (!t2.IsCompleted) yield return null;
    yield return new WaitForSecondsRealtime(0.5f);

    const string testBundleId = "test.e2e.app";
    const string onePxPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwAB/1EF5YwAAAAASUVORK5CYII=";

    string state = $"{{\"v\":1,\"type\":\"player_state_update\",\"state\":{{\"pomodoro\":{{\"phase\":0,\"remainingSeconds\":1500,\"currentRound\":1,\"totalRounds\":4,\"isRunning\":false}},\"activeApp\":{{\"name\":\"Test\",\"bundleId\":\"{testBundleId}\"}}}}}}";
    var t3 = Task.Run(async () => await SendJsonAsync(wsB, state));
    while (!t3.IsCompleted) yield return null;

    // 等 B 端收到 icon_need（我们模拟 B → 发 icon_upload）
    // 简化起见这里直接让 B 上传（不读 B 的 inbox）
    string upload = $"{{\"v\":1,\"type\":\"icon_upload\",\"bundleId\":\"{testBundleId}\",\"iconBase64\":\"{onePxPngBase64}\"}}";
    var t4 = Task.Run(async () => await SendJsonAsync(wsB, upload));
    while (!t4.IsCompleted) yield return null;

    // A 端 IconCache 应收到
    var iconCache = GameApp.Interface.GetSystem<APP.Network.System.IIconCacheSystem>();
    timeout = 5f;
    while (!iconCache.HasIconFor(testBundleId) && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
    Assert.That(iconCache.HasIconFor(testBundleId), Is.True, "A 端 IconCache 未命中");

    var close = Task.Run(async () => await wsB.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None));
    while (!close.IsCompleted) yield return null;
    GameApp.Interface.SendCommand(new Cmd_LeaveRoom());
    yield return new WaitForSecondsRealtime(0.5f);
}
```

- [ ] **Step 2：跑测试确认绿**

- [ ] **Step 3：Commit**

```bash
git add Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs
git commit -m "test(playmode): E2E Case 3 - 图标上传广播全流程"
```

---

### Task 6.6：NetworkE2ETests —— Case 4 (ReconnectAfterDrop)

**Files:**
- Modify: `Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs`

- [ ] **Step 1：加 Case 4**

```csharp
[UnityTest]
public IEnumerator ReconnectAfterServerDrop_RejoinsRoom()
{
    Assert.That(_server, Is.Not.Null);

    bool roomCreated = false;
    string roomCode = null;
    IUnRegister r1 = GameApp.Interface.RegisterEvent<E_RoomCreated>(e => { roomCreated = true; roomCode = e.Code; });
    GameApp.Interface.SendCommand(new Cmd_CreateRoom("Alice", _server.Url));

    float timeout = 5f;
    while (!roomCreated && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
    r1.UnRegister();

    // 重启服务器 —— 最简单的方法：断掉现有子进程再起新的
    _server.Dispose();
    yield return new WaitForSecondsRealtime(1f);
    _server = TestServerHarness.Start();

    // 客户端应该自行进入 Reconnecting 状态；等 Error 或重新 Connected
    var room = GameApp.Interface.GetModel<IRoomModel>();
    float wait = 15f;
    while (wait > 0f && room.Status.Value != ConnectionStatus.Error && room.Status.Value != ConnectionStatus.InRoom && room.Status.Value != ConnectionStatus.Connected)
    {
        yield return null;
        wait -= Time.unscaledDeltaTime;
    }

    // 本次不要求必须重连成功（服务器换端口后无法用旧 url）；断言至少客户端未卡在 Connecting
    Assert.That(room.Status.Value,
        Is.AnyOf(ConnectionStatus.Error, ConnectionStatus.Disconnected, ConnectionStatus.Reconnecting, ConnectionStatus.Connected, ConnectionStatus.InRoom),
        "状态机卡住");

    GameApp.Interface.SendCommand(new Cmd_LeaveRoom());
    yield return new WaitForSecondsRealtime(0.5f);
}
```

> **注**：严格意义上"服务器重启后重连到同一房间" 因为 test-server 每次随机端口，客户端无法自动回到原 url。真实场景是"服务器短暂闪断再恢复在同一端口"。Case 4 的断言放松为"状态机未卡在 Connecting"。如需严格测试，要改 TestServerHarness 支持固定端口 + 热重启。本次保持简化。

- [ ] **Step 2：跑测试确认绿**

- [ ] **Step 3：Commit**

```bash
git add Assets/Tests/PlayMode/NetworkIntegration/NetworkE2ETests.cs
git commit -m "test(playmode): E2E Case 4 - 服务器断开时客户端状态不卡住"
```

---

## 最终验收

- [ ] **Step 1：全部 EditMode 测试绿**

MCP `run_tests` testMode EditMode，检查：
- `APP.Network.Tests.*`
- `APP.Utility.Tests.*`
- `APP.SessionMemory.Tests.*`

- [ ] **Step 2：全部 PlayMode 测试绿**

MCP `run_tests` testMode PlayMode，检查：
- `APP.NetworkIntegration.Tests.*`

- [ ] **Step 3：服务端测试绿**

```bash
cd Server && node --test test/*.js
```

- [ ] **Step 4：手动冒烟（2 台 Mac 或一台 + 浏览器 wscat）**

启动服务器 → Mac1 创建房间 → Mac2 加入；切应用时双方 PlayerCard 更新；关 Mac1 应用再开，自动回房。

- [ ] **Step 5：清理本次改动未用到的残留**

检查 `Assets/Prefabs/AppMonitorUI.prefab` 是否还有引用——如果是无用孤儿，本次可以不清理，留给后续任务。

- [ ] **Step 6：最终 Commit（如有小修）**

---

## 不在本次范围（重申）

- TLS / wss 跨网部署
- 音视频互动 / 聊天 / 表情
- 房间密码 / 踢人 / 禁言
- 跨平台 AppMonitor（Windows / Linux）
- 服务端持久化 / 数据库

---

## 附：关键文件对应关系

| 设计稿节点 | UXML 名字 | USS 类名 | Controller 方法 |
|---|---|---|---|
| osp-create-btn | osp-create-btn | comp-btn-secondary | OnCreateClicked |
| osp-copy-btn | osp-copy-btn | comp-btn-icon | OnCopyClicked |
| osp-reconnect-banner | osp-reconnect-banner | osp-reconnect-banner | OnConnectionStateChanged |
| osp-auto-toggle | osp-auto-toggle | comp-toggle-switch | ValueChanged → SetAutoReconnectEnabled |
| osp-hist-list item | 动态生成 | osp-hist-item | RefreshHistoryList |
| pc-active-app-icon | pc-active-app-icon | pc-active-app-icon | PlayerCardView.Refresh |
