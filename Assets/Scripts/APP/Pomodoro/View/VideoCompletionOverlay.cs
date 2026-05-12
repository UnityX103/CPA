using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using APP.Pomodoro.Native;
using APP.Pomodoro.System;
using Kirurobo;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace APP.Pomodoro.View
{
    /// <summary>
    /// 计时结束播放视频的表现层 Controller。挂在场景中预存的 “Video” GameObject 上。
    ///
    /// 透明背景走「视频自带 alpha 通道」原生方案——VideoPlayer 解码到 ARGB32 RenderTexture，
    /// RawImage 用默认 UI shader 直出，不做任何扣绿。要求本地视频源必须是带 alpha 的格式
    /// （macOS 推荐 HEVC with alpha .mov，或 ProRes 4444 .mov），否则透明区域不会透。
    ///
    /// 关键时序（避免首帧灰屏）：
    /// 1. Awake 立即预建 Canvas（ScreenSpaceOverlay）+ RawImage + Skip Button，
    ///    并把 Canvas.enabled 置 false，让整套 UI 处于"创建好但完全不渲染"的态。
    /// 2. 收到 E_RequestPlayCompletionVideo 后启动 PrepareAndShowCoroutine：
    ///    a. 隐藏 _uiRootsToHide 里的 UI 根（UIDocument 走 rootVisualElement.style.display=None，
    ///       不切 GameObject.active；这样视频结束后 visual tree 不重建，QFramework Controller
    ///       的 BindableProperty / 事件订阅不会失效。没挂 UIDocument 的根才回退 SetActive）
    ///    b. 创建 ARGB32 RenderTexture 并指给 RawImage
    ///    c. AddComponent VideoPlayer，配静音、URL、目标 RT
    ///    d. Prepare()，等 prepareCompleted
    ///    e. Play()，再等一帧 + WaitForEndOfFrame，确保 VideoPlayer 已把首帧写进 RT
    ///    f. 把 Canvas.enabled 打开 → 用户看到的第一帧就是真正的视频画面，不是灰色 RT 默认色
    /// 3. Hide() 把 Canvas.enabled 切回 false、停 Player、销毁 RT、恢复 UI 根原始 display/active。
    ///
    /// 视频固定静音（VideoAudioOutputMode.None + controlledAudioTrackCount = 0）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VideoCompletionOverlay : MonoBehaviour, IController, ICanSendEvent
    {
        [Header("播放期间需要隐藏的 UI 根节点（番茄钟主面板 / 设置面板等）")] [SerializeField]
        private GameObject[] _uiRootsToHide;

        // 预建的覆盖层，整个组件生命周期都在
        private Canvas _canvas;
        private RawImage _rawImage;

        // 每次播放重建
        private VideoPlayer _player;
        private RenderTexture _renderTexture;

        // 协程 / UI 隐藏状态
        private Coroutine _playCoroutine;
        private List<HiddenRootState> _hiddenRootStates;

        // 临时帧率切换状态（仅在视频播放期间生效）
        private bool _frameRateOverridden;
        private int _originalTargetFrameRate;

        // 临时显示器切换状态（仅在视频播放期间生效）。null 表示没动；
        // 不为 null 时记录视频播放前的 model.TargetMonitorIndex.Value，Hide 时还原。
        private int? _monitorIndexBeforeVideo;

        // 视频播放期间强制关闭"基于 alpha 的点击穿透"，避免视频画面下方的桌面被穿透点中。
        // Hide 时把 isHitTestEnabled / isClickThrough 双双对称还原；null 表示没动。
        private UniWindowController _uwc;
        private bool? _hitTestEnabledBeforeVideo;
        private bool? _clickThroughBeforeVideo;

        // 记录每个 UI 根的隐藏前状态。优先用 UIDocument.rootVisualElement.style.display
        // 切换显隐——这样 GameObject 始终激活，UIDocument 不会重建 visual tree，
        // QFramework Controller 在 panel 上注册的 BindableProperty / 事件回调不会失效。
        // 没挂 UIDocument 的根才回退走 SetActive。
        private struct HiddenRootState
        {
            public GameObject Root;
            public VisualElement Element;
            public StyleEnum<DisplayStyle> OriginalDisplay;
            public bool OriginalActive;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return APP.Pomodoro.GameApp.Interface;
        }

        private void Awake()
        {
            BuildOverlayCanvas();

            this.RegisterEvent<E_RequestPlayCompletionVideo>(OnRequest)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

#if UNITY_EDITOR
        // 编辑器 PlayMode 下的快捷测试：按 N 直接拉 model 当前 EndActionVideoPath 起播。
        // 仅在 UNITY_EDITOR 下编译，打包后不带这段代码。
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.N))
            {
                return;
            }

            if (_player != null)
            {
                Debug.Log("[VideoCompletionOverlay][EditorTest] 正在播放中，N 键忽略");
                return;
            }

            string path = null;
            try
            {
                IPomodoroModel model = this.GetModel<IPomodoroModel>();
                path = model?.EndActionVideoPath?.Value;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VideoCompletionOverlay][EditorTest] 取 model 视频路径失败：{ex}");
                return;
            }

            Debug.Log($"[VideoCompletionOverlay][EditorTest] N 按下，model.EndActionVideoPath='{path ?? "<null>"}'");
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("[VideoCompletionOverlay][EditorTest] 模型里没有视频路径，先去番茄钟设置面板选一个再按 N");
                return;
            }

            Play(path);
        }
