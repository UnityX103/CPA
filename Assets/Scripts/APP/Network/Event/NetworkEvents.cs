using System.Collections.Generic;
using APP.Network.Model;

namespace APP.Network.Event
{
    public readonly struct E_RoomCreated
    {
        public readonly string Code;

        public E_RoomCreated(string code)
        {
            Code = code;
        }
    }

    public readonly struct E_RoomJoined
    {
        public readonly string Code;
        public readonly List<RemotePlayerData> InitialPlayers;

        public E_RoomJoined(string code, List<RemotePlayerData> initialPlayers)
        {
            Code = code;
            InitialPlayers = initialPlayers ?? new List<RemotePlayerData>();
        }
    }

    public readonly struct E_PlayerJoined
    {
        public readonly RemotePlayerData Player;

        public E_PlayerJoined(RemotePlayerData player)
        {
            Player = player;
        }
    }

    public readonly struct E_PlayerLeft
    {
        public readonly string PlayerId;

        public E_PlayerLeft(string playerId)
        {
            PlayerId = playerId;
        }
    }

    public readonly struct E_RemoteStateUpdated
    {
        public readonly string PlayerId;

        public E_RemoteStateUpdated(string playerId)
        {
            PlayerId = playerId;
        }
    }

    public readonly struct E_ConnectionStateChanged
    {
        public readonly ConnectionStatus Status;

        public E_ConnectionStateChanged(ConnectionStatus status)
        {
            Status = status;
        }
    }

    public readonly struct E_NetworkError
    {
        public readonly string Code;
        public readonly string Message;

        public E_NetworkError(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }

    public readonly struct E_RoomSnapshot
    {
        public readonly List<RemotePlayerData> Players;

        public E_RoomSnapshot(List<RemotePlayerData> players)
        {
            Players = players ?? new List<RemotePlayerData>();
        }
    }
}
