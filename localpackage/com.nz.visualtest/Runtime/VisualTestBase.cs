using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using FFmpegOut;

namespace NZ.VisualTest
{
    /// <summary>
    /// 可视化测试基类
    /// 提供屏幕录制、GUI 操作提示覆盖层和输入模拟功能
    /// </summary>
    public abstract class VisualTestBase
    {
        private GameObject _cameraObject;
        private GameObject _guiHelperObject;
        private VisualTestGuiHelper _guiHelper;
        private RuntimeFFmpegRecorder _runtimeRecorder;

        /// <summary>
        /// 测试名称，默认为类名，子类可覆盖
        /// </summary>
        protected virtual string TestName => GetType().Name;

        protected virtual bool UseDedicatedTestCamera => true;

        /// <summary>
        /// 是否在 SetUp 阶段自动启动录制，默认 true。
        /// 子类可覆盖为 false 以延迟录制，并在合适时机调用 <see cref="StartRecordingNow"/>。
        /// </summary>
        protected virtual bool RecordOnSetUp => true;

        /// <summary>
        /// 供子类在延迟录制场景下手动启动录制。
        /// 仅当 <see cref="RecordOnSetUp"/> 为 false 时才需要手动调用。
        /// </summary>
        protected void StartRecordingNow() => StartRecorder();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UseDedicatedTestCamera)
            {
                SetUpCamera();
            }
            SetUpGuiHelper();
            yield return null;
            if (RecordOnSetUp)
                StartRecorder();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StopRecorder();

            if (_guiHelperObject != null)
            {
                UnityEngine.Object.Destroy(_guiHelperObject);
                _guiHelperObject = null;
                _guiHelper = null;
            }

