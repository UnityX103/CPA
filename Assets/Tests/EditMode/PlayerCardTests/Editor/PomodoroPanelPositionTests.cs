using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Tests
{
    public sealed class PomodoroPanelPositionTests
    {
        [Test]
        public void DefaultValue_IsNegativeInfinity_ActingAsSentinel()
        {
            var model = new PomodoroModel();
            ((IModel)model).Init();

            Vector2 pos = model.PomodoroPanelPosition.Value;
            Assert.That(float.IsNegativeInfinity(pos.x), "x 应为 -Infinity");
            Assert.That(float.IsNegativeInfinity(pos.y), "y 应为 -Infinity");
        }

        [Test]
        public void SetValue_PersistsInMemory()
        {
            var model = new PomodoroModel();
            ((IModel)model).Init();

            var target = new Vector2(100f, 200f);
            model.PomodoroPanelPosition.Value = target;

            Assert.That(model.PomodoroPanelPosition.Value, Is.EqualTo(target));
        }
    }
}
