using System;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// "未保存更改"对话框薄适配层：保持原有 API（Init/Show/Hide/IsVisible）不变，
    /// 内部委托给 ConfirmDialogController，传入预置的标题/正文/按钮文案，countdownSeconds=0。
    /// 仅为兼容 PomodoroSettingsPanel 现有调用点，避免破坏测试。
    /// </summary>
    public sealed class UnsavedChangesDialogController
    {
        private readonly ConfirmDialogController _inner = new ConfirmDialogController();

        public bool IsVisible => _inner.IsVisible;

        /// <summary>
        /// 注意：template 现应传入 ConfirmDialog.uxml，而不是已删除的 UnsavedChangesDialog.uxml。
        /// UnifiedSettingsPanelDriver 的序列化字段需要重新指向新资源。
        /// </summary>
        public void Init(VisualElement host, VisualTreeAsset confirmDialogTemplate)
            => _inner.Init(host, confirmDialogTemplate);

        public void Show(Action onConfirm, Action onCancel)
            => _inner.Show(
                title:       "有未保存的更改",
                subtitle:    "请先应用或取消后再继续",
                body:        "你修改了番茄钟设置但尚未应用。离开此面板将丢失这些改动，是否先保存并继续？",
                confirmText: "保存并继续",
                cancelText:  "取消",
                onConfirm:   onConfirm,
                onCancel:    onCancel,
                countdownSeconds: 0f);

        public void Hide() => _inner.Hide();
    }
}
