# Unity 视觉图片测试与技能设计

## 概述

本文定义一套新的 Unity 视觉图片测试方案，目标是把“产图”和“判定”明确分层：

- Unity 测试基类只负责在正确的时机截取图片、按稳定结构保存工件、输出本次测试运行的清单信息。
- 图片是否符合目标效果，不在 NUnit 测试中做像素级或阈值级断言，而由技能在测试完成后基于目标图执行 AI 视觉验证并给出结论。
- 同一次测试运行产出的多张图片必须归档到同一个运行目录，便于人工审阅、技能消费和后续追踪。

这套方案优先服务两类工作：

1. 编写新的 Unity 视觉测试。
2. 修改已有视觉测试并根据结果图与目标效果做视觉验证。

## 设计目标

### 主要目标

- 提供一个可复用的视觉图片测试基类，让子类在重写测试时只需要手动调用统一截图方法。
- 让每次测试运行都生成独立文件夹，容纳该次运行的全部图片结果和元数据。
- 让技能成为图片效果的最终裁判，支持使用仓库基线图或用户临时指定的外部目标图。
- 让技能既能创建新的视觉测试，也能改造已有视觉测试。

### 非目标

- 第一版不在 Unity 测试中做失败断言或自动像素比对。
- 第一版不要求通用化的声明式步骤编排框架。
- 第一版不引入复杂的多目标图聚合判定模型。

## 方案选择

本设计采用“强技能型”方案。

### 已确认约束

- 每次测试运行对应一个独立文件夹。
- 目标效果来源为“仓库内固定基线图 + 可选外部目标图”，优先使用外部目标图。
- 图片是否符合预期由技能负责判定，不在测试代码里直接失败。
- 技术路线采用 AI 视觉语义比对，而非严格像素比对。
- 子类测试通过手动调用方法触发截图，不采用声明式步骤列表。

### 为什么选择强技能型

- 更贴合“基于视觉”的验收目标，能评价整体观感是否符合目标效果。
- 避免把设计审美层面的判断硬编码进 NUnit 阈值。
- 让 C# 基类保持轻量，专注做稳定的图片产出器。

## Unity 基类设计

### 命名与定位

新增或重构一个专用于图片工件输出的基类，建议命名为 `VisualImageTestBase`。

该基类的职责只有两类：

- 管理测试运行上下文与目录。
- 在子类请求时捕获截图并记录步骤信息。

它不负责图片通过/失败判定。

### 运行目录结构

每个测试方法每次运行创建一个独立目录：

```text
TestOutput/
  <TestMethodName>/
    <RunId>/
      manifest.json
      01-pomodoro-actual.png
      02-online-actual.png
      03-pet-actual.png
```

其中：

- `<TestMethodName>` 取当前 NUnit 测试方法名。
- `<RunId>` 由时间戳和短随机后缀组成，保证可读性与唯一性。
- 同一测试运行中的所有步骤图片都落在该目录内。

### 基类公开 API

第一版保留最少 API：

- `CaptureStep(string stepName, VisualElement target, string baselinePath = null, string notes = null)`
  - 捕获指定 `VisualElement` 区域截图。
  - 生成步骤编号与输出图片名。
  - 记录这一步的目标图路径和说明。
- `CaptureScreenStep(string stepName, string baselinePath = null, string notes = null)`
  - 捕获整屏截图，供非 UI Toolkit 或整屏场景使用。
- `CurrentRunDirectory`
  - 返回本次测试运行目录的绝对路径。
- `CurrentManifest`
  - 返回当前运行上下文的只读对象，便于子类读取当前状态。

### 基类内部行为

- 在 `SetUp` 阶段初始化运行目录与 manifest 内存对象。
- 每次 `CaptureStep` 或 `CaptureScreenStep` 调用时：
  - 为步骤自动分配递增序号。
  - 生成 `NN-stepName-actual.png` 格式文件名。
  - 保存图片到当前运行目录。
  - 记录相对图片路径、基线图路径、备注和步骤名称。
  - 刷新写出 `manifest.json`。
- 在 `TearDown` 阶段做最终 manifest 落盘与资源清理。

### 子类使用方式

子类测试专注于“什么时候截图”，不自己拼目录或文件名。例如：

```csharp
[UnityTest]
public IEnumerator UnifiedSettingsPanel_ShouldRenderExpectedStates()
{
    yield return LoadMainScene();
    yield return WaitForUiReady();

    yield return CaptureStep(
        "pomodoro",
        overlayElement,
        "TestArtifacts/PencilReferences/unified-settings-pomodoro.png");

    yield return SwitchToTab("online");
    yield return WaitForUiReady();

    yield return CaptureStep(
        "online",
        overlayElement,
        "TestArtifacts/PencilReferences/unified-settings-online-not-joined.png");
}
```

## 测试产物契约

### manifest 结构

每次运行目录中必须包含一个 `manifest.json`，用于把测试结果交接给技能。

建议结构如下：

