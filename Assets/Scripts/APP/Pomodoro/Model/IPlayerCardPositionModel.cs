using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 按 PlayerId 存储玩家卡片在主面板内的左上角位置。
    /// 持久化到 PlayerPrefs key "CPA.PlayerCardPositions"（JSON 格式的 entries 数组）。
    /// </summary>
    public interface IPlayerCardPositionModel : IModel
    {
        bool TryGet(string playerId, out Vector2 position);
        void Set(string playerId, Vector2 position);
        void Remove(string playerId);
    }
}
