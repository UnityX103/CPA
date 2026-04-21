using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NZ.VisualTest
{
    /// <summary>
    /// 图片比对结果。
    /// </summary>
    public readonly struct VisualImageComparisonResult
    {
        public VisualImageComparisonResult(
            bool sizeMatches,
            int expectedWidth,
            int expectedHeight,
            int actualWidth,
            int actualHeight,
            int totalPixelCount,
            int mismatchPixelCount,
            int maxChannelDifference,
            float meanChannelDifference)
        {
            SizeMatches = sizeMatches;
            ExpectedWidth = expectedWidth;
            ExpectedHeight = expectedHeight;
            ActualWidth = actualWidth;
            ActualHeight = actualHeight;
            TotalPixelCount = totalPixelCount;
            MismatchPixelCount = mismatchPixelCount;
            MaxChannelDifference = maxChannelDifference;
            MeanChannelDifference = meanChannelDifference;
        }

        public bool SizeMatches { get; }

        public int ExpectedWidth { get; }

        public int ExpectedHeight { get; }

        public int ActualWidth { get; }

        public int ActualHeight { get; }

        public int TotalPixelCount { get; }

        public int MismatchPixelCount { get; }

        public int MaxChannelDifference { get; }

        public float MeanChannelDifference { get; }

        public float MismatchRatio => TotalPixelCount <= 0
            ? 1f
            : (float)MismatchPixelCount / TotalPixelCount;
    }

    /// <summary>
    /// 图片视觉测试辅助工具：保存截图、上传测试附件、生成 diff、执行像素比对。
    /// </summary>
    public static class VisualTestImageUtility
    {
        public static string BuildImageOutputDirectory(string testName)
        {
            if (string.IsNullOrWhiteSpace(testName))
            {
                throw new ArgumentException("testName 不能为空。", nameof(testName));
            }

            string outputDirectory = Path.Combine(
                Application.temporaryCachePath,
                "TestOutput",
                SanitizeFileName(testName),
                "Images");

            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }

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

        public static string BuildArtifactPath(string testName, string artifactName)
        {
            string safeArtifactName = SanitizeFileName(artifactName);
            return Path.Combine(BuildImageOutputDirectory(testName), $"{safeArtifactName}.png");
        }

        public static string BuildStepArtifactFileName(int stepIndex, string stepName, string suffix = "actual")
        {
            if (stepIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepIndex), "stepIndex 必须大于 0。");
            }

            if (string.IsNullOrWhiteSpace(stepName))
            {
                throw new ArgumentException("stepName 不能为空。", nameof(stepName));
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                throw new ArgumentException("suffix 不能为空。", nameof(suffix));
            }

            return $"{stepIndex:00}-{SanitizeFileName(stepName)}-{SanitizeFileName(suffix)}.png";
        }

        public static RectInt CreateScreenRegionFromTopLeftRect(
            Rect topLeftRect,
            int screenWidth,
            int screenHeight,
            int padding = 0)
        {
            int left = Mathf.Clamp(Mathf.FloorToInt(topLeftRect.xMin) - padding, 0, screenWidth);
            int right = Mathf.Clamp(Mathf.CeilToInt(topLeftRect.xMax) + padding, left, screenWidth);
            int top = Mathf.Clamp(Mathf.FloorToInt(topLeftRect.yMin) - padding, 0, screenHeight);
            int bottom = Mathf.Clamp(Mathf.CeilToInt(topLeftRect.yMax) + padding, top, screenHeight);

            int width = Mathf.Max(0, right - left);
            int height = Mathf.Max(0, bottom - top);
            int y = Mathf.Clamp(screenHeight - bottom, 0, screenHeight);

            return new RectInt(left, y, width, height);
        }

        public static void CaptureScreenRegionToFile(string outputPath, RectInt screenRegion)
        {
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (screenshot == null)
            {
                throw new InvalidOperationException("无法捕获屏幕截图。");
            }

            try
            {
                Texture2D cropped = CropTexture(screenshot, screenRegion);
                try
                {
                    SaveTextureToFile(cropped, outputPath);
                }
                finally
                {
                    UnityEngine.Object.Destroy(cropped);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(screenshot);
            }
        }

        public static void SaveTextureToFile(Texture2D texture, string outputPath)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("outputPath 不能为空。", nameof(outputPath));
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] pngBytes = texture.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                throw new InvalidOperationException("PNG 编码失败。");
            }

            File.WriteAllBytes(outputPath, pngBytes);
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

            string normalizedOutputDirectory = Path.GetFullPath(outputDirectory);
            if (string.IsNullOrWhiteSpace(manifest.outputDirectory))
            {
                manifest.outputDirectory = normalizedOutputDirectory;
            }
            else if (!PathsEqual(manifest.outputDirectory, normalizedOutputDirectory))
            {
                throw new ArgumentException("manifest.outputDirectory 与 outputDirectory 不一致。", nameof(outputDirectory));
            }

            Directory.CreateDirectory(normalizedOutputDirectory);

            string manifestPath = Path.Combine(normalizedOutputDirectory, "manifest.json");
            string json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(manifestPath, json);
        }

        public static string AttachArtifact(string filePath, string description = null)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            string testName = TestContext.CurrentContext?.Test?.Name ?? "VisualTest";
            string uploadDirectory = Path.Combine(BuildImageOutputDirectory(testName), "Uploaded");
            Directory.CreateDirectory(uploadDirectory);

            string fileName = string.IsNullOrWhiteSpace(description)
                ? Path.GetFileName(filePath)
                : $"{SanitizeFileName(description)}{Path.GetExtension(filePath)}";

            string uploadedPath = Path.Combine(uploadDirectory, fileName);
            File.Copy(filePath, uploadedPath, true);

            Debug.Log($"[VisualTestImageUtility] 已上传图片到临时目录：{uploadedPath}");
            return uploadedPath;
        }

        public static VisualImageComparisonResult ComparePngFiles(
            string expectedPath,
            string actualPath,
            string diffOutputPath,
            byte tolerance = 0)
        {
            Texture2D expected = LoadPng(expectedPath);
            Texture2D actual = LoadPng(actualPath);

            try
            {
                return CompareTextures(expected, actual, diffOutputPath, tolerance);
            }
            finally
            {
                UnityEngine.Object.Destroy(expected);
                UnityEngine.Object.Destroy(actual);
            }
        }

        public static VisualImageComparisonResult CompareTextures(
            Texture2D expected,
            Texture2D actual,
            string diffOutputPath,
            byte tolerance = 0)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            if (expected.width != actual.width || expected.height != actual.height)
            {
                return new VisualImageComparisonResult(
                    false,
                    expected.width,
                    expected.height,
                    actual.width,
                    actual.height,
                    0,
                    0,
                    255,
                    255f);
            }

            Color32[] expectedPixels = expected.GetPixels32();
            Color32[] actualPixels = actual.GetPixels32();
            Color32[] diffPixels = new Color32[expectedPixels.Length];

            int mismatchPixelCount = 0;
            int maxChannelDifference = 0;
            long totalChannelDifference = 0L;

            for (int i = 0; i < expectedPixels.Length; i++)
            {
                Color32 expectedPixel = expectedPixels[i];
                Color32 actualPixel = actualPixels[i];

                int redDiff = Mathf.Abs(expectedPixel.r - actualPixel.r);
                int greenDiff = Mathf.Abs(expectedPixel.g - actualPixel.g);
                int blueDiff = Mathf.Abs(expectedPixel.b - actualPixel.b);
                int alphaDiff = Mathf.Abs(expectedPixel.a - actualPixel.a);

                int pixelMaxDifference = Mathf.Max(redDiff, greenDiff, blueDiff, alphaDiff);
                maxChannelDifference = Mathf.Max(maxChannelDifference, pixelMaxDifference);
                totalChannelDifference += redDiff + greenDiff + blueDiff + alphaDiff;

                bool mismatched = pixelMaxDifference > tolerance;
                if (mismatched)
                {
                    mismatchPixelCount++;
                    diffPixels[i] = new Color32(255, 0, 0, 255);
                }
                else
                {
                    byte fadedAlpha = (byte)Mathf.Clamp(actualPixel.a / 4, 0, 96);
                    diffPixels[i] = new Color32(actualPixel.r, actualPixel.g, actualPixel.b, fadedAlpha);
                }
            }

            float meanChannelDifference = expectedPixels.Length == 0
                ? 0f
                : totalChannelDifference / (expectedPixels.Length * 4f);

            if (!string.IsNullOrWhiteSpace(diffOutputPath))
            {
                Texture2D diffTexture = new Texture2D(expected.width, expected.height, TextureFormat.RGBA32, false);
                try
                {
                    diffTexture.SetPixels32(diffPixels);
                    diffTexture.Apply(false, false);
                    SaveTextureToFile(diffTexture, diffOutputPath);
                }
                finally
                {
                    UnityEngine.Object.Destroy(diffTexture);
                }
            }

            return new VisualImageComparisonResult(
                true,
                expected.width,
                expected.height,
                actual.width,
                actual.height,
                expectedPixels.Length,
                mismatchPixelCount,
                maxChannelDifference,
                meanChannelDifference);
        }

        private static Texture2D LoadPng(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("找不到 PNG 文件。", filePath);
            }

            byte[] imageBytes = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(imageBytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidOperationException($"PNG 加载失败：{filePath}");
            }

            return texture;
        }

        private static Texture2D CropTexture(Texture2D source, RectInt screenRegion)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            RectInt clampedRegion = ClampRegion(screenRegion, source.width, source.height);
            if (clampedRegion.width <= 0 || clampedRegion.height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(screenRegion), "截图区域无效。");
            }

            Texture2D cropped = new Texture2D(clampedRegion.width, clampedRegion.height, TextureFormat.RGBA32, false);
            Color[] pixels = source.GetPixels(
                clampedRegion.x,
                clampedRegion.y,
                clampedRegion.width,
                clampedRegion.height);

            cropped.SetPixels(pixels);
            cropped.Apply(false, false);
            return cropped;
        }

        private static RectInt ClampRegion(RectInt region, int maxWidth, int maxHeight)
        {
            int x = Mathf.Clamp(region.x, 0, maxWidth);
            int y = Mathf.Clamp(region.y, 0, maxHeight);
            int width = Mathf.Clamp(region.width, 0, maxWidth - x);
            int height = Mathf.Clamp(region.height, 0, maxHeight - y);
            return new RectInt(x, y, width, height);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "artifact";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0 || chars[i] == '/')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static bool PathsEqual(string leftPath, string rightPath)
        {
            if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
            {
                return false;
            }

            string normalizedLeftPath = Path.GetFullPath(leftPath);
            string normalizedRightPath = Path.GetFullPath(rightPath);
            return string.Equals(normalizedLeftPath, normalizedRightPath, StringComparison.Ordinal);
        }
    }
}
