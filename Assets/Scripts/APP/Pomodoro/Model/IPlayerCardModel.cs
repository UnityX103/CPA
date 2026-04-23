using System.Collections.Generic;

using QFramework;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 远端玩家卡片容器 + 持久化仓库。
    /// Cards 仅含当前在线玩家的实例（X 语义）；仓库记录独立驻留 Model 内部，离线后保留供再次 Join 时恢复。
    /// 持久化 key: "CPA.PlayerCards"。
    /// </summary>
    public interface IPlayerCardModel : IModel
    {
        IReadOnlyList<IPlayerCard> Cards { get; }
        IPlayerCard Find(string playerId);
        IPlayerCard AddOrGet(string playerId);
        void Remove(string playerId);
    }
}
