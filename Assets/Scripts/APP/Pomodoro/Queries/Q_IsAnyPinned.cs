using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro.Queries
{
    /// <summary>
    /// 读取当前 AnyPinned 的快照（PomodoroModel.IsPinned ∥ 任一 PlayerCard.IsPinned）。
    /// 用于 Editor 调试、启动时初值读取、单元测试断言。
    /// 运行期订阅应使用 IWindowVisibilityCoordinatorSystem.AnyPinned。
    /// </summary>
    public sealed class Q_IsAnyPinned : AbstractQuery<bool>
    {
        protected override bool OnDo() =>
            this.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned.Value;
    }
}
