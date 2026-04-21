using APP.Network.Model;
using APP.Pomodoro.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 统一设置面板控制器（纯 C# 类）。
    /// 管理 DeskWindow.uxml 中 settings-overlay 的显隐、侧边栏 tab 切换、内容区加载。
    /// 持有并初始化三个设置面板子控制器。
    /// </summary>
    public sealed class UnifiedSettingsPanelController
    {
        // ─── UI 元素 ─────────────────────────────────────────────
        private VisualElement _overlay;
        private VisualElement _contentHost;
        private VisualElement _tabPomodoro;
        private VisualElement _tabOnline;
        private VisualElement _tabPet;
        private VisualElement _closeBtn;

        // ─── 状态 ────────────────────────────────────────────────
        private string _activeTab = "pomodoro";

        // ─── 子控制器 ────────────────────────────────────────────
        private PomodoroSettingsPanelController _pomodoroSettings;
        private OnlineSettingsPanelController _onlineSettings;
        private PetSettingsPanelController _petSettings;

        // ─── 模板与缓存实例 ──────────────────────────────────────
        private VisualTreeAsset _pomodoroTemplate;
        private VisualTreeAsset _onlineTemplate;
        private VisualTreeAsset _petTemplate;
        private IPomodoroModel _model;
        private IRoomModel _roomModel;
        private GameObject _lifecycleOwner;
        private VisualElement _pomodoroRoot;
        private VisualElement _onlineRoot;
        private VisualElement _petRoot;

        // ─── 公开属性 ────────────────────────────────────────────
        public bool IsVisible => _overlay != null && _overlay.resolvedStyle.display == DisplayStyle.Flex;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 初始化统一设置面板。
        /// </summary>
        /// <param name="root">DeskWindow 的 rootVisualElement</param>
        /// <param name="model">番茄钟 Model</param>
        /// <param name="roomModel">房间 Model</param>
        /// <param name="pomodoroTemplate">PomodoroSettingsPanel.uxml 资源</param>
        /// <param name="onlineTemplate">OnlineSettingsPanel.uxml 资源</param>
        /// <param name="petTemplate">PetSettingsPanel.uxml 资源</param>
        /// <param name="lifecycleOwner">事件解绑生命周期宿主 GameObject</param>
        public void Init(
            VisualElement root,
            IPomodoroModel model,
            IRoomModel roomModel,
            VisualTreeAsset pomodoroTemplate,
            VisualTreeAsset onlineTemplate,
            VisualTreeAsset petTemplate,
            GameObject lifecycleOwner)
        {
            _model = model;
            _roomModel = roomModel;
            _pomodoroTemplate = pomodoroTemplate;
            _onlineTemplate = onlineTemplate;
            _petTemplate = petTemplate;
            _lifecycleOwner = lifecycleOwner;

            // 查询 overlay 及子元素
            _overlay = root.Q("settings-overlay");
            _contentHost = root.Q("settings-content-host");
            _tabPomodoro = root.Q("tab-pomodoro");
            _tabOnline = root.Q("tab-online");
            _tabPet = root.Q("tab-pet");
            _closeBtn = root.Q("settings-close");

            // 注册关闭与 tab 切换事件
            _closeBtn?.RegisterCallback<PointerUpEvent>(_ => Hide());
            _tabPomodoro?.RegisterCallback<PointerUpEvent>(_ => SelectTab("pomodoro"));
            _tabOnline?.RegisterCallback<PointerUpEvent>(_ => SelectTab("online"));
            _tabPet?.RegisterCallback<PointerUpEvent>(_ => SelectTab("pet"));

            SelectTab(_activeTab);
        }

        // ─── 显隐控制 ────────────────────────────────────────────

        public void Show()
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.style.display = DisplayStyle.Flex;
            SelectTab(_activeTab);
        }

        public void Hide()
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.style.display = DisplayStyle.None;
        }

        // ─── Tab 切换 ────────────────────────────────────────────

        private void SelectTab(string tabName)
        {
            _activeTab = tabName;

            // 更新 sidebar-tab--active CSS 类
            _tabPomodoro?.EnableInClassList("sidebar-tab--active", tabName == "pomodoro");
            _tabOnline?.EnableInClassList("sidebar-tab--active", tabName == "online");
            _tabPet?.EnableInClassList("sidebar-tab--active", tabName == "pet");

            VisualElement content = EnsureTabContent(tabName);

            if (_contentHost != null && content != null)
            {
                _contentHost.Clear();
                _contentHost.Add(content);
            }

            // 刷新当前激活面板的数据
            switch (tabName)
            {
                case "pomodoro":
                    _pomodoroSettings?.RefreshFromModel();
                    break;
                case "online":
                    _onlineSettings?.RefreshCardState();
                    break;
            }
        }

        private VisualElement EnsureTabContent(string tabName)
        {
            switch (tabName)
            {
                case "online":
                    if (_onlineRoot == null)
                    {
                        _onlineRoot = CloneTemplate(_onlineTemplate);
                        _onlineSettings = new OnlineSettingsPanelController();
                        _onlineSettings.Init(_onlineRoot, _roomModel, _lifecycleOwner);
                    }

                    return _onlineRoot;

                case "pet":
                    if (_petRoot == null)
                    {
                        _petRoot = CloneTemplate(_petTemplate);
                        _petSettings = new PetSettingsPanelController();
                        _petSettings.Init(_petRoot, _lifecycleOwner);
                    }

                    return _petRoot;

                case "pomodoro":
                default:
                    if (_pomodoroRoot == null)
                    {
                        _pomodoroRoot = CloneTemplate(_pomodoroTemplate);
                        _pomodoroSettings = new PomodoroSettingsPanelController();
                        _pomodoroSettings.Init(_pomodoroRoot, _model, _lifecycleOwner);
                    }

                    return _pomodoroRoot;
            }
        }

        private static VisualElement CloneTemplate(VisualTreeAsset template)
        {
            return template?.CloneTree();
        }
    }
}
