using QFramework;

namespace APP.Pomodoro.Model
{
    public sealed class PomodoroModel : AbstractModel, IPomodoroModel
    {
        public BindableProperty<int> FocusDurationSeconds { get; } = new BindableProperty<int>(25 * 60);
        public BindableProperty<int> BreakDurationSeconds { get; } = new BindableProperty<int>(5 * 60);
        public BindableProperty<int> TotalRounds { get; } = new BindableProperty<int>(4);
        public BindableProperty<int> CurrentRound { get; } = new BindableProperty<int>(1);
        public BindableProperty<int> RemainingSeconds { get; } = new BindableProperty<int>(25 * 60);
        public BindableProperty<PomodoroPhase> CurrentPhase { get; } =
            new BindableProperty<PomodoroPhase>(PomodoroPhase.Focus);
        public BindableProperty<bool> IsRunning { get; } = new BindableProperty<bool>(false);
        public BindableProperty<PomodoroWindowAnchor> WindowAnchor { get; } =
            new BindableProperty<PomodoroWindowAnchor>(PomodoroWindowAnchor.Bottom);
        public BindableProperty<bool> AutoJumpToTopOnComplete { get; } = new BindableProperty<bool>(true);
        public BindableProperty<int> CompletionClipIndex { get; } = new BindableProperty<int>(0);

        protected override void OnInit()
        {
            // 初始剩余时间 = 专注时长
            RemainingSeconds.Value = FocusDurationSeconds.Value;
        }
    }
}
