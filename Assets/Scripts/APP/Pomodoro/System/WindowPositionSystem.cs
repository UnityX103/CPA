using APP.Pomodoro.Model;
using Kirurobo;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.System
{
    public sealed class WindowPositionSystem : AbstractSystem, IWindowPositionSystem
    {
        private UniWindowController _uwc;
        private float _windowHeight;
        private float _verticalMargin;

        protected override void OnInit() { }

        public void Initialize(UniWindowController uwc, float windowHeight, float verticalMargin)
        {
            _uwc = uwc;
            _windowHeight = windowHeight;
            _verticalMargin = verticalMargin;
            Debug.Log($"[WindowPositionSystem] 初始化完成: windowHeight={windowHeight}, verticalMargin={verticalMargin}");
        }

        public void MoveToMonitor(int monitorIndex)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            int monitorCount = UniWindowController.GetMonitorCount();

            if (monitorCount <= 0)
            {
                model.TargetMonitorIndex.Value = 0;
                return;
            }

            int safeIndex = Mathf.Clamp(monitorIndex, 0, monitorCount - 1);
            model.TargetMonitorIndex.Value = safeIndex;

            if (_uwc == null)
            {
                Debug.LogWarning("[WindowPositionSystem] UniWindowController 未初始化，跳过显示器切换。");
                return;
            }

            // 切换显示器后，按当前锚点重新定位
            Debug.Log($"[WindowPositionSystem] MoveToMonitor({safeIndex})");
            MoveTo(model.WindowAnchor.Value);
        }

        public void SetTopmost(bool isTopmost)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.IsTopmost.Value = isTopmost;

            if (_uwc != null)
            {
                _uwc.isTopmost = isTopmost;
                Debug.Log($"[WindowPositionSystem] SetTopmost({isTopmost})");
            }
        }

        /// <summary>
        /// 物理移动窗口到屏幕顶端或底端（不修改 CSS 布局）。
        /// </summary>
        public void MoveTo(PomodoroWindowAnchor anchor)
        {
            if (_uwc == null)
            {
                Debug.LogWarning("[WindowPositionSystem] UniWindowController 未初始化，跳过物理定位。");
                return;
            }

            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            int monitorIndex = model.TargetMonitorIndex.Value;
            int monitorCount = UniWindowController.GetMonitorCount();
            if (monitorCount <= 0)
            {
                return;
            }

            int safeIndex = Mathf.Clamp(monitorIndex, 0, monitorCount - 1);
            Rect monitorRect = UniWindowController.GetMonitorRect(safeIndex);
            if (monitorRect.width <= 0f || monitorRect.height <= 0f)
            {
                Debug.LogWarning($"[WindowPositionSystem] 无法获取显示器 {safeIndex} 的有效区域。");
                return;
            }

            float x = monitorRect.x;
            float y = anchor == PomodoroWindowAnchor.Top
                ? monitorRect.y + _verticalMargin
                : monitorRect.y + monitorRect.height - _windowHeight - _verticalMargin;

            _uwc.windowPosition = new Vector2(x, y);
            _uwc.windowSize = new Vector2(monitorRect.width, _windowHeight);

            Debug.Log($"[WindowPositionSystem] MoveTo({anchor}): position=({x}, {y}), size=({monitorRect.width}, {_windowHeight})");
        }

        /// <summary>
        /// 仅物理移动窗口到屏幕顶端，不改变 Model 的 WindowAnchor（CSS 布局不变）。
        /// 用于阶段切换时的提示跳顶。
        /// </summary>
        public void JumpToScreenTop()
        {
            if (_uwc == null)
            {
                Debug.LogWarning("[WindowPositionSystem] UniWindowController 未初始化，跳过物理定位。");
                return;
            }

            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            int monitorIndex = model.TargetMonitorIndex.Value;
            int monitorCount = UniWindowController.GetMonitorCount();
            if (monitorCount <= 0)
            {
                return;
            }

            int safeIndex = Mathf.Clamp(monitorIndex, 0, monitorCount - 1);
            Rect monitorRect = UniWindowController.GetMonitorRect(safeIndex);
            if (monitorRect.width <= 0f || monitorRect.height <= 0f)
            {
                return;
            }

            float x = monitorRect.x;
            float y = monitorRect.y + _verticalMargin;

            _uwc.windowPosition = new Vector2(x, y);
            _uwc.windowSize = new Vector2(monitorRect.width, _windowHeight);

            Debug.Log($"[WindowPositionSystem] JumpToScreenTop: position=({x}, {y})");
        }
    }
}
