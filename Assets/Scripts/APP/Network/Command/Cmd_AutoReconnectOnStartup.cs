using APP.SessionMemory.Model;
using QFramework;

namespace APP.Network.Command
{
    /// <summary>
    /// 应用启动时检查 SessionMemory，若开启自动联网且有上次房间码，则发 Cmd_JoinRoom。
    /// </summary>
    public sealed class Cmd_AutoReconnectOnStartup : AbstractCommand
    {
        protected override void OnExecute()
        {
            ISessionMemoryModel memory = this.GetModel<ISessionMemoryModel>();
            if (!memory.AutoReconnectEnabled.Value) return;

            string code = memory.LastRoomCode.Value;
            string name = memory.LastPlayerName.Value;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) return;

            this.SendCommand(new Cmd_JoinRoom(code, name));
        }
    }
}
