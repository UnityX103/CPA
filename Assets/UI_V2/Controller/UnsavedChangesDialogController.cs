using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// "未保存更改"提示对话框控制器（纯 C# 类，无 MonoBehaviour）。
    /// 由 UnifiedSettingsPanelController 实例化，挂在 settings-overlay 下的 host 容器上。
    /// </summary>
    public sealed class UnsavedChangesDialogController
    {
        // ─── UI 元素 ─────────────────────────────────────────────
        private VisualElement _root;       // name="dlg-root"
        private Button _confirm;           // name="dlg-confirm"
        private Button _cancel;            // name="dlg-cancel"
        private VisualElement _closeBtn;   // name="dlg-close"

        // ─── 回调 ────────────────────────────────────────────────
        private Action _onConfirm;
        private Action _onCancel;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 把对话框 UXML 克隆到给定 host 容器并绑定按钮事件。
        /// </summary>
        public void Init(VisualElement host, VisualTreeAsset template)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (template == null)
            {
                Debug.LogError("[UnsavedChangesDialogController] template 为空，对话框不可用");
                return;
            }

            host.Clear();
            template.CloneTree(host);

            _root     = host.Q<VisualElement>("dlg-root");
            _confirm  = host.Q<Button>("dlg-confirm");
            _cancel   = host.Q<Button>("dlg-cancel");
            _closeBtn = host.Q<VisualElement>("dlg-close");

            if (_confirm != null) { _confirm.clicked += HandleConfirm; }
            if (_cancel  != null) { _cancel.clicked  += HandleCancel;  }
            _closeBtn?.RegisterCallback<PointerUpEvent>(_ => HandleCancel());

            // 阻止遮罩点击穿透到下层设置面板
            _root?.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            _root?.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());

            Hide();
        }

        // ─── 公开 API ────────────────────────────────────────────

        public bool IsVisible => _root != null && _root.style.display != DisplayStyle.None;

        /// <summary>显示对话框并登记两路回调（点"保存并继续" / 点"取消"或 X）。</summary>
        public void Show(Action onConfirm, Action onCancel)
        {
            if (_root == null)
            {
                return;
            }

            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root == null)
            {
                return;
            }

            _root.style.display = DisplayStyle.None;
        }

        // ─── 内部处理 ────────────────────────────────────────────

        private void HandleConfirm()
        {
            Action cb = _onConfirm;
            _onConfirm = null;
            _onCancel = null;
            Hide();
            cb?.Invoke();
        }

        private void HandleCancel()
        {
            Action cb = _onCancel;
            _onConfirm = null;
            _onCancel = null;
            Hide();
            cb?.Invoke();
        }
    }
}
