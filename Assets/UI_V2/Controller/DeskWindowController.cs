using APP.Network.Command;
using APP.Network.System;
using APP.Pomodoro.Command;
using APP.Pomodoro.Config;
using APP.Pomodoro.Model;
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
        private const string PlayerCardTemplatePath = "Assets/UI_V2/Documents/PlayerCard.uxml";

        // ─── Inspector 引用 ──────────────────────────────────────
        [Header("配置表")]
        [SerializeField] private PomodoroConfig _config;

        [Header("UniWindowController（可留空，运行时自动查找）")]
        [SerializeField] private UniWindowController _uwc;

        [Header("番茄钟子面板视图")]
        [SerializeField] private PomodoroPanelView _pomodoroPanelView;

        [Header("玩家卡片 UXML 模板")]
        [SerializeField] private VisualTreeAsset _playerCardTemplate;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument     _uiDocument;
        private IPomodoroModel _model;
        private PlayerCardManager _playerCardManager;

        // 主容器元素
        private TemplateContainer _pomodoroPanelContainer;

        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── Unity 生命周期 ──────────────────────────────────────

        private void Awake()
        {
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
            this.SendCommand(new Cmd_PomodoroTick(Time.deltaTime));
            this.GetSystem<IActiveAppSystem>().Tick(Time.unscaledDeltaTime);
            this.GetSystem<IStateSyncSystem>().Tick(Time.unscaledDeltaTime);
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

        // ─── UI 绑定 ─────────────────────────────────────────────

        private void BindUI()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            root.AddToClassList("dw-root-anchor");

            _pomodoroPanelContainer = root.Q<TemplateContainer>("pomodoro-panel");
            var cardLayer = root.Q<VisualElement>("card-layer");

            // 玩家卡片管理器（挂在 #card-layer 上，绝对定位 + NextSlot 算法）
            _playerCardTemplate = EnsureEditorTemplateLoaded(
                _playerCardTemplate,
                PlayerCardTemplatePath,
                "PlayerCard.uxml");

            _playerCardManager = new PlayerCardManager();
            _playerCardManager.Initialize(_playerCardTemplate, cardLayer, gameObject);
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
