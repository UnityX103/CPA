using System.IO;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.System
{
    public sealed class PomodoroEndActionSystem : AbstractSystem, IPomodoroEndActionSystem
    {
        protected override void OnInit()
        {
            this.RegisterEvent<E_PomodoroPhaseAutoAdvanced>(OnPhaseAutoAdvanced);
            this.RegisterEvent<E_PomodoroCycleCompleted>(OnCycleCompleted);
        }

        private void OnPhaseAutoAdvanced(E_PomodoroPhaseAutoAdvanced evt)
        {
            // 仅 Focus → Break 这种"段落真实结束"的瞬间触发；
            // Break → Focus 的轮次衔接同样视作段落结束，需要走结束提示。
            // 进入 Completed 状态由 OnCycleCompleted 兜底。
            if (evt.ToPhase != PomodoroPhase.Break && evt.ToPhase != PomodoroPhase.Focus)
            {
                return;
            }
            DispatchByMode();
        }

        private void OnCycleCompleted(E_PomodoroCycleCompleted _)
        {
            DispatchByMode();
        }

        private void DispatchByMode()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            if (model.EndActionMode.Value != PomodoroEndActionMode.PlayVideo)
            {
                // TopWindow 模式由 PhaseTransitionFlashSystem 自行处理，无需额外动作
                return;
            }

            string path = model.EndActionVideoPath.Value;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Debug.LogWarning(
                    $"[PomodoroEndActionSystem] PlayVideo 模式但视频路径无效，回退到默认提示：path='{path}'");
                return;
            }

            this.SendEvent(new E_RequestPlayCompletionVideo(path));
        }
    }
}
