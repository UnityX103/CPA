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
    /// 联机设置面板控制器（独立 UIDocument）。
    /// 管理 OnlineSettingsPanel.uxml 的 UI 绑定与联机逻辑。
    /// 三个卡片根据房间状态互斥显示：
    ///   - osp-join-card + osp-hist-card：未加入房间时
    ///   - osp-room-card：已加入房间时
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OnlineSettingsPanelController : MonoBehaviour, IController, ISettingsPanel
    {
        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument _uiDocument;
        private IRoomModel _roomModel;
        private bool _modelBound;

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

        // ─── ISettingsPanel ──────────────────────────────────────
        public bool IsVisible => _uiDocument != null && _uiDocument.enabled;

        public void Show()
        {
            if (_uiDocument == null)
            {
                return;
            }

            _uiDocument.enabled = true;
            BindUI();
            RefreshCardState();
        }

        public void Hide()
        {
            if (_uiDocument == null)
            {
                return;
            }

            _uiDocument.enabled = false;
        }

        // ─── Unity 生命周期 ──────────────────────────────────────

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (_uiDocument != null)
            {
                _uiDocument.enabled = false;
            }
        }

        // ─── UI 绑定 ─────────────────────────────────────────────

        private void BindUI()
        {
            if (_uiDocument == null)
            {
                return;
            }

            VisualElement root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            // 关闭按钮
            root.Q<Button>("back-btn")?.RegisterCallback<PointerUpEvent>(_ => Hide());

            // 卡片容器
            _joinCard   = root.Q<VisualElement>("osp-join-card");
            _roomCard   = root.Q<VisualElement>("osp-room-card");
            _histCard   = root.Q<VisualElement>("osp-hist-card");
            _memberList = root.Q<VisualElement>("osp-member-list");
            _roomName   = root.Q<Label>("osp-room-name");
            _roomStatus = root.Q<Label>("osp-room-status");
            _ospError   = root.Q<Label>("osp-error");

            // 输入字段
            _usernameField = root.Q<TextField>("osp-username");
            _roomIdField   = root.Q<TextField>("osp-room-id");

            // 按钮事件
            root.Q<Button>("osp-join-btn")?.RegisterCallback<PointerUpEvent>(_ => OnJoinClicked());
            root.Q<Button>("osp-exit-btn")?.RegisterCallback<PointerUpEvent>(_ => OnExitClicked());

            // Model/Event 订阅（仅一次）
            if (!_modelBound)
            {
                _roomModel = this.GetModel<IRoomModel>();

                if (_roomModel != null)
                {
                    _roomModel.IsInRoom.Register(_ => RefreshCardState())
                        .UnRegisterWhenGameObjectDestroyed(gameObject);
                }

                this.RegisterEvent<E_NetworkError>(OnNetworkError)
                    .UnRegisterWhenGameObjectDestroyed(gameObject);

                _modelBound = true;
            }

            // 回填上次的用户名
            if (_usernameField != null && _roomModel != null)
            {
                string savedName = _roomModel.LocalPlayerName.Value;
                if (!string.IsNullOrEmpty(savedName))
                {
                    _usernameField.value = savedName;
                }
            }
        }

        // ─── 卡片状态切换 ────────────────────────────────────────

        private void RefreshCardState()
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
