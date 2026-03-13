using APP.Pomodoro.System;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 应用设置面板中输入的参数
    /// </summary>
    public sealed class Cmd_PomodoroApplySettings : AbstractCommand
    {
        private readonly int _focusMinutes;
        private readonly int _breakMinutes;
        private readonly int _totalRounds;
        private readonly bool _resetProgress;

        public Cmd_PomodoroApplySettings(int focusMinutes, int breakMinutes, int totalRounds, bool resetProgress)
        {
            _focusMinutes = Mathf.Max(1, focusMinutes);
            _breakMinutes = Mathf.Max(0, breakMinutes);
            _totalRounds = Mathf.Max(1, totalRounds);
            _resetProgress = resetProgress;
        }

        protected override void OnExecute()
        {
            this.GetSystem<IPomodoroTimerSystem>().ApplySettings(
                _focusMinutes * 60,
                _breakMinutes * 60,
                _totalRounds,
                _resetProgress);
        }
    }
}
