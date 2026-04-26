using APP.Utility;
using QFramework;
using UnityEngine;

namespace APP.Settings.Model
{
    public sealed class SettingsModel : AbstractModel, ISettingsModel
    {
        public const float MinScale     = 0.5f;
        public const float MaxScale     = 3.0f;
        public const float DefaultScale = 1.0f;

        private const string UiScaleKey = "Settings.UiScale";

        public BindableProperty<float> PreviewUiScale       { get; } = new BindableProperty<float>(DefaultScale);
        public BindableProperty<float> UiScale              { get; } = new BindableProperty<float>(DefaultScale);
        public BindableProperty<int>   PreviewTargetDisplay { get; } = new BindableProperty<int>(0);

        protected override void OnInit()
        {
            var storage = this.GetUtility<IStorageUtility>();
            var loaded  = Mathf.Clamp(
                storage.LoadFloat(UiScaleKey, DefaultScale),
                MinScale, MaxScale);

            UiScale.SetValueWithoutEvent(loaded);
            PreviewUiScale.SetValueWithoutEvent(loaded);

            // 只有正式值自动持久化；预览值不写 PlayerPrefs
            UiScale.Register(v => storage.SaveFloat(UiScaleKey, v));
        }
    }
}
