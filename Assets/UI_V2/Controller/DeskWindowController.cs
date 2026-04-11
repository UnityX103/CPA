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
        // ─── Inspector 引用 ──────────────────────────────────────
        [Header("配置表")]
        [SerializeField] private PomodoroConfig _config;

        [Header("UniWindowController（可留空，运行时自动查找）")]
        [SerializeField] private UniWindowController _uwc;

        [Header("番茄钟子面板视图")]
        [SerializeField] private PomodoroPanelView _pomodoroPanelView;

        [Header("玩家卡片 UXML 模板")]
        [SerializeField] private VisualTreeAsset _playerCardTemplate;

        [Header("设置面板 UXML 模板")]
        [SerializeField] private VisualTreeAsset _pomodoroSettingsTemplate;
        [SerializeField] private VisualTreeAsset _onlineSettingsTemplate;
        [SerializeField] private VisualTreeAsset _petSettingsTemplate;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument     _uiDocument;
        private IPomodoroModel _model;
        private PlayerCardManager _playerCardManager;
        private UnifiedSettingsPanelController _settingsPanel;

        // 主容器元素
        private VisualElement    _dwWrap;
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
                _pomodoroPanelView.SetVisible(true);
            }
            else if (_pomodoroPanelView != null)
            {
                Debug.LogError("[DeskWindowController] 未找到 pomodoro-panel 节点，番茄钟面板无法初始化。");
            }

        }

        private void Update()
        {
            this.SendCommand(new Cmd_PomodoroTick(Time.deltaTime));
            this.GetSystem<IStateSyncSystem>().Tick(Time.unscaledDeltaTime);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                this.SendCommand(new Cmd_PomodoroRevertTopmost());
            }
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

            _dwWrap = root.Q<VisualElement>("dw-wrap");
            _pomodoroPanelContainer = root.Q<TemplateContainer>("pomodoro-panel");

            // 拖拽手柄
            var dragHandle = root.Q<Label>("drag-handle");
            if (dragHandle != null && _dwWrap != null)
            {
                DraggableElement.MakeDraggable(_dwWrap, dragHandle);
            }

            // 统一设置面板
            var roomModel = this.GetModel<APP.Network.Model.IRoomModel>();
            _settingsPanel = new UnifiedSettingsPanelController();
            _settingsPanel.Init(
                root,
                _model,
                roomModel,
                _pomodoroSettingsTemplate,
                _onlineSettingsTemplate,
                _petSettingsTemplate,
                gameObject);

            // 多人番茄钟：卡片管理器（嵌入 card-list ScrollView）
            _playerCardManager = new PlayerCardManager();
            var cardListScrollView = root.Q<ScrollView>("card-list");
            _playerCardManager.Initialize(_playerCardTemplate, cardListScrollView.contentContainer, gameObject);

            // 齿轮按钮切换设置面板
            root.Q("settings-btn")?.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (_settingsPanel.IsVisible)
                    _settingsPanel.Hide();
                else
                    _settingsPanel.Show();
            });
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
            _model.IsTopmost.Register(_ => SaveState(false))
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
