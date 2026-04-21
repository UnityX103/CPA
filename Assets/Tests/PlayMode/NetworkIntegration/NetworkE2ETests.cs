using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using APP.Network.Command;
using APP.Network.Event;
using APP.Network.Model;
using APP.Pomodoro;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.TestTools;

namespace APP.NetworkIntegration.Tests
{
    [TestFixture]
    public sealed class NetworkE2ETests
    {
        private TestServerHarness _server;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = TestServerHarness.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server?.Dispose();
        }

        [UnityTest]
        public IEnumerator CreateRoom_ReceivesRoomCreatedAndInRoomStatus()
        {
            Assert.That(_server, Is.Not.Null, "harness 未启动");

            bool roomCreated = false;
            string createdCode = null;
            IUnRegister reg = GameApp.Interface.RegisterEvent<E_RoomCreated>(e =>
            {
                roomCreated = true;
                createdCode = e.Code;
            });

            GameApp.Interface.SendCommand(new Cmd_CreateRoom("Alice", _server.Url));

            float timeout = 5f;
            while (!roomCreated && timeout > 0f)
            {
                yield return null;
                timeout -= Time.unscaledDeltaTime;
            }

            reg.UnRegister();

            Assert.That(roomCreated, Is.True, "未在 5 秒内触发 E_RoomCreated");
            Assert.That(createdCode, Is.Not.Null.And.Not.Empty);
            Assert.That(GameApp.Interface.GetModel<IRoomModel>().IsInRoom.Value, Is.True);

            // 清理
            GameApp.Interface.SendCommand(new Cmd_LeaveRoom());
            yield return new WaitForSecondsRealtime(0.5f);
        }

        [UnityTest]
        public IEnumerator TwoClients_JoinAndStateSync()
        {
            Assert.That(_server, Is.Not.Null);

            // A: 真实客户端创建房间
            bool roomCreated = false;
            string roomCode = null;
            IUnRegister r1 = GameApp.Interface.RegisterEvent<E_RoomCreated>(e => { roomCreated = true; roomCode = e.Code; });
            GameApp.Interface.SendCommand(new Cmd_CreateRoom("Alice", _server.Url));

            float timeout = 5f;
            while (!roomCreated && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
            r1.UnRegister();
            Assert.That(roomCreated);

            // B: 裸 WS
            ClientWebSocket wsB = null;
            var connectTask = Task.Run(async () => wsB = await OpenBareWsAsync(_server.Url));
            while (!connectTask.IsCompleted) yield return null;
            Assert.That(wsB, Is.Not.Null);

            bool playerJoined = false;
            IUnRegister r2 = GameApp.Interface.RegisterEvent<E_PlayerJoined>(e =>
            {
                if (e.Player?.PlayerName == "Bob") playerJoined = true;
            });

            string joinJson = $"{{\"v\":1,\"type\":\"join_room\",\"roomCode\":\"{roomCode}\",\"playerName\":\"Bob\"}}";
            var sendTask = Task.Run(async () => await SendJsonAsync(wsB, joinJson));
            while (!sendTask.IsCompleted) yield return null;

            timeout = 5f;
            while (!playerJoined && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
            r2.UnRegister();
            Assert.That(playerJoined, Is.True, "A 未收到 Bob 加入事件");

            // B 推 state → A 应收到
            bool stateReceived = false;
            IUnRegister r3 = GameApp.Interface.RegisterEvent<E_RemoteStateUpdated>(_ => stateReceived = true);

            string stateJson = "{\"v\":1,\"type\":\"player_state_update\",\"state\":{\"pomodoro\":{\"phase\":0,\"remainingSeconds\":1499,\"currentRound\":1,\"totalRounds\":4,\"isRunning\":true},\"activeApp\":null}}";
            var pushTask = Task.Run(async () => await SendJsonAsync(wsB, stateJson));
            while (!pushTask.IsCompleted) yield return null;

            timeout = 5f;
            while (!stateReceived && timeout > 0f) { yield return null; timeout -= Time.unscaledDeltaTime; }
            r3.UnRegister();
            Assert.That(stateReceived, Is.True, "A 未收到 B 的 state 广播");

            // 清理
            var closeTask = Task.Run(async () => await wsB.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None));
            while (!closeTask.IsCompleted) yield return null;
            GameApp.Interface.SendCommand(new Cmd_LeaveRoom());
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ─── 下面 Case 3/4 在后续 Task 里追加 ───

        // ─── 裸 WS helper ───
        private static async Task<ClientWebSocket> OpenBareWsAsync(string url)
        {
            var ws = new ClientWebSocket();
            await ws.ConnectAsync(new System.Uri(url), CancellationToken.None);
            return ws;
        }

        private static Task SendJsonAsync(ClientWebSocket ws, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            return ws.SendAsync(
                new System.ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, CancellationToken.None).AsTask();
        }
    }
}
