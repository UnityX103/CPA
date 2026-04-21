using System.Collections.Generic;
using APP.SessionMemory.Model;
using NUnit.Framework;

namespace APP.SessionMemory.Tests
{
    [TestFixture]
    public sealed class HistoryRoomSerializerTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var entries = new List<HistoryRoomEntry>
            {
                new HistoryRoomEntry { RoomCode = "ABC123", LastPlayerName = "小明", LastJoinedAtUnixMs = 1700000000000L },
                new HistoryRoomEntry { RoomCode = "XYZ789", LastPlayerName = "小红", LastJoinedAtUnixMs = 1700000001000L },
            };

            string json = HistoryRoomSerializer.Serialize(entries);
            List<HistoryRoomEntry> restored = HistoryRoomSerializer.Deserialize(json);

            Assert.That(restored, Has.Count.EqualTo(2));
            Assert.That(restored[0].RoomCode, Is.EqualTo("ABC123"));
            Assert.That(restored[0].LastPlayerName, Is.EqualTo("小明"));
            Assert.That(restored[0].LastJoinedAtUnixMs, Is.EqualTo(1700000000000L));
        }

        [Test]
        public void Deserialize_EmptyString_ReturnsEmptyList()
        {
            Assert.That(HistoryRoomSerializer.Deserialize(""), Is.Empty);
        }

        [Test]
        public void Deserialize_Garbage_ReturnsEmptyList()
        {
            Assert.That(HistoryRoomSerializer.Deserialize("not-json"), Is.Empty);
        }
    }
}
