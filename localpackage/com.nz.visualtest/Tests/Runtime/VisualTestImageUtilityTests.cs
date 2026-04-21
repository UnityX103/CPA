using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NZ.VisualTest.Tests
{
    public sealed class VisualTestImageUtilityTests
    {
        private string _outputDirectory;

        [SetUp]
        public void SetUp()
        {
            _outputDirectory = Path.Combine(
                Application.temporaryCachePath,
                "NZ.VisualTest.Tests",
                TestContext.CurrentContext.Test.Name);

            if (Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, true);
            }

            Directory.CreateDirectory(_outputDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, true);
            }
        }

        [Test]
        public void CreateScreenRegionFromTopLeftRect_ConvertsToBottomLeftCoordinates()
        {
            RectInt region = VisualTestImageUtility.CreateScreenRegionFromTopLeftRect(
                new Rect(10f, 20f, 30f, 40f),
                200,
                120,
                2);

            Assert.That(region.x, Is.EqualTo(8));
            Assert.That(region.y, Is.EqualTo(58));
            Assert.That(region.width, Is.EqualTo(34));
            Assert.That(region.height, Is.EqualTo(44));
        }

        [Test]
        public void ComparePngFiles_ReturnsPerfectMatch_ForIdenticalImages()
        {
            string expectedPath = Path.Combine(_outputDirectory, "expected.png");
            string actualPath = Path.Combine(_outputDirectory, "actual.png");
            string diffPath = Path.Combine(_outputDirectory, "diff.png");

            Texture2D expected = CreateSolidTexture(new Color32(10, 20, 30, 255));
            Texture2D actual = CreateSolidTexture(new Color32(10, 20, 30, 255));

            try
            {
                VisualTestImageUtility.SaveTextureToFile(expected, expectedPath);
                VisualTestImageUtility.SaveTextureToFile(actual, actualPath);
            }
            finally
            {
                Object.DestroyImmediate(expected);
                Object.DestroyImmediate(actual);
            }

            VisualImageComparisonResult result = VisualTestImageUtility.ComparePngFiles(
                expectedPath,
                actualPath,
                diffPath);

            Assert.That(result.SizeMatches, Is.True);
            Assert.That(result.MismatchPixelCount, Is.EqualTo(0));
            Assert.That(result.MismatchRatio, Is.EqualTo(0f));
            Assert.That(File.Exists(diffPath), Is.True);
        }

        [Test]
        public void ComparePngFiles_DetectsChangedPixels_AndWritesDiff()
        {
            string expectedPath = Path.Combine(_outputDirectory, "expected.png");
            string actualPath = Path.Combine(_outputDirectory, "actual.png");
            string diffPath = Path.Combine(_outputDirectory, "diff.png");

            Texture2D expected = CreateSolidTexture(new Color32(255, 255, 255, 255));
            Texture2D actual = CreateSolidTexture(new Color32(255, 255, 255, 255));
            actual.SetPixel(0, 0, new Color32(0, 0, 0, 255));
            actual.Apply(false, false);

            try
            {
                VisualTestImageUtility.SaveTextureToFile(expected, expectedPath);
                VisualTestImageUtility.SaveTextureToFile(actual, actualPath);
            }
            finally
            {
                Object.DestroyImmediate(expected);
                Object.DestroyImmediate(actual);
            }

            VisualImageComparisonResult result = VisualTestImageUtility.ComparePngFiles(
                expectedPath,
                actualPath,
                diffPath);

            Assert.That(result.SizeMatches, Is.True);
            Assert.That(result.MismatchPixelCount, Is.EqualTo(1));
            Assert.That(result.MismatchRatio, Is.EqualTo(0.25f));
            Assert.That(File.Exists(diffPath), Is.True);
        }

        [Test]
        public void BuildRunOutputDirectory_UsesMethodScopedFolder()
        {
            string testName = "UnifiedSettingsPanel_ShouldCaptureExpectedStates";
            string runId = "20260421_153011_ab12cd";
            string outputDirectory = VisualTestImageUtility.BuildRunOutputDirectory(testName, runId);

            try
            {
                Assert.That(outputDirectory, Does.Contain("TestOutput"));
                Assert.That(Directory.Exists(outputDirectory), Is.True);

                DirectoryInfo outputInfo = new DirectoryInfo(outputDirectory);
                Assert.That(outputInfo.Name, Is.EqualTo(runId));
                Assert.That(outputInfo.Parent, Is.Not.Null);
                Assert.That(outputInfo.Parent.Name, Is.EqualTo(testName));
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }

        [Test]
        public void BuildStepArtifactFileName_PrefixesSequenceAndSuffix()
        {
            string fileName = VisualTestImageUtility.BuildStepArtifactFileName(2, "online state", "actual");

            Assert.That(fileName, Is.EqualTo("02-online state-actual.png"));
        }

        [Test]
        public void BuildImageOutputDirectory_UsesLegacyImagesSuffix()
        {
            string testName = "UnifiedSettingsPanel_ShouldCaptureExpectedStates";
            string outputDirectory = VisualTestImageUtility.BuildImageOutputDirectory(testName);

            try
            {
                Assert.That(outputDirectory, Does.Contain("TestOutput"));
                Assert.That(Directory.Exists(outputDirectory), Is.True);

                DirectoryInfo outputInfo = new DirectoryInfo(outputDirectory);
                Assert.That(outputInfo.Name, Is.EqualTo("Images"));
                Assert.That(outputInfo.Parent, Is.Not.Null);
                Assert.That(outputInfo.Parent.Name, Is.EqualTo(testName));
            }
            finally
            {
                DirectoryInfo outputInfo = new DirectoryInfo(outputDirectory);
                string testDirectory = outputInfo.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(testDirectory) && Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
        }

        [Test]
        public void BuildStepArtifactFileName_Throws_WhenStepNameIsBlank()
        {
            Assert.Throws<ArgumentException>(() =>
                VisualTestImageUtility.BuildStepArtifactFileName(1, " ", "actual"));
        }

        [Test]
        public void BuildStepArtifactFileName_Throws_WhenSuffixIsBlank()
        {
            Assert.Throws<ArgumentException>(() =>
                VisualTestImageUtility.BuildStepArtifactFileName(1, "online state", " "));
        }

        [Test]
        public void SaveManifest_WritesExpectedJsonFields()
        {
            VisualImageTestRunManifest manifest = new VisualImageTestRunManifest
            {
                testName = "UnifiedSettingsPanel_ShouldCaptureExpectedStates",
                testClass = "NZ.VisualTest.Tests.UnifiedSettingsPanelVisualTests",
                runId = "20260421_153011_ab12cd",
                createdAt = "2026-04-21T15:30:11Z",
                steps =
                {
                    new VisualImageTestStepManifest
                    {
                        index = 1,
                        name = "online state",
                        actualImagePath = "actual/01-online state-actual.png",
                        baselineImagePath = "TestArtifacts/PencilReferences/unified-settings-pomodoro.png",
                        notes = "baseline reference"
                    }
                }
            };

            VisualTestImageUtility.SaveManifest(manifest, _outputDirectory);

            string manifestPath = Path.Combine(_outputDirectory, "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True);
            Assert.That(manifest.outputDirectory, Is.EqualTo(_outputDirectory));

            string json = File.ReadAllText(manifestPath);
            VisualImageTestRunManifest savedManifest = JsonUtility.FromJson<VisualImageTestRunManifest>(json);

            Assert.That(savedManifest, Is.Not.Null);
            Assert.That(savedManifest.testName, Is.EqualTo("UnifiedSettingsPanel_ShouldCaptureExpectedStates"));
            Assert.That(savedManifest.outputDirectory, Is.EqualTo(_outputDirectory));
            Assert.That(savedManifest.steps, Is.Not.Null);
            Assert.That(savedManifest.steps.Count, Is.EqualTo(1));

            VisualImageTestStepManifest step = savedManifest.steps[0];
            Assert.That(step.index, Is.EqualTo(1));
            Assert.That(step.name, Is.EqualTo("online state"));
            Assert.That(step.actualImagePath, Is.EqualTo("actual/01-online state-actual.png"));
            Assert.That(step.baselineImagePath, Is.EqualTo("TestArtifacts/PencilReferences/unified-settings-pomodoro.png"));
            Assert.That(step.notes, Is.EqualTo("baseline reference"));
        }

        [Test]
        public void SaveManifest_Throws_WhenManifestOutputDirectoryConflicts()
        {
            VisualImageTestRunManifest manifest = new VisualImageTestRunManifest
            {
                testName = "UnifiedSettingsPanel_ShouldCaptureExpectedStates",
                outputDirectory = Path.Combine(_outputDirectory, "other")
            };

            Assert.Throws<ArgumentException>(() =>
                VisualTestImageUtility.SaveManifest(manifest, _outputDirectory));
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color32[] pixels = { color, color, color, color };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
