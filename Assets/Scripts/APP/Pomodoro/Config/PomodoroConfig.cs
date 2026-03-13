using System.Collections.Generic;
using APP.Pomodoro.Model;
using UnityEngine;

namespace APP.Pomodoro.Config
{
    /// <summary>
    /// 番茄钟全局配置表 - 可在 Assets/Settings/ 目录下创建并赋值
    /// 菜单：Assets → Create → APP/Pomodoro/配置表
    /// </summary>
    [CreateAssetMenu(menuName = "APP/Pomodoro/配置表", fileName = "PomodoroConfig")]
    public sealed class PomodoroConfig : ScriptableObject
    {
        [Header("默认计时参数")]
        [Tooltip("默认专注时长（分钟）")]
        [Min(1)]
        public int DefaultFocusMinutes = 25;

        [Tooltip("默认休息时长（分钟）")]
        [Min(1)]
        public int DefaultBreakMinutes = 5;

        [Tooltip("默认轮次数")]
        [Min(1)]
        public int DefaultRounds = 4;

        [Header("窗口位置")]
        [Tooltip("初始屏幕锚点（顶端/底端）")]
        public PomodoroWindowAnchor DefaultWindowAnchor = PomodoroWindowAnchor.Bottom;

        [Tooltip("窗口高度（像素），用于计算底端 Y 坐标")]
        [Min(1f)]
        public float FixedWindowHeight = 120f;

        [Tooltip("屏幕边缘留白（像素）")]
        [Min(0f)]
        public float VerticalMargin = 0f;

        [Header("完成提示")]
        [Tooltip("在底端计时完成后，是否默认自动跳到顶端提示")]
        public bool DefaultAutoJumpToTopOnComplete = true;

        [Header("完成音效")]
        [Tooltip("默认使用的音效索引（对应 CompletionClips 列表）")]
        [Min(0)]
        public int DefaultCompletionClipIndex = 0;

        [Tooltip("完成音效音量")]
        [Range(0f, 1f)]
        public float CompletionVolume = 1f;

        [Tooltip("可选完成音效列表；第 0 个为默认，留空则静音")]
        public List<AudioClip> CompletionClips = new List<AudioClip>();

        /// <summary>
        /// 安全获取指定索引的 AudioClip，超出范围返回 null
        /// </summary>
        public AudioClip GetCompletionClip(int index)
        {
            if (CompletionClips == null || CompletionClips.Count == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Clamp(index, 0, CompletionClips.Count - 1);
            return CompletionClips[safeIndex];
        }
    }
}
