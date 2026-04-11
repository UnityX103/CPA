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
        private VisualElement _contentPomodoro;
        private VisualElement _contentOnline;
        private VisualElement _contentPet;
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
            // 查询 overlay 及子元素
            _overlay         = root.Q("settings-overlay");
            _contentPomodoro = root.Q("content-pomodoro");
            _contentOnline   = root.Q("content-online");
            _contentPet      = root.Q("content-pet");
            _tabPomodoro     = root.Q("tab-pomodoro");
            _tabOnline       = root.Q("tab-online");
            _tabPet          = root.Q("tab-pet");
            _closeBtn        = root.Q("settings-close");

            // 将各设置面板 UXML 克隆到对应内容容器
            pomodoroTemplate?.CloneTree(_contentPomodoro);
            onlineTemplate?.CloneTree(_contentOnline);
            petTemplate?.CloneTree(_contentPet);

            // 注册关闭与 tab 切换事件
            _closeBtn?.RegisterCallback<PointerUpEvent>(_ => Hide());
            _tabPomodoro?.RegisterCallback<PointerUpEvent>(_ => SelectTab("pomodoro"));
            _tabOnline?.RegisterCallback<PointerUpEvent>(_ => SelectTab("online"));
            _tabPet?.RegisterCallback<PointerUpEvent>(_ => SelectTab("pet"));

            // 初始化三个子面板控制器
            _pomodoroSettings = new PomodoroSettingsPanelController();
            _pomodoroSettings.Init(_contentPomodoro, model, lifecycleOwner);

            _onlineSettings = new OnlineSettingsPanelController();
            _onlineSettings.Init(_contentOnline, roomModel, lifecycleOwner);

            _petSettings = new PetSettingsPanelController();
            _petSettings.Init(_contentPet, lifecycleOwner);
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

            // 切换内容区 display
            if (_contentPomodoro != null)
                _contentPomodoro.style.display = tabName == "pomodoro" ? DisplayStyle.Flex : DisplayStyle.None;
            if (_contentOnline != null)
                _contentOnline.style.display = tabName == "online" ? DisplayStyle.Flex : DisplayStyle.None;
            if (_contentPet != null)
                _contentPet.style.display = tabName == "pet" ? DisplayStyle.Flex : DisplayStyle.None;

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
    }
}
