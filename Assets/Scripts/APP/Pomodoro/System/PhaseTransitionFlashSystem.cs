using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.System
{
    public sealed class PhaseTransitionFlashSystem : AbstractSystem, IPhaseTransitionFlashSystem
    {
        private readonly BindableProperty<bool> _isFlashing = new BindableProperty<bool>(false);

        public IReadonlyBindableProperty<bool> IsFlashing => _isFlashing;

        protected override void OnInit()
        {
            this.RegisterEvent<E_PomodoroPhaseAutoAdvanced>(OnPhaseAutoAdvanced);
        }

        public void Dismiss()
        {
            if (_isFlashing.Value)
            {
                _isFlashing.Value = false;
            }
        }

        private void OnPhaseAutoAdvanced(E_PomodoroPhaseAutoAdvanced evt)
        {
            // 仅对 Focus↔Break 的自然切换触发；Completed 不触发。
            if (evt.ToPhase != PomodoroPhase.Focus && evt.ToPhase != PomodoroPhase.Break)
            {
                return;
            }

            // 番茄钟本身已 pin：面板已常驻可见，无需 Flash。
            if (this.GetModel<IPomodoroModel>().IsPinned.Value)
            {
                return;
            }

            _isFlashing.Value = true;
        }
    }
}
