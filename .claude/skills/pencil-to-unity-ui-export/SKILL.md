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
- 导入前先用下方「Unity UI Toolkit 与 Web CSS 差异速查」过一遍 Pencil 设计稿，识别"Web 能画但 Unity 画不出来"的部分，在导入阶段就降级或丢弃，不要带到 USS 调试阶段。
- 阴影、毛玻璃、背景模糊等 Unity 当前不支持或项目不支持的视觉效果，在导入阶段直接忽略；对比阶段也不要把这些效果缺失判为需要修复。
- 圆角必须使用有限值；不要把 Pencil 的超大圆角或胶囊圆角导成 `999px`、`9999px`、`var(--radius-full)` 这类无限圆角。
- 图片和图标导出必须保持等比；未指定尺寸时默认以 `256×256` 作为导出目标或容器基准。
- 导入后必须有截图型视觉测试。测试只负责打开对应 UI、截图、写 manifest，不做像素一致断言；视觉判断由助手打开 actual 与 baseline 后给出修改建议。
- 视觉测试必须覆盖本次导入影响到的所有面板和关键状态，不允许只截当前正在修改的单个局部后就结束。
- **组件优先（Component-First）导出**：面板里使用的可复用元素（按钮、滑块、Toggle、标题栏、对话框、卡片等）一律走"组件库 → Unity 组件文件"通道，不允许把组件结构内联展开到面板 UXML/USS 里。Pencil 主组件旁边的状态实例 → Unity USS 中的伪类 / modifier class 状态规则。

## 组件优先（Component-First）导出

### 触发条件

只要 Pencil 设计稿里出现以下任一情况，就必须走组件流程：

- 节点是 Pencil 组件实例（被复用的元素），不是一次性图形。
- 节点对应的视觉在 Pencil 组件库里有"主组件 + 状态实例"成组存在。
- 节点语义是控件（按钮、滑块、Toggle、RadioButton、DropdownField、TextField、对话框、Tab、卡片、面板标题）。
- Unity 侧 `Assets/UI_V2/Documents/Components/` 或 `Assets/UI_V2/Styles/Components/` 下已有同名/同语义文件。

### 导出面板时的组件检查流程

每次导出一个面板（`Assets/UI_V2/Documents/<Panel>.uxml`）之前，先按下面顺序检查它依赖的每个组件：

1. **识别面板里用到了哪些组件**
   - 用 `mcp__pencil__batch_get` 读面板节点，沿子树找出所有"组件实例"节点（Pencil 里通常有 `componentId` / `mainComponent` 之类标记，或节点名直接对应组件库里的主组件名）。
   - 对照 Pencil 组件库，列出本次面板用到的组件清单（PascalCase 组件名）。

2. **检查组件在 Unity 中是否已存在**
   - 对每个组件名 `<Name>`，检查：
     - `Assets/UI_V2/Documents/Components/<Name>.uxml` 是否存在
     - `Assets/UI_V2/Styles/Components/<Name>.uss` 是否存在
   - 若两者都已存在 → 当前面板 UXML 用 `<ui:Instance template="..."/>` 或现有项目约定的引用方式复用，不要把组件内部结构再展开一遍写到面板里。
   - 若任一不存在 → 进入下一步"导出该组件"。

3. **导出缺失的组件（基于状态实例驱动 USS）**
   - 在 Pencil 组件库里 `mcp__pencil__batch_get` 读取该组件的**主组件 + 全部状态实例**。
   - 主组件 → 对应 `Components/<Name>.uxml` 的结构 + `Components/<Name>.uss` 的基础样式（默认状态）。
   - 每个状态实例 → 对应 `Components/<Name>.uss` 中的一条**状态规则**：
     - 交互伪类状态（hover / pressed / focused / disabled / checked）→ USS `:hover` / `:active` / `:focus` / `:disabled` / `:checked` 选择器。
     - 视觉变体（primary / secondary / ghost、small / medium / large）→ USS modifier class（`.primary`、`.size-small`）。
     - 进度/数值状态（slider 0% / 50% / 100%）→ USS modifier class 或定制 `.unity-base-slider__tracker` / `.unity-base-slider__dragger` 内部 class。
   - 状态规则只写出与默认状态**有差异**的属性（背景色、边框、字色、translate、scale 等），不要把整套属性复制一遍。
   - 不要凭空发明"应该有的状态"：USS 中能写出的状态必须能在 Pencil 组件库里找到对应的状态实例；组件库没画的状态，留给后续设计补，不要导出阶段杜撰。

