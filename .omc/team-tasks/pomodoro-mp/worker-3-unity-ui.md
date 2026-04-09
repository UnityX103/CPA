# Subtask 3: Unity UI 层（UI Toolkit 玩家面板）

**所属方案**: `/Users/xpy/Desktop/NanZhai/CPA/.omc/plans/multiplayer-pomodoro-plan.md` 的 **Phase 4 + Phase 5 UI 测试部分**
**相关方案章节**: Phase 4（Step 4.1 - 4.7），Phase 5（Step 5.2 UI 部分）

## 文件所有权（严格独占）

你**只能**写入这些路径：

**新建（5 个）**
- `Assets/UI_V2/Documents/PlayerCard.uxml`
- `Assets/UI_V2/Styles/PlayerCard.uss`
- `Assets/UI_V2/Controller/DraggableElement.cs`
- `Assets/UI_V2/Controller/PlayerCardView.cs`
- `Assets/UI_V2/Controller/PlayerCardManager.cs`

**测试（3 个）**
- `Assets/Tests/EditMode/PlayerCardTests/PlayerCardManagerTests.cs`
- `Assets/Tests/EditMode/PlayerCardTests/DraggableElementTests.cs`
- `Assets/Tests/EditMode/PlayerCardTests/PlayerCardViewTests.cs`
- 如需要，创建 `Assets/Tests/EditMode/PlayerCardTests/PlayerCardTests.asmdef`

**修改（3 个）**
- `Assets/UI_V2/Documents/OnlineSettingsPanel.uxml` — **完全重写**（方案 Step 4.7）
- `Assets/UI_V2/Styles/OnlineSettingsPanel.uss` — **完全重写**
- `Assets/UI_V2/Controller/DeskWindowController.cs` — 集成 PlayerCardManager、修正 PointerDown 事件路由（M-5 修订）、绑定 OnlineSettingsPanel 事件

**Unity Scene 挂载**（可选）
- `Assets/Scenes/MainV2.unity` — 如必要，通过 unity-agent MCP 在 DeskWindow GameObject 上挂载 `NetworkDispatcherBehaviour`（脚本由 Worker 2 提供），以及分配 PlayerCard UXML 引用到 PlayerCardManager 字段

**严禁**写入 `Server/**`、`Assets/Scripts/APP/Network/**`、`Assets/Scripts/APP/Pomodoro/**`（包括 GameApp.cs）。

## 前置依赖（需要 Worker 2 的产物）

你的代码会引用 Worker 2 创建的以下符号：
- `APP.Network.Model.IRoomModel`
- `APP.Network.Model.RemotePlayerData`
- `APP.Network.Event.*`（如 `E_RemoteStateUpdated`, `E_PlayerJoined`, `E_PlayerLeft`, `E_RoomJoined`, `E_ConnectionStatusChanged`）
- `APP.Network.Command.Cmd_CreateRoom / Cmd_JoinRoom / Cmd_LeaveRoom`

**协调策略**：
- 如果启动时 Worker 2 的代码尚未就绪，**先只完成 UXML/USS + DraggableElement**（这些不依赖 Worker 2）
- 然后通过 mailbox 向 worker-2 发送 `status_check` 询问进度
- 待 worker-2 提交相关类后再完成 PlayerCardManager 的事件订阅逻辑
- 遇到符号未定义，**不要**自己创建占位接口；通过 mailbox 协商

## 实施要点（方案 Step 4.1 - 4.7）

1. **Step 4.1 `PlayerCard.uxml`**：
   - 结构：根 `player-card` → `card-header`（昵称）→ `card-body`（pet 占位 32×32 + 番茄钟阶段/剩余时间/轮次）
   - 仅静态结构，所有动态文本绑定通过 `PlayerCardView.cs` 用 `element.Q<Label>("...").text = ...` 更新
2. **Step 4.2 `PlayerCard.uss`**：
   - 远程卡片配色与本地 DeskWindow 视觉区分（AC-4 只读感）
   - `.player-card` 基础样式，`.player-card--running`, `.player-card--break` 等状态类
