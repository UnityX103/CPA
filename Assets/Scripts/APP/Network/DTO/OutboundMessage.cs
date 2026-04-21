using System;

namespace APP.Network.DTO
{
    [Serializable]
    public class OutboundMessage
    {
        public int v = 1;
        public string type;
    }

    [Serializable]
    public sealed class OutboundCreateRoom : OutboundMessage
    {
        public string playerName;
    }

    [Serializable]
    public sealed class OutboundJoinRoom : OutboundMessage
    {
        public string roomCode;              // was: code
        public string playerName;
    }

    [Serializable]
    public sealed class OutboundLeaveRoom : OutboundMessage
    {
    }

    [Serializable]
    public sealed class OutboundSyncState : OutboundMessage
    {
        public RemoteState state;
    }

    [Serializable]
    public sealed class OutboundIconUpload : OutboundMessage
    {
        public string bundleId;
        public string iconBase64;
    }

    [Serializable]
    public sealed class OutboundIconRequest : OutboundMessage
    {
        public string[] bundleIds;
    }

    [Serializable]
    public sealed class OutboundPing : OutboundMessage
    {
    }
}
