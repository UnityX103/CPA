using APP.Pomodoro.Model;

namespace APP.Network.Model
{
    /// <summary>
    /// 远端玩家的只读快照。
    /// </summary>
    public sealed class RemotePlayerData
    {
        public string PlayerId;
        public string PlayerName;
        public PomodoroPhase Phase;
        public int RemainingSeconds;
        public int CurrentRound;
        public int TotalRounds;
        public bool IsRunning;
        public string ActiveAppName;
        public string ActiveAppBundleId;
        public string ActiveAppIconId;

        public RemotePlayerData Clone()
        {
            return new RemotePlayerData
            {
                PlayerId = PlayerId,
                PlayerName = PlayerName,
                Phase = Phase,
                RemainingSeconds = RemainingSeconds,
                CurrentRound = CurrentRound,
                TotalRounds = TotalRounds,
                IsRunning = IsRunning,
                ActiveAppName = ActiveAppName,
                ActiveAppBundleId = ActiveAppBundleId,
                ActiveAppIconId = ActiveAppIconId,
            };
        }
    }
}
