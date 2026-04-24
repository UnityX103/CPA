using System.Collections.Generic;
using APP.Network.Model;
using APP.Network.System;
using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 远端玩家卡片控制器（嵌入式 VisualElement）。
    /// 每个远程玩家对应一个从 PlayerCard.uxml CloneTree 得到的 VisualElement。
    /// 只读展示 RemotePlayerData，不持有业务写能力。
    /// </summary>
    public sealed class PlayerCardController : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private static readonly string[] PhaseClasses =
        {
            "pc-phase-idle",
            "pc-phase-focus",
            "pc-phase-rest",
            "pc-phase-paused",
            "pc-phase-completed",
        };

        // 阶段文字自动缩放：基准字号 10px，超过 3 个字符时按比例缩小，最小 7px
        private const int PhaseBaseFontSize = 10;
        private const int PhaseMinFontSize = 7;
        private const int PhaseMaxCharsAtBaseSize = 3;

        private readonly VisualElement _root;
        private Label _nameLabel;
        private Label _phaseLabel;
        private Label _timeLabel;
        private Label _roundsLabel;
        private Label _appLabel;
        private VisualElement _appIcon;
        private IPlayerCard _card;
        private VisualElement _pinBtn;
        private readonly List<IUnRegister> _unRegisters = new List<IUnRegister>();

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
            _appIcon = _root.Q<VisualElement>("pc-active-app-icon");
            _pinBtn = _root.Q<VisualElement>("pc-pin-btn");
            if (_pinBtn != null)
            {
                _pinBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                _pinBtn.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (_card == null) return;
                    this.SendCommand(new Cmd_SetPlayerCardPinned(_card.PlayerId, !_card.IsPinned.Value));
                });
            }
        }

        private void ApplyData(RemotePlayerData data)
        {
            if (_nameLabel != null)
                _nameLabel.text = string.IsNullOrEmpty(data.PlayerName) ? "玩家" : data.PlayerName;

            if (_phaseLabel != null)
            {
                string phaseText = PlayerCardView.FormatPhase(data.Phase, data.IsRunning);
                _phaseLabel.text = phaseText;
                AutoSizePhaseLabel(phaseText);
            }

            if (_timeLabel != null)
                _timeLabel.text = PlayerCardView.FormatTime(data.RemainingSeconds);

            if (_roundsLabel != null)
                _roundsLabel.text = $"{data.CurrentRound}/{data.TotalRounds} 轮";

            if (_appLabel != null)
                _appLabel.text = string.IsNullOrEmpty(data.ActiveAppName) ? "—" : data.ActiveAppName;

            ApplyAppIcon(data.ActiveAppBundleId);

            ApplyPhaseClass(data.Phase, data.IsRunning);
        }

        private void ApplyAppIcon(string bundleId)
        {
            if (_appIcon == null) return;

            Texture2D tex = null;
            if (!string.IsNullOrEmpty(bundleId))
            {
                IIconCacheSystem iconCache = GameApp.Interface.GetSystem<IIconCacheSystem>();
                tex = iconCache?.GetTexture(bundleId);
            }

            // tex 为空时用 Null 清掉 inline，回落到 USS 默认 app-window 图标；
            // 用 None 会显式置空，覆盖 USS 默认导致图标消失
            _appIcon.style.backgroundImage = tex != null
                ? new StyleBackground(tex)
                : new StyleBackground(StyleKeyword.Null);
        }

        /// <summary>
        /// 根据文字长度自动缩小阶段标签字号，防止撑大 badge。
        /// 3 个字符以内保持 10px；超出按比例缩小，最小 7px。
        /// </summary>
        private void AutoSizePhaseLabel(string text)
        {
            if (_phaseLabel == null) return;

            int len = string.IsNullOrEmpty(text) ? 0 : text.Length;
            if (len <= PhaseMaxCharsAtBaseSize)
            {
                _phaseLabel.style.fontSize = PhaseBaseFontSize;
            }
            else
            {
                float ratio = (float)PhaseMaxCharsAtBaseSize / len;
                int size = Mathf.Max(Mathf.RoundToInt(PhaseBaseFontSize * ratio), PhaseMinFontSize);
                _phaseLabel.style.fontSize = size;
            }
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

        /// <summary>
        /// 绑定卡片 Model 实例。由 PlayerCardManager 在创建后调用。
        /// 订阅 IsPinned 与 GameModel.IsAppFocused；解除由 Dispose 负责。
        /// </summary>
        public void Bind(IPlayerCard card)
        {
            _card = card;
            if (_card == null) return;

            _unRegisters.Add(_card.IsPinned.RegisterWithInitValue(OnPinnedChanged));
            _unRegisters.Add(GameApp.Interface.GetModel<IGameModel>()
                .IsAppFocused.RegisterWithInitValue(_ => RefreshVisibility()));
        }

        /// <summary>
        /// 由 PlayerCardManager 在移除卡片前调用，解除 Bindable 订阅，防止泄漏。
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _unRegisters.Count; i++) _unRegisters[i].UnRegister();
            _unRegisters.Clear();
            _card = null;
        }

        private void OnPinnedChanged(bool pinned)
        {
            _pinBtn?.EnableInClassList("pc-pin-btn--unpinned", !pinned);
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (_root == null || _card == null) return;
            bool focused = GameApp.Interface.GetModel<IGameModel>().IsAppFocused.Value;
            bool visible = focused || _card.IsPinned.Value;
            _root.EnableInClassList("pc-hidden", !visible);
        }
    }
}
