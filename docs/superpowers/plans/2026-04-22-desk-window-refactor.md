# DeskWindow 透明全屏主面板 + 独立设置面板 重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal**: 把 DeskWindow 主面板重构为透明全屏容器，内部放两类独立可拖、位置持久化的子元素（番茄钟面板 YRqeB、玩家卡片 drqFB）；设置面板 vnYnS 升级为独立 UIDocument + 独立 PanelSettings。

**Architecture**: QFramework 四层分层（Utility/Model/System/Controller）不变；新增 `IPlayerCardPositionModel` 管理 per-PlayerId 位置；`PomodoroModel` 新增 `PomodoroPanelPosition` 字段并入 JSON schema；主场景新增第二个 UIDocument + 独立 `PanelSettings` 承载设置面板，靠 SortOrder 10 叠在主面板之上；跨 UIDocument 通信经 QFramework Event（`Cmd_Open/CloseUnifiedSettings` → `E_Open/CloseUnifiedSettings`）；拖拽结束通过 `DraggableElement.OnDragEnd` 回调 `SendCommand` 写位置。

**Tech Stack**: Unity 6000.0.25f1、UI Toolkit、QFramework v1.0、Pencil MCP（设计稿）、UnityMCP（编辑器操作）、NUnit（EditMode 测试）、Unity Test Framework（PlayMode 测试）。

**Spec**: `docs/superpowers/specs/2026-04-22-desk-window-refactor-design.md`

---

## 文件与模块清单

| 类型 | 路径 | 职责 |
|------|------|------|
| Model（改） | `Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs` `.../PomodoroModel.cs` | 加 `PomodoroPanelPosition` |
| Model（新） | `Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs` `.../PlayerCardPositionModel.cs` | 按 PlayerId 存 Vector2 |
| Persistence（改） | `PomodoroModel.cs` 内 `PomodoroPersistence` | JSON schema 追加位置 |
| Command（新） | `.../Command/Cmd_SetPomodoroPanelPosition.cs` `Cmd_SetPlayerCardPosition.cs` `Cmd_OpenUnifiedSettings.cs` `Cmd_CloseUnifiedSettings.cs` | 写位置 / 开关设置 |
| Event（新） | `.../Event/UnifiedSettingsEvents.cs` | `E_OpenUnifiedSettings` / `E_CloseUnifiedSettings` |
| GameApp（改） | `Assets/Scripts/APP/GameApp.cs` | 注册 `IPlayerCardPositionModel` |
| DraggableElement（改） | `Assets/UI_V2/Controller/DraggableElement.cs` | 加 `OnDragEnd` 回调 |
| Pencil（改） | `AUI/PUI.pen` 节点 84Qri / YRqeB / drqFB | 去描边、加 handleBar |
| UXML（改） | `Assets/UI_V2/Documents/DeskWindow.uxml` `PomodoroPanel.uxml` `PlayerCard.uxml` | 结构调整 |
| UXML（新） | `Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml` | 设置面板独立 |
| USS（改） | `Assets/UI_V2/Styles/DeskWindow.uss` `PomodoroPanel.uss` `PlayerCard.uss` | 样式同步 |
| USS（新） | `Assets/UI_V2/Styles/UnifiedSettingsPanel.uss` | 迁入 overlay 相关规则 |
| Asset（新） | `Assets/UI_V2/PanelSettings_Settings.asset` | 独立 PanelSettings |
| Scene（改） | 主场景（Assets 下查找） | 新增 UnifiedSettingsPanel GameObject + UIDocument |
| Controller（改） | `DeskWindowController.cs` `UnifiedSettingsPanelController.cs` `PomodoroPanelView.cs` `PlayerCardController.cs` `PlayerCardManager.cs` | 适配新结构 |
| Controller（新） | `Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs` | 承载第二个 UIDocument |
| Tests（改） | `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardManagerTests.cs` | 新签名 + 新布局测试 |
| Tests（新） | `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs` `PomodoroPanelPositionTests.cs` `DraggableElementOnDragEndTests.cs` | 覆盖新能力 |
| Tests（基线） | `Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests` | 基线重抓 |

---

## Phase A — Model & Persistence 基础

### Task 1：给 `IPomodoroModel` 加 `PomodoroPanelPosition` 字段

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs`
- Modify: `Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs`
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs`

Sentinel 约定：`Vector2.negativeInfinity` 表示"无持久化记录"，由 View 首帧算出默认坐标再回写。

- [ ] **Step 1: 写失败测试（首次读取返回 sentinel）**

```csharp
// Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs
using APP.Pomodoro.Model;
using NUnit.Framework;
using UnityEngine;

namespace APP.Pomodoro.Tests
{
    public sealed class PomodoroPanelPositionTests
    {
        [Test]
        public void DefaultValue_IsNegativeInfinity_ActingAsSentinel()
        {
            var model = new PomodoroModel();
            ((IModel)model).Init(); // AbstractModel.Init 会走 OnInit

            Vector2 pos = model.PomodoroPanelPosition.Value;
            Assert.That(float.IsNegativeInfinity(pos.x), "x 应为 -Infinity");
            Assert.That(float.IsNegativeInfinity(pos.y), "y 应为 -Infinity");
        }

        [Test]
        public void SetValue_PersistsInMemory()
        {
            var model = new PomodoroModel();
            ((IModel)model).Init();

            var target = new Vector2(100f, 200f);
            model.PomodoroPanelPosition.Value = target;

            Assert.That(model.PomodoroPanelPosition.Value, Is.EqualTo(target));
        }
    }
}
```

注意 `IModel.Init()` 需要 `using QFramework;`。

- [ ] **Step 2: 运行测试确认失败**

通过 Unity MCP：`run_tests`，`testNames: ["APP.Pomodoro.Tests.PomodoroPanelPositionTests.DefaultValue_IsNegativeInfinity_ActingAsSentinel"]`。

期望：FAIL，信息 `IPomodoroModel 不含 PomodoroPanelPosition`（编译错）。

- [ ] **Step 3: 给 Interface 加字段**

修改 `IPomodoroModel.cs`，在 `CompletionClipIndex` 下方新增：

```csharp
        /// <summary>选中的完成音效索引</summary>
        BindableProperty<int> CompletionClipIndex { get; }

        /// <summary>
        /// 番茄钟面板（YRqeB）在全屏主面板坐标系内的左上角位置。
        /// 值为 Vector2.negativeInfinity 时代表"未初始化"——View 首帧计算默认右下角锚点后回写。
        /// </summary>
        BindableProperty<Vector2> PomodoroPanelPosition { get; }
```

文件顶端加 `using UnityEngine;`（`BindableProperty<Vector2>` 需要）。

- [ ] **Step 4: 给 PomodoroModel 实现该字段**

`PomodoroModel.cs` 类体内加：

```csharp
        public BindableProperty<Vector2> PomodoroPanelPosition { get; }
            = new BindableProperty<Vector2>(new Vector2(float.NegativeInfinity, float.NegativeInfinity));
```

文件顶端加 `using UnityEngine;`（如未有）。

- [ ] **Step 5: 运行测试确认通过**

`run_tests`，两条都跑。期望：PASS。再 `read_console filter types: error` 确认无编译错。

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/APP/Pomodoro/Model/IPomodoroModel.cs \
        Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs.meta
git commit -m "feat(pomodoro): 新增 PomodoroPanelPosition 字段与 sentinel 默认值

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 2：`PomodoroPersistence` JSON schema 追加 `pomodoroPanelPosition`

**Files:**
- Modify: `Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs`（`PomodoroPersistentState` + `Save` + `ApplyState`）
- Modify: `Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs`

向后兼容策略：旧存档缺该字段 → `JsonUtility.FromJson` 得到 0/0；因此存档本身额外记 `bool HasPomodoroPanelPosition` 布尔哨兵，缺字段时视为未初始化 → 回写 sentinel。

- [ ] **Step 1: 新增持久化往返测试**

追加到 `PomodoroPanelPositionTests.cs`：

```csharp
        [Test]
        public void Persistence_SaveAndLoad_RestoresPomodoroPanelPosition()
        {
            // 清 key 避免跨用例污染
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");

            var save = new PomodoroModel();
            ((IModel)save).Init();
            save.PomodoroPanelPosition.Value = new Vector2(512.5f, 768.25f);

            PomodoroPersistence.Save(save, flushToDisk: true);

            var load = new PomodoroModel();
            ((IModel)load).Init();
            bool ok = PomodoroPersistence.TryLoad(load);

            Assert.That(ok, Is.True, "TryLoad 应成功");
            Assert.That(load.PomodoroPanelPosition.Value.x, Is.EqualTo(512.5f).Within(0.001f));
            Assert.That(load.PomodoroPanelPosition.Value.y, Is.EqualTo(768.25f).Within(0.001f));

            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [Test]
        public void Persistence_LegacySave_MissingField_LeavesSentinel()
        {
            // 模拟旧版无该字段的 JSON
            const string legacyJson =
                "{\"FocusDurationSeconds\":1500,\"BreakDurationSeconds\":300,\"TotalRounds\":4," +
                "\"CurrentRound\":1,\"RemainingSeconds\":1500,\"CurrentPhase\":0,\"IsRunning\":false," +
                "\"IsTopmost\":false,\"WindowAnchor\":1,\"AutoJumpToTopOnComplete\":true," +
                "\"AutoStartBreak\":true,\"TargetMonitorIndex\":0,\"CompletionClipIndex\":0}";
            PlayerPrefs.SetString("APP.Pomodoro.PersistentState.v1", legacyJson);
            PlayerPrefs.Save();

            var model = new PomodoroModel();
            ((IModel)model).Init();
            PomodoroPersistence.TryLoad(model);

            Assert.That(float.IsNegativeInfinity(model.PomodoroPanelPosition.Value.x),
                "旧存档缺字段时应保持 sentinel，由 View 首帧算默认位置");

            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }
```

- [ ] **Step 2: 运行测试确认失败**

`run_tests`，`testNames: ["APP.Pomodoro.Tests.PomodoroPanelPositionTests.Persistence_SaveAndLoad_RestoresPomodoroPanelPosition"]`。期望：FAIL（字段未写入 JSON）。

- [ ] **Step 3: 扩展 `PomodoroPersistentState`**

在 `PomodoroModel.cs` 的 `PomodoroPersistentState` 类末尾追加：

```csharp
        // 番茄钟面板位置（UI 坐标系）。旧存档解析出 (0,0)，靠 HasPomodoroPanelPosition 区分"未设置"
        public float PomodoroPanelPositionX;
        public float PomodoroPanelPositionY;
        public bool  HasPomodoroPanelPosition;
```

