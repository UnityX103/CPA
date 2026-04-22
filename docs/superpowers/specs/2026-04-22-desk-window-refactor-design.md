# DeskWindow 透明全屏主面板 + 独立设置面板 重构设计

**日期**：2026-04-22
**范围**：Pencil 设计稿 + Unity UI Toolkit（UXML/USS）+ 表现层代码（Controller/System/Model）
**前置决策**：brainstorming Q1–Q6（在本文件"决策摘要"一节列出）

---

## 1. 概述

把现在带描边、带背景、内嵌 ScrollView 卡片列表的 `DeskWindow` 主面板，重构为**透明全屏容器**，里面放两类**独立可拖、独立持久化位置**的子元素：

- **番茄钟面板（YRqeB）**：带自己的 handleBar（拖拽把手 + 设置按钮），持久化位置
- **玩家卡片（drqFB）**：每张卡顶部带 handleBar，按 PlayerId 持久化位置；新卡按"右侧相邻 + 自动换行"规则首次摆放

设置面板（vnYnS）从当前的 `settings-overlay`（同画布覆盖层）升级为**独立 UIDocument + 独立 PanelSettings（高 SortOrder）**。

## 2. 决策摘要

| 编号 | 决策 |
|------|------|
| Q1 | 番茄钟位置 + 每个玩家卡位置都**跨会话持久化**（玩家按 PlayerId 映射） |
| Q2 | 设置面板 = **独立 UIDocument + 独立 PanelSettings（高 SortOrder）** |
| Q3 | 玩家卡片**顶部新增独立 handleBar**（对齐番茄钟的 handleBar 做法） |
| Q4a | 新卡默认摆在"上一张卡的**右侧**"：`newX = prevX + cardWidth + gap` |
| Q4b | 无"上一张"时，**第一张卡固定锚点 = (40, 40)**（屏幕左上） |
| Q4c | 右边界溢出时**自动换行**到下一行：`y += cardHeight + gap`，x 回到 40 |
| Q4d | 返回玩家（已有持久化位置）**恢复上次位置**；无持久化记录的新玩家走"隔壁规则" |
| Q5 | 拖拽**硬 clamp 到屏幕内**（保持现有 DraggableElement 语义） |
| Q6a | 番茄钟初始位置 = **屏幕右下角**（距右/下各 20px） |
| Q6b | 第一张玩家卡固定锚点 = **屏幕左上 (40, 40)** |
| Q6c | 移除 DeskWindow 顶部的 `drag-handle` 与 `settings-btn`（迁入番茄钟 handleBar） |
| Q6d | **新建 `PomodoroPanelPosition: BindableProperty<Vector2>`**（原 `WindowAnchor` 是 Top/Bottom 枚举，语义不同，保留不动） |

## 3. 架构：QFramework 分层映射

### 3.1 场景结构

Hierarchy 顶层挂两个 `UIDocument`（分属不同 GameObject）：

- `DeskWindow (UIDocument #1)`：主透明容器；source = 新 DeskWindow.uxml（本次重构后）
- `UnifiedSettingsPanel (UIDocument #2)`：独立渲染层；source = 新 UnifiedSettingsPanel.uxml（把原 DeskWindow 的 settings-overlay 抽出来）

两份 `PanelSettings` 资源（`PanelSettings_Main.asset`、`PanelSettings_Settings.asset`），后者 SortOrder 更高（例如 10），确保设置面板渲染在主面板之上。

### 3.2 层级职责

