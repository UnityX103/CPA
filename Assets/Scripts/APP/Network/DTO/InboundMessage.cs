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
        public BindingKeyDto bindingKey;

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

            return PomodoroStateDto.EqualsLogical(left.pomodoro, right.pomodoro)
                && BindingKeyDto.EqualsLogical(left.bindingKey, right.bindingKey);
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

    /// <summary>
    /// 远端同步：本端通过 BindingKey 设置面板"标记同步"选中的那个 entry 的快照。
    /// 当本端没有 SyncedKeyId 或该 entry 已被删除时，整个 RemoteState.bindingKey 为 null，
    /// 接收端据此把 PlayerCard 的 KeyCounterPill 隐藏。
    /// 不传 entryId / KeyCode，只传可展示字段（label + count）。
    /// </summary>
    [Serializable]
    public sealed class BindingKeyDto
    {
        public string keyLabel;
        public int pressCount;

        /// <summary>
        /// 仅比较结构性字段（keyLabel + null-or-not）；pressCount 故意不参与判等，
        /// 否则用户连续按键时会每秒触发一次"changed"立即重发。
        /// pressCount 的最终值通过 StateSyncSystem 的 5s heartbeat 兜底，
        /// 显式 ForceSyncNow（synced id 变化）也会带最新计数。
        /// </summary>
        public static bool EqualsLogical(BindingKeyDto left, BindingKeyDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.keyLabel ?? string.Empty, right.keyLabel ?? string.Empty);
        }
    }
}
