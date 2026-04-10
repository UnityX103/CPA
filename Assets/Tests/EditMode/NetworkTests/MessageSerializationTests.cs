using System.Collections.Generic;
using APP.Network.DTO;
using NUnit.Framework;
using UnityEngine;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class MessageSerializationTests
    {
        [Test]
        public void InboundMessage_WhenRoundTripped_PreservesSnapshotPayload()
        {
            var message = new InboundMessage
            {
                v = 1,
                type = "room_snapshot",
                snapshot = new List<SnapshotEntry>
                {
                    new SnapshotEntry
                    {
                        playerId = "remote-1",
                        playerName = "Alice",
                        state = new RemoteState
                        {
                            pomodoro = new PomodoroStateDto
                            {
                                phase = 1,
                                remainingSeconds = 300,
                                currentRound = 2,
                                totalRounds = 4,
                                isRunning = true,
                            },
                            activeApp = null,
                        },
                    },
                },
            };

            string json = JsonUtility.ToJson(message);
            InboundMessage restored = JsonUtility.FromJson<InboundMessage>(json);

            Assert.That(restored.v, Is.EqualTo(1));
            Assert.That(restored.type, Is.EqualTo("room_snapshot"));
            Assert.That(restored.snapshot, Has.Count.EqualTo(1));
            Assert.That(restored.snapshot[0].playerName, Is.EqualTo("Alice"));
            Assert.That(restored.snapshot[0].state.pomodoro.remainingSeconds, Is.EqualTo(300));
        }

        [Test]
        public void RemoteStateEqualsLogical_WhenPomodoroMatches_ReturnsTrue()
        {
            var left = new RemoteState
            {
                pomodoro = new PomodoroStateDto
                {
                    phase = 0,
                    remainingSeconds = 1200,
                    currentRound = 1,
                    totalRounds = 4,
                    isRunning = true,
                },
                activeApp = new ActiveAppDto
                {
                    name = "Editor",
                    bundleId = "com.unity.editor",
                    iconId = "unity",
                },
            };
            var right = new RemoteState
            {
                pomodoro = new PomodoroStateDto
                {
                    phase = 0,
                    remainingSeconds = 1200,
                    currentRound = 1,
                    totalRounds = 4,
                    isRunning = true,
                },
                activeApp = new ActiveAppDto
                {
                    name = "Different",
                    bundleId = "another.bundle",
                    iconId = "another",
                },
            };

            Assert.That(RemoteState.EqualsLogical(left, right), Is.True);

            right.pomodoro.remainingSeconds = 1199;

            Assert.That(RemoteState.EqualsLogical(left, right), Is.False);
        }

        [Test]
        public void InboundMessage_WhenOptionalFieldsMissing_UsesDefaultValues()
        {
            const string json = "{\"v\":1,\"type\":\"player_joined\",\"playerId\":\"remote-2\"}";

            InboundMessage restored = JsonUtility.FromJson<InboundMessage>(json);

            Assert.That(restored.v, Is.EqualTo(1));
            Assert.That(restored.type, Is.EqualTo("player_joined"));
            Assert.That(restored.playerId, Is.EqualTo("remote-2"));
            Assert.That(restored.playerName, Is.Null);
            Assert.That(restored.state, Is.Null);
            Assert.That(restored.snapshot, Is.Null);
        }

        [Test]
        public void OutboundSyncState_WhenSerialized_PreservesPomodoroPayload()
        {
            var message = new OutboundSyncState
            {
                v = 1,
                type = "sync_state",
                data = new RemoteState
                {
                    pomodoro = new PomodoroStateDto
                    {
                        phase = 2,
                        remainingSeconds = 0,
                        currentRound = 4,
                        totalRounds = 4,
                        isRunning = false,
                    },
                    activeApp = null,
                },
            };

            string json = JsonUtility.ToJson(message);
            OutboundSyncState restored = JsonUtility.FromJson<OutboundSyncState>(json);

            Assert.That(restored.type, Is.EqualTo("sync_state"));
            Assert.That(restored.data.pomodoro.phase, Is.EqualTo(2));
            Assert.That(restored.data.pomodoro.currentRound, Is.EqualTo(4));
            Assert.That(restored.data.pomodoro.isRunning, Is.False);
        }
    }
}
