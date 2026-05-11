using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>Esc / 失焦 → 取消监听设置态，保留旧键。</summary>
    public sealed class Cmd_CancelBindingCapture : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetModel<IBindingKeyModel>().ListeningKeyId.Value = string.Empty;
        }
    }
}
