using APP.Pomodoro.Model;

namespace APP.Pomodoro.Event
{
    /// <summary>
    /// 番茄钟阶段发生变化（专注 → 休息 → 完成）时广播
    /// </summary>
    public readonly struct E_PomodoroPhaseChanged
    {
        public readonly PomodoroPhase Phase;
        public readonly int CurrentRound;
        public readonly int TotalRounds;

        public E_PomodoroPhaseChanged(PomodoroPhase phase, int currentRound, int totalRounds)
        {
            Phase = phase;
            CurrentRound = currentRound;
            TotalRounds = totalRounds;
        }
    }

    /// <summary>
    /// 所有轮次全部完成时广播（可触发音效/自动跳顶）
    /// </summary>
    public readonly struct E_PomodoroCycleCompleted
    {
        public readonly int CompletedRounds;

        public E_PomodoroCycleCompleted(int completedRounds)
        {
            CompletedRounds = completedRounds;
        }
    }
}
