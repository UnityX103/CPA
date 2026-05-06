using QFramework;

namespace APP.Pomodoro.System
{
    /// <summary>
    /// 计时结束提示系统。
    /// 订阅阶段自然推进事件，按 <c>EndActionMode</c> 决定行为：
    /// - TopWindow：交给现有 PhaseTransitionFlash 流程，不额外动作；
    /// - PlayVideo：广播 <c>E_RequestPlayCompletionVideo</c>，由 VideoCompletionOverlay 接收。
    /// </summary>
    public interface IPomodoroEndActionSystem : ISystem
    {
    }
}
