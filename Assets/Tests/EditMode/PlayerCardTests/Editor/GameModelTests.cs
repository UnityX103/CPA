using APP.Pomodoro.Model;
using NUnit.Framework;

namespace APP.Tests.PlayerCardTests
{
    public sealed class GameModelTests
    {
        [Test]
        public void IsAppFocused_DefaultTrue()
        {
            IGameModel model = new GameModel();
            Assert.IsTrue(model.IsAppFocused.Value);
        }

        [Test]
        public void IsAppFocused_WriteTriggersSubscriber()
        {
            IGameModel model = new GameModel();
            bool? received = null;
            model.IsAppFocused.Register(v => received = v);

            model.IsAppFocused.Value = false;

            Assert.IsTrue(received.HasValue);
            Assert.IsFalse(received.Value);
        }
    }
}
