using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;

namespace APP.Pomodoro.Tests
{
    public sealed class PlayerCardViewTests
    {
        [Test]
        public void FormatTime_PadsMinutesAndSeconds()
        {
            Assert.AreEqual("00:00", PlayerCardView.FormatTime(0));
            Assert.AreEqual("00:09", PlayerCardView.FormatTime(9));
            Assert.AreEqual("01:00", PlayerCardView.FormatTime(60));
            Assert.AreEqual("25:00", PlayerCardView.FormatTime(1500));
            Assert.AreEqual("99:59", PlayerCardView.FormatTime(99 * 60 + 59));
        }

        [Test]
        public void FormatTime_ClampsNegativeValuesToZero()
        {
            Assert.AreEqual("00:00", PlayerCardView.FormatTime(-5));
            Assert.AreEqual("00:00", PlayerCardView.FormatTime(int.MinValue));
        }

        [Test]
        public void FormatPhase_MapsFocusBreakCompletedCorrectly()
        {
            Assert.AreEqual("专注中", PlayerCardView.FormatPhase(PomodoroPhase.Focus, true));
            Assert.AreEqual("专注暂停", PlayerCardView.FormatPhase(PomodoroPhase.Focus, false));
            Assert.AreEqual("休息中", PlayerCardView.FormatPhase(PomodoroPhase.Break, true));
            Assert.AreEqual("休息暂停", PlayerCardView.FormatPhase(PomodoroPhase.Break, false));
            Assert.AreEqual("已完成", PlayerCardView.FormatPhase(PomodoroPhase.Completed, true));
            Assert.AreEqual("已完成", PlayerCardView.FormatPhase(PomodoroPhase.Completed, false));
        }

        [Test]
        public void GetPhaseClass_ReturnsFocusWhenRunning()
        {
            Assert.AreEqual("pc-phase-focus", PlayerCardView.GetPhaseClass(PomodoroPhase.Focus, true));
            Assert.AreEqual("pc-phase-rest", PlayerCardView.GetPhaseClass(PomodoroPhase.Break, true));
        }

        [Test]
        public void GetPhaseClass_ReturnsPausedWhenNotRunningExceptCompleted()
        {
            Assert.AreEqual("pc-phase-paused", PlayerCardView.GetPhaseClass(PomodoroPhase.Focus, false));
            Assert.AreEqual("pc-phase-paused", PlayerCardView.GetPhaseClass(PomodoroPhase.Break, false));
            Assert.AreEqual("pc-phase-completed", PlayerCardView.GetPhaseClass(PomodoroPhase.Completed, false));
        }
    }
}
