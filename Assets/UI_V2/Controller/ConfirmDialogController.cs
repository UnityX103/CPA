using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 通用确认对话框控制器（纯 C# 类）。
    /// 可选 countdown 模式：Show(countdownSeconds > 0) 时启用倒计时，到 0 自动触发 onCancel。
    /// 使用 VisualElement.schedule 跑 tick，不依赖 MonoBehaviour。
    /// </summary>
    public sealed class ConfirmDialogController
    {
        private VisualElement _root;
        private VisualElement _countdownRow;
        private VisualElement _closeBtn;
        private Label _title;
        private Label _subtitle;
        private Label _body;
        private Label _countdownText;
        private Button _confirmBtn;
        private Button _cancelBtn;

        private IVisualElementScheduledItem _tickItem;
        private float _remainingSeconds;
        private string _countdownTemplate = "剩余 {0}s 后自动还原";

        private Action _onConfirm;
        private Action _onCancel;

        public bool IsVisible => _root != null && _root.style.display != DisplayStyle.None;

        public void Init(VisualElement host, VisualTreeAsset template)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (template == null)
            {
                Debug.LogError("[ConfirmDialogController] template 为空，对话框不可用");
                return;
            }

            host.Clear();
            template.CloneTree(host);

            _root          = host.Q<VisualElement>("dlg-root");
            _countdownRow  = host.Q<VisualElement>("dlg-countdown");
            _closeBtn      = host.Q<VisualElement>("dlg-close");
            _title         = host.Q<Label>("dlg-title");
            _subtitle      = host.Q<Label>("dlg-subtitle");
            _body          = host.Q<Label>("dlg-body");
            _countdownText = host.Q<Label>("dlg-countdown-text");
            _confirmBtn    = host.Q<Button>("dlg-confirm");
            _cancelBtn     = host.Q<Button>("dlg-cancel");

            if (_confirmBtn != null) _confirmBtn.clicked += HandleConfirm;
            if (_cancelBtn  != null) _cancelBtn.clicked  += HandleCancel;
            _closeBtn?.RegisterCallback<PointerUpEvent>(_ => HandleCancel());

            // 阻止 backdrop/卡片点击穿透
            _root?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            _root?.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());

            Hide();
        }

        public void Show(
            string title,
            string subtitle,
            string body,
            string confirmText,
            string cancelText,
            Action onConfirm,
            Action onCancel,
            float countdownSeconds = 0f)
        {
            if (_root == null) return;

            if (_title    != null) _title.text    = title    ?? string.Empty;
            if (_subtitle != null) _subtitle.text = subtitle ?? string.Empty;
            if (_body     != null) _body.text     = body     ?? string.Empty;
            if (_confirmBtn != null) _confirmBtn.text = confirmText ?? string.Empty;
            if (_cancelBtn  != null) _cancelBtn.text  = cancelText  ?? string.Empty;

            _onConfirm = onConfirm;
            _onCancel  = onCancel;

            StopCountdown();

            if (countdownSeconds > 0f)
            {
                _remainingSeconds = countdownSeconds;
                SetCountdownRowVisible(true);
                RefreshCountdownLabel();
                StartCountdown();
            }
            else
            {
                SetCountdownRowVisible(false);
            }

            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root == null) return;
            StopCountdown();
            _root.style.display = DisplayStyle.None;
        }

        // ─── 测试钩子（internal，仅供 EditMode 测试绕过 UI 事件直接触发） ──
        internal void TriggerConfirmForTest() => HandleConfirm();
        internal void TriggerCancelForTest()  => HandleCancel();

        /// <summary>
        /// 内部：每 tick 扣减剩余秒数并刷新文字；到 0 触发 HandleCancel。
        /// 公开 internal visibility 以便 EditMode 测试绕过 scheduler 调用。
        /// </summary>
        internal void TickElapsed(float deltaSeconds)
        {
            if (!IsVisible) return;
            _remainingSeconds -= deltaSeconds;
            if (_remainingSeconds <= 0f)
            {
                HandleCancel();
                return;
            }
            RefreshCountdownLabel();
        }

        internal float RemainingSeconds => _remainingSeconds;

        private void HandleConfirm()
        {
            var cb = _onConfirm;
            _onConfirm = null;
            _onCancel  = null;
            Hide();
            cb?.Invoke();
        }

        private void HandleCancel()
        {
            var cb = _onCancel;
            _onConfirm = null;
            _onCancel  = null;
            Hide();
            cb?.Invoke();
        }

        private void StartCountdown()
        {
            if (_root == null) return;
            _tickItem = _root.schedule
                .Execute(() => TickElapsed(0.5f))
                .Every(500);
        }

        private void StopCountdown()
        {
            _tickItem?.Pause();
            _tickItem = null;
        }

        private void SetCountdownRowVisible(bool visible)
        {
            if (_countdownRow == null) return;
            _countdownRow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshCountdownLabel()
        {
            if (_countdownText == null) return;
            int secs = Mathf.Max(0, Mathf.CeilToInt(_remainingSeconds));
            _countdownText.text = string.Format(_countdownTemplate, secs);
        }
    }
}
