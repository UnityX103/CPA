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
#if UNITY_STANDALONE_OSX
        [DllImport("__Internal")]
        private static extern IntPtr cpa_native_pick_video_file();
#endif

        /// <summary>
        /// 阻塞式选择本地视频文件；用户取消、失败或当前平台不支持时返回 null。
        /// </summary>
        public static string PickVideoFile()
        {
#if UNITY_EDITOR_OSX
            try
            {
                string selectedPath = EditorUtility.OpenFilePanelWithFilters(
                    "选择计时结束播放的视频",
                    string.Empty,
                    new[]
                    {
                        "视频文件",
                        "mp4,mov,m4v,webm"
                    });

                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    return null;
                }

                return selectedPath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[NativeFilePicker] macOS Editor 选择视频文件失败。平台: {Application.platform}，异常: {ex}");
                return null;
            }
#elif UNITY_STANDALONE_OSX
            try
            {
                IntPtr ptr = cpa_native_pick_video_file();
                if (ptr == IntPtr.Zero)
                {
                    return null;
                }

                return Marshal.PtrToStringAnsi(ptr);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[NativeFilePicker] macOS Standalone 选择视频文件失败。平台: {Application.platform}，异常: {ex}");
                return null;
            }
#else
            Debug.LogWarning(
                $"[NativeFilePicker] 当前平台不支持原生视频文件选择。平台: {Application.platform}");
            return null;
#endif
        }
    }
}
