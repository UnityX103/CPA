using QFramework;

namespace APP.Network.System
{
    public interface IStateSyncSystem : ISystem
    {
        void Tick(float deltaTime);
        void ForceSyncNow();
    }
}
