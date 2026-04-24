using System.Collections.Generic;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
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

            // 4) 写初值
            Recalculate();

            // 5) AnyPinned 变化 → 驱动原生窗口层级
            _anyPinned.Register(v => this.GetSystem<IWindowPositionSystem>().SetTopmost(v));
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

            bool any = pomodoro.IsPinned.Value;
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
