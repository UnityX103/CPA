using System.Collections.Generic;
using APP.Network.Model;
using APP.Network.System;
using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
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

        // 玩家名自适应字号范围：基础=设计稿 14，最小=7。
        // 9px 实测对 6 字以内长名仍 ellipsis；下探到 7 让 6~7 个汉字也能完整显示，超出再交给 USS overflow:ellipsis 兜底
        private const float NameBaseFontSize = 14f;
        private const float NameMinFontSize = 7f;
        // 安全系数：避免测量值与实际渲染舍入带来的溢出
        private const float NameSafetyFactor = 0.95f;

        private const string PillHiddenClass = "pc-key-counter-pill--hidden";

        private readonly VisualElement _root;
        private Label _nameLabel;
        private Label _phaseLabel;
        private Label _timeLabel;
        // _roundsLabel 已移除：新设计的 keyCounterPill 不再展示 rounds（替换为按键计数）
        private Label _appLabel;
        private VisualElement _appIcon;
        private VisualElement _keyCounterPill;
        private VisualElement _keyCounterPillBadge;
        private Label _keyCounterPillKey;
        private Label _keyCounterPillCount;
        private IPlayerCard _card;
        private VisualElement _pinBtn;
        private readonly List<IUnRegister> _unRegisters = new List<IUnRegister>();

        // 防止 AutoFit 内部修改 fontSize 触发 GeometryChangedEvent 再次进入导致递归
        private bool _isAutoFittingName;

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
            _appLabel = _root.Q<Label>("pc-app");
            _appIcon = _root.Q<VisualElement>("pc-active-app-icon");
            _pinBtn = _root.Q<VisualElement>("pc-pin-btn");
            _keyCounterPill = _root.Q<VisualElement>("pc-key-counter-pill");
            // KeyCounterPill 内部 Label 命名固定，Q 限定在 pill 子树里避免和未来其他 pill 同名冲突
            _keyCounterPillBadge = _keyCounterPill?.Q<VisualElement>("key-counter-pill-badge");
            _keyCounterPillKey   = _keyCounterPill?.Q<Label>("key-counter-pill-key");
            _keyCounterPillCount = _keyCounterPill?.Q<Label>("key-counter-pill-count");

            // 玩家名容器布局变化时重新自适应字号（卡片改宽 / 时间区文本变更挤压 nameCol 等）
            if (_nameLabel != null)
            {
                var nameCol = _nameLabel.parent;
                nameCol?.RegisterCallback<GeometryChangedEvent>(_ => AutoFitNameFontSize());
            }
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
            {
                _nameLabel.text = string.IsNullOrEmpty(data.PlayerName) ? "玩家" : data.PlayerName;
                // 文本变化后触发自适应；schedule 下一帧让布局先完成一次 pass
                _nameLabel.schedule.Execute(AutoFitNameFontSize).StartingIn(0);
            }

            if (_phaseLabel != null)
            {
                string phaseText = PlayerCardView.FormatPhase(data.Phase, data.IsRunning);
                _phaseLabel.text = phaseText;
                AutoSizePhaseLabel(phaseText);
            }

            if (_timeLabel != null)
                _timeLabel.text = PlayerCardView.FormatTime(data.RemainingSeconds);

            ApplyBindingKey(data.BindingKeyLabel, data.BindingPressCount);

            if (_appLabel != null)
                _appLabel.text = string.IsNullOrEmpty(data.ActiveAppName) ? "—" : data.ActiveAppName;

            ApplyAppIcon(data.ActiveAppBundleId);

            ApplyPhaseClass(data.Phase, data.IsRunning);
        }

        /// <summary>
        /// 远端 KeyCounterPill 渲染：
        ///   - keyLabel 为空 / 未提供 → 整 pill 加 .pc-key-counter-pill--hidden（USS display:none）
        ///   - 否则写文本，移除隐藏类
        /// 本地 InputCounterPanel 的 pill 由 InputCounterPanelController 独立驱动，不走这里。
        /// </summary>
        private void ApplyBindingKey(string keyLabel, int pressCount)
        {
            if (_keyCounterPill == null) return;
            bool hasBinding = !string.IsNullOrEmpty(keyLabel);
            _keyCounterPill.EnableInClassList(PillHiddenClass, !hasBinding);
            if (!hasBinding) return;
            if (_keyCounterPillKey != null)   _keyCounterPillKey.text   = keyLabel;
            if (_keyCounterPillCount != null)
            {
                _keyCounterPillCount.text = FormatPressCount(pressCount);
                // 实际计数在 tooltip 里保留：UI Toolkit Editor 模式悬停可见，
                // Runtime 触屏拿不到但至少有可访问性回路（screen reader / UI debugger）。
                _keyCounterPillCount.tooltip = pressCount.ToString();
            }
            PlayerCardView.ApplyKeyBadgeMouseClass(_keyCounterPillBadge, keyLabel);
        }

        // 卡片宽 153，KeyCounterPill 在 head 右侧的可用宽度（"远端玩家"中文名 + 10px gap + pill）
        // 只能容下 2 位数字字符。3 位起 count Label 会把整个 pill 撑出卡片右边界，被 pc-root overflow:hidden 裁掉
        // （codex review B 点；早期尝试过 "99+" 但 3 字符同样溢出）。
        // 保守做法：>=100 视觉收敛到 "99"，配合 tooltip 保留真实数值；语义上视为"99 或更多"。
        private const int PressCountDisplayCap = 99;
        private static string FormatPressCount(int pressCount)
        {
            if (pressCount < 0) return "0";
            if (pressCount >= PressCountDisplayCap) return PressCountDisplayCap.ToString();
            return pressCount.ToString();
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
            if (tex != null)
            {
                _appIcon.style.backgroundImage = new StyleBackground(tex);
                // USS 的灰褐色 tint 会把真实彩色 App 图标染脏，强制 white 让原色透出
                _appIcon.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
            }
            else
            {
                _appIcon.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
                _appIcon.style.unityBackgroundImageTintColor = new StyleColor(StyleKeyword.Null);
            }
        }

        /// <summary>
        /// 根据玩家名容器宽度逐步缩小字体，直到完整文本可在一行内显示为止。
        /// 若缩到最小字号仍溢出，USS 的 overflow:hidden + text-overflow 作为兜底。
        /// </summary>
        private void AutoFitNameFontSize()
        {
            if (_isAutoFittingName) return;
            if (_nameLabel == null) return;
            if (string.IsNullOrEmpty(_nameLabel.text)) return;

            var parent = _nameLabel.parent;
            if (parent == null) return;

            float available = parent.resolvedStyle.width;
            if (available <= 0f) return; // 尚未完成布局

            _isAutoFittingName = true;
            try
            {
                _nameLabel.style.fontSize = NameBaseFontSize;
                Vector2 measured = _nameLabel.MeasureTextSize(
                    _nameLabel.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);

                if (measured.x <= available)
                {
                    return;
                }

                float ratio = available / measured.x;
                float newSize = Mathf.Max(NameMinFontSize, NameBaseFontSize * ratio * NameSafetyFactor);
                _nameLabel.style.fontSize = newSize;
            }
            finally
            {
                _isAutoFittingName = false;
            }
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
            _unRegisters.Add(GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>()
                .AnyPinned.RegisterWithInitValue(_ => RefreshVisibility()));
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
            bool anyPinned = GameApp.Interface.GetSystem<IWindowVisibilityCoordinatorSystem>().AnyPinned.Value;
            bool thisPinned = _card.IsPinned.Value;
            // S2 隐藏条件：整窗口置顶(AnyPinned) 且失焦 且本卡非 pinned
            bool hidden = !thisPinned && !focused && anyPinned;
            _root.EnableInClassList("pc-hidden", hidden);
        }
    }
}
