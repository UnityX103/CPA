using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>
    /// 切换按键计数面板（InputCounterPanel）的 pin 状态。
    /// 写入 IBindingKeyModel.PanelPinned；该值会被 WindowVisibilityCoordinatorSystem
    /// 聚合进 AnyPinned，驱动窗口置顶。
    /// </summary>
    public sealed class Cmd_SetInputCounterPanelPinned : AbstractCommand
    {
        private readonly bool _pinned;
        public Cmd_SetInputCounterPanelPinned(bool pinned) => _pinned = pinned;

        protected override void OnExecute() =>
            this.GetModel<IBindingKeyModel>().PanelPinned.Value = _pinned;
    }
}
