using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>用户点击某 row 的 listener → 进入监听设置态。</summary>
    public sealed class Cmd_BeginBindingCapture : AbstractCommand
    {
        private readonly string _entryId;
        public Cmd_BeginBindingCapture(string entryId) => _entryId = entryId ?? string.Empty;

        protected override void OnExecute()
        {
            this.GetModel<IBindingKeyModel>().ListeningKeyId.Value = _entryId;
        }
    }
}