```json
{
  "testName": "UnifiedSettingsPanel_ShouldRenderExpectedStates",
  "testClass": "UnifiedSettingsPanelVisualTests",
  "runId": "20260421_153011_3f8c1b",
  "createdAt": "2026-04-21T15:30:11+09:00",
  "outputDirectory": "/absolute/path/to/TestOutput/...",
  "steps": [
    {
      "index": 1,
      "name": "pomodoro",
      "actualImagePath": "01-pomodoro-actual.png",
      "baselineImagePath": "TestArtifacts/PencilReferences/unified-settings-pomodoro.png",
      "notes": "settings-overlay"
    }
  ]
}
```

### 契约原则

- `actualImagePath` 必填，且相对当前运行目录。
- `baselineImagePath` 可空，为空表示需要技能从外部参数提供目标图。
- manifest 不包含 `passed`、`score`、`mismatchRatio` 等裁判字段。
- 每个步骤对应一张结果图；第一版不支持一个步骤绑定多张目标图。

## 技能设计

### 技能定位

新增一个专门的技能，用于 Unity 视觉测试图片验证工作流。该技能的核心能力是：

- 编写新的视觉测试。
- 修改已有视觉测试。
- 运行测试并定位本次输出目录。
- 将结果图片与目标效果进行 AI 视觉验证。
- 输出逐步骤结论与修改建议。

### 技能输入

技能至少支持以下输入场景：

1. 用户要求“创建一个新的视觉测试”。
2. 用户要求“修改现有视觉测试，让它覆盖某些状态并截图”。
3. 用户要求“运行某个视觉测试，并将结果与目标图做验证”。

技能在验证图片时解析目标图的优先级为：

1. 用户明确传入的外部目标图路径。
2. manifest 中步骤自带的 `baselineImagePath`。
3. 技能默认参数中的基线路径。

### 技能执行流程

#### 场景一：编写或修改测试

- 先定位已有测试基类和目标测试文件。
- 如目标测试不存在，则新建一个继承 `VisualImageTestBase` 的 PlayMode 测试。
- 如目标测试已存在，则优先保留原有交互流程，只补充截图调用和基线图登记。
- 运行对应测试，产出结果图和 manifest。

#### 场景二：图片验证

- 找到本次测试运行目录。
- 读取 `manifest.json`。
- 对每个步骤收集：
  - 结果图路径。
  - 目标图路径。
  - 步骤名称与备注。
- 使用 AI 视觉能力对“结果图 vs 目标图”做语义级验证。
- 输出逐步骤判断和整体结论。

### 技能输出

技能最终输出应包含：

- 本次验证所使用的测试名称和运行目录。
- 每个步骤的结果图路径与目标图路径。
- 每个步骤的视觉判断结论：
  - 是否符合目标效果。
  - 不符合时的主要原因。
  - 是否更像布局问题、资源问题、状态问题或测试脚本问题。
- 整体结论：
  - 测试是否通过本次视觉验收。
  - 若不通过，优先建议修改测试脚本还是修改 UI 代码。

## 错误处理

### Unity 基类错误处理

- 截图失败时应抛出明确异常，指出是整屏捕获失败还是区域裁切失败。
- 目标 `VisualElement` 无效、尺寸为 0 或尚未布局完成时，应给出明确错误。
- manifest 写出失败时应让测试失败，因为这会导致技能无法消费结果。

### 技能错误处理

- manifest 缺失时，技能应明确说明测试工件不完整，无法继续验证。
- 目标图缺失时，技能应指出缺失的是哪一步、按什么优先级解析失败。
- 如果 AI 验证无法给出稳定结论，技能应明确返回“结果不确定”，并列出建议人工复核的步骤。

## 测试策略

### 基类测试

应新增或更新运行时/编辑器测试，覆盖：

- 运行目录创建规则是否正确。
- 步骤序号与图片命名是否稳定。
- manifest 是否在多步骤场景下正确记录。
- 目标图路径与备注是否按预期落盘。

### 集成测试

至少保留一个真实 PlayMode 集成用例，验证：

- 加载场景后可完成 UI 稳定截图。
- 同一测试中可连续输出多张步骤图。
- 运行结束后目录与 manifest 可被技能消费。

## 实施范围

第一阶段只实现以下范围：

- 图片测试基类的目录管理、截图输出和 manifest 契约。
- 一个真实业务用例迁移到新基类。
- 一个新的技能文档，明确如何编写/修改/验证 Unity 视觉测试。

以下内容暂不进入第一阶段：

- 像素 diff 图自动生成。
- 一步多目标图。
- 自动更新基线图工作流。
- 通用声明式步骤 DSL。

## 风险与取舍

### 风险

- AI 视觉判断可能受截图质量、缩放、字体渲染差异影响。
- 若测试未正确等待 UI 稳定，技能可能把“测试时机不对”误判成“UI 实现不对”。
- 若目标图来源管理混乱，技能结论会失去可信度。

### 取舍

- 第一版优先要“能稳定产图并可被技能验证”，而不是“自动化判定尽可能全面”。
- 先把手动调用型 API 做稳，再考虑是否需要抽象更高层的步骤框架。
- 先依赖技能做最终判断，不在 Unity 里并行维护另一套视觉断言规则。

## 后续计划入口

设计确认后，下一步应输出实现计划，拆分为：

1. 基类与 manifest 数据模型调整。
2. 现有示例测试迁移与真实集成测试接入。
3. Unity 视觉验证技能编写与说明文档落地。
