using System;
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
    /// 关闭 / 切 tab 前若番茄钟面板有未应用草稿，弹出 UnsavedChangesDialog 拦截。
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
        private VisualElement _unsavedDialogHost;

        // ─── 状态 ────────────────────────────────────────────────
        private string _activeTab = "pomodoro";

        // ─── 子控制器 ────────────────────────────────────────────
        private PomodoroSettingsPanelController _pomodoroSettings;
        private OnlineSettingsPanelController _onlineSettings;
        private PetSettingsPanelController _petSettings;
        private UnsavedChangesDialogController _unsavedDialog;

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

        /// <summary>供测试检查未保存对话框当前是否浮出。</summary>
        public bool IsUnsavedDialogVisible => _unsavedDialog?.IsVisible == true;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 初始化统一设置面板。
        /// </summary>
        public void Init(
            VisualElement root,
            IPomodoroModel model,
            IRoomModel roomModel,
            VisualTreeAsset pomodoroTemplate,
            VisualTreeAsset onlineTemplate,
            VisualTreeAsset petTemplate,
            VisualTreeAsset unsavedDialogTemplate,
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
            _unsavedDialogHost = root.Q("unsaved-dialog-host");

            // 初始化未保存提示对话框
            _unsavedDialog = new UnsavedChangesDialogController();
            if (_unsavedDialogHost != null && unsavedDialogTemplate != null)
            {
                _unsavedDialog.Init(_unsavedDialogHost, unsavedDialogTemplate);
            }

            // 注册关闭与 tab 切换事件
            _closeBtn?.RegisterCallback<PointerUpEvent>(_ => RequestClose(null));
            _tabPomodoro?.RegisterCallback<PointerUpEvent>(_ => SelectTab("pomodoro"));
            _tabOnline?.RegisterCallback<PointerUpEvent>(_ => SelectTab("online"));
            _tabPet?.RegisterCallback<PointerUpEvent>(_ => SelectTab("pet"));

            // 整窗拖拽：以 overlay 本身作为 handle；注意 overlay 初始用 flex 居中于全屏锚点，
            // 首次拖拽前需切换为 position:absolute 并把 left/top 锁定在当前布局位置。
            if (_overlay != null)
            {
                _overlay.RegisterCallback<PointerDownEvent>(PinOverlayForDrag);
                DraggableElement.MakeDraggable(_overlay);

                // 交互子节点：阻断 PointerDown 冒泡，避免点击它们时误触整窗拖拽
                _closeBtn?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                _tabPomodoro?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                _tabOnline?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                _tabPet?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                // 内容区（含 ScrollView、按钮、输入框等）统一阻断
                var contentScroll = root.Q<ScrollView>("settings-content");
                contentScroll?.contentContainer.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            }

            DoSelectTab(_activeTab);
        }

        /// <summary>
        /// 首次 PointerDown 时把 overlay 从 flex 居中切换为绝对定位，
        /// 并把 left/top 钉在当前布局坐标，避免 DragController 读到 0 导致跳位。
        /// </summary>
        private void PinOverlayForDrag(PointerDownEvent _)
        {
            if (_overlay == null) return;
            if (_overlay.style.position.keyword == StyleKeyword.Null
                && _overlay.resolvedStyle.position == Position.Absolute)
            {
                return;
            }
            if (_overlay.style.position.value == Position.Absolute)
            {
                return;
            }

            var layout = _overlay.layout;
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = layout.x;
            _overlay.style.top = layout.y;
        }

        // ─── 显隐控制 ────────────────────────────────────────────

        public void Show()
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.style.display = DisplayStyle.Flex;
            DoSelectTab(_activeTab);
        }

        /// <summary>
        /// 请求关闭面板。若番茄钟面板有未应用草稿，弹出确认对话框；
        /// 否则立即执行关闭并回调 <paramref name="onCloseConfirmed"/>。
        /// </summary>
        /// <param name="onCloseConfirmed">
        /// 面板真正关闭后执行（Driver 用它把 UIDocument 根也隐藏掉）。
        /// Cancel 时不会被调用。
        /// </param>
        public void RequestClose(Action onCloseConfirmed)
        {
            _pomodoroSettings?.ForceCommitPendingEdits();

            if (_pomodoroSettings?.IsDirty == true && _unsavedDialog != null)
            {
                _unsavedDialog.Show(
                    onConfirm: () =>
                    {
                        _pomodoroSettings.TryApply();
                        DoHide();
                        onCloseConfirmed?.Invoke();
                    },
                    onCancel: null);
                return;
            }

            DoHide();
            onCloseConfirmed?.Invoke();
        }

        private void DoHide()
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.style.display = DisplayStyle.None;
        }

        // 保留旧 API 供未来直接隐藏场景使用（不走 dirty 检查）
        public void Hide() => DoHide();

        // ─── Tab 切换 ────────────────────────────────────────────

        /// <summary>
        /// 切换设置面板 tab。从 pomodoro 切到别的 tab 前若有未应用草稿，弹出确认对话框。
        /// 同 tab 重选或切到 pomodoro 本身不触发拦截。
        /// </summary>
        public void SelectTab(string tabName)
        {
            if (tabName == _activeTab)
            {
                DoSelectTab(tabName);
                return;
            }

            _pomodoroSettings?.ForceCommitPendingEdits();

            if (_activeTab == "pomodoro"
                && _pomodoroSettings?.IsDirty == true
                && _unsavedDialog != null)
            {
                _unsavedDialog.Show(
                    onConfirm: () =>
                    {
                        _pomodoroSettings.TryApply();
                        DoSelectTab(tabName);
                    },
                    onCancel: null);
                return;
            }

            DoSelectTab(tabName);
        }

        private void DoSelectTab(string tabName)
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
