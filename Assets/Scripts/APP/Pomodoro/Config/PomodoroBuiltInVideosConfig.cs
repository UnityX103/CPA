using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace APP.Pomodoro.Config
{
    /// <summary>
    /// 番茄钟内置视频配置表。运行时从 <c>Resources/PomodoroBuiltInVideos.asset</c> 加载。
    /// 设计稿 XKfRo 下拉框 "已选择哪条内置视频" 的数据源；同时被 PomodoroEndActionSystem
    /// 用来在 EndActionMode = PlayVideo 且 EndActionVideoIndex >= 0 时解析视频路径。
    ///
    /// 路径策略（参考 task 描述里的"VideoClip → runtime path 这一步是关键技术点"）：
    ///   - <see cref="Entry.Path"/> 优先：相对路径，运行时会拼到 <c>Application.streamingAssetsPath</c>。
    ///   - <see cref="Entry.Clip"/>：仅 Editor inspector 用作可选预览/拖拽锚点；
    ///     build 后取不到磁盘路径，所以**不要**依赖它做运行时播放。
    ///   - <see cref="Entry.FallbackResourcePath"/>：未来若想直接 <c>Resources.Load&lt;VideoClip&gt;</c>
    ///     的兜底入口（当前 PomodoroEndActionSystem 只读 Path / StreamingAssets）。
    /// </summary>
    [CreateAssetMenu(
        fileName = "PomodoroBuiltInVideos",
        menuName = "CPA/Pomodoro Built-In Videos")]
    public sealed class PomodoroBuiltInVideosConfig : ScriptableObject
    {
        private const string ResourceName = "PomodoroBuiltInVideos";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("下拉框显示名（用户可见）")]
            public string DisplayName = "示例视频";

            [Tooltip("Editor 拖拽用的视频资源；build 后无法直接拿磁盘路径，因此运行时只用 Path 字段。")]
            public VideoClip Clip;

            [Tooltip("StreamingAssets 内的相对路径（推荐）。运行时拼到 Application.streamingAssetsPath。")]
            public string Path = string.Empty;

            [Tooltip("可选：Resources/ 下的相对路径（不带扩展名），作为兜底加载入口。")]
            public string FallbackResourcePath = string.Empty;
        }

        [SerializeField]
        private List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries =>
            _entries ?? (_entries = new List<Entry>());

        /// <summary>
        /// 从 Resources 加载配置实例；找不到时返回 null（调用方需做空判断）。
        /// 加载结果会被 Unity Resources 子系统缓存，因此重复调用是廉价的。
        /// </summary>
        public static PomodoroBuiltInVideosConfig LoadFromResources()
        {
            PomodoroBuiltInVideosConfig config = Resources.Load<PomodoroBuiltInVideosConfig>(ResourceName);
            if (config == null)
            {
                Debug.LogWarning(
                    $"[PomodoroBuiltInVideosConfig] 未找到 Resources/{ResourceName}.asset，" +
                    "内置视频列表将为空。");
            }
            return config;
        }

        /// <summary>
        /// 取指定下标 entry 的运行时视频路径。
        /// 解析顺序：
        ///   1) <see cref="Entry.Path"/> 非空 → 拼 <c>Application.streamingAssetsPath</c>（推荐，build 后可用）；
        ///   2) Editor 下若 <see cref="Entry.Clip"/> 有引用 → 通过 AssetDatabase 反推该 VideoClip
        ///      在工程内的磁盘路径，转成绝对路径。与自定义视频走同一条 file:// 绝对路径链路，
        ///      便于开发期未配置 StreamingAssets 也能直接播。注意：build 后 AssetDatabase 不可用，
        ///      所以发版前仍须把视频放到 StreamingAssets 并填 Path。
        ///   3) 都不可用 → 返回空串。
        /// </summary>
        public string ResolveRuntimePath(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Count)
            {
                return string.Empty;
            }

            Entry entry = _entries[index];
            if (entry == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(entry.Path))
            {
                return Path.Combine(Application.streamingAssetsPath, entry.Path);
            }

#if UNITY_EDITOR
            if (entry.Clip != null)
            {
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(entry.Clip);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // assetPath 形如 "Assets/Resources/ms1.webm"；Application.dataPath 指向 .../CPA/Assets，
                    // 父目录即工程根，拼上 assetPath 得到磁盘绝对路径。
                    string projectRoot = Path.GetDirectoryName(Application.dataPath);
                    if (!string.IsNullOrEmpty(projectRoot))
                    {
                        return Path.Combine(projectRoot, assetPath);
                    }
                }
            }
#endif

            return string.Empty;
        }
    }
}