#endif

        private void BuildOverlayCanvas()
        {
            GameObject canvasGo = new GameObject("VideoCanvas");
            canvasGo.transform.SetParent(transform, worldPositionStays: false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue;
            _canvas.enabled = false; // 关键：默认不渲染，等首帧到位才打开
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject imageGo = new GameObject("VideoRawImage");
            imageGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);

            _rawImage = imageGo.AddComponent<RawImage>();
            RectTransform rt = _rawImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            UnityEngine.UI.Button btn = imageGo.AddComponent<UnityEngine.UI.Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Hide);

            Debug.Log("[VideoCompletionOverlay] BuildOverlayCanvas 完成（Canvas.enabled=false）");
        }

        private void OnRequest(E_RequestPlayCompletionVideo evt)
        {
            Debug.Log(
                $"[VideoCompletionOverlay] 收到 E_RequestPlayCompletionVideo，VideoPath='{evt.VideoPath ?? "<null>"}'");
            try
            {
                Play(evt.VideoPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VideoCompletionOverlay] 播放失败: {ex}");
                Hide();
            }
        }

        private void Play(string path)
        {
            Debug.Log(
                $"[VideoCompletionOverlay] Play 进入，path='{path ?? "<null>"}'，FileExists={(string.IsNullOrWhiteSpace(path) ? "n/a" : File.Exists(path).ToString())}");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[VideoCompletionOverlay] 无效视频路径: {path}");
                return;
            }

            Hide();
            HideUIRoots();
            DisableClickThroughForVideo();
            SwitchToForegroundMonitorIfNeeded();

            int width = Mathf.Max(2, Screen.width);
            int height = Mathf.Max(2, Screen.height);
            // ARGB32 显式带 alpha 通道，确保视频解码出的 alpha 写得进 RT。
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            if (_rawImage != null)
            {
                _rawImage.texture = _renderTexture;
            }

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.source = VideoSource.Url;
            _player.url = path.StartsWith("file://", StringComparison.Ordinal) ? path : $"file://{path}";
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _renderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.controlledAudioTrackCount = 0;
            _player.loopPointReached += OnLoopPointReached;
            _player.errorReceived += OnErrorReceived;

            _playCoroutine = StartCoroutine(PrepareAndShowCoroutine());
        }

        // VideoPlayer.Prepare 在编码异常 / 源码损坏的极端情况下可能既不 prepareCompleted 也不 errorReceived；
        // 不加超时会让 UI 永久保持 HideUIRoots + click-through 关闭，用户只能强杀进程。
        private const float PrepareTimeoutSeconds = 5f;

        private IEnumerator PrepareAndShowCoroutine()
        {
            VideoPlayer player = _player;
            if (player == null)
            {
                yield break;
            }

            bool prepared = false;
            VideoPlayer.EventHandler onPrepared = _ => prepared = true;
            player.prepareCompleted += onPrepared;
            Debug.Log("[VideoCompletionOverlay] PrepareAndShowCoroutine：调 Prepare()");
            player.Prepare();

            // 等 Prepare 完成（VideoPlayer 已经把首帧解码就绪到内部缓冲）
            float deadline = Time.realtimeSinceStartup + PrepareTimeoutSeconds;
            while (!prepared)
            {
                if (_player == null || _player != player)
                {
                    player.prepareCompleted -= onPrepared;
                    yield break;
                }

                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogWarning(
                        $"[VideoCompletionOverlay] PrepareAndShowCoroutine：Prepare 超时（>{PrepareTimeoutSeconds:F1}s），自动 Hide 恢复 UI/click-through");
                    player.prepareCompleted -= onPrepared;
                    // 在 Hide() 进 StopCoroutine 自己之前把句柄抹掉，避免对正在执行的协程做无意义停止。
                    _playCoroutine = null;
                    Hide();
                    yield break;
                }

                yield return null;
            }

            player.prepareCompleted -= onPrepared;

            // Prepare 完成后 player.frameRate 才有效。把全局 targetFrameRate 临时切到视频帧率，
            // 让播放期间 Update/渲染节奏匹配源帧率，避免空闲低帧率（如 IdleFps=10）下视频卡顿。
            float videoFps = player.frameRate;
            if (videoFps > 0f)
            {
                _originalTargetFrameRate = Application.targetFrameRate;
                int target = Mathf.Max(1, Mathf.RoundToInt(videoFps));
                Application.targetFrameRate = target;
                _frameRateOverridden = true;
                Debug.Log(
                    $"[VideoCompletionOverlay] PrepareAndShowCoroutine：targetFrameRate {_originalTargetFrameRate} -> {target}（视频源 {videoFps:F2} fps）");
            }
            else
            {
                Debug.LogWarning(
                    $"[VideoCompletionOverlay] PrepareAndShowCoroutine：未取到视频帧率（player.frameRate={videoFps}），跳过帧率切换");
            }

            Debug.Log("[VideoCompletionOverlay] PrepareAndShowCoroutine：Prepare 完成，调 Play()");
            player.Play();

            // 再等一帧 + WaitForEndOfFrame：让 VideoPlayer 把首帧真正渲染到 RT，再打开 Canvas，避免灰帧
            yield return null;
            yield return new WaitForEndOfFrame();

            if (_player != player)
            {
                yield break;
            }

            if (_canvas != null)
            {
                _canvas.enabled = true;
                Debug.Log("[VideoCompletionOverlay] PrepareAndShowCoroutine：首帧已落 RT，打开 Canvas");
            }

            _playCoroutine = null;
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            Debug.Log("[VideoCompletionOverlay] loopPointReached：自然结束");
            Hide();
        }

        private void OnErrorReceived(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[VideoCompletionOverlay] VideoPlayer error: {message}");
            Hide();
        }

        private void Hide()
        {
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }

            if (_canvas != null)
            {
                _canvas.enabled = false;
            }

            if (_rawImage != null)
            {
                _rawImage.texture = null;
            }

            if (_player != null)
            {
                _player.loopPointReached -= OnLoopPointReached;
                _player.errorReceived -= OnErrorReceived;
                _player.Stop();
                Destroy(_player);
                _player = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_frameRateOverridden)
            {
                Application.targetFrameRate = _originalTargetFrameRate;
                Debug.Log($"[VideoCompletionOverlay] Hide：targetFrameRate 恢复到 {_originalTargetFrameRate}");
                _frameRateOverridden = false;
            }

            RestoreMonitorIfMoved();
            RestoreClickThroughAfterVideo();
            RestoreUIRoots();
            ClearStaleInputFocus();

            // 广播给 PanelView 强制重算 hidden：视频期间窗口可能失焦，PanelView 的
            // RefreshVisibility 依赖 IsAppFocused/AnyPinned 等 BindableProperty 变化触发；
            // 若这些值在视频前后没变化，display=default 之后面板仍会挂着 pp-hidden。
            this.SendEvent(new E_VideoOverlayClosed());
        }

        /// <summary>
        /// 视频播放期间禁用 UniWindowController 的点击穿透：
        /// 整个 RawImage 铺满屏幕、画面像素也包含半透明区域，若继续按 Opacity 自动判定，
        /// 透明像素会被穿透到桌面，导致用户无法点击 Skip 关闭视频。直接禁掉自动判定并把
        /// isClickThrough 强制为 false，Hide 时再恢复原值（默认 true，恢复 alpha 自动判定）。
        /// </summary>
        private void DisableClickThroughForVideo()
        {
            if (_uwc == null)
            {
                _uwc = FindAnyObjectByType<UniWindowController>();
            }

            if (_uwc == null)
            {
                Debug.Log("[VideoCompletionOverlay] DisableClickThrough：未找到 UniWindowController，跳过");
                return;
            }

            _hitTestEnabledBeforeVideo = _uwc.isHitTestEnabled;
            _clickThroughBeforeVideo = _uwc.isClickThrough;
            _uwc.isHitTestEnabled = false;
            _uwc.isClickThrough = false;
            Debug.Log(
                $"[VideoCompletionOverlay] DisableClickThrough：isHitTestEnabled {_hitTestEnabledBeforeVideo} -> false，isClickThrough {_clickThroughBeforeVideo} -> false");
        }

        private void RestoreClickThroughAfterVideo()
        {
            // 两个 nullable 标志在 Disable 入口同时被写、在这里同时被清，所以以 isHitTestEnabled 那条为闸门即可。
            if (!_hitTestEnabledBeforeVideo.HasValue) return;

            bool restoreHitTest = _hitTestEnabledBeforeVideo.Value;
            bool restoreClickThrough = _clickThroughBeforeVideo ?? false;
            _hitTestEnabledBeforeVideo = null;
            _clickThroughBeforeVideo = null;

            if (_uwc == null) return;
            _uwc.isHitTestEnabled = restoreHitTest;
            _uwc.isClickThrough = restoreClickThrough;
            Debug.Log(
                $"[VideoCompletionOverlay] RestoreClickThrough：isHitTestEnabled→{restoreHitTest}，isClickThrough→{restoreClickThrough}");
        }

        /// <summary>
        /// 视频播放期间用户点击的目标可能是覆盖层 UGUI Skip Button 或被 display=None 的 UIDocument 焦点元素，
        /// Hide/RestoreUIRoots 之后这些焦点持有者已经不接受输入，但 EventSystem.currentSelectedGameObject
        /// 和 UI Toolkit 各 panel 的 focusController.focusedElement 仍可能停在旧引用上，造成下一次用户点击
        /// 番茄钟设置面板的 TextField 时键盘事件被错误路由。这里显式清空两侧的"残留焦点"，让下一次点击
        /// 进入干净的新焦点获取流程。
        /// </summary>
        private void ClearStaleInputFocus()
        {
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null)
            {
                Debug.Log(
                    $"[VideoCompletionOverlay] ClearStaleInputFocus：清掉 EventSystem.selected={es.currentSelectedGameObject.name}");
                es.SetSelectedGameObject(null);
            }

            UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < docs.Length; i++)
            {
                UIDocument doc = docs[i];
                if (doc == null) continue;
                VisualElement root = doc.rootVisualElement;
                if (root == null || root.panel == null) continue;
                FocusController fc = root.focusController;
                if (fc == null) continue;
                Focusable focused = fc.focusedElement;
                if (focused == null) continue;
                Debug.Log(
                    $"[VideoCompletionOverlay] ClearStaleInputFocus：Blur '{doc.name}' 上的 focusedElement={focused.GetType().Name}");
                if (focused is VisualElement ve)
                {
                    ve.Blur();
                }
            }
        }

        private void SwitchToForegroundMonitorIfNeeded()
        {
            int targetIndex = NativeFilePicker.TryGetFrontmostWindowScreenIndex();
            if (targetIndex < 0)
            {
                Debug.Log("[VideoCompletionOverlay] SwitchToForegroundMonitor：原生层返回 -1（前台是自己 / 未授权 / 非 macOS），跳过切屏");
                return;
            }

            int monitorCount = UniWindowController.GetMonitorCount();
            if (targetIndex >= monitorCount)
            {
                Debug.LogWarning(
                    $"[VideoCompletionOverlay] SwitchToForegroundMonitor：原生返回 index={targetIndex} 越界 (UniWindow monitorCount={monitorCount})，跳过切屏");
                return;
            }

            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            int currentIndex = model.TargetMonitorIndex.Value;
            if (currentIndex == targetIndex)
            {
                Debug.Log($"[VideoCompletionOverlay] SwitchToForegroundMonitor：当前已在 monitor={currentIndex}，与前台一致，无需切屏");
                return;
            }

            IWindowPositionSystem winSys = this.GetSystem<IWindowPositionSystem>();
            // 用 Preview 不写 model.TargetMonitorIndex —— 视频结束后还要恢复，
            // 期间不能污染用户的持久化偏好。
            winSys.PreviewMoveToMonitor(targetIndex);
            _monitorIndexBeforeVideo = currentIndex;
            Debug.Log(
                $"[VideoCompletionOverlay] SwitchToForegroundMonitor：临时从 monitor={currentIndex} 切到前台 monitor={targetIndex}");
        }

        private void RestoreMonitorIfMoved()
        {
            if (!_monitorIndexBeforeVideo.HasValue)
            {
                return;
            }

            int restoreIndex = _monitorIndexBeforeVideo.Value;
            _monitorIndexBeforeVideo = null;

            try
            {
                IWindowPositionSystem winSys = this.GetSystem<IWindowPositionSystem>();
                winSys.PreviewMoveToMonitor(restoreIndex);
                Debug.Log($"[VideoCompletionOverlay] RestoreMonitor：恢复到原 monitor={restoreIndex}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VideoCompletionOverlay] RestoreMonitor 失败：{ex}");
            }
        }

        private void HideUIRoots()
        {
            if (_uiRootsToHide == null || _uiRootsToHide.Length == 0)
            {
                Debug.Log("[VideoCompletionOverlay] HideUIRoots：未配置要隐藏的 UI 根节点");
                return;
            }

            _hiddenRootStates = new List<HiddenRootState>(_uiRootsToHide.Length);
            foreach (GameObject root in _uiRootsToHide)
            {
                if (root == null)
                {
                    continue;
                }

                UIDocument doc = root.GetComponent<UIDocument>();
                VisualElement element = doc != null ? doc.rootVisualElement : null;

                if (element != null)
                {
                    HiddenRootState state = new HiddenRootState
                    {
                        Root = root,
                        Element = element,
                        OriginalDisplay = element.style.display,
                        OriginalActive = root.activeSelf,
                    };
                    element.style.display = DisplayStyle.None;
                    _hiddenRootStates.Add(state);
                    Debug.Log(
                        $"[VideoCompletionOverlay] HideUIRoots：'{root.name}' 走 UIDocument display=None（原 display={state.OriginalDisplay.value}）");
                }
                else
                {
                    HiddenRootState state = new HiddenRootState
                    {
                        Root = root,
                        Element = null,
                        OriginalActive = root.activeSelf,
                    };
                    root.SetActive(false);
                    _hiddenRootStates.Add(state);
                    Debug.Log(
                        $"[VideoCompletionOverlay] HideUIRoots：'{root.name}' 无 UIDocument，回退 SetActive(false)（原 active={state.OriginalActive}）");
                }
            }
        }

        private void RestoreUIRoots()
        {
            if (_hiddenRootStates == null)
            {
                return;
            }

            foreach (HiddenRootState state in _hiddenRootStates)
            {
                if (state.Root == null)
                {
                    continue;
                }

                if (state.Element != null)
                {
                    state.Element.style.display = state.OriginalDisplay;
                    Debug.Log(
                        $"[VideoCompletionOverlay] RestoreUIRoots：恢复 '{state.Root.name}' display={state.OriginalDisplay.value}");
                }
                else
                {
                    state.Root.SetActive(state.OriginalActive);
                    Debug.Log(
                        $"[VideoCompletionOverlay] RestoreUIRoots：恢复 '{state.Root.name}' active={state.OriginalActive}");
                }
            }

            _hiddenRootStates = null;
        }

        private void OnDestroy()
        {
            Hide();
        }
    }
}