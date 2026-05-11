using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>捕到首个输入 → 把键写入对应 entry，并退出监听态。</summary>
    public sealed class Cmd_CompleteBindingCapture : AbstractCommand
    {
        private readonly string _entryId;
        private readonly int    _keyCode;
        private readonly string _label;

        public Cmd_CompleteBindingCapture(string entryId, int keyCode, string label)
        {
            _entryId = entryId ?? string.Empty;
            _keyCode = keyCode;
            _label   = label ?? string.Empty;
        }

        protected override void OnExecute()
        {
            var m = this.GetModel<IBindingKeyModel>();
            m.TryUpdateEntryKey(_entryId, _keyCode, _label);
            m.ListeningKeyId.Value = string.Empty;
        }
    }
}
