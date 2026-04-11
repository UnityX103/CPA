using APP.Network.Model;
using APP.Pomodoro.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 远端玩家卡片控制器（独立 UIDocument 预制体）。
    /// 每个远程玩家对应一个 GameObject 实例，拥有独立的 UIDocument。
    /// 只读展示 RemotePlayerData，不持有业务写能力。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlayerCardController : MonoBehaviour
    {
        private static readonly string[] PhaseClasses =
        {
            "pc-phase-idle",
            "pc-phase-focus",
            "pc-phase-rest",
            "pc-phase-paused",
            "pc-phase-completed",
        };

        private UIDocument _uiDocument;
        private VisualElement _root;
        private Label _nameLabel;
        private Label _phaseLabel;
        private Label _timeLabel;
        private Label _roundsLabel;
        private Label _appLabel;

        private RemotePlayerData _pendingData;
        private bool _uiBound;

        /// <summary>该卡片对应的远端玩家 ID。</summary>
        public string PlayerId { get; private set; }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // UIDocument.rootVisualElement 在 OnEnable 时可能已就绪
            TryBindAndApply();
        }

        private void Update()
        {
            // 如果 OnEnable 时 UI 还没就绪，每帧尝试直到绑定成功
            if (!_uiBound && _pendingData != null)
            {
                TryBindAndApply();
            }

            // 绑定成功后不再需要 Update
            if (_uiBound)
            {
                enabled = false; // 停止 Update，节省性能
            }
        }

        /// <summary>
        /// 初始化卡片数据。在 Instantiate 后由 PlayerCardManager 调用。
        /// 如果 UI 尚未就绪，数据会被缓存并在就绪后自动应用。
        /// </summary>
        public void Setup(RemotePlayerData data)
        {
            if (data == null) return;

            PlayerId = data.PlayerId;
            _pendingData = data;
            TryBindAndApply();
        }

        /// <summary>根据最新远端数据刷新视图。</summary>
        public void Refresh(RemotePlayerData data)
        {
            if (data == null) return;

            _pendingData = data;

            if (_uiBound)
            {
                ApplyData(data);
            }
            // 未绑定时 _pendingData 已更新，TryBindAndApply 会在之后应用
        }

        private void TryBindAndApply()
        {
            if (_uiBound) return;
            if (!BindUI()) return;

            _uiBound = true;

            if (_pendingData != null)
            {
                ApplyData(_pendingData);
            }
        }

        private bool BindUI()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return false;

            VisualElement docRoot = _uiDocument.rootVisualElement;
            if (docRoot == null) return false;
            if (docRoot.childCount == 0) return false; // UXML 还没加载

            _root = docRoot.Q<VisualElement>(className: "pc-root") ?? docRoot;

            var dragHandle = _root.Q<Label>("drag-handle");
            if (dragHandle != null)
            {
                DraggableElement.MakeDraggable(_root, dragHandle);
            }

            _nameLabel = _root.Q<Label>("pc-name");
            _phaseLabel = _root.Q<Label>("pc-phase");
            _timeLabel = _root.Q<Label>("pc-time");
            _roundsLabel = _root.Q<Label>("pc-rounds");
            _appLabel = _root.Q<Label>("pc-app");

            return _nameLabel != null; // 至少找到一个元素才算绑定成功
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
