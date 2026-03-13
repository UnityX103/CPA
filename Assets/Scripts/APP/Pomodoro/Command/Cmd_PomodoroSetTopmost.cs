using APP.Pomodoro.System;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 设置窗口置顶状态（UniWindowController.isTopmost）
    /// </summary>
    public sealed class Cmd_PomodoroSetTopmost : AbstractCommand
    {
        private readonly bool _isTopmost;

        public Cmd_PomodoroSetTopmost(bool isTopmost) => _isTopmost = isTopmost;

        protected override void OnExecute()
        {
            Debug.Log($"[Pomodoro] 窗口置顶: {_isTopmost}");
            this.GetSystem<IWindowPositionSystem>().SetTopmost(_isTopmost);
        }
    }
}