| 层 | 类 | 职责 |
|---|---|---|
| Controller | `DeskWindowController`（改） | 主 UIDocument 的根绑定；实例化 `PomodoroPanelView` / `PlayerCardManager`；订阅"请求打开设置"事件，调用独立设置面板 UIDocument |
| Controller | `UnifiedSettingsPanelController`（改） | 从 overlay 改为挂在独立 UIDocument 的根上；Show/Hide 时开关独立 UIDocument 的 `rootVisualElement.style.display` 或整体 enabled |
| Controller | `PomodoroPanelView`（改） | 增加 handleBar 识别（`#pp-handle-bar`、`#pp-handle-drag`、`#pp-settings-btn`）；读/写 `PomodoroPanelPosition` 并驱动 DraggableElement |
| Controller | `PlayerCardManager`（改） | 卡片不再塞进 ScrollView；改为直接挂到主面板根上；新卡摆放规则走新算法；保存位置变更 |
| Controller | `PlayerCardController`（改） | 绑定新 handleBar；拖拽时回写 `IPlayerCardPositionModel`（通过 Command） |
| Command | `Cmd_SetPomodoroPanelPosition`（新） | 写入 `IPomodoroModel.PomodoroPanelPosition` |
| Command | `Cmd_SetPlayerCardPosition`（新） | 写入 `IPlayerCardPositionModel[playerId]` |
| Command | `Cmd_OpenUnifiedSettings` / `Cmd_CloseUnifiedSettings`（新） | 发 Event `E_OpenUnifiedSettings` / `E_CloseUnifiedSettings`，由持有独立 UIDocument 的 `UnifiedSettingsPanelDriver` 订阅后切换显隐（见 §7） |
| Model | `IPomodoroModel`（改） | 新增 `BindableProperty<Vector2> PomodoroPanelPosition` |
| Model | `IPlayerCardPositionModel`（新） | 存 `Dictionary<string, Vector2>`；CRUD + 变更事件 |
| System | 无新 System | 位置持久化复用 `PomodoroPersistence` 与 `IStorageUtility`（Model 内部 Register 触发 Save） |
| Event | `E_PomodoroPanelPositionChanged` / `E_PlayerCardPositionChanged`（新，可选） | 如果 Model 自身的 BindableProperty 订阅够用，这两个事件可不建 |

### 3.3 持久化

- `PomodoroPanelPosition` 通过 `PomodoroPersistence.Save()` 一起落盘，字段加在现有 JSON schema 尾部（向后兼容：旧存档缺字段时回退到 Q6a 默认值 (screen.width - panelWidth - 20, screen.height - panelHeight - 20)）
- `IPlayerCardPositionModel` 用独立 `PlayerCardPositions.json` 经 `IStorageUtility` 持久化（`Dictionary<string, Vector2>` 序列化为 `{ "playerId": {x, y}, ... }`）
- 触发时机：Model 内部 `BindableProperty.Register(_ => storage.Save(...))`，与现有 Pomodoro 持久化风格一致
- 存储介质：沿用现有 `PlayerPrefsStorageUtility`（不新建独立 JSON 文件），两组数据都作为 PlayerPrefs key 保存

## 4. Pencil 设计稿改动

### 4.1 84Qri（主面板）

- **移除**：外边框描边、填充背景（外边框 border 1px rgb(241,229,216)、圆角 28px 都删掉）
- **移除**：顶部 `handle-bar`（含 •••、settings 齿轮）
- **移除**：横向 ScrollView `card-list` 容器——卡片直接摆在 84Qri 自身上
- **保留**：仅作为"设计时的全屏画布参考"存在，padding 清 0；在 Pencil 里视觉上就是一个透明全屏矩形

### 4.2 YRqeB（番茄钟面板）

- **新增**：顶部 `handleBar`（子节点名 `pp-handle-bar`），内含：
  - 左：占位 spacer（flex-grow:1）
  - 中：三点拖拽指示（`pp-handle-drag`，text="•••"，cursor:move）
  - 右：设置按钮（`pp-settings-btn`，齿轮图标，28×28 圆角 11px，沿用现 `.settings-btn` 样式）
- 其余（pp-title/pp-streak/pp-clock/pp-actions）不变

### 4.3 drqFB（玩家卡片）

- **新增**：顶部 `pc-handle-bar`（子节点），一行高度约 16–20px
  - 视觉：小幅内缩、浅灰三点（与 YRqeB 风格一致但更小）
  - cursor:move
- **原 pc-head/pc-divider/pc-footer 结构不变**；整体卡片高度从 97 增加到约 113（具体数值看 Pencil 排版后确定）

