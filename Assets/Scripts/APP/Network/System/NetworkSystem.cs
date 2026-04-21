using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using APP.Network.DTO;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro.Model;
using APP.SessionMemory.Model;
using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public sealed class NetworkSystem : AbstractSystem, INetworkSystem
    {
        private const int MaxReconnectAttempts = 5;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private readonly ConcurrentQueue<string> _pendingMessages = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private string _serverUrl;
        private string _playerName;

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            Disconnect();
            _sendLock.Dispose();
        }

        public void Connect(string serverUrl, string playerName)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return;
            }

            _serverUrl = serverUrl;
            _playerName = playerName ?? string.Empty;

            if (_ws != null && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.Connecting))
            {
                return;
            }

            if (_cts == null || _cts.IsCancellationRequested)
            {
                _cts = new CancellationTokenSource();
            }

            IRoomModel roomModel = this.GetModel<IRoomModel>();
            roomModel.SetStatus(ConnectionStatus.Connecting);
            roomModel.SetConnectionFlags(false, false);
            roomModel.SetRoomCode(string.Empty);
            roomModel.SetLocalPlayerName(_playerName);
            roomModel.SetLocalPlayerId(string.Empty);
            roomModel.ClearRemotePlayers();
            EnqueueMainThread(() => this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.Connecting)));

            _ = RunSessionAsync(_serverUrl, _playerName, _cts, 0);
        }

        public void Disconnect()
        {
            _ = DisconnectInternalAsync();
        }

        public void Send(object message)
        {
            if (message == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(message);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            CancellationTokenSource session = _cts;
            ClientWebSocket socket = _ws;

            if (session == null || session.IsCancellationRequested)
            {
                return;
            }

            if (socket == null || socket.State != WebSocketState.Open)
            {
                _pendingMessages.Enqueue(json);
                return;
            }

            _ = SendRawAsync(json, session.Token);
        }

        public void DrainMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private async Task RunSessionAsync(
            string serverUrl,
            string playerName,
            CancellationTokenSource session,
            int reconnectAttempt)
        {
            if (!IsSessionActive(session))
            {
                return;
            }

            if (reconnectAttempt > 0)
            {
                int delaySeconds = 1 << reconnectAttempt;
                EnqueueMainThread(() =>
                {
                    IRoomModel room = this.GetModel<IRoomModel>();
                    room.SetStatus(ConnectionStatus.Reconnecting);
                    room.SetConnectionFlags(false, room.IsInRoom.Value);
                    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.Reconnecting));
                });

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), session.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!IsSessionActive(session))
                {
                    return;
                }
            }

            var socket = new ClientWebSocket();
            _ws = socket;

            bool connected = false;
            bool shouldReconnect = false;
            string errorCode = string.Empty;
            string errorMessage = string.Empty;

            try
            {
                await socket.ConnectAsync(new Uri(serverUrl), session.Token);
                connected = true;

                EnqueueMainThread(() =>
                {
                    if (!IsSessionActive(session))
                    {
                        return;
                    }

                    IRoomModel room = this.GetModel<IRoomModel>();
                    room.SetConnectionFlags(true, room.IsInRoom.Value);
                    room.SetStatus(room.IsInRoom.Value ? ConnectionStatus.InRoom : ConnectionStatus.Connected);
                    room.SetLocalPlayerName(playerName);
                    this.SendEvent(new E_ConnectionStateChanged(room.Status.Value));
                });

                await FlushPendingMessagesAsync(session.Token);
                await SendRejoinMessageIfNeededAsync(session.Token, reconnectAttempt);
                shouldReconnect = await ReceiveLoopAsync(socket, session.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                shouldReconnect = IsSessionActive(session);
                errorMessage = ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_ws, socket))
                {
                    _ws = null;
                }

                socket.Dispose();
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                errorCode = "transport_error";
                EnqueueMainThread(() =>
                {
                    this.SendEvent(new E_NetworkError(errorCode, errorMessage));
                });
            }

            if (!shouldReconnect || !IsSessionActive(session))
            {
                return;
            }

            int nextAttempt = connected ? 1 : reconnectAttempt + 1;
            if (nextAttempt > MaxReconnectAttempts)
            {
                EnqueueMainThread(() =>
                {
                    IRoomModel room = this.GetModel<IRoomModel>();
                    room.SetStatus(ConnectionStatus.Error);
                    room.SetConnectionFlags(false, room.IsInRoom.Value);
                    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.Error));
                });
                return;
            }

            await RunSessionAsync(serverUrl, playerName, session, nextAttempt);
        }

        private async Task<bool> ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            var buffer = new byte[8192];

            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return !token.IsCancellationRequested;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                string json = Encoding.UTF8.GetString(ms.ToArray());
                InboundMessage inbound = JsonUtility.FromJson<InboundMessage>(json);

                EnqueueMainThread(() => DispatchInbound(inbound));
            }

            return !token.IsCancellationRequested;
        }

        private async Task<bool> SendRawAsync(string json, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                await _sendLock.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            try
            {
                ClientWebSocket socket = _ws;
                if (socket == null || socket.State != WebSocketState.Open)
                {
                    return false;
                }

                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                EnqueueMainThread(() => this.SendEvent(new E_NetworkError("send_failed", ex.Message)));
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task FlushPendingMessagesAsync(CancellationToken token)
        {
            while (_pendingMessages.TryDequeue(out string json))
            {
                bool sent = await SendRawAsync(json, token);
                if (sent)
                {
                    continue;
                }

                _pendingMessages.Enqueue(json);
                break;
            }
        }

        private async Task SendRejoinMessageIfNeededAsync(CancellationToken token, int reconnectAttempt)
        {
            if (reconnectAttempt <= 0 || !_pendingMessages.IsEmpty)
            {
                return;
            }

            IRoomModel room = this.GetModel<IRoomModel>();
            if (!room.IsInRoom.Value
                || string.IsNullOrWhiteSpace(room.RoomCode.Value)
                || string.IsNullOrWhiteSpace(room.LocalPlayerName.Value))
            {
                return;
            }

            var rejoinMessage = new OutboundJoinRoom
            {
                type = "join_room",
                roomCode = room.RoomCode.Value,
                playerName = room.LocalPlayerName.Value,
            };

            await SendRawAsync(JsonUtility.ToJson(rejoinMessage), token);
        }

        private void DispatchInbound(InboundMessage inbound)
        {
            if (inbound == null || string.IsNullOrWhiteSpace(inbound.type))
            {
                return;
            }

            switch (inbound.type)
            {
                case "room_created":
                    HandleRoomCreated(inbound);
                    break;
                case "room_joined":
                    HandleRoomJoined(inbound);
                    break;
                case "room_snapshot":
                    HandleRoomSnapshot(inbound);
                    break;
                case "player_joined":
                    HandlePlayerJoined(inbound);
                    break;
                case "player_left":
                    HandlePlayerLeft(inbound);
                    break;
                case "player_state_broadcast":
                    HandleStateUpdated(inbound);
                    break;
                case "error":
                    HandleNetworkError(inbound);
                    break;
                case "icon_need":
                    HandleIconNeed(inbound);
                    break;
                case "icon_broadcast":
                    HandleIconBroadcast(inbound);
                    break;
                case "pong":
                    break;
            }
        }

        private void HandleRoomCreated(InboundMessage inbound)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            room.SetRoomCode(inbound.roomCode);
            room.SetLocalPlayerId(inbound.playerId);
            room.SetConnectionFlags(true, true);
            room.SetStatus(ConnectionStatus.InRoom);

            // room_created 不含 players，等随后的 room_snapshot 补齐
            room.ApplySnapshot(new List<RemotePlayerData>());

            this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.InRoom));
            this.SendEvent(new E_RoomCreated(inbound.roomCode));
            this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));

            this.GetModel<ISessionMemoryModel>().RememberJoin(
                this.GetModel<IRoomModel>().LocalPlayerName.Value,
                inbound.roomCode);
        }

        private void HandleRoomJoined(InboundMessage inbound)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            room.SetRoomCode(inbound.roomCode);
            room.SetLocalPlayerId(inbound.playerId);
            room.SetConnectionFlags(true, true);
            room.SetStatus(ConnectionStatus.InRoom);

            room.ApplySnapshot(new List<RemotePlayerData>());

            this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.InRoom));
            this.SendEvent(new E_RoomJoined(inbound.roomCode, new List<RemotePlayerData>()));
            this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));

            this.GetModel<ISessionMemoryModel>().RememberJoin(
                this.GetModel<IRoomModel>().LocalPlayerName.Value,
                inbound.roomCode);
        }

        private void HandleRoomSnapshot(InboundMessage inbound)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            List<RemotePlayerData> players = BuildRemotePlayers(inbound.players, room.LocalPlayerId.Value);
            room.ApplySnapshot(players);
            this.SendEvent(new E_RoomSnapshot(ClonePlayers(players)));

            RequestMissingIcons(players);
        }

        private void HandlePlayerJoined(InboundMessage inbound)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            if (inbound.players == null || inbound.players.Count == 0)
            {
                return;
            }

            SnapshotEntry entry = inbound.players[0];
            if (string.IsNullOrWhiteSpace(entry.playerId) || entry.playerId == room.LocalPlayerId.Value)
            {
                return;
            }

            RemotePlayerData player = ToRemotePlayerData(entry.playerId, entry.playerName, entry.state);
            room.AddOrUpdateRemotePlayer(player);

            this.SendEvent(new E_PlayerJoined(player.Clone()));
        }

        private void HandlePlayerLeft(InboundMessage inbound)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            room.RemoveRemotePlayer(inbound.playerId);
            this.SendEvent(new E_PlayerLeft(inbound.playerId));
        }

        private void HandleStateUpdated(InboundMessage inbound)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            if (string.IsNullOrWhiteSpace(inbound.playerId) || inbound.playerId == room.LocalPlayerId.Value)
            {
                return;
            }

            RemotePlayerData player = ToRemotePlayerData(inbound.playerId, inbound.playerName, inbound.state);
            room.AddOrUpdateRemotePlayer(player);

            this.SendEvent(new E_RemoteStateUpdated(inbound.playerId));
        }

        private void HandleNetworkError(InboundMessage inbound)
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            room.SetStatus(ConnectionStatus.Error);
            string code = string.IsNullOrEmpty(inbound.error) ? "UNKNOWN" : inbound.error;

            if (code == "ROOM_NOT_FOUND")
            {
                this.GetModel<ISessionMemoryModel>().ForgetLastRoom();
            }

            this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.Error));
            this.SendEvent(new E_NetworkError(code, string.Empty));
        }

        private void HandleIconNeed(InboundMessage inbound)
        {
            if (string.IsNullOrEmpty(inbound.bundleId)) return;

            IActiveAppSystem appSys = this.GetSystem<IActiveAppSystem>();
            ActiveAppSnapshot snap = appSys.Current;
            if (snap.BundleId != inbound.bundleId || snap.IconPngBytes == null)
            {
                // 当前已切到别的 App 了，忽略（服务端下次再问）
                return;
            }

            IIconCacheSystem iconCache = this.GetSystem<IIconCacheSystem>();
            string base64 = iconCache.EncodeBase64FromPngBytes(snap.IconPngBytes);
            if (string.IsNullOrEmpty(base64)) return;

            Send(new OutboundIconUpload
            {
                type = "icon_upload",
                bundleId = inbound.bundleId,
                iconBase64 = base64,
            });
        }

        private void HandleIconBroadcast(InboundMessage inbound)
        {
            if (string.IsNullOrEmpty(inbound.bundleId) || string.IsNullOrEmpty(inbound.iconBase64)) return;
            IIconCacheSystem iconCache = this.GetSystem<IIconCacheSystem>();
            iconCache.StoreFromBase64(inbound.bundleId, inbound.iconBase64);
            this.SendEvent(new E_IconUpdated(inbound.bundleId));
        }

        private void RequestMissingIcons(List<RemotePlayerData> players)
        {
            if (players == null || players.Count == 0) return;
            IIconCacheSystem iconCache = this.GetSystem<IIconCacheSystem>();

            var missing = new HashSet<string>();
            foreach (RemotePlayerData p in players)
            {
                if (!string.IsNullOrEmpty(p.ActiveAppBundleId) && !iconCache.HasIconFor(p.ActiveAppBundleId))
                {
                    missing.Add(p.ActiveAppBundleId);
                }
            }

            if (missing.Count == 0) return;

            var arr = new string[missing.Count];
            missing.CopyTo(arr);
            Send(new OutboundIconRequest { type = "icon_request", bundleIds = arr });
        }

        private List<RemotePlayerData> BuildRemotePlayers(IList<SnapshotEntry> snapshot, string localPlayerId)
        {
            var players = new List<RemotePlayerData>();

            if (snapshot == null)
            {
                return players;
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                SnapshotEntry entry = snapshot[i];
                if (entry == null || entry.playerId == localPlayerId)
                {
                    continue;
                }

                players.Add(ToRemotePlayerData(entry.playerId, entry.playerName, entry.state));
            }

            return players;
        }

        private RemotePlayerData ToRemotePlayerData(string playerId, string playerName, RemoteState state)
        {
            PomodoroStateDto pomodoro = state?.pomodoro;
            ActiveAppDto activeApp = state?.activeApp;

            return new RemotePlayerData
            {
                PlayerId = playerId ?? string.Empty,
                PlayerName = playerName ?? string.Empty,
                Phase = pomodoro != null && Enum.IsDefined(typeof(PomodoroPhase), pomodoro.phase)
                    ? (PomodoroPhase)pomodoro.phase
                    : PomodoroPhase.Focus,
                RemainingSeconds = pomodoro?.remainingSeconds ?? 0,
                CurrentRound = pomodoro?.currentRound ?? 0,
                TotalRounds = pomodoro?.totalRounds ?? 0,
                IsRunning = pomodoro?.isRunning ?? false,
                ActiveAppName = activeApp?.name,
                ActiveAppBundleId = activeApp?.bundleId,
                ActiveAppIconId = activeApp?.iconId,
            };
        }

        private List<RemotePlayerData> ClonePlayers(IList<RemotePlayerData> source)
        {
            var clones = new List<RemotePlayerData>();

            if (source == null)
            {
                return clones;
            }

            for (int i = 0; i < source.Count; i++)
            {
                RemotePlayerData player = source[i];
                if (player != null)
                {
                    clones.Add(player.Clone());
                }
            }

            return clones;
        }

        private void EnqueueMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            _mainThreadQueue.Enqueue(action);
        }

        private bool IsSessionActive(CancellationTokenSource session)
        {
            return session != null
                && ReferenceEquals(_cts, session)
                && !session.IsCancellationRequested;
        }

        private async Task DisconnectInternalAsync()
        {
            CancellationTokenSource session = _cts;
            ClientWebSocket socket = _ws;

            _cts = null;
            _ws = null;

            try
            {
                session?.Cancel();

                if (socket != null && socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "client disconnect",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                EnqueueMainThread(() => this.SendEvent(new E_NetworkError("disconnect_failed", ex.Message)));
            }
            finally
            {
                socket?.Dispose();
                session?.Dispose();

                while (_mainThreadQueue.TryDequeue(out _))
                {
                }

                while (_pendingMessages.TryDequeue(out _))
                {
                }

                EnqueueMainThread(() =>
                {
                    IRoomModel room = this.GetModel<IRoomModel>();
                    room.SetConnectionFlags(false, false);
                    room.SetStatus(ConnectionStatus.Disconnected);
                    room.ResetRoomState();
                    this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));
                    this.SendEvent(new E_ConnectionStateChanged(ConnectionStatus.Disconnected));
                });
            }
        }
    }
}
