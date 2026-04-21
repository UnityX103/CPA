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
