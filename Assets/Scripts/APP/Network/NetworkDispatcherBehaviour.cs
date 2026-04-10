using APP.Network.System;
using APP.Pomodoro;
using QFramework;
using UnityEngine;

namespace APP.Network
{
    /// <summary>
    /// 把网络线程回调安全地切回 Unity 主线程。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class NetworkDispatcherBehaviour : MonoBehaviour, IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private void Update()
        {
            this.GetSystem<INetworkSystem>().DrainMainThreadQueue();
        }

        private void OnDestroy()
        {
            this.GetSystem<INetworkSystem>().Disconnect();
        }
    }
}
