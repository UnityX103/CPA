# Unity 视觉图片测试与技能 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一个只负责产图的 Unity 视觉图片测试基类，并新增一个能编写或修改这类测试、在运行后基于目标图做 AI 视觉验证的本地技能。

**Architecture:** 保留现有 `VisualTestBase` 的录屏/输入模拟职责，新增独立的 `VisualImageTestBase` 负责图片工件输出与 manifest 契约。所有视觉判定移出 NUnit 测试，改由用户本地技能消费 `manifest.json` 和结果图片，再结合仓库基线图或外部目标图给出结论。

**Tech Stack:** Unity 6、Unity Test Framework、NUnit、UI Toolkit、`JsonUtility`、本地 Codex skills

---

## 文件结构

- Create: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestManifest.cs`
  - 运行目录 manifest 与步骤项的数据模型。
- Create: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestBase.cs`
  - 图片测试基类，负责运行目录、截图输出、步骤登记。
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualTestImageUtility.cs`
  - 补充运行目录、图片命名、manifest 落盘与整屏截图辅助函数，保留现有 diff 相关逻辑。
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualTestImageUtilityTests.cs`
  - 为运行目录、文件命名、manifest 落盘补充纯工具测试。
- Create: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualImageTestBaseTests.cs`
  - 用最小 PlayMode 用例验证 `VisualImageTestBase` 的图片与 manifest 输出行为。
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs`
  - 迁移真实业务用例到“只产图不判定”的流程。
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/README.md`
  - 文档化新的图片基类与 manifest 目录约定。
- Create: `/Users/xpy/.agents/skills/unity-visual-image-validation/SKILL.md`
  - 用户本地技能，指导编写/修改 Unity 视觉测试并做 AI 视觉验证。

## Task 1: 加入 manifest 模型与工件辅助函数

**Files:**
- Create: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestManifest.cs`
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualTestImageUtility.cs`
- Test: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualTestImageUtilityTests.cs`

- [ ] **Step 1: 先把失败测试写出来**

在 `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualTestImageUtilityTests.cs` 追加下面三条测试：

```csharp
[Test]
public void BuildRunOutputDirectory_UsesMethodScopedFolder()
{
    string outputDirectory = VisualTestImageUtility.BuildRunOutputDirectory(
        "UnifiedSettingsPanel_ShouldCaptureExpectedStates",
        "20260421_153011_ab12cd");

    StringAssert.Contains("TestOutput", outputDirectory);
    StringAssert.Contains("UnifiedSettingsPanel_ShouldCaptureExpectedStates", outputDirectory);
    StringAssert.Contains("20260421_153011_ab12cd", outputDirectory);
}

[Test]
public void BuildStepArtifactFileName_PrefixesSequenceAndSuffix()
{
    string fileName = VisualTestImageUtility.BuildStepArtifactFileName(2, "online state", "actual");
    Assert.That(fileName, Is.EqualTo("02-online state-actual.png"));
}

[Test]
public void SaveManifest_WritesExpectedJsonFields()
{
    var manifest = new VisualImageTestRunManifest
    {
        testName = "UnifiedSettingsPanel_ShouldCaptureExpectedStates",
        testClass = "UnifiedSettingsPanelImageValidationTests",
        runId = "20260421_153011_ab12cd",
        createdAt = "2026-04-21T15:30:11+09:00",
        outputDirectory = _outputDirectory,
        steps = new List<VisualImageTestStepManifest>
        {
            new VisualImageTestStepManifest
            {
                index = 1,
                name = "pomodoro",
                actualImagePath = "01-pomodoro-actual.png",
                baselineImagePath = "TestArtifacts/PencilReferences/unified-settings-pomodoro.png",
                notes = "settings-overlay"
            }
        }
    };

    VisualTestImageUtility.SaveManifest(manifest, _outputDirectory);

    string manifestPath = Path.Combine(_outputDirectory, "manifest.json");
    Assert.That(File.Exists(manifestPath), Is.True);

    string json = File.ReadAllText(manifestPath);
    StringAssert.Contains("\"testName\":\"UnifiedSettingsPanel_ShouldCaptureExpectedStates\"", json);
    StringAssert.Contains("\"baselineImagePath\":\"TestArtifacts/PencilReferences/unified-settings-pomodoro.png\"", json);
}
```

- [ ] **Step 2: 跑测试确认它先失败**

通过 MCP 运行以下测试：

