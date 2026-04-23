using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 切换指定玩家卡片的 pin 状态。
    /// 玩家离线时（Find 返回 null）静默 Warn 不抛异常。
    /// </summary>
    public sealed class Cmd_SetPlayerCardPinned : AbstractCommand
    {
        private readonly string _playerId;
        private readonly bool _pinned;

        public Cmd_SetPlayerCardPinned(string playerId, bool pinned)
        {
            _playerId = playerId;
            _pinned = pinned;
        }

        protected override void OnExecute()
        {
            var card = this.GetModel<IPlayerCardModel>().Find(_playerId);
            if (card == null)
            {
                Debug.LogWarning($"[Cmd_SetPlayerCardPinned] 未找到 playerId={_playerId}");
                return;
            }
            card.IsPinned.Value = _pinned;
        }
    }
}