4. **回到面板**
   - 面板 UXML 引用 `<ui:Instance template="<Name>"/>`，并给 `name` / `class` 让控制器和 USS 能定位（保留控制器需要的 `name`）。
   - 面板 USS 不重复组件内部样式；只允许写"组件在该面板里的位置 / 尺寸 / 间距"覆盖。
   - 检查 C# 控制器对该组件的 `Q<>` / `Q("name")` 调用是否仍然命中。

### 组件 USS 状态映射速查

| Pencil 状态实例 | Unity USS 选择器 |
|------------------|-------------------|
| `Button / hover` | `Button:hover`、`.button:hover` |
| `Button / pressed` | `Button:active` |
| `Button / focused` | `Button:focus` |
| `Button / disabled` | `Button:disabled` |
| `Button / primary` / `secondary` / `ghost` | `.button.primary` / `.button.secondary` / `.button.ghost` |
| `Toggle / on` / `off` | `Toggle:checked` / `Toggle`（默认） |
| `Slider / 0%` / `50%` / `100%` | 控制 `.unity-base-slider__tracker` 宽度 / `.unity-base-slider__dragger` 位置；通常由 C# 同步进度，USS 只定 tracker/dragger 视觉 |
| `Slider / disabled` | `Slider:disabled` 或 `Slider:disabled .unity-base-slider__tracker` |
| `PanelHeader / with-back-button` | `.panel-header.has-back` |
| `ConfirmDialog / no-close` | `.confirm-dialog.no-close` |

### 导出顺序约定

- 面板里用到的组件**先**导出/更新，再导出/更新面板本身。
- 多个面板共用同一组件时，组件文件只更新一次；不要让面板 A 的导出和面板 B 的导出各自维护一份组件副本。
- 修改组件 USS 后，必须复查所有引用该组件的面板视觉测试，看是否需要补 step。

## Pencil 读取与映射流程

1. 用 Pencil MCP 读取目标节点：`batch_get(filePath="PUI.pen", nodeIds=[...], readDepth=...)`。
2. 记录节点的 `name`、尺寸、布局、子节点层级、文本、颜色、padding、gap、圆角、图片/图标引用。
3. **盘点面板依赖的组件**（见上文「组件优先（Component-First）导出」章节）：
   - 列出面板里所有 Pencil 组件实例对应的组件名。
   - 对每个组件名检查 `Assets/UI_V2/Documents/Components/<Name>.uxml` 与 `Assets/UI_V2/Styles/Components/<Name>.uss` 是否已存在。
   - 不存在的组件先按"主组件 + 状态实例 → UXML + USS 状态规则"流程导出，再回头处理面板。
4. 找 Unity 目标文件：
   - UXML：`Assets/UI_V2/Documents/<PanelName>.uxml`
   - USS：`Assets/UI_V2/Styles/<PanelName>.uss`
   - 组件 UXML/USS：`Assets/UI_V2/Documents/Components/*`、`Assets/UI_V2/Styles/Components/*`
   - 图片/图标：优先放入 `Assets/UI_V2/Icons` 或已有同类目录。
5. 搜索控制器引用，避免改名破坏 C# 查询：重点检查 `root.Q("name")`、`Q<Button>`、`Q<Slider>`、class 选择器和测试引用。
6. 如果 Pencil 节点是状态展示（例如滑块半程、弹窗无关闭按钮），优先把它当成组件的"状态实例"处理：状态规则写到组件 USS，面板里只放实例引用，不在面板里展开组件结构。

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

## Unity UI Toolkit 与 Web CSS 差异速查

Unity 6（UI Toolkit）的 USS 在语法上贴近 CSS，但只实现了一个受限子集，并用 Yoga 布局引擎替代了浏览器排版。以下差异直接决定 "Pencil 能画但 Unity 画不出来" 的边界。导入前先用这张表过一遍设计稿，不兼容效果在 Pencil→Unity 阶段就要降级或丢弃。