- [ ] **Step 4: `Save()` 写入新字段**

`Save` 里构造 `state` 时追加：

```csharp
                PomodoroPanelPositionX = float.IsNegativeInfinity(model.PomodoroPanelPosition.Value.x) ? 0f : model.PomodoroPanelPosition.Value.x,
                PomodoroPanelPositionY = float.IsNegativeInfinity(model.PomodoroPanelPosition.Value.y) ? 0f : model.PomodoroPanelPosition.Value.y,
                HasPomodoroPanelPosition = !float.IsNegativeInfinity(model.PomodoroPanelPosition.Value.x)
                                         && !float.IsNegativeInfinity(model.PomodoroPanelPosition.Value.y),
```

- [ ] **Step 5: `ApplyState()` 读取字段**

在 `ApplyState` 末尾追加：

```csharp
            model.PomodoroPanelPosition.Value = state.HasPomodoroPanelPosition
                ? new Vector2(state.PomodoroPanelPositionX, state.PomodoroPanelPositionY)
                : new Vector2(float.NegativeInfinity, float.NegativeInfinity);
```

- [ ] **Step 6: 运行测试确认通过**

`run_tests`，两条持久化测试都跑。期望：PASS。

- [ ] **Step 7: 提交**

```bash
git add Assets/Scripts/APP/Pomodoro/Model/PomodoroModel.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs
git commit -m "feat(pomodoro): 持久化 PomodoroPanelPosition，兼容旧存档缺字段

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 3：创建 `IPlayerCardPositionModel` + `PlayerCardPositionModel`

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs`
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs`

PlayerPrefs key：`"CPA.PlayerCardPositions"`，值为 JSON 字符串 `{ "entries": [ { "id":"p1", "x":40, "y":40 } ] }`（`JsonUtility` 不支持 `Dictionary<string, Vector2>`，所以包装成数组）。

- [ ] **Step 1: 写失败测试**

```csharp
// Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs
using APP.Pomodoro.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardPositionModelTests
    {
        private const string TestKey = "CPA.PlayerCardPositions";

        [SetUp] public void SetUp()    { PlayerPrefs.DeleteKey(TestKey); }
        [TearDown] public void TearDown() { PlayerPrefs.DeleteKey(TestKey); }

        [Test]
        public void TryGet_UnknownPlayer_ReturnsFalse()
        {
            var model = CreateModel();
            Assert.That(model.TryGet("nobody", out _), Is.False);
        }

        [Test]
        public void Set_ThenTryGet_ReturnsStoredPosition()
        {
            var model = CreateModel();
            model.Set("p1", new Vector2(40f, 40f));

            Assert.That(model.TryGet("p1", out Vector2 pos), Is.True);
            Assert.That(pos, Is.EqualTo(new Vector2(40f, 40f)));
        }

        [Test]
        public void Remove_RemovesEntry()
        {
            var model = CreateModel();
            model.Set("p1", new Vector2(1f, 2f));
            model.Remove("p1");
            Assert.That(model.TryGet("p1", out _), Is.False);
        }

        [Test]
        public void Set_Persists_AcrossModelInstances()
        {
            var m1 = CreateModel();
            m1.Set("alice", new Vector2(100f, 200f));
            PlayerPrefs.Save();

            var m2 = CreateModel();
            Assert.That(m2.TryGet("alice", out Vector2 pos), Is.True);
            Assert.That(pos, Is.EqualTo(new Vector2(100f, 200f)));
        }

        // 每个测试用独立的 GameApp-like 容器避免污染
        private static IPlayerCardPositionModel CreateModel()
        {
            var model = new PlayerCardPositionModel();
            var arch = new TestArchitecture();
            arch.RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());
            arch.RegisterModel<IPlayerCardPositionModel>(model);
            arch.InitArchitecture();
            return model;
        }

        private sealed class TestArchitecture : Architecture<TestArchitecture>
        {
            protected override void Init() { }
            public void InitArchitecture() => base.InitArchitecture();
        }
    }
}
```

注：`Architecture<T>` 的 `InitArchitecture` 是 protected，所以需要上面这个暴露包装。若 QFramework 版本差异导致上面方法不存在，改为 `var _ = TestArchitecture.Interface;` 触发懒加载，并在 `Init()` 里直接注册。

- [ ] **Step 2: 运行测试确认失败**

`run_tests`，`testNames: ["APP.Pomodoro.Tests.PlayerCardPositionModelTests.TryGet_UnknownPlayer_ReturnsFalse"]`。期望：FAIL（类型未定义）。

- [ ] **Step 3: 创建 Interface**

```csharp
// Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 按 PlayerId 存储玩家卡片在主面板内的左上角位置。
    /// 持久化到 PlayerPrefs key "CPA.PlayerCardPositions"（JSON 格式的 entries 数组）。
    /// </summary>
    public interface IPlayerCardPositionModel : IModel
    {
        bool TryGet(string playerId, out Vector2 position);
        void Set(string playerId, Vector2 position);
        void Remove(string playerId);
    }
}
```

- [ ] **Step 4: 创建实现**

```csharp
// Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs
using System;
using System.Collections.Generic;
using APP.Utility;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    public sealed class PlayerCardPositionModel : AbstractModel, IPlayerCardPositionModel
    {
        private const string StorageKey = "CPA.PlayerCardPositions";

        private readonly Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();

        [Serializable]
        private struct Entry
        {
            public string id;
            public float  x;
            public float  y;
        }

        [Serializable]
        private sealed class Envelope
        {
            public Entry[] entries = Array.Empty<Entry>();
        }

        protected override void OnInit()
        {
            var storage = this.GetUtility<IStorageUtility>();
            string json = storage?.LoadString(StorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                Envelope env = JsonUtility.FromJson<Envelope>(json);
                if (env?.entries == null) return;
                for (int i = 0; i < env.entries.Length; i++)
                {
                    var e = env.entries[i];
                    if (!string.IsNullOrEmpty(e.id))
                        _positions[e.id] = new Vector2(e.x, e.y);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerCardPositionModel] 解析持久化数据失败：{ex.Message}");
            }
        }

        public bool TryGet(string playerId, out Vector2 position)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                position = Vector2.zero;
                return false;
            }
            return _positions.TryGetValue(playerId, out position);
        }

        public void Set(string playerId, Vector2 position)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            _positions[playerId] = position;
            Persist();
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_positions.Remove(playerId)) Persist();
        }

        private void Persist()
        {
            var storage = this.GetUtility<IStorageUtility>();
            if (storage == null) return;

            var env = new Envelope { entries = new Entry[_positions.Count] };
            int idx = 0;
            foreach (var kv in _positions)
            {
                env.entries[idx++] = new Entry { id = kv.Key, x = kv.Value.x, y = kv.Value.y };
            }
            storage.SaveString(StorageKey, JsonUtility.ToJson(env));
            storage.Flush();
        }
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

`run_tests` 跑 `PlayerCardPositionModelTests` 的四条测试。期望：四条全 PASS。

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs \
        Assets/Scripts/APP/Pomodoro/Model/IPlayerCardPositionModel.cs.meta \
        Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs \
        Assets/Scripts/APP/Pomodoro/Model/PlayerCardPositionModel.cs.meta \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs.meta
git commit -m "feat(pomodoro): 新增 IPlayerCardPositionModel 按 PlayerId 持久化卡片位置

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 4：在 `GameApp` 注册 `IPlayerCardPositionModel`

**Files:**
- Modify: `Assets/Scripts/APP/GameApp.cs`

- [ ] **Step 1: 注册 Model**

在 `GameApp.Init()` 中，`RegisterModel<ISessionMemoryModel>` 之后插入：

```csharp
            RegisterModel<IPlayerCardPositionModel>(new PlayerCardPositionModel());
```

- [ ] **Step 2: 编译验证**

MCP `read_console filter types: error`，确认无错误。

- [ ] **Step 3: 写烟雾测试（可选但推荐）**

追加到 `PlayerCardPositionModelTests.cs`：

```csharp
        [Test]
        public void GameApp_RegistersModel()
        {
            var model = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            Assert.That(model, Is.Not.Null, "GameApp 应注册 IPlayerCardPositionModel");
        }
```

需要 `using APP.Pomodoro;`。运行确认 PASS。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/APP/GameApp.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs
git commit -m "feat(app): 注册 IPlayerCardPositionModel

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase B — Commands & Events

### Task 5：`Cmd_SetPomodoroPanelPosition`

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPanelPosition.cs`

命令无外部副作用，直接写 Model；测试不需要，写入路径由 `PomodoroPanelPositionTests` 间接覆盖。但为严谨起见写一条验证 Command 路径。

- [ ] **Step 1: 写失败测试**

追加到 `PomodoroPanelPositionTests.cs`：

```csharp
        [Test]
        public void Cmd_SetPomodoroPanelPosition_WritesModel()
        {
            var model = GameApp.Interface.GetModel<IPomodoroModel>();
            GameApp.Interface.SendCommand(
                new APP.Pomodoro.Command.Cmd_SetPomodoroPanelPosition(new Vector2(7f, 9f)));
            Assert.That(model.PomodoroPanelPosition.Value, Is.EqualTo(new Vector2(7f, 9f)));
        }
