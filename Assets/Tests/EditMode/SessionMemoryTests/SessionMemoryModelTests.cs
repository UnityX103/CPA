using APP.SessionMemory.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;

namespace APP.SessionMemory.Tests
{
    [TestFixture]
    public sealed class SessionMemoryModelTests
    {
        private sealed class TestArch : Architecture<TestArch>
        {
            public static readonly InMemoryStorageUtility SharedStorage = new InMemoryStorageUtility();
            protected override void Init()
            {
                RegisterUtility<IStorageUtility>(SharedStorage);
            }
        }

        private SessionMemoryModel _model;

        [SetUp]
        public void SetUp()
        {
            _ = TestArch.Interface;              // 确保 Init 执行过一次
            TestArch.SharedStorage.Clear();      // 清上次测试遗留
            _model = new SessionMemoryModel();
            ((ICanSetArchitecture)_model).SetArchitecture(TestArch.Interface);
            ((ICanInit)_model).Init();
        }

        [Test]
        public void RememberJoin_InsertsEntry_MostRecentFirst()
        {
            _model.RememberJoin("Alice", "ABC123");
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ABC123"));
            Assert.That(_model.LastRoomCode.Value, Is.EqualTo("ABC123"));
            Assert.That(_model.LastPlayerName.Value, Is.EqualTo("Alice"));
        }

        [Test]
        public void RememberJoin_SameRoomTwice_MovesToFront_NoDuplicate()
        {
            _model.RememberJoin("Alice", "ABC123");
            _model.RememberJoin("Bob", "XYZ789");
            _model.RememberJoin("Alice", "ABC123");

            Assert.That(_model.RecentRooms, Has.Count.EqualTo(2));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ABC123"));
            Assert.That(_model.RecentRooms[1].RoomCode, Is.EqualTo("XYZ789"));
        }

        [Test]
        public void RememberJoin_ExceedsFive_TrimsOldest()
        {
            for (int i = 0; i < 7; i++)
            {
                _model.RememberJoin($"User{i}", $"ROOM{i:D2}");
            }

            Assert.That(_model.RecentRooms, Has.Count.EqualTo(5));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ROOM06"));
            Assert.That(_model.RecentRooms[4].RoomCode, Is.EqualTo("ROOM02"));
        }

        [Test]
        public void ForgetLastRoom_ClearsCode_KeepsHistory()
        {
            _model.RememberJoin("Alice", "ABC123");
            _model.ForgetLastRoom();

            Assert.That(_model.LastRoomCode.Value, Is.EqualTo(string.Empty));
            Assert.That(_model.RecentRooms, Has.Count.EqualTo(1));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("ABC123"));
        }

        [Test]
        public void RemoveHistoryEntry_RemovesMatching()
        {
            _model.RememberJoin("Alice", "ABC123");
            _model.RememberJoin("Bob", "XYZ789");
            _model.RemoveHistoryEntry("ABC123");

            Assert.That(_model.RecentRooms, Has.Count.EqualTo(1));
            Assert.That(_model.RecentRooms[0].RoomCode, Is.EqualTo("XYZ789"));
        }

        [Test]
        public void SetAutoReconnectEnabled_WritesToStorage()
        {
            _model.SetAutoReconnectEnabled(true);
            Assert.That(_model.AutoReconnectEnabled.Value, Is.True);

            _model.SetAutoReconnectEnabled(false);
            Assert.That(_model.AutoReconnectEnabled.Value, Is.False);
        }

        [Test]
        public void RememberJoin_PersistsThroughReload()
        {
            _model.RememberJoin("Alice", "ABC123");
            _model.SetAutoReconnectEnabled(true);

            var reloaded = new SessionMemoryModel();
            ((ICanSetArchitecture)reloaded).SetArchitecture(TestArch.Interface);
            ((ICanInit)reloaded).Init();

            Assert.That(reloaded.LastRoomCode.Value, Is.EqualTo("ABC123"));
            Assert.That(reloaded.LastPlayerName.Value, Is.EqualTo("Alice"));
            Assert.That(reloaded.AutoReconnectEnabled.Value, Is.True);
            Assert.That(reloaded.RecentRooms, Has.Count.EqualTo(1));
        }
    }
}
