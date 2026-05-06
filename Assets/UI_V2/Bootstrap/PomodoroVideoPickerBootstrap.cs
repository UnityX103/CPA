using APP.Pomodoro.Controller;
using APP.Pomodoro.Native;
using UnityEngine;

namespace APP.Pomodoro.Bootstrap
{
    /// <summary>
    /// 在 Editor / Standalone 启动时把 NativeFilePicker.PickVideoFile 注入到
    /// PomodoroSettingsPanelController.VideoFilePicker，避免 Controller 直接
    /// 依赖 Native 桥，方便测试用替身覆盖。
    /// </summary>
    public static class PomodoroVideoPickerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bind()
        {
            // 已有注入（例如测试覆盖）就不动；保持可被替换
            if (PomodoroSettingsPanelController.VideoFilePicker == null)
            {
                PomodoroSettingsPanelController.VideoFilePicker = NativeFilePicker.PickVideoFile;
            }
        }
    }
}
