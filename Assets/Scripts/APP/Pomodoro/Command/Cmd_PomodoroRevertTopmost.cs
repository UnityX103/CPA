using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 将窗口置顶状态恢复为用户偏好设置值。
    /// 用于用户聚焦窗口后取消临时置顶。
    /// </summary>
    public sealed class Cmd_PomodoroRevertTopmost : AbstractCommand
    {
        protected override void OnExecute() =>
            this.GetSystem<IWindowPositionSystem>().RevertTopmost();
    }
}
