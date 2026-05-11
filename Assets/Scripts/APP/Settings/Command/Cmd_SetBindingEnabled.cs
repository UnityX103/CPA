using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>开/关按键计数功能。开关变化自动持久化。</summary>
    public sealed class Cmd_SetBindingEnabled : AbstractCommand
    {
        private readonly bool _enabled;
        public Cmd_SetBindingEnabled(bool enabled) => _enabled = enabled;

        protected override void OnExecute()
        {
            this.GetModel<IBindingKeyModel>().Enabled.Value = _enabled;
        }
    }
}
