using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>把指定 entry 的累计计数归零。</summary>
    public sealed class Cmd_ResetBindingCount : AbstractCommand
    {
        private readonly string _entryId;
        public Cmd_ResetBindingCount(string entryId) => _entryId = entryId ?? string.Empty;

        protected override void OnExecute()
        {
            this.GetModel<IBindingKeyModel>().ResetEntryCount(_entryId);
        }
    }
}
