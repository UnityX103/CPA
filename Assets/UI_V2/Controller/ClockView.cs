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
        // 与 Pencil 设计稿对应：78×78，innerRadius=0.77 → 环宽 9px
        private const float RingThickness = 9f;
        private const float ArcStartDeg   = -90f; // 从顶部（12 点钟）开始

        // ─── Painter2D 环色（非样式，属于 2D 绘制参数，保留在 C# 中）─
        // focus: 橙红
        private static readonly Color FocusTrack    = new Color32(0xFF, 0xE5, 0xD9, 0xFF); // #FFE5D9
        private static readonly Color FocusProgress = new Color32(0xD1, 0x5F, 0x3D, 0xFF); // #D15F3D

        // rest: 绿色
        private static readonly Color RestTrack    = new Color32(0xC8, 0xED, 0xD5, 0xFF); // #C8EDD5
        private static readonly Color RestProgress = new Color32(0x34, 0xA8, 0x53, 0xFF); // #34A853

        // off / idle / completed: 灰色（track/progress 同色以等效不显示进度）
        private static readonly Color OffRing = new Color32(0xE0, 0xD9, 0xD3, 0xFF); // #E0D9D3

        // paused: 琥珀
        private static readonly Color PausedTrack    = new Color32(0xFF, 0xF0, 0xD4, 0xFF); // #FFF0D4
        private static readonly Color PausedProgress = new Color32(0xF5, 0xA6, 0x23, 0xFF); // #F5A623

        // ─── 状态 class（文字颜色由 Clock.uss 维护）──────────────
        private const string StateIdle      = "state-idle";
        private const string StateFocus     = "state-focus";
        private const string StateRest      = "state-rest";
        private const string StatePaused    = "state-paused";
        private const string StateCompleted = "state-completed";

        // ─── UXML 元素引用 ────────────────────────────────────────
        private readonly VisualElement _clockRoot;
        private readonly VisualElement _ring;
        private readonly Label         _timeLabel;
        private readonly Label         _stateLabel;

        // ─── 绘制状态（由 Refresh 写入，generateVisualContent 读取）─
        private Color _trackColor    = OffRing;
        private Color _progressColor = OffRing;
        private float _progress;      // 0 ~ 1，1 = 全满

        // 当前应用的状态 class（用于增量切换，避免重复 Add/Remove）
        private string _currentStateClass;

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

            // Clock.uxml 根是 name="clock-root"；当通过 <ui:Instance> 嵌入时，
            // 传入的是外层 TemplateContainer，需要再查一层。
            _clockRoot  = clockRoot.Q<VisualElement>("clock-root") ?? clockRoot;
            _ring       = clockRoot.Q<VisualElement>("clock-ring");
            _timeLabel  = clockRoot.Q<Label>("clock-time");
            _stateLabel = clockRoot.Q<Label>("clock-state");

            if (_ring != null)
            {
                _ring.generateVisualContent += DrawRing;
            }

            // 初始渲染：idle 状态（仅切换 class，不直接写样式）
            ApplyState(StateIdle, OffRing, OffRing, "未开始");
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
                    ApplyState(StateFocus, FocusTrack, FocusProgress, "专注中");
                    break;
                case ClockDisplayState.Rest:
                    ApplyState(StateRest, RestTrack, RestProgress, "休息中");
                    break;
                case ClockDisplayState.Paused:
                    ApplyState(StatePaused, PausedTrack, PausedProgress, "暂停中");
                    break;
                case ClockDisplayState.Completed:
                    _progress = 0f;
                    ApplyState(StateCompleted, OffRing, OffRing, "已完成");
                    break;
                default: // Idle
                    _progress = 0f;
                    ApplyState(StateIdle, OffRing, OffRing, "未开始");
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

        /// <summary>
        /// 切换到新的显示状态：
        ///   1) Painter2D 环色（绘制参数，非样式）
        ///   2) state-* class（文字颜色由 Clock.uss 维护）
        ///   3) 状态文字内容
        /// </summary>
        private void ApplyState(string stateClass, Color track, Color progress, string stateText)
        {
            _trackColor    = track;
            _progressColor = progress;

            SetStateClass(stateClass);

            if (_stateLabel != null)
            {
                _stateLabel.text = stateText;
            }
        }

        private void SetStateClass(string newStateClass)
        {
            if (_clockRoot == null || _currentStateClass == newStateClass)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_currentStateClass))
            {
                _clockRoot.RemoveFromClassList(_currentStateClass);
            }

            _clockRoot.AddToClassList(newStateClass);
            _currentStateClass = newStateClass;
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
