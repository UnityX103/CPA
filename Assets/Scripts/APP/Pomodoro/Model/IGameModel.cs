using QFramework;

namespace APP.Pomodoro.Model
{
    /// <summary>
    /// 全局 Model：承载跨领域的运行时状态。
    /// 当前仅含 IsAppFocused；失焦真实数据源留待后续会话接入，
    /// 本次由 Editor 调试窗口（Tools/Model 调试器）手动赋值。
    /// </summary>
    public interface IGameModel : IModel
    {
        BindableProperty<bool> IsAppFocused { get; }
    }
}
