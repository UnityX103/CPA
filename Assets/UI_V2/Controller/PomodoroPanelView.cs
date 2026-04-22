using APP.Pomodoro.Command;
using APP.Pomodoro.Config;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
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

            _ppRoot?.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

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

            // handleBar 拖拽 + 设置按钮
            var handleBar = pomodoroTemplateContainer.Q<VisualElement>("pp-handle-bar");
            var settingsBtn = pomodoroTemplateContainer.Q<VisualElement>("pp-settings-btn");
            if (_ppRoot != null && handleBar != null)
            {
                var dragController = DraggableElement.MakeDraggable(_ppRoot, handleBar);
                dragController.OnDragEnd += pos =>
                    this.SendCommand(new Cmd_SetPomodoroPanelPosition(pos));
            }
            settingsBtn?.RegisterCallback<PointerUpEvent>(_ =>
                this.SendCommand(new Cmd_OpenUnifiedSettings()));
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

        private void OnPomodoroPositionChanged(Vector2 pos)
        {
            if (_ppRoot == null) return;
            if (float.IsNegativeInfinity(pos.x) || float.IsNegativeInfinity(pos.y))
            {
                return; // sentinel：等 GeometryChanged 算默认位置
            }
            _ppRoot.style.left = pos.x;
            _ppRoot.style.top  = pos.y;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent _)
        {
            if (_model == null || _ppRoot == null) return;
            var current = _model.PomodoroPanelPosition.Value;
            if (!float.IsNegativeInfinity(current.x) && !float.IsNegativeInfinity(current.y))
                return; // 已有持久化值，不覆盖

            var parentLayout = _ppRoot.parent?.layout ?? _ppRoot.layout;
            if (parentLayout.width <= 0 || parentLayout.height <= 0) return;
            if (_ppRoot.layout.width <= 0 || _ppRoot.layout.height <= 0) return;

            // Q6a=B: 屏幕右下角默认（距右/下各 20px）
            float x = parentLayout.width  - _ppRoot.layout.width  - 20f;
            float y = parentLayout.height - _ppRoot.layout.height - 20f;
            x = Mathf.Max(0, x);
            y = Mathf.Max(0, y);
            this.SendCommand(new Cmd_SetPomodoroPanelPosition(new Vector2(x, y)));
        }
    }
}
