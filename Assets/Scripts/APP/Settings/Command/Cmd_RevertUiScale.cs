using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>
    /// 把预览值刷回正式值（PanelScaleApplier 把 PanelSettings.scale 回滚）。
    /// </summary>
    public sealed class Cmd_RevertUiScale : AbstractCommand
    {
        protected override void OnExecute()
        {
            var m = this.GetModel<ISettingsModel>();
            m.PreviewUiScale.Value = m.UiScale.Value;
        }
    }
}
