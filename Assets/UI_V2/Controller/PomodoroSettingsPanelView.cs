using APP.Pomodoro.Model;
using System;
using System.Globalization;
using System.IO;
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
        /// <summary>计时结束提示行点击时触发</summary>
        public event Action OnEndActionRowClicked;

        /// <summary>视频文件行点击时触发</summary>
        public event Action OnVideoPathRowClicked;

        /// <summary>专注时长（分钟）提交时触发（Blur 或回车）</summary>
        public event Action<int> OnFocusMinutesChanged;

        /// <summary>休息时长（分钟）提交时触发（Blur 或回车）</summary>
        public event Action<int> OnBreakMinutesChanged;

        /// <summary>"应用"按钮点击时触发</summary>
        public event Action OnApplyClicked;

        // ─── UXML 元素引用 ────────────────────────────────────────
        private readonly TextField _focusValue;             // Instance name="psp-focus-suffix" 内 TextField
        private readonly TextField _breakValue;             // Instance name="psp-break-suffix" 内 TextField
        private readonly Label _soundLabel;                 // name="psp-sound-label"
        private readonly VisualElement _endActionRow;       // name="psp-end-action-row"
        private readonly Label _endActionStateLabel;        // name="psp-end-action-state"
        private readonly VisualElement _videoPathRow;       // name="psp-video-path-row"
        private readonly Label _videoPathStateLabel;        // name="psp-video-path-state"
        private readonly Button _applyBtn;                  // name="apply-btn"

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

            // 元素通过 Instance 容器向内查找（AttributeOverrides 不覆盖 name）
            _focusValue = panelRoot.Q<TemplateContainer>("psp-focus-suffix")?.Q<TextField>("value");
            _breakValue = panelRoot.Q<TemplateContainer>("psp-break-suffix")?.Q<TextField>("value");
            _soundLabel = panelRoot.Q<Label>("psp-sound-label");
            _endActionRow = panelRoot.Q<VisualElement>("psp-end-action-row");
            _endActionStateLabel = panelRoot.Q<Label>("psp-end-action-state");
            _videoPathRow = panelRoot.Q<VisualElement>("psp-video-path-row");
            _videoPathStateLabel = panelRoot.Q<Label>("psp-video-path-state");
            _applyBtn = panelRoot.Q<Button>("apply-btn");

            RegisterRowCallbacks();
            RegisterDurationCallbacks();
            RegisterApplyCallback();
        }

        // ─── 公开 API ─────────────────────────────────────────────

        /// <summary>
        /// 用 Model 数据刷新整个设置面板（不触发事件）。
        /// </summary>
        /// <param name="focusMinutes">专注时长（分钟）</param>
        /// <param name="breakMinutes">休息时长（分钟）</param>
        /// <param name="soundName">当前选中的提示音名称</param>
        /// <param name="mode">计时结束提示动作</param>
        /// <param name="videoPath">视频文件路径</param>
        public void Refresh(
            int focusMinutes,
            int breakMinutes,
            string soundName,
            PomodoroEndActionMode mode,
            string videoPath)
        {
            _lastFocusMin = focusMinutes;
            _lastBreakMin = breakMinutes;

            _focusValue?.SetValueWithoutNotify(focusMinutes.ToString(CultureInfo.InvariantCulture));
            _breakValue?.SetValueWithoutNotify(breakMinutes.ToString(CultureInfo.InvariantCulture));

            if (_soundLabel != null)
            {
                _soundLabel.text = soundName ?? string.Empty;
            }

            if (_endActionStateLabel != null)
            {
                _endActionStateLabel.text = mode == PomodoroEndActionMode.PlayVideo ? "播放视频" : "弹窗到顶部";
            }

            _videoPathRow?.EnableInClassList("is-video-mode", mode == PomodoroEndActionMode.PlayVideo);

            if (_videoPathStateLabel != null)
            {
                _videoPathStateLabel.text = string.IsNullOrEmpty(videoPath) ? "未选择" : Path.GetFileName(videoPath);
            }
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

            _applyBtn.EnableInClassList("apply-btn--hidden", !visible);
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

        private void RegisterRowCallbacks()
        {
            _endActionRow?.RegisterCallback<ClickEvent>(_ => OnEndActionRowClicked?.Invoke());
            _videoPathRow?.RegisterCallback<ClickEvent>(_ => OnVideoPathRowClicked?.Invoke());
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
