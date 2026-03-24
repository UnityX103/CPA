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
    /// 包含：菜单显隐、覆盖面板切换、番茄钟面板可见性管理。
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

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument     _uiDocument;
        private IPomodoroModel _model;

        // 主容器元素
        private VisualElement    _dwWrap;
        private VisualElement    _panelOverlay;
        private TemplateContainer _pomodoroPanelContainer;

        // 设置子面板
        private TemplateContainer _panelPomodoroS;
        private TemplateContainer _panelOnlineS;
        private TemplateContainer _panelPetS;

        // 覆盖层关闭按钮
        private Button _overlayClose;

        // 菜单按钮
        private Button _btnPomodoro;
        private Button _btnOnline;
        private Button _btnPet;


        // ─── 覆盖面板枚举 ────────────────────────────────────────
        private enum ActivePanel
        {
            Pomodoro,
            Online,
            Pet,
        }

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
                // 默认可见但收纳
                _pomodoroPanelView.SetVisible(true);
            }
            else if (_pomodoroPanelView != null)
            {
                Debug.LogError("[DeskWindowController] 未找到 pomodoro-panel 节点，番茄钟面板无法初始化。请检查 DeskWindow.uxml 中是否存在 name=\"pomodoro-panel\" 的 Instance。");
            }
        }

        private void Update()
        {
            this.SendCommand(new Cmd_PomodoroTick(Time.deltaTime));
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

            // 1. 将 root 定位到右下角（全屏透明窗口坐标系）
            root.style.justifyContent = Justify.FlexEnd;
            root.style.alignItems     = Align.FlexEnd;

            // 2. 查找关键元素
            _dwWrap       = root.Q<VisualElement>("dw-wrap");
            _panelOverlay = root.Q<VisualElement>("panel-overlay");

            // 番茄钟面板 TemplateContainer（PomodoroPanel.uxml 实例）
            _pomodoroPanelContainer = root.Q<TemplateContainer>("pomodoro-panel");

            // 各设置子面板
            _panelPomodoroS = root.Q<TemplateContainer>("panel-pomodoro-s");
            _panelOnlineS   = root.Q<TemplateContainer>("panel-online-s");
            _panelPetS      = root.Q<TemplateContainer>("panel-pet-s");

            // 覆盖层关闭按钮
            _overlayClose = root.Q<Button>("overlay-close");

            // 菜单按钮
            _btnPomodoro = root.Q<Button>("btn-pomodoro");
            _btnOnline   = root.Q<Button>("btn-online");
            _btnPet      = root.Q<Button>("btn-pet");

            // 3. 初始状态
            _panelOverlay?.AddToClassList("hidden");    // 覆盖层默认隐藏

            // 4. 点击 dw-wrap 外部 → 收纳番茄钟面板
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                bool clickInDwWrap = _dwWrap != null && _dwWrap.worldBound.Contains(evt.position);
                if (!clickInDwWrap)
                {
                    _pomodoroPanelView?.Collapse();
                }
            }, TrickleDown.TrickleDown);

            // 覆盖层关闭按钮
            _overlayClose?.RegisterCallback<PointerUpEvent>(_ => HideOverlay());

            // 7. 菜单按钮事件
            _btnPomodoro?.RegisterCallback<PointerUpEvent>(_ => ShowOverlayPanel(ActivePanel.Pomodoro));
            _btnOnline?.RegisterCallback<PointerUpEvent>(_ => ShowOverlayPanel(ActivePanel.Online));
            _btnPet?.RegisterCallback<PointerUpEvent>(_ => ShowOverlayPanel(ActivePanel.Pet));
        }


        // ─── 覆盖面板控制 ────────────────────────────────────────

        private void ShowOverlayPanel(ActivePanel panel)
        {
            _panelOverlay?.RemoveFromClassList("hidden");

            // 根据目标面板显示对应子面板
            if (_panelPomodoroS != null)
            {
                _panelPomodoroS.style.display =
                    panel == ActivePanel.Pomodoro ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_panelOnlineS != null)
            {
                _panelOnlineS.style.display =
                    panel == ActivePanel.Online ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_panelPetS != null)
            {
                _panelPetS.style.display =
                    panel == ActivePanel.Pet ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // 确保覆盖层在最顶层
            _panelOverlay?.BringToFront();
        }

        private void HideOverlay()
        {
            _panelOverlay?.AddToClassList("hidden");
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
