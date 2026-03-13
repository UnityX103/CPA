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
            int previousIndex = model.TargetMonitorIndex.Value;
            model.TargetMonitorIndex.Value = safeIndex;

            // 仅在显示器实际切换时重新定位窗口
            if (safeIndex == previousIndex || _uwc == null)
            {
                return;
            }

            Debug.Log($"[WindowPositionSystem] MoveToMonitor({previousIndex} → {safeIndex})");
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
        /// 临时置顶窗口（不改变 Model.IsTopmost 偏好）。
        /// 用于阶段切换时吸引用户注意。
        /// </summary>
        public void JumpToScreenTop()
        {
            if (_uwc == null)
            {
                return;
            }

            _uwc.isTopmost = true;
            Debug.Log("[WindowPositionSystem] JumpToScreenTop: 临时置顶窗口");
        }

        /// <summary>
        /// 将 isTopmost 恢复为 Model 中用户的偏好值。
        /// 用于用户聚焦窗口后取消临时置顶。
        /// </summary>
        public void RevertTopmost()
        {
            if (_uwc == null)
            {
                return;
            }

            bool preferred = this.GetModel<IPomodoroModel>().IsTopmost.Value;
            _uwc.isTopmost = preferred;
            Debug.Log($"[WindowPositionSystem] RevertTopmost: 恢复置顶={preferred}");
        }
    }
}