```text
run_tests
testNames:
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.BuildRunOutputDirectory_UsesMethodScopedFolder
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.BuildStepArtifactFileName_PrefixesSequenceAndSuffix
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.SaveManifest_WritesExpectedJsonFields
```

Expected:
- 编译失败，提示 `VisualTestImageUtility` 中还没有 `BuildRunOutputDirectory`、`BuildStepArtifactFileName`、`SaveManifest`
- 或提示 `VisualImageTestRunManifest` / `VisualImageTestStepManifest` 未定义

- [ ] **Step 3: 写最小实现让测试变绿**

创建 `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestManifest.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace NZ.VisualTest
{
    [Serializable]
    public sealed class VisualImageTestRunManifest
    {
        public string testName;
        public string testClass;
        public string runId;
        public string createdAt;
        public string outputDirectory;
        public List<VisualImageTestStepManifest> steps = new List<VisualImageTestStepManifest>();
    }

    [Serializable]
    public sealed class VisualImageTestStepManifest
    {
        public int index;
        public string name;
        public string actualImagePath;
        public string baselineImagePath;
        public string notes;
    }
}
```

修改 `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualTestImageUtility.cs`，补入这些方法：

```csharp
public static string BuildRunOutputDirectory(string testName, string runId)
{
    if (string.IsNullOrWhiteSpace(testName))
    {
        throw new ArgumentException("testName 不能为空。", nameof(testName));
    }

    if (string.IsNullOrWhiteSpace(runId))
    {
        throw new ArgumentException("runId 不能为空。", nameof(runId));
    }

    string outputDirectory = Path.Combine(
        Application.temporaryCachePath,
        "TestOutput",
        SanitizeFileName(testName),
        SanitizeFileName(runId));

    Directory.CreateDirectory(outputDirectory);
    return outputDirectory;
}

public static string BuildStepArtifactFileName(int stepIndex, string stepName, string suffix = "actual")
{
    if (stepIndex <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(stepIndex), "stepIndex 必须大于 0。");
    }

    return $"{stepIndex:00}-{SanitizeFileName(stepName)}-{SanitizeFileName(suffix)}.png";
}

public static void SaveManifest(VisualImageTestRunManifest manifest, string outputDirectory)
{
    if (manifest == null)
    {
        throw new ArgumentNullException(nameof(manifest));
    }

    if (string.IsNullOrWhiteSpace(outputDirectory))
    {
        throw new ArgumentException("outputDirectory 不能为空。", nameof(outputDirectory));
    }

    Directory.CreateDirectory(outputDirectory);
    string manifestPath = Path.Combine(outputDirectory, "manifest.json");
    string json = JsonUtility.ToJson(manifest, true);
    File.WriteAllText(manifestPath, json);
}
```

保留现有 `BuildImageOutputDirectory` 行为不动，避免影响仍在使用旧断言流程的测试；新的图片运行目录只由 `BuildRunOutputDirectory` 与 `VisualImageTestBase` 使用。

- [ ] **Step 4: 再跑一遍测试确认通过**

再次通过 MCP 运行：

```text
run_tests
testNames:
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.BuildRunOutputDirectory_UsesMethodScopedFolder
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.BuildStepArtifactFileName_PrefixesSequenceAndSuffix
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.SaveManifest_WritesExpectedJsonFields
```

Expected:
- 3/3 PASS
- 没有新的编译错误

- [ ] **Step 5: 提交这一小步**

```bash
git add /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestManifest.cs /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualTestImageUtility.cs /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualTestImageUtilityTests.cs
git commit -m "feat: add visual image artifact manifest helpers"
```

### Task 2: 新建 `VisualImageTestBase`

