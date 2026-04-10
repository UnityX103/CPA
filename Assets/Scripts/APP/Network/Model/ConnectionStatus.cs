namespace APP.Network.Model
{
    /// <summary>
    /// 网络连接状态。
    /// </summary>
    public enum ConnectionStatus
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        InRoom = 3,
        Reconnecting = 4,
        Error = 5,
    }
}
