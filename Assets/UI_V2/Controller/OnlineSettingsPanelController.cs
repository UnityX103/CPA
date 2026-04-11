using APP.Network.Command;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro.Model;
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

        // ─── UXML 元素引用 ────────────────────────────────────────
        private VisualElement _joinCard;
        private VisualElement _roomCard;
        private VisualElement _histCard;
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

            // 查询 UI 元素
            _joinCard      = container.Q<VisualElement>("osp-join-card");
            _roomCard      = container.Q<VisualElement>("osp-room-card");
            _histCard      = container.Q<VisualElement>("osp-hist-card");
            _memberList    = container.Q<VisualElement>("osp-member-list");
            _roomName      = container.Q<Label>("osp-room-name");
            _roomStatus    = container.Q<Label>("osp-room-status");
            _ospError      = container.Q<Label>("osp-error");
            _usernameField = container.Q<TextField>("osp-username");
            _roomIdField   = container.Q<TextField>("osp-room-id");

            // 按钮事件
            container.Q<Button>("osp-join-btn")?.RegisterCallback<PointerUpEvent>(_ => OnJoinClicked());
            container.Q<Button>("osp-exit-btn")?.RegisterCallback<PointerUpEvent>(_ => OnExitClicked());

            // Model/Event 订阅
            if (_roomModel != null)
            {
                _roomModel.IsInRoom.Register(_ => RefreshCardState())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            }

            this.RegisterEvent<E_NetworkError>(OnNetworkError)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);

            // 回填上次的用户名
            if (_usernameField != null && _roomModel != null)
            {
                string savedName = _roomModel.LocalPlayerName.Value;
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

            this.SendCommand(new Cmd_JoinRoom(roomId, username));
        }

        private void OnExitClicked()
        {
            this.SendCommand(new Cmd_LeaveRoom());
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnNetworkError(E_NetworkError e)
        {
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
    }
}
