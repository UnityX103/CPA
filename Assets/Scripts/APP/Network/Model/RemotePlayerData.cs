using APP.Pomodoro.Model;

namespace APP.Network.Model
{
    /// <summary>
    /// 远端玩家的只读快照。
    /// BindingKeyLabel 为 null 或空字符串时表示对端没有标记同步的按键 →
    /// PlayerCardController 把 KeyCounterPill 整体隐藏。
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
        public string BindingKeyLabel;
        public int    BindingPressCount;

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
                BindingKeyLabel = BindingKeyLabel,
                BindingPressCount = BindingPressCount,
            };
        }
    }
}