**Files:**
- Create: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestBase.cs`
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualTestImageUtility.cs`
- Test: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualImageTestBaseTests.cs`

- [ ] **Step 1: 先写一个最小 PlayMode 失败测试**

创建 `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualImageTestBaseTests.cs`：

```csharp
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZ.VisualTest.Tests
{
    [TestFixture]
    public sealed class VisualImageTestBaseTests : VisualImageTestBase
    {
        private GameObject _cameraObject;

        [UnitySetUp]
        public IEnumerator SetUpScene()
        {
            _cameraObject = new GameObject("VisualImageTestBaseTests_Camera");
            _cameraObject.AddComponent<Camera>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            UnityEngine.Object.Destroy(_cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureScreenStep_WritesArtifactAndManifest()
        {
            yield return CaptureScreenStep("full-screen", "Baselines/full-screen.png", "smoke");

            string imagePath = Path.Combine(CurrentRunDirectory, "01-full-screen-actual.png");
            Assert.That(File.Exists(imagePath), Is.True);

            string manifestPath = Path.Combine(CurrentRunDirectory, "manifest.json");
            var manifest = JsonUtility.FromJson<VisualImageTestRunManifest>(File.ReadAllText(manifestPath));

            Assert.That(manifest.steps.Count, Is.EqualTo(1));
            Assert.That(manifest.steps[0].name, Is.EqualTo("full-screen"));
            Assert.That(manifest.steps[0].baselineImagePath, Is.EqualTo("Baselines/full-screen.png"));
            Assert.That(manifest.steps[0].notes, Is.EqualTo("smoke"));
        }
    }
}
```

- [ ] **Step 2: 跑测试确认缺基类时它会失败**

通过 MCP 运行：

```text
run_tests
testNames:
- NZ.VisualTest.Tests.VisualImageTestBaseTests.CaptureScreenStep_WritesArtifactAndManifest
```

Expected:
- 编译失败，提示 `VisualImageTestBase`、`CaptureScreenStep`、`CurrentRunDirectory` 尚不存在

- [ ] **Step 3: 写最小实现让这个基类可用**

在 `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestBase.cs` 写入：

```csharp
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NZ.VisualTest
{
    public abstract class VisualImageTestBase
    {
        private VisualImageTestRunManifest _currentManifest;
        private int _stepIndex;

        protected string CurrentRunDirectory { get; private set; }

        protected VisualImageTestRunManifest CurrentManifest => _currentManifest;

        [UnitySetUp]
        public IEnumerator SetUpVisualImageRun()
        {
            string testName = TestContext.CurrentContext.Test.MethodName ?? TestContext.CurrentContext.Test.Name;
            string runId = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            CurrentRunDirectory = VisualTestImageUtility.BuildRunOutputDirectory(testName, runId);
            _currentManifest = new VisualImageTestRunManifest
            {
                testName = testName,
                testClass = GetType().Name,
                runId = runId,
                createdAt = DateTimeOffset.Now.ToString("O"),
                outputDirectory = CurrentRunDirectory
            };

            _stepIndex = 0;
            VisualTestImageUtility.SaveManifest(_currentManifest, CurrentRunDirectory);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownVisualImageRun()
        {
            VisualTestImageUtility.SaveManifest(_currentManifest, CurrentRunDirectory);
            yield return null;
        }

        protected IEnumerator CaptureScreenStep(string stepName, string baselinePath = null, string notes = null)
        {
            yield return new WaitForEndOfFrame();

            _stepIndex++;
            string fileName = VisualTestImageUtility.BuildStepArtifactFileName(_stepIndex, stepName, "actual");
            string outputPath = Path.Combine(CurrentRunDirectory, fileName);

            VisualTestImageUtility.CaptureFullScreenToFile(outputPath);
            RegisterStep(stepName, fileName, baselinePath, notes);
        }

        protected IEnumerator CaptureStep(string stepName, VisualElement target, string baselinePath = null, string notes = null, int padding = 0)
        {
            Assert.That(target, Is.Not.Null, "待截图的 VisualElement 不能为空。");
            Assert.That(target.worldBound.width, Is.GreaterThan(0f), "待截图元素宽度必须大于 0。");
            Assert.That(target.worldBound.height, Is.GreaterThan(0f), "待截图元素高度必须大于 0。");

            yield return new WaitForEndOfFrame();

            _stepIndex++;
            string fileName = VisualTestImageUtility.BuildStepArtifactFileName(_stepIndex, stepName, "actual");
            string outputPath = Path.Combine(CurrentRunDirectory, fileName);
            RectInt region = VisualTestImageUtility.CreateScreenRegionFromTopLeftRect(target.worldBound, Screen.width, Screen.height, padding);

            VisualTestImageUtility.CaptureScreenRegionToFile(outputPath, region);
            RegisterStep(stepName, fileName, baselinePath, notes);
        }

        private void RegisterStep(string stepName, string actualImagePath, string baselinePath, string notes)
        {
            _currentManifest.steps.Add(new VisualImageTestStepManifest
            {
                index = _stepIndex,
                name = stepName,
                actualImagePath = actualImagePath,
                baselineImagePath = baselinePath,
                notes = notes
            });

            VisualTestImageUtility.SaveManifest(_currentManifest, CurrentRunDirectory);
        }
    }
}
```

并在 `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualTestImageUtility.cs` 追加整屏截图方法：

```csharp
public static void CaptureFullScreenToFile(string outputPath)
{
    Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
    if (screenshot == null)
    {
        throw new InvalidOperationException("无法捕获整屏截图。");
    }

    try
    {
        SaveTextureToFile(screenshot, outputPath);
    }
    finally
    {
        UnityEngine.Object.Destroy(screenshot);
    }
}
```

- [ ] **Step 4: 再跑一次 PlayMode 测试确认变绿**

通过 MCP 运行：

```text
run_tests
testNames:
- NZ.VisualTest.Tests.VisualImageTestBaseTests.CaptureScreenStep_WritesArtifactAndManifest
```

Expected:
- 1/1 PASS
- 运行后 `manifest.json` 与 `01-full-screen-actual.png` 都被写入运行目录

- [ ] **Step 5: 提交这一小步**

```bash
git add /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualImageTestBase.cs /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Runtime/VisualTestImageUtility.cs /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualImageTestBaseTests.cs
git commit -m "feat: add visual image test base"
```

### Task 3: 迁移真实业务用例到 capture-only 流程

**Files:**
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs`
- Test: `/Users/xpy/Desktop/NanZhai/CPA/Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs`

- [ ] **Step 1: 先把真实业务测试改成失败版断言**

把 `/Users/xpy/Desktop/NanZhai/CPA/Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs` 改成下面这个目标形态，让它先因为尚未接入新基类或行为不完整而失败：

```csharp
[TestFixture]
public sealed class UnifiedSettingsPanelImageValidationTests : VisualImageTestBase
{
    [UnityTest]
    public IEnumerator UnifiedSettingsPanel_ShouldCaptureExpectedStates()
    {
#if !UNITY_EDITOR
        Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
        yield break;
#else
        yield return EditorSceneManager.LoadSceneInPlayMode(
            ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));

        yield return WaitForFrames(10);

        DeskWindowController controller = UnityEngine.Object.FindFirstObjectByType<DeskWindowController>();
        Assert.That(controller, Is.Not.Null);

        UIDocument uiDocument = controller.GetComponent<UIDocument>();
        Assert.That(uiDocument, Is.Not.Null);

        yield return WaitUntilFieldAssigned(controller, "_settingsPanel", 60);

        object settingsPanel = GetPrivateField(controller, "_settingsPanel");
        InvokePublic(settingsPanel, "Show");

        yield return WaitUntilReady(uiDocument.rootVisualElement, "settings-overlay", "psp-root", 60);
        yield return CaptureOverlay(uiDocument.rootVisualElement, "pomodoro", "TestArtifacts/PencilReferences/unified-settings-pomodoro.png");

        InvokePrivate(settingsPanel, "SelectTab", "online");
        yield return WaitUntilReady(uiDocument.rootVisualElement, "settings-overlay", "osp-root", 60);
        yield return CaptureOverlay(uiDocument.rootVisualElement, "online", "TestArtifacts/PencilReferences/unified-settings-online-not-joined.png");

        InvokePrivate(settingsPanel, "SelectTab", "pet");
        yield return WaitUntilReady(uiDocument.rootVisualElement, "settings-overlay", "pet-root", 60);
        yield return CaptureOverlay(uiDocument.rootVisualElement, "pet", "TestArtifacts/PencilReferences/unified-settings-pet.png");

        Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3));
        Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "01-pomodoro-actual.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "02-online-actual.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "03-pet-actual.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(CurrentRunDirectory, "manifest.json")), Is.True);
#endif
    }

    private IEnumerator CaptureOverlay(VisualElement root, string stepName, string baselinePath)
    {
        VisualElement overlay = root.Q<VisualElement>("settings-overlay");
        Assert.That(overlay, Is.Not.Null);
        yield return CaptureStep(stepName, overlay, baselinePath, "settings-overlay");
    }
}
```

- [ ] **Step 2: 跑真实用例确认现在还是红的**

通过 MCP 运行：

```text
run_tests
testNames:
- APP.NetworkIntegration.Tests.UnifiedSettingsPanelImageValidationTests.UnifiedSettingsPanel_ShouldCaptureExpectedStates
```

Expected:
- 如果 `VisualImageTestBase` 接口与测试代码不一致，会先在编译期暴露
- 如果接口对上了，但步骤登记或命名不正确，则在运行期断言失败

- [ ] **Step 3: 把用例真正迁移到新基类**

在同一文件中完成这三件事：

```csharp
// 1. 移除这类像素级断言调用
AssertScreenshotMatchesBaseline(...);

// 2. 保留原有场景加载、反射切页与等待逻辑
yield return WaitUntilReady(...);

// 3. 把每个状态改成 capture-only
yield return CaptureOverlay(uiDocument.rootVisualElement, "pomodoro", "TestArtifacts/PencilReferences/unified-settings-pomodoro.png");
yield return CaptureOverlay(uiDocument.rootVisualElement, "online", "TestArtifacts/PencilReferences/unified-settings-online-not-joined.png");
yield return CaptureOverlay(uiDocument.rootVisualElement, "pet", "TestArtifacts/PencilReferences/unified-settings-pet.png");
```

保持辅助方法 `WaitUntilReady`、`WaitUntilFieldAssigned`、`GetPrivateField`、`InvokePublic`、`InvokePrivate` 不动，只修改截图与断言路径，避免把“测试时机”和“视觉判定”再次绑回同一个方法。

- [ ] **Step 4: 再跑一次真实业务测试确认它输出了三张图**

通过 MCP 运行：

```text
run_tests
testNames:
- APP.NetworkIntegration.Tests.UnifiedSettingsPanelImageValidationTests.UnifiedSettingsPanel_ShouldCaptureExpectedStates
```

Expected:
- 1/1 PASS
- 运行目录里有 `01-pomodoro-actual.png`、`02-online-actual.png`、`03-pet-actual.png` 与 `manifest.json`
- manifest 中 3 个步骤都带有对应的 `baselineImagePath`

- [ ] **Step 5: 提交这一小步**

```bash
git add /Users/xpy/Desktop/NanZhai/CPA/Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs
git commit -m "test: migrate unified settings panel to visual image artifacts"
```

### Task 4: 创建本地技能并补仓库内使用说明

**Files:**
- Create: `/Users/xpy/.agents/skills/unity-visual-image-validation/SKILL.md`
- Modify: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/README.md`

- [ ] **Step 1: 先记录一个没有技能时的失败场景**

在一个新会话里先尝试这个请求，不要手动补充仓库背景：

```text
帮我修改 APP.NetworkIntegration.Tests.UnifiedSettingsPanelImageValidationTests，
让它在切换 tab 时输出截图结果，
然后用结果图和 TestArtifacts/PencilReferences 里的目标图做视觉验证。
```

Expected:
- 代理会缺少“运行目录 + manifest + 目标图优先级”的固定工作流
- 容易重新把像素级判断塞回 C# 测试，而不是让技能当裁判

- [ ] **Step 2: 确认这个缺口确实存在**

人工核对上面的 baseline 行为，确认至少有一项不符合下面三条：

- 不知道 `VisualImageTestBase` 是 capture-only
- 不知道目标图优先级是“外部目标图 > manifest baseline > 技能默认值”
- 不知道每次运行目录应该是 `TestOutput/<TestMethod>/<RunId>/`

如果 baseline 已经天然满足这三条，再缩小技能范围，只保留“如何运行与如何验证”的工作流说明，不重复仓库细节。

- [ ] **Step 3: 写出技能文件与 README 增补**

在 `/Users/xpy/.agents/skills/unity-visual-image-validation/SKILL.md` 写入下面这个起始版本：

```markdown
---
name: unity-visual-image-validation
description: Use when writing or modifying Unity PlayMode visual capture tests, or when validating a test run's output images against repo baselines or external target images.
---

# Unity Visual Image Validation

## When to Use

- Create or modify a PlayMode test that should emit screenshot artifacts
- Validate a completed Unity visual test run using `manifest.json`
- Compare output images against repo baselines or user-provided external target images

## Workflow

1. Locate or create a PlayMode test that inherits `NZ.VisualTest.VisualImageTestBase`.
2. Add capture calls with `CaptureStep(...)` or `CaptureScreenStep(...)`.
3. Run the target test through Unity MCP `run_tests`.
4. Find the latest run directory under `Application.temporaryCachePath/TestOutput/<TestMethod>/<RunId>/`.
5. Read `manifest.json` and resolve target images in this order:
   - user-provided external target image
   - `baselineImagePath` from the manifest step
   - skill-level default baseline path
6. Review each `actualImagePath` against its target image using AI visual judgment.
7. Report, per step, whether the output matches the target effect and whether the likely issue is in layout, state timing, assets, or the test script.

## Rules

- Do not add pixel-threshold assertions to the C# test unless the user explicitly asks for them.
- Keep all screenshots from one test run inside the same run directory.
- Prefer modifying an existing business test over creating a duplicate test file.
```

同时在 `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/README.md` 增补一节：

```markdown
## VisualImageTestBase

`VisualImageTestBase` 用于“只产出图片工件，不做视觉断言”的 PlayMode 测试。

- 运行目录：`Application.temporaryCachePath/TestOutput/<TestMethod>/<RunId>/`
- 每次调用 `CaptureStep(...)` 或 `CaptureScreenStep(...)` 都会写出一张 `NN-stepName-actual.png`
- 同目录下会持续刷新 `manifest.json`
- 视觉判定由本地技能 `unity-visual-image-validation` 完成，而不是在 NUnit 中直接失败
```

- [ ] **Step 4: 验证技能与 README 都可被检索到**

运行：

```bash
rg -n "unity-visual-image-validation|VisualImageTestBase|manifest.json" /Users/xpy/.agents/skills/unity-visual-image-validation/SKILL.md /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/README.md
```

Expected:
- `SKILL.md` 中能看到技能名、适用场景、目标图优先级与 capture-only 规则
- `README.md` 中能看到新的运行目录与 manifest 说明

- [ ] **Step 5: 提交仓库内文档改动**

本任务的技能文件在用户本地目录，不在仓库内，不要试图把它强行纳入当前 git 仓库。只提交仓库里的 README 更新：

```bash
git add /Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/README.md
git commit -m "docs: document visual image test workflow"
```

### Task 5: 做一次完整验收回归

**Files:**
- Verify only: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualTestImageUtilityTests.cs`
- Verify only: `/Users/xpy/Desktop/NanZhai/CPA/localpackage/com.nz.visualtest/Tests/Runtime/VisualImageTestBaseTests.cs`
- Verify only: `/Users/xpy/Desktop/NanZhai/CPA/Assets/Tests/PlayMode/NetworkIntegration/UnifiedSettingsPanelImageValidationTests.cs`
- Verify only: `/Users/xpy/.agents/skills/unity-visual-image-validation/SKILL.md`

- [ ] **Step 1: 运行所有新增或修改过的自动化测试**

通过 MCP 运行：

```text
run_tests
testNames:
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.BuildRunOutputDirectory_UsesMethodScopedFolder
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.BuildStepArtifactFileName_PrefixesSequenceAndSuffix
- NZ.VisualTest.Tests.VisualTestImageUtilityTests.SaveManifest_WritesExpectedJsonFields
- NZ.VisualTest.Tests.VisualImageTestBaseTests.CaptureScreenStep_WritesArtifactAndManifest
- APP.NetworkIntegration.Tests.UnifiedSettingsPanelImageValidationTests.UnifiedSettingsPanel_ShouldCaptureExpectedStates
```

Expected:
- 5/5 PASS
- 没有新增编译错误

- [ ] **Step 2: 核对真实业务测试的最新运行目录**

在 Unity 测试日志或调试输出中定位 `CurrentRunDirectory`，确认目录中至少有：

```text
manifest.json
01-pomodoro-actual.png
02-online-actual.png
03-pet-actual.png
```

Expected:
- 图片文件名顺序稳定
- `manifest.json` 中 3 个步骤都带有 `actualImagePath` 与 `baselineImagePath`

- [ ] **Step 3: 用技能流程做一次人工验收演练**

按照本地技能流程，用下面的输入做一轮人工演练：

```text
使用 unity-visual-image-validation，
读取 UnifiedSettingsPanel_ShouldCaptureExpectedStates 本次运行目录里的 manifest.json，
把 3 张 actual 图分别和 TestArtifacts/PencilReferences 中的目标图做 AI 视觉验证，
并输出逐步骤结论与整体结论。
```

Expected:
- 演练流程会先读取 manifest 再解析目标图，而不是直接猜测图片路径
- 输出会按步骤给出“是否符合目标效果”和问题归因

- [ ] **Step 4: 如果验收中发现问题，回到对应任务修正后重跑**

修正顺序固定为：

```text
先修 Task 1/2 的工具或基类问题
再修 Task 3 的业务测试时机问题
最后修 Task 4 的技能说明问题
```

只有所有测试重新通过后，才允许进入下一步。

- [ ] **Step 5: 提交最后的验证性修正**

如果 Step 4 没有产生额外文件改动，这一步只记录“无需额外提交”。如果有修正，按实际改动提交：

```bash
git add /Users/xpy/Desktop/NanZhai/CPA
git commit -m "fix: complete visual image test verification"
```
