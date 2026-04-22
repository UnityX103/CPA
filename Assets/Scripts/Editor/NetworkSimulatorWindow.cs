#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Model;
using UnityEditor;
using UnityEngine;

namespace APP.Editor
{
    /// <summary>
    /// 网络模拟器：在编辑器中模拟远程玩家。
    ///
    /// 两种模式：
    /// 1) 本地事件注入（"模拟玩家" 区）—— 直接走 TypeEventSystem，用来单元测 UI 层，
    ///    不碰 WebSocket、不对服务端有副作用。
    /// 2) 真实远端玩家（"真实远端玩家 (WebSocket)" 区）—— 每个条目自起一个
    ///    ClientWebSocket，用真实协议连本地 Node 服务端，join 当前 RoomModel 的
    ///    房间号，周期性发 player_state_update。用来做端到端联调。
    /// </summary>
    public sealed class NetworkSimulatorWindow : EditorWindow
    {
        [MenuItem("Tools/网络模拟器 (PlayerCard 测试)")]
        private static void Open()
        {
            GetWindow<NetworkSimulatorWindow>("网络模拟器").Show();
        }

        // ─── 模拟玩家池（本地事件注入） ────────────────────────
        private readonly List<SimPlayer> _players = new List<SimPlayer>();
        private Vector2 _scrollPos;
        private int _nextId = 1;
        private string _quickName = "";

        // ─── 真实远端玩家池（WebSocket） ───────────────────────
        private readonly List<RealPlayer> _realPlayers = new List<RealPlayer>();
        private Vector2 _realScrollPos;
        private string _realQuickName = "";
        private string _realServerUrl = "ws://127.0.0.1:8765";
        private string _realRoomCodeOverride = "";

        private sealed class SimPlayer
        {
            public string Id;
            public string Name;
            public PomodoroPhase Phase;
            public int RemainingSeconds = 1500;
            public int CurrentRound = 1;
            public int TotalRounds = 4;
            public bool IsRunning;
            public bool AutoTick;
        }

        private enum RealPlayerStatus { Connecting, Joining, Joined, Closed, Error }

        private sealed class RealPlayer
        {
            public string LocalKey;           // 仅编辑器内唯一 id，不参与协议
            public string Name;
            public string RoomCode;
            public ClientWebSocket Socket;
            public CancellationTokenSource Cts;
            public RealPlayerStatus Status = RealPlayerStatus.Connecting;
            public string StatusMessage = "";

            public PomodoroPhase Phase = PomodoroPhase.Focus;
            public int RemainingSeconds = 1500;
            public int CurrentRound = 1;
            public int TotalRounds = 4;
            public bool IsRunning;
            public bool AutoTick;
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            // 关窗时把所有真实连接关掉，避免泄漏
            foreach (var rp in _realPlayers) CloseRealPlayer(rp, notifyLeave: false);
            _realPlayers.Clear();
        }

