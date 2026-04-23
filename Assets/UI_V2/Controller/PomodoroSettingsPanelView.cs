using System;
using System.Globalization;
using UnityEngine;
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
        // ─── 事件（父级 Controller 订阅）────────────────────────
        /// <summary>阶段切换窗口提示开关（psp-hint-toggle）变化时触发</summary>
        public event Action<bool> OnHintToggleChanged;

        /// <summary>专注时长（分钟）提交时触发（Blur 或回车）</summary>
        public event Action<int> OnFocusMinutesChanged;

        /// <summary>休息时长（分钟）提交时触发（Blur 或回车）</summary>
        public event Action<int> OnBreakMinutesChanged;

        /// <summary>"应用"按钮点击时触发</summary>
        public event Action OnApplyClicked;

        // ─── UXML 元素引用 ────────────────────────────────────────
        private readonly Toggle _hintToggle;     // name="psp-hint-toggle"
        private readonly TextField _focusValue;  // name="psp-focus-value"
        private readonly TextField _breakValue;  // name="psp-break-value"
        private readonly Label  _soundLabel;     // name="psp-sound-label"
        private readonly Button _applyBtn;       // name="psp-apply-btn"

        // 最近一次 Refresh 时的 Model 值，用于非法输入回滚与去抖
        private int _lastFocusMin = 1;
        private int _lastBreakMin = 0;

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

            _hintToggle   = panelRoot.Q<Toggle>("psp-hint-toggle");
            _focusValue   = panelRoot.Q<TextField>("psp-focus-value");
            _breakValue   = panelRoot.Q<TextField>("psp-break-value");
            _soundLabel   = panelRoot.Q<Label>("psp-sound-label");
            _applyBtn     = panelRoot.Q<Button>("psp-apply-btn");

            RegisterToggleCallbacks();
            RegisterDurationCallbacks();
            RegisterApplyCallback();
        }

        // ─── 公开 API ─────────────────────────────────────────────

        /// <summary>
        /// 用 Model 数据刷新整个设置面板（不触发事件）。
        /// </summary>
        /// <param name="focusMinutes">专注时长（分钟）</param>
        /// <param name="breakMinutes">休息时长（分钟）</param>
        /// <param name="hintEnabled">阶段切换窗口提示是否启用</param>
        /// <param name="soundName">当前选中的提示音名称</param>
        public void Refresh(int focusMinutes, int breakMinutes, bool hintEnabled, string soundName)
        {
            _lastFocusMin = focusMinutes;
            _lastBreakMin = breakMinutes;

            _focusValue?.SetValueWithoutNotify(focusMinutes.ToString(CultureInfo.InvariantCulture));
            _breakValue?.SetValueWithoutNotify(breakMinutes.ToString(CultureInfo.InvariantCulture));

            if (_soundLabel != null)
            {
                _soundLabel.text = soundName ?? string.Empty;
            }

            // Toggle 用 SetValueWithoutNotify 避免触发回调循环
            _hintToggle?.SetValueWithoutNotify(hintEnabled);
        }

        /// <summary>
        /// 控制"应用"按钮的显隐。true 表示有未保存草稿，按钮浮出。
        /// </summary>
        public void SetApplyVisible(bool visible)
        {
            if (_applyBtn == null)
            {
                return;
            }

            _applyBtn.EnableInClassList("psp-apply-btn--hidden", !visible);
        }

        /// <summary>
        /// 强制 Commit 尚未失焦的 TextField，把当前文本同步到 Controller 草稿。
        /// 供"关闭 / 切 tab 前"的守卫流程使用，避免光标还在输入框内时改动遗失。
        /// </summary>
        public void ForceCommitDrafts()
        {
            CommitFocusValue();
            CommitBreakValue();
        }

        /// <summary>
        /// 读取 psp-focus-value 当前文本：合法（整数 ≥1）则触发 <see cref="OnFocusMinutesChanged"/>；
        /// 非法则回滚到最近一次 <see cref="Refresh"/> 传入的值。
        /// 公开以便 EditMode 测试绕过 BlurEvent / KeyDownEvent 直接触发提交。
        /// </summary>
        public void CommitFocusValue()
        {
            if (_focusValue == null)
            {
                return;
            }

            if (int.TryParse(_focusValue.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)
                && minutes >= 1)
            {
                if (minutes == _lastFocusMin)
                {
                    return;
                }

                _lastFocusMin = minutes;
                OnFocusMinutesChanged?.Invoke(minutes);
            }
            else
            {
                _focusValue.SetValueWithoutNotify(_lastFocusMin.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// 以编程方式写入 psp-hint-toggle 的值，并触发 <see cref="OnHintToggleChanged"/>。
        /// 用户拨动开关时由 ValueChangedCallback 走同一条路径；
        /// EditMode 测试无 Panel 上下文，<c>Toggle.value = x</c> 不会发 ChangeEvent，
        /// 因此测试通过此方法复用同一条提交路径。
        /// </summary>
        public void CommitHintToggle(bool value)
        {
            _hintToggle?.SetValueWithoutNotify(value);
            OnHintToggleChanged?.Invoke(value);
        }

        /// <summary>
        /// 读取 psp-break-value 当前文本：合法（整数 ≥0）则触发 <see cref="OnBreakMinutesChanged"/>；
        /// 非法则回滚到最近一次 <see cref="Refresh"/> 传入的值。
        /// </summary>
        public void CommitBreakValue()
        {
            if (_breakValue == null)
            {
                return;
            }

            if (int.TryParse(_breakValue.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)
                && minutes >= 0)
            {
                if (minutes == _lastBreakMin)
                {
                    return;
                }

                _lastBreakMin = minutes;
                OnBreakMinutesChanged?.Invoke(minutes);
            }
            else
            {
                _breakValue.SetValueWithoutNotify(_lastBreakMin.ToString(CultureInfo.InvariantCulture));
            }
        }

        // ─── 私有辅助 ─────────────────────────────────────────────

        private void RegisterToggleCallbacks()
        {
            _hintToggle?.RegisterValueChangedCallback(evt =>
            {
                OnHintToggleChanged?.Invoke(evt.newValue);
            });
        }

        private void RegisterDurationCallbacks()
        {
            if (_focusValue != null)
            {
                _focusValue.RegisterCallback<BlurEvent>(_ => CommitFocusValue());
                _focusValue.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        CommitFocusValue();
                    }
                });
            }

            if (_breakValue != null)
            {
                _breakValue.RegisterCallback<BlurEvent>(_ => CommitBreakValue());
                _breakValue.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        CommitBreakValue();
                    }
                });
            }
        }

        private void RegisterApplyCallback()
        {
            if (_applyBtn == null)
            {
                return;
            }

            _applyBtn.clicked += () => OnApplyClicked?.Invoke();
        }

    }
}
