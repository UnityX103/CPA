using APP.Pomodoro.System;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 将窗口移动到指定显示器并铺满该显示器分辨率
    /// </summary>
    public sealed class Cmd_PomodoroSetMonitor : AbstractCommand
    {
        private readonly int _monitorIndex;

        public Cmd_PomodoroSetMonitor(int monitorIndex)
        {
            _monitorIndex = Mathf.Max(0, monitorIndex);
        }

        protected override void OnExecute()
        {
            this.GetSystem<IWindowPositionSystem>().MoveToMonitor(_monitorIndex);
        }
    }
}
