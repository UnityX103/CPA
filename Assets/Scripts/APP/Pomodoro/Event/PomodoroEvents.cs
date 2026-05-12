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

    /// <summary>
    /// 计时自然到期引发阶段切换时广播（不包含用户手动 Reset/Skip）。
    /// 用于驱动 Flash 之类只在真实"段落结束"时触发的 UI 行为。
    /// </summary>
    public readonly struct E_PomodoroPhaseAutoAdvanced
    {
        public readonly PomodoroPhase FromPhase;
        public readonly PomodoroPhase ToPhase;

        public E_PomodoroPhaseAutoAdvanced(PomodoroPhase fromPhase, PomodoroPhase toPhase)
        {
            FromPhase = fromPhase;
            ToPhase = toPhase;
        }
    }

    /// <summary>
    /// 请求播放计时结束视频。
    /// PomodoroEndActionSystem 在阶段自然推进且 EndActionMode==PlayVideo 时发出，
    /// 由 VideoCompletionOverlay 监听并启动播放。
    /// </summary>
    public readonly struct E_RequestPlayCompletionVideo
    {
        /// <summary>本地视频绝对路径（已校验非空）</summary>
        public readonly string VideoPath;

        public E_RequestPlayCompletionVideo(string videoPath)
        {
            VideoPath = videoPath;
        }
    }

    /// <summary>
    /// 视频覆盖层 Hide 完成后广播（自然结束 / 用户点击关闭 / 错误回退都会发）。
    /// 用于让面板视图层（PomodoroPanelView 等）在视频结束的瞬间主动重算 hidden class，
    /// 防止"视频期间窗口失焦 + AnyPinned 真值"组合让面板继续挂着 pp-hidden、
    /// 直到下次 IsAppFocused/AnyPinned 自然变化才恢复——用户体感为"UI 很久才回来"。
    /// </summary>
    public readonly struct E_VideoOverlayClosed { }
}
