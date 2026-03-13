using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 仅物理移动窗口到屏幕顶端（不改变 CSS 布局锚点）。
    /// 用于阶段切换时的提示跳顶。
    /// </summary>
    public sealed class Cmd_PomodoroJumpToScreenTop : AbstractCommand
    {
        protected override void OnExecute() =>
            this.GetSystem<IWindowPositionSystem>().JumpToScreenTop();
    }
}
