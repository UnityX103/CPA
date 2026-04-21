using System;
using System.Collections.Generic;

namespace APP.Network.DTO
{
    [Serializable]
    public sealed class InboundMessage
    {
        public int v;
        public string type;
        public string roomCode;           // was: code
        public string playerId;
        public string playerName;
        public RemoteState state;
        public List<SnapshotEntry> players; // was: snapshot
        public string error;              // was: errorCode + message
        public string bundleId;           // 新增：icon_need / icon_broadcast
        public string iconBase64;         // 新增：icon_broadcast
    }

    [Serializable]
    public sealed class SnapshotEntry
    {
        public string playerId;
        public string playerName;
        public RemoteState state;
    }

    [Serializable]
    public sealed class RemoteState
    {
        public PomodoroStateDto pomodoro;
        public ActiveAppDto activeApp;

        public static bool EqualsLogical(RemoteState left, RemoteState right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return PomodoroStateDto.EqualsLogical(left.pomodoro, right.pomodoro);
        }
    }

    [Serializable]
    public sealed class PomodoroStateDto
    {
        public int phase;
        public int remainingSeconds;
        public int currentRound;
        public int totalRounds;
        public bool isRunning;

        public static bool EqualsLogical(PomodoroStateDto left, PomodoroStateDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return left.phase == right.phase
                && left.remainingSeconds == right.remainingSeconds
                && left.currentRound == right.currentRound
                && left.totalRounds == right.totalRounds
                && left.isRunning == right.isRunning;
        }
    }

    [Serializable]
    public sealed class ActiveAppDto
    {
        public string name;
        public string bundleId;
        public string iconId;
    }
}
