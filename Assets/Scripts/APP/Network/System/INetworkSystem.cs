using QFramework;

namespace APP.Network.System
{
    public interface INetworkSystem : ISystem
    {
        void Connect(string serverUrl, string playerName);
        void Disconnect();
        void Send(object message);
        void DrainMainThreadQueue();
    }
}
