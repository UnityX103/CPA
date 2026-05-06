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

    /// <summary>
    /// 计时结束时执行的提示动作。Pencil 设计稿 pomoEndAction 行对应的状态枚举。
    /// </summary>
    public enum PomodoroEndActionMode
    {
        /// <summary>把窗口弹到顶部（默认；继承旧的"阶段切换自动指定窗口提示"行为）</summary>
        TopWindow = 0,
        /// <summary>播放预先选定的视频</summary>
        PlayVideo = 1,
    }
}