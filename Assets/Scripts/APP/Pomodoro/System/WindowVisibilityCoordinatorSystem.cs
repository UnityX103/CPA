using System.Collections.Generic;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using APP.Settings.Model;
using QFramework;

namespace APP.Pomodoro.System
{
    public sealed class WindowVisibilityCoordinatorSystem
        : AbstractSystem, IWindowVisibilityCoordinatorSystem
    {
        private readonly BindableProperty<bool> _anyPinned = new BindableProperty<bool>(false);
        private readonly Dictionary<string, IUnRegister> _cardSubs = new Dictionary<string, IUnRegister>();

        public IReadonlyBindableProperty<bool> AnyPinned => _anyPinned;

        protected override void OnInit()
        {
            IPomodoroModel pomodoro = this.GetModel<IPomodoroModel>();
            IPlayerCardModel cards = this.GetModel<IPlayerCardModel>();
            IBindingKeyModel binding = this.GetModel<IBindingKeyModel>();

            // 1) 订阅番茄钟 IsPinned
            pomodoro.IsPinned.Register(_ => Recalculate());

            // 2) 订阅 PlayerCard 动态集合（加/删）
            this.RegisterEvent<E_PlayerCardAdded>(e => OnCardAdded(e.PlayerId));
            this.RegisterEvent<E_PlayerCardRemoved>(e => OnCardRemoved(e.PlayerId));

            // 3) 对已存在的 Card（冷启动时从持久化恢复）订阅
            foreach (IPlayerCard card in cards.Cards)
            {
                SubscribeCard(card);
            }

            // 3.5) 订阅按键计数面板的 PanelPinned（与番茄面板同走 AnyPinned 链路）。
            //      EntriesRevision 触发 Recalculate 让"binding 删空 → 面板被销毁"的瞬间也能让 AnyPinned 降回 false。
            binding.PanelPinned.Register(_ => Recalculate());
            binding.EntriesRevision.Register(_ => Recalculate());

            // 4) 写初值
            Recalculate();

            // 5) AnyPinned 或 IsFlashing 变化 → 驱动原生窗口层级
            //    topmost = AnyPinned ∥ IsFlashing
            //    其中 IsFlashing 由 PhaseTransitionFlashSystem 在阶段自然切换时置位
            _anyPinned.Register(_ => ApplyTopmost());
            this.GetSystem<IPhaseTransitionFlashSystem>().IsFlashing.Register(_ => ApplyTopmost());
            ApplyTopmost();
        }

        private void ApplyTopmost()
        {
            bool flashing = this.GetSystem<IPhaseTransitionFlashSystem>().IsFlashing.Value;
            bool topmost = _anyPinned.Value || flashing;
            this.GetSystem<IWindowPositionSystem>().SetTopmost(topmost);
        }

        private void OnCardAdded(string playerId)
        {
            IPlayerCard card = this.GetModel<IPlayerCardModel>().Find(playerId);
            if (card == null) return;
            SubscribeCard(card);
            Recalculate();
        }

        private void OnCardRemoved(string playerId)
        {
            if (_cardSubs.TryGetValue(playerId, out IUnRegister handle))
            {
                handle.UnRegister();
                _cardSubs.Remove(playerId);
            }
            Recalculate();
        }

        private void SubscribeCard(IPlayerCard card)
        {
            if (_cardSubs.ContainsKey(card.PlayerId)) return;
            _cardSubs[card.PlayerId] = card.IsPinned.Register(_ => Recalculate());
        }

        private void Recalculate()
        {
            IPomodoroModel pomodoro = this.GetModel<IPomodoroModel>();
            IPlayerCardModel cards = this.GetModel<IPlayerCardModel>();
            IBindingKeyModel binding = this.GetModel<IBindingKeyModel>();

            // PanelPinned 只在 InputCounterPanel 实际存在时计入 AnyPinned（面板存在性由 binding.Entries.Count 决定）。
            // 否则用户删掉所有 binding 让面板销毁后，PanelPinned 仍持久化为 true，窗口会一直置顶且番茄面板按
            // AnyPinned 进入失焦隐藏，用户却没有 UI 入口去关闭它。
            bool panelPinnedActive = binding.PanelPinned.Value && binding.Entries.Count > 0;
            bool any = pomodoro.IsPinned.Value || panelPinnedActive;
            if (!any)
            {
                for (int i = 0; i < cards.Cards.Count; i++)
                {
                    if (cards.Cards[i].IsPinned.Value)
                    {
                        any = true;
                        break;
                    }
                }
            }
            _anyPinned.Value = any;
        }
    }
}
