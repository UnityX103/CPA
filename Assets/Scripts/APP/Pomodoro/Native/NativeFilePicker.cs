using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace APP.Pomodoro.Native
{
    public static class NativeFilePicker
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [DllImport("__Internal")]
        private static extern IntPtr cpa_native_pick_video_file();
#endif

        /// <summary>
        /// 阻塞式选择本地视频文件；用户取消、失败或当前平台不支持时返回 null。
        /// </summary>
        public static string PickVideoFile()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
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
                Debug.LogWarning($"[NativeFilePicker] 选择视频文件失败: {ex.Message}");
                return null;
            }
#else
            Debug.LogWarning("[NativeFilePicker] 当前平台不支持原生视频文件选择。");
            return null;
#endif
        }
    }
}
