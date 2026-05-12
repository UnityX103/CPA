using System;
using System.Collections;
using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// AOT 侧的启动入口。挂在 MainV2 场景的某个 GameObject 上，
    /// 它会依次完成：
    ///   1. 初始化 Addressables。
    ///   2. 通过 Addressables 拉取 AOT 泛型补充元数据 DLL，注册到 HybridCLR。
    ///   3. 通过 Addressables 拉取 App.Hotfix.dll.bytes，反射调用 App.Hotfix.HotfixEntry.Start()。
    /// Editor 下走 Assembly 直接查找，无需走真实 AA 链路；这样在编辑器里仍能调试原代码。
    /// </summary>
    public sealed class BootstrapEntry : MonoBehaviour
    {
        [Tooltip("挂在场景里的 BootstrapEntry，DontDestroyOnLoad 跟随主流程")]
        [SerializeField] private bool _dontDestroyOnLoad = true;

        [Tooltip("是否在 Awake 自动启动加载流程；如果想手动控制就关掉")]
        [SerializeField] private bool _autoStart = true;

        private bool _started;

        private void Awake()
        {
            if (_dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private IEnumerator Start()
        {
            if (!_autoStart) yield break;
            yield return RunLoad();
        }

        /// <summary>
        /// 也允许外部手动驱动 — 比如先显示一个 splash UI 再来调用。
        /// </summary>
        public IEnumerator RunLoad()
        {
            if (_started)
            {
                Debug.LogWarning("[Bootstrap] RunLoad 已被调用过，忽略重入。");
                yield break;
            }
            _started = true;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("[Bootstrap] starting hot-update load pipeline.");

            yield return LoadHotfixSystem.RunAsync(OnFatal);
            sw.Stop();
            Debug.Log($"[Bootstrap] hot-update finished in {sw.ElapsedMilliseconds} ms.");
        }

        private void OnFatal(Exception ex)
        {
            Debug.LogError($"[Bootstrap] hot-update 致命错误：{ex}");
            // TODO: 显示带「重试」按钮的故障 UI。先 throw 让上层日志可见。
        }
    }
}
