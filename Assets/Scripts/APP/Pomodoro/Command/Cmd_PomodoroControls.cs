using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro.Command
{
    public sealed class Cmd_PomodoroStart : AbstractCommand
    {
        protected override void OnExecute() =>
            this.GetSystem<IPomodoroTimerSystem>().StartTimer();
    }

    public sealed class Cmd_PomodoroPause : AbstractCommand
    {
        protected override void OnExecute() =>
            this.GetSystem<IPomodoroTimerSystem>().PauseTimer();
    }

    public sealed class Cmd_PomodoroReset : AbstractCommand
    {
        protected override void OnExecute() =>
            this.GetSystem<IPomodoroTimerSystem>().ResetCycle();
    }

    public sealed class Cmd_PomodoroTick : AbstractCommand
    {
        private readonly float _deltaTime;

        public Cmd_PomodoroTick(float deltaTime) => _deltaTime = deltaTime;

        protected override void OnExecute() =>
            this.GetSystem<IPomodoroTimerSystem>().Tick(_deltaTime);
    }
}
