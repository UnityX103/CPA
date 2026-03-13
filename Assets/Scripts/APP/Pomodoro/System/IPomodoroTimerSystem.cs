using QFramework;

namespace APP.Pomodoro.System
{
    public interface IPomodoroTimerSystem : ISystem
    {
        /// <summary>开始或恢复计时</summary>
        void StartTimer();

        /// <summary>暂停计时（保留进度）</summary>
        void PauseTimer();

        /// <summary>重置当前阶段进度，恢复到初始专注状态</summary>
        void ResetCycle();

        /// <summary>每帧由 Controller 驱动，推进倒计时</summary>
        void Tick(float deltaTime);

        /// <summary>应用设置面板中填写的参数并选择性重置进度</summary>
        void ApplySettings(int focusSeconds, int breakSeconds, int totalRounds, bool resetProgress);
    }
}