### 4.4 vnYnS（设置面板）

- 本身结构不变（Pencil 已经有这个独立组件）
- 在 Unity 侧成为独立 UIDocument 的 source，不再是 84Qri 的 overlay

## 5. UXML / USS 改动

### 5.1 `DeskWindow.uxml`

- 根改为 `#dw-canvas`（类 `.dw-canvas`）——纯空容器
- 移除：`dw-wrap`、`handle-bar`、`content-row`、`card-list`、`settings-overlay` 节点
- 新结构：
  ```xml
  <ui:VisualElement name="dw-canvas" class="dw-canvas">
    <ui:Instance name="pomodoro-panel" template="PomodoroPanel" class="dw-floating" />
    <ui:VisualElement name="card-layer" class="dw-card-layer" />
  </ui:VisualElement>
  ```
- 卡片直接挂在 `#card-layer` 上（绝对定位）；`pomodoro-panel` 也绝对定位

### 5.2 `DeskWindow.uss`

- **删除**：`.dw-wrap`（描边+圆角+padding）
- **删除**：`.handle-bar`、`.handle-spacer`、`.handle-right`、`.drag-handle`、`.settings-btn`（整体 handle 区）
- **删除**：`.card-list`（ScrollView 样式）
- **删除**：`.settings-overlay`、`.settings-header`、`.settings-title`、`.settings-sidebar`、`.sidebar-tab*`、`.settings-content*`（迁移到 UnifiedSettingsPanel.uss）
- **新增**：
  ```css
  .dw-canvas {
      position: absolute;
      left: 0; top: 0; right: 0; bottom: 0;
      /* 无 background、无 border */
  }
  .dw-floating { position: absolute; }  /* 所有可拖子元素共享 */
  .dw-card-layer { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }
  ```
- `.dw-root-anchor` 保留（给根 VisualElement 用）

### 5.3 `PomodoroPanel.uxml`

- 在 `#pp-root` 内**最前**新增：
  ```xml
  <ui:VisualElement name="pp-handle-bar" class="pp-handle-bar">
    <ui:VisualElement class="pp-handle-spacer"/>
    <ui:Label name="pp-handle-drag" text="•••" class="pp-handle-drag"/>
    <ui:VisualElement class="pp-handle-right">
      <ui:VisualElement name="pp-settings-btn" class="pp-settings-btn">
        <ui:Label class="pp-settings-btn-icon"/>
      </ui:VisualElement>
    </ui:VisualElement>
  </ui:VisualElement>
  ```
- `.pp-root` 加 `position: absolute`，供 DraggableElement 写入 left/top
- `.pp-root` 需要显式 `width/height`（否则 absolute 元素收缩）；实现阶段第一步用 `mcp__pencil__batch_get` 读出 YRqeB 新尺寸（含新增 handleBar 后的总高），写进 USS

### 5.4 `PomodoroPanel.uss`

- 复用原 `.handle-bar` / `.drag-handle` / `.settings-btn` 样式规则，前缀统一加 `.pp-`
- `.pp-root { position: absolute; }` 与 `width/height`

### 5.5 `PlayerCard.uxml`

- 在 `<ui:VisualElement class="pc-root">` 内**最前**新增：
  ```xml
  <ui:VisualElement name="pc-handle-bar" class="pc-handle-bar">
    <ui:Label text="•••" class="pc-handle-drag"/>
  </ui:VisualElement>
  ```
- `.pc-root` 改为 `position: absolute`，代码写入 left/top
- 高度从 97 → 约 113（handleBar ≈ 16px）

### 5.6 `PlayerCard.uss`

- `.pc-root { position: absolute; margin-right: 0; }`（原 `margin-right:12` 为 ScrollView 场景用，现移除）
- 新增 `.pc-handle-bar`（高约 16px、居中 `•••`、cursor:move、flex-direction:row align-items:center justify-content:center）与 `.pc-handle-drag`（浅灰三点）

### 5.7 新文件 `UnifiedSettingsPanel.uxml` / `UnifiedSettingsPanel.uss`

