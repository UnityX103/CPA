using APP.Pomodoro.Model;
using APP.Settings.Model;
using QFramework;
using UnityEngine;

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
            int from = pomo.TargetMonitorIndex.Value;
            int to   = settings.PreviewTargetDisplay.Value;
            Debug.Log($"[Cmd_CommitTargetDisplay] TargetMonitorIndex({from}→{to})");
            pomo.TargetMonitorIndex.Value = to;
        }
    }
}
