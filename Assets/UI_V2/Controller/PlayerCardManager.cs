using System.Collections.Generic;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 玩家卡片管理器：订阅网络事件，维护卡片集合的增删与刷新。
    /// 独立 player-card-layer 挂到 UIDocument 的 root，脱离 dw-wrap 的 flex 布局。
    /// </summary>
    public sealed class PlayerCardManager : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private readonly Dictionary<string, PlayerCardView> _cards = new Dictionary<string, PlayerCardView>();
        private readonly Dictionary<string, DraggableElement.DragController> _dragControllers =
            new Dictionary<string, DraggableElement.DragController>();

        private VisualElement _cardLayer;
        private VisualTreeAsset _cardTemplate;
        private bool _initialized;

        public IReadOnlyDictionary<string, PlayerCardView> Cards => _cards;
        public VisualElement CardLayer => _cardLayer;

        /// <summary>
        /// 初始化：创建独立 card-layer 并注册网络事件订阅。
        /// 必须由宿主 MonoBehaviour 在 Start 中调用，并传入用于自动反注册的 GameObject。
        /// </summary>
        public void Initialize(VisualElement root, VisualTreeAsset cardTemplate, GameObject lifecycleOwner)
        {
            if (_initialized)
            {
                return;
            }

            if (root == null)
            {
                Debug.LogError("[PlayerCardManager] root 为空，无法初始化卡片层。");
                return;
            }

            _cardTemplate = cardTemplate;

            _cardLayer = new VisualElement
            {
                name = "player-card-layer",
                pickingMode = PickingMode.Ignore,
            };
            _cardLayer.AddToClassList("player-card-layer");
            root.Add(_cardLayer);

            if (lifecycleOwner != null)
            {
                this.RegisterEvent<E_PlayerJoined>(OnPlayerJoined)
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_PlayerLeft>(OnPlayerLeft)
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated)
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RoomSnapshot>(OnSnapshot)
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                this.RegisterEvent<E_RoomJoined>(OnRoomJoined)
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            }
            else
            {
                this.RegisterEvent<E_PlayerJoined>(OnPlayerJoined);
                this.RegisterEvent<E_PlayerLeft>(OnPlayerLeft);
                this.RegisterEvent<E_RemoteStateUpdated>(OnStateUpdated);
                this.RegisterEvent<E_RoomSnapshot>(OnSnapshot);
                this.RegisterEvent<E_RoomJoined>(OnRoomJoined);
            }

            _initialized = true;
        }

        /// <summary>
        /// 测试专用：显式初始化但不订阅事件，允许调用方手动驱动 AddOrUpdate/Remove。
        /// </summary>
        public void InitializeForTests(VisualElement root, VisualTreeAsset cardTemplate)
        {
            _cardTemplate = cardTemplate;

            _cardLayer = new VisualElement
            {
                name = "player-card-layer",
                pickingMode = PickingMode.Ignore,
            };
            _cardLayer.AddToClassList("player-card-layer");
            root?.Add(_cardLayer);

            _initialized = true;
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnPlayerJoined(E_PlayerJoined e)
        {
            if (e.Player == null)
            {
                return;
            }

            AddOrUpdate(e.Player);
        }

        private void OnPlayerLeft(E_PlayerLeft e)
        {
            Remove(e.PlayerId);
        }

        private void OnStateUpdated(E_RemoteStateUpdated e)
        {
            if (string.IsNullOrEmpty(e.PlayerId))
            {
                return;
            }

            IRoomModel room = this.GetModel<IRoomModel>();
            RemotePlayerData data = FindRemotePlayer(room, e.PlayerId);
            if (data == null)
            {
                return;
            }

            if (_cards.TryGetValue(e.PlayerId, out PlayerCardView card))
            {
                card.Refresh(data);
            }
            else
            {
                AddOrUpdate(data);
            }
        }

        private void OnRoomJoined(E_RoomJoined e)
        {
            RebuildFromSnapshot(e.InitialPlayers);
        }

        private void OnSnapshot(E_RoomSnapshot e)
        {
            RebuildFromSnapshot(e.Players);
        }

        // ─── 暴露给测试/事件回调的操作 ──────────────────────────

        public void AddOrUpdate(RemotePlayerData data)
        {
            if (data == null || string.IsNullOrEmpty(data.PlayerId))
            {
                return;
            }

            if (_cards.TryGetValue(data.PlayerId, out PlayerCardView existing))
            {
                existing.Refresh(data);
                return;
            }

            if (_cardTemplate == null || _cardLayer == null)
            {
                Debug.LogError("[PlayerCardManager] 无法创建卡片：" +
                    (_cardTemplate == null ? "PlayerCard 模板未分配。" : string.Empty) +
                    (_cardLayer == null ? "卡片层未初始化。" : string.Empty) +
                    $" 玩家 '{data.PlayerName}' (id={data.PlayerId}) 被跳过。");
                return;
            }

            var card = new PlayerCardView(data, _cardTemplate);
            card.Root.style.left = Random.Range(100, 400);
            card.Root.style.top = Random.Range(100, 300);
            _cardLayer.Add(card.Root);
            _cards[data.PlayerId] = card;

            var drag = DraggableElement.MakeDraggable(card.Root);
            _dragControllers[data.PlayerId] = drag;
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }

            if (_cards.TryGetValue(playerId, out PlayerCardView card))
            {
                card.Root.RemoveFromHierarchy();
                _cards.Remove(playerId);
            }

            _dragControllers.Remove(playerId);
        }

        public void Clear()
        {
            foreach (KeyValuePair<string, PlayerCardView> pair in _cards)
            {
                pair.Value.Root.RemoveFromHierarchy();
            }

            _cards.Clear();
            _dragControllers.Clear();
        }

        private void RebuildFromSnapshot(IList<RemotePlayerData> players)
        {
            Clear();
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                AddOrUpdate(players[i]);
            }
        }

        private static RemotePlayerData FindRemotePlayer(IRoomModel room, string playerId)
        {
            if (room == null)
            {
                return null;
            }

            IReadOnlyList<RemotePlayerData> players = room.RemotePlayers;
            if (players == null)
            {
                return null;
            }

            for (int i = 0; i < players.Count; i++)
            {
                RemotePlayerData p = players[i];
                if (p != null && p.PlayerId == playerId)
                {
                    return p;
                }
            }

            return null;
        }
    }
}
