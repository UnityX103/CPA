using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>写入番茄钟面板在主面板坐标系内的左上角位置。</summary>
    public sealed class Cmd_SetPomodoroPanelPosition : AbstractCommand
    {
        private readonly Vector2 _position;

        public Cmd_SetPomodoroPanelPosition(Vector2 position) => _position = position;

        protected override void OnExecute()
        {
            this.GetModel<IPomodoroModel>().PomodoroPanelPosition.Value = _position;
        }
    }
}