把当前 DeskWindow.uxml 中 `settings-overlay` 及子树整体迁到这里；UXML 根节点成为独立 UIDocument 的根。节点 id 保持 `#settings-overlay` / `#settings-content-host` / `#tab-pomodoro` 等不变，避免 Controller 查询改动过多。

## 6. 表现层代码改动

### 6.1 Model

**`IPomodoroModel` / `PomodoroModel`**（修改）：
- 新增 `BindableProperty<Vector2> PomodoroPanelPosition`
  - 默认值：`Vector2.negativeInfinity` 哨兵（表示"用屏幕默认右下角算出来"）
  - `OnInit` 里从 `PomodoroPersistence` 读；变更时 `Register(_ => storage.Save(...))`

**`IPlayerCardPositionModel` / `PlayerCardPositionModel`**（新）：
```csharp
public interface IPlayerCardPositionModel : IModel
{
    bool TryGet(string playerId, out Vector2 pos);
    void Set(string playerId, Vector2 pos);
    void Remove(string playerId);
}
```
内部：`Dictionary<string, Vector2>` + `IStorageUtility` 持久化（key = `"PlayerCardPositions"`）。

`GameApp.Init` 注册新 Model。

### 6.2 Command

- **`Cmd_SetPomodoroPanelPosition(Vector2)`**：写 `IPomodoroModel.PomodoroPanelPosition`
- **`Cmd_SetPlayerCardPosition(string playerId, Vector2)`**：写 `IPlayerCardPositionModel`
- **`Cmd_OpenUnifiedSettings` / `Cmd_CloseUnifiedSettings`**：发 Event `E_OpenUnifiedSettings` / `E_CloseUnifiedSettings`，由独立 UIDocument 上的 Controller 订阅后切换显隐

### 6.3 Controller

**`DeskWindowController`**：
- 删除：`_playerCardTemplate` 以外的设置面板模板字段、`EnsureSettingsTemplatesLoaded`、`_settingsPanel`、`settings-btn` 回调
- `BindUI` 只做：根添加 `.dw-root-anchor`；Q 出 `pomodoro-panel`、`card-layer`；初始化 `PomodoroPanelView`（把 handleBar 拖拽 + 初始位置 + 持久化回写都在 View 内）；初始化 `PlayerCardManager`（传入 `card-layer` 而非 ScrollView）
- 保留 Update/Focus/Quit 的现有逻辑

**`PomodoroPanelView`**（修改）：
- `Init(TemplateContainer)` 之后新增：
  - 查 `#pp-handle-bar` / `#pp-handle-drag` / `#pp-settings-btn`
  - `DraggableElement.MakeDraggable(pp-root, pp-handle-bar)`
  - 订阅拖拽结束：读出最终 `left/top`，`SendCommand(new Cmd_SetPomodoroPanelPosition(pos))`
  - 订阅 `model.PomodoroPanelPosition.RegisterWithInitValue` → 写 style.left/top
  - 首次启动（值为 NegativeInfinity）时，在 `GeometryChangedEvent` 里拿到自身尺寸 + 屏幕尺寸，算出右下角锚点并 `SendCommand` 写回（也会触发 Save）
  - `pp-settings-btn.RegisterCallback<PointerUpEvent>(_ => SendCommand(new Cmd_OpenUnifiedSettings()))`

**`PlayerCardManager`**（修改）：
- `Initialize` 签名：`VisualElement cardLayer`（替换原 `ScrollView contentContainer`）
- `AddOrUpdate`：
  1. CloneTree 卡片；根 `pc-root` 加 `.dw-floating`
  2. 查询 `IPlayerCardPositionModel.TryGet(playerId)` → 命中则用该坐标；miss 则走 "下一空位"算法（见 §6.4）
  3. 写入 `pc-root.style.left/top`
  4. `new PlayerCardController(root)` + `Setup(data)`
  5. 给 `pc-handle-bar` 绑 `DraggableElement.MakeDraggable(pc-root, pc-handle-bar)`；拖拽结束回调 `SendCommand(new Cmd_SetPlayerCardPosition(playerId, newPos))`

