using System.IO;
using APP.Pomodoro.Config;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.System
{
    public sealed class PomodoroEndActionSystem : AbstractSystem, IPomodoroEndActionSystem
    {
        private PomodoroBuiltInVideosConfig _builtInConfig;

        protected override void OnInit()
        {
            this.RegisterEvent<E_PomodoroPhaseAutoAdvanced>(OnPhaseAutoAdvanced);
        }

        private void OnPhaseAutoAdvanced(E_PomodoroPhaseAutoAdvanced evt)
        {
            // 只在"专注阶段结束"的瞬间触发视频：Focus → Break。
            // Break → Focus（休息结束、进入下一轮专注）和 Break → Completed（最后一轮休息结束）
            // 都不放视频，由 PhaseTransitionFlashSystem 把面板置顶 flash 即可。
            if (evt.FromPhase != PomodoroPhase.Focus || evt.ToPhase != PomodoroPhase.Break)
            {
                return;
            }
            DispatchByMode();
        }

        private void DispatchByMode()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            PomodoroEndActionMode mode = model.EndActionMode.Value;

            switch (mode)
            {
                case PomodoroEndActionMode.TopWindow:
                    // TopWindow 模式由 PhaseTransitionFlashSystem 自行处理，无需额外动作
                    return;

                case PomodoroEndActionMode.PlayVideo:
                    int idx = model.EndActionVideoIndex.Value;
                    if (idx == -1)
                    {
                        DispatchCustomFile(model.EndActionVideoPath.Value);
                    }
                    else
                    {
                        DispatchBuiltIn(idx);
                    }
                    return;
            }
        }

        private void DispatchCustomFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning(
                    $"[PomodoroEndActionSystem] PlayVideo 自定义模式但视频路径无效，回退到默认提示：path='{path}'");
                return;
            }

            this.SendEvent(new E_RequestPlayCompletionVideo(path));
        }

        private void DispatchBuiltIn(int index)
        {
            if (index < 0)
            {
                Debug.LogWarning(
                    $"[PomodoroEndActionSystem] DispatchBuiltIn 收到非法索引 {index}，回退到默认提示。");
                return;
            }

            PomodoroBuiltInVideosConfig config = LoadConfigLazy();
            if (config == null || config.Entries.Count == 0)
            {
                Debug.LogWarning(
                    "[PomodoroEndActionSystem] 内置视频配置缺失或为空，回退到默认提示。");
                return;
            }

            string path = config.ResolveRuntimePath(index);
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning(
                    $"[PomodoroEndActionSystem] 内置视频 index={index} 解析路径为空，回退到默认提示。");
                return;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    $"[PomodoroEndActionSystem] 内置视频文件不存在：'{path}'，回退到默认提示。");
                return;
            }

            this.SendEvent(new E_RequestPlayCompletionVideo(path));
        }

        private PomodoroBuiltInVideosConfig LoadConfigLazy()
        {
            if (_builtInConfig == null)
            {
                _builtInConfig = PomodoroBuiltInVideosConfig.LoadFromResources();
            }
            return _builtInConfig;
        }
    }
}
