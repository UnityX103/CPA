using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 写入番茄钟面板左上角相对父容器的归一化位置（x/y ∈ [0, 1]）。
    /// Command 只做范围保护（Clamp01），不关心屏幕像素。
    /// </summary>
    public sealed class Cmd_SetPomodoroPanelPosition : AbstractCommand
    {
        private readonly Vector2 _position;

        public Cmd_SetPomodoroPanelPosition(Vector2 position) => _position = position;

        protected override void OnExecute()
        {
            // NaN/Infinity 不是合法 ratio（Mathf.Clamp01(NaN) = NaN 会传染）：
            // 丢弃本次写入，保留 Model 当前值（或首次 sentinel），防止污染持久化。
            if (!float.IsFinite(_position.x) || !float.IsFinite(_position.y))
            {
                return;
            }
            Vector2 clamped = new Vector2(
                Mathf.Clamp01(_position.x),
                Mathf.Clamp01(_position.y));
            this.GetModel<IPomodoroModel>().PomodoroPanelPosition.Value = clamped;
        }
    }
}