**`PlayerCardController`**（修改）：
- 暴露 `Root` 已有；新增"知道自己的 PlayerId"（构造时传入）以便拖拽回调写 Command

**`UnifiedSettingsPanelController`**（修改）：
- `Init` 不再传 DeskWindow 的 root；改成接收独立 UIDocument 的 rootVisualElement
- 暴露静态入口或用 Event 订阅 `E_OpenUnifiedSettings` / `E_CloseUnifiedSettings`（推荐事件，解耦更干净）
- 挂在 `UnifiedSettingsPanelDriver`（新 MonoBehaviour，持有第二个 UIDocument）上，Awake/Start 里注册 Controller、订阅 Event

### 6.4 玩家卡片"下一空位"算法（4a + 4b + 4c）

```
input:  joinedOrder  = List<Vector2> 主面板上卡片"按加入顺序"的左上角坐标
                        (时间序，不是空间序；包含从持久化恢复的卡片，按它们的恢复顺序入列)
        cardSize     = Vector2(cardW, cardH)
        screen       = Vector2(screenW, screenH)
        gap          = 12
        firstAnchor  = Vector2(40, 40)

algorithm NextSlot:
    if joinedOrder.empty:
        return firstAnchor
    prev = joinedOrder.last                   // 最后一张进入/恢复的卡片
    candidate.x = prev.x + cardW + gap
    candidate.y = prev.y
    if candidate.x + cardW > screenW - 20:   // 右边界预留 20
        candidate.x = firstAnchor.x           // 换行
        candidate.y = prev.y + cardH + gap
    // 若超下边界，硬 clamp（最后一行允许堆叠，符合 Q5=A）
    candidate.y = min(candidate.y, screenH - cardH - 20)
    return candidate
```

`PlayerCardManager` 内部维护 `List<string> _joinOrder`，`AddOrUpdate` 时 append、`Remove` 时 erase，由它驱动上面的 `joinedOrder`。

Q4d 规则体现在调用点：先 `IPlayerCardPositionModel.TryGet(playerId)`，miss 时才调 `NextSlot`；得到位置后再 `Cmd_SetPlayerCardPosition` 回写（让新玩家首次位置也进入持久化）。

玩家离开（`E_PlayerLeft`）时：**不删除**持久化记录（便于 Q4d 恢复）。仅从主面板移除 VisualElement。

### 6.5 拖拽结束事件

`DraggableElement.DragController` 增加 `event Action<Vector2> OnDragEnd`（或直接传 callback），在 `OnPointerUp` / `OnPointerCaptureOut` 里触发。不改现有 API，新增 optional 参数 / 属性，向后兼容。

## 7. 独立设置面板的事件流

1. 番茄钟 handleBar 的设置按钮被点击 → `Cmd_OpenUnifiedSettings` → 发 `E_OpenUnifiedSettings`
2. `UnifiedSettingsPanelDriver`（挂在设置 UIDocument 的 GameObject 上）订阅该 Event → 调 `_controller.Show()` → 设置 UIDocument `rootVisualElement.style.display = Flex`
3. 关闭按钮（`settings-close`）→ `Cmd_CloseUnifiedSettings` → Event → `Hide()`
4. Q2 的 PanelSettings SortOrder 由两份资源静态配置，不在运行时切换

**为什么用 Command + Event 而不直接引用**：符合 QFramework "下层→上层只能发 Event" 的分层规则；`DeskWindowController` 不持有 `UnifiedSettingsPanelDriver` 引用，解耦两个 UIDocument。

## 8. 持久化 Schema

- `PomodoroPersistence` 现有 JSON 追加：
  ```json
  { "...": "...", "pomodoroPanelPosition": { "x": 1520.0, "y": 820.0 } }
  ```
  读档缺字段 → 值为 `Vector2.negativeInfinity`（首帧再算默认右下角）
