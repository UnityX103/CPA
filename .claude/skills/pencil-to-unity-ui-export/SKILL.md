---
name: pencil-to-unity-ui-export
description: Use when exporting Pencil .pen UI designs into this CPA Unity UI Toolkit project, including UXML/USS updates, asset/image export rules, unsupported-effect handling, and required screenshot-based visual validation after import.
---

# Pencil To Unity UI Export

## 何时使用

- 用户要求把 Pencil / `.pen` 中的 UI “导回 Unity”“同步到 Unity”“生成 UXML/USS”。
- 用户给出 Pencil Node ID，需要把对应设计转成 `Assets/UI_V2/Documents/*.uxml`、`Assets/UI_V2/Styles/*.uss`、图片资源或测试 baseline。
- 导入后需要编写或更新视觉测试，运行测试产出截图，再由助手人工判断差异和修改点。

## 核心原则

- 保持 Unity UI Toolkit 可实现性优先，不追求 Pencil 渲染器的逐像素复刻。
- 阴影、毛玻璃、背景模糊等 Unity 当前不支持或项目不支持的视觉效果，在导入阶段直接忽略；对比阶段也不要把这些效果缺失判为需要修复。
- 圆角必须使用有限值；不要把 Pencil 的超大圆角或胶囊圆角导成 `999px`、`9999px`、`var(--radius-full)` 这类无限圆角。
- 图片和图标导出必须保持等比；未指定尺寸时默认以 `256×256` 作为导出目标或容器基准。
- 导入后必须有截图型视觉测试。测试只负责打开对应 UI、截图、写 manifest，不做像素一致断言；视觉判断由助手打开 actual 与 baseline 后给出修改建议。
- 视觉测试必须覆盖本次导入影响到的所有面板和关键状态，不允许只截当前正在修改的单个局部后就结束。

## Pencil 读取与映射流程

1. 用 Pencil MCP 读取目标节点：`batch_get(filePath="PUI.pen", nodeIds=[...], readDepth=...)`。
2. 记录节点的 `name`、尺寸、布局、子节点层级、文本、颜色、padding、gap、圆角、图片/图标引用。
3. 找 Unity 目标文件：
   - UXML：`Assets/UI_V2/Documents/<PanelName>.uxml`
   - USS：`Assets/UI_V2/Styles/<PanelName>.uss`
   - 组件 UXML/USS：`Assets/UI_V2/Documents/Components/*`、`Assets/UI_V2/Styles/Components/*`
   - 图片/图标：优先放入 `Assets/UI_V2/Icons` 或已有同类目录。
4. 搜索控制器引用，避免改名破坏 C# 查询：重点检查 `root.Q("name")`、`Q<Button>`、`Q<Slider>`、class 选择器和测试引用。
5. 如果 Pencil 节点是状态展示（例如滑块半程、弹窗无关闭按钮），Unity 侧应同步结构/样式，并保留控制器需要的 `name`。

## Unity 导入规则

### UXML

- 只表达结构和可被控制器查询的元素。
- 保留现有 `name`，除非同步修改所有 C# 与测试引用。
- 对运行时需要查询的控件使用真实 UI Toolkit 类型，例如 `Button`、`Slider`、`Label`。
- 如果原生控件无法呈现 Pencil 状态（例如 Slider 左侧填充），优先考虑：
  1. USS 定制原生控件内部 class；
  2. 添加辅助 VisualElement 表示视觉状态；
  3. 必要时改为自定义组合控件，但要检查控制器逻辑。

### USS

- 使用项目变量：`Assets/UI_V2/Styles/Variables.uss` 中已有颜色、间距和圆角优先。
- 圆角使用有限值：
  - 小控件：`4px`、`8px`、`10px`、`12px`、`14px`
  - 卡片/面板：`16px`、`20px`、`24px`
  - 圆形图标按钮：半径 = 宽高的一半，例如 `28×28` 用 `14px`
- 禁止导入：`999px`、`9999px`、`var(--radius-full)`，因为 Unity 会把超大圆角处理成异常效果或与 Pencil 不一致。
- 阴影、毛玻璃、背景模糊直接忽略，不写入 USS；如果已有注释需要说明，可写“Unity 当前忽略 Pencil shadow/blur”。
- 透明背景、边框、padding、gap、对齐方式应尽量同步。

