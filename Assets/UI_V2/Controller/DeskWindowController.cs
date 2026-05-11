using APP.Network.Command;
using APP.Network.System;
using APP.Pomodoro.Command;
using APP.Pomodoro.Config;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using APP.Settings.System;
using Kirurobo;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// DeskWindow 主控制器，负责整个桌面组件的 UI 交互逻辑。
    /// 包含：Tab 按钮切换独立设置面板、番茄钟面板可见性管理。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DeskWindowController : MonoBehaviour, IController
    {
        private const int LightweightQualityLevel = 1;
        private const int TargetFrameRate = 15;
        private const string PlayerCardTemplatePath = "Assets/UI_V2/Documents/PlayerCard.uxml";
        private const string InputCounterPanelTemplatePath = "Assets/UI_V2/Documents/InputCounterPanel.uxml";
        private const string KeyCounterPillTemplatePath = "Assets/UI_V2/Documents/Components/KeyCounterPill.uxml";

        // ─── Inspector 引用 ──────────────────────────────────────
        [Header("配置表")]
        [SerializeField] private PomodoroConfig _config;

        [Header("UniWindowController（可留空，运行时自动查找）")]
        [SerializeField] private UniWindowController _uwc;

        [Header("番茄钟子面板视图")]
        [SerializeField] private PomodoroPanelView _pomodoroPanelView;

        [Header("玩家卡片 UXML 模板")]
        [SerializeField] private VisualTreeAsset _playerCardTemplate;

        [Header("输入计数面板 UXML 模板")]
        [SerializeField] private VisualTreeAsset _inputCounterPanelTemplate;

        [Header("KeyCounterPill 子模板（运行时按 entry 克隆 pill）")]
        [SerializeField] private VisualTreeAsset _keyCounterPillTemplate;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument     _uiDocument;
        private IPomodoroModel _model;
        private IPomodoroTimerSystem _pomodoroTimerSystem;
        private IActiveAppSystem _activeAppSystem;
        private IStateSyncSystem _stateSyncSystem;
        private IBindingKeyCounterSystem _bindingKeyCounterSystem;
        private PlayerCardManager _playerCardManager;
        // 输入计数面板：全局唯一一个面板；entries.Count==0 时销毁、>0 时按需创建。
        private VisualElement _icpHostLayer;
        private VisualElement _icpContainer;
        private VisualElement _icpRoot;
        private InputCounterPanelController _icpController;

        // 主容器元素
        private TemplateContainer _pomodoroPanelContainer;

        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── Unity 生命周期 ──────────────────────────────────────

        private void Awake()
        {
            ConfigureRuntimePerformance();

            _uiDocument = GetComponent<UIDocument>();

            if (_uwc == null)
            {
                _uwc = FindAnyObjectByType<UniWindowController>();
            }
        }

        private void Start()
        {
            // 1. 初始化 Architecture（确保 Model/System 已注册）
            _ = GameApp.Interface;

            // 2. 发送初始化命令（写入配置默认值、绑定 UWC）
            this.SendCommand(new Cmd_PomodoroInitialize(_config, _uwc));

            // 3. 缓存 Model
            _model = this.GetModel<IPomodoroModel>();
            _pomodoroTimerSystem = this.GetSystem<IPomodoroTimerSystem>();
            _activeAppSystem = this.GetSystem<IActiveAppSystem>();
            _stateSyncSystem = this.GetSystem<IStateSyncSystem>();
            _bindingKeyCounterSystem = this.GetSystem<IBindingKeyCounterSystem>();

            // 4. 注册持久化回调
            RegisterPersistenceCallbacks();

            // 5. 绑定 UI 元素
            BindUI();

            // 6. 初始化番茄钟子面板
            if (_pomodoroPanelView != null && _pomodoroPanelContainer != null)
            {
                _pomodoroPanelView.Init(_pomodoroPanelContainer);
            }
            else if (_pomodoroPanelView != null)
            {
                Debug.LogError("[DeskWindowController] 未找到 pomodoro-panel 节点，番茄钟面板无法初始化。");
            }

            this.SendCommand(new Cmd_AutoReconnectOnStartup());
        }

        private void Update()
        {
            _pomodoroTimerSystem?.Tick(Time.deltaTime);
            _activeAppSystem?.Tick(Time.unscaledDeltaTime);
            _stateSyncSystem?.Tick(Time.unscaledDeltaTime);
            _bindingKeyCounterSystem?.Tick(Time.unscaledDeltaTime);
        }

        private static void ConfigureRuntimePerformance()
        {
            QualitySettings.vSyncCount = 0;

            if (QualitySettings.GetQualityLevel() > LightweightQualityLevel)
            {
                QualitySettings.SetQualityLevel(LightweightQualityLevel, true);
            }

            Application.targetFrameRate = TargetFrameRate;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 将 Unity 的应用焦点状态统一汇入 IGameModel.IsAppFocused，
            // 由 WindowVisibilityCoordinatorSystem + View 层公式驱动可见性。
            this.SendCommand(new Cmd_SetAppFocused(hasFocus));
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                PomodoroPersistence.Save(_model, true);
            }
        }

        private void OnApplicationQuit()
        {
            PomodoroPersistence.Save(_model, true);
        }

        private void OnDestroy()
        {
            // 在仍有 entries 的情况下销毁宿主时，主动释放 controller 订阅，防止 IActiveAppSystem.Changed
            // 持有已脱离面板的引用造成回调泄漏 / 重建后重复回调。
            DestroyInputCounterPanel();
        }

        // ─── UI 绑定 ─────────────────────────────────────────────

        private void BindUI()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            root.AddToClassList("dw-root-anchor");

            // Flash 态下，用户在应用内任意位置点击即退出 Flash，恢复原可见性/置顶策略
            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);

            _pomodoroPanelContainer = root.Q<TemplateContainer>("pomodoro-panel");
            var cardLayer = root.Q<VisualElement>("card-layer");

            // 玩家卡片管理器（挂在 #card-layer 上，绝对定位 + NextSlot 算法）
            _playerCardTemplate = EnsureEditorTemplateLoaded(
                _playerCardTemplate,
                PlayerCardTemplatePath,
                "PlayerCard.uxml");

            _playerCardManager = new PlayerCardManager();
            _playerCardManager.Initialize(_playerCardTemplate, cardLayer, gameObject);

            // 输入计数面板：全局唯一面板，entries.Count>0 时创建、==0 时销毁
            _inputCounterPanelTemplate = EnsureEditorTemplateLoaded(
                _inputCounterPanelTemplate,
                InputCounterPanelTemplatePath,
                "InputCounterPanel.uxml");
            _keyCounterPillTemplate = EnsureEditorTemplateLoaded(
                _keyCounterPillTemplate,
                KeyCounterPillTemplatePath,
                "KeyCounterPill.uxml");
            _icpHostLayer = cardLayer;
            if (_inputCounterPanelTemplate == null)
            {
                Debug.LogWarning("[DeskWindowController] InputCounterPanel.uxml 未能加载，面板不会显示。");
            }
            else
            {
                RebuildInputCounterPanels();
                var bindingModel = this.GetModel<APP.Settings.Model.IBindingKeyModel>();
                bindingModel.EntriesRevision.Register(_ => RebuildInputCounterPanels())
                    .UnRegisterWhenGameObjectDestroyed(gameObject);
                // binding.Enabled 不再驱动面板增删——面板存在与否只看 entries.Count，
                // Enabled 只影响 BindingKeyCounterSystem 的计数 tick。
            }
        }

        /// <summary>
        /// 同步 InputCounterPanel 状态到当前 Model：
        ///   entries.Count == 0 → 销毁面板（释放 DOM 节点 + 事件订阅）
        ///   entries.Count >  0 → 确保单一面板存在，把 pill 列表交给 controller 增量重建
        /// 不再为每个 entry 单独建一个面板；多绑定时复用同一个面板里的 pill-list。
        /// </summary>
        private void RebuildInputCounterPanels()
        {
            if (_inputCounterPanelTemplate == null || _icpHostLayer == null) return;

            var binding = this.GetModel<APP.Settings.Model.IBindingKeyModel>();
            int count = binding.Entries.Count;

            if (count == 0)
            {
                DestroyInputCounterPanel();
                return;
            }

            EnsureInputCounterPanel();
            _icpController?.SyncPillsFromEntries();
        }

        private void EnsureInputCounterPanel()
        {
            if (_icpContainer != null) return;
            var container = _inputCounterPanelTemplate.CloneTree();
            _icpHostLayer.Add(container);
            VisualElement icpRoot = container.Q<VisualElement>("icp-root") ?? container;
            icpRoot.style.left = 12;
            icpRoot.style.top  = 12;
            DraggableElement.MakeDraggable(icpRoot);

            var controller = new InputCounterPanelController();
            controller.Init(icpRoot, gameObject, _keyCounterPillTemplate);

            _icpContainer  = container;
            _icpRoot       = icpRoot;
            _icpController = controller;
        }

        private void DestroyInputCounterPanel()
        {
            _icpController?.Dispose();
            _icpController = null;
            if (_icpContainer != null && _icpContainer.parent == _icpHostLayer)
            {
                _icpHostLayer.Remove(_icpContainer);
            }
            _icpContainer = null;
            _icpRoot = null;
        }

        private void OnRootPointerDown(PointerDownEvent _)
        {
            // 不 StopPropagation：仍让子级按钮/拖拽等功能正常执行
            this.GetSystem<IPhaseTransitionFlashSystem>().Dismiss();
        }

        private static VisualTreeAsset EnsureEditorTemplateLoaded(
            VisualTreeAsset currentTemplate,
            string assetPath,
            string displayName)
        {
            if (currentTemplate != null)
            {
                return currentTemplate;
            }

#if UNITY_EDITOR
            currentTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
#endif

            if (currentTemplate == null)
            {
                Debug.LogWarning($"[DeskWindowController] {displayName} 未在 Inspector 赋值，且未能从 {assetPath} 自动加载。");
            }

            return currentTemplate;
        }

        // ─── 持久化 ──────────────────────────────────────────────

        private void RegisterPersistenceCallbacks()
        {
            if (_model == null)
            {
                return;
            }

            _model.FocusDurationSeconds.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.BreakDurationSeconds.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.TotalRounds.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CurrentRound.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.RemainingSeconds.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CurrentPhase.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.IsRunning.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.IsPinned.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.WindowAnchor.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.AutoJumpToTopOnComplete.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.AutoStartBreak.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.TargetMonitorIndex.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.CompletionClipIndex.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.EndActionMode.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            _model.EndActionVideoPath.Register(_ => SaveState(false))
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void SaveState(bool flushToDisk)
        {
            if (_model == null)
            {
                return;
            }

            PomodoroPersistence.Save(_model, flushToDisk);
        }
    }
}
