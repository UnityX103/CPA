using QFramework;

namespace APP.Pomodoro.System
{
    /// <summary>
    /// 窗口可见性协调器：聚合 IPomodoroModel.IsPinned 与所有在线 IPlayerCard.IsPinned，
    /// 派生出 AnyPinned 标志，并在其变化时驱动 IWindowPositionSystem.SetTopmost。
    /// 对外只暴露只读快照，不允许外部直接写入。
    /// </summary>
    public interface IWindowVisibilityCoordinatorSystem : ISystem
    {
        /// <summary>
        /// 当前是否存在任何 pinned 的 UI。
        /// 由 PomodoroModel.IsPinned ∥ 任一 PlayerCard.IsPinned 派生。
        /// </summary>
        IReadOnlyBindableProperty<bool> AnyPinned { get; }
    }
}
