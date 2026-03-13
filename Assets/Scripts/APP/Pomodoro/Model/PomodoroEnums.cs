namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 番茄钟当前阶段
    /// </summary>
    public enum PomodoroPhase
    {
        /// <summary>专注计时中</summary>
        Focus = 0,
        /// <summary>休息计时中</summary>
        Break = 1,
        /// <summary>全部轮次已完成</summary>
        Completed = 2,
    }

    /// <summary>
    /// 悬浮窗口的屏幕锚点
    /// </summary>
    public enum PomodoroWindowAnchor
    {
        /// <summary>吸附到屏幕顶端</summary>
        Top = 0,
        /// <summary>吸附到屏幕底端</summary>
        Bottom = 1,
    }
}