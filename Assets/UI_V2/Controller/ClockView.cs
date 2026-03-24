using System;
using APP.Pomodoro.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 时钟的显示状态（由父级 Controller 负责决策，ClockView 只负责渲染）
    /// </summary>
    public enum ClockDisplayState
    {
        /// <summary>未开始：进度 0，灰色主题</summary>
        Idle,
        /// <summary>专注计时中：橙红主题</summary>
        Focus,
        /// <summary>休息计时中：绿色主题</summary>
        Rest,
        /// <summary>暂停中（专注或休息皆可）：琥珀主题</summary>
        Paused,
        /// <summary>全部轮次已完成：灰色主题</summary>
        Completed,
    }

    /// <summary>
    /// 时钟环形视图控制器。
    /// 驱动 Clock.uxml 中的 Painter2D 进度弧、颜色主题切换和文字同步。
    /// 由 PomodoroPanelController 实例化并持有。
    /// </summary>
    public sealed class ClockView
    {
        // ─── 几何常量 ────────────────────────────────────────────
        // 与 Pencil 设计稿对应：104×104，innerRadius=0.77 → 环宽 12px
        private const float RingThickness = 12f;
        private const float ArcStartDeg   = -90f; // 从顶部（12 点钟）开始

        // ─── 颜色主题（对应 Clock.uss 注释中的 CLOCK_THEMES）────
        // focus: 橙红
        private static readonly Color FocusTrack    = new Color32(0xFF, 0xE5, 0xD9, 0xFF); // #FFE5D9
        private static readonly Color FocusProgress = new Color32(0xD1, 0x5F, 0x3D, 0xFF); // #D15F3D
        private static readonly Color FocusValue    = new Color32(0x5B, 0x46, 0x36, 0xFF); // #5B4636
        private static readonly Color FocusLabel    = new Color32(0xD1, 0x5F, 0x3D, 0xFF); // #D15F3D

        // rest: 绿色
        private static readonly Color RestTrack    = new Color32(0xC8, 0xED, 0xD5, 0xFF); // #C8EDD5
        private static readonly Color RestProgress = new Color32(0x34, 0xA8, 0x53, 0xFF); // #34A853
        private static readonly Color RestValue    = new Color32(0x1D, 0x6B, 0x35, 0xFF); // #1D6B35
        private static readonly Color RestLabel    = new Color32(0x34, 0xA8, 0x53, 0xFF); // #34A853

        // off / idle / completed: 灰色
        private static readonly Color OffRing  = new Color32(0xE0, 0xD9, 0xD3, 0xFF); // #E0D9D3
        private static readonly Color OffValue = new Color32(0x5B, 0x46, 0x36, 0xFF); // #5B4636
        private static readonly Color OffLabel = new Color32(0xB5, 0xA4, 0x9A, 0xFF); // #B5A49A

        // paused: 琥珀
        private static readonly Color PausedTrack    = new Color32(0xFF, 0xF0, 0xD4, 0xFF); // #FFF0D4
        private static readonly Color PausedProgress = new Color32(0xF5, 0xA6, 0x23, 0xFF); // #F5A623
        private static readonly Color PausedValue    = new Color32(0x5B, 0x46, 0x36, 0xFF); // #5B4636
        private static readonly Color PausedLabel    = new Color32(0xE0, 0x8C, 0x10, 0xFF); // #E08C10

        // ─── UXML 元素引用 ────────────────────────────────────────
        private readonly VisualElement _ring;
        private readonly Label         _timeLabel;
        private readonly Label         _stateLabel;

        // ─── 绘制状态（由 Refresh 写入，generateVisualContent 读取）─
        private Color _trackColor    = OffRing;
        private Color _progressColor = OffRing;
        private float _progress;      // 0 ~ 1，1 = 全满

        // ─── 构造 ─────────────────────────────────────────────────

        /// <param name="clockRoot">
        /// Clock.uxml 的根 VisualElement。
        /// 当 Clock 通过 &lt;ui:Instance&gt; 嵌入时，传入 TemplateContainer 本身即可：
        /// <code>var ppClock = root.Q&lt;TemplateContainer&gt;("pp-clock");</code>
        /// </param>
        public ClockView(VisualElement clockRoot)
        {
            if (clockRoot == null)
            {
                throw new ArgumentNullException(nameof(clockRoot));
            }

            _ring       = clockRoot.Q<VisualElement>("clock-ring");
            _timeLabel  = clockRoot.Q<Label>("clock-time");
            _stateLabel = clockRoot.Q<Label>("clock-state");

            if (_ring != null)
            {
                _ring.generateVisualContent += DrawRing;
            }

            // 初始渲染：idle 状态
            ApplyTheme(OffRing, OffRing, OffValue, OffLabel, "未开始");
        }

        // ─── 公开 API ─────────────────────────────────────────────

        /// <summary>
        /// 刷新时钟视图。
        /// </summary>
        /// <param name="displayState">显示状态（由父级 Controller 根据 Phase + IsRunning 决定）</param>
        /// <param name="remainingSeconds">当前阶段剩余秒数</param>
        /// <param name="phaseTotalSeconds">当前阶段总秒数（用于计算进度）</param>
        public void Refresh(ClockDisplayState displayState, int remainingSeconds, int phaseTotalSeconds)
        {
            _progress = phaseTotalSeconds > 0
                ? Mathf.Clamp01(1f - (float)remainingSeconds / phaseTotalSeconds)
                : 0f;

            UpdateTimeLabel(remainingSeconds);

            switch (displayState)
            {
                case ClockDisplayState.Focus:
                    ApplyTheme(FocusTrack, FocusProgress, FocusValue, FocusLabel, "专注中");
                    break;
                case ClockDisplayState.Rest:
                    ApplyTheme(RestTrack, RestProgress, RestValue, RestLabel, "休息中");
                    break;
                case ClockDisplayState.Paused:
                    ApplyTheme(PausedTrack, PausedProgress, PausedValue, PausedLabel, "暂停中");
                    break;
                case ClockDisplayState.Completed:
                    _progress = 0f;
                    ApplyTheme(OffRing, OffRing, OffValue, OffLabel, "已完成");
                    break;
                default: // Idle
                    _progress = 0f;
                    ApplyTheme(OffRing, OffRing, OffValue, OffLabel, "未开始");
                    break;
            }

            _ring?.MarkDirtyRepaint();
        }

        /// <summary>
        /// 将 PomodoroPhase + IsRunning 映射为 ClockDisplayState 的便捷方法。
        /// </summary>
        public static ClockDisplayState ResolveState(PomodoroPhase phase, bool isRunning)
        {
            return phase switch
            {
                PomodoroPhase.Focus     => isRunning ? ClockDisplayState.Focus     : ClockDisplayState.Paused,
                PomodoroPhase.Break     => isRunning ? ClockDisplayState.Rest      : ClockDisplayState.Paused,
                PomodoroPhase.Completed => ClockDisplayState.Completed,
                _                       => ClockDisplayState.Idle,
            };
        }

        // ─── 私有辅助 ─────────────────────────────────────────────

        private void ApplyTheme(Color track, Color progress, Color valueColor, Color labelColor, string stateText)
        {
            _trackColor    = track;
            _progressColor = progress;

            if (_timeLabel  != null) _timeLabel.style.color  = new StyleColor(valueColor);
            if (_stateLabel != null)
            {
                _stateLabel.style.color = new StyleColor(labelColor);
                _stateLabel.text        = stateText;
            }
        }

        private void UpdateTimeLabel(int totalSeconds)
        {
            if (_timeLabel == null)
            {
                return;
            }

            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            _timeLabel.text = $"{m:00}:{s:00}";
        }

        // ─── Painter2D 进度弧绘制 ────────────────────────────────

        private void DrawRing(MeshGenerationContext ctx)
        {
            Rect rect = _ring.contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Painter2D p  = ctx.painter2D;
            float cx     = rect.width  * 0.5f;
            float cy     = rect.height * 0.5f;
            float radius = Mathf.Min(cx, cy) - RingThickness * 0.5f;

            p.lineWidth = RingThickness;
            p.lineCap   = LineCap.Butt;

            // 1. 轨道环：完整 360°
            p.strokeColor = _trackColor;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius, ArcStartDeg, ArcStartDeg + 360f);
            p.Stroke();

            // 2. 进度弧：从顶部顺时针，长度 = progress × 360°
            if (_progress > 0.001f)
            {
                float sweepDeg = _progress * 360f;
                p.strokeColor  = _progressColor;
                p.BeginPath();
                p.Arc(new Vector2(cx, cy), radius, ArcStartDeg, ArcStartDeg + sweepDeg, ArcDirection.Clockwise);
                p.Stroke();
            }
        }
    }
}
