using APP.Network.Model;
using APP.Network.System;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using APP.SessionMemory.Model;
using APP.Utility;
using QFramework;

namespace APP.Pomodoro
{
    public sealed class GameApp : Architecture<GameApp>
    {
        protected override void Init()
        {
            // Utility 必须最先注册，Model/System 的 OnInit 可能会用
            RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());

            RegisterModel<IPomodoroModel>(new PomodoroModel());
            RegisterModel<IRoomModel>(new RoomModel());
            RegisterModel<ISessionMemoryModel>(new SessionMemoryModel());
            RegisterModel<IGameModel>(new GameModel());
            RegisterModel<IPlayerCardModel>(new PlayerCardModel());

            RegisterSystem<IPomodoroTimerSystem>(new PomodoroTimerSystem());
            RegisterSystem<IWindowPositionSystem>(new WindowPositionSystem());
            RegisterSystem<IActiveAppSystem>(new ActiveAppSystem());
            RegisterSystem<IIconCacheSystem>(new IconCacheSystem());
            RegisterSystem<INetworkSystem>(new NetworkSystem());
            RegisterSystem<IStateSyncSystem>(new StateSyncSystem());
        }
    }
}
