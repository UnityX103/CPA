using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.System
{
    public interface IWindowPositionSystem : ISystem
    {
        /// <summary>初始化：传入 UniWindowController 实例和窗口高度/边距</summary>
        void Initialize(Kirurobo.UniWindowController uwc, float windowHeight, float verticalMargin);

/// <summary>将窗口移动到指定显示器并铺满该显示器</summary>
        void MoveToMonitor(int monitorIndex);

        /// <summary>设置窗口置顶状态</summary>
        void SetTopmost(bool isTopmost);

        /// <summary>更新窗口锚点（全屏透明窗口下由 USS class 决定卡片位置，不物理移动窗口）</summary>
        void MoveTo(PomodoroWindowAnchor anchor);
    }
}
