using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.System
{
    public sealed class PomodoroTimerSystem : AbstractSystem, IPomodoroTimerSystem
    {
        // 累计秒数（不足 1 秒时暂存）
        private float _accumulator;

        protected override void OnInit()
        {
            _accumulator = 0f;
        }

        public void StartTimer()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();

            // 若已全部完成，先重置再开始
            if (model.CurrentPhase.Value == PomodoroPhase.Completed)
            {
                ResetCycle();
            }

            model.IsRunning.Value = true;
        }

        public void PauseTimer()
        {
            this.GetModel<IPomodoroModel>().IsRunning.Value = false;
        }

        public void ResetCycle()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            _accumulator = 0f;
            model.IsRunning.Value = false;
            model.CurrentRound.Value = 1;
            model.CurrentPhase.Value = PomodoroPhase.Focus;
            model.RemainingSeconds.Value = model.FocusDurationSeconds.Value;

            this.SendEvent(new E_PomodoroPhaseChanged(PomodoroPhase.Focus, 1, model.TotalRounds.Value));
        }

        public void Tick(float deltaTime)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();

            if (!model.IsRunning.Value || model.CurrentPhase.Value == PomodoroPhase.Completed)
            {
                return;
            }

            _accumulator += deltaTime;

            // 每累计满 1 秒，扣减一次剩余时间
            while (_accumulator >= 1f)
            {
                _accumulator -= 1f;
                int remaining = model.RemainingSeconds.Value - 1;

                if (remaining > 0)
                {
                    model.RemainingSeconds.Value = remaining;
                }
                else
                {
                    model.RemainingSeconds.Value = 0;
                    AdvancePhase(model);
                    // 不清零累加器——剩余时间继续驱动新阶段
                    return;
                }
            }
        }

        public void SkipCurrentPhase()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();

            if (!model.IsRunning.Value || model.CurrentPhase.Value == PomodoroPhase.Completed)
            {
                return;
            }

            _accumulator = 0f;
            AdvancePhase(model);
        }

        public void ApplySettings(int focusSeconds, int breakSeconds, int totalRounds, bool resetProgress)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.FocusDurationSeconds.Value = focusSeconds;
            model.BreakDurationSeconds.Value = breakSeconds;
            model.TotalRounds.Value = totalRounds;

            if (resetProgress)
            {
                ResetCycle();
            }
        }

        // ─── 私有辅助 ───────────────────────────────────────

        private void AdvancePhase(IPomodoroModel model)
        {
            if (model.CurrentPhase.Value == PomodoroPhase.Focus)
            {
                // 专注结束 → 进入休息
                model.CurrentPhase.Value = PomodoroPhase.Break;
                model.RemainingSeconds.Value = model.BreakDurationSeconds.Value;
                if (!model.AutoStartBreak.Value)
                {
                    model.IsRunning.Value = false;
                    _accumulator = 0f;
                }
                this.SendEvent(new E_PomodoroPhaseChanged(
                    PomodoroPhase.Break, model.CurrentRound.Value, model.TotalRounds.Value));
            }
            else if (model.CurrentPhase.Value == PomodoroPhase.Break)
            {
                int round = model.CurrentRound.Value;

                if (round < model.TotalRounds.Value)
                {
                    // 休息结束 → 下一轮专注（暂停，等待手动开始）
                    round++;
                    model.CurrentRound.Value = round;
                    model.CurrentPhase.Value = PomodoroPhase.Focus;
                    model.RemainingSeconds.Value = model.FocusDurationSeconds.Value;
                    model.IsRunning.Value = false;
                    _accumulator = 0f;
                    this.SendEvent(new E_PomodoroPhaseChanged(
                        PomodoroPhase.Focus, round, model.TotalRounds.Value));
                }
                else
                {
                    // 最后一轮休息结束 → 全部完成
                    model.IsRunning.Value = false;
                    model.CurrentPhase.Value = PomodoroPhase.Completed;
                    model.RemainingSeconds.Value = 0;
                    this.SendEvent(new E_PomodoroPhaseChanged(
                        PomodoroPhase.Completed, round, model.TotalRounds.Value));
                    this.SendEvent(new E_PomodoroCycleCompleted(round));
                }
            }
        }
    }
}
