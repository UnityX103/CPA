using System;
using APP.Pomodoro;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Native;
using APP.Pomodoro.System;
using QFramework;
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
            Debug.Log($"[PomodoroVideoPickerBootstrap] Bind 调起。当前 VideoFilePicker={(PomodoroSettingsPanelController.VideoFilePicker != null ? "non-null" : "null")}, TopmostGuardFactory={(NativeFilePicker.TopmostGuardFactory != null ? "non-null" : "null")}");

            // 已有注入（例如测试覆盖）就不动；保持可被替换
            if (PomodoroSettingsPanelController.VideoFilePicker == null)
            {
                PomodoroSettingsPanelController.VideoFilePicker = NativeFilePicker.PickVideoFile;
                Debug.Log("[PomodoroVideoPickerBootstrap] 已绑定 VideoFilePicker = NativeFilePicker.PickVideoFile");
            }

            if (NativeFilePicker.TopmostGuardFactory == null)
            {
                NativeFilePicker.TopmostGuardFactory = CreateTopmostGuardScope;
                Debug.Log("[PomodoroVideoPickerBootstrap] 已绑定 TopmostGuardFactory = CreateTopmostGuardScope");
            }
        }

        private static IDisposable CreateTopmostGuardScope()
        {
            Debug.Log("[PomodoroVideoPickerBootstrap] CreateTopmostGuardScope 进入");

            try
            {
                IWindowPositionSystem windowPositionSystem = GameApp.Interface?.GetSystem<IWindowPositionSystem>();
                if (windowPositionSystem == null)
                {
                    Debug.LogWarning("[PomodoroVideoPickerBootstrap] IWindowPositionSystem == null，无法降置顶。返回 null guard");
                    return null;
                }

                bool originalTopmost = windowPositionSystem.IsTopmost;
                Debug.Log($"[PomodoroVideoPickerBootstrap] 创建 scope，originalTopmost={originalTopmost}");
                return new TopmostOverrideScope(windowPositionSystem, originalTopmost);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PomodoroVideoPickerBootstrap] 创建窗口置顶守护失败：{ex}");
                return null;
            }
        }

        private sealed class TopmostOverrideScope : IDisposable
        {
            private readonly bool _originalTopmost;
            private readonly IWindowPositionSystem _windowPositionSystem;
            private bool _disposed;

            public TopmostOverrideScope(IWindowPositionSystem windowPositionSystem, bool originalTopmost)
            {
                _windowPositionSystem = windowPositionSystem;
                _originalTopmost = originalTopmost;
                Debug.Log($"[PomodoroVideoPickerBootstrap.TopmostOverrideScope] enter：调 SetTopmost(false)（原值={originalTopmost}）");
                _windowPositionSystem.SetTopmost(false);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    Debug.Log("[PomodoroVideoPickerBootstrap.TopmostOverrideScope] Dispose：已处理过，跳过");
                    return;
                }

                _disposed = true;
                Debug.Log($"[PomodoroVideoPickerBootstrap.TopmostOverrideScope] Dispose：恢复 SetTopmost({_originalTopmost})");
                _windowPositionSystem.SetTopmost(_originalTopmost);
            }
        }
    }
}
