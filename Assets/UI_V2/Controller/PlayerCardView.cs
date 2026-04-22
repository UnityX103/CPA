using System;
using APP.Network.Model;
using APP.Pomodoro.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 远端玩家卡片视图。只读展示 RemotePlayerData，不持有业务写能力。
    /// </summary>
    public sealed class PlayerCardView
    {
        private static readonly string[] PhaseClasses =
        {
            "pc-phase-idle",
            "pc-phase-focus",
            "pc-phase-rest",
            "pc-phase-paused",
            "pc-phase-completed",
        };

        // 玩家名自适应字号范围：基础=设计稿 14，最小=9，低于此保留 overflow 兜底
        private const float NameBaseFontSize = 14f;
        private const float NameMinFontSize = 9f;
        // 安全系数，避免测量值与实际渲染舍入带来的溢出
        private const float NameSafetyFactor = 0.98f;

        private readonly Label _nameLabel;
        private readonly Label _phaseLabel;
        private readonly Label _timeLabel;
        private readonly Label _roundsLabel;
        private readonly Label _appLabel;

        // 防止 AutoFit 内部修改 fontSize 触发 GeometryChangedEvent 再次进入导致递归
        private bool _isAutoFittingName;

        public VisualElement Root { get; }
        public string PlayerId { get; }

        public PlayerCardView(RemotePlayerData data, VisualTreeAsset uxml)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (uxml == null)
            {
                throw new ArgumentNullException(nameof(uxml));
            }

            PlayerId = data.PlayerId;

            TemplateContainer container = uxml.Instantiate();
            // 把卡片的绝对定位和样式直接作用在 .pc-root 节点上，避免 TemplateContainer 多一层包装干扰定位
            Root = container.Q<VisualElement>(className: "pc-root") ?? container;
            Root.pickingMode = PickingMode.Position;

            _nameLabel = Root.Q<Label>("pc-name");
            _phaseLabel = Root.Q<Label>("pc-phase");
            _timeLabel = Root.Q<Label>("pc-time");
            _roundsLabel = Root.Q<Label>("pc-rounds");
            _appLabel = Root.Q<Label>("pc-app");

            // 玩家名容器布局变化时重新自适应字号（例如卡片被改宽）
            if (_nameLabel != null)
            {
                var nameCol = _nameLabel.parent;
                nameCol?.RegisterCallback<GeometryChangedEvent>(_ => AutoFitNameFontSize());
            }

            Refresh(data);
        }

        /// <summary>根据最新远端数据刷新视图。</summary>
        public void Refresh(RemotePlayerData data)
        {
            if (data == null)
            {
                return;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = string.IsNullOrEmpty(data.PlayerName) ? "玩家" : data.PlayerName;
                // 文本变化后触发自适应；schedule 下一帧让布局先完成一次 pass
                _nameLabel.schedule.Execute(AutoFitNameFontSize).StartingIn(0);
            }

            if (_phaseLabel != null)
            {
                _phaseLabel.text = FormatPhase(data.Phase, data.IsRunning);
            }

            if (_timeLabel != null)
            {
                _timeLabel.text = FormatTime(data.RemainingSeconds);
            }

            if (_roundsLabel != null)
            {
                _roundsLabel.text = $"{data.CurrentRound}/{data.TotalRounds}";
            }

            if (_appLabel != null)
            {
                _appLabel.text = string.IsNullOrEmpty(data.ActiveAppName) ? "—" : data.ActiveAppName;
            }

            ApplyPhaseClass(data.Phase, data.IsRunning);
        }

        /// <summary>将秒数格式化为 MM:SS（负值和超大值做保护）。</summary>
        public static string FormatTime(int totalSeconds)
        {
            if (totalSeconds < 0)
            {
                totalSeconds = 0;
            }

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>中文阶段标签。</summary>
        public static string FormatPhase(PomodoroPhase phase, bool isRunning)
        {
            switch (phase)
            {
                case PomodoroPhase.Focus:
                    return isRunning ? "专注中" : "专注暂停";
                case PomodoroPhase.Break:
                    return isRunning ? "休息中" : "休息暂停";
                case PomodoroPhase.Completed:
                    return "已完成";
                default:
                    return "待机";
            }
        }

        /// <summary>根据阶段切换 USS 类名。</summary>
        public static string GetPhaseClass(PomodoroPhase phase, bool isRunning)
        {
            if (!isRunning && phase != PomodoroPhase.Completed)
            {
                return "pc-phase-paused";
            }

            switch (phase)
            {
                case PomodoroPhase.Focus:
                    return "pc-phase-focus";
                case PomodoroPhase.Break:
                    return "pc-phase-rest";
                case PomodoroPhase.Completed:
                    return "pc-phase-completed";
                default:
                    return "pc-phase-idle";
            }
        }

        /// <summary>
        /// 根据玩家名容器宽度逐步缩小字体，直到完整文本可在一行内显示为止。
        /// 若缩到最小字号仍溢出，USS 的 overflow:hidden + text-overflow 作为兜底。
        /// </summary>
        private void AutoFitNameFontSize()
        {
            if (_isAutoFittingName) return;
            if (_nameLabel == null) return;
            if (string.IsNullOrEmpty(_nameLabel.text)) return;

            var parent = _nameLabel.parent;
            if (parent == null) return;

            float available = parent.resolvedStyle.width;
            if (available <= 0f) return; // 尚未完成布局

            _isAutoFittingName = true;
            try
            {
                // 从基础字号开始测量
                _nameLabel.style.fontSize = NameBaseFontSize;
                Vector2 measured = _nameLabel.MeasureTextSize(
                    _nameLabel.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);

                if (measured.x <= available)
                {
                    return;
                }

                float ratio = available / measured.x;
                float newSize = Mathf.Max(NameMinFontSize, NameBaseFontSize * ratio * NameSafetyFactor);
                _nameLabel.style.fontSize = newSize;
            }
            finally
            {
                _isAutoFittingName = false;
            }
        }

        private void ApplyPhaseClass(PomodoroPhase phase, bool isRunning)
        {
            string target = GetPhaseClass(phase, isRunning);
            for (int i = 0; i < PhaseClasses.Length; i++)
            {
                string cls = PhaseClasses[i];
                if (cls == target)
                {
                    if (!Root.ClassListContains(cls))
                    {
                        Root.AddToClassList(cls);
                    }
                }
                else
                {
                    Root.RemoveFromClassList(cls);
                }
            }
        }
    }
}
