using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>切换"同步到远端"的 entry。传 ""=取消所有同步；同一 id 再次调用=切换为不同步。</summary>
    public sealed class Cmd_SetSyncedBindingKey : AbstractCommand
    {
        private readonly string _entryId;
        public Cmd_SetSyncedBindingKey(string entryId) => _entryId = entryId ?? string.Empty;

        protected override void OnExecute()
        {
            var m = this.GetModel<IBindingKeyModel>();
            // 同 id 再点 → 取消；不同 id → 切换
            m.SyncedKeyId.Value = (m.SyncedKeyId.Value == _entryId) ? string.Empty : _entryId;
        }
    }
}
