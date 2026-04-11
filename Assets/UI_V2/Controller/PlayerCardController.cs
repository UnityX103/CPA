using APP.Network.Model;
using APP.Pomodoro.Model;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 远端玩家卡片控制器（嵌入式 VisualElement）。
    /// 每个远程玩家对应一个从 PlayerCard.uxml CloneTree 得到的 VisualElement。
    /// 只读展示 RemotePlayerData，不持有业务写能力。
    /// </summary>
    public sealed class PlayerCardController
    {
        private static readonly string[] PhaseClasses =
        {
            "pc-phase-idle",
            "pc-phase-focus",
            "pc-phase-rest",
            "pc-phase-paused",
            "pc-phase-completed",
        };

        private readonly VisualElement _root;
        private Label _nameLabel;
        private Label _phaseLabel;
        private Label _timeLabel;
        private Label _roundsLabel;
        private Label _appLabel;

        /// <summary>该卡片对应的远端玩家 ID。</summary>
        public string PlayerId { get; private set; }

        /// <summary>卡片根节点（.pc-root）。</summary>
        public VisualElement Root => _root;

        public PlayerCardController(VisualElement root)
        {
            _root = root;
            BindUI();
        }

        /// <summary>
        /// 初始化卡片数据。在 CloneTree 后由 PlayerCardManager 调用。
        /// </summary>
        public void Setup(RemotePlayerData data)
        {
            if (data == null) return;

            PlayerId = data.PlayerId;
            ApplyData(data);
        }

        /// <summary>根据最新远端数据刷新视图。</summary>
        public void Refresh(RemotePlayerData data)
        {
            if (data == null) return;

            ApplyData(data);
        }

        private void BindUI()
        {
            _nameLabel = _root.Q<Label>("pc-name");
            _phaseLabel = _root.Q<Label>("pc-phase");
            _timeLabel = _root.Q<Label>("pc-time");
            _roundsLabel = _root.Q<Label>("pc-rounds");
            _appLabel = _root.Q<Label>("pc-app");
        }

        private void ApplyData(RemotePlayerData data)
        {
            if (_nameLabel != null)
                _nameLabel.text = string.IsNullOrEmpty(data.PlayerName) ? "玩家" : data.PlayerName;

            if (_phaseLabel != null)
                _phaseLabel.text = PlayerCardView.FormatPhase(data.Phase, data.IsRunning);

            if (_timeLabel != null)
                _timeLabel.text = PlayerCardView.FormatTime(data.RemainingSeconds);

            if (_roundsLabel != null)
                _roundsLabel.text = $"{data.CurrentRound}/{data.TotalRounds}";

            if (_appLabel != null)
                _appLabel.text = string.IsNullOrEmpty(data.ActiveAppName) ? "—" : data.ActiveAppName;

            ApplyPhaseClass(data.Phase, data.IsRunning);
        }

        private void ApplyPhaseClass(PomodoroPhase phase, bool isRunning)
        {
            if (_root == null) return;

            string target = PlayerCardView.GetPhaseClass(phase, isRunning);
            for (int i = 0; i < PhaseClasses.Length; i++)
            {
                string cls = PhaseClasses[i];
                if (cls == target)
                {
                    if (!_root.ClassListContains(cls))
                        _root.AddToClassList(cls);
                }
                else
                {
                    _root.RemoveFromClassList(cls);
                }
            }
        }
    }
}
