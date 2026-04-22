using System.Collections.Generic;
using APP.Network.DTO;
using APP.Network.Event;
using APP.Network.Model;
using APP.Network.System;
using QFramework;

namespace APP.Network.Command
{
    public sealed class Cmd_CreateRoom : AbstractCommand
    {
        private const string DefaultServerUrl = "ws://localhost:8765";

        private readonly string _playerName;
        private readonly string _serverUrl;
        private readonly string _desiredRoomCode;

        public Cmd_CreateRoom(string playerName, string serverUrl = null, string desiredRoomCode = null)
        {
            _playerName = playerName;
            _serverUrl = string.IsNullOrEmpty(serverUrl) ? DefaultServerUrl : serverUrl;
            _desiredRoomCode = desiredRoomCode;
        }

        protected override void OnExecute()
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            room.SetLocalPlayerName(_playerName);
            room.ResetRoomState();
            this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));

            INetworkSystem network = this.GetSystem<INetworkSystem>();
            network.Connect(_serverUrl, _playerName);
            network.Send(new OutboundCreateRoom
            {
                type = "create_room",
                playerName = _playerName,
                roomCode = string.IsNullOrWhiteSpace(_desiredRoomCode)
                    ? string.Empty
                    : _desiredRoomCode.Trim().ToUpperInvariant(),
            });
        }
    }
}
