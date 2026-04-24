using APP.Settings.Model;
using QFramework;
using UnityEngine;

namespace APP.Settings.Command
{
    /// <summary>
    /// 写入 PreviewUiScale（驱动 PanelScaleApplier 应用到 PanelSettings.scale）。
    /// 对非法值（NaN/Infinity）做保护——直接丢弃；合法值 Clamp 到 [MinScale, MaxScale]。
    /// </summary>
    public sealed class Cmd_SetPreviewUiScale : AbstractCommand
    {
        private readonly float _scale;
        public Cmd_SetPreviewUiScale(float scale) => _scale = scale;

        protected override void OnExecute()
        {
            if (!float.IsFinite(_scale)) return;
            this.GetModel<ISettingsModel>().PreviewUiScale.Value =
                Mathf.Clamp(_scale, SettingsModel.MinScale, SettingsModel.MaxScale);
        }
    }
}
