using APP.Pomodoro.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 番茄钟设置面板视图辅助类。
    /// 绑定 PomodoroSettingsPanel.uxml 中的动态元素，向父级 Controller 暴露事件。
    ///
    /// 计时结束提示行 / 视频选择行用与 GlobalSettingsPanel 共用的 InputDropdownBinding（Pencil Frjkw）：
    ///   - 触发器（.comp-input-dropdown.psp-row-dropdown）：白色 pill + chevron。
    ///   - 触发器后面挂兄弟节点 .comp-input-dropdown-menu，display:none ↔ flex 通过
    ///     .comp-input-dropdown-menu--hidden 切换，菜单项由 InputDropdownBinding 统一构建。
    ///   - 不再单独维护 psp-row-dropdown-menu 一套 class，避免与 Frjkw 组件不同步。
    /// 自定义视频文件行（psp-video-custom-row）仍是普通 ClickEvent，作为系统文件选择器入口。
    /// </summary>
    public sealed class PomodoroSettingsPanelView
    {
        // ─── 事件（父级 Controller 订阅）────────────────────────
        /// <summary>计时结束 mode 下拉选中变化（index 对应 ModeDisplayChoices 顺序，与 PomodoroEndActionMode 枚举值一致）</summary>
        public event Action<int> OnEndActionModeSelected;

        /// <summary>视频选择下拉选中变化：内置项是 0..N-1，"自定义"翻译为 -1</summary>
        public event Action<int> OnVideoSelectionChanged;

        /// <summary>自定义视频文件行点击时触发（Jvg0I 文件选择器入口）</summary>
        public event Action OnVideoCustomRowClicked;

        /// <summary>专注时长（分钟）提交时触发（Blur 或回车）</summary>
        public event Action<int> OnFocusMinutesChanged;

        /// <summary>休息时长（分钟）提交时触发（Blur 或回车）</summary>
        public event Action<int> OnBreakMinutesChanged;

        /// <summary>"应用"按钮点击时触发</summary>
        public event Action OnApplyClicked;

        // ─── UXML 元素引用 ────────────────────────────────────────
        private readonly TextField _focusValue;
        private readonly TextField _breakValue;
        private readonly Label _soundLabel;

        // 计时结束提示行（mode 下拉）
        private readonly VisualElement _endActionRow;
        private readonly InputDropdownBinding _endActionDropdown;

        // 视频文件行（视频选择下拉）
        private readonly VisualElement _videoPathRow;
        private readonly InputDropdownBinding _videoPathDropdown;

        // 自定义视频文件行
        private readonly VisualElement _videoCustomRow;
        private readonly Label _videoCustomStateLabel;

        private readonly Button _applyBtn;

        // 状态
        private int _lastFocusMin = 1;
        private int _lastBreakMin = 0;

        /// <summary>计时结束 mode 的固定 choices 顺序——下标必须严格对齐 PomodoroEndActionMode 枚举：
        /// 0=TopWindow, 1=PlayVideo。Controller 直接 cast 即可。</summary>
        private static readonly string[] ModeDisplayChoices =
        {
            "弹窗到顶部",  // index 0 = TopWindow
            "播放视频",    // index 1 = PlayVideo
        };

        // ─── 构造 ─────────────────────────────────────────────────

        public PomodoroSettingsPanelView(VisualElement panelRoot)
        {
            if (panelRoot == null)
            {
                throw new ArgumentNullException(nameof(panelRoot));
            }

            _focusValue = panelRoot.Q<TemplateContainer>("psp-focus-suffix")?.Q<TextField>("value");
            _breakValue = panelRoot.Q<TemplateContainer>("psp-break-suffix")?.Q<TextField>("value");
            _soundLabel = panelRoot.Q<Label>("psp-sound-label");

            _endActionRow = panelRoot.Q<VisualElement>("psp-end-action-row");
            VisualElement endActionTrigger = panelRoot.Q<VisualElement>("psp-end-action-dropdown");
            Label endActionValue = panelRoot.Q<Label>("psp-end-action-dropdown-value");
            VisualElement endActionMenu = panelRoot.Q<VisualElement>("psp-end-action-menu");
            if (endActionTrigger != null && endActionValue != null && endActionMenu != null)
            {
                _endActionDropdown = new InputDropdownBinding(endActionTrigger, endActionValue, endActionMenu);
            }

            _videoPathRow = panelRoot.Q<VisualElement>("psp-video-path-row");
            VisualElement videoTrigger = panelRoot.Q<VisualElement>("psp-video-path-dropdown");
            Label videoValue = panelRoot.Q<Label>("psp-video-path-dropdown-value");
            VisualElement videoMenu = panelRoot.Q<VisualElement>("psp-video-path-menu");
            if (videoTrigger != null && videoValue != null && videoMenu != null)
            {
                _videoPathDropdown = new InputDropdownBinding(videoTrigger, videoValue, videoMenu);
            }

            _videoCustomRow = panelRoot.Q<VisualElement>("psp-video-custom-row");
            _videoCustomStateLabel = panelRoot.Q<Label>("psp-video-custom-state");
            _applyBtn = panelRoot.Q<Button>("apply-btn");

            // 两个菜单互斥
            if (_endActionDropdown != null && _videoPathDropdown != null)
            {
                _endActionDropdown.OnAboutToOpen += () => _videoPathDropdown.Close();
                _videoPathDropdown.OnAboutToOpen += () => _endActionDropdown.Close();
            }

            RegisterRowCallbacks();
            RegisterDurationCallbacks();
            RegisterApplyCallback();
        }

        // ─── 公开 API ─────────────────────────────────────────────

        public void Refresh(
            int focusMinutes,
            int breakMinutes,
            string soundName,
            PomodoroEndActionMode mode,
            int videoIndex,
            IReadOnlyList<string> builtInDisplayNames,
            string customVideoPath)
        {
            _lastFocusMin = focusMinutes;
            _lastBreakMin = breakMinutes;

            _focusValue?.SetValueWithoutNotify(focusMinutes.ToString(CultureInfo.InvariantCulture));
            _breakValue?.SetValueWithoutNotify(breakMinutes.ToString(CultureInfo.InvariantCulture));

            if (_soundLabel != null)
            {
                _soundLabel.text = soundName ?? string.Empty;
            }

            // ── 计时结束 mode 下拉 ──
            if (_endActionDropdown != null)
            {
                _endActionDropdown.SetTriggerText(ModeDisplayName(mode));
                _endActionDropdown.SetItems(ModeDisplayChoices, (int)mode, OnEndActionModeSelected);
            }

            // ── "视频文件"行 mode == PlayVideo 时显示 ──
            _videoPathRow?.EnableInClassList("is-video-mode", mode == PomodoroEndActionMode.PlayVideo);

            // ── 视频选择下拉 ──
            _videoBuiltInCount = builtInDisplayNames?.Count ?? 0;
            if (_videoPathDropdown != null)
            {
                _videoPathDropdown.SetTriggerText(ResolveVideoChoiceText(videoIndex, builtInDisplayNames));
                _videoPathDropdown.SetItems(
                    BuildVideoChoices(builtInDisplayNames),
                    ResolveVideoSelectedIndex(videoIndex, _videoBuiltInCount),
                    OnVideoSelectionChangedFromMenu);
            }

            // ── "自定义视频文件"行：mode == PlayVideo && videoIndex == -1 时显示 ──
            bool customVisible = mode == PomodoroEndActionMode.PlayVideo && videoIndex == -1;
            _videoCustomRow?.EnableInClassList("is-custom-video", customVisible);

            if (_videoCustomStateLabel != null)
            {
                _videoCustomStateLabel.text = string.IsNullOrEmpty(customVideoPath)
                    ? "未选择"
                    : Path.GetFileName(customVideoPath);
            }
        }

        public void SetApplyVisible(bool visible)
        {
            if (_applyBtn == null)
            {
                return;
            }

            _applyBtn.EnableInClassList("apply-btn--hidden", !visible);
        }

        public void ForceCommitDrafts()
        {
            CommitFocusValue();
            CommitBreakValue();
        }

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

        private static string ModeDisplayName(PomodoroEndActionMode mode)
        {
            return mode switch
            {
                PomodoroEndActionMode.PlayVideo => "播放视频",
                _ => "弹窗到顶部",
            };
        }

        private static string ResolveVideoChoiceText(int videoIndex, IReadOnlyList<string> builtInDisplayNames)
        {
            if (videoIndex == -1)
            {
                return "自定义";
            }
            if (builtInDisplayNames == null || videoIndex < 0 || videoIndex >= builtInDisplayNames.Count)
            {
                return "自定义";
            }
            string name = builtInDisplayNames[videoIndex];
            return string.IsNullOrEmpty(name) ? $"视频 {videoIndex + 1}" : name;
        }

        // 把"内置项 0..N-1 + 末项=自定义"组织成 InputDropdownBinding 的 choices 数组。
        private static IReadOnlyList<string> BuildVideoChoices(IReadOnlyList<string> builtInDisplayNames)
        {
            int builtInCount = builtInDisplayNames?.Count ?? 0;
            string[] choices = new string[builtInCount + 1];
            for (int i = 0; i < builtInCount; i++)
            {
                string raw = builtInDisplayNames[i];
                choices[i] = string.IsNullOrEmpty(raw) ? $"视频 {i + 1}" : raw;
            }
            choices[builtInCount] = "自定义";
            return choices;
        }

        // 把 videoIndex（-1=自定义；其余 0..builtInCount-1=内置）翻译成菜单 selectedIndex（末项=自定义）。
        private static int ResolveVideoSelectedIndex(int videoIndex, int builtInCount)
        {
            if (videoIndex == -1)
            {
                return builtInCount; // 末项 = "自定义"
            }
            if (videoIndex >= 0 && videoIndex < builtInCount)
            {
                return videoIndex;
            }
            return -1;
        }

        // 菜单 onSelect(index) → 翻译成 videoIndex 后转发：末项 → -1，其余原样。
        private void OnVideoSelectionChangedFromMenu(int menuIndex)
        {
            int builtInCount = _videoBuiltInCount;
            int videoIndex = (menuIndex == builtInCount) ? -1 : menuIndex;
            OnVideoSelectionChanged?.Invoke(videoIndex);
        }

        // 缓存最近一次 Refresh 时的内置项数（用于 menuIndex → videoIndex 翻译）。
        private int _videoBuiltInCount;

        private void RegisterRowCallbacks()
        {
            // 自定义视频文件行点击 = 触发文件选择器（mode/video 行自身已有触发器接管点击）
            _videoCustomRow?.RegisterCallback<ClickEvent>(_ => OnVideoCustomRowClicked?.Invoke());
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
