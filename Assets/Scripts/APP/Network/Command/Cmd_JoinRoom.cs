using System.Collections.Generic;
using APP.Network.DTO;
using APP.Network.Event;
using APP.Network.Model;
using APP.Network.System;
using QFramework;

namespace APP.Network.Command
{
    public sealed class Cmd_JoinRoom : AbstractCommand
    {
        private const string DefaultServerUrl = "ws://localhost:8765";

        private readonly string _code;
        private readonly string _playerName;
        private readonly string _serverUrl;

        public Cmd_JoinRoom(string code, string playerName, string serverUrl = null)
        {
            _code = code;
            _playerName = playerName;
            _serverUrl = string.IsNullOrEmpty(serverUrl) ? DefaultServerUrl : serverUrl;
        }

        protected override void OnExecute()
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            room.SetLocalPlayerName(_playerName);
            room.ResetRoomState();
            this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));

            INetworkSystem network = this.GetSystem<INetworkSystem>();
            network.Connect(_serverUrl, _playerName);
            network.Send(new OutboundJoinRoom
            {
                type = "join_room",
                code = _code,
                playerName = _playerName,
            });
        }
    }
}