### 布局与定位

| 能力 | Web CSS | Unity USS | Pencil 导出对策 |
|------|---------|-----------|-----------------|
| 盒模型 | content-box / border-box 可切换 | 固定 border-box，不能切换 | 换算 padding/border 时直接按 border-box 理解 |
| 默认 flex-direction | `row` | **`column`** | 根容器若要横排，UXML/USS 必须显式写 `flex-direction: row` |
| CSS Grid (`display: grid`) | 支持 | **不支持**；`display` 只接受 `flex` / `none` | 网格拆成嵌套 flex（外层 column + 内层 row） |
| `position: fixed` / `sticky` | 支持 | **不支持**；只有 `absolute` / `relative` | 浮窗改 `absolute`，挂到 overlay 根节点 |
| `z-index` | 支持 | **不支持** | 靠 UXML 文档顺序（后写盖前写）+ 父节点层级 |
| `calc()` | 支持 | **不支持** | 提前算成 px，或运行时在 C# 里算 |
| `@media` 查询 | 支持 | **不支持** | 响应式用代码切 class 或换 StyleSheet |
| `gap` | 支持 | Unity 2022+ 稳定可用 | 版本不确定时退化为 `margin` |
| `overflow: scroll / auto` | 支持 | **不支持**；`overflow` 只有 `visible` / `hidden` | 需要滚动必须套 `ScrollView` 控件 |

### 视觉效果

| 能力 | Web CSS | Unity USS | Pencil 导出对策 |
|------|---------|-----------|-----------------|
| `box-shadow` | 支持 | **不支持** | 直接忽略；必要时烘焙成带阴影的九宫格贴图 |
| `filter` / `backdrop-filter` | 支持 | **不支持** | 模糊 / 亮度 / 饱和全部忽略，背景降级为半透明纯色 |
| `linear-gradient` / `radial-gradient` | 支持 | **不支持**（`background-image` 只吃 URL） | 烘焙成 PNG 贴图，或退化为纯色 |
| `clip-path` / `mask-image` | 支持 | **不支持** | 换成 `overflow: hidden` + `border-radius` 裁切 |
| `transform` 简写 | 支持 | **不支持简写**；Unity 2021+ 提供独立 `translate` / `rotate` / `scale` 属性 | Pencil 的旋转/缩放要拆成独立 USS 属性 |
| `border-radius` 椭圆角（`a/b` 语法） | 支持 | **不支持** | 只写单一半径，椭圆角降级为最接近的圆角 |
| `text-shadow` | 支持 | **不支持** | 删除;需要描边改用 `-unity-text-outline-color` + `-unity-text-outline-width` |
| `text-decoration`（下划线/删除线） | 支持 | **不支持** | 需要时用一根 1px 的 VisualElement 模拟 |
| `::before` / `::after` | 支持 | **不支持** | 装饰元素写成真实的 UXML VisualElement |
| `@keyframes` / `animation` | 支持 | **不支持** | 只能用 `transition` 做单次过渡，复杂动画写 C# |
| `mix-blend-mode` / `background-blend-mode` | 支持 | **不支持** | 忽略 |

### 选择器

- **支持**：类型、class（`.foo`）、name（`#foo`）、`:root`、状态伪类 `:hover` / `:active` / `:focus` / `:disabled` / `:checked`、组合器（空格、`>`、`+`、`~`）。
- **不支持**：`:nth-child`、`:first-of-type`、`:last-of-type`、`:not()`、`:is()`、`:has()`、属性选择器 `[type="..."]`、伪元素 `::before` / `::after`。

### 单位与取值

- 只支持 **`px`** 和 **`%`**；**不支持** `em` / `rem` / `vw` / `vh` / `vmin` / `vmax` / `ch` / `fr`。
- 颜色支持 `rgb()` / `rgba()` / `#hex` / 命名色；**不支持** `hsl()` / `hsla()` / `color-mix()` / `oklch()` / `color()`。
- `font-weight` 数值（100–900）不被识别；只能用 `-unity-font-style: normal | bold | italic | bold-and-italic`。
- 字号必须是 `px`，Pencil 若给了 `rem` / `em` / `vw` 都要先换算。

