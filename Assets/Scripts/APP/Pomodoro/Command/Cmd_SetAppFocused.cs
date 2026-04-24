using APP.Pomodoro.Model;
using QFramework;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 写入 IGameModel.IsAppFocused。
    /// 由 DeskWindowController.OnApplicationFocus 调用，
    /// 将 Unity 的应用焦点状态统一汇入 QFramework Model。
    /// </summary>
    public sealed class Cmd_SetAppFocused : AbstractCommand
    {
        private readonly bool _isFocused;

        public Cmd_SetAppFocused(bool isFocused) => _isFocused = isFocused;

        protected override void OnExecute() =>
            this.GetModel<IGameModel>().IsAppFocused.Value = _isFocused;
    }
}
