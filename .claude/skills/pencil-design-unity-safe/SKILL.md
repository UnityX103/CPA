---
name: pencil-design-unity-safe
description: Use when creating, modifying, copying, or iterating Pencil (.pen) UI design drafts inside this CPA Unity project. Enforces Unity UI Toolkit compatibility constraints at the design stage so drafts only contain effects the runtime can actually render (no drop/text shadow, no backdrop blur, no gradient fills, no CSS grid, no z-index, no pill-shaped 999px radii, no em/rem units, no text-decoration, no ::before/::after, no @keyframes, etc.).
---

# Pencil Design Unity-Safe

## 何时使用

- 用户要求创建、修改、补全、复制、迭代一个 Pencil `.pen` UI 设计稿。
- 用户说 "设计 / 画 / 补一个 XX 面板 / 对话框 / 组件 / 控件"。
- 调用 `mcp__pencil__batch_design` 的 `insert` / `copy` / `update` / `replace` 操作**之前**必须过一遍本技能。
- 用户说"帮我设计"、"出一版 UI"、"画一下 XX"、"迭代这个面板"。

## 核心原则

- 本项目所有 .pen 设计稿最终要交付到 Unity UI Toolkit（UXML + USS），不是浏览器。
- 凡是导出到 Unity 一定会被丢弃或画不出来的效果，**在 Pencil 阶段就别画**，不要让视觉期望走在引擎能力之前。
- 圆角、间距、层级、控件替换都按 Unity USS 的能力来决定。
- 保持视觉简洁，优先使用纯色 + 清晰边界 + 合理圆角，避免 Web 式的炫技效果。
- **组件优先（Component-First）**：可复用元素（按钮、滑块、开关、标题、卡片头、对话框、输入框等）必须先在组件库里建一个 Pencil 组件，再以实例形式放进面板，**不要**在面板里直接画一份散装结构。

## 组件优先（Component-First）设计

### 何时必须建组件，不允许散装画

只要满足下列任意一条，就必须在组件库里新建一个 Pencil 组件，再用实例摆进面板：

- 同一视觉元素在 ≥ 2 个面板中出现（如返回按钮、应用按钮、面板标题栏）。
- 同一视觉元素在同一面板中以多种状态出现（如 hover/pressed/disabled/checked，或 slider 的 0%/50%/100%）。
- Unity 侧已经存在对应的可复用 UXML/USS 文件（例如 `Assets/UI_V2/Documents/Components/*` 或 `Assets/UI_V2/Styles/Components/*`）。
- 元素具备明显的"控件语义"（按钮、滑块、Toggle、RadioButton、DropdownField、TextField、对话框、Tab、卡片）。

如果只是面板里一次性的纯装饰图形（背景色块、分隔线、单独一个 Label），可以直接画在面板内，**不必**抽成组件。

### 组件库与实例的存放约定

- Pencil 文件里维护一个集中的"组件库"区域（顶部、独立画板或单独一页都可以），所有可复用组件的"主版本"放这里。
- 在主版本旁边水平排列该组件的**全部状态实例**，状态命名直接对应 USS 伪类或 modifier，例如：
  - 按钮：`Button / default`、`Button / hover`、`Button / pressed`、`Button / disabled`
  - 主按钮 vs 次按钮：`Button / primary`、`Button / secondary`、`Button / ghost`
  - 滑块：`Slider / 0%`、`Slider / 50%`、`Slider / 100%`、`Slider / disabled`
  - 开关：`Toggle / off`、`Toggle / on`、`Toggle / disabled`
  - 标题：`PanelHeader / default`、`PanelHeader / with-back-button`
- 状态实例必须是**真实复刻**该状态下的视觉，而不是文字标注。例如 hover 状态要把背景色调成实际的 hover 色，而不是只在旁边写"hover"。
- 状态实例与主组件挨在一起，便于一眼比对差异；不允许把状态实例散落到各个面板里面再"反查"。
- 组件命名要稳定，能直接对应到 Unity 侧的目标文件名（PascalCase，例如 `BackButton`、`ConfirmDialog`、`PanelHeader`、`PrimaryButton`）。

### 复用与覆盖

- 面板里使用组件时，必须以"实例"形式引用主组件，而不是把主组件复制粘贴一份再改。
- 实例上只允许覆盖**内容**（文字、图标、绑定的 name），不允许在实例上改组件本身的视觉属性（圆角、padding、颜色、字号）；这类改动必须回到主组件或新增一个状态变体。
- 如果某个面板需要一个组件的"特殊变体"，先判断：是新增一个状态（加进组件库里同一组件的旁边），还是新增一个独立组件。规则：变体仅靠伪类/class 切换 → 加状态；变体在结构或语义上不同 → 新组件。

### 与 Unity 侧的对应关系

- Pencil 组件名 ↔ Unity `Assets/UI_V2/Documents/Components/<Name>.uxml` + `Assets/UI_V2/Styles/Components/<Name>.uss` 一一对应。
- Pencil 组件的状态实例 ↔ USS 中的伪类/modifier class（例如 `:hover`、`:disabled`、`.primary`、`.is-checked`、`.value-50`）。
- 设计阶段就要想清楚：每个状态实例在 Unity 侧落到哪个伪类或 class 上，避免导出阶段反推不出来。

