using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.System
{
    public sealed class WindowPositionSystem : AbstractSystem, IWindowPositionSystem
    {
        private Kirurobo.UniWindowController _uwc;
        private float _windowHeight;
        private float _verticalMargin;

        protected override void OnInit() { }

        public void Initialize(Kirurobo.UniWindowController uwc, float windowHeight, float verticalMargin)
        {
            _uwc = uwc;
            _windowHeight = windowHeight;
            _verticalMargin = verticalMargin;
        }

        public void MoveTo(PomodoroWindowAnchor anchor)
        {
            if (_uwc == null)
            {
                Debug.LogWarning("[WindowPositionSystem] UniWindowController 未初始化，跳过位置设置。");
                return;
            }

            // 获取主显示器分辨率（逻辑像素）
            int screenHeight = Screen.currentResolution.height;
            int screenWidth = Screen.currentResolution.width;

            // 保持窗口水平居中（X 不变，或按需调整）
            float posX = _uwc.windowPosition.x;

            float posY;
            if (anchor == PomodoroWindowAnchor.Top)
            {
                // macOS 坐标原点在左下角，Y 向上增大
                // 顶端：屏幕高度 - 窗口高度 - 边距
                posY = screenHeight - _windowHeight - _verticalMargin;
            }
            else
            {
                // 底端：仅留边距
                posY = _verticalMargin;
            }

            _uwc.windowPosition = new Vector2(posX, posY);

            // 同步 Model
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.WindowAnchor.Value = anchor;
        }
    }
}
