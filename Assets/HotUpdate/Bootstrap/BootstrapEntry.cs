using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Bootstrap
{
    /// <summary>
    /// AOT 侧的启动入口。挂在 Init 场景的 Bootstrap GameObject 上，按顺序完成：
    ///   1. 初始化 Addressables。
    ///   2. 通过 Addressables 拉取 AOT 泛型补充元数据 DLL，注册到 HybridCLR。
    ///   3. 通过 Addressables 拉取 App.Hotfix.dll.bytes，反射调用 App.Hotfix.HotfixEntry.Start()。
    ///   4. 加载主场景（默认 MainV2）。
    /// Editor 下走 Assembly 直接查找，无需走真实 AA 链路；这样在编辑器里仍能调试原代码。
    /// </summary>
    public sealed class BootstrapEntry : MonoBehaviour
    {
        [Tooltip("挂在 Init 场景的 BootstrapEntry，DontDestroyOnLoad 跟随主流程")]
        [SerializeField] private bool _dontDestroyOnLoad = true;

        [Tooltip("是否在 Awake 自动启动加载流程；如果想手动控制就关掉")]
        [SerializeField] private bool _autoStart = true;

        [Tooltip("热更新加载完成后要切到的主场景；通过 SceneManager.LoadScene 加载，需在 Build Settings 里勾选或走 Addressables。")]
        [SerializeField] private string _nextSceneName = "MainV2";

        [Tooltip("启用后，主场景通过 Addressables.LoadSceneAsync 加载（要求 MainV2 标记为可加载的 Addressable 资源）；关闭则走 SceneManager.LoadSceneAsync。\n注意：Player Build Settings 通常只勾选 Init.unity，因此默认必须为 true，否则主场景不在 player 内会失败。")]
        [SerializeField] private bool _loadNextSceneViaAddressables = true;

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

            bool fatal = false;
            yield return LoadHotfixSystem.RunAsync(ex =>
            {
                fatal = true;
                OnFatal(ex);
            });

            sw.Stop();
            Debug.Log($"[Bootstrap] hot-update finished in {sw.ElapsedMilliseconds} ms.");

            if (fatal)
            {
                Debug.LogError("[Bootstrap] 因致命错误中止主场景加载。");
                yield break;
            }

            if (string.IsNullOrEmpty(_nextSceneName))
            {
                Debug.Log("[Bootstrap] _nextSceneName 为空，跳过主场景加载。");
                yield break;
            }

            yield return LoadNextScene();
        }

        private IEnumerator LoadNextScene()
        {
            Debug.Log($"[Bootstrap] 开始加载主场景：{_nextSceneName} (viaAA={_loadNextSceneViaAddressables})");

            if (_loadNextSceneViaAddressables)
            {
                var handle = UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(
                    _nextSceneName, LoadSceneMode.Single);
                yield return handle;
                if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    OnFatal(new InvalidOperationException(
                        $"Addressables.LoadSceneAsync('{_nextSceneName}') 失败：{handle.OperationException}"));
                }
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(_nextSceneName, LoadSceneMode.Single);
            if (op == null)
            {
                OnFatal(new InvalidOperationException(
                    $"SceneManager.LoadSceneAsync 返回 null，请确认 '{_nextSceneName}' 在 Build Settings 里启用。"));
                yield break;
            }
            yield return op;
        }

        private void OnFatal(Exception ex)
        {
            Debug.LogError($"[Bootstrap] hot-update 致命错误：{ex}");
            // TODO: 显示带「重试」按钮的故障 UI。先把错误打到 console。
        }
    }
}
