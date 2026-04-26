using APP.Pomodoro.System;
using APP.Settings.Model;
using QFramework;
using UnityEngine;

namespace APP.Settings.Command
{
    /// <summary>
    /// 把 PreviewTargetDisplay 写到 SettingsModel，并触发 IWindowPositionSystem 做物理预览移动。
    /// 不写 IPomodoroModel.TargetMonitorIndex —— 提交才会落到正式持久化。
    /// </summary>
    public sealed class Cmd_SetPreviewTargetDisplay : AbstractCommand
    {
        private readonly int _index;
        public Cmd_SetPreviewTargetDisplay(int index) => _index = index;

        protected override void OnExecute()
        {
            int safe = Mathf.Max(0, _index);
            int prevPreview = this.GetModel<ISettingsModel>().PreviewTargetDisplay.Value;
            Debug.Log($"[Cmd_SetPreviewTargetDisplay] _index={_index}, safe={safe}, " +
                      $"PreviewTargetDisplay({prevPreview}→{safe})");
            this.GetModel<ISettingsModel>().PreviewTargetDisplay.Value = safe;
            this.GetSystem<IWindowPositionSystem>().PreviewMoveToMonitor(safe);
        }
    }
}
