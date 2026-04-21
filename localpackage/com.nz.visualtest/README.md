# NZ Visual Test

Unity 视觉测试基类 VisualTestBase：自动建摄像机、GUI 操作提示覆盖层、FFmpeg 屏幕录制、键鼠输入模拟。适合在 PlayMode 测试中人眼回放 + 录像留证。

内嵌 FFmpegOut 作为录制后端（Keijiro Takahashi 原作 port）。

## 安装

在目标项目的 Packages/manifest.json 中加一条：

    "com.nz.visualtest": "file:<相对路径>/com.nz.visualtest"

## 使用

让 PlayMode 测试类继承 NZ.VisualTest.VisualTestBase；SetUp/TearDown 会自动创建临时摄像机、启动 FFmpeg 录制。子类通过 LogInputAction / SimulateKey / SimulateMouseButton 与 GUI 交互。录制产物输出到 Application.persistentDataPath/TestOutput/<类名>/Video/。可覆盖 TestName / UseDedicatedTestCamera / RecordOnSetUp。

## VisualImageTestBase

`VisualImageTestBase` 用于“只产出图片工件，不做视觉断言”的 PlayMode 测试基类。运行时会在 `Application.temporaryCachePath/TestOutput/<TestMethod>/<RunId>/` 下建立单次运行目录。

每次调用 `CaptureStep(...)` 或 `CaptureScreenStep(...)`，都会在当前运行目录写出一张 `NN-stepName-actual.png`。同目录下的 `manifest.json` 会随着步骤追加持续刷新，用来记录步骤名、实际图片路径、可选的 `baselineImagePath` 和备注信息。

这类测试的视觉判定不应直接在 NUnit 中失败；应由本地技能 `unity-visual-image-validation` 在测试运行后读取 `manifest.json`，再对输出图与 baseline 或外部目标图做 AI 视觉验证。

## 依赖

- com.unity.inputsystem ≥ 1.4.4
- com.unity.test-framework ≥ 1.1.33
- FFmpeg 可执行文件需位于 PATH（未找到会自动降级为不录，不阻塞测试）
