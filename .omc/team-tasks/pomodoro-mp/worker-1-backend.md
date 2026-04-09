# Subtask 1: Node.js WebSocket 后端服务器

**所属方案**: `/Users/xpy/Desktop/NanZhai/CPA/.omc/plans/multiplayer-pomodoro-plan.md` 的 **Phase 1**
**相关方案章节**: Phase 1（Step 1.1 - 1.5），文件清单中的 `Server/**`

## 文件所有权（严格独占）

你**只能**写入这些路径，其他 worker 不会碰：

- `Server/package.json`
- `Server/src/index.js`
- `Server/src/RoomManager.js`
- `Server/src/protocol.js`
- `Server/test/room.test.js`
- `Server/test/protocol.test.js`
- `Server/test/integration.test.js`
- `Server/test/latency.test.js`
- `Server/README.md`（可选，简要运行说明）

**严禁**写入 `Assets/**` 或修改方案文件。

## 实施目标

严格按照方案 Phase 1 实现 Node.js + ws 后端：

1. **Step 1.1 项目初始化**：`Server/` 目录下 `npm init`，`type: "module"`，依赖 `ws@^8` + `nanoid@^5`，测试使用 Node 内置 `node --test`
2. **Step 1.2 RoomManager**：
   - 房间码 6 位，字符集 `ABCDEFGHJKMNPQRSTUVWXYZ23456789`（排除 0/O/1/I/L）
   - `createRoom / joinRoom / leaveRoom / updatePlayerState` API
   - 空房间 30s 销毁定时器
   - 每房间最多 8 人，玩家名 1-16 字符
   - `latestState` 缓存用于 room_snapshot
3. **Step 1.3 消息协议** (`protocol.js`)：
   - 所有消息带 `v: 1` 字段（protocolVersion）
   - 客户端→服务端: `create_room`, `join_room`, `leave_room`, `player_state_update`
   - 服务端→客户端: `room_created`, `room_joined`, `room_snapshot`, `player_joined`, `player_left`, `player_state_broadcast`, `error`
   - 服务端通过 UUID 生成 playerId（`room_created/joined` 返回 playerId）
4. **Step 1.4 节流 & 心跳**：
   - 服务端对 `player_state_update` 实施 **10 Hz 滑动窗口**节流（DoS 防御）
   - 阶段切换（phase 变化或 isRunning 变化）通过服务端**状态指纹比对**识别，自动 bypass 节流立即广播
   - 心跳超时 30 秒，移除 player 并广播 `player_left`
5. **Step 1.5 入口 (`src/index.js`)**：
   - 监听端口 8080（或 `PORT` env）
   - 解析 ws 消息，路由到 RoomManager
   - 全局异常捕获

## 测试要求

使用 `node --test` 编写，覆盖：

- `room.test.js` — RoomManager 单元测试（createRoom 房间码唯一性、joinRoom 满员拒绝、leaveRoom 自动销毁）
- `protocol.test.js` — 消息序列化/反序列化、版本字段校验
- `integration.test.js` — 启动一个 ws server，两个客户端完整 create/join/state_update/leave 流程
- `latency.test.js` — 端对端延迟测试：客户端 A 发 state_update 到客户端 B 收到广播 **≤ 500ms**

运行：`cd Server && node --test test/`，必须全部通过。

## 验收标准

- [ ] 所有 AC-1, AC-2, AC-3, AC-5, AC-8, AC-9, AC-10（服务端部分）达成
- [ ] `node --test` 全部通过
- [ ] `package.json` 包含 `scripts.start` 和 `scripts.test`
- [ ] 代码符合方案中 Phase 1 的所有细节（DTO 字段名必须与 Unity 端对齐，避免后续协议不一致）

## 与其他 Worker 的协议对齐（关键）

Unity 端的 DTO 字段将由 Worker 2 实现，两边**必须字段名一致**。以方案 Step 2.2 的 [Serializable] DTO 定义为**权威来源**：

```typescript
// InboundMessage（服务端发送，客户端接收）
{
  v: 1,                          // protocolVersion
  type: string,                  // 消息类型
  roomCode: string,              // 可选
  playerId: string,              // 可选
  players: SnapshotEntry[],      // room_snapshot 时有
  state: RemoteState,            // player_state_broadcast 时有
  error: string                  // error 时有
}

RemoteState = {
  pomodoro: { phase, remainingSeconds, currentRound, totalRounds, isRunning },
  activeApp: null   // v2 预留，v1 始终为 null
}
```

**严禁**引入方案未定义的新字段。任何字段命名不一致会直接导致 Unity 端反序列化失败。

## 完成后的输出

在子任务 transition 到 done 之前：
1. 执行 `cd Server && node --test test/ 2>&1 | tail -50` 截取测试结果
2. 在 mailbox 发送摘要给 leader-fixed：`{"tests_passed": N, "tests_failed": N, "files_created": [...]}`
