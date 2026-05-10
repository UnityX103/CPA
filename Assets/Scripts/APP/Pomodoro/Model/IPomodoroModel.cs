using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    public interface IPomodoroModel : IModel
    {
        /// <summary>专注时长（秒）</summary>
        BindableProperty<int> FocusDurationSeconds { get; }

        /// <summary>休息时长（秒）</summary>
        BindableProperty<int> BreakDurationSeconds { get; }

        /// <summary>总轮次数</summary>
        BindableProperty<int> TotalRounds { get; }

        /// <summary>当前进行中的轮次（1-based）</summary>
        BindableProperty<int> CurrentRound { get; }

        /// <summary>当前阶段剩余秒数</summary>
        BindableProperty<int> RemainingSeconds { get; }

        /// <summary>当前阶段</summary>
        BindableProperty<PomodoroPhase> CurrentPhase { get; }

        /// <summary>是否正在计时</summary>
        BindableProperty<bool> IsRunning { get; }

        /// <summary>番茄钟面板是否被 pin（不因失焦隐藏）。默认 false。</summary>
        BindableProperty<bool> IsPinned { get; }

        /// <summary>窗口吸附位置</summary>
        BindableProperty<PomodoroWindowAnchor> WindowAnchor { get; }

        /// <summary>在底端计时完成后是否自动跳到顶端提示</summary>
        BindableProperty<bool> AutoJumpToTopOnComplete { get; }

        /// <summary>专注结束后是否自动开始休息</summary>
        BindableProperty<bool> AutoStartBreak { get; }

        /// <summary>目标显示器索引（0-based）</summary>
        BindableProperty<int> TargetMonitorIndex { get; }

        /// <summary>选中的完成音效索引</summary>
        BindableProperty<int> CompletionClipIndex { get; }

        /// <summary>
        /// 计时结束时执行的提示动作。
        /// 对应番茄设置面板"计时结束提示"行（Pencil pomoEndAction）。
        /// 默认 <see cref="PomodoroEndActionMode.TopWindow"/>。
        /// </summary>
        BindableProperty<PomodoroEndActionMode> EndActionMode { get; }

        /// <summary>
        /// 当 <see cref="EndActionMode"/> 为 <see cref="PomodoroEndActionMode.PlayVideo"/> 时，
        /// 计时结束应播放的本地视频绝对路径。空串表示尚未选择，此时回退到 TopWindow。
        /// 仅在 <see cref="EndActionVideoIndex"/> == -1（用户选了"自定义"）时生效。
        /// </summary>
        BindableProperty<string> EndActionVideoPath { get; }

        /// <summary>
        /// 当前选中的内置视频在 PomodoroBuiltInVideosConfig.Entries 里的下标（0-based）。
        /// -1 表示用户在"视频选择"下拉里选了"自定义"，此时改用 <see cref="EndActionVideoPath"/>。
        /// 默认 0。在 <see cref="EndActionMode"/> = PlayVideo 且非 -1 时被使用。
        /// </summary>
        BindableProperty<int> EndActionVideoIndex { get; }

        /// <summary>
        /// 番茄钟面板（YRqeB）左上角相对主面板（父容器）的归一化位置，x/y ∈ [0, 1]。
        /// 0 = 父容器左/上边；1 = 父容器右/下边。View 在 GeometryChanged 时根据当前父容器尺寸换算成像素应用。
        /// 值为 Vector2.negativeInfinity 时代表"未初始化"——View 首帧计算默认右下角锚点后回写比例。
        /// 兼容旧版本持久化：若读出的值 > 1 统一 clamp 到 1；&lt; 0 clamp 到 0。
        /// </summary>
        BindableProperty<Vector2> PomodoroPanelPosition { get; }
    }
}
