using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 番茄钟设置面板控制器（独立 UIDocument）。
    /// 管理 PomodoroSettingsPanel.uxml 的 UI 绑定与生命周期。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PomodoroSettingsPanelController : MonoBehaviour, IController, ISettingsPanel
    {
        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument _uiDocument;
        private PomodoroSettingsPanelView _view;
        private IPomodoroModel _model;
        private bool _modelBound;

        // ─── ISettingsPanel ──────────────────────────────────────
        public bool IsVisible => _uiDocument != null && _uiDocument.enabled;

        public void Show()
        {
            if (_uiDocument == null)
            {
                return;
            }

            _uiDocument.enabled = true;
            BindUI();
            RefreshFromModel();
        }

        public void Hide()
        {
            if (_uiDocument == null)
            {
                return;
            }

            _uiDocument.enabled = false;
        }

        // ─── Unity 生命周期 ──────────────────────────────────────

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void Start()
        {
            // 默认隐藏
            if (_uiDocument != null)
            {
                _uiDocument.enabled = false;
            }
        }

        // ─── UI 绑定 ─────────────────────────────────────────────

        /// <summary>
        /// 每次 Show 时重新绑定 UI 元素。
        /// UIDocument.enabled false→true 会重建 visual tree，旧回调全部失效。
        /// </summary>
        private void BindUI()
        {
            if (_uiDocument == null)
            {
                return;
            }

            VisualElement root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            // 返回按钮（每次重建都需重新绑定）
            root.Q<Button>("back-btn")?.RegisterCallback<PointerUpEvent>(_ => Hide());

            // 视图辅助类（每次重建都需重新创建，因为它持有旧 VisualElement 引用）
            _view = new PomodoroSettingsPanelView(root);
            _view.OnEnabledChanged += OnEnabledToggleChanged;
            _view.OnHintToggleChanged += OnHintToggleChanged;

            // Model 订阅（仅一次，生命周期绑定 GameObject）
            if (!_modelBound)
            {
                _model = this.GetModel<IPomodoroModel>();
                if (_model != null)
                {
                    _model.FocusDurationSeconds.Register(_ => RefreshFromModel())
                        .UnRegisterWhenGameObjectDestroyed(gameObject);
                    _model.BreakDurationSeconds.Register(_ => RefreshFromModel())
                        .UnRegisterWhenGameObjectDestroyed(gameObject);
                    _model.CompletionClipIndex.Register(_ => RefreshFromModel())
                        .UnRegisterWhenGameObjectDestroyed(gameObject);
                }

                _modelBound = true;
            }
        }

        // ─── 数据刷新 ────────────────────────────────────────────

        private void RefreshFromModel()
        {
            if (_view == null || _model == null)
            {
                return;
            }

            int focusMin = _model.FocusDurationSeconds.Value / 60;
            int breakMin = _model.BreakDurationSeconds.Value / 60;
            bool isEnabled = true; // 番茄钟始终启用
            bool hintEnabled = _model.AutoJumpToTopOnComplete.Value;
            string soundName = "柔和铃声"; // 暂用固定文本

            _view.Refresh(focusMin, breakMin, isEnabled, hintEnabled, soundName);
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnEnabledToggleChanged(bool enabled)
        {
            // 预留：未来可发 Command 切换番茄钟启用状态
        }

        private void OnHintToggleChanged(bool enabled)
        {
            if (_model == null)
            {
                return;
            }

            this.SendCommand(new Cmd_PomodoroApplyMetaSettings(
                enabled,
                _model.AutoStartBreak.Value,
                _model.CompletionClipIndex.Value));
        }
    }
}
