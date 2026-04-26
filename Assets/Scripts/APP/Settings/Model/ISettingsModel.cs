using QFramework;

namespace APP.Settings.Model
{
    /// <summary>
    /// 全局设置 Model。
    /// UiScale 为已保留/持久化的正式值；PreviewUiScale 为当前正在生效/预览的值，
    /// 订阅者（PanelScaleApplier）据此写 PanelSettings.scale。
    /// 仅 UiScale 会被持久化到 PlayerPrefs。
    /// </summary>
    public interface ISettingsModel : IModel
    {
        /// <summary>当前正在生效/预览中的缩放倍率。不持久化。</summary>
        BindableProperty<float> PreviewUiScale { get; }

        /// <summary>已保留/持久化的缩放倍率。自动写入 PlayerPrefs。</summary>
        BindableProperty<float> UiScale { get; }

        /// <summary>
        /// 当前正在生效/预览中的目标显示器索引。不持久化；
        /// 委托物理移动通过 IWindowPositionSystem.PreviewMoveToMonitor。
        /// 已提交/持久化的索引复用 IPomodoroModel.TargetMonitorIndex。
        /// </summary>
        BindableProperty<int> PreviewTargetDisplay { get; }
    }
}
