using System.Collections.Generic;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 玩家卡片管理器：订阅网络事件，管理 PlayerCardController 的生命周期。
    /// 卡片以绝对定位方式挂在主面板 #card-layer 上：
    ///  - 已持久化位置 → 恢复上次坐标
    ///  - 否则按"上一张右侧 + 右界换行"算法摆放（首张固定 (40,40)）
    /// 拖拽结束后通过 Cmd_SetPlayerCardPosition 持久化。
    /// </summary>
    public sealed class PlayerCardManager : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        public const float CardWidth  = 153f;
        public const float CardHeight = 94f;
        public const float Gap        = 12f;
        public static readonly Vector2 FirstAnchor = new Vector2(40f, 40f);

        private readonly Dictionary<string, PlayerCardController> _cards = new Dictionary<string, PlayerCardController>();
        private readonly List<string> _joinOrder = new List<string>();

        private VisualTreeAsset _cardTemplate;
        private VisualElement _cardLayer;
        private bool _initialized;

        public IReadOnlyDictionary<string, PlayerCardController> Cards => _cards;

        public void Initialize(VisualTreeAsset cardTemplate, VisualElement cardLayer, GameObject lifecycleOwner)
        {
            if (_initialized) return;
            _cardTemplate = cardTemplate;
            _cardLayer = cardLayer;

            if (_cardTemplate == null) Debug.LogError("[PlayerCardManager] PlayerCard.uxml 未分配。");
            if (_cardLayer == null)    Debug.LogError("[PlayerCardManager] cardLayer 未分配。");

            if (lifecycleOwner != null)
            {
                this.RegisterEvent<E_PlayerJoined>(OnPlayerJoined).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_PlayerLeft>(OnPlayerLeft).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RoomSnapshot>(OnSnapshot).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RoomJoined>(OnRoomJoined).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_IconUpdated>(OnIconUpdated).UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            }
            else
            {
                this.RegisterEvent<E_PlayerJoined>(OnPlayerJoined);
                this.RegisterEvent<E_PlayerLeft>(OnPlayerLeft);
                this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated);
                this.RegisterEvent<E_RoomSnapshot>(OnSnapshot);
                this.RegisterEvent<E_RoomJoined>(OnRoomJoined);
                this.RegisterEvent<E_IconUpdated>(OnIconUpdated);
            }

            _initialized = true;
        }

        public void InitializeForTests(VisualTreeAsset cardTemplate, VisualElement cardLayer)
        {
            _cardTemplate = cardTemplate;
            _cardLayer = cardLayer;
            _initialized = true;
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnPlayerJoined(E_PlayerJoined e) { if (e.Player != null) AddOrUpdate(e.Player); }
        private void OnPlayerLeft(E_PlayerLeft e)     { Remove(e.PlayerId); }

        private void OnStateUpdated(E_RemoteStateUpdated e)
        {
            if (string.IsNullOrEmpty(e.PlayerId)) return;
            var room = this.GetModel<IRoomModel>();
            var data = FindRemotePlayer(room, e.PlayerId);
            if (data == null) return;
            if (_cards.TryGetValue(e.PlayerId, out var card)) card.Refresh(data);
            else AddOrUpdate(data);
        }

        private void OnRoomJoined(E_RoomJoined e)   { RebuildFromSnapshot(e.InitialPlayers); }
        private void OnSnapshot(E_RoomSnapshot e)   { RebuildFromSnapshot(e.Players); }

        private void OnIconUpdated(E_IconUpdated e)
        {
            if (string.IsNullOrEmpty(e.BundleId)) return;
            var room = this.GetModel<IRoomModel>();
            foreach (var kv in _cards)
            {
                var data = FindRemotePlayer(room, kv.Key);
                if (data != null && data.ActiveAppBundleId == e.BundleId) kv.Value.Refresh(data);
            }
        }

        // ─── 核心 CRUD ──────────────────────────────────────────

        public void AddOrUpdate(RemotePlayerData data)
        {
            if (data == null || string.IsNullOrEmpty(data.PlayerId)) return;

            if (_cards.TryGetValue(data.PlayerId, out var existing))
            {
                existing.Refresh(data);
                return;
            }

            if (_cardTemplate == null)
            {
                Debug.LogError($"[PlayerCardManager] 无法创建卡片：PlayerCard.uxml 未分配。玩家 '{data.PlayerName}' (id={data.PlayerId}) 被跳过。");
                return;
            }
            if (_cardLayer == null)
            {
                Debug.LogError($"[PlayerCardManager] 无法创建卡片：cardLayer 未分配。");
                return;
            }

            var tpl = _cardTemplate.CloneTree();
            var pcRoot = tpl.Q<VisualElement>(className: "pc-root") ?? tpl;
            tpl.style.flexShrink = 0;
            _cardLayer.Add(tpl);

            // 位置：持久化 > NextSlot（隔壁规则）
            Vector2 pos = ResolveInitialPosition(data.PlayerId);
            pcRoot.style.position = Position.Absolute;
            pcRoot.style.left = pos.x;
            pcRoot.style.top  = pos.y;

            var ctrl = new PlayerCardController(pcRoot);
            ctrl.Setup(data);
            _cards[data.PlayerId] = ctrl;
            _joinOrder.Add(data.PlayerId);

            // 新玩家（Model 里没有位置记录）→ 把 NextSlot 的结果写回 Model 持久化
            // TODO(Task 7): NetworkSystem 将接管 IPlayerCardModel 生命周期；Manager 不应驱动 Model 生命周期。
            var cardModel = this.GetModel<IPlayerCardModel>();
            var card = cardModel?.AddOrGet(data.PlayerId);
            // sentinel: NextSlot 起点 (40,40) 永不产出 (0,0)；新建 IPlayerCard 默认 Position=(0,0) 即"未持久化"
            if (card != null && card.Position.Value == Vector2.zero)
            {
                this.SendCommand(new Cmd_SetPlayerCardPosition(data.PlayerId, pos));
            }

            // 整卡拖拽：以 pcRoot 本身作为 handle，选中卡片任意位置皆可拖动
            // 拖拽结束 → 持久化
            {
                var drag = DraggableElement.MakeDraggable(pcRoot);
                var id = data.PlayerId;
                drag.OnDragEnd += p => this.SendCommand(new Cmd_SetPlayerCardPosition(id, p));
            }
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (_cards.TryGetValue(playerId, out var card))
            {
                card.Root.parent?.Remove(card.Root);
                _cards.Remove(playerId);
                _joinOrder.Remove(playerId);

                // TODO(Task 7): NetworkSystem 将接管 IPlayerCardModel 的生命周期；
                // 此处 Manager 临时对称调用 Remove，避免离线玩家在 _entries 里泄漏。
                this.GetModel<IPlayerCardModel>()?.Remove(playerId);
            }
        }

        public void Clear()
        {
            _cardLayer?.Clear();
            _cards.Clear();
            _joinOrder.Clear();
        }

        private void RebuildFromSnapshot(IList<RemotePlayerData> players)
        {
            Clear();
            if (players == null) return;
            for (int i = 0; i < players.Count; i++) AddOrUpdate(players[i]);
        }

        private static RemotePlayerData FindRemotePlayer(IRoomModel room, string playerId)
        {
            if (room == null) return null;
            var players = room.RemotePlayers;
            if (players == null) return null;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && p.PlayerId == playerId) return p;
            }
            return null;
        }

        // ─── 初始位置解析 ────────────────────────────────────────

        private Vector2 ResolveInitialPosition(string playerId)
        {
            var cardModel = this.GetModel<IPlayerCardModel>();
            var card = cardModel?.Find(playerId);
            // sentinel: NextSlot 起点 (40,40) 永不产出 (0,0)；!= Vector2.zero 等同于"有持久化位置"
            if (card != null && card.Position.Value != Vector2.zero)
            {
                return card.Position.Value;
            }
            return NextSlot();
        }

        /// <summary>
        /// 下一空位：
        ///  - joinOrder 空 → FirstAnchor (40,40)
        ///  - 否则 → prev 右侧 153+12；越过 layerW-20 → 换行 y += 113+12, x 归 40
        ///  - 下界 clamp 到 layerH-113-20（允许最后一行堆叠）
        /// </summary>
        public Vector2 NextSlot()
        {
            if (_joinOrder.Count == 0) return FirstAnchor;
            var prevId = _joinOrder[_joinOrder.Count - 1];
            if (!_cards.TryGetValue(prevId, out var prev)) return FirstAnchor;
            float prevX = prev.Root.style.left.value.value;
            float prevY = prev.Root.style.top.value.value;

            float layerW = (_cardLayer?.resolvedStyle.width  ?? 0) > 0
                ? _cardLayer.resolvedStyle.width
                : (_cardLayer?.style.width.value.value ?? Screen.width);
            float layerH = (_cardLayer?.resolvedStyle.height ?? 0) > 0
                ? _cardLayer.resolvedStyle.height
                : (_cardLayer?.style.height.value.value ?? Screen.height);

            float x = prevX + CardWidth + Gap;
            float y = prevY;
            if (x + CardWidth > layerW - 20f)
            {
                x = FirstAnchor.x;
                y = prevY + CardHeight + Gap;
            }
            y = Mathf.Min(y, layerH - CardHeight - 20f);
            return new Vector2(x, y);
        }
    }
}
