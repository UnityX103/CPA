using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>切换某 entry 是否激活；激活后 CounterSystem 监听其 BoundKey，InputCounterPanel 弹出。</summary>
    public sealed class Cmd_SetBindingEntryEnabled : AbstractCommand
    {
        private readonly string _entryId;
        private readonly bool _enabled;
        public Cmd_SetBindingEntryEnabled(string entryId, bool enabled)
        {
            _entryId = entryId ?? string.Empty;
            _enabled = enabled;
        }

        protected override void OnExecute()
        {
            this.GetModel<IBindingKeyModel>().SetEntryEnabled(_entryId, _enabled);
        }
    }
}
