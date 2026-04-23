using System.Collections.Generic;
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.Queries
{
    /// <summary>
    /// 列出当前在线的所有玩家卡片实例（只读）。
    /// 供 Editor 调试窗口等不在 Architecture 内的消费方使用。
    /// </summary>
    public sealed class Q_ListPlayerCards : AbstractQuery<IReadOnlyList<IPlayerCard>>
    {
        protected override IReadOnlyList<IPlayerCard> OnDo() =>
            this.GetModel<IPlayerCardModel>().Cards;
    }
}
