using APP.Pomodoro.Model;
using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>
    /// 把 PreviewTargetDisplay 提交为正式值。
    /// 持久化复用 IPomodoroModel.TargetMonitorIndex（已通过 PomodoroPersistence 自动落盘）。
    /// </summary>
    public sealed class Cmd_CommitTargetDisplay : AbstractCommand
    {
        protected override void OnExecute()
        {
            var settings = this.GetModel<ISettingsModel>();
            var pomo     = this.GetModel<IPomodoroModel>();
            pomo.TargetMonitorIndex.Value = settings.PreviewTargetDisplay.Value;
        }
    }
}