```

需要 `using APP.Pomodoro;`（GameApp 所在 namespace）。

- [ ] **Step 2: 运行测试确认失败**

`run_tests` 跑 `Cmd_SetPomodoroPanelPosition_WritesModel`。期望：FAIL（类型未定义）。

- [ ] **Step 3: 创建 Command**

```csharp
// Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPanelPosition.cs
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>写入番茄钟面板在主面板坐标系内的左上角位置。</summary>
    public sealed class Cmd_SetPomodoroPanelPosition : AbstractCommand
    {
        private readonly Vector2 _position;

        public Cmd_SetPomodoroPanelPosition(Vector2 position) => _position = position;

        protected override void OnExecute()
        {
            this.GetModel<IPomodoroModel>().PomodoroPanelPosition.Value = _position;
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPanelPosition.cs \
        Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPomodoroPanelPosition.cs.meta \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PomodoroPanelPositionTests.cs
git commit -m "feat(pomodoro): 新增 Cmd_SetPomodoroPanelPosition

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 6：`Cmd_SetPlayerCardPosition`

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPosition.cs`

- [ ] **Step 1: 写失败测试**

追加到 `PlayerCardPositionModelTests.cs`：

```csharp
        [Test]
        public void Cmd_SetPlayerCardPosition_WritesModel()
        {
            var model = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            GameApp.Interface.SendCommand(
                new APP.Pomodoro.Command.Cmd_SetPlayerCardPosition("px", new Vector2(11f, 22f)));
            Assert.That(model.TryGet("px", out Vector2 got), Is.True);
            Assert.That(got, Is.EqualTo(new Vector2(11f, 22f)));
            model.Remove("px"); // 清场
        }
```

`using APP.Pomodoro;`。

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 创建 Command**

```csharp
// Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPosition.cs
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>按 PlayerId 写入玩家卡片在主面板坐标系内的左上角位置。</summary>
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
            this.GetModel<IPlayerCardPositionModel>().Set(_playerId, _position);
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPosition.cs \
        Assets/Scripts/APP/Pomodoro/Command/Cmd_SetPlayerCardPosition.cs.meta \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardPositionModelTests.cs
git commit -m "feat(pomodoro): 新增 Cmd_SetPlayerCardPosition

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 7：Unified Settings 的 Events + Commands

**Files:**
- Create: `Assets/Scripts/APP/Pomodoro/Event/UnifiedSettingsEvents.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Command/Cmd_OpenUnifiedSettings.cs`
- Create: `Assets/Scripts/APP/Pomodoro/Command/Cmd_CloseUnifiedSettings.cs`
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/UnifiedSettingsCommandTests.cs`

- [ ] **Step 1: 写失败测试（验证命令会触发对应事件）**

```csharp
// Assets/Tests/EditMode/PlayerCardTests/Editor/UnifiedSettingsCommandTests.cs
using APP.Pomodoro;
using APP.Pomodoro.Command;
using APP.Pomodoro.Event;
using NUnit.Framework;
using QFramework;

namespace APP.Pomodoro.Tests
{
    public sealed class UnifiedSettingsCommandTests
    {
        [Test]
        public void Cmd_OpenUnifiedSettings_FiresEvent()
        {
            bool fired = false;
            var handle = TypeEventSystem.Global.Register<E_OpenUnifiedSettings>(_ => fired = true);
            try
            {
                GameApp.Interface.SendCommand(new Cmd_OpenUnifiedSettings());
                Assert.That(fired, Is.True);
            }
            finally { handle.UnRegister(); }
        }

        [Test]
        public void Cmd_CloseUnifiedSettings_FiresEvent()
        {
            bool fired = false;
            var handle = TypeEventSystem.Global.Register<E_CloseUnifiedSettings>(_ => fired = true);
            try
            {
                GameApp.Interface.SendCommand(new Cmd_CloseUnifiedSettings());
                Assert.That(fired, Is.True);
            }
            finally { handle.UnRegister(); }
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 创建 Events**

```csharp
// Assets/Scripts/APP/Pomodoro/Event/UnifiedSettingsEvents.cs
namespace APP.Pomodoro.Event
{
    /// <summary>请求打开统一设置面板（独立 UIDocument 的 Driver 订阅并显示）。</summary>
    public readonly struct E_OpenUnifiedSettings { }

    /// <summary>请求关闭统一设置面板。</summary>
    public readonly struct E_CloseUnifiedSettings { }
}
```

- [ ] **Step 4: 创建 Commands**

```csharp
// Assets/Scripts/APP/Pomodoro/Command/Cmd_OpenUnifiedSettings.cs
using APP.Pomodoro.Event;
using QFramework;

namespace APP.Pomodoro.Command
{
    public sealed class Cmd_OpenUnifiedSettings : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.SendEvent<E_OpenUnifiedSettings>();
        }
    }
}
```

```csharp
// Assets/Scripts/APP/Pomodoro/Command/Cmd_CloseUnifiedSettings.cs
using APP.Pomodoro.Event;
using QFramework;

namespace APP.Pomodoro.Command
{
    public sealed class Cmd_CloseUnifiedSettings : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.SendEvent<E_CloseUnifiedSettings>();
        }
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/APP/Pomodoro/Event/UnifiedSettingsEvents.cs \
        Assets/Scripts/APP/Pomodoro/Event/UnifiedSettingsEvents.cs.meta \
        Assets/Scripts/APP/Pomodoro/Command/Cmd_OpenUnifiedSettings.cs \
        Assets/Scripts/APP/Pomodoro/Command/Cmd_OpenUnifiedSettings.cs.meta \
        Assets/Scripts/APP/Pomodoro/Command/Cmd_CloseUnifiedSettings.cs \
        Assets/Scripts/APP/Pomodoro/Command/Cmd_CloseUnifiedSettings.cs.meta \
        Assets/Tests/EditMode/PlayerCardTests/Editor/UnifiedSettingsCommandTests.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/UnifiedSettingsCommandTests.cs.meta
git commit -m "feat(pomodoro): 统一设置面板 Event/Cmd（Open/Close）

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase C — DraggableElement 能力扩展

### Task 8：`DraggableElement` 新增 `OnDragEnd` 回调

**Files:**
- Modify: `Assets/UI_V2/Controller/DraggableElement.cs`
- Create: `Assets/Tests/EditMode/PlayerCardTests/Editor/DraggableElementOnDragEndTests.cs`

- [ ] **Step 1: 写失败测试**

```csharp
// Assets/Tests/EditMode/PlayerCardTests/Editor/DraggableElementOnDragEndTests.cs
using APP.Pomodoro.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class DraggableElementOnDragEndTests
    {
        [Test]
        public void OnDragEnd_InvokedWithFinalPosition_AfterPointerUp()
        {
            var parent = new VisualElement { style = { width = 400, height = 300 } };
            var target = new VisualElement { style = { width = 100, height = 50, position = Position.Absolute } };
            var handle = new VisualElement();
            parent.Add(target);
            target.Add(handle);

            var ctrl = new DraggableElement.DragController(target, handle);

            Vector2? result = null;
            ctrl.OnDragEnd += pos => result = pos;

            ctrl.ProcessPointerDown(new Vector2(10, 10), pointerId: 0);
            ctrl.ProcessPointerMove(new Vector2(60, 40));
            ctrl.ProcessPointerUp(pointerId: 0);

            Assert.That(result, Is.Not.Null, "拖拽结束应触发 OnDragEnd");
            Assert.That(result.Value, Is.EqualTo(new Vector2(50, 30)),
                "OnDragEnd 传入的位置应等于 target 最终 left/top");
        }

        [Test]
        public void OnDragEnd_NotInvoked_WhenNeverDragged()
        {
            var target = new VisualElement();
            var handle = new VisualElement();
            target.Add(handle);
            var ctrl = new DraggableElement.DragController(target, handle);

            bool called = false;
            ctrl.OnDragEnd += _ => called = true;

            ctrl.ProcessPointerUp(pointerId: 0); // 无对应的 down
            Assert.That(called, Is.False);
        }
    }
}
```

注：`DragController` 当前仅公开 `Process*` 便于测试；`OnDragEnd` 是本任务新增字段。`_activePointerId` 初始为 `-1`，`ProcessPointerUp(0)` 会因 `_activePointerId != 0` 而早返。

- [ ] **Step 2: 运行测试确认失败**

`run_tests`，`testNames: ["APP.Pomodoro.Tests.DraggableElementOnDragEndTests.OnDragEnd_InvokedWithFinalPosition_AfterPointerUp"]`。期望：FAIL（字段未定义）。

- [ ] **Step 3: 扩展 `DragController`**

在 `DraggableElement.DragController` 类体内顶部加字段：

```csharp
            /// <summary>拖拽结束（PointerUp / PointerCaptureOut）时触发，参数为 target 最终左上角（left, top）。</summary>
            public event System.Action<Vector2> OnDragEnd;
```

在 `ProcessPointerUp` 末尾（在 `_dragging = false` 之前或其后，保证先读坐标）调整为：

```csharp
            public void ProcessPointerUp(int pointerId)
            {
                if (_activePointerId != pointerId)
                {
                    return;
                }

                var finalPos = new Vector2(GetCurrentLeft(_target), GetCurrentTop(_target));
                _dragging = false;
                _activePointerId = -1;
                OnDragEnd?.Invoke(finalPos);
            }
```

同样修改 `ProcessPointerCaptureOut`：

```csharp
            public void ProcessPointerCaptureOut()
            {
                if (!_dragging) { return; }
                var finalPos = new Vector2(GetCurrentLeft(_target), GetCurrentTop(_target));
                _dragging = false;
                _activePointerId = -1;
                OnDragEnd?.Invoke(finalPos);
            }
```

- [ ] **Step 4: 运行测试确认通过**

两条测试都 PASS。同时跑原有 `DraggableElementTests` 确认未回归。

- [ ] **Step 5: 提交**

```bash
git add Assets/UI_V2/Controller/DraggableElement.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/DraggableElementOnDragEndTests.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/DraggableElementOnDragEndTests.cs.meta
git commit -m "feat(ui): DraggableElement 新增 OnDragEnd 回调

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase D — Pencil 设计稿改动

> ⚠️ 先设计稿，再 UXML/USS。Pencil 是视觉 ground truth（CLAUDE.md 强制要求）。
> 必须用 `mcp__pencil__batch_design` 工具；不要直接编辑 .pen 文件。

### Task 9：Pencil — 84Qri 去描边+去背景

**Files:**
- Modify: `AUI/PUI.pen` 节点 `84Qri`（通过 pencil MCP）

- [ ] **Step 1: 打开文档并读取当前 84Qri 属性**

```
mcp__pencil__open_document('/Users/xpy/Desktop/NanZhai/CPA/AUI/PUI.pen')
mcp__pencil__batch_get patterns=["84Qri"]
```

结果可能超限；若超限改为 `nodeIds=["84Qri"]`，或用 `patterns=["84Qri"]` + 过滤字段。

记录现有的 `fill` / `stroke` / `cornerRadius` / `padding` 字段。

- [ ] **Step 2: 通过 batch_design 去除描边与背景**

```
mcp__pencil__batch_design operations:
  U("84Qri", { stroke: null, strokeWidth: 0, fill: null, cornerRadius: 0, padding: [0,0,0,0] })
```

（具体 key 命名以 Pencil schema 为准——若字段不是 stroke/fill，从 Step 1 读到的属性 key 代入。）

- [ ] **Step 3: 截图验证**

```
mcp__pencil__get_screenshot nodeId="84Qri"
```

肉眼确认：无描边、无背景、内部子节点正常。

- [ ] **Step 4: 提交（.pen 是二进制，直接 add 整文件）**

```bash
git add AUI/PUI.pen
git commit -m "design(pencil): 84Qri 移除描边与背景以适配透明全屏主面板

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 10：Pencil — YRqeB 新增 handleBar（三点 + 设置齿轮）

**Files:**
- Modify: `AUI/PUI.pen` 节点 `YRqeB`

- [ ] **Step 1: 读取 YRqeB 现结构**

```
mcp__pencil__batch_get patterns=["YRqeB"]
```

记录子节点顺序，handleBar 要作为第一个子节点插入。

- [ ] **Step 2: 读 "pomodoroPanel guideline"（若有）**

```
mcp__pencil__get_guidelines category="component" name="pomodoroPanel"
```

非必须；若无返回跳过。

- [ ] **Step 3: 插入 handleBar 子节点（pp-handle-bar）**

handleBar 规格：水平行（row），高 28px，宽与父等宽；内部三段：
- 左 spacer（flex-grow:1）
- 中 三点 `•••`（14px、`#C4B5A8`，cursor:move）
- 右 齿轮按钮 28×28 圆角 11、背景 `#F0E0D0`，内部 16×16 齿轮图标颜色 `#A28B79`

（具体属性 key 以 Pencil schema 为准，参考同类组件 84Qri 中原 handleBar 的结构。）

`batch_design` 操作大致：

```
bar = I("YRqeB", { name:"pp-handle-bar", layout:"row", width:"fill", height:28, align:"center" })
I(bar, { name:"spacer", flexGrow:1 })
I(bar, { name:"pp-handle-drag", type:"text", text:"•••", fontSize:14, color:"#C4B5A8", align:"center" })
right = I(bar, { layout:"row", justify:"end", align:"center" })
btn = I(right, { name:"pp-settings-btn", width:28, height:28, cornerRadius:11, fill:"#F0E0D0", align:"center", justify:"center" })
I(btn, { width:16, height:16, icon:"settings" })
```

关键是 pp-handle-bar 需要是 YRqeB 的**第一个**子节点（若 batch_design 无法插入首位，读现有首个子节点 id 后用 `insertBefore`）。

- [ ] **Step 4: 截图验证**

```
mcp__pencil__get_screenshot nodeId="YRqeB"
```

确认：顶部出现三点 + 右侧齿轮按钮。

- [ ] **Step 5: 提交**

```bash
git add AUI/PUI.pen
git commit -m "design(pencil): YRqeB 顶部新增 handleBar（三点 + 设置齿轮）

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 11：Pencil — drqFB 新增 handleBar

**Files:**
- Modify: `AUI/PUI.pen` 节点 `drqFB`

handleBar 规格：高 16px、全宽、水平居中三点 `•••` 字号 12 颜色 `#C4B5A8`，cursor:move。

- [ ] **Step 1: 读取 drqFB 当前结构**

```
mcp__pencil__batch_get nodeIds=["drqFB"]
```

- [ ] **Step 2: 在 drqFB 顶部插入 pc-handle-bar**

```
bar = I("drqFB", { name:"pc-handle-bar", layout:"row", width:"fill", height:16, align:"center", justify:"center" })
I(bar, { name:"pc-handle-drag", type:"text", text:"•••", fontSize:12, color:"#C4B5A8" })
```

同样需要是**第一个**子节点（在 `tyyE3` 之前）。

- [ ] **Step 3: 调整 drqFB 高度**

原 97 → 新 113（16px handleBar + 12px 间距重新分配）。

```
U("drqFB", { height: 113 })
```

- [ ] **Step 4: 截图验证**

```
mcp__pencil__get_screenshot nodeId="drqFB"
```

- [ ] **Step 5: 提交**

```bash
git add AUI/PUI.pen
git commit -m "design(pencil): drqFB 顶部新增 pc-handle-bar，高度调整为 113

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase E — UXML / USS

### Task 12：PomodoroPanel.uxml + .uss 新增 handleBar 与 absolute 定位

**Files:**
- Modify: `Assets/UI_V2/Documents/PomodoroPanel.uxml`
- Modify: `Assets/UI_V2/Styles/PomodoroPanel.uss`

- [ ] **Step 1: UXML — 在 `#pp-root` 内最前新增 handleBar**

在 `<ui:VisualElement name="pp-root" class="pp-root">` 打开标签之后、第一行 `<!-- 顶部：标题 ... -->` 之前，插入：

```xml
    <!-- 顶部拖拽把手 + 设置按钮（Pencil: pp-handle-bar） -->
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

- [ ] **Step 2: USS — 追加 handleBar 样式 + `.pp-root` 绝对定位**

在 `PomodoroPanel.uss` 末尾追加：

```css
/* ── pp-root 绝对定位（挂在 #dw-canvas 上） ──────────────── */
.pp-root {
    position: absolute;
    /* width/height 由 Pencil YRqeB 量出的实际尺寸决定，由 Task 19 读出后写入 */
}

/* ── 顶部拖拽把手（对齐 Pencil handleBar） ──────────────── */
.pp-handle-bar {
    flex-direction: row;
    align-items: center;
    width: 100%;
    height: 28px;
}
.pp-handle-spacer {
    flex-grow: 1;
    height: 28px;
}
.pp-handle-drag {
    -unity-text-align: middle-center;
    font-size: 14px;
    -unity-font-style: bold;
    color: rgb(196, 181, 168);
    cursor: move-arrow;
    flex-shrink: 0;
}
.pp-handle-right {
    flex-direction: row;
    justify-content: flex-end;
    align-items: center;
    flex-shrink: 0;
}
.pp-settings-btn {
    width: 28px;
    height: 28px;
    border-radius: 11px;
    background-color: rgb(240, 224, 208);
    align-items: center;
    justify-content: center;
}
.pp-settings-btn-icon {
    width: 16px;
    height: 16px;
    font-size: 16px;
    color: rgb(162, 139, 121);
    -unity-text-align: middle-center;
}
```

- [ ] **Step 3: 编辑器 Refresh + 编译验证**

MCP `refresh_unity`，然后 `read_console filter types: error`。无错。

- [ ] **Step 4: 提交**

```bash
git add Assets/UI_V2/Documents/PomodoroPanel.uxml \
        Assets/UI_V2/Styles/PomodoroPanel.uss
git commit -m "feat(ui): PomodoroPanel 新增 handleBar 与 absolute 根样式

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 13：PlayerCard.uxml + .uss 新增 handleBar 与 absolute 定位

**Files:**
- Modify: `Assets/UI_V2/Documents/PlayerCard.uxml`
- Modify: `Assets/UI_V2/Styles/PlayerCard.uss`

- [ ] **Step 1: UXML — 在 `<ui:VisualElement class="pc-root">` 内最前插入 handleBar**

```xml
        <!-- 顶部拖拽把手（Pencil: pc-handle-bar） -->
        <ui:VisualElement name="pc-handle-bar" class="pc-handle-bar">
            <ui:Label text="•••" class="pc-handle-drag"/>
        </ui:VisualElement>
```

放在 `<!-- tyyE3: head -->` 行之上。

- [ ] **Step 2: USS — `.pc-root` 改为绝对定位，新增 handleBar 样式**

将 `.pc-root` 原规则修改为：

```css
.pc-root {
    position: absolute;      /* 挂在 #card-layer 上，由 C# 写 left/top */
    width: 153px;
    height: 113px;           /* 原 97 + handleBar 16 */
    padding: 14px;
    border-radius: 20px;
    background-color: rgba(255, 253, 251, 0.95);
    border-width: 1px;
    border-color: rgb(239, 220, 205);
    flex-direction: column;
    overflow: hidden;
    /* margin-right: 12 已删除 —— 绝对定位不再需要 */
}

.pc-handle-bar {
    flex-direction: row;
    align-items: center;
    justify-content: center;
    width: 100%;
    height: 16px;
    cursor: move-arrow;
    flex-shrink: 0;
    margin-bottom: 0;
}
.pc-handle-drag {
    font-size: 12px;
    -unity-font-style: bold;
    color: rgb(196, 181, 168);
    -unity-text-align: middle-center;
}
```

删除原规则中 `margin-right: 12px` 一行（其他属性合并到上面）。

- [ ] **Step 3: Refresh + 编译验证**

- [ ] **Step 4: 提交**

```bash
git add Assets/UI_V2/Documents/PlayerCard.uxml \
        Assets/UI_V2/Styles/PlayerCard.uss
git commit -m "feat(ui): PlayerCard 新增 pc-handle-bar 与 absolute 定位

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 14：新 `UnifiedSettingsPanel.uxml` + `UnifiedSettingsPanel.uss`（从 DeskWindow 抽离）

**Files:**
- Create: `Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml`
- Create: `Assets/UI_V2/Styles/UnifiedSettingsPanel.uss`

- [ ] **Step 1: 创建 UXML**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements"
         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         noNamespaceSchemaLocation="../../../UIElementsSchema/UIElements.xsd"
         editor-extension-mode="False">
    <Style src="../Styles/Variables.uss"/>
    <Style src="../Styles/UnifiedSettingsPanel.uss"/>
    <Style src="../Styles/PomodoroSettingsPanel.uss"/>
    <Style src="../Styles/OnlineSettingsPanel.uss"/>
    <Style src="../Styles/PetSettingsPanel.uss"/>

    <!-- 设置面板：独立 UIDocument 根（对应 Pencil vnYnS） -->
    <ui:VisualElement name="settings-overlay" class="settings-overlay" style="display: none;">
        <ui:VisualElement class="settings-header">
            <ui:Label text="设置" class="settings-title" />
            <ui:VisualElement class="settings-spacer" />
            <ui:VisualElement name="settings-close" class="settings-close">
                <ui:Label text="✕" class="settings-close-icon" />
            </ui:VisualElement>
        </ui:VisualElement>
        <ui:VisualElement class="settings-body">
            <ui:VisualElement class="settings-sidebar">
                <ui:VisualElement name="tab-pomodoro" class="sidebar-tab sidebar-tab--active">
                    <ui:Label text="番茄钟" class="sidebar-tab-label" />
                </ui:VisualElement>
                <ui:VisualElement name="tab-online" class="sidebar-tab">
                    <ui:Label text="联机" class="sidebar-tab-label" />
                </ui:VisualElement>
                <ui:VisualElement name="tab-pet" class="sidebar-tab">
                    <ui:Label text="宠物" class="sidebar-tab-label" />
                </ui:VisualElement>
            </ui:VisualElement>
            <ui:ScrollView name="settings-content" vertical-scroller-visibility="Auto"
                           horizontal-scroller-visibility="Hidden" class="settings-content">
                <ui:VisualElement name="settings-content-host" class="settings-content-host" />
            </ui:ScrollView>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: 创建 USS（从 DeskWindow.uss 搬过来的 overlay 相关规则）**

```css
/* ── 统一设置面板（独立 UIDocument 根） ───────────────────── */
.settings-overlay {
    position: absolute;
    left: 0; top: 0; right: 0; bottom: 0;
    background-color: rgb(255, 255, 255);
    border-radius: 20px;
    padding: 20px;
    flex-direction: column;
}

.settings-header {
    flex-direction: row;
    align-items: center;
    margin-bottom: 16px;
}

.settings-title {
    font-size: 18px;
    -unity-font-style: bold;
    color: rgb(91, 70, 54);
}

.settings-spacer { flex-grow: 1; }

.settings-close {
    width: 28px; height: 28px;
    border-radius: 14px;
    background-color: rgb(243, 237, 231);
    align-items: center; justify-content: center;
}

.settings-close-icon {
    font-size: 14px;
    color: rgb(162, 139, 121);
    -unity-text-align: middle-center;
}

.settings-body {
    flex-direction: row;
    flex-grow: 1;
    overflow: hidden;
}

.settings-sidebar {
    width: 71px;
    background-color: rgb(243, 237, 231);
    padding-top: 8px; padding-bottom: 8px;
    flex-direction: column;
    border-radius: 5px;
    flex-shrink: 0;
}

.sidebar-tab { padding: 10px 14px; }

.sidebar-tab-label {
    font-size: 13px;
    color: rgb(158, 142, 128);
    -unity-font-style: normal;
}

.sidebar-tab--active {
    background-color: rgb(255, 255, 255);
    border-top-left-radius: 0;
    border-top-right-radius: 12px;
    border-bottom-right-radius: 12px;
    border-bottom-left-radius: 0;
}

.sidebar-tab--active .sidebar-tab-label {
    color: rgb(209, 95, 61);
    -unity-font-style: bold;
}

.settings-content {
    flex-grow: 1;
    padding-left: 16px;
    padding-right: 16px;
}

.settings-content-host {
    flex-direction: column;
    width: 100%;
}

.settings-content .unity-scroll-view__content-container {
    flex-direction: column;
    width: 100%;
}
```

- [ ] **Step 3: Refresh + 编译验证**

- [ ] **Step 4: 提交**

```bash
git add Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml \
        Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml.meta \
        Assets/UI_V2/Styles/UnifiedSettingsPanel.uss \
        Assets/UI_V2/Styles/UnifiedSettingsPanel.uss.meta
git commit -m "feat(ui): 新增 UnifiedSettingsPanel.uxml/.uss（独立 UIDocument 用）

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 15：重写 `DeskWindow.uxml` + 精简 `DeskWindow.uss`

**Files:**
- Modify: `Assets/UI_V2/Documents/DeskWindow.uxml`
- Modify: `Assets/UI_V2/Styles/DeskWindow.uss`

- [ ] **Step 1: 替换 `DeskWindow.uxml` 为精简版**

整体替换为：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements"
         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         noNamespaceSchemaLocation="../../../UIElementsSchema/UIElements.xsd" editor-extension-mode="False">
    <ui:Template name="PomodoroPanel" src="project://database/Assets/UI_V2/Documents/PomodoroPanel.uxml?fileID=9197481963319205126&amp;guid=cbf5638ad0ab048998be547920a9d8ca&amp;type=3#PomodoroPanel" />
    <Style src="project://database/Assets/UI_V2/Styles/Variables.uss?fileID=7433441132597879392&amp;guid=4b56b48a338d343dfb0e16e579e22bef&amp;type=3#Variables" />
    <Style src="project://database/Assets/UI_V2/Styles/DeskWindow.uss?fileID=7433441132597879392&amp;guid=1fd1c07e4450e431fad56cc90cc090fb&amp;type=3#DeskWindow" />
    <Style src="project://database/Assets/UI_V2/Styles/PlayerCard.uss?fileID=7433441132597879392&amp;guid=23b0113cd0f2a41b3bf91059f3d0d0b9&amp;type=3#PlayerCard" />
    <Style src="project://database/Assets/UI_V2/Styles/PomodoroPanel.uss?fileID=7433441132597879392&amp;guid=5b74c2cdcf0d44e13b91a9a7c33b8cad&amp;type=3#PomodoroPanel"/>
    <!-- DeskWindow 主面板：透明全屏，只做 pomodoro + card-layer 宿主 -->
    <ui:VisualElement name="dw-canvas" class="dw-canvas">
        <ui:Instance name="pomodoro-panel" template="PomodoroPanel" class="dw-floating" />
        <ui:VisualElement name="card-layer" class="dw-card-layer" />
    </ui:VisualElement>
</ui:UXML>
```

（`PomodoroPanel.uss` 的 guid 用实际值——可能与我填的 `5b74c2cdcf0d44e13b91a9a7c33b8cad` 不同；读原 `PomodoroPanel.uss.meta` 的 `guid:` 字段替换。）

- [ ] **Step 2: 精简 `DeskWindow.uss`**

保留 `.dw-root-anchor`；**删除**：`.dw-wrap`、`.handle-bar`、`.handle-spacer`、`.handle-right`、`.drag-handle`、`.settings-btn`、`.settings-btn-icon`、`.content-row`、`.dw-pomodoro`、`.hover-hint*`、`.card-list*`、`.settings-overlay` 及其下所有 `.settings-*` / `.sidebar-tab*` / `.settings-content*` 规则。

新增：

```css
.dw-canvas {
    position: absolute;
    left: 0; top: 0; right: 0; bottom: 0;
    /* 透明：无 background，无 border */
}

.dw-floating { position: absolute; }

.dw-card-layer {
    position: absolute;
    left: 0; top: 0; right: 0; bottom: 0;
}
```

最终文件只包含：注释头、`.dw-root-anchor`、`.dw-canvas`、`.dw-floating`、`.dw-card-layer` 四条规则（+ 保留的注释）。

- [ ] **Step 3: Refresh + 编译验证**

Unity 控制台无报错；编辑器里打开 `DeskWindow.uxml` 预览：应是空白透明区域 + 番茄钟子面板预览（绝对定位默认 left/top 0）。

- [ ] **Step 4: 提交**

```bash
git add Assets/UI_V2/Documents/DeskWindow.uxml \
        Assets/UI_V2/Styles/DeskWindow.uss
git commit -m "refactor(ui): DeskWindow 改为透明全屏 canvas + card-layer

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase F — Scene & Asset

### Task 16：`PanelSettings_Settings.asset` + 场景新增 UnifiedSettingsPanel GameObject

**Files:**
- Create: `Assets/UI_V2/PanelSettings_Settings.asset`
- Modify: 主场景（通过 UnityMCP 查询现有 PanelSettings 所在资源，复制一份并改 SortOrder）

- [ ] **Step 1: 找到现有 PanelSettings 资产位置**

```
mcp__UnityMCP__manage_asset operation="find" path="Assets" query="PanelSettings"
```

记录返回的 PanelSettings 资产路径（例如 `Assets/UI_V2/PanelSettings.asset`）。

- [ ] **Step 2: 复制一份作为设置专用**

```
mcp__UnityMCP__manage_asset operation="copy"
  source=<上一步找到的路径>
  destination="Assets/UI_V2/PanelSettings_Settings.asset"
```

- [ ] **Step 3: 把新资产的 `sortingOrder` 改为 10**

```
mcp__UnityMCP__execute_code code=@"
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(\"Assets/UI_V2/PanelSettings_Settings.asset\");
var so = new SerializedObject(ps);
var prop = so.FindProperty(\"m_SortingOrder\");
prop.floatValue = 10f;
so.ApplyModifiedProperties();
AssetDatabase.SaveAssets();
Debug.Log(\"PanelSettings_Settings sortingOrder = 10\");
"
```

- [ ] **Step 4: 读当前场景路径**

```
mcp__UnityMCP__manage_scene operation="get_active"
```

记录场景路径。

- [ ] **Step 5: 在场景中新建 `UnifiedSettingsPanel` GameObject**

```
mcp__UnityMCP__manage_gameobject
  operation="create"
  name="UnifiedSettingsPanel"
```

然后添加 UIDocument 组件并指定 PanelSettings + source UXML：

```
mcp__UnityMCP__execute_code code=@"
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

var go = GameObject.Find(\"UnifiedSettingsPanel\");
var doc = go.AddComponent<UIDocument>();
doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(\"Assets/UI_V2/PanelSettings_Settings.asset\");
doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(\"Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml\");
EditorUtility.SetDirty(go);
EditorSceneManager.MarkSceneDirty(go.scene);
EditorSceneManager.SaveScene(go.scene);
Debug.Log(\"UnifiedSettingsPanel GameObject 已配置\");
"
```

导入 `UnityEditor.SceneManagement` 若缺。

- [ ] **Step 6: 场景视觉验证**

进入 PlayMode 前用 `find_gameobjects` 确认存在：

```
mcp__UnityMCP__find_gameobjects name="UnifiedSettingsPanel"
```

- [ ] **Step 7: 提交**

```bash
git add Assets/UI_V2/PanelSettings_Settings.asset \
        Assets/UI_V2/PanelSettings_Settings.asset.meta \
        Assets/**/SampleScene.unity  # 替换为 Step 4 的实际场景路径
git commit -m "feat(scene): 新增 UnifiedSettingsPanel UIDocument + 独立 PanelSettings

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase G — Controller 重构

### Task 17：`UnifiedSettingsPanelController` 适配独立 UIDocument 根

**Files:**
- Modify: `Assets/UI_V2/Controller/UnifiedSettingsPanelController.cs`

该 Controller 当前 `Init` 接收 DeskWindow 的 root 并在里面 Q 出 settings-overlay。新结构下，独立 UIDocument 的 `rootVisualElement` 本身就是 UnifiedSettingsPanel.uxml 的根 → overlay 是其子节点。

- [ ] **Step 1: 改 `Init` 签名（不破坏原参数语义，但不再依赖 DeskWindow）**

在类顶部增加注释：现在 `root` 参数预期是 UnifiedSettingsPanel.uxml 的 rootVisualElement（仍通过 Q 找 settings-overlay，语义不变）。

无需改签名或代码——`root.Q("settings-overlay")` 在新 UXML 中同样有效，因为 UXML 里该元素 id 保持不变。

- [ ] **Step 2: 验证（人工）**

`validate_script path="Assets/UI_V2/Controller/UnifiedSettingsPanelController.cs" level="standard"`，无警告即可。

- [ ] **Step 3: 不需要单独提交**（后续 Task 18 一起提交）

---

### Task 18：新 `UnifiedSettingsPanelDriver` MonoBehaviour

**Files:**
- Create: `Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs`
- Modify: 场景中 UnifiedSettingsPanel GameObject 挂该脚本（通过 MCP）

- [ ] **Step 1: 创建 Driver**

```csharp
// Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs
using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 承载独立 UIDocument 的驱动 MonoBehaviour。
    /// 订阅 E_OpenUnifiedSettings / E_CloseUnifiedSettings，切换设置面板显隐。
    /// 与 DeskWindowController 之间通过 QFramework Event 通信，不相互持有引用。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UnifiedSettingsPanelDriver : MonoBehaviour, IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        [Header("子面板 UXML 模板（从 UnifiedSettingsPanelController 迁入）")]
        [SerializeField] private VisualTreeAsset _pomodoroSettingsTemplate;
        [SerializeField] private VisualTreeAsset _onlineSettingsTemplate;
        [SerializeField] private VisualTreeAsset _petSettingsTemplate;

        private UIDocument _doc;
        private UnifiedSettingsPanelController _controller;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void Start()
        {
            _ = GameApp.Interface;

            var root = _doc.rootVisualElement;
            root.style.display = DisplayStyle.None;   // 初始隐藏

            var pomodoroModel = this.GetModel<IPomodoroModel>();
            var roomModel = this.GetModel<IRoomModel>();

            _controller = new UnifiedSettingsPanelController();
            _controller.Init(
                root,
                pomodoroModel,
                roomModel,
                _pomodoroSettingsTemplate,
                _onlineSettingsTemplate,
                _petSettingsTemplate,
                gameObject);

            this.RegisterEvent<E_OpenUnifiedSettings>(_ =>
            {
                root.style.display = DisplayStyle.Flex;
                _controller.Show();
            }).UnRegisterWhenGameObjectDestroyed(gameObject);

            this.RegisterEvent<E_CloseUnifiedSettings>(_ =>
            {
                _controller.Hide();
                root.style.display = DisplayStyle.None;
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
        }
    }
}
```

- [ ] **Step 2: 把脚本挂到场景 GameObject 并赋三个 Template**

```
mcp__UnityMCP__execute_code code=@"
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.SceneManagement;

var go = GameObject.Find(\"UnifiedSettingsPanel\");
var driver = go.AddComponent<APP.Pomodoro.Controller.UnifiedSettingsPanelDriver>();
var so = new SerializedObject(driver);
so.FindProperty(\"_pomodoroSettingsTemplate\").objectReferenceValue =
    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(\"Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml\");
so.FindProperty(\"_onlineSettingsTemplate\").objectReferenceValue =
    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(\"Assets/UI_V2/Documents/OnlineSettingsPanel.uxml\");
so.FindProperty(\"_petSettingsTemplate\").objectReferenceValue =
    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(\"Assets/UI_V2/Documents/PetSettingsPanel.uxml\");
so.ApplyModifiedProperties();
EditorSceneManager.MarkSceneDirty(go.scene);
EditorSceneManager.SaveScene(go.scene);
Debug.Log(\"UnifiedSettingsPanelDriver 挂载完毕\");
"
```

- [ ] **Step 3: `read_console` 验证无错**

- [ ] **Step 4: 提交**

```bash
git add Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs \
        Assets/UI_V2/Controller/UnifiedSettingsPanelDriver.cs.meta \
        Assets/**/SampleScene.unity
git commit -m "feat(ui): UnifiedSettingsPanelDriver 承载独立 UIDocument，经 Event 通信

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 19：`PomodoroPanelView` 新增 handleBar 绑定 + 位置持久化 + 设置按钮

**Files:**
- Modify: `Assets/UI_V2/Controller/PomodoroPanelView.cs`

- [ ] **Step 1: 用 Pencil 查 YRqeB 最终尺寸并写进 USS**

`mcp__pencil__batch_get nodeIds=["YRqeB"]`，读出宽高（例如 W×H）。

用 Edit 工具补到 `Assets/UI_V2/Styles/PomodoroPanel.uss` 的 `.pp-root`：

```css
.pp-root {
    position: absolute;
    width: <W>px;      /* 来自 Pencil YRqeB.size.w */
    height: <H>px;     /* 来自 Pencil YRqeB.size.h */
}
```

- [ ] **Step 2: 修改 `PomodoroPanelView.cs` — 在 `Init` 中新增 handleBar 逻辑**

在 `BindElements` 末尾（`_ppBtnSecondary?.RegisterCallback` 之后）新增：

```csharp
            // handleBar 拖拽 + 设置按钮
            var handleBar = pomodoroTemplateContainer.Q<VisualElement>("pp-handle-bar");
            var settingsBtn = pomodoroTemplateContainer.Q<VisualElement>("pp-settings-btn");
            if (_ppRoot != null && handleBar != null)
            {
                var dragController = DraggableElement.MakeDraggable(_ppRoot, handleBar);
                dragController.OnDragEnd += pos =>
                    this.SendCommand(new Cmd_SetPomodoroPanelPosition(pos));
            }
            settingsBtn?.RegisterCallback<PointerUpEvent>(_ =>
                this.SendCommand(new Cmd_OpenUnifiedSettings()));
```

需要 `using APP.Pomodoro.Command;`（已有）；如果 `DraggableElement` 不在 `APP.Pomodoro.Controller` namespace 的同一 using 范围，显式写全名或 `using APP.Pomodoro.Controller;`（同 namespace 不需要）。

- [ ] **Step 3: `Init` 之后添加位置订阅**

在 `Init` 方法末尾（`_isInitialized = true;` 之前）插入：

```csharp
            // 订阅位置 Model → 写 style.left/top；首帧 sentinel 时用屏幕右下默认值
            _model = _model ?? this.GetModel<IPomodoroModel>();
            _model.PomodoroPanelPosition.RegisterWithInitValue(OnPomodoroPositionChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            _ppRoot?.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
```

并在类末尾新增方法：

```csharp
        private void OnPomodoroPositionChanged(Vector2 pos)
        {
            if (_ppRoot == null) return;
            if (float.IsNegativeInfinity(pos.x) || float.IsNegativeInfinity(pos.y))
            {
                return; // sentinel：等 GeometryChanged 算默认位置
            }
            _ppRoot.style.left = pos.x;
            _ppRoot.style.top  = pos.y;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent _)
        {
            if (_model == null || _ppRoot == null) return;
            var current = _model.PomodoroPanelPosition.Value;
            if (!float.IsNegativeInfinity(current.x) && !float.IsNegativeInfinity(current.y))
                return; // 已有持久化值，不覆盖

            var rootLayout = _ppRoot.parent?.layout ?? _ppRoot.layout;
            if (rootLayout.width <= 0 || rootLayout.height <= 0) return;
            if (_ppRoot.layout.width <= 0 || _ppRoot.layout.height <= 0) return;

            // 右下角锚点（距右/下各 20px）
            float x = rootLayout.width  - _ppRoot.layout.width  - 20f;
            float y = rootLayout.height - _ppRoot.layout.height - 20f;
            x = Mathf.Max(0, x);
            y = Mathf.Max(0, y);
            this.SendCommand(new Cmd_SetPomodoroPanelPosition(new Vector2(x, y)));
        }
```

文件顶部加 `using UnityEngine;` / `using APP.Pomodoro.Command;` 如缺。

- [ ] **Step 4: 编译验证**

`read_console filter types: error`，无错。

- [ ] **Step 5: 手动跑 PlayMode 烟雾测试**

进入 Play → 确认番茄钟出现在屏幕右下角；点三点拖拽可移动；点齿轮会弹出独立设置面板（由 Driver 响应）；退出重进位置保留。

`mcp__UnityMCP__manage_editor operation="start_play_mode"` 后观察 `read_console`。

- [ ] **Step 6: 提交**

```bash
git add Assets/UI_V2/Controller/PomodoroPanelView.cs \
        Assets/UI_V2/Styles/PomodoroPanel.uss
git commit -m "feat(pomodoro): PomodoroPanelView 接入 handleBar 拖拽 + 位置持久化 + 设置按钮

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 20：`PlayerCardController` 持有 `PlayerId`（Setup 已有，确认可用）

**Files:**
- Modify: `Assets/UI_V2/Controller/PlayerCardController.cs`（可能无需改动）

Controller 已有 `public string PlayerId { get; private set; }`，`Setup(data)` 内赋值。本任务只做确认：

- [ ] **Step 1: 确认 `PlayerId` 属性可用**

打开 `PlayerCardController.cs`，确认 line 39 存在 `public string PlayerId { get; private set; }` 且 `Setup` 内赋值。无需改动则本任务跳过 commit。

- [ ] **Step 2: （无）不需要提交**

> 备注：如果后续 Task 21 需要"卡片尚未 Setup 但要拿 PlayerId"的场景，则在这里把 PlayerId 移到构造函数参数；目前 Task 21 的调用顺序是"先 CloneTree + Setup，再 MakeDraggable"，不需要提前拿 PlayerId。

---

### Task 21：`PlayerCardManager` 新签名 + 布局算法 + 拖拽持久化

**Files:**
- Modify: `Assets/UI_V2/Controller/PlayerCardManager.cs`
- Modify: `Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardManagerTests.cs`

- [ ] **Step 1: 为新布局算法写失败测试**

替换（或追加）`PlayerCardManagerTests.cs` 中的测试。先清空原 `InitializeForTests` 相关测试，替换为：

```csharp
using System.Text.RegularExpressions;
using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardManagerTests
    {
        private VisualElement _cardLayer;
        private VisualTreeAsset _template;

        [SetUp]
        public void SetUp()
        {
            _cardLayer = new VisualElement { style = { width = 1920, height = 1080 } };
            _template = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI_V2/Documents/PlayerCard.uxml");
            Assert.That(_template, Is.Not.Null, "PlayerCard.uxml 必须存在");

            // 清空持久化位置，避免跨用例污染
            PlayerPrefs.DeleteKey("CPA.PlayerCardPositions");
            var posModel = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            posModel.Remove("p1"); posModel.Remove("p2"); posModel.Remove("p3");
        }

        [TearDown]
        public void TearDown()
        {
            _cardLayer = null;
        }

        [Test]
        public void FirstCard_PlacedAtFixedAnchor_40_40()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            mgr.AddOrUpdate(NewPlayer("p1", "Alice"));

            var card = mgr.Cards["p1"].Root;
            Assert.That(card.style.left.value.value, Is.EqualTo(40f));
            Assert.That(card.style.top.value.value,  Is.EqualTo(40f));
        }

        [Test]
        public void SecondCard_PlacedToRightOfFirst_WithGap12()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            mgr.AddOrUpdate(NewPlayer("p1", "A"));
            mgr.AddOrUpdate(NewPlayer("p2", "B"));

            var card2 = mgr.Cards["p2"].Root;
            // 期望：x = 40 + 153 + 12 = 205，y = 40
            Assert.That(card2.style.left.value.value, Is.EqualTo(205f));
            Assert.That(card2.style.top.value.value,  Is.EqualTo(40f));
        }

        [Test]
        public void OverflowRightEdge_WrapsToNextLine()
        {
            var narrowLayer = new VisualElement { style = { width = 400, height = 800 } };
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, narrowLayer);
            // 400 - 20 = 380 可用宽；每张 153 + 12 gap
            // p1 @ (40, 40), p2 @ (205, 40)，p3 候选 x=370，370+153=523 > 380 → 换行
            mgr.AddOrUpdate(NewPlayer("p1", "A"));
            mgr.AddOrUpdate(NewPlayer("p2", "B"));
            mgr.AddOrUpdate(NewPlayer("p3", "C"));

            var card3 = mgr.Cards["p3"].Root;
            Assert.That(card3.style.left.value.value, Is.EqualTo(40f));
            Assert.That(card3.style.top.value.value,  Is.EqualTo(40f + 113f + 12f));
        }

        [Test]
        public void ReturningPlayer_RestoresPersistedPosition()
        {
            var posModel = GameApp.Interface.GetModel<IPlayerCardPositionModel>();
            posModel.Set("p1", new Vector2(333f, 444f));

            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            mgr.AddOrUpdate(NewPlayer("p1", "Alice"));

            var card = mgr.Cards["p1"].Root;
            Assert.That(card.style.left.value.value, Is.EqualTo(333f));
            Assert.That(card.style.top.value.value,  Is.EqualTo(444f));

            posModel.Remove("p1");
        }

        [Test]
        public void Remove_UnknownPlayer_DoesNotThrow()
        {
            var mgr = new PlayerCardManager();
            mgr.InitializeForTests(_template, _cardLayer);
            Assert.DoesNotThrow(() => mgr.Remove("unknown"));
        }

        private static RemotePlayerData NewPlayer(string id, string name) => new RemotePlayerData
        {
            PlayerId = id,
            PlayerName = name,
            Phase = PomodoroPhase.Focus,
            RemainingSeconds = 1500,
            CurrentRound = 1,
            TotalRounds = 4,
            IsRunning = true,
        };
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

`run_tests`，`testNames: ["APP.Pomodoro.Tests.PlayerCardManagerTests.FirstCard_PlacedAtFixedAnchor_40_40"]`。期望：FAIL（方法签名或字段不存在）。

- [ ] **Step 3: 重写 `PlayerCardManager`**

完整替换为：

```csharp
// Assets/UI_V2/Controller/PlayerCardManager.cs
using System.Collections.Generic;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 玩家卡片管理器：订阅网络事件，管理 PlayerCardController 的生命周期。
    /// 卡片以绝对定位方式挂在主面板 #card-layer 上：
    ///  - 已持久化位置 → 恢复上次坐标
    ///  - 否则按"上一张右侧 + 右界换行"算法摆放（首张固定 (40,40)）
    /// 拖拽结束后通过 Cmd_SetPlayerCardPosition 持久化。
    /// </summary>
    public sealed class PlayerCardManager : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        public const float CardWidth  = 153f;
        public const float CardHeight = 113f;
        public const float Gap        = 12f;
        public static readonly Vector2 FirstAnchor = new Vector2(40f, 40f);

        private readonly Dictionary<string, PlayerCardController> _cards = new Dictionary<string, PlayerCardController>();
        private readonly List<string> _joinOrder = new List<string>();

        private VisualTreeAsset _cardTemplate;
        private VisualElement _cardLayer;
        private bool _initialized;

        public IReadOnlyDictionary<string, PlayerCardController> Cards => _cards;

        public void Initialize(VisualTreeAsset cardTemplate, VisualElement cardLayer, GameObject lifecycleOwner)
        {
            if (_initialized) return;
            _cardTemplate = cardTemplate;
            _cardLayer = cardLayer;

            if (_cardTemplate == null) Debug.LogError("[PlayerCardManager] PlayerCard.uxml 未分配。");
            if (_cardLayer == null)    Debug.LogError("[PlayerCardManager] cardLayer 未分配。");

            if (lifecycleOwner != null)
            {
                this.RegisterEvent<E_PlayerJoined>(OnPlayerJoined).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_PlayerLeft>(OnPlayerLeft).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RoomSnapshot>(OnSnapshot).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RoomJoined>(OnRoomJoined).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_IconUpdated>(OnIconUpdated).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            }
            else
            {
                this.RegisterEvent<E_PlayerJoined>(OnPlayerJoined);
                this.RegisterEvent<E_PlayerLeft>(OnPlayerLeft);
                this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated);
                this.RegisterEvent<E_RoomSnapshot>(OnSnapshot);
                this.RegisterEvent<E_RoomJoined>(OnRoomJoined);
                this.RegisterEvent<E_IconUpdated>(OnIconUpdated);
            }

            _initialized = true;
        }

        public void InitializeForTests(VisualTreeAsset cardTemplate, VisualElement cardLayer)
        {
            _cardTemplate = cardTemplate;
            _cardLayer = cardLayer;
            _initialized = true;
        }

        // ─── 事件回调（与原实现一致） ───────────────────────────

        private void OnPlayerJoined(E_PlayerJoined e) { if (e.Player != null) AddOrUpdate(e.Player); }
        private void OnPlayerLeft(E_PlayerLeft e)     { Remove(e.PlayerId); }

        private void OnStateUpdated(E_RemoteStateUpdated e)
        {
            if (string.IsNullOrEmpty(e.PlayerId)) return;
            var room = this.GetModel<IRoomModel>();
            var data = FindRemotePlayer(room, e.PlayerId);
            if (data == null) return;
            if (_cards.TryGetValue(e.PlayerId, out var card)) card.Refresh(data);
            else AddOrUpdate(data);
        }

        private void OnRoomJoined(E_RoomJoined e)   { RebuildFromSnapshot(e.InitialPlayers); }
        private void OnSnapshot(E_RoomSnapshot e)   { RebuildFromSnapshot(e.Players); }

        private void OnIconUpdated(E_IconUpdated e)
        {
            if (string.IsNullOrEmpty(e.BundleId)) return;
            var room = this.GetModel<IRoomModel>();
            foreach (var kv in _cards)
            {
                var data = FindRemotePlayer(room, kv.Key);
                if (data != null && data.ActiveAppBundleId == e.BundleId) kv.Value.Refresh(data);
            }
        }

        // ─── 核心：Add / Remove / Clear ─────────────────────────

        public void AddOrUpdate(RemotePlayerData data)
        {
            if (data == null || string.IsNullOrEmpty(data.PlayerId)) return;

            if (_cards.TryGetValue(data.PlayerId, out var existing))
            {
                existing.Refresh(data);
                return;
            }

            if (_cardTemplate == null)
            {
                Debug.LogError($"[PlayerCardManager] 无法创建卡片：PlayerCard.uxml 未分配。玩家 '{data.PlayerName}' (id={data.PlayerId}) 被跳过。");
                return;
            }
            if (_cardLayer == null)
            {
                Debug.LogError($"[PlayerCardManager] 无法创建卡片：cardLayer 未分配。");
                return;
            }

            var tpl = _cardTemplate.CloneTree();
            var pcRoot = tpl.Q<VisualElement>(className: "pc-root") ?? tpl;
            tpl.style.flexShrink = 0;
            _cardLayer.Add(tpl);

            // 解析位置：优先持久化 → 否则走"下一空位"
            Vector2 pos = ResolveInitialPosition(data.PlayerId);
            pcRoot.style.position = Position.Absolute;
            pcRoot.style.left = pos.x;
            pcRoot.style.top  = pos.y;

            var ctrl = new PlayerCardController(pcRoot);
            ctrl.Setup(data);
            _cards[data.PlayerId] = ctrl;
            _joinOrder.Add(data.PlayerId);

            // 首次摆放的新玩家：写回 Model 以便下次恢复
            var posModel = this.GetModel<IPlayerCardPositionModel>();
            if (!posModel.TryGet(data.PlayerId, out _))
            {
                this.SendCommand(new Cmd_SetPlayerCardPosition(data.PlayerId, pos));
            }

            // 拖拽结束 → 持久化
            var handle = pcRoot.Q<VisualElement>("pc-handle-bar");
            if (handle != null)
            {
                var drag = DraggableElement.MakeDraggable(pcRoot, handle);
                var id = data.PlayerId;
                drag.OnDragEnd += p => this.SendCommand(new Cmd_SetPlayerCardPosition(id, p));
            }
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_cards.TryGetValue(playerId, out var card))
            {
                card.Root.parent?.Remove(card.Root);
                _cards.Remove(playerId);
                _joinOrder.Remove(playerId);
            }
        }

        public void Clear()
        {
            _cardLayer?.Clear();
            _cards.Clear();
            _joinOrder.Clear();
        }

        private void RebuildFromSnapshot(IList<RemotePlayerData> players)
        {
            Clear();
            if (players == null) return;
            for (int i = 0; i < players.Count; i++) AddOrUpdate(players[i]);
        }

        private static RemotePlayerData FindRemotePlayer(IRoomModel room, string playerId)
        {
            if (room == null) return null;
            var players = room.RemotePlayers;
            if (players == null) return null;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && p.PlayerId == playerId) return p;
            }
            return null;
        }

        // ─── 初始位置解析 ────────────────────────────────────────

        private Vector2 ResolveInitialPosition(string playerId)
        {
            var posModel = this.GetModel<IPlayerCardPositionModel>();
            if (posModel != null && posModel.TryGet(playerId, out Vector2 saved))
            {
                return saved;
            }
            return NextSlot();
        }

        /// <summary>
        /// 下一张卡的默认位置：
        ///  - joinOrder 空 → FirstAnchor (40,40)
        ///  - 否则 → prev 右侧 153+12；若 X 越过右界 (layerWidth - 20) → 换行 y += 113+12，x 归 40
        ///  - 若越过下界 → clamp 到 layerHeight - 113 - 20（最后一行允许堆叠）
        /// </summary>
        public Vector2 NextSlot()
        {
            if (_joinOrder.Count == 0) return FirstAnchor;
            var prevId = _joinOrder[_joinOrder.Count - 1];
            if (!_cards.TryGetValue(prevId, out var prev)) return FirstAnchor;
            float prevX = prev.Root.style.left.value.value;
            float prevY = prev.Root.style.top.value.value;

            float layerW = (_cardLayer?.resolvedStyle.width  ?? 0) > 0
                ? _cardLayer.resolvedStyle.width
                : (_cardLayer?.style.width.value.value ?? Screen.width);
            float layerH = (_cardLayer?.resolvedStyle.height ?? 0) > 0
                ? _cardLayer.resolvedStyle.height
                : (_cardLayer?.style.height.value.value ?? Screen.height);

            float x = prevX + CardWidth + Gap;
            float y = prevY;
            if (x + CardWidth > layerW - 20f)
            {
                x = FirstAnchor.x;
                y = prevY + CardHeight + Gap;
            }
            y = Mathf.Min(y, layerH - CardHeight - 20f);
            return new Vector2(x, y);
        }
    }
}
```

注意 `Position.Absolute` 需要 `using UnityEngine.UIElements;`（已有）。

- [ ] **Step 4: 运行测试确认通过**

`run_tests`，跑 `PlayerCardManagerTests` 全部；其他 EditMode 测试也跑一遍确认无回归。

若 `style.left.value.value` 不是 px——注意 UI Toolkit 中 `StyleLength` 的 keyword 判断：用 `style.left.value.unit == LengthUnit.Pixel` 再取 value。测试里若 assert 失败用 `resolvedStyle.left` 替代（不过在无 Layout 的 unit test 中 resolvedStyle 可能是 NaN；在 UI Toolkit EditMode 中 `style.*.value.value` 通常可直接读）。若测试失败，改用 `((Length)card.style.left.value).value` 或手动比较 `StyleLength`。

- [ ] **Step 5: 提交**

```bash
git add Assets/UI_V2/Controller/PlayerCardManager.cs \
        Assets/Tests/EditMode/PlayerCardTests/Editor/PlayerCardManagerTests.cs
git commit -m "feat(ui): PlayerCardManager 绝对定位布局 + 隔壁换行 + 位置持久化

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

### Task 22：精简 `DeskWindowController`

**Files:**
- Modify: `Assets/UI_V2/Controller/DeskWindowController.cs`

- [ ] **Step 1: 删除 UnifiedSettingsPanel 相关字段**

移除以下（Inspector 字段、私有字段、代码块）：
- `_pomodoroSettingsTemplate` / `_onlineSettingsTemplate` / `_petSettingsTemplate` Inspector 字段 + const path
- `_settingsPanel` 字段
- `EnsureSettingsTemplatesLoaded()` 方法
- `root.Q("settings-btn")?.RegisterCallback` 点击回调（设置按钮已迁入番茄钟面板）

保留：PomodoroPanel 模板加载、PlayerCard 模板加载、`_dwWrap` 相关逻辑需要更新为 `_dwCanvas`。

- [ ] **Step 2: 更新 `BindUI()` 引用新节点名**

完整替换 `BindUI`：

```csharp
        private void BindUI()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            root.AddToClassList("dw-root-anchor");

            var dwCanvas = root.Q<VisualElement>("dw-canvas");
            _pomodoroPanelContainer = root.Q<TemplateContainer>("pomodoro-panel");
            var cardLayer = root.Q<VisualElement>("card-layer");

            // PlayerCardManager 改挂 #card-layer
            _playerCardTemplate = EnsureEditorTemplateLoaded(
                _playerCardTemplate, PlayerCardTemplatePath, "PlayerCard.uxml");
            _playerCardManager = new PlayerCardManager();
            _playerCardManager.Initialize(_playerCardTemplate, cardLayer, gameObject);
        }
```

连同 `private VisualElement _dwWrap;` 字段删除（未使用）。

- [ ] **Step 3: 清理不再需要的字段/字符串常量**

删除：
```csharp
        private const string PomodoroSettingsTemplatePath = "...";
        private const string OnlineSettingsTemplatePath   = "...";
        private const string PetSettingsTemplatePath      = "...";

        [Header("设置面板 UXML 模板")]
        [SerializeField] private VisualTreeAsset _pomodoroSettingsTemplate;
        [SerializeField] private VisualTreeAsset _onlineSettingsTemplate;
        [SerializeField] private VisualTreeAsset _petSettingsTemplate;

        private VisualElement _dwWrap;
        private UnifiedSettingsPanelController _settingsPanel;
```

删除 `EnsureSettingsTemplatesLoaded()` 方法。

- [ ] **Step 4: 编译验证**

`read_console filter types: error`，无错；`validate_script` 通过。

- [ ] **Step 5: 人工 PlayMode 烟雾测试**

进入 Play → 确认：
1. 番茄钟面板正常显示在右下角、可拖
2. 点齿轮 → 独立设置面板出现（覆盖全屏，半透明中央）
3. 设置面板关闭按钮工作
4. 远端玩家加入 → 卡片出现在 (40,40)；第二个玩家 → 出现在 (205,40)

- [ ] **Step 6: 提交**

```bash
git add Assets/UI_V2/Controller/DeskWindowController.cs
git commit -m "refactor(ui): DeskWindowController 瘦身，移除设置面板与旧 dw-wrap 逻辑

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase H — 视觉基线

### Task 23：重抓 `UnifiedSettingsPanelImageValidationTests` 基线

**Files:**
- Modify: `Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs`（若有代码改动）
- Modify: 对应基线 PNG（测试产物目录）

- [ ] **Step 1: 读现有测试，了解基线文件位置**

```bash
find /Users/xpy/Desktop/NanZhai/CPA/Assets/Tests/PlayMode/NetworkIntegration -name "UnifiedSettingsPanelImageValidationTests*"
```

打开对应 .cs 文件，确认 `BaselineDir` / 截图名规则。

- [ ] **Step 2: 运行现有视觉测试（应失败，因为独立 UIDocument 改变了布局）**

```
mcp__UnityMCP__run_tests testNames=["<UnifiedSettingsPanelImageValidationTests 的具体方法名>"]
```

`read_console` 检查差异报告位置。

- [ ] **Step 3: 按 unity-visual-image-validation 技能流程审视差异**

调用项目内技能（不是子代理）：查看 `Assets/Tests/PlayMode/NetworkIntegration/.../output/manifest.json`，用 `.claude/skills/unity-visual-image-validation/SKILL.md` 的步骤对比。

- [ ] **Step 4: 确认差异是期望的（独立 UIDocument + 透明主面板），更新基线**

按技能提示将 `manifest.json` 里的 `actual` PNG 复制到基线目录。用 `manage_asset` 或 Bash `cp`。

- [ ] **Step 5: 再次运行测试确认 PASS**

- [ ] **Step 6: 提交基线 + 任何测试代码小改动**

```bash
git add Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs \
        Assets/Tests/PlayMode/NetworkIntegration/<baseline-dir>/*.png \
        Assets/Tests/PlayMode/NetworkIntegration/<baseline-dir>/*.png.meta
git commit -m "test(playmode): 更新 UnifiedSettingsPanel 视觉基线（独立 UIDocument 布局）

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase I — 集成验收

### Task 24：端到端 PlayMode 手动验收

**Files:** 无代码改动；仅验证

- [ ] **Step 1: 进入 PlayMode**

`mcp__UnityMCP__manage_editor operation="start_play_mode"`

- [ ] **Step 2: 按 spec 决策表逐项确认**

| 验证项 | 期望 |
|--------|------|
| 首次进入（清空 PlayerPrefs） | 番茄钟在右下角距 20 处 |
| 拖番茄钟 handleBar → 退出 → 再进 | 位置恢复 |
| 点番茄钟齿轮 | 独立设置面板显示（层级在番茄钟之上） |
| 关闭设置按钮 | 面板消失，主面板交互恢复 |
| 模拟远端玩家加入（用编辑器网络模拟器） | 第 1 张卡在 (40,40) |
| 再加入第 2 位 | 第 2 张卡在 (205,40) |
| 拖第 1 张卡到 (500,300) → 重启 | 恢复 (500,300) |
| 第 1 位退出后重新加入（用同一 PlayerId） | 恢复持久化的 (500,300) |
| 屏幕宽度缩到 400 px，加第 3 位 | 换行到下一行 |

任何一项不符合预期 → 回到对应 Task 修复。

- [ ] **Step 3: 退出 PlayMode**

`mcp__UnityMCP__manage_editor operation="stop_play_mode"`

- [ ] **Step 4: 最后一次全量 `run_tests`**

```
mcp__UnityMCP__run_tests   # 不指定 testNames 跑全部 EditMode/PlayMode
```

期望：全绿。

- [ ] **Step 5: 不需要提交**（只是验收）

---

## Self-Review 结果

**Spec 覆盖检查**（逐节比对 spec 章节）：
- §2 决策摘要 Q1–Q6：均在 Task 1/3/14/16/19/21 覆盖 ✓
- §3.1 场景结构：Task 16 ✓
- §3.2 层级职责：Task 1–8 + 17–22 ✓
- §3.3 持久化：Task 2（Pomodoro）+ Task 3（PlayerCard）✓
- §4 Pencil 改动：Task 9–11 ✓
- §5.1–5.7 UXML/USS：Task 12–15 ✓
- §6.1 Model：Task 1 + 3 ✓
- §6.2 Command：Task 5/6/7 ✓
- §6.3 Controller：Task 17–22 ✓
- §6.4 摆放算法：Task 21 Step 3 的 `NextSlot()` ✓
- §6.5 OnDragEnd：Task 8 ✓
- §7 事件流：Task 7 + 18 ✓
- §8 持久化 Schema：Task 2 + 3 ✓
- §9 测试：Task 1/2/3/6/8/21/23 + 集成 Task 24 ✓
- §10 兼容性：Task 2 `Persistence_LegacySave_MissingField_LeavesSentinel` ✓
- §11 范围外：明确，无需任务 ✓
- §12 交付物清单：完全对应任务 ✓

**Placeholder scan**：全文已搜 "TODO"、"TBD"、"implement later"、"handle edge cases"——无。

**类型 / 命名一致性**：
- `Cmd_SetPomodoroPanelPosition` / `Cmd_SetPlayerCardPosition` / `Cmd_OpenUnifiedSettings` / `Cmd_CloseUnifiedSettings` 在 Task 5/6/7 定义，在 Task 19/21/22 使用 — 一致
- `IPlayerCardPositionModel.TryGet/Set/Remove` 在 Task 3 定义，在 Task 21 使用 — 一致
- `PomodoroPanelPosition` 字段在 Task 1 定义，在 Task 19 使用 — 一致
- `OnDragEnd` 在 Task 8 定义，在 Task 19/21 使用 — 一致
- `PlayerCardManager.Initialize(cardTemplate, cardLayer, lifecycleOwner)` 新签名在 Task 21 Step 3 定义，`DeskWindowController` Task 22 Step 2 使用 — 一致

**作用域**：单个实现计划可覆盖，不需要拆分。
