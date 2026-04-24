using APP.Pomodoro;
using APP.Settings.Command;
using APP.Settings.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 全局设置面板控制器（纯 C# 类）。
    /// 滑块拖动只更新本地 pending，不影响 Model；点击"应用"发 Cmd_SetPreviewUiScale
    /// 让 PanelScaleApplier 写 PanelSettings.scale，同步弹出倒计时对话框。
    /// 用户点"保留" → Cmd_CommitUiScale；点"还原"或 5s 超时 → Cmd_RevertUiScale。
    /// </summary>
    public sealed class GlobalSettingsPanelController : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        public const float CountdownSeconds = 5f;

        private Slider _slider;
        private VisualElement _progressFill;
        private Label _valueLabel;
        private Button _applyBtn;
        private ConfirmDialogController _scaleDialog;

        private float _pendingScale;

        public bool IsScaleDialogVisible => _scaleDialog?.IsVisible == true;

        public void Init(
            VisualElement root,
            VisualElement dialogHost,
            VisualTreeAsset confirmDialogTemplate,
            GameObject lifecycleOwner)
        {
            var model = this.GetModel<ISettingsModel>();

            _slider     = root.Q<Slider>("gsp-scale-slider");
            _progressFill = root.Q<VisualElement>("gsp-scale-slider-fill");
            _valueLabel = root.Q<Label>("gsp-scale-value");
            _applyBtn   = root.Q<Button>("apply-btn");

            if (_slider != null)
            {
                _slider.lowValue  = SettingsModel.MinScale;
                _slider.highValue = SettingsModel.MaxScale;
            }

            _scaleDialog = new ConfirmDialogController();
            if (dialogHost != null && confirmDialogTemplate != null)
            {
                _scaleDialog.Init(dialogHost, confirmDialogTemplate);
            }

            SyncSliderFromModel(model.UiScale.Value);

            _slider?.RegisterValueChangedCallback(OnSliderChanged);
            if (_applyBtn != null) _applyBtn.clicked += OnApplyClicked;

            model.UiScale.Register(SyncSliderFromModel)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
        }

        /// <summary>进入 global tab 时调用，把滑块刷回 UiScale（丢弃上次未应用的拖动残留）。</summary>
        public void RefreshFromModel()
            => SyncSliderFromModel(this.GetModel<ISettingsModel>().UiScale.Value);

        /// <summary>测试钩子（internal）：直接触发"应用"逻辑，绕过 UI 事件。</summary>
        internal void TriggerApplyForTest() => OnApplyClicked();

        /// <summary>
        /// 测试钩子（internal）：直接触发 slider 变化逻辑，绕过 ChangeEvent。
        /// EditMode 下 VisualElement 无 panel 时 Slider.value 的 setter 不会派发 ChangeEvent，
        /// 故测试通过此钩子等价模拟用户拖动。
        /// </summary>
        internal void TriggerSliderChangeForTest(float newValue)
        {
            _pendingScale = SnapToStep(newValue);
            if (_valueLabel != null) _valueLabel.text = FormatScale(_pendingScale);
            _slider?.SetValueWithoutNotify(_pendingScale);
            RefreshProgressFill(_pendingScale);
        }

        // ─── 内部 ────────────────────────────────────────────────

        private void OnSliderChanged(ChangeEvent<float> evt)
        {
            _pendingScale    = SnapToStep(evt.newValue);
            if (_valueLabel != null) _valueLabel.text = FormatScale(_pendingScale);
            RefreshProgressFill(_pendingScale);
        }

        private void OnApplyClicked()
        {
            var model   = this.GetModel<ISettingsModel>();
            var current = model.UiScale.Value;
            var target  = SnapToStep(_pendingScale);

            if (Mathf.Approximately(current, target)) return;
            if (_scaleDialog.IsVisible) return;

            this.SendCommand(new Cmd_SetPreviewUiScale(target));

            _scaleDialog.Show(
                title:       "保留新缩放吗？",
                subtitle:    $"当前 {FormatScale(target)}，原 {FormatScale(current)}",
                body:        "如 5 秒内未保留，将自动还原到原缩放。",
                confirmText: "保留",
                cancelText:  "还原",
                onConfirm:   () => this.SendCommand(new Cmd_CommitUiScale()),
                onCancel:    () => this.SendCommand(new Cmd_RevertUiScale()),
                countdownSeconds: CountdownSeconds);
        }

        private void SyncSliderFromModel(float v)
        {
            _pendingScale = v;
            _slider?.SetValueWithoutNotify(v);
            if (_valueLabel != null) _valueLabel.text = FormatScale(v);
            RefreshProgressFill(v);
        }

        private void RefreshProgressFill(float value)
        {
            if (_progressFill == null) return;

            float normalized = Mathf.InverseLerp(SettingsModel.MinScale, SettingsModel.MaxScale, value);
            _progressFill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
        }

        internal static float  SnapToStep(float v) => Mathf.Round(v * 10f) / 10f;
        internal static string FormatScale(float v) => $"{v:0.0}×";
    }
}
