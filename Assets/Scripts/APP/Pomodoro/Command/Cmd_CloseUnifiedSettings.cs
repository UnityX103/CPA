using APP.Pomodoro.Event;
using QFramework;

namespace APP.Pomodoro.Command
{
    public sealed class Cmd_CloseUnifiedSettings : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.SendEvent<E_CloseUnifiedSettings>();
        }
    }
}
