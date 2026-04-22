using UnityEngine;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 全局帧率限速：空闲 10 FPS，拖拽过程中提升到 60 FPS，拖拽结束恢复为 10 FPS。
    /// 通过引用计数支持多个同时拖拽的元素（例如多个玩家卡片）。
    /// 初始化通过 RuntimeInitializeOnLoadMethod 在场景加载前自动完成。
    /// </summary>
    public static class FrameRateController
    {
        public const int IdleFps = 10;
        public const int ActiveFps = 60;

        private static int _activeDragCount;
        private static bool _initialized;

        /// <summary>当前拖拽引用计数（测试用）。</summary>
        public static int ActiveDragCount => _activeDragCount;

        /// <summary>当前目标帧率（测试用）。</summary>
        public static int CurrentTargetFps => Application.targetFrameRate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            Initialize();
        }

        /// <summary>
        /// 手动初始化（测试场景可调用）。vSync 置 0 才能让 targetFrameRate 生效。
        /// </summary>
        public static void Initialize()
        {
            _activeDragCount = 0;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = IdleFps;
            _initialized = true;
        }

        /// <summary>通知一次拖拽开始；若从空闲切入，立刻提升目标帧率。</summary>
        public static void BeginDrag()
        {
            if (!_initialized)
            {
                Initialize();
            }

            _activeDragCount++;
            if (_activeDragCount == 1)
            {
                Application.targetFrameRate = ActiveFps;
            }
        }

        /// <summary>通知一次拖拽结束；计数归零时回落到空闲帧率。</summary>
        public static void EndDrag()
        {
            if (!_initialized)
            {
                Initialize();
            }

            _activeDragCount = Mathf.Max(0, _activeDragCount - 1);
            if (_activeDragCount == 0)
            {
                Application.targetFrameRate = IdleFps;
            }
        }
    }
}
