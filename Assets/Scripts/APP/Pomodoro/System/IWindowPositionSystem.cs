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

        /// <summary>更新窗口锚点并物理移动窗口到屏幕顶端或底端</summary>
        void MoveTo(PomodoroWindowAnchor anchor);

        /// <summary>仅物理移动窗口到屏幕顶端（不改变 CSS 锚点）</summary>
        void JumpToScreenTop();
    }
}
