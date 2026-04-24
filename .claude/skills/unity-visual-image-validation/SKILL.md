---
name: unity-visual-image-validation
description: Use when reviewing VisualImageTestBase screenshot runs in this CPA Unity project, especially after PlayMode UI image tests produce manifest.json and PNG artifacts that must be visually checked against project baselines.
---

# Unity Visual Image Validation

## 何时使用

- 运行或复查 `NZ.VisualTest.VisualImageTestBase` 派生的 PlayMode 视觉测试时
- 需要读取 `manifest.json`、最新 run 目录和步骤截图，再和项目 baseline 做人工可审阅的视觉比对时
- 用户要求“截图出来后由你判断哪里不一样、需要怎么改”时
- 尤其适用于 `APP.NetworkIntegration.Tests.UnifiedSettingsPanelImageValidationTests.UnifiedSettingsPanel_ShouldCaptureExpectedStates`

## 项目约定

- 先配合 `unity-mcp-orchestrator`：读取 `mcpforunity://instances`，绑定唯一实例，再确认 `mcpforunity://editor/state` 中 `ready_for_tools == true`
- 本项目 UnityMCP 常用 `stdio` 模式；只要 `mcpforunity://instances` 返回可用实例即可继续，不必切回 HTTP
- 业务 baseline 在 `TestArtifacts/PencilReferences`
- 截图工件在 `Application.temporaryCachePath/TestOutput/<TestMethod>/<RunId>/`
- `manifest.outputDirectory` 是本次 run 的绝对根目录；`steps[].actualImagePath` 是相对它的相对路径

## 工作流

1. 用 `run_tests` 只跑目标 PlayMode 用例。
2. 用 `execute_code` 读取 `UnityEngine.Application.temporaryCachePath`，不要手猜系统临时目录。
3. 找到最新 `RunId` 目录，读取其中的 `manifest.json`。
4. 对每个 step，用 `manifest.outputDirectory + actualImagePath` 解析实际截图。
5. 目标图优先级固定为：外部目标图 > `baselineImagePath` > 报告缺失并停止该步骤判断。
6. 逐步查看实际图与目标图，重点判断布局范围、层级、控件状态、文字/图标资源、裁切与遮挡。
7. 不要要求像素一致，也不要把像素级差异作为唯一验收标准；Unity UI Toolkit 与 Pencil 的字体渲染、阴影外扩、抗锯齿、Slider 内部结构和圆角处理都可能产生非业务差异。
8. 不要把 NUnit `Passed` 直接写成“视觉正确”；这类测试默认只保证工件产出成功。
9. 由助手打开实际图与 baseline，逐项描述可见差异，并给出需要修改的 Unity UXML/USS/C# 或 Pencil baseline 建议。

## 人工视觉判断输出要求

- 先说明截图产物路径与对应 baseline 路径。
- 按 step 分组判断，不使用“像素一致/不一致”作为结论。
- 每个 step 至少检查：尺寸范围、位置与留白、颜色、圆角、阴影、文本、控件状态、裁切/遮挡。
- 如果差异来自测试截图目标范围错误，优先建议修正测试捕获目标，而不是修改 UI。
- 如果差异来自 Unity 导入效果错误，明确指出应修改的文件和样式/结构，例如 `Assets/UI_V2/Styles/*.uss` 或 `Assets/UI_V2/Documents/*.uxml`。
- 如果差异来自 Pencil baseline 包含阴影外扩或透明区域，而 Unity 截图只截内容盒，明确说明两者截图范围不一致，并建议统一截图范围。

## UnifiedSettingsPanel 速查

- 测试全名：`APP.NetworkIntegration.Tests.UnifiedSettingsPanelImageValidationTests.UnifiedSettingsPanel_ShouldCaptureExpectedStates`
- 常见步骤：`pomodoro`、`online`、`pet`
- 常见 baseline：
  - `TestArtifacts/PencilReferences/unified-settings-pomodoro.png`
  - `TestArtifacts/PencilReferences/unified-settings-online-not-joined.png`
  - `TestArtifacts/PencilReferences/unified-settings-pet.png`
- 如果实际图明显比 baseline 大很多，且抓到了整块 `settings-overlay` 而不是紧凑面板，优先归因为“截图目标层级或布局范围不匹配”
