using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace APP.Pomodoro.Native
{
    public static class NativeFilePicker
    {
        public static Func<IDisposable> TopmostGuardFactory { get; set; }

        internal static Func<string> PickerOverride { get; set; }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR_OSX
        // 原生面板封装在 Assets/Plugins/macOS/NativeFilePicker.bundle 中，
        // bundle 文件名（不含扩展名）就是 DllImport 名。Editor 走 EditorUtility，
        // 所以编辑器下不需要解析这个符号。
        private const string NativePluginName = "NativeFilePicker";

        [DllImport(NativePluginName)]
        private static extern IntPtr cpa_native_pick_video_file();
#endif

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        // 前台窗口屏幕查询：Editor 也能用，因为 Editor 已经把 NativeFilePicker.bundle
        // 当 macOS Editor 插件加载（见 .bundle.meta 的 Editor: OSX enabled）。
        private const string ScreenQueryPluginName = "NativeFilePicker";

        [DllImport(ScreenQueryPluginName)]
        private static extern int cpa_native_get_frontmost_window_screen_index();
#endif

        /// <summary>
        /// 取前台聚焦应用主窗口所在的 NSScreen 索引（即 [NSScreen screens] 顺序）。
        /// 返回值含义：
        /// - 非负：屏幕索引，调用方可直接交给 UniWindowController.GetMonitorRect / WindowPositionSystem。
        /// - -1：未拿到（前台是自己 / 没 Accessibility 授权 / 焦点窗口不存在 / 平台不支持）。
        /// 不抛异常；任何 native 异常都吞掉返回 -1。
        /// </summary>
        public static int TryGetFrontmostWindowScreenIndex()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            try
            {
                int idx = cpa_native_get_frontmost_window_screen_index();
                Debug.Log($"[NativeFilePicker] cpa_native_get_frontmost_window_screen_index -> {idx}");
                return idx;
            }
            catch (DllNotFoundException dllEx)
            {
                Debug.LogWarning($"[NativeFilePicker] DllNotFoundException 取前台屏幕：{dllEx.Message}");
                return -1;
            }
            catch (EntryPointNotFoundException epEx)
            {
                Debug.LogWarning($"[NativeFilePicker] EntryPointNotFoundException 取前台屏幕：{epEx.Message}");
                return -1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NativeFilePicker] 取前台屏幕异常：{ex}");
                return -1;
            }
#else
            return -1;
#endif
        }

        /// <summary>
        /// 阻塞式选择本地视频文件；用户取消、失败或当前平台不支持时返回 null。
        /// </summary>
        public static string PickVideoFile()
        {
            Debug.Log($"[NativeFilePicker] PickVideoFile 进入。platform={Application.platform}, override={(PickerOverride != null)}, guardFactory={(TopmostGuardFactory != null)}");

            try
            {
                using (new TopmostOverrideScope())
                {
                    if (PickerOverride != null)
                    {
                        Debug.Log("[NativeFilePicker] 走 PickerOverride 分支（测试或外部注入）");
                        string overridePath = NormalizeSelectedPath(PickerOverride.Invoke());
                        Debug.Log($"[NativeFilePicker] PickerOverride 返回={overridePath ?? "<null>"}");
                        return overridePath;
                    }

#if UNITY_EDITOR_OSX
                    Debug.Log("[NativeFilePicker] 走 UNITY_EDITOR_OSX 分支：调 EditorUtility.OpenFilePanelWithFilters");
                    string selectedPath = EditorUtility.OpenFilePanelWithFilters(
                        "选择计时结束播放的视频",
                        string.Empty,
                        new[]
                        {
                            "视频文件",
                            "mp4,mov,m4v,webm"
                        });
                    Debug.Log($"[NativeFilePicker] Editor 面板返回 selectedPath='{selectedPath ?? "<null>"}'");

                    return NormalizeSelectedPath(selectedPath);
#elif UNITY_STANDALONE_OSX
                    Debug.Log("[NativeFilePicker] 走 UNITY_STANDALONE_OSX 分支：准备 P/Invoke cpa_native_pick_video_file()");
                    IntPtr ptr;
                    try
                    {
                        ptr = cpa_native_pick_video_file();
                    }
                    catch (DllNotFoundException dllEx)
                    {
                        Debug.LogError($"[NativeFilePicker] DllNotFoundException：未能加载 {NativePluginName} bundle。{dllEx}");
                        return null;
                    }
                    catch (EntryPointNotFoundException epEx)
                    {
                        Debug.LogError($"[NativeFilePicker] EntryPointNotFoundException：bundle 已加载但找不到符号 cpa_native_pick_video_file。{epEx}");
                        return null;
                    }

                    Debug.Log($"[NativeFilePicker] 原生 picker 返回 ptr={(ptr == IntPtr.Zero ? "Zero" : "0x" + ptr.ToInt64().ToString("x"))}");
                    if (ptr == IntPtr.Zero)
                    {
                        return null;
                    }

                    string standalonePath = Marshal.PtrToStringAnsi(ptr);
                    Debug.Log($"[NativeFilePicker] 解出路径='{standalonePath ?? "<null>"}'");
                    return NormalizeSelectedPath(standalonePath);
#else
                    Debug.LogWarning(
                        $"[NativeFilePicker] 当前平台不支持原生视频文件选择。平台: {Application.platform}");
                    return null;
#endif
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[NativeFilePicker] 选择视频文件失败。平台: {Application.platform}，异常: {ex}");
                return null;
            }
            finally
            {
                Debug.Log("[NativeFilePicker] PickVideoFile 退出（finally）");
            }
        }

        private static string NormalizeSelectedPath(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return null;
            }

            return selectedPath;
        }

        private sealed class TopmostOverrideScope : IDisposable
        {
            private readonly IDisposable _innerScope;

            public TopmostOverrideScope()
            {
                if (TopmostGuardFactory == null)
                {
                    Debug.Log("[NativeFilePicker][TopmostOverrideScope] enter：TopmostGuardFactory=null，跳过");
                    return;
                }

                try
                {
                    _innerScope = TopmostGuardFactory.Invoke();
                    Debug.Log($"[NativeFilePicker][TopmostOverrideScope] enter：factory 返回 {(_innerScope != null ? "scope" : "<null>")}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NativeFilePicker][TopmostOverrideScope] enter 抛异常：{ex}");
                }
            }

            public void Dispose()
            {
                if (_innerScope == null)
                {
                    Debug.Log("[NativeFilePicker][TopmostOverrideScope] leave：内部 scope 为空");
                    return;
                }

                try
                {
                    _innerScope.Dispose();
                    Debug.Log("[NativeFilePicker][TopmostOverrideScope] leave：scope.Dispose() 完成");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NativeFilePicker][TopmostOverrideScope] leave 抛异常：{ex}");
                }
            }
        }
    }
}
