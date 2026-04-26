using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.System
{
    public interface IWindowPositionSystem : ISystem
    {
        /// <summary>初始化：传入 UniWindowController 实例和窗口高度/边距</summary>
        void Initialize(Kirurobo.UniWindowController uwc, float windowHeight, float verticalMargin);

        /// <summary>将窗口移动到指定显示器并铺满该显示器（同时写入 IPomodoroModel.TargetMonitorIndex）</summary>
        void MoveToMonitor(int monitorIndex);

        /// <summary>
        /// 仅做物理移动 + resize，不写 IPomodoroModel.TargetMonitorIndex。
        /// 用于"目标显示器预览"：在用户确认前不污染已持久化的状态。
        /// </summary>
        void PreviewMoveToMonitor(int monitorIndex);

        /// <summary>设置窗口置顶状态</summary>
        void SetTopmost(bool isTopmost);
    }
}
