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

        [Header("玩家卡片预制体（多人番茄钟，含 UIDocument + PlayerCardController）")]
        [SerializeField] private GameObject _playerCardPrefab;

        [Header("远端玩家容器（运行时实例化 PlayerCard 的父物体）")]
        [SerializeField] private Transform _remotePlayerContainer;

        [Header("设置面板（独立 UIDocument）")]
        [SerializeField] private PomodoroSettingsPanelController _pomodoroSettingsPanel;
        [SerializeField] private OnlineSettingsPanelController _onlineSettingsPanel;
        [SerializeField] private PetSettingsPanelController _petSettingsPanel;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument     _uiDocument;
        private IPomodoroModel _model;
        private PlayerCardManager _playerCardManager;

        // 主容器元素
        private VisualElement    _dwWrap;
        private TemplateContainer _pomodoroPanelContainer;

        // Tab 按钮
        private Button _btnPomodoro;
        private Button _btnOnline;
        private Button _btnPet;

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

            // 7. 多人番茄钟：卡片管理器（每个远程玩家一个独立 UIDocument 预制体）
            _playerCardManager = new PlayerCardManager();
            _playerCardManager.Initialize(_playerCardPrefab, _remotePlayerContainer, gameObject);
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

            // 拖拽手柄：需要等布局完成后，将 flex 定位转为 absolute 定位
            var dragHandle = root.Q<Label>("drag-handle");
            if (dragHandle != null && _dwWrap != null)
            {
                _dwWrap.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    if (_dwWrap.resolvedStyle.position == Position.Absolute)
                        return;

                    // 读取 flex 布局计算出的实际位置
                    var parentBound = _dwWrap.parent.worldBound;
                    var wrapBound = _dwWrap.worldBound;
                    float left = wrapBound.x - parentBound.x;
                    float top = wrapBound.y - parentBound.y;

                    // 切换为 absolute 定位，保持视觉位置不变
                    _dwWrap.style.position = Position.Absolute;
                    _dwWrap.style.left = left;
                    _dwWrap.style.top = top;

                    DraggableElement.MakeDraggable(_dwWrap, dragHandle);
                });
            }

            // Tab 按钮
            _btnPomodoro = root.Q<Button>("btn-pomodoro");
            _btnOnline   = root.Q<Button>("btn-online");
            _btnPet      = root.Q<Button>("btn-pet");

            // 点击 dw-wrap 外部的处理（玩家卡片已独立为单独 UIDocument，无需白名单）
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                bool clickInDwWrap = _dwWrap != null && _dwWrap.worldBound.Contains(evt.position);
                if (!clickInDwWrap)
                {
                    // 点击面板外部（已移除收缩逻辑）
                }
            });

            // Tab 按钮事件 → 切换独立 UIDocument 面板
            _btnPomodoro?.RegisterCallback<PointerUpEvent>(_ => TogglePanel(_pomodoroSettingsPanel));
            _btnOnline?.RegisterCallback<PointerUpEvent>(_ => TogglePanel(_onlineSettingsPanel));
            _btnPet?.RegisterCallback<PointerUpEvent>(_ => TogglePanel(_petSettingsPanel));
        }

        // ─── 面板切换 ────────────────────────────────────────────

        private void TogglePanel(ISettingsPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            if (panel.IsVisible)
            {
                panel.Hide();
                return;
            }

            HideAllPanels();
            panel.Show();
        }

        private void HideAllPanels()
        {
            _pomodoroSettingsPanel?.Hide();
            _onlineSettingsPanel?.Hide();
            _petSettingsPanel?.Hide();
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