### 字体与文本

- 字体必须是工程内资源：`-unity-font-definition: url("project://database/...")`；不支持 Web `@font-face` 网络下载。
- 没有 `line-height`；行距用 `-unity-paragraph-spacing`，或拆成多个 Label。
- `white-space` 只接受 `normal` / `nowrap`；不支持 `pre-wrap` / `pre-line` / `break-spaces`。
- 文本对齐使用 `-unity-text-align`，值为 `upper-left` / `upper-center` / `upper-right` / `middle-left` / `middle-center` / `middle-right` / `lower-left` / `lower-center` / `lower-right`；不是 CSS 的 `text-align: center`。
- `text-overflow: ellipsis` 可用，但需要 `overflow: hidden` + `white-space: nowrap` 配合;多行省略号不直接支持。
- `letter-spacing` 支持；`word-spacing` 不可靠,尽量不用。

### 图像与背景

- 只支持单个 `background-image`；**不支持**多重背景。
- 没有 `background-size: cover`；使用 `-unity-background-scale-mode: scale-to-fit | scale-and-crop | stretch-to-fill`，或 `background-size: contain`。
- **不支持** `background-repeat`，需要平铺请直接准备足够大的贴图。
- SVG 不能直接作为 `background-image`；必须转 Unity `VectorImage` asset，或导出为 PNG。
- 九宫格切片使用 `-unity-slice-left/right/top/bottom` + `-unity-slice-scale` + `-unity-slice-type`。

### 交互与控件

- `cursor` 只在 Editor UI 生效，**Runtime 下被忽略**，不要把光标变化当成 UI 反馈信号。
- HTML 原生 `<select>` / `<input>` / `<textarea>` 无对应；Unity 需用 `DropdownField` / `TextField` / `IntegerField` / `FloatField` / `Toggle` / `RadioButton` / `Slider` 等控件。
- 普通 VisualElement 不能靠 `overflow: scroll` 得到滚动条；滚动必须套 `ScrollView`。
- hover 仅在指针设备下触发,触屏环境拿不到稳定的 hover 状态。

### 动画与过渡

- 支持 `transition-property` / `transition-duration` / `transition-timing-function` / `transition-delay`。
- 可过渡的属性子集有限:颜色、尺寸、`margin`/`padding`、`opacity`、`translate` / `rotate` / `scale`、`border-*`、`background-color`。
- `display` / `position` / `flex-direction` 等枚举类属性是离散值,不会插值中间态。
- **不支持** `@keyframes` / `animation` / `animation-*`;需要时间线动画请写 C#。

### 不透明与混合

- 父 `opacity` 会叠乘到所有子元素,与 CSS 行为一致,但嵌套多层容易出现意外的整体变淡。
- **不支持** `mix-blend-mode` / `background-blend-mode`;Pencil 里带混合模式的效果全部丢弃。

## 导入后必须编写视觉测试

1. 优先复用 `NZ.VisualTest.VisualImageTestBase`。
2. 测试放在合适的 PlayMode 测试目录，例如：
   - `Assets/Tests/PlayMode/NetworkIntegration/*ImageValidationTests.cs`
3. 测试职责：
   - 加载对应场景或创建测试 UIDocument；
   - 打开/切换到目标界面状态；
   - 覆盖本次导入涉及的所有面板；如果导入影响统一设置窗口、弹窗、子面板或组件库，必须分别截图；
   - 覆盖关键交互状态，例如初始值、修改值后、应用按钮可见/不可见、弹窗显示/隐藏、tab active、slider 进度等；
   - 在截图前先 Assert 待测面板已 attach 到 panel、`worldBound.width/height > 0`；
   - 调用 `CaptureScreenStep(stepName, baselinePath, notes)`（**不要再用 `CaptureStep(target,...)` / 自定义 `CaptureScaled`**），详见下文「截图区域：一律截整屏」。
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

### 截图区域：一律截整屏