3. **Step 4.3 DraggableElement**（方案最详细，严格照抄）：
   - **PointerDown** 里 `element.CapturePointer(evt.pointerId)` + `evt.StopPropagation()` **+ `evt.PreventDefault()`（若 UI Toolkit 有）**
   - **PointerMove** 里计算 delta 更新 `style.left/top`
   - **PointerUp** 里 `ReleasePointer`
   - **PointerCaptureOutEvent** 兜底清空状态（防止意外丢失捕获）
   - 整体用 `try-catch` 包裹 handler 体，异常只 Debug.LogError 不中断
   - 边界约束基于 `element.parent.resolvedStyle.width/height`（不是 Screen.width）
   - **单指针假设**（Implementation Notes #4）：第二根手指按下会覆盖闭包变量，v1 可接受
4. **Step 4.4 `PlayerCardView.cs`**：
   - 每个 PlayerCardView 绑定一个 `RemotePlayerData`
   - 订阅 `E_RemoteStateUpdated`，diff 玩家 id 相等则更新 UI
   - `Dispose()` 时 UnRegisterEvent
5. **Step 4.5 `PlayerCardManager.cs`**：
   - 独立 `player-card-layer` VisualElement，`pickingMode = PickingMode.Ignore`（不拦截 DeskWindow 事件）
   - 订阅 `E_PlayerJoined` → 创建 PlayerCardView 并 addTo player-card-layer
   - 订阅 `E_PlayerLeft` → 从 layer 移除并 Dispose
   - 订阅 `E_RoomJoined` → 从 snapshot 创建所有已有玩家的卡片
   - 订阅 `E_ConnectionStatusChanged` → 断线清空所有卡片
   - 附加 `DraggableElement` 到每个 PlayerCardView
6. **Step 4.6 DeskWindowController 修订（M-5 最关键）**：
   - `root` 的 PointerDown handler 改为 `TrickleDown.NoTrickleDown`（冒泡阶段）
   - **循环向上**查找祖先：如果任一祖先在白名单（含 `player-card-layer`、`pomodoro-panel` 本体等）里，直接 return（不触发收纳）
   - 循环：`var e = evt.target as VisualElement; while (e != null) { if (isWhitelisted(e)) return; e = e.parent; }`
   - 保留现有 `Cmd_PomodoroTick(Time.deltaTime)` 在 Update 里
   - **新增** `this.GetSystem<IStateSyncSystem>().Tick(Time.unscaledDeltaTime)` 在 `Cmd_PomodoroTick` **之后**（方案 N-2 修订，Implementation Notes #3 说明为什么用 unscaledDeltaTime）
   - OnlineSettingsPanel 的 Create/Join/Leave 按钮绑定对应 Command
7. **Step 4.7 OnlineSettingsPanel 完全重写**：
   - UXML: 状态栏 + 昵称输入 + 房间码输入 + [创建房间][加入房间][离开房间] 按钮 + 当前房间码显示 + 连接状态指示
   - USS: 与项目现有 AUI 设计风格对齐
   - 事件绑定在 DeskWindowController 里做（OnlineSettingsPanel 只管 UXML/USS）

## 测试要求

Unity EditMode + NUnit：

- `DraggableElementTests.cs` — 模拟 PointerDown/Move/Up，验证 style.left/top 更新；异常路径不崩溃；PointerCaptureOut 清理状态
- `PlayerCardViewTests.cs` — 绑定 RemotePlayerData 后 UI 文本正确
- `PlayerCardManagerTests.cs` — 事件驱动创建/移除卡片；断线清空；白名单路由（用 fake RoomModel 模拟状态）

## 验收标准

- [ ] 所有新建/修改文件通过 Unity 编译
- [ ] EditMode 测试全部通过
- [ ] DeskWindowController 保留原有番茄钟功能，拖拽不触发收纳
- [ ] AC-4, AC-6, AC-7 达成

## 完成后的输出

1. 运行 `mcp__plugin_oh-my-claudecode_t__lsp_diagnostics_directory` 对 `Assets/UI_V2/` 做诊断
2. 在 mailbox 发送摘要给 leader-fixed：`{"files_created": N, "files_modified": N, "compile_errors": 0}`
