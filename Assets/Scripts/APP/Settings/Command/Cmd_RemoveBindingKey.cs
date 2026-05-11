using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>删除指定 entry；若是 SyncedKeyId 也会自动清空。</summary>
    public sealed class Cmd_RemoveBindingKey : AbstractCommand
    {
        private readonly string _entryId;
        public Cmd_RemoveBindingKey(string entryId) => _entryId = entryId ?? string.Empty;

        protected override void OnExecute()
        {
            this.GetModel<IBindingKeyModel>().RemoveEntry(_entryId);
        }
    }
}