**项目硬性规则**：所有 PlayMode 视觉测试都必须用 `VisualImageTestBase.CaptureScreenStep(stepName, baselinePath, notes)`，截整张 game view（`Screen.width × Screen.height`）。**禁止**调 `CaptureStep(stepName, target, ...)` 局部截图，**禁止**自定义按面板缩放的 `CaptureScaled` 之类只截目标元素 worldBound 的私货——那种局部图会把下拉框、长卡片、弹窗剪出画面，审图人看不到完整上下文（标题栏、应用按钮、tab、侧栏、同级控件、空白布局区），diff 时无法判断真实视觉。

规则：

1. **"待测元素" ≠ "截图区域"**。测试断言的是控件状态（如 `_copyBtn.text = "已复制"` 或 `dropdown.value == "显示器 2"`），但截图永远是整屏，由 game view 真实分辨率决定；不要再去算 worldBound、不要按面板做 scale。
2. 截图前的存活/就绪断言仍然落在"目标面板/overlay/控件"上：
   - `overlay.worldBound.width > 0 && overlay.worldBound.height > 0` 作为"已渲染"前置；
   - 但调用 `CaptureScreenStep` 时不再传它进去。
3. 把面板放在 game view 内"自然位置"摆好，让整屏拍下来就能交差：
   - 创建临时 UIDocument 时，根容器用 `Position.Absolute + left=0 + top=0 + width/height` 钉在 game view 左上；
   - 加载真实 Scene 的视觉测试（如 `MainV2`），保持场景里 UIDocument 自身的位置不变。
4. baseline 也按"整屏"重制：Pencil 端按 `Screen.width × Screen.height` 画一张同分辨率的参考图，对应的 `TestArtifacts/PencilReferences/*.png` 必须是整屏视图，不要再拿"只截目标"的小图当 baseline。旧的局部 baseline 一律视为过期，需要逐个刷新。
5. `CaptureScreenStep` 已自带 `WaitForEndOfFrame + 写 manifest`，不要再用 reflection 自己塞 step。
6. 唯一例外：纯独立组件单测（UIDocument 只挂一个 component uxml，没有面板外壳）——此时整屏=组件本身，不属于例外，仍走 `CaptureScreenStep`。

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

## Weixin 发送验证图

视觉测试产出截图后，必须使用 `weixin` 技能把所有 actual 截图和对应 baseline/目标截图发送给用户，便于人工复核。

### 发送前检查

- 先读取 `manifest.json`，不要手写路径。
- 对每个 `steps[]` 解析：
  - actual：`Path.Combine(manifest.outputDirectory, step.actualImagePath)`
  - baseline：优先使用 `step.baselineImagePath`；如果用户另给外部目标图，则使用外部目标图。
- 确认每个文件存在；不存在时先在回复中说明缺失，不要假装已发送。
- 先用 `bash ~/.claude/skills/weixin/scripts/weixin_send.sh status` 检查 openclaw gateway 与 openclaw-weixin 插件是否就绪；未就绪时按 `weixin` 技能说明提示用户（例如 `openclaw daemon start`、`openclaw doctor`），不要绕过或伪造发送。

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

使用 `weixin` 技能脚本发送文本与图片：

```bash
bash ~/.claude/skills/weixin/scripts/weixin_send.sh text "视觉测试 <TestName> / <RunId>：已生成 <N> 个 step。请忽略 Unity 不支持的阴影、毛玻璃和背景模糊差异。"
bash ~/.claude/skills/weixin/scripts/weixin_send.sh image "<actual_png_path>" "[<stepName>] actual - Unity 导入截图"
bash ~/.claude/skills/weixin/scripts/weixin_send.sh image "<baseline_png_path>" "[<stepName>] baseline - Pencil 目标图"
```

如需切换收件人或 bot 账号，按 `weixin` 技能说明通过环境变量覆盖，例如：

```bash
export WEIXIN_TARGET=<openId>@im.wechat
export WEIXIN_ACCOUNT=<bot-account-id>
```

发送完成后，在最终回复里列出已发送的 step 名称，以及任何未发送成功或缺失的文件。

## 常见问题与处理

### 阴影 / 模糊 / 渐变

