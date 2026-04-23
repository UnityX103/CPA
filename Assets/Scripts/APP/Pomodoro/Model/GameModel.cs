using QFramework;

namespace APP.Pomodoro.Model
{
    public sealed class GameModel : AbstractModel, IGameModel
    {
        public BindableProperty<bool> IsAppFocused { get; } = new BindableProperty<bool>(true);

        protected override void OnInit() { }
    }
}
