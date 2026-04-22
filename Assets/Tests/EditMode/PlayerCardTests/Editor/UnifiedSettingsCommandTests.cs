using APP.Pomodoro;
using APP.Pomodoro.Command;
using APP.Pomodoro.Event;
using NUnit.Framework;
using QFramework;

namespace APP.Pomodoro.Tests
{
    public sealed class UnifiedSettingsCommandTests
    {
        [Test]
        public void Cmd_OpenUnifiedSettings_FiresEvent()
        {
            bool fired = false;
            var handle = GameApp.Interface.RegisterEvent<E_OpenUnifiedSettings>(_ => fired = true);
            try
            {
                GameApp.Interface.SendCommand(new Cmd_OpenUnifiedSettings());
                Assert.That(fired, Is.True);
            }
            finally { handle.UnRegister(); }
        }

        [Test]
        public void Cmd_CloseUnifiedSettings_FiresEvent()
        {
            bool fired = false;
            var handle = GameApp.Interface.RegisterEvent<E_CloseUnifiedSettings>(_ => fired = true);
            try
            {
                GameApp.Interface.SendCommand(new Cmd_CloseUnifiedSettings());
                Assert.That(fired, Is.True);
            }
            finally { handle.UnRegister(); }
        }
    }
}