## 设计阶段硬约束（画之前必须知道）

### 必须避免 — 画了也会在导出阶段被丢掉

| 类别 | 禁用 | 替代方案 |
|------|------|----------|
| 阴影 | drop shadow / inner shadow / text-shadow | 不加阴影；需要强调用纯色边框或提亮背景 |
| 模糊 | 毛玻璃 / backdrop-filter / Gaussian blur | 背景换半透明纯色（如 `rgba(255,253,251,0.85)`）模拟亚克力感 |
| 渐变 | linear-gradient / radial-gradient 填充 | 纯色；或备注"导出时烘焙成 PNG" |
| 圆角 | `999px` / `9999px` / `var(--radius-full)` 胶囊角 | 具体像素值，等于宽或高的一半（如 28 高写 14px） |
| 椭圆角 | `border-radius: a / b` 长短半径 | 单一圆角半径 |
| 布局 | CSS Grid (`display: grid`) | 嵌套 flex（外层 column + 内层 row） |
| 定位 | `position: fixed` / `sticky` | `absolute` + 挂到 overlay 根节点 |
| 层级 | `z-index` | DOM 顺序，后写盖前写 |
| 文本 | text-decoration 下划线 / 删除线 | 需要时用一根 1px 的 VisualElement 模拟 |
| 文本 | text-shadow | `-unity-text-outline-color` + `-unity-text-outline-width` |
| 裁切 | clip-path / mask-image 异形裁剪 | `overflow: hidden` + `border-radius` |
| 装饰 | `::before` / `::after` 伪元素 | 真实 VisualElement |
| 动画 | `@keyframes` 关键帧动画 | 单次 transition；复杂动画写 C# |
| 混合 | `mix-blend-mode` / `background-blend-mode` | 不用混合模式 |
| 变换简写 | `transform: translate(...) rotate(...)` | 独立属性 `translate` / `rotate` / `scale` |
| 背景 | 多重 `background-image` | 单图；或叠多个 VisualElement |
| 背景 | `background-repeat` 平铺 | 准备足够大的贴图 |

### 单位与数值硬约定

- 字号统一用 `px`；**不要用** `em` / `rem` / `vw` / `vh` / `vmin` / `vmax` / `ch` / `fr`。
- 字重只用 `normal` 或 `bold`；**不要**用 `500` / `600` / `700` 等数值字重。
- 颜色用 `rgb()` / `rgba()` / `#hex`；**不要**用 `hsl()` / `hsla()` / `color-mix()` / `oklch()`。
- 所有尺寸都用 `px` 或 `%`，不混用 Web-only 单位。

### 布局与方向

- Unity UI Toolkit 默认 `flex-direction: column`（和 Web 默认 `row` 相反）；画横向布局时要明确这是 row。
- 弹窗 / 浮层一律用 `absolute` 定位，视为挂在 overlay 根节点。
- 滚动容器在设计稿上显式标记"Unity 侧套 ScrollView"。
- 叠层通过节点顺序表达（后写的覆盖前写的），不要在备注里依赖 z-index。
- 容器之间的间距：Unity 2022+ 支持 `gap`，保守起见优先画成 `margin` 行为。

### 控件替换

Pencil 里的 Web-style 控件导出后要被替换成 Unity 控件，画设计稿时就按目标画：

| Pencil / Web | Unity 控件 |
|---------------|------------|
| `<select>` 下拉 | `DropdownField` |
| `<input type="text">` | `TextField` |
| `<input type="number">` | `IntegerField` / `FloatField` |
| `<input type="checkbox">` | `Toggle` |
| `<input type="radio">` | `RadioButton` |
| 进度条 / 滑块 | `Slider`（必要时标注要定制 tracker/dragger） |
| 滚动区域 | `ScrollView`（显式标记） |
| 按钮 | `Button`（标明圆角具体像素、hover/disabled 状态） |

### 图标与图片

- 图标按 1:1 画布设计，目标导出 `256×256` 等比透明 PNG。
- 避免纯 SVG 装饰；复杂矢量要能一键导出为 PNG（或转 `VectorImage` asset）。
- 图标配色直接画进原图，**不要**通过 CSS `filter` 调色 / 调亮度。
- 非正方形图注意留白，保证导出时不拉伸变形。

### 文本对齐

- Unity 用 `-unity-text-align` 的 9 宫格值：`upper-left` / `upper-center` / `upper-right` / `middle-left` / `middle-center` / `middle-right` / `lower-left` / `lower-center` / `lower-right`。
- 不是 CSS 的 `text-align: center` + `vertical-align: middle` 分开写。
- 行距没有 `line-height`；用 `-unity-paragraph-spacing` 或拆多个 Label。
- 省略号只能单行（`overflow: hidden` + `white-space: nowrap` + `text-overflow: ellipsis`）。

## 何时 OK（可以放心用的效果）

