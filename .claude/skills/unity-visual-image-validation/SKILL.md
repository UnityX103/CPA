---
name: unity-visual-image-validation
description: Use when reviewing VisualImageTestBase screenshot runs in this CPA Unity project, especially after PlayMode UI image tests produce manifest.json and PNG artifacts that must be checked against project baselines.
---

# Unity Visual Image Validation

## 何时使用

- 运行或复查 `NZ.VisualTest.VisualImageTestBase` 派生的 PlayMode 视觉测试时
- 需要读取 `manifest.json`、最新 run 目录和步骤截图，再和项目 baseline 做人工可审阅的视觉比对时
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
7. 不要把 NUnit `Passed` 直接写成“视觉正确”；这类测试默认只保证工件产出成功。

## UnifiedSettingsPanel 速查

- 测试全名：`APP.NetworkIntegration.Tests.UnifiedSettingsPanelImageValidationTests.UnifiedSettingsPanel_ShouldCaptureExpectedStates`
- 常见步骤：`pomodoro`、`online`、`pet`
- 常见 baseline：
  - `TestArtifacts/PencilReferences/unified-settings-pomodoro.png`
  - `TestArtifacts/PencilReferences/unified-settings-online-not-joined.png`
  - `TestArtifacts/PencilReferences/unified-settings-pet.png`
- 如果实际图明显比 baseline 大很多，且抓到了整块 `settings-overlay` 而不是紧凑面板，优先归因为“截图目标层级或布局范围不匹配”
