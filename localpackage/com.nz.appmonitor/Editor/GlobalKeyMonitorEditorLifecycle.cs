#if UNITY_EDITOR
using CPA.Monitoring;
using UnityEditor;
using UnityEngine;

namespace CPA.Monitoring.Editor
{
    /// <summary>
    /// Editor 兜底：PlayMode 退出 / Domain Reload / Editor 退出时确保
    /// GlobalKeyMonitor 被 Stop，避免原生 NSEvent monitor 残留导致下次
    /// PlayMode 启动后队列里堆积上次会话残留事件。
    ///
    /// 不依赖 QFramework OnDeinit（Editor 退出 PlayMode 不保证调用 ISystem.OnDeinit）。
    /// </summary>
    [InitializeOnLoad]
    internal static class GlobalKeyMonitorEditorLifecycle
    {
        static GlobalKeyMonitorEditorLifecycle()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // 域重载会先调 [InitializeOnLoad]ctor，所以如果上次 PlayMode 留了 monitor 在跑，
            // 这里也是个保险——Stop() 是幂等的（未启动时 no-op）。
            GlobalKeyMonitor.Stop();
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode
                || change == PlayModeStateChange.EnteredEditMode)
            {
                GlobalKeyMonitor.Stop();
            }
        }

        private static void OnEditorQuitting()
        {
            GlobalKeyMonitor.Stop();
        }
    }
}
#endif