- `:hover` / `:focus` / `:active` / `:disabled` / `:checked` 状态样式。
- 简单 `transition`（颜色、尺寸、opacity、translate/rotate/scale、border-*）。
- `translate` / `rotate` / `scale` 独立属性（Unity 2021+，**不是** `transform` 简写）。
- `overflow: hidden` + `border-radius` 做圆角裁切。
- 九宫格切片（Unity 有 `-unity-slice-*` 对应）。
- `-unity-text-outline` 描边替代 `text-shadow`。
- `letter-spacing` 字距。
- CSS 变量（USS `:root { --foo: ... }` + `var(--foo)`）。

## 工作流

1. **读需求** — 明确要画哪个面板 / 组件及哪些状态。
2. **打开目标 .pen**
   - `mcp__pencil__get_editor_state`（看当前打开的 .pen 与选中节点）。
   - 需要切换时 `mcp__pencil__open_document`。
3. **盘点可复用元素** — 在动笔之前，先列出本次要画的 UI 中哪些是可复用元素（按钮、滑块、Toggle、标题栏、对话框、输入框等）。对每个元素：
   - 看组件库里是否已有同名/同语义组件 → 有就用实例引用。
   - 没有 → 先在组件库里新建该组件的主版本 + 全部状态实例，再回面板里引用。
   - 严禁直接在面板里散装画一遍可复用元素。
4. **参考项目变量** — 读取现有 Pencil variables 与 Unity `Variables.uss`，颜色 / 圆角 / 间距尽量复用：
   - `Assets/UI_V2/Styles/Variables.uss` 中的 `--color-*`、`--radius-*`、`--space-*` 是事实标准。
5. **按硬约束草拟结构** — 在脑内或纸面过一遍本技能的"必须避免"表，剔除 Web-only 效果。
6. **调用 `mcp__pencil__batch_design`** — insert/copy/update/replace 时，不要写入被列入"必须避免"的属性。组件主版本与状态实例的创建优先于面板内容。每次最多 25 个操作。
7. **保存 .pen** — 改完必须立即触发 `pencil-autosave` 技能执行 ⌘S，否则 Pencil MCP 的变更只在内存。
8. **（如需导出）跳到 `pencil-to-unity-ui-export`** — 同步到 UXML/USS、视觉测试、weixin 发图对比。

## 设计完成前检查清单

提交或让用户看之前过一遍:

- [ ] 所有可复用元素（按钮、滑块、Toggle、标题栏、对话框等）都建在组件库里，并且主组件旁边铺好了全部状态实例（hover/pressed/disabled/checked、slider 0%/50%/100% 等）。
- [ ] 面板里没有"散装"复制的可复用元素；所有复用都走"组件实例"。
- [ ] 实例上只覆盖了内容（文字/图标/name），没有偷偷改组件视觉属性；新视觉变体已加成新状态或新组件。
- [ ] 组件名是 PascalCase，能直接对应 Unity 侧 `Assets/UI_V2/Documents/Components/<Name>.uxml` + `Assets/UI_V2/Styles/Components/<Name>.uss`。
- [ ] 没有 drop shadow / inner shadow / text-shadow 效果。
- [ ] 没有 blur / backdrop-filter 效果。
- [ ] 没有渐变填充（或已明确标注"导出时烘焙成 PNG"）。
- [ ] 圆角没有 `999px` / `9999px` / `var(--radius-full)`；胶囊按钮按宽高一半给具体像素。
- [ ] 没有椭圆角（长短半径不一致）写法。
- [ ] 没有 CSS Grid / `position: fixed` / `sticky` / `z-index` 依赖。
- [ ] 字号全部 `px`；字重只用 `normal` / `bold`。
- [ ] 颜色是 rgb/rgba/hex；未使用 hsl / color-mix。
- [ ] 没有 `text-decoration` 下划线 / 删除线；没有 `::before` / `::after` 装饰。
- [ ] 没有 `@keyframes` / 多重 background / 平铺 background-repeat。
- [ ] 控件类型在设计稿中能对应到 Unity 标准（Button / Label / TextField / DropdownField / Toggle / RadioButton / Slider / ScrollView）。
- [ ] 图标 / 图片等比，能按 `256×256` 画布友好导出。
- [ ] 已调用 `pencil-autosave` 保存到磁盘。

## 对接其他技能

- **保存 .pen 文件** → `pencil-autosave`（⌘S 落盘）。
- **导出到 Unity UXML/USS** → `pencil-to-unity-ui-export`（同步 + 视觉测试 + weixin 发图）。
- **导出后视觉测试验证** → `unity-visual-image-validation`（manifest.json + actual/baseline 对比）。
- **微信发图** → `weixin`（把 actual/baseline 推给用户复核）。

## 详细差异参考

完整的 Unity UI Toolkit 与 Web CSS 差异速查表在姊妹技能里：

- `.claude/skills/pencil-to-unity-ui-export/SKILL.md` 的「Unity UI Toolkit 与 Web CSS 差异速查」章节，包含布局/视觉效果/选择器/单位/字体文本/图像/交互控件/动画/不透明混合 9 类详细差异表。

当不确定某个效果能不能用时，回去查那张速查表。