        // ─── 自动倒计时 & 主线程回调 drain ─────────────────────
        private double _lastTickTime;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private void OnEditorUpdate()
        {
            while (_mainThreadQueue.TryDequeue(out Action act))
            {
                try { act?.Invoke(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTickTime < 1.0) return;
            _lastTickTime = now;

            bool dirty = false;
            foreach (var p in _players)
            {
                if (!p.AutoTick || !p.IsRunning || p.RemainingSeconds <= 0) continue;

                p.RemainingSeconds--;
                if (p.RemainingSeconds <= 0) AdvancePhase(p);
                SendStateUpdate(p);
                dirty = true;
            }

            foreach (var rp in _realPlayers)
            {
                if (!rp.AutoTick || !rp.IsRunning || rp.RemainingSeconds <= 0) continue;
                if (rp.Status != RealPlayerStatus.Joined) continue;

                rp.RemainingSeconds--;
                if (rp.RemainingSeconds <= 0) AdvanceRealPhase(rp);
                SendRealStateUpdate(rp);
                dirty = true;
            }

            if (dirty) Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("网络模拟器", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("请先进入 Play 模式！", MessageType.Warning);
                return;
            }

            DrawRealRemoteSection();
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("────── 本地事件注入（不走 WebSocket）──────", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "本地事件注入仅通过 TypeEventSystem 驱动 PlayerCardManager，用于单独测 UI 层。",
                MessageType.Info);
            DrawQuickAdd();
            EditorGUILayout.Space(8);
            DrawBatchActions();
            EditorGUILayout.Space(8);
            DrawPlayerList();
        }

        // ───────────────────────────────────────────────────────
        // 真实远端玩家 UI
        // ───────────────────────────────────────────────────────

        private void DrawRealRemoteSection()
        {
            EditorGUILayout.LabelField("真实远端玩家 (WebSocket)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "每个条目起一个独立的 ClientWebSocket，连接到下方服务器 URL，\n" +
                "加入本地 RoomModel 的当前房间（或覆盖房间码），走真实协议。\n" +
                "前提：本地已 Create/Join Room；Node 服务端已启动。",
                MessageType.Info);

            string localRoomCode = ReadLocalRoomCode();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("本地房间码", GUILayout.Width(80));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(localRoomCode) ? "(尚未进房)" : localRoomCode,
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            _realServerUrl = EditorGUILayout.TextField("服务器 URL", _realServerUrl);
            _realRoomCodeOverride = EditorGUILayout.TextField("房间码(可选覆盖)", _realRoomCodeOverride);

            using (new EditorGUILayout.HorizontalScope())
            {
                _realQuickName = EditorGUILayout.TextField("玩家昵称", _realQuickName);
                using (new EditorGUI.DisabledScope(
                    string.IsNullOrWhiteSpace(_realServerUrl)
                    || (string.IsNullOrWhiteSpace(localRoomCode)
                        && string.IsNullOrWhiteSpace(_realRoomCodeOverride))))
                {
                    if (GUILayout.Button("添加真实玩家", GUILayout.Width(110)))
                    {
                        string name = string.IsNullOrWhiteSpace(_realQuickName)
                            ? $"远端{_realPlayers.Count + 1}"
                            : _realQuickName.Trim();
                        string code = string.IsNullOrWhiteSpace(_realRoomCodeOverride)
                            ? localRoomCode
                            : _realRoomCodeOverride.Trim().ToUpperInvariant();
                        AddRealPlayer(name, code);
                        _realQuickName = "";
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_realPlayers.Count == 0))
                {
                    if (GUILayout.Button("全部开始"))
                    {
                        foreach (var rp in _realPlayers)
                        {
                            rp.IsRunning = true;
                            rp.AutoTick = true;
                            SendRealStateUpdate(rp);
                        }
                    }
                    if (GUILayout.Button("全部暂停"))
                    {
                        foreach (var rp in _realPlayers)
                        {
                            rp.IsRunning = false;
                            SendRealStateUpdate(rp);
                        }
                    }
                    if (GUILayout.Button("全部断开"))
                    {
                        for (int i = _realPlayers.Count - 1; i >= 0; i--)
                        {
                            CloseRealPlayer(_realPlayers[i], notifyLeave: true);
                        }
                        _realPlayers.Clear();
                    }
                }
            }

            if (_realPlayers.Count == 0)
            {
                EditorGUILayout.LabelField("（尚无真实远端玩家）", EditorStyles.miniLabel);
                return;
            }

            _realScrollPos = EditorGUILayout.BeginScrollView(_realScrollPos, GUILayout.MinHeight(160));
            for (int i = _realPlayers.Count - 1; i >= 0; i--)
            {
                DrawRealPlayerRow(_realPlayers[i], i);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRealPlayerRow(RealPlayer rp, int index)
        {
            EditorGUILayout.BeginVertical("box");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{rp.Name}  [{rp.RoomCode}]  ·  {rp.Status}",
                    EditorStyles.boldLabel);
                if (GUILayout.Button("离开并关闭", GUILayout.Width(90)))
                {
                    CloseRealPlayer(rp, notifyLeave: true);
                    _realPlayers.RemoveAt(index);
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(rp.StatusMessage))
            {
                EditorGUILayout.LabelField(rp.StatusMessage, EditorStyles.miniLabel);
            }

            using (new EditorGUI.DisabledScope(rp.Status != RealPlayerStatus.Joined))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var newPhase = (PomodoroPhase)EditorGUILayout.EnumPopup("阶段", rp.Phase);
                    if (newPhase != rp.Phase)
                    {
                        rp.Phase = newPhase;
                        rp.RemainingSeconds = GetDefaultSeconds(newPhase);
                        SendRealStateUpdate(rp);
                    }
                    int mins = rp.RemainingSeconds / 60;
                    int secs = rp.RemainingSeconds % 60;
                    EditorGUILayout.LabelField($"{mins:D2}:{secs:D2}", GUILayout.Width(50));
                }

                int newRound = EditorGUILayout.IntSlider("轮次", rp.CurrentRound, 1, rp.TotalRounds);
                if (newRound != rp.CurrentRound)
                {
                    rp.CurrentRound = newRound;
                    SendRealStateUpdate(rp);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(rp.IsRunning ? "暂停" : "开始"))
                    {
                        rp.IsRunning = !rp.IsRunning;
                        rp.AutoTick = rp.IsRunning;
                        SendRealStateUpdate(rp);
                    }
                    if (GUILayout.Button("重置"))
                    {
                        rp.Phase = PomodoroPhase.Focus;
                        rp.RemainingSeconds = 1500;
                        rp.CurrentRound = 1;
                        rp.IsRunning = false;
                        rp.AutoTick = false;
                        SendRealStateUpdate(rp);
                    }
                    rp.AutoTick = EditorGUILayout.ToggleLeft("自动倒计时", rp.AutoTick, GUILayout.Width(90));
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ───────────────────────────────────────────────────────
        // 真实远端玩家 —— 核心流程
        // ───────────────────────────────────────────────────────

        private void AddRealPlayer(string name, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                Debug.LogWarning("[NetworkSimulator] 房间码为空，跳过添加真实玩家。");
                return;
            }

            var rp = new RealPlayer
            {
                LocalKey = Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = name,
                RoomCode = roomCode,
                Cts = new CancellationTokenSource(),
                Socket = new ClientWebSocket(),
                Status = RealPlayerStatus.Connecting,
                StatusMessage = $"连接 {_realServerUrl}...",
            };
            _realPlayers.Add(rp);

            _ = RunRealPlayerSessionAsync(rp, _realServerUrl);
            Debug.Log($"[NetworkSimulator] 发起真实远端玩家 '{name}' 连接 {_realServerUrl} 加入 {roomCode}");
        }

        private async Task RunRealPlayerSessionAsync(RealPlayer rp, string serverUrl)
        {
            try
            {
                await rp.Socket.ConnectAsync(new Uri(serverUrl), rp.Cts.Token);
                EnqueueUi(() =>
                {
                    rp.Status = RealPlayerStatus.Joining;
                    rp.StatusMessage = "已连接，发送 join_room...";
                });

                string joinJson =
                    $"{{\"v\":1,\"type\":\"join_room\",\"roomCode\":\"{Escape(rp.RoomCode)}\"," +
                    $"\"playerName\":\"{Escape(rp.Name)}\"}}";
                await SendRawAsync(rp, joinJson);

                var buffer = new byte[8192];
                while (!rp.Cts.IsCancellationRequested && rp.Socket.State == WebSocketState.Open)
                {
                    using var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await rp.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), rp.Cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    HandleInbound(rp, json);
                }
            }
            catch (OperationCanceledException) { /* 正常关闭 */ }
            catch (Exception ex)
            {
                EnqueueUi(() =>
                {
                    rp.Status = RealPlayerStatus.Error;
                    rp.StatusMessage = ex.Message;
                    Debug.LogWarning($"[NetworkSimulator] {rp.Name} 连接异常：{ex.Message}");
                });
            }
            finally
            {
                try { rp.Socket.Dispose(); } catch { }
                EnqueueUi(() =>
                {
                    if (rp.Status != RealPlayerStatus.Error) rp.Status = RealPlayerStatus.Closed;
                });
            }
        }

        private void HandleInbound(RealPlayer rp, string json)
        {
            // 粗暴识别 type 字段：避免为模拟器另引 JSON 库
            string type = ExtractJsonString(json, "type");
            switch (type)
            {
                case "room_joined":
                    EnqueueUi(() =>
                    {
                        rp.Status = RealPlayerStatus.Joined;
                        rp.StatusMessage = $"已加入房间 {rp.RoomCode}";
                    });
                    break;
                case "error":
                    string err = ExtractJsonString(json, "error");
                    EnqueueUi(() =>
                    {
                        rp.Status = RealPlayerStatus.Error;
                        rp.StatusMessage = $"服务端错误：{err}";
                    });
                    break;
                // room_snapshot / player_joined / player_left / player_state_broadcast / icon_* 暂不解析
            }
        }

        private void SendRealStateUpdate(RealPlayer rp)
        {
            if (rp.Status != RealPlayerStatus.Joined) return;

            string json =
                "{\"v\":1,\"type\":\"player_state_update\"," +
                "\"state\":{" +
                    "\"pomodoro\":{" +
                        $"\"phase\":{(int)rp.Phase}," +
                        $"\"remainingSeconds\":{rp.RemainingSeconds}," +
                        $"\"currentRound\":{rp.CurrentRound}," +
                        $"\"totalRounds\":{rp.TotalRounds}," +
                        $"\"isRunning\":{(rp.IsRunning ? "true" : "false")}" +
                    "}," +
                    "\"activeApp\":null" +
                "}}";

            _ = SendRawAsync(rp, json);
        }

        private async Task SendRawAsync(RealPlayer rp, string json)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await rp.Socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true, rp.Cts.Token);
            }
            catch (Exception ex)
            {
                EnqueueUi(() => Debug.LogWarning($"[NetworkSimulator] {rp.Name} 发送失败：{ex.Message}"));
            }
        }

        private void CloseRealPlayer(RealPlayer rp, bool notifyLeave)
        {
            try
            {
                if (notifyLeave && rp.Status == RealPlayerStatus.Joined
                    && rp.Socket != null && rp.Socket.State == WebSocketState.Open)
                {
                    _ = SendRawAsync(rp, "{\"v\":1,\"type\":\"leave_room\"}");
                }
            }
            catch { }

            try { rp.Cts?.Cancel(); } catch { }
            try
            {
                if (rp.Socket != null && rp.Socket.State == WebSocketState.Open)
                {
                    _ = rp.Socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "simulator close", CancellationToken.None);
                }
            }
            catch { }
            rp.Status = RealPlayerStatus.Closed;
        }

        private static void AdvanceRealPhase(RealPlayer rp)
        {
            switch (rp.Phase)
            {
                case PomodoroPhase.Focus:
                    rp.Phase = PomodoroPhase.Break;
                    rp.RemainingSeconds = 300;
                    break;
                case PomodoroPhase.Break:
                    rp.CurrentRound++;
                    if (rp.CurrentRound > rp.TotalRounds)
                    {
                        rp.Phase = PomodoroPhase.Completed;
                        rp.RemainingSeconds = 0;
                        rp.IsRunning = false;
                        rp.AutoTick = false;
                    }
                    else
                    {
                        rp.Phase = PomodoroPhase.Focus;
                        rp.RemainingSeconds = 1500;
                    }
                    break;
                case PomodoroPhase.Completed:
                    rp.IsRunning = false;
                    rp.AutoTick = false;
                    break;
            }
        }

        private string ReadLocalRoomCode()
        {
            try
            {
                var room = GameApp.Interface.GetModel<IRoomModel>();
                return room?.RoomCode?.Value ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void EnqueueUi(Action act)
        {
            if (act != null) _mainThreadQueue.Enqueue(act);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// 从 JSON 顶层粗糙提取 `"key":"value"` 的 value。
        /// 只用于 type/error 两个短字段，不做完整解析。
        /// </summary>
        private static string ExtractJsonString(string json, string key)
        {
            string marker = "\"" + key + "\":\"";
            int idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return string.Empty;
            int start = idx + marker.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : string.Empty;
        }

        // ───────────────────────────────────────────────────────
        // 本地事件注入（保留原有功能）
        // ───────────────────────────────────────────────────────

        private void DrawQuickAdd()
        {
            EditorGUILayout.BeginHorizontal();
            _quickName = EditorGUILayout.TextField("玩家昵称", _quickName);
            if (GUILayout.Button("添加玩家", GUILayout.Width(80)))
            {
                string name = string.IsNullOrEmpty(_quickName) ? $"玩家{_nextId}" : _quickName;
                AddPlayer(name);
                _quickName = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加 3 个测试玩家"))
            {
                AddPlayer("小明");
                AddPlayer("小红");
                AddPlayer("小刚");
            }
            if (GUILayout.Button("发送 Snapshot 事件"))
            {
                SendSnapshotEvent();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBatchActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部开始"))
            {
                foreach (var p in _players)
                {
                    p.IsRunning = true;
                    p.AutoTick = true;
                    SendStateUpdate(p);
                }
            }

            if (GUILayout.Button("全部暂停"))
            {
                foreach (var p in _players)
                {
                    p.IsRunning = false;
                    SendStateUpdate(p);
                }
            }

            if (GUILayout.Button("全部移除"))
            {
                for (int i = _players.Count - 1; i >= 0; i--)
                {
                    SendPlayerLeft(_players[i].Id);
                }
                _players.Clear();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayerList()
        {
            EditorGUILayout.LabelField($"模拟玩家 ({_players.Count})", EditorStyles.boldLabel);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = _players.Count - 1; i >= 0; i--)
            {
                DrawPlayerRow(_players[i], i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPlayerRow(SimPlayer p, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{p.Name} ({p.Id})", EditorStyles.boldLabel);
            if (GUILayout.Button("移除", GUILayout.Width(50)))
            {
                SendPlayerLeft(p.Id);
                _players.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var newPhase = (PomodoroPhase)EditorGUILayout.EnumPopup("阶段", p.Phase);
            if (newPhase != p.Phase)
            {
                p.Phase = newPhase;
                p.RemainingSeconds = GetDefaultSeconds(newPhase);
                SendStateUpdate(p);
            }

            int mins = p.RemainingSeconds / 60;
            int secs = p.RemainingSeconds % 60;
            EditorGUILayout.LabelField($"{mins:D2}:{secs:D2}", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            int newRound = EditorGUILayout.IntSlider("轮次", p.CurrentRound, 1, p.TotalRounds);
            if (newRound != p.CurrentRound)
            {
                p.CurrentRound = newRound;
                SendStateUpdate(p);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(p.IsRunning ? "暂停" : "开始"))
            {
                p.IsRunning = !p.IsRunning;
                p.AutoTick = p.IsRunning;
                SendStateUpdate(p);
            }

            if (GUILayout.Button("重置"))
            {
                p.Phase = PomodoroPhase.Focus;
                p.RemainingSeconds = 1500;
                p.CurrentRound = 1;
                p.IsRunning = false;
                p.AutoTick = false;
                SendStateUpdate(p);
            }

            p.AutoTick = EditorGUILayout.ToggleLeft("自动倒计时", p.AutoTick, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void AddPlayer(string name)
        {
            var player = new SimPlayer
            {
                Id = $"sim-{_nextId++}",
                Name = name,
                Phase = PomodoroPhase.Focus,
                RemainingSeconds = 1500,
                CurrentRound = 1,
                TotalRounds = 4,
                IsRunning = false,
            };
            _players.Add(player);

            var data = ToRemotePlayerData(player);
            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.AddOrUpdateRemotePlayer(data);

            GameApp.Interface.SendEvent(new E_PlayerJoined(data));

            Debug.Log($"[NetworkSimulator] 玩家 '{name}' (id={player.Id}) 已加入");
        }

        private void SendPlayerLeft(string playerId)
        {
            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.RemoveRemotePlayer(playerId);

            GameApp.Interface.SendEvent(new E_PlayerLeft(playerId));

            Debug.Log($"[NetworkSimulator] 玩家 (id={playerId}) 已离开");
        }

        private void SendStateUpdate(SimPlayer p)
        {
            if (!Application.isPlaying) return;

            var data = ToRemotePlayerData(p);
            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.AddOrUpdateRemotePlayer(data);

            GameApp.Interface.SendEvent(new E_RemoteStateUpdated(p.Id));
        }

        private void SendSnapshotEvent()
        {
            var list = new List<RemotePlayerData>();
            foreach (var p in _players)
            {
                list.Add(ToRemotePlayerData(p));
            }

            var roomModel = GameApp.Interface.GetModel<IRoomModel>();
            roomModel.ClearRemotePlayers();
            foreach (var d in list)
            {
                roomModel.AddOrUpdateRemotePlayer(d);
            }

            GameApp.Interface.SendEvent(new E_RoomSnapshot(list));

            Debug.Log($"[NetworkSimulator] 已发送 Snapshot，包含 {list.Count} 个玩家");
        }

        private static RemotePlayerData ToRemotePlayerData(SimPlayer p)
        {
            return new RemotePlayerData
            {
                PlayerId = p.Id,
                PlayerName = p.Name,
                Phase = p.Phase,
                RemainingSeconds = p.RemainingSeconds,
                CurrentRound = p.CurrentRound,
                TotalRounds = p.TotalRounds,
                IsRunning = p.IsRunning,
            };
        }

        private static int GetDefaultSeconds(PomodoroPhase phase)
        {
            switch (phase)
            {
                case PomodoroPhase.Focus: return 1500;
                case PomodoroPhase.Break: return 300;
                case PomodoroPhase.Completed: return 0;
                default: return 1500;
            }
        }

        private static void AdvancePhase(SimPlayer p)
        {
            switch (p.Phase)
            {
                case PomodoroPhase.Focus:
                    p.Phase = PomodoroPhase.Break;
                    p.RemainingSeconds = 300;
                    break;
                case PomodoroPhase.Break:
                    p.CurrentRound++;
                    if (p.CurrentRound > p.TotalRounds)
                    {
                        p.Phase = PomodoroPhase.Completed;
                        p.RemainingSeconds = 0;
                        p.IsRunning = false;
                        p.AutoTick = false;
                    }
                    else
                    {
                        p.Phase = PomodoroPhase.Focus;
                        p.RemainingSeconds = 1500;
                    }
                    break;
                case PomodoroPhase.Completed:
                    p.IsRunning = false;
                    p.AutoTick = false;
                    break;
            }
        }
    }
}
#endif