            if (_cameraObject != null)
            {
                UnityEngine.Object.Destroy(_cameraObject);
                _cameraObject = null;
            }
            yield return null;
        }

        private void SetUpCamera()
        {
            _cameraObject = new GameObject("VisualTest_Camera");
            var camera = _cameraObject.AddComponent<Camera>();
            _cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            _cameraObject.transform.rotation = Quaternion.identity;
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = -1;
        }

        private void SetUpGuiHelper()
        {
            _guiHelperObject = new GameObject("VisualTest_GuiHelper");
            _guiHelper = _guiHelperObject.AddComponent<VisualTestGuiHelper>();
        }

        private string GetVideoOutputDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "TestOutput", GetType().Name, "Video");
        }

        private string BuildTimestampedFileName()
        {
            return $"{TestName}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        private void StartRecorder()
        {
            try
            {
                if (!FFmpegPipe.IsAvailable)
                {
                    Debug.LogWarning("[VisualTestBase] FFmpegOut 录制器启动失败：未找到 FFmpeg 可执行文件。");
                    return;
                }

                if (!SystemInfo.supportsAsyncGPUReadback)
                {
                    Debug.LogWarning("[VisualTestBase] FFmpegOut 录制器启动失败：当前图形 API 不支持 AsyncGPUReadback。");
                    return;
                }

                var camera = ResolveCaptureCamera();
                if (camera == null)
                {
                    Debug.LogWarning("[VisualTestBase] FFmpegOut 录制器启动失败：未找到可用于录制的 Camera。");
                    return;
                }

                // 输出路径：persistentDataPath/TestOutput/{目标测试类}/Video
                string outputDir = GetVideoOutputDirectory();
                Directory.CreateDirectory(outputDir);

                string outputPath = Path.Combine(outputDir, $"{BuildTimestampedFileName()}.mp4");
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                _runtimeRecorder = camera.gameObject.GetComponent<RuntimeFFmpegRecorder>();
                if (_runtimeRecorder == null)
                {
                    _runtimeRecorder = camera.gameObject.AddComponent<RuntimeFFmpegRecorder>();
                }
                // macOS Metal 上 ScreenCapture 返回正向图像，但 Preprocess.shader 会再次 Y-flip，
                // 因此无论在 Editor 还是 Standalone 都需要 FFmpeg vflip 来抵消。
                _runtimeRecorder.StartRecording(
                    outputPath,
                    30f,
                    FFmpegPreset.H264Default,
                    true);
                Debug.Log($"[VisualTestBase] FFmpegOut 录制已启动：{outputPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VisualTestBase] FFmpegOut 录制器启动失败，测试将继续但不录制视频。原因：{e.Message}");
            }
        }

        private void StopRecorder()
        {
            if (_runtimeRecorder == null)
                return;

            _runtimeRecorder.StopRecording();
            UnityEngine.Object.Destroy(_runtimeRecorder);
            _runtimeRecorder = null;
        }

        private Camera ResolveCaptureCamera()
        {
            if (_cameraObject != null)
            {
                var dedicatedCamera = _cameraObject.GetComponent<Camera>();
                if (dedicatedCamera != null)
                    return dedicatedCamera;
            }

            if (Camera.main != null)
                return Camera.main;

            return UnityEngine.Object.FindFirstObjectByType<Camera>();
        }

        private sealed class RuntimeFFmpegRecorder : MonoBehaviour
        {
            private FFmpegSession _session;
            private RenderTexture _captureTexture;
            private Coroutine _recordCoroutine;
            private float _frameRate;
            private int _frameCount;
            private float _startTime;
            private int _frameDropCount;

            private float FrameTime => _startTime + (_frameCount - 0.5f) / _frameRate;

            public void StartRecording(string outputPath, float frameRate, FFmpegPreset preset, bool applyExtraVFlip)
            {
                StopRecording();

                _frameRate = Mathf.Max(1f, frameRate);
                int width = Mathf.Max(8, Screen.width);
                int height = Mathf.Max(8, Screen.height);

                _captureTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                _captureTexture.Create();

                if (applyExtraVFlip)
                {
                    // ScreenCapture 到 FFmpegOut 这条链路在运行时会出现额外倒置，这里补一层 vflip 抵消。
                    var arguments =
                        "-y -f rawvideo -vcodec rawvideo -pixel_format rgba"
                        + " -colorspace bt709"
                        + " -video_size " + width + "x" + height
                        + " -framerate " + _frameRate
                        + " -loglevel warning -i - -vf vflip " + preset.GetOptions()
                        + " \"" + outputPath + "\"";
                    _session = FFmpegSession.CreateWithArguments(arguments);
                }
                else
                {
                    _session = FFmpegSession.CreateWithOutputPath(outputPath, width, height, _frameRate, preset);
                }

                _frameCount = 0;
                _frameDropCount = 0;
                _startTime = Time.time;
                _recordCoroutine = StartCoroutine(RecordLoop());
            }

            public void StopRecording()
            {
                if (_recordCoroutine != null)
                {
                    StopCoroutine(_recordCoroutine);
                    _recordCoroutine = null;
                }

                if (_session != null)
                {
                    _session.Close();
                    _session.Dispose();
                    _session = null;
                }

                if (_captureTexture != null)
                {
                    _captureTexture.Release();
                    UnityEngine.Object.Destroy(_captureTexture);
                    _captureTexture = null;
                }
            }

            private IEnumerator RecordLoop()
            {
                var endOfFrame = new WaitForEndOfFrame();
                while (_session != null)
                {
                    yield return endOfFrame;
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(_captureTexture);
                    PushFrameWithTiming(_captureTexture);
                    _session.CompletePushFrames();
                }
            }

            private void PushFrameWithTiming(Texture frameTexture)
            {
                float gap = Time.time - FrameTime;
                float delta = 1f / _frameRate;

                if (gap < 0f)
                {
                    _session.PushFrame(null);
                }
                else if (gap < delta)
                {
                    _session.PushFrame(frameTexture);
                    _frameCount++;
                }
                else if (gap < delta * 2f)
                {
                    _session.PushFrame(frameTexture);
                    _session.PushFrame(frameTexture);
                    _frameCount += 2;
                }
                else
                {
                    if (++_frameDropCount == 10)
                    {
                        Debug.LogWarning("[VisualTestBase] FFmpegOut 录制检测到明显丢帧，建议降低录制帧率。");
                    }

                    _session.PushFrame(frameTexture);
                    _frameCount += Mathf.FloorToInt(gap * _frameRate);
                }
            }

            private void OnDisable()
            {
                StopRecording();
            }
        }

        /// <summary>
        /// 向 GUI 输出当前操作描述
        /// </summary>
        protected void LogInputAction(string text)
        {
            _guiHelper?.LogAction(text);
            Debug.Log($"[VisualTest] {text}");
        }

        /// <summary>
        /// 模拟键盘按键并记录日志
        /// </summary>
        protected IEnumerator SimulateKey(Key key, string displayName = null)
        {
            string label = displayName ?? key.ToString();
            LogInputAction($"按键: {label}");

            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            yield return null;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.RemoveDevice(keyboard);
            yield return null;
        }

        /// <summary>
        /// 模拟鼠标按键并记录日志
        /// </summary>
        /// <param name="button">0=左键, 1=右键, 2=中键</param>
        /// <param name="displayName">显示名称</param>
        protected IEnumerator SimulateMouseButton(int button, string displayName = null)
        {
            string label = displayName ?? $"鼠标按键{button}";
            LogInputAction($"点击: {label}");

            var mouse = InputSystem.AddDevice<Mouse>();

            MouseState pressedState = new MouseState();
            MouseState releasedState = new MouseState();

            switch (button)
            {
                case 0:
                    pressedState = pressedState.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
                    break;
                case 1:
                    pressedState = pressedState.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, true);
                    break;
                case 2:
                    pressedState = pressedState.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Middle, true);
                    break;
                default:
                    pressedState = pressedState.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
                    break;
            }

            InputSystem.QueueStateEvent(mouse, pressedState);
            InputSystem.Update();
            yield return null;

            InputSystem.QueueStateEvent(mouse, releasedState);
            InputSystem.Update();
            InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        /// <summary>
        /// 捕获指定 UI Toolkit 元素截图，保存到项目临时目录并作为测试附件上传。
        /// </summary>
        protected IEnumerator CaptureElementScreenshot(
            VisualElement element,
            string artifactName,
            Action<string> onSaved = null,
            int padding = 0)
        {
            Assert.That(element, Is.Not.Null, "待截图的 VisualElement 不能为空。");
            Assert.That(element.worldBound.width, Is.GreaterThan(0f), "待截图元素宽度必须大于 0。");
            Assert.That(element.worldBound.height, Is.GreaterThan(0f), "待截图元素高度必须大于 0。");

            yield return new WaitForEndOfFrame();

            RectInt region = VisualTestImageUtility.CreateScreenRegionFromTopLeftRect(
                element.worldBound,
                Screen.width,
                Screen.height,
                padding);

            Assert.That(region.width, Is.GreaterThan(0), "截图区域宽度必须大于 0。");
            Assert.That(region.height, Is.GreaterThan(0), "截图区域高度必须大于 0。");

            string actualPath = VisualTestImageUtility.BuildArtifactPath(TestName, artifactName);
            VisualTestImageUtility.CaptureScreenRegionToFile(actualPath, region);
            VisualTestImageUtility.AttachArtifact(actualPath, $"Captured {artifactName}");
            onSaved?.Invoke(actualPath);
        }

        /// <summary>
        /// 将捕获图片与设计基线图片进行比对，并输出 diff 附件。
        /// </summary>
        protected void AssertScreenshotMatchesBaseline(
            string actualPath,
            string expectedPath,
            string diffArtifactName,
            byte tolerance = 0,
            float maxMismatchRatio = 0f)
        {
            Assert.That(File.Exists(actualPath), Is.True, $"实际截图不存在：{actualPath}");
            Assert.That(File.Exists(expectedPath), Is.True, $"基线图片不存在：{expectedPath}");

            VisualTestImageUtility.AttachArtifact(expectedPath, $"Baseline {Path.GetFileName(expectedPath)}");

            string diffPath = VisualTestImageUtility.BuildArtifactPath(TestName, diffArtifactName);
            VisualImageComparisonResult result = VisualTestImageUtility.ComparePngFiles(
                expectedPath,
                actualPath,
                diffPath,
                tolerance);

            VisualTestImageUtility.AttachArtifact(diffPath, $"Diff {diffArtifactName}");

            Assert.That(
                result.SizeMatches,
                Is.True,
                $"截图尺寸不一致。expected={result.ExpectedWidth}x{result.ExpectedHeight}, actual={result.ActualWidth}x{result.ActualHeight}");

            Assert.That(
                result.MismatchRatio,
                Is.LessThanOrEqualTo(maxMismatchRatio),
                $"截图与基线差异过大。mismatch={result.MismatchPixelCount}/{result.TotalPixelCount}, ratio={result.MismatchRatio:P2}, maxChannelDiff={result.MaxChannelDifference}, meanChannelDiff={result.MeanChannelDifference:F2}");
        }
    }
}
