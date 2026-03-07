using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

namespace NZ.VisualTest.Runtime
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

#if UNITY_EDITOR
        private RecorderController _recorderController;
#endif

        /// <summary>
        /// 测试名称，默认为类名，子类可覆盖
        /// </summary>
        protected virtual string TestName => GetType().Name;

        protected virtual bool UseDedicatedTestCamera => true;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UseDedicatedTestCamera)
            {
                SetUpCamera();
            }
            SetUpGuiHelper();
            yield return null;

#if UNITY_EDITOR
            // 录制器启动时 Unity 音频系统可能禁用，会产生已知错误日志
            // 提前声明忽略，避免 Test Runner 将其判定为测试失败
            LogAssert.ignoreFailingMessages = true;
            StartRecorder();
            // PrepareRecording 内部会将 Time.timeScale 设为 0 等待第一帧
            // 等 2 帧让 Recorder 完成初始化，再强制恢复 timeScale，
            // 否则后续 WaitForSeconds 永远不推进
            yield return null;
            yield return null;
            if (Time.timeScale == 0f)
                Time.timeScale = 1f;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
#if UNITY_EDITOR
            if (_recorderController != null && _recorderController.IsRecording())
            {
                _recorderController.StopRecording();
            }
            yield return new WaitForSeconds(0.5f);
            _recorderController = null;
#endif

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

#if !UNITY_EDITOR
            yield return null;
#endif
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

#if UNITY_EDITOR
        private void StartRecorder()
        {
            try
            {
                var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                controllerSettings.SetRecordModeToManual();

                var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movieSettings.Enabled = true;
                movieSettings.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
                movieSettings.ImageInputSettings = new GameViewInputSettings();
                // 测试环境音频系统被禁用，关闭音轨录制避免 "Zero sample rate" 错误
                movieSettings.AudioInputSettings.PreserveAudio = false;

                // 输出路径：项目根目录/TestVideo/TestName_时间戳
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string outputDir = Path.Combine(projectRoot, "TestVideo");
                Directory.CreateDirectory(outputDir);

                // 设置输出绝对路径
                var fileNameGenerator = movieSettings.FileNameGenerator;
                fileNameGenerator.Root = OutputPath.Root.Absolute;

                var absPathProperty = typeof(FileNameGenerator).GetProperty(
                    "AbsolutePath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (absPathProperty != null)
                {
                    absPathProperty.SetValue(fileNameGenerator, outputDir);
                }
                else
                {
                    var leaf = typeof(FileNameGenerator).GetField(
                        "m_Leaf",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (leaf != null)
                        leaf.SetValue(fileNameGenerator, outputDir);
                }

                fileNameGenerator.FileName = $"{TestName}_{DateTime.Now:yyyyMMdd_HHmmss}";

                controllerSettings.AddRecorderSettings(movieSettings);
                _recorderController = new RecorderController(controllerSettings);
                // PrepareRecording 初始化录制会话（必须调用，否则 m_RecordingSessions 为空导致不录制）
                // 注意：它会将 Time.timeScale 设为 0，SetUp 会在之后手动恢复
                _recorderController.PrepareRecording();
                _recorderController.StartRecording();
                Debug.Log($"[VisualTestBase] 录制已启动：{outputDir}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VisualTestBase] 录制器启动失败，测试将继续但不录制视频。原因：{e.Message}");
            }
        }
#endif

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
                    pressedState = pressedState.WithButton(MouseButton.Left, true);
                    break;
                case 1:
                    pressedState = pressedState.WithButton(MouseButton.Right, true);
                    break;
                case 2:
                    pressedState = pressedState.WithButton(MouseButton.Middle, true);
                    break;
                default:
                    pressedState = pressedState.WithButton(MouseButton.Left, true);
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
    }
}
