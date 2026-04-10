using APP.Network.Model;
using NUnit.Framework;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class RoomModelTests
    {
        [Test]
        public void StatusBindableProperty_WhenValueChanges_NotifiesOnce()
        {
            var model = new RoomModel();
            int notifyCount = 0;
            ConnectionStatus observedStatus = ConnectionStatus.Disconnected;

            model.Status.Register(status =>
            {
                notifyCount++;
                observedStatus = status;
            });

            model.SetStatus(ConnectionStatus.Connecting);
            model.SetStatus(ConnectionStatus.Connecting);

            Assert.That(notifyCount, Is.EqualTo(1));
            Assert.That(observedStatus, Is.EqualTo(ConnectionStatus.Connecting));
        }

        [Test]
        public void RemotePlayers_WhenAddedUpdatedRemoved_ReflectLatestCollection()
        {
            var model = new RoomModel();

            model.AddOrUpdateRemotePlayer(new RemotePlayerData
            {
                PlayerId = "p1",
                PlayerName = "Alice",
                RemainingSeconds = 1500,
            });
            model.AddOrUpdateRemotePlayer(new RemotePlayerData
            {
                PlayerId = "p2",
                PlayerName = "Bob",
                RemainingSeconds = 1200,
            });

            Assert.That(model.RemotePlayers.Count, Is.EqualTo(2));
            Assert.That(model.RemotePlayers[0].PlayerName, Is.EqualTo("Alice"));

            model.AddOrUpdateRemotePlayer(new RemotePlayerData
            {
                PlayerId = "p1",
                PlayerName = "Alice Updated",
                RemainingSeconds = 900,
            });

            Assert.That(model.RemotePlayers.Count, Is.EqualTo(2));
            Assert.That(model.RemotePlayers[0].PlayerName, Is.EqualTo("Alice Updated"));
            Assert.That(model.RemotePlayers[0].RemainingSeconds, Is.EqualTo(900));

            model.RemoveRemotePlayer("p2");

            Assert.That(model.RemotePlayers.Count, Is.EqualTo(1));
            Assert.That(model.RemotePlayers[0].PlayerId, Is.EqualTo("p1"));

            model.ClearRemotePlayers();

            Assert.That(model.RemotePlayers, Is.Empty);
        }
    }
}
