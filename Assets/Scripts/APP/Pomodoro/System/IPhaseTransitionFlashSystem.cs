using QFramework;

namespace APP.Pomodoro.System
{
    /// <summary>
    /// 番茄钟阶段切换 Flash 协调器。
    /// 当阶段自然到期（Focus↔Break）且番茄钟未 pin 时进入 Flash 状态：
    /// 强制番茄钟面板可见，并（由 WindowVisibilityCoordinatorSystem）保证窗口置顶。
    /// 用户点击任意位置后由外部调用 <see cref="Dismiss"/> 退出 Flash，恢复默认可见性与置顶策略。
    /// </summary>
    public interface IPhaseTransitionFlashSystem : ISystem
    {
        /// <summary>是否正处于阶段切换 Flash 状态。</summary>
        IReadonlyBindableProperty<bool> IsFlashing { get; }

        /// <summary>退出 Flash 状态（用户点击任意位置时由 Controller 调用）。</summary>
        void Dismiss();
    }
}
