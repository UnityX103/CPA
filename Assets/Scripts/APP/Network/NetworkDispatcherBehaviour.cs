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
        private INetworkSystem _networkSystem;

        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private void Awake()
        {
            _networkSystem = this.GetSystem<INetworkSystem>();
        }

        private void Update()
        {
            _networkSystem?.DrainMainThreadQueue();
        }

        private void OnDestroy()
        {
            _networkSystem?.Disconnect();
        }
    }
}
