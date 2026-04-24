using APP.Pomodoro.Command;
using APP.Pomodoro.Config;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 番茄钟子面板视图控制器。
    /// 由 DeskWindowController.Start() 调用 Init() 完成 UI 绑定；
    /// 自身不在 Start() 中自动绑定。
    /// </summary>
    public sealed class PomodoroPanelView : MonoBehaviour, IController
    {
        // ─── Inspector 引用 ──────────────────────────────────────
        [Header("配置表")]
        [SerializeField] private PomodoroConfig _config;

        [Header("音效源（可留空，运行时自动查找）")]
        [SerializeField] private AudioSource _audioSource;

        // ─── UXML 元素引用 ────────────────────────────────────────
        private VisualElement _ppRoot;
        private Label         _ppStreakValue;
        private Button        _ppBtnPrimary;
        private Button        _ppBtnSecondary;
        private VisualElement _ppPinBtn;

        // ─── 子视图 ───────────────────────────────────────────────
        private ClockView _clockView;

        // ─── 缓存 ─────────────────────────────────────────────────
        private IPomodoroModel _model;
        private bool           _isInitialized;

        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── Unity 生命周期 ──────────────────────────────────────

        private void Awake()
        {
            // 若 Inspector 未赋值，在当前 GameObject 及其父节点上自动查找
            if (_audioSource == null)
            {
                _audioSource = GetComponentInParent<AudioSource>();
            }
        }

        // ─── 公开初始化入口 ──────────────────────────────────────

        /// <summary>
        /// 由 DeskWindowController 在 Start() 中调用，完成 UI 绑定与 Model 订阅。
        /// </summary>
        /// <param name="pomodoroTemplateContainer">
        /// DeskWindow.uxml 中 id="pomodoro-panel" 的 TemplateContainer 实例。
        /// </param>
        public void Init(VisualElement pomodoroTemplateContainer)
        {
            if (_isInitialized)
            {
                return;
            }

            if (pomodoroTemplateContainer == null)
            {
                Debug.LogError("[PomodoroPanelView] Init 传入的 pomodoroTemplateContainer 为 null！");
                return;
            }

            BindElements(pomodoroTemplateContainer);
            SubscribeModel();
            SubscribeEvents();

            // 位置 Model 订阅 → 写 style.left/top
            _model = _model ?? this.GetModel<IPomodoroModel>();
            _model.PomodoroPanelPosition.RegisterWithInitValue(OnPomodoroPositionChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            // 自身 GeometryChanged：仅用于首帧 sentinel → 计算默认位置。
            // 拖拽过程中 style.left/top 会频繁触发这里，务必不要在此覆盖正在拖拽的值。
            _ppRoot?.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            // 父容器 GeometryChanged：分辨率/窗口大小变化时按当前 ratio 重算像素位置。
            _ppRoot?.parent?.RegisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);

            _isInitialized = true;
        }

        // ─── 公开方法（供 DeskWindowController 调用）────────────

        /// <summary>显示或隐藏整个面板。</summary>
        /// <remarks>
        /// 仅切换 <c>pp-hidden</c> class，不直接写 inline style。
        /// 所有显示相关规则由 PomodoroPanel.uss 管理。
        /// </remarks>
        public void SetVisible(bool visible)
        {
            if (_ppRoot == null)
            {
                return;
            }

            _ppRoot.EnableInClassList("pp-hidden", !visible);
        }

        // ─── UI 绑定 ─────────────────────────────────────────────

        private void BindElements(VisualElement pomodoroTemplateContainer)
        {
            _ppRoot        = pomodoroTemplateContainer.Q<VisualElement>("pp-root");
            _ppStreakValue = pomodoroTemplateContainer.Q<Label>("pp-streak-value");
            _ppBtnPrimary   = pomodoroTemplateContainer.Q<Button>("pp-btn-primary");
            _ppBtnSecondary = pomodoroTemplateContainer.Q<Button>("pp-btn-secondary");
            // 查找 Clock TemplateContainer 并初始化 ClockView
            var ppClockContainer = pomodoroTemplateContainer.Q<TemplateContainer>("pp-clock");
            if (ppClockContainer != null)
            {
                _clockView = new ClockView(ppClockContainer);
                // Clock 初始化完成
            }
            else
            {
                Debug.LogWarning("[PomodoroPanelView] 未找到 pp-clock TemplateContainer，ClockView 未初始化。");
            }

            // 注册按钮事件
            _ppBtnPrimary?.RegisterCallback<PointerUpEvent>(_ => OnPrimaryButtonClicked());
            _ppBtnSecondary?.RegisterCallback<PointerUpEvent>(_ => OnSecondaryButtonClicked());

            // 整卡拖拽：以 pp-root 本身作为 handle，选中面板任意位置皆可拖动。
            // DragController 回传像素坐标，此处换算为父容器归一化比例后写入 Model。
            if (_ppRoot != null)
            {
                var dragController = DraggableElement.MakeDraggable(_ppRoot);
                dragController.OnDragEnd += pxPos =>
                {
                    Vector2 ratio = PixelToRatio(pxPos);
                    this.SendCommand(new Cmd_SetPomodoroPanelPosition(ratio));
                };
            }

            // 设置齿轮按钮：在 PointerDown 阻断冒泡，避免触发整卡拖拽
            var settingsBtn = pomodoroTemplateContainer.Q<VisualElement>("pp-settings-btn");
            if (settingsBtn != null)
            {
                settingsBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                settingsBtn.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    this.SendCommand(new Cmd_OpenUnifiedSettings());
                });
            }

            // 主/次操作按钮：在 PointerDown 阻断冒泡，避免点击按钮时误触发整卡拖拽
            _ppBtnPrimary?.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            _ppBtnSecondary?.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            _ppPinBtn = pomodoroTemplateContainer.Q<VisualElement>("pp-pin-btn");
            BindPinButton();
        }

        private void BindPinButton()
        {
            if (_ppPinBtn == null) return;
            _ppPinBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            _ppPinBtn.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                if (_model == null) return;
                bool next = !_model.IsPinned.Value;
                this.SendCommand(new Cmd_SetPomodoroPinned(next));
            });
        }

        // ─── Model 订阅 ──────────────────────────────────────────

        private void SubscribeModel()
        {
            _model = this.GetModel<IPomodoroModel>();

            if (_model == null)
            {
                Debug.LogError("[PomodoroPanelView] 无法获取 IPomodoroModel，请确认 Architecture 已初始化。");
                return;
            }

            _model.RemainingSeconds.RegisterWithInitValue(OnRemainingSecondsChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            _model.CurrentPhase.RegisterWithInitValue(OnCurrentPhaseChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            _model.CurrentRound.RegisterWithInitValue(OnCurrentRoundChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            _model.TotalRounds.RegisterWithInitValue(_ => OnCurrentRoundChanged(_model.CurrentRound.Value))
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            _model.IsRunning.RegisterWithInitValue(OnIsRunningChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            _model.AutoStartBreak.RegisterWithInitValue(_ => RefreshClock())
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            _model.IsPinned.RegisterWithInitValue(OnPomodoroPinnedChanged)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.GetModel<IGameModel>().IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility())
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned
                .RegisterWithInitValue(_ => RefreshVisibility())
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.GetSystem<IPhaseTransitionFlashSystem>().IsFlashing
                .RegisterWithInitValue(_ => RefreshVisibility())
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // ─── 事件订阅 ────────────────────────────────────────────

        private void SubscribeEvents()
        {
            this.RegisterEvent<E_PomodoroPhaseChanged>(OnPhaseChangedEvent)
                .UnRegisterWhenGameObjectDestroyed(gameObject);

            this.RegisterEvent<E_PomodoroCycleCompleted>(OnCycleCompletedEvent)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // ─── Model 变化回调 ──────────────────────────────────────

        private void OnRemainingSecondsChanged(int _)
        {
            RefreshClock();
        }

        private void OnCurrentPhaseChanged(PomodoroPhase _)
        {
            RefreshClock();
        }

        private void OnCurrentRoundChanged(int round)
        {
            if (_ppStreakValue == null || _model == null)
            {
                return;
            }

            _ppStreakValue.text = $"{round} / {_model.TotalRounds.Value}";
        }

        private void OnIsRunningChanged(bool running)
        {
            // 更新主按钮文本
            if (_ppBtnPrimary != null)
            {
                _ppBtnPrimary.text = running ? "暂停" : "开始";
            }

            RefreshClock();
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnPhaseChangedEvent(E_PomodoroPhaseChanged evt)
        {
            // 阶段切换时播放提示音效（非完成阶段不播放，完成阶段由 CycleCompleted 处理）
            if (evt.Phase == PomodoroPhase.Break)
            {
                PlayCompletionSound();
            }
        }

        private void OnCycleCompletedEvent(E_PomodoroCycleCompleted evt)
        {
            PlayCompletionSound();
        }

        // ─── 按钮回调 ────────────────────────────────────────────

        private void OnPrimaryButtonClicked()
        {
            if (_model == null)
            {
                return;
            }

            if (_model.IsRunning.Value)
            {
                this.SendCommand(new Cmd_PomodoroPause());
            }
            else
            {
                this.SendCommand(new Cmd_PomodoroStart());
            }
        }

        private void OnSecondaryButtonClicked()
        {
            this.SendCommand(new Cmd_PomodoroSkipCurrentPhase());
        }

        // ─── 时钟刷新 ────────────────────────────────────────────

        private void RefreshClock()
        {
            if (_clockView == null || _model == null)
            {
                return;
            }

            ClockDisplayState displayState = ClockView.ResolveState(
                _model.CurrentPhase.Value,
                _model.IsRunning.Value);

            int phaseTotalSeconds = _model.CurrentPhase.Value == PomodoroPhase.Focus
                ? _model.FocusDurationSeconds.Value
                : _model.BreakDurationSeconds.Value;

            _clockView.Refresh(displayState, _model.RemainingSeconds.Value, phaseTotalSeconds);
        }

        // ─── 音效播放 ────────────────────────────────────────────

        private void PlayCompletionSound()
        {
            if (_config == null || _audioSource == null || _model == null)
            {
                return;
            }

            AudioClip clip = _config.GetCompletionClip(_model.CompletionClipIndex.Value);
            if (clip == null)
            {
                return;
            }

            _audioSource.PlayOneShot(clip, _config.CompletionVolume);
        }

        // ─── 位置持久化 ──────────────────────────────────────────

        private const float DefaultMarginPx = 20f;

        private void OnPomodoroPositionChanged(Vector2 ratio)
        {
            if (_ppRoot == null) return;
            // sentinel / 脏值（NaN、±Infinity）：等 GeometryChanged 算默认位置，不写 style
            if (!float.IsFinite(ratio.x) || !float.IsFinite(ratio.y))
            {
                return;
            }
            ApplyRatioToStyle(ratio);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent _)
        {
            if (_model == null || _ppRoot == null) return;

            var current = _model.PomodoroPanelPosition.Value;
            // 已有合法持久化 ratio：不在此处重算像素。
            // 拖拽期间每次 style.left/top 变化都会触发本回调，若用 Model 的旧 ratio 覆盖 style，
            // 将把拖拽位置立刻吃掉。分辨率变化由 OnParentGeometryChanged 处理；
            // Model 自身变化由 OnPomodoroPositionChanged 处理。
            // NaN/±Infinity 视为 sentinel，需要走默认右下角计算。
            if (float.IsFinite(current.x) && float.IsFinite(current.y)) return;

            var parentLayout = _ppRoot.parent?.layout ?? _ppRoot.layout;
            if (parentLayout.width <= 0 || parentLayout.height <= 0) return;
            if (_ppRoot.layout.width <= 0 || _ppRoot.layout.height <= 0) return;

            // 首帧 sentinel：屏幕右下角默认（距右/下各 DefaultMarginPx），换算成归一化比例后回写
            float pxX = Mathf.Max(0f, parentLayout.width  - _ppRoot.layout.width  - DefaultMarginPx);
            float pxY = Mathf.Max(0f, parentLayout.height - _ppRoot.layout.height - DefaultMarginPx);
            Vector2 ratio = PixelToRatio(new Vector2(pxX, pxY), parentLayout);
            this.SendCommand(new Cmd_SetPomodoroPanelPosition(ratio));
        }

        private void OnParentGeometryChanged(GeometryChangedEvent _)
        {
            if (_model == null || _ppRoot == null) return;
            var current = _model.PomodoroPanelPosition.Value;
            // sentinel 或脏值（NaN/±Infinity）时不写 style，留给 OnRootGeometryChanged 算默认
            if (!float.IsFinite(current.x) || !float.IsFinite(current.y)) return;
            // 父容器尺寸变化（换分辨率/改窗口大小等）时重算像素位置
            ApplyRatioToStyle(current);
        }

        /// <summary>
        /// 将归一化比例换算成像素并写入 style.left/top；
        /// 对面板自身宽高做 clamp，避免超出父容器右/下边。
        /// </summary>
        private void ApplyRatioToStyle(Vector2 ratio)
        {
            // 最后一道防线：任何非有限 ratio 一律拒写，防止 NaN 污染 style 并被 DraggableElement 读回。
            if (!float.IsFinite(ratio.x) || !float.IsFinite(ratio.y)) return;
            var parentLayout = _ppRoot.parent?.layout ?? _ppRoot.layout;
            if (parentLayout.width <= 0 || parentLayout.height <= 0) return;

            float rx = Mathf.Clamp01(ratio.x);
            float ry = Mathf.Clamp01(ratio.y);

            float targetW = _ppRoot.layout.width;
            float targetH = _ppRoot.layout.height;
            float maxLeft = Mathf.Max(0f, parentLayout.width  - targetW);
            float maxTop  = Mathf.Max(0f, parentLayout.height - targetH);

            float px = Mathf.Clamp(rx * parentLayout.width,  0f, maxLeft);
            float py = Mathf.Clamp(ry * parentLayout.height, 0f, maxTop);

            _ppRoot.style.left = px;
            _ppRoot.style.top  = py;
        }

        private Vector2 PixelToRatio(Vector2 pxPos)
        {
            var parentLayout = _ppRoot?.parent?.layout ?? default;
            return PixelToRatio(pxPos, parentLayout);
        }

        private static Vector2 PixelToRatio(Vector2 pxPos, Rect parentLayout)
        {
            float rx = parentLayout.width  > 0 ? Mathf.Clamp01(pxPos.x / parentLayout.width)  : 0f;
            float ry = parentLayout.height > 0 ? Mathf.Clamp01(pxPos.y / parentLayout.height) : 0f;
            return new Vector2(rx, ry);
        }

        // ─── Pin 状态与可见性 ────────────────────────────────────

        private void OnPomodoroPinnedChanged(bool pinned)
        {
            _ppPinBtn?.EnableInClassList("pp-pin-btn--unpinned", !pinned);
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (_ppRoot == null || _model == null) return;
            bool focused = this.GetModel<IGameModel>().IsAppFocused.Value;
            bool anyPinned = this.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned.Value;
            bool thisPinned = _model.IsPinned.Value;
            bool flashing = this.GetSystem<IPhaseTransitionFlashSystem>().IsFlashing.Value;
            // S2 隐藏条件：整窗口置顶(AnyPinned) 且失焦 且本 UI 非 pinned；
            // Flash 状态强制显示（覆盖上述隐藏规则）
            bool hidden = !thisPinned && !focused && anyPinned && !flashing;
            _ppRoot.EnableInClassList("pp-hidden", hidden);
        }
    }
}