- `PlayerPrefs` key = `"CPA.PlayerCardPositions"`，value 为 JSON 字符串 `{ "playerId-a": {"x":40,"y":40}, "playerId-b": {"x":205,"y":40} }`（通过现有 `IStorageUtility.Save/Load` 序列化，不新建文件）

## 9. 测试策略

现有测试位置：
- `Assets/Tests/EditMode/PlayerCardTests/` → 沿用
- `Assets/Tests/PlayMode/NetworkIntegration/` → `UnifiedSettingsPanelImageValidationTests`、`PlayerCardIntegrationTests`

改动：
- `PlayerCardManagerTests`：更新 `Initialize` 签名（`cardLayer` 替 ScrollView）、新增"首卡锚点 / 隔壁规则 / 自动换行 / 恢复持久化位置"4 个测试
- `DraggableElementTests`：现有拖拽 clamp 测试沿用
- 新增 `PlayerCardPositionModelTests`（EditMode）：TryGet/Set/Remove + 持久化 roundtrip
- 新增 `PomodoroPanelViewPositionTests`（EditMode）：首次启动 NegativeInfinity → 算出右下锚点；拖拽后回写 Command
- `UnifiedSettingsPanelImageValidationTests`：迁移到新 UIDocument 宿主后的视觉基线需要重新抓一张；跑 `unity-visual-image-validation` 技能对比

## 10. 向后兼容 / 迁移

- `PomodoroWindowAnchor`（Top/Bottom 枚举）保留，`WindowPositionSystem` 不改——它管的是 OS 窗口位置，和本次 UI 内位置正交
- 旧存档缺 `pomodoroPanelPosition` → fallback 到默认右下角
- 无 `PlayerCardPositions` 文件 → 空 dict，所有卡片都走"隔壁规则"首次摆放

## 11. 范围外（明确不做）

- 不做多选/群拖
- 不做对齐吸附、网格吸附
- 不做"重置所有位置"入口（Q5=A 硬 clamp 已保证卡不会消失）
- 不动 `UniWindowController` 透明/点击穿透配置
- 不改 `WindowPositionSystem`（OS 级窗口位置不受影响）

## 12. 交付物清单（供 writing-plans 拆步）

| 类型 | 文件 |
|------|------|
| Pencil | 84Qri 去描边/背景；YRqeB 新增 handleBar + 设置按钮；drqFB 新增 handleBar |
| Scene | Hierarchy 新增 `UnifiedSettingsPanel` GameObject + UIDocument + PanelSettings_Settings.asset |
| Asset | 新增 `PanelSettings_Settings.asset`（SortOrder 10） |
| UXML（改） | `DeskWindow.uxml`、`PomodoroPanel.uxml`、`PlayerCard.uxml` |
| UXML（新） | `UnifiedSettingsPanel.uxml` |
| USS（改） | `DeskWindow.uss`、`PomodoroPanel.uss`、`PlayerCard.uss` |
| USS（新） | `UnifiedSettingsPanel.uss` |
| Model（改） | `IPomodoroModel` / `PomodoroModel`（+ `PomodoroPanelPosition` 字段 + 持久化 schema） |
| Model（新） | `IPlayerCardPositionModel` / `PlayerCardPositionModel` |
| Command（新） | `Cmd_SetPomodoroPanelPosition`、`Cmd_SetPlayerCardPosition`、`Cmd_OpenUnifiedSettings`、`Cmd_CloseUnifiedSettings` |
| Event（新） | `E_OpenUnifiedSettings`、`E_CloseUnifiedSettings`（可选：位置变更 Event） |
| Controller（改） | `DeskWindowController`、`PomodoroPanelView`、`PlayerCardManager`、`PlayerCardController`、`UnifiedSettingsPanelController` |
| Controller（新） | `UnifiedSettingsPanelDriver`（MonoBehaviour，承载第二个 UIDocument） |
| Tests（改） | `PlayerCardManagerTests`、`UnifiedSettingsPanelImageValidationTests`（基线重抓） |
| Tests（新） | `PlayerCardPositionModelTests`、`PomodoroPanelViewPositionTests` |
| 文档 | 本文件；commit 到 git |
