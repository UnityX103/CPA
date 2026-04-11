using System;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 番茄钟设置面板视图辅助类。
    /// 绑定 PomodoroSettingsPanel.uxml 中的动态元素，向父级 Controller 暴露事件。
    /// 由 DeskWindowController（或等效 Controller）实例化并持有。
    /// </summary>
    public sealed class PomodoroSettingsPanelView
    {
        // ─── 事件（父级 Controller 订阅后发 Command）────────────────
        /// <summary>番茄钟启用开关（psp-toggle）变化时触发</summary>
        public event Action<bool> OnEnabledChanged;

        /// <summary>阶段切换窗口提示开关（psp-hint-toggle）变化时触发</summary>
        public event Action<bool> OnHintToggleChanged;

        // ─── UXML 元素引用 ────────────────────────────────────────
        private readonly Toggle _enableToggle;   // name="psp-toggle"
        private readonly Toggle _hintToggle;     // name="psp-hint-toggle"
        private readonly Label  _focusValue;     // name="psp-focus-value"
        private readonly Label  _breakValue;     // name="psp-break-value"
        private readonly Label  _soundLabel;     // name="psp-sound-label"
        // _statusText 已移除（toggle 改为独立行，无需文字标签）

        // ─── 构造 ─────────────────────────────────────────────────

        /// <param name="panelRoot">
        /// PomodoroSettingsPanel.uxml 的根 VisualElement。
        /// 通过 <c>ui:Instance</c> 嵌入时传入 TemplateContainer：
        /// <code>root.Q&lt;TemplateContainer&gt;("panel-pomodoro-s")</code>
        /// </param>
        public PomodoroSettingsPanelView(VisualElement panelRoot)
        {
            if (panelRoot == null)
            {
                throw new ArgumentNullException(nameof(panelRoot));
            }

            _enableToggle = panelRoot.Q<Toggle>("psp-toggle");
            _hintToggle   = panelRoot.Q<Toggle>("psp-hint-toggle");
            _focusValue   = panelRoot.Q<Label>("psp-focus-value");
            _breakValue   = panelRoot.Q<Label>("psp-break-value");
            _soundLabel   = panelRoot.Q<Label>("psp-sound-label");
            RegisterToggleCallbacks();
        }

        // ─── 公开 API ─────────────────────────────────────────────

        /// <summary>
        /// 用 Model 数据刷新整个设置面板（不触发事件）。
        /// </summary>
        /// <param name="focusMinutes">专注时长（分钟）</param>
        /// <param name="breakMinutes">休息时长（分钟）</param>
        /// <param name="isEnabled">番茄钟是否启用</param>
        /// <param name="hintEnabled">阶段切换窗口提示是否启用</param>
        /// <param name="soundName">当前选中的提示音名称</param>
        public void Refresh(int focusMinutes, int breakMinutes, bool isEnabled, bool hintEnabled, string soundName)
        {
            if (_focusValue != null)
            {
                _focusValue.text = focusMinutes.ToString();
            }

            if (_breakValue != null)
            {
                _breakValue.text = breakMinutes.ToString();
            }

            if (_soundLabel != null)
            {
                _soundLabel.text = soundName ?? string.Empty;
            }

            // Toggle 用 SetValueWithoutNotify 避免触发回调循环
            _enableToggle?.SetValueWithoutNotify(isEnabled);
            _hintToggle?.SetValueWithoutNotify(hintEnabled);
        }

        // ─── 私有辅助 ─────────────────────────────────────────────

        private void RegisterToggleCallbacks()
        {
            _enableToggle?.RegisterValueChangedCallback(evt =>
            {
                OnEnabledChanged?.Invoke(evt.newValue);
            });

            _hintToggle?.RegisterValueChangedCallback(evt =>
            {
                OnHintToggleChanged?.Invoke(evt.newValue);
            });
        }

    }
}