### 图片与图标

- 只有 Pencil 节点包含图片、图标、纹理或位图填充时才导出图片；没有图片就跳过。
- 默认导出尺寸为 `256×256`。
- 必须等比导出，不要拉伸：
  - 正方形图标：导出到 `256×256`。
  - 非正方形图：保持长宽比，放进 `256×256` 透明画布或使用 Unity `background-size: contain`。
- Unity USS 使用：
  - `background-image: url("project://database/...")` 或项目现有路径写法；
  - `-unity-background-scale-mode: scale-to-fit`；
  - `background-size: contain`；
  - 居中显示。
- 导出后刷新 Unity，并检查 `.meta` 是否生成。

## 不支持效果处理

导入时直接忽略以下 Pencil 效果：

- 外阴影、内阴影、drop shadow。
- 毛玻璃、背景模糊、backdrop blur。
- 复杂混合模式、滤镜、非 UI Toolkit 标准效果。

视觉对比时也忽略这些差异，只判断布局、颜色、文字、控件状态、图标尺寸与裁切等 Unity 应支持的部分。

## 导入后必须编写视觉测试

1. 优先复用 `NZ.VisualTest.VisualImageTestBase`。
2. 测试放在合适的 PlayMode 测试目录，例如：
   - `Assets/Tests/PlayMode/NetworkIntegration/*ImageValidationTests.cs`
3. 测试职责：
   - 加载对应场景或创建测试 UIDocument；
   - 打开/切换到目标界面状态；
   - 覆盖本次导入涉及的所有面板；如果导入影响统一设置窗口、弹窗、子面板或组件库，必须分别截图；
   - 覆盖关键交互状态，例如初始值、修改值后、应用按钮可见/不可见、弹窗显示/隐藏、tab active、slider 进度等；
   - 等待目标 VisualElement 有 panel、宽高大于 0；
   - 调用 `CaptureStep(stepName, target, baselinePath, notes)`；
   - 断言截图文件和 manifest 存在。
4. 测试禁止职责：
   - 不做像素一致断言；
   - 不因为阴影、毛玻璃、模糊缺失失败；
   - 不把 NUnit Passed 解读为视觉正确。
5. baseline 放在 `TestArtifacts/PencilReferences`。如果是从 Pencil 导出给人工对比，命名应清晰，例如 `global-settings-panel-half.png`。
6. 如果某个新状态暂时没有 Pencil baseline，仍然要截图并在 manifest 中登记；`baselinePath` 可为空，但最终回复必须说明该 step 需要人工判断或补充目标图。

### 面板覆盖要求

- 对于单个面板导入：至少覆盖该面板的默认状态和本次改动相关状态。
- 对于统一设置类容器：必须覆盖所有受影响 tab/子面板，不只截当前 tab。
- 对于通用组件变更（按钮、输入框、滑块、弹窗）：必须覆盖所有使用该组件且本次导入可能影响的面板。
- 对于“修改值后才出现差异”的控件，例如应用按钮、slider、输入框校验状态，必须新增单独 step 保存输出。
- 每个 step 的 `name` 必须能看出面板和状态，例如 `global-settings-changed-value`、`confirm-dialog-no-close`。

## 视觉判断流程

导入并运行测试后，必须使用 `unity-visual-image-validation` 技能继续判断：

1. 用 `run_tests` 跑目标 PlayMode 测试。
2. 用 Unity 执行代码读取 `Application.temporaryCachePath`。
3. 找到最新 `TestOutput/<TestMethod>/<RunId>/manifest.json`。
4. 逐个打开：
   - `manifest.outputDirectory + actualImagePath`
   - `baselineImagePath`
5. 只做人工视觉判断，不做像素一致结论。
6. 忽略阴影、毛玻璃、背景模糊等不支持效果。
7. 输出差异和修改建议，至少覆盖：
   - 尺寸范围与截图目标是否一致；
   - 位置、留白、padding、gap；
   - 颜色与透明度；
   - 圆角是否有限且视觉正确；
   - 文本内容、字号、字重；
   - 控件状态，例如 slider 进度、按钮显隐、tab active；
   - 图标是否等比、是否默认 256×256 导出、是否裁切。

