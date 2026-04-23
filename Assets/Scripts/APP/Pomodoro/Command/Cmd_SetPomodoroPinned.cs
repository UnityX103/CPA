using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 切换番茄钟面板的 pin 状态（不因失焦隐藏）。
    /// </summary>
    public sealed class Cmd_SetPomodoroPinned : AbstractCommand
    {
        private readonly bool _pinned;
        public Cmd_SetPomodoroPinned(bool pinned) => _pinned = pinned;

        protected override void OnExecute() =>
            this.GetModel<IPomodoroModel>().IsPinned.Value = _pinned;
    }
}
