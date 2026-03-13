using QFramework;

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

        /// <summary>窗口吸附位置</summary>
        BindableProperty<PomodoroWindowAnchor> WindowAnchor { get; }

        /// <summary>在底端计时完成后是否自动跳到顶端提示</summary>
        BindableProperty<bool> AutoJumpToTopOnComplete { get; }

        /// <summary>选中的完成音效索引</summary>
        BindableProperty<int> CompletionClipIndex { get; }
    }
}
