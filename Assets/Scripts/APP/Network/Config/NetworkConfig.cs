using UnityEngine;

namespace APP.Network.Config
{
    /// <summary>
    /// 当前连接的服务器环境枚举
    /// </summary>
    public enum ServerEnvironment
    {
        Development,
        Production,
    }

    /// <summary>
    /// 网络服务器地址配置。切换 CurrentEnvironment 会影响运行时（含打包后）所有
    /// 从 ActiveServerUrl 读取的地方。
    /// 资源必须放在 Assets/Resources/NetworkConfig.asset 以便打包后仍能通过
    /// Resources.Load 加载。
    /// </summary>
    [CreateAssetMenu(menuName = "APP/Network/配置表", fileName = "NetworkConfig")]
    public sealed class NetworkConfig : ScriptableObject
    {
        private const string ResourcePath = "NetworkConfig";

        [Header("当前生效环境")]
        [Tooltip("客户端将使用下方对应的 URL 连接服务器；打包时此字段被冻结到 Build")]
        public ServerEnvironment CurrentEnvironment = ServerEnvironment.Development;

        [Header("开发环境 URL")]
        [Tooltip("通常为本机 Node 服务 ws://localhost:8039")]
        public string DevelopmentServerUrl = "ws://localhost:8039";

        [Header("生产环境 URL")]
        [Tooltip("部署在华为云的生产服务 ws://113.46.152.120:8039")]
        public string ProductionServerUrl = "ws://113.46.152.120:8039";

        private static NetworkConfig _cached;

        /// <summary>
        /// 当前 ServerEnvironment 对应的 WebSocket URL
        /// </summary>
        public string ActiveServerUrl
        {
            get
            {
                return CurrentEnvironment == ServerEnvironment.Production
                    ? ProductionServerUrl
                    : DevelopmentServerUrl;
            }
        }

        /// <summary>
        /// 运行时单例；若 Resources 下未放置资源则返回带默认值的临时实例并打警告
        /// </summary>
        public static NetworkConfig Instance
        {
            get
            {
                if (_cached != null)
                {
                    return _cached;
                }

                _cached = Resources.Load<NetworkConfig>(ResourcePath);
                if (_cached == null)
                {
                    Debug.LogWarning(
                        $"[NetworkConfig] Resources/{ResourcePath}.asset 未找到，使用默认开发环境 URL。");
                    _cached = CreateInstance<NetworkConfig>();
                }

                return _cached;
            }
        }
    }
}
