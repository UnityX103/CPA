using System.Collections.Generic;
using APP.Network.DTO;
using APP.Network.Event;
using APP.Network.Model;
using APP.Network.System;
using APP.SessionMemory.Model;
using QFramework;

namespace APP.Network.Command
{
    public sealed class Cmd_LeaveRoom : AbstractCommand
    {
        protected override void OnExecute()
        {
            IRoomModel room = this.GetModel<IRoomModel>();
            this.GetSystem<INetworkSystem>().Send(new OutboundLeaveRoom { type = "leave_room" });

            room.ResetRoomState();
            this.SendEvent(new E_RoomSnapshot(new List<RemotePlayerData>()));
            room.SetStatus(room.IsConnected.Value ? ConnectionStatus.Connected : ConnectionStatus.Disconnected);

            this.GetModel<ISessionMemoryModel>().ForgetLastRoom();
        }
    }
}
