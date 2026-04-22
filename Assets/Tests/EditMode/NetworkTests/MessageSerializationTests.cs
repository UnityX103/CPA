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
        public void InboundMessage_WhenRoundTripped_PreservesPlayersPayload()
        {
            var message = new InboundMessage
            {
                v = 1,
                type = "room_snapshot",
                roomCode = "ABC123",
                players = new List<SnapshotEntry>
                {
                    new SnapshotEntry
                    {
                        playerId = "remote-1",
                        playerName = "Alice",
                        state = new RemoteState
                        {
                            pomodoro = new PomodoroStateDto
                            {
                                phase = 1, remainingSeconds = 300,
                                currentRound = 2, totalRounds = 4, isRunning = true,
                            },
                            activeApp = null,
                        },
                    },
                },
            };

            string json = JsonUtility.ToJson(message);
            InboundMessage restored = JsonUtility.FromJson<InboundMessage>(json);

            Assert.That(restored.roomCode, Is.EqualTo("ABC123"));
            Assert.That(restored.players, Has.Count.EqualTo(1));
            Assert.That(restored.players[0].playerName, Is.EqualTo("Alice"));
            Assert.That(restored.players[0].state.pomodoro.remainingSeconds, Is.EqualTo(300));
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
        public void InboundMessage_WhenErrorFieldProvided_ParsesErrorCode()
        {
            const string json = "{\"v\":1,\"type\":\"error\",\"error\":\"ROOM_FULL\"}";

            InboundMessage restored = JsonUtility.FromJson<InboundMessage>(json);

            Assert.That(restored.type, Is.EqualTo("error"));
            Assert.That(restored.error, Is.EqualTo("ROOM_FULL"));
        }

        [Test]
        public void OutboundJoinRoom_WhenSerialized_UsesRoomCodeField()
        {
            var msg = new OutboundJoinRoom
            {
                type = "join_room",
                roomCode = "XYZ789",
                playerName = "Bob",
            };

            string json = JsonUtility.ToJson(msg);
            Assert.That(json, Does.Contain("\"roomCode\":\"XYZ789\""));
            Assert.That(json, Does.Not.Contain("\"code\":"));
        }

        [Test]
        public void OutboundSyncState_WhenSerialized_PreservesPomodoroPayload()
        {
            var message = new OutboundSyncState
            {
                v = 1,
                type = "player_state_update",
                state = new RemoteState
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

            Assert.That(restored.type, Is.EqualTo("player_state_update"));
            Assert.That(restored.state.pomodoro.phase, Is.EqualTo(2));
            Assert.That(restored.state.pomodoro.currentRound, Is.EqualTo(4));
            Assert.That(restored.state.pomodoro.isRunning, Is.False);
        }
    }
}
