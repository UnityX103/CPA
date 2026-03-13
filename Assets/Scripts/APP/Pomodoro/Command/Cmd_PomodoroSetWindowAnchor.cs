using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 切换窗口吸附到屏幕顶端或底端
    /// </summary>
    public sealed class Cmd_PomodoroSetWindowAnchor : AbstractCommand
    {
        private readonly PomodoroWindowAnchor _anchor;

        public Cmd_PomodoroSetWindowAnchor(PomodoroWindowAnchor anchor) => _anchor = anchor;

        protected override void OnExecute() =>
            this.GetSystem<IWindowPositionSystem>().MoveTo(_anchor);
    }
}
