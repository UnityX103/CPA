using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using APP.Settings.Model;
using QFramework;
using UnityEngine;

namespace APP.Settings.Command
{
    /// <summary>
    /// 把 PreviewTargetDisplay 刷回 IPomodoroModel.TargetMonitorIndex（持久化的源），
    /// 并触发物理移动回滚。
    /// </summary>
    public sealed class Cmd_RevertTargetDisplay : AbstractCommand
    {
        protected override void OnExecute()
        {
            var settings = this.GetModel<ISettingsModel>();
            var pomo     = this.GetModel<IPomodoroModel>();

            int committed = pomo.TargetMonitorIndex.Value;
            int prevPreview = settings.PreviewTargetDisplay.Value;
            Debug.Log($"[Cmd_RevertTargetDisplay] 还原: PreviewTargetDisplay({prevPreview}→{committed}), " +
                      $"committed TargetMonitorIndex={committed}");
            settings.PreviewTargetDisplay.Value = committed;
            this.GetSystem<IWindowPositionSystem>().PreviewMoveToMonitor(committed);
        }
    }
}
