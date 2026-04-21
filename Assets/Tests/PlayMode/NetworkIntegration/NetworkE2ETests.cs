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

        // ─── 下面 Case 2/3/4 在后续 Task 里追加 ───

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
