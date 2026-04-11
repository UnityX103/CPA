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
    /// 玩家卡片管理器：订阅网络事件，管理嵌入式 PlayerCardController 的生命周期。
    /// 每个远程玩家对应一个从 PlayerCard.uxml CloneTree 得到的 VisualElement。
    /// </summary>
    public sealed class PlayerCardManager : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private readonly Dictionary<string, PlayerCardController> _cards = new Dictionary<string, PlayerCardController>();

        private VisualTreeAsset _cardTemplate;
        private VisualElement _cardContainer;
        private bool _initialized;

        /// <summary>当前活跃的卡片（只读，供测试使用）。</summary>
        public IReadOnlyDictionary<string, PlayerCardController> Cards => _cards;

        /// <summary>
        /// 初始化：注册网络事件订阅。
        /// </summary>
        /// <param name="cardTemplate">PlayerCard.uxml VisualTreeAsset</param>
        /// <param name="cardContainer">卡片挂载的容器（ScrollView 的 contentContainer）</param>
        /// <param name="lifecycleOwner">用于自动反注册事件的 GameObject</param>
        public void Initialize(VisualTreeAsset cardTemplate, VisualElement cardContainer, GameObject lifecycleOwner)
        {
            if (_initialized) return;

            _cardTemplate = cardTemplate;
            _cardContainer = cardContainer;

            if (_cardTemplate == null)
            {
                Debug.LogError("[PlayerCardManager] PlayerCard.uxml 未分配。");
            }

            if (_cardContainer == null)
            {
                Debug.LogError("[PlayerCardManager] cardContainer 未分配。");
            }

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
        public void InitializeForTests(VisualTreeAsset cardTemplate, VisualElement cardContainer)
        {
            _cardTemplate = cardTemplate;
            _cardContainer = cardContainer;
            _initialized = true;
        }

        // ─── 事件回调 ────────────────────────────────────────

        private void OnPlayerJoined(E_PlayerJoined e)
        {
            if (e.Player == null) return;
            AddOrUpdate(e.Player);
        }

        private void OnPlayerLeft(E_PlayerLeft e)
        {
            Remove(e.PlayerId);
        }

        private void OnStateUpdated(E_RemoteStateUpdated e)
        {
            if (string.IsNullOrEmpty(e.PlayerId)) return;

            IRoomModel room = this.GetModel<IRoomModel>();
            RemotePlayerData data = FindRemotePlayer(room, e.PlayerId);
            if (data == null) return;

            if (_cards.TryGetValue(e.PlayerId, out PlayerCardController card))
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
            if (data == null || string.IsNullOrEmpty(data.PlayerId)) return;

            // 已存在 → 刷新
            if (_cards.TryGetValue(data.PlayerId, out PlayerCardController existing))
            {
                existing.Refresh(data);
                return;
            }

            // 新建 → CloneTree 创建卡片 VisualElement
            if (_cardTemplate == null)
            {
                Debug.LogError($"[PlayerCardManager] 无法创建卡片：PlayerCard.uxml 未分配。" +
                    $" 玩家 '{data.PlayerName}' (id={data.PlayerId}) 被跳过。");
                return;
            }

            if (_cardContainer == null)
            {
                Debug.LogError($"[PlayerCardManager] 无法创建卡片：cardContainer 未分配。");
                return;
            }

            TemplateContainer tplContainer = _cardTemplate.CloneTree();
            VisualElement pcRoot = tplContainer.Q<VisualElement>(className: "pc-root") ?? tplContainer;
            tplContainer.style.flexShrink = 0;
            _cardContainer.Add(tplContainer);

            var controller = new PlayerCardController(pcRoot);
            controller.Setup(data);
            _cards[data.PlayerId] = controller;
        }

        public void Remove(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            if (_cards.TryGetValue(playerId, out PlayerCardController card))
            {
                card.Root.parent?.Remove(card.Root);
                _cards.Remove(playerId);
            }
        }

        public void Clear()
        {
            _cardContainer?.Clear();
            _cards.Clear();
        }

        private void RebuildFromSnapshot(IList<RemotePlayerData> players)
        {
            Clear();
            if (players == null) return;

            for (int i = 0; i < players.Count; i++)
            {
                AddOrUpdate(players[i]);
            }
        }

        private static RemotePlayerData FindRemotePlayer(IRoomModel room, string playerId)
        {
            if (room == null) return null;

            IReadOnlyList<RemotePlayerData> players = room.RemotePlayers;
            if (players == null) return null;

            for (int i = 0; i < players.Count; i++)
            {
                RemotePlayerData p = players[i];
                if (p != null && p.PlayerId == playerId)
                    return p;
            }

            return null;
        }
    }
}
