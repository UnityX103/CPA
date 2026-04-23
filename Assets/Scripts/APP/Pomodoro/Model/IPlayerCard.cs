using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 单个远端玩家卡片的运行时状态实例。
    /// PlayerId 终身不变；Position 与 IsPinned 以 BindableProperty 向外提供订阅。
    /// </summary>
    public interface IPlayerCard
    {
        string PlayerId { get; }
        BindableProperty<Vector2> Position { get; }
        BindableProperty<bool> IsPinned { get; }
    }
}
