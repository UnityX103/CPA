using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 临时置顶窗口（不改变用户的置顶偏好设置）。
    /// 用于阶段切换时吸引用户注意。
    /// </summary>
    public sealed class Cmd_PomodoroJumpToScreenTop : AbstractCommand
    {
        protected override void OnExecute() =>
            this.GetSystem<IWindowPositionSystem>().JumpToScreenTop();
    }
}
