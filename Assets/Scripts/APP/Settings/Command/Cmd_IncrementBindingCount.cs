using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>System 每帧检到 BoundKey 按下时派发，对应 entry 的 PressCount++。</summary>
    public sealed class Cmd_IncrementBindingCount : AbstractCommand
    {
        private readonly string _entryId;
        public Cmd_IncrementBindingCount(string entryId) => _entryId = entryId ?? string.Empty;

        protected override void OnExecute()
        {
            this.GetModel<IBindingKeyModel>().IncrementEntry(_entryId);
        }
    }
}
