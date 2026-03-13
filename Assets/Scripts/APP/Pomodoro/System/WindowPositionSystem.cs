using APP.Pomodoro.Model;
using Kirurobo;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.System
{
    public sealed class WindowPositionSystem : AbstractSystem, IWindowPositionSystem
    {
        private UniWindowController _uwc;

        protected override void OnInit() { }

        public void Initialize(UniWindowController uwc, float windowHeight, float verticalMargin)
        {
            _uwc = uwc;
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

            Rect monitorRect = UniWindowController.GetMonitorRect(safeIndex);
            if (monitorRect.width <= 0f || monitorRect.height <= 0f)
            {
                Debug.LogWarning($"[WindowPositionSystem] 无法获取显示器 {safeIndex} 的有效区域。");
                return;
            }

            _uwc.windowPosition = monitorRect.position;
            _uwc.windowSize = monitorRect.size;
        }

        public void SetTopmost(bool isTopmost)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.IsTopmost.Value = isTopmost;

            if (_uwc != null)
            {
                _uwc.isTopmost = isTopmost;
            }
        }

        /// <summary>
        /// 全屏透明窗口下不再物理移动窗口，仅更新 Model。
        /// 卡片顶/底吸附由 USS class "anchor-bottom" 控制。
        /// </summary>
        public void MoveTo(PomodoroWindowAnchor anchor)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.WindowAnchor.Value = anchor;
        }
    }
}
