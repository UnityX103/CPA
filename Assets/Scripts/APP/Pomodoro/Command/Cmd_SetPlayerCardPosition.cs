using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>按 PlayerId 写入玩家卡片在主面板坐标系内的左上角位置。</summary>
    public sealed class Cmd_SetPlayerCardPosition : AbstractCommand
    {
        private readonly string _playerId;
        private readonly Vector2 _position;

        public Cmd_SetPlayerCardPosition(string playerId, Vector2 position)
        {
            _playerId = playerId;
            _position = position;
        }

        protected override void OnExecute()
        {
            this.GetModel<IPlayerCardPositionModel>().Set(_playerId, _position);
        }
    }
}
