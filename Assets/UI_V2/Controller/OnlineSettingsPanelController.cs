using APP.Network.Command;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro.Model;
using APP.SessionMemory.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 联机设置面板控制器（纯 C# 类，无 MonoBehaviour）。
    /// 接收 VisualElement 容器，管理联机设置的 UI 绑定与联机逻辑。
    /// 三个卡片根据房间状态互斥显示：
    ///   - osp-join-card + osp-hist-card：未加入房间时
    ///   - osp-room-card：已加入房间时
    /// </summary>
    public sealed class OnlineSettingsPanelController : IController
    {
        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 私有字段 ────────────────────────────────────────────
        private IRoomModel _roomModel;
        private ISessionMemoryModel _sessionMemory;
        private Toggle _autoToggle;
        private Button _createBtn;
        private Button _copyBtn;
        private Label _reconnectBanner;

        // 用户点加入（或从历史快捷加入）后的 pending 态：若服务端回 ROOM_NOT_FOUND，
        // 自动回退为 create_room（用用户当时输入的房间号作为 key）。
        // 自动重连（Cmd_AutoReconnectOnStartup）不走这个分支。
        private bool _userJoinAutoCreateOnMissing;
        private string _userJoinFallbackName;
        private string _userJoinFallbackRoomCode;

        // ─── UXML 元素引用 ────────────────────────────────────────
        private VisualElement _joinCard;
        private VisualElement _roomCard;
        private VisualElement _histCard;
        private VisualElement _histList;
        private VisualElement _memberList;
        private Label _roomName;
        private Label _roomStatus;
        private Label _ospError;
        private TextField _usernameField;
        private TextField _roomIdField;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 初始化面板。容器内已由 UnifiedSettingsPanelController 克隆好 UXML。
        /// </summary>
        public void Init(VisualElement container, IRoomModel roomModel, GameObject lifecycleOwner)
        {
            _roomModel = roomModel;
            _sessionMemory = GameApp.Interface.GetModel<ISessionMemoryModel>();

            // 查询 UI 元素
            _joinCard      = container.Q<VisualElement>("osp-join-card");
            _roomCard      = container.Q<VisualElement>("osp-room-card");
            _histCard      = container.Q<VisualElement>("osp-hist-card");
            _histList      = container.Q<VisualElement>("osp-hist-list");
            _memberList    = container.Q<VisualElement>("osp-member-list");
            _roomName      = container.Q<Label>("osp-room-name");
            _roomStatus    = container.Q<Label>("osp-room-status");
            _ospError      = container.Q<Label>("osp-error");
            _usernameField = container.Q<TextField>("osp-username");
            _roomIdField   = container.Q<TextField>("osp-room-id");
            _autoToggle      = container.Q<Toggle>("osp-auto-toggle");
            _createBtn       = container.Q<Button>("osp-create-btn");
            _copyBtn         = container.Q<Button>("osp-copy-btn");
            _reconnectBanner = container.Q<Label>("osp-reconnect-banner");

            // 按钮事件
            container.Q<Button>("osp-join-btn")?.RegisterCallback<PointerUpEvent>(_ => OnJoinClicked());
            container.Q<Button>("osp-exit-btn")?.RegisterCallback<PointerUpEvent>(_ => OnExitClicked());
            _createBtn?.RegisterCallback<PointerUpEvent>(_ => OnCreateClicked());
            _copyBtn?.RegisterCallback<PointerUpEvent>(_ => OnCopyClicked());

            if (_autoToggle != null)
            {
                _autoToggle.SetValueWithoutNotify(_sessionMemory.AutoReconnectEnabled.Value);
                _autoToggle.RegisterValueChangedCallback(e => _sessionMemory.SetAutoReconnectEnabled(e.newValue));
            }

            // Model/Event 订阅
            if (_roomModel != null)
            {
                _roomModel.IsInRoom.Register(_ => RefreshCardState())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            }

            this.RegisterEvent<E_NetworkError>(OnNetworkError)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);

            this.RegisterEvent<E_ConnectionStateChanged>(OnConnectionStateChanged)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);

            this.RegisterEvent<APP.SessionMemory.Event.E_RecentRoomsChanged>(_ => RefreshHistoryList())
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);

            // 回填上次的用户名
            if (_usernameField != null && _sessionMemory != null)
            {
                string savedName = _sessionMemory.LastPlayerName.Value;
                if (!string.IsNullOrEmpty(savedName))
                {
                    _usernameField.value = savedName;
                }
            }

            RefreshCardState();
        }

        // ─── 卡片状态切换 ────────────────────────────────────────

        public void RefreshCardState()
        {
            bool inRoom = _roomModel != null && _roomModel.IsInRoom.Value;

            _joinCard?.EnableInClassList("osp-hidden", inRoom);
            _histCard?.EnableInClassList("osp-hidden", inRoom);
            _roomCard?.EnableInClassList("osp-hidden", !inRoom);

            if (inRoom)
            {
                RefreshRoomInfo();
            }
            else
            {
                RefreshHistoryList();
            }
        }

        private void RefreshRoomInfo()
        {
            if (_roomModel == null)
            {
                return;
            }

            if (_roomName != null)
            {
                _roomName.text = _roomModel.RoomCode.Value ?? string.Empty;
            }

            if (_roomStatus != null)
            {
                int count = _roomModel.RemotePlayers.Count + 1; // +1 包含自己
                _roomStatus.text = $"已连接 · {count} 位成员";
            }

            RefreshMemberList();
        }

        private void RefreshMemberList()
        {
            if (_memberList == null || _roomModel == null)
            {
                return;
            }

            _memberList.Clear();

            // 自己
            AddMemberItem(
                _roomModel.LocalPlayerName.Value ?? "我",
                true,
                "我",
                "osp-member-status--idle");

            // 远端玩家
            foreach (RemotePlayerData player in _roomModel.RemotePlayers)
            {
                string statusText;
                string statusClass;

                if (player.IsRunning && player.Phase == PomodoroPhase.Focus)
                {
                    statusText = "专注中";
                    statusClass = "osp-member-status--focus";
                }
                else if (player.IsRunning && player.Phase == PomodoroPhase.Break)
                {
                    statusText = "休息中";
                    statusClass = "osp-member-status--break";
                }
                else
                {
                    statusText = "未开始";
                    statusClass = "osp-member-status--idle";
                }

                AddMemberItem(player.PlayerName, true, statusText, statusClass);
            }
        }

        private void AddMemberItem(string name, bool isOnline, string statusText, string statusClass)
        {
            VisualElement item = new VisualElement();
            item.AddToClassList("osp-member-item");

            VisualElement dot = new VisualElement();
            dot.AddToClassList("osp-member-dot");
            dot.AddToClassList(isOnline ? "osp-member-dot--online" : "osp-member-dot--offline");
            item.Add(dot);

            Label nameLabel = new Label(name);
            nameLabel.AddToClassList("osp-member-name");
            item.Add(nameLabel);

            Label statusLabel = new Label(statusText);
            statusLabel.AddToClassList("osp-member-status");
            statusLabel.AddToClassList(statusClass);
            item.Add(statusLabel);

            _memberList.Add(item);
        }

        // ─── 按钮回调 ────────────────────────────────────────────

        private void OnJoinClicked()
        {
            string username = _usernameField?.value ?? string.Empty;
            string roomId   = _roomIdField?.value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(roomId))
            {
                ShowError("请输入用户名和房间号");
                return;
            }

            _userJoinAutoCreateOnMissing = true;
            _userJoinFallbackName = username;
            _userJoinFallbackRoomCode = roomId;
            this.SendCommand(new Cmd_JoinRoom(roomId, username));
        }

        private void OnExitClicked()
        {
            this.SendCommand(new Cmd_LeaveRoom());
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnNetworkError(E_NetworkError e)
        {
            // 用户点了加入，但服务端说房间不存在 —— 用输入的房间号作为 key 创建新房间
            if (_userJoinAutoCreateOnMissing
                && !string.IsNullOrEmpty(e.Code)
                && e.Code == "ROOM_NOT_FOUND"
                && !string.IsNullOrWhiteSpace(_userJoinFallbackName))
            {
                string name = _userJoinFallbackName;
                string desiredCode = _userJoinFallbackRoomCode;
                _userJoinAutoCreateOnMissing = false;
                _userJoinFallbackName = null;
                _userJoinFallbackRoomCode = null;

                ShowError(string.IsNullOrWhiteSpace(desiredCode)
                    ? "房间不存在，已为你创建新房间"
                    : $"房间 {desiredCode} 不存在，已为你创建");
                this.SendCommand(new Cmd_CreateRoom(name, null, desiredCode));
                return;
            }

            _userJoinAutoCreateOnMissing = false;
            _userJoinFallbackName = null;
            _userJoinFallbackRoomCode = null;

            string message = string.IsNullOrEmpty(e.Message) ? e.Code : e.Message;
            ShowError(message);
        }

        private void ShowError(string message)
        {
            if (_ospError == null)
            {
                return;
            }

            _ospError.text = message ?? string.Empty;
            _ospError.EnableInClassList("is-visible", !string.IsNullOrEmpty(message));
        }

        private void OnCreateClicked()
        {
            string username = _usernameField?.value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("请输入用户名");
                return;
            }

            this.SendCommand(new Cmd_CreateRoom(username));
        }

        private void OnCopyClicked()
        {
            string code = _roomModel?.RoomCode.Value ?? string.Empty;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            if (_copyBtn != null) _copyBtn.text = "已复制";
            _copyBtn?.schedule.Execute(() =>
            {
                if (_copyBtn != null) _copyBtn.text = "复制";
            }).StartingIn(2000);
        }

        private void OnConnectionStateChanged(E_ConnectionStateChanged e)
        {
            if (_reconnectBanner == null) return;

            switch (e.Status)
            {
                case ConnectionStatus.Reconnecting:
                    _reconnectBanner.text = "正在重新连接...";
                    _reconnectBanner.RemoveFromClassList("osp-reconnect-banner--error");
                    _reconnectBanner.RemoveFromClassList("osp-hidden");
                    break;
                case ConnectionStatus.Error:
                    _reconnectBanner.text = "重连失败，请重试";
                    _reconnectBanner.AddToClassList("osp-reconnect-banner--error");
                    _reconnectBanner.RemoveFromClassList("osp-hidden");
                    break;
                case ConnectionStatus.Connected:
                case ConnectionStatus.InRoom:
                case ConnectionStatus.Disconnected:
                    _reconnectBanner.AddToClassList("osp-hidden");
                    break;
            }
        }

        // ─── 历史房间列表 ────────────────────────────────────────

        private void RefreshHistoryList()
        {
            if (_histList == null || _sessionMemory == null) return;

            _histList.Clear();

            var rooms = _sessionMemory.RecentRooms;
            if (rooms == null || rooms.Count == 0)
            {
                var empty = new Label("暂无历史房间");
                empty.AddToClassList("osp-hist-empty");
                _histList.Add(empty);
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                HistoryRoomEntry entry = rooms[i];

                VisualElement item = new VisualElement();
                item.AddToClassList("osp-hist-item");

                Label codeLabel = new Label(entry.RoomCode);
                codeLabel.AddToClassList("osp-hist-code");
                item.Add(codeLabel);

                Label nameLabel = new Label(entry.LastPlayerName ?? string.Empty);
                nameLabel.AddToClassList("osp-hist-item-name");
                item.Add(nameLabel);

                Label timeLabel = new Label(RelativeTimeFormatter.Format(entry.LastJoinedAtUnixMs));
                timeLabel.AddToClassList("osp-hist-item-time");
                item.Add(timeLabel);

                string capturedCode = entry.RoomCode;
                Button joinBtn = new Button(() => OnHistoryJoinClicked(capturedCode)) { text = "加入" };
                joinBtn.AddToClassList("comp-btn-icon");
                joinBtn.AddToClassList("osp-hist-join-btn");
                item.Add(joinBtn);

                Button delBtn = new Button(() => OnHistoryDeleteClicked(capturedCode)) { text = "删除" };
                delBtn.AddToClassList("comp-btn-icon");
                delBtn.AddToClassList("osp-hist-del-btn");
                item.Add(delBtn);

                _histList.Add(item);
            }
        }

        private void OnHistoryJoinClicked(string roomCode)
        {
            string username = _usernameField?.value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("请输入用户名");
                return;
            }
            _userJoinAutoCreateOnMissing = true;
            _userJoinFallbackName = username;
            _userJoinFallbackRoomCode = roomCode;
            this.SendCommand(new Cmd_JoinRoom(roomCode, username));
        }

        private void OnHistoryDeleteClicked(string roomCode)
        {
            _sessionMemory.RemoveHistoryEntry(roomCode);
        }
    }
}