- Unity 图比 Pencil 少一圈透明/阴影：通常是截图目标不同。优先统一截图范围，**不要为阴影修改 UI**。
- Pencil 的 drop-shadow / inner-shadow / text-shadow：整体丢弃，不要在 USS 里用多 VisualElement 拼假阴影。
- Pencil backdrop-filter / 毛玻璃 / Gaussian blur：丢弃模糊，背景降级为半透明纯色（例如 `rgba(255,253,251,0.85)`）模拟亚克力感。
- Pencil 线性/径向渐变填充：两种处理——烘焙成带透明的 PNG 贴图，或直接换纯色；**不要**用多个叠色 VisualElement 手工拼渐变。

### 形状 / 边角

- Pencil 胶囊按钮，Unity 变成怪异椭圆：把 `999px` / `9999px` / `var(--radius-full)` 改成宽或高一半的具体像素值（例如高 28 就写 `14px`）。
- Pencil 椭圆角（长短半径不同，`a / b` 语法）：Unity 不支持斜杠写法，只能降级为单一半径。
- Pencil 使用 `clip-path` 裁剪异形：改用矩形 / 圆角矩形 + `overflow: hidden`；异形裁切做不了。

### 布局

- Pencil 用 CSS Grid 画九宫格：拆成嵌套 flex（外层 `flex-direction: column`、内层 `flex-direction: row`），不要尝试 `display: grid`。
- Pencil 绝对定位浮窗用了 `position: fixed`：改成 `absolute`，并挂到 overlay 根节点下。
- Pencil 叠层靠 `z-index`：Unity 不支持，调整 UXML 文档顺序——后写的盖前写的；浮层应写在父容器末尾。
- Pencil 根容器默认 `flex-direction: row`：Unity 默认是 `column`，需要在根元素显式写 `flex-direction: row`，否则整体方向会翻转。
- Pencil 使用 `gap` 做等距分隔：Unity 2022+ 支持 `gap`，目标版本不确定时用 `margin-left/right` 或 `margin-top/bottom` 交替补位。
- Pencil 依赖 `absolute` 子元素的 `left/top`：注意 Unity 中 `absolute` 基于父 border-box 起点，不减父 padding，不要扁平化带 padding 的中间层。

### 尺寸 / 单位

- Pencil 字号写 `1rem` / `16em` / `1.2vw`：全部换算成 `px`。
- Pencil `font-weight: 500 / 600`：合并为 `-unity-font-style: bold` 或 `normal`；Unity 没有数值字重。
- Pencil 在 flex 中用 `min-content` / `max-content`：改成 `flex-grow` / `flex-basis: auto`。
- Pencil USS 里混用 `calc()`：提前算好 px，USS 里只放最终值。

### 文本

- Pencil 文本下划线 / 删除线（`text-decoration`）：Unity 无对应样式；必要时在下方用一根 1px 的 VisualElement 模拟。
- Pencil 文字阴影（`text-shadow`）：删掉；需要描边改用 `-unity-text-outline-color` + `-unity-text-outline-width`。
- Pencil 多行省略号：Unity 只能单行 `text-overflow: ellipsis`（配合 `overflow: hidden` + `white-space: nowrap`），多行省略不直接支持。
- Pencil `line-height: 1.5`：Unity 没有 `line-height`，改用 `-unity-paragraph-spacing` 或拆成多 Label。
- Pencil `text-align: center`：Unity 写 `-unity-text-align: middle-center`（九宫格纵横双向值），不是 `text-align`。

### 图像 / 图标

- 图标变形：检查导出是否等比，USS 用 `-unity-background-scale-mode: scale-to-fit` 或 `background-size: contain`。
- SVG 图标直接放 USS：Unity `background-image` 不认 SVG；导出为 256×256 PNG，或转 `VectorImage` asset。
- 多背景合成（`background-image: a.png, b.png`）：Unity 只吃单图；要么合成一张，要么叠多个 VisualElement。
- 背景平铺（`background-repeat`）：Unity 不支持；贴图自己做得够大。

### 控件替换

- Pencil 画了 HTML `<select>`：换 Unity `DropdownField`；样式对接 `.unity-base-dropdown__*`。
- Pencil 画了 `<input type="checkbox">` / `radio`：分别换 `Toggle` / `RadioButton`。
- Pencil 画了 `<input type="text">` / `number`：换 `TextField` / `IntegerField` / `FloatField`。
- Slider 左侧填充缺失：Unity 原生 Slider 没有按 Pencil 状态显示，需要定制 `.unity-base-slider__tracker` / `.unity-base-slider__dragger`，或改成组合控件。
- Pencil 画了滚动条：Unity 普通 VisualElement 不能滚动，必须套 `ScrollView`。

