using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>
    /// 把当前预览值提交为正式值（触发 UiScale.Register 持久化）。
    /// </summary>
    public sealed class Cmd_CommitUiScale : AbstractCommand
    {
        protected override void OnExecute()
        {
            var m = this.GetModel<ISettingsModel>();
            m.UiScale.Value = m.PreviewUiScale.Value;
        }
    }
}
