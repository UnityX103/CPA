using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using QFramework;

namespace APP.Pomodoro
{
    /// <summary>
    /// QFramework 架构注册入口
    /// 注册顺序：Model → System（System 可以在 OnInit 中访问 Model）
    /// </summary>
    public sealed class GameApp : Architecture<GameApp>
    {
        protected override void Init()
        {
            RegisterModel<IPomodoroModel>(new PomodoroModel());
            RegisterSystem<IPomodoroTimerSystem>(new PomodoroTimerSystem());
            RegisterSystem<IWindowPositionSystem>(new WindowPositionSystem());
        }
    }
}
