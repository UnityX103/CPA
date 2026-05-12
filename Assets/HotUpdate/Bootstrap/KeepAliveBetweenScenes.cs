using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// 挂在希望跨场景存活的根 GameObject 上。
    /// Awake 时调用 DontDestroyOnLoad，把自身移入 DDOL 场景；
    /// 多实例去重（例如重新加载 Init 场景），避免 UniWindowController 重复存在。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeepAliveBetweenScenes : MonoBehaviour
    {
        [Tooltip("用于多实例去重的标记；同一标记的多个实例只留首个。留空则按 GameObject 名比较。\n注意：组件需要挂在根 GameObject 上，挂在子物体会被自动 detach 到根层级，会破坏原有父子关系。")]
        [SerializeField] private string _uniqueKey;

        private static readonly System.Collections.Generic.HashSet<string> AliveKeys =
            new System.Collections.Generic.HashSet<string>();

        // Editor 关掉 Domain Reload 时（Enter Play Mode Options），static 字段会跨 PlayMode 残留，
        // 导致首次重启 PlayMode 时合法实例被当作重复实例销毁。SubsystemRegistration 阶段在每次进入
        // PlayMode 前会执行，确保 HashSet 干净。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            AliveKeys.Clear();
        }

        // 标记该实例是否真正占用了 AliveKeys，避免重复实例的 OnDestroy 错误地清掉合法实例的 key
        private bool _ownsKey;
        private string _registeredKey;

        private void Awake()
        {
            string key = string.IsNullOrEmpty(_uniqueKey) ? gameObject.name : _uniqueKey;
            if (!AliveKeys.Add(key))
            {
                // 已有其它实例占据该 key — 本实例不应触碰 AliveKeys
                _ownsKey = false;
                Destroy(gameObject);
                return;
            }
            _ownsKey = true;
            _registeredKey = key;

            if (transform.parent != null)
            {
                // DontDestroyOnLoad 要求根对象，自动 detach 到根层级；调用方需自行保证不依赖原有父子关系。
                Debug.LogWarning(
                    $"[KeepAlive] {name} 不是根对象，已自动 transform.SetParent(null)。建议把它直接挂在场景根。");
                transform.SetParent(null, worldPositionStays: true);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (!_ownsKey) return;
            AliveKeys.Remove(_registeredKey);
        }
    }
}
