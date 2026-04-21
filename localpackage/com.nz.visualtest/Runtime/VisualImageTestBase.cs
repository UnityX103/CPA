using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NZ.VisualTest
{
    /// <summary>
    /// 视觉图片测试基类：只负责产出截图工件与 manifest，不在测试期内做视觉判定。
    /// </summary>
    public abstract class VisualImageTestBase
    {
        private VisualImageTestRunManifest _currentManifest;
        private int _stepIndex;

        protected string CurrentRunDirectory { get; private set; }

        protected VisualImageTestRunManifest CurrentManifest => CloneManifest(_currentManifest);

        [UnitySetUp]
        public IEnumerator SetUpVisualImageRun()
        {
            Type testType = GetType();
            string testName = TestContext.CurrentContext.Test.MethodName ?? TestContext.CurrentContext.Test.Name;
            string runId = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            CurrentRunDirectory = VisualTestImageUtility.BuildRunOutputDirectory(testName, runId);
            _currentManifest = new VisualImageTestRunManifest
            {
                testName = testName,
                testClass = testType.FullName ?? testType.Name,
                runId = runId,
                createdAt = DateTimeOffset.Now.ToString("O"),
                outputDirectory = CurrentRunDirectory
            };
            _stepIndex = 0;

            SaveManifest();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownVisualImageRun()
        {
            SaveManifest();
            yield return null;
        }

        protected IEnumerator CaptureScreenStep(string stepName, string baselinePath = null, string notes = null)
        {
            EnsureRunInitialized();
            yield return new WaitForEndOfFrame();

            _stepIndex++;
            string fileName = VisualTestImageUtility.BuildStepArtifactFileName(_stepIndex, stepName, "actual");
            string outputPath = Path.Combine(CurrentRunDirectory, fileName);

            VisualTestImageUtility.CaptureFullScreenToFile(outputPath);
            RegisterStep(stepName, fileName, baselinePath, notes);
        }

        protected IEnumerator CaptureStep(
            string stepName,
            VisualElement target,
            string baselinePath = null,
            string notes = null,
            int padding = 0)
        {
            EnsureRunInitialized();
            Assert.That(target, Is.Not.Null, "待截图的 VisualElement 不能为空。");
            Assert.That(target.worldBound.width, Is.GreaterThan(0f), "待截图元素宽度必须大于 0。");
            Assert.That(target.worldBound.height, Is.GreaterThan(0f), "待截图元素高度必须大于 0。");

            yield return new WaitForEndOfFrame();

            _stepIndex++;
            string fileName = VisualTestImageUtility.BuildStepArtifactFileName(_stepIndex, stepName, "actual");
            string outputPath = Path.Combine(CurrentRunDirectory, fileName);
            RectInt region = VisualTestImageUtility.CreateScreenRegionFromTopLeftRect(
                target.worldBound,
                Screen.width,
                Screen.height,
                padding);

            Assert.That(region.width, Is.GreaterThan(0), "截图区域宽度必须大于 0。");
            Assert.That(region.height, Is.GreaterThan(0), "截图区域高度必须大于 0。");

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

            SaveManifest();
        }

        private void SaveManifest()
        {
            VisualTestImageUtility.SaveManifest(_currentManifest, CurrentRunDirectory);
        }

        private void EnsureRunInitialized()
        {
            if (string.IsNullOrWhiteSpace(CurrentRunDirectory) || _currentManifest == null)
            {
                throw new InvalidOperationException(
                    "视觉图片测试运行尚未初始化。请先通过 SetUpVisualImageRun 完成初始化，再调用截图步骤。");
            }
        }

        private static VisualImageTestRunManifest CloneManifest(VisualImageTestRunManifest manifest)
        {
            if (manifest == null)
            {
                return null;
            }

            var clone = new VisualImageTestRunManifest
            {
                testName = manifest.testName,
                testClass = manifest.testClass,
                runId = manifest.runId,
                createdAt = manifest.createdAt,
                outputDirectory = manifest.outputDirectory
            };

            if (manifest.steps == null)
            {
                return clone;
            }

            foreach (VisualImageTestStepManifest step in manifest.steps)
            {
                clone.steps.Add(CloneStep(step));
            }

            return clone;
        }

        private static VisualImageTestStepManifest CloneStep(VisualImageTestStepManifest step)
        {
            if (step == null)
            {
                return null;
            }

            return new VisualImageTestStepManifest
            {
                index = step.index,
                name = step.name,
                actualImagePath = step.actualImagePath,
                baselineImagePath = step.baselineImagePath,
                notes = step.notes
            };
        }
    }
}