## Discord 发送验证图

视觉测试产出截图后，必须使用 `discord` 技能把所有 actual 截图和对应 baseline/目标截图发送给用户，便于人工复核。

### 发送前检查

- 先读取 `manifest.json`，不要手写路径。
- 对每个 `steps[]` 解析：
  - actual：`Path.Combine(manifest.outputDirectory, step.actualImagePath)`
  - baseline：优先使用 `step.baselineImagePath`；如果用户另给外部目标图，则使用外部目标图。
- 确认每个文件存在；不存在时先在回复中说明缺失，不要假装已发送。
- 如果未配置 `DISCORD_BOT_TOKEN` 或 `DISCORD_CHANNEL_ID`，按 `discord` 技能说明提示用户配置；不要把 token 写入仓库或日志。

### 发送内容要求

每个 step 至少发送两张图：

1. `actual`：Unity 截图产物。
2. `baseline`：Pencil 目标图或用户提供的目标图。

建议发送顺序：

1. 先发一条文本摘要，包含测试名、runId、step 数量、说明“阴影/毛玻璃/背景模糊差异忽略”。
2. 按 step 发送 actual 图，说明格式：`[stepName] actual - Unity 导入截图`。
3. 紧接着发送 baseline 图，说明格式：`[stepName] baseline - Pencil 目标图`。
4. 如果有多个 step，不要只发送第一组；必须发送 manifest 中所有 step 的 actual 与 baseline。

### 推荐命令

使用 `discord` 技能脚本发送文件：

```bash
python3 /Users/xpy/.codex/skills/discord/scripts/discord_sender.py send "视觉测试 <TestName> / <RunId>：已生成 <N> 个 step。请忽略 Unity 不支持的阴影、毛玻璃和背景模糊差异。"
python3 /Users/xpy/.codex/skills/discord/scripts/discord_sender.py file "<actual_png_path>" "[<stepName>] actual - Unity 导入截图"
python3 /Users/xpy/.codex/skills/discord/scripts/discord_sender.py file "<baseline_png_path>" "[<stepName>] baseline - Pencil 目标图"
```

如果需要指定频道 ID：

```bash
python3 /Users/xpy/.codex/skills/discord/scripts/discord_sender.py file <channel_id> "<png_path>" "说明文字"
```

发送完成后，在最终回复里列出已发送的 step 名称，以及任何未发送成功或缺失的文件。

## 常见问题与处理

- Unity 图比 Pencil 少一圈透明/阴影：通常是截图目标不同。优先统一截图范围，不要为阴影修改 UI。
- Pencil 是胶囊按钮，Unity 变成怪异圆角：把 `999px` 改成宽高一半以内的有限半径。
- Slider 左侧填充缺失：Unity 原生 Slider 可能没有按 Pencil 状态显示，需要定制内部 tracker/dragger 或改组合控件。
- 图标变形：检查导出是否等比，Unity USS 是否使用 contain/scale-to-fit。
- 视觉测试通过但画面明显不对：测试只证明截图产出成功，必须人工查看 actual/baseline 后下结论。

## 完成前检查清单

- [ ] 已读取 Pencil 目标节点并确认尺寸/层级/状态。
- [ ] 已更新对应 UXML/USS，并保护 C# 查询名。
- [ ] 所有圆角都是有限值。
- [ ] 阴影、毛玻璃、模糊没有作为 Unity 必实现项导入。
- [ ] 如有图片/图标，已等比导出，默认 256×256。
- [ ] 已刷新 Unity 资源并检查相关控制台错误。
- [ ] 已新增或更新 PlayMode 视觉测试，仅产出截图和 manifest。
- [ ] 视觉测试已覆盖本次导入影响到的所有面板和关键状态，包括修改值后/按钮状态等动态表现。
- [ ] 已运行测试并用 `unity-visual-image-validation` 人工判断差异。
- [ ] 已使用 `discord` 技能把所有 actual 截图和对应 baseline/目标截图发送给用户；如未配置 Discord，已明确说明缺失配置。