### 交互 / 状态

- Pencil 给了 hover 状态样式：在 USS 中写 `.foo:hover { ... }` 复刻。
- Pencil 标记了 `:focus` / `:disabled`：USS 用同名伪类 `:focus` / `:disabled`。
- Pencil 画了 `::before` / `::after` 图标装饰：在 UXML 里加真实 VisualElement，不要期望伪元素。
- Pencil 设计稿标了 `cursor`：Runtime 下忽略，不要把光标变化当成 UI 反馈信号。
- Pencil 用了 `:nth-child` 等高级伪类：Unity 不支持；给元素加具体 class 或 name 去选择。

### 动画 / 过渡

- Pencil 用 `@keyframes` 描述关键帧动画：Unity 不支持；只能用 `transition` 做单次过渡，复杂时间线写 C#。
- 过渡属性是枚举（`display` / `position` / `flex-direction`）：它们是离散值，不会有中间态，切换会直接跳变。

### 测试与验证

- 视觉测试通过但画面明显不对：测试只证明截图产出成功，必须人工查看 actual/baseline 后下结论。
- baseline 与 actual 对不上时：先补 baseline 或在 manifest 中标"待补目标图"，再决定是否发 weixin。

## 完成前检查清单

- [ ] 已读取 Pencil 目标节点并确认尺寸/层级/状态。
- [ ] 已盘点面板里使用的所有组件，并对每个组件名核对 `Assets/UI_V2/Documents/Components/<Name>.uxml` + `Assets/UI_V2/Styles/Components/<Name>.uss` 是否存在；不存在的已先导出再处理面板。
- [ ] 缺失组件的 USS 状态规则是从 Pencil 主组件旁边的状态实例（hover/pressed/disabled/checked、primary/secondary、slider 0%/50%/100% 等）映射出来的，没有杜撰 Pencil 没画的状态。
- [ ] 面板 UXML 没有把组件结构内联展开；用的是 `<ui:Instance template="..."/>` 或项目现行的实例引用方式。
- [ ] 面板 USS 没有重复组件内部样式，只覆盖了"组件在该面板里的位置/尺寸/间距"。
- [ ] 已用「Unity UI Toolkit 与 Web CSS 差异速查」过一遍设计稿，识别不可实现效果并在导入阶段降级。
- [ ] 已更新对应 UXML/USS，并保护 C# 查询名。
- [ ] 所有圆角都是有限值。
- [ ] 阴影、毛玻璃、模糊、渐变、`clip-path`、`grid`、`z-index`、`position: fixed` 等 Web 特性没有作为 Unity 必实现项导入。
- [ ] `display` 只用了 `flex` / `none`；不存在 `grid` / `block` / `inline-block`。
- [ ] 字体使用工程内 `-unity-font-definition`，字号单位为 `px`，没有 `em` / `rem` / `vw`。
- [ ] 如有图片/图标，已等比导出，默认 256×256，SVG 已转 PNG 或 VectorImage。
- [ ] 已刷新 Unity 资源并检查相关控制台错误。
- [ ] 已新增或更新 PlayMode 视觉测试，仅产出截图和 manifest。
- [ ] 视觉测试已覆盖本次导入影响到的所有面板和关键状态，包括修改值后/按钮状态等动态表现。
- [ ] 所有 step 都通过 `CaptureScreenStep(stepName, baselinePath, notes)` 截整屏；没有 `CaptureStep(target,...)` / `CaptureScaled` 这类只截局部的调用残留。
- [ ] baseline 已是整屏 Pencil 参考图（与 `Screen.width × Screen.height` 同分辨率）；旧的局部 baseline 已刷新或在 manifest 备注"待补整屏目标图"。
- [ ] 已运行测试并用 `unity-visual-image-validation` 人工判断差异。
- [ ] 已使用 `weixin` 技能把所有 actual 截图和对应 baseline/目标截图发送给用户；如 openclaw gateway 或 openclaw-weixin 未就绪，已明确说明缺失配置。
