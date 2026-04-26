using System.Collections.Generic;
using APP.Pomodoro;
using APP.Pomodoro.Model;
using APP.Settings.Command;
using APP.Settings.Model;
using APP.Settings.Queries;
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
    ///
    /// 目标显示器走相同 preview / commit / revert 流程：
    /// 下拉选择即触发 Cmd_SetPreviewTargetDisplay（物理移动窗口）+ 弹窗倒计时；
    /// 保留 → Cmd_CommitTargetDisplay（写 IPomodoroModel.TargetMonitorIndex）；
    /// 还原 / 超时 → Cmd_RevertTargetDisplay（窗口移回 + dropdown 复位）。
    /// </summary>
    public sealed class GlobalSettingsPanelController : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        public const float CountdownSeconds = 5f;

        // ─── 缩放 ─────────────────────────────────────────────────
        private Slider _slider;
        private VisualElement _progressFill;
        private Label _valueLabel;
        private Button _applyBtn;
        private ConfirmDialogController _scaleDialog;
        private float _pendingScale;

        // ─── 目标显示器 ───────────────────────────────────────────
        private DropdownField _displayDropdown;
        private IReadOnlyList<DisplayChoice> _availableDisplays;
        private int _pendingDisplayIndex;

        public bool IsScaleDialogVisible => _scaleDialog?.IsVisible == true;

        public void Init(
            VisualElement root,
            VisualElement dialogHost,
            VisualTreeAsset confirmDialogTemplate,
            GameObject lifecycleOwner)
        {
            var settings = this.GetModel<ISettingsModel>();
            var pomo     = this.GetModel<IPomodoroModel>();

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

            SyncSliderFromModel(settings.UiScale.Value);

            _slider?.RegisterValueChangedCallback(OnSliderChanged);
            if (_applyBtn != null) _applyBtn.clicked += OnApplyClicked;

            settings.UiScale.Register(SyncSliderFromModel)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);

            // 目标显示器
            _displayDropdown = root.Q<DropdownField>("gsp-display-dropdown");
            if (_displayDropdown != null)
            {
                _availableDisplays = this.SendQuery(new Q_GetAvailableDisplays());
                var choices = new List<string>(_availableDisplays.Count);
                foreach (var d in _availableDisplays) choices.Add(d.Label);
                _displayDropdown.choices = choices;

                int initialIndex = Mathf.Clamp(pomo.TargetMonitorIndex.Value, 0, _availableDisplays.Count - 1);
                _pendingDisplayIndex = initialIndex;
                settings.PreviewTargetDisplay.SetValueWithoutEvent(initialIndex);
                SyncDropdownFromIndex(initialIndex);

                _displayDropdown.RegisterValueChangedCallback(OnDropdownChanged);
            }

            settings.PreviewTargetDisplay.Register(SyncDropdownFromIndex)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            pomo.TargetMonitorIndex.Register(SyncDropdownFromIndex)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
        }

        /// <summary>进入 global tab 时调用，把滑块刷回 UiScale（丢弃上次未应用的拖动残留）。</summary>
        public void RefreshFromModel()
        {
            var settings = this.GetModel<ISettingsModel>();
            var pomo     = this.GetModel<IPomodoroModel>();
            SyncSliderFromModel(settings.UiScale.Value);
            int committed = pomo.TargetMonitorIndex.Value;
            settings.PreviewTargetDisplay.SetValueWithoutEvent(committed);
            SyncDropdownFromIndex(committed);
            _pendingDisplayIndex = committed;
        }

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

        /// <summary>测试钩子（internal）：模拟用户从下拉框选中索引 index。</summary>
        internal void TriggerDropdownChangeForTest(int index) => HandleDropdownIndex(index);

        /// <summary>
        /// 测试钩子（internal）：注入伪显示器列表（绕过 UniWindowController）。
        /// EditMode 没有真实显示器列表，注入后才能验证多选项流程。
        /// </summary>
        internal void OverrideAvailableDisplaysForTest(IReadOnlyList<DisplayChoice> displays)
        {
            _availableDisplays = displays;
            if (_displayDropdown != null)
            {
                var choices = new List<string>(displays.Count);
                foreach (var d in displays) choices.Add(d.Label);
                _displayDropdown.choices = choices;
            }
            int committed = this.GetModel<IPomodoroModel>().TargetMonitorIndex.Value;
            int safe = displays.Count == 0 ? 0 : Mathf.Clamp(committed, 0, displays.Count - 1);
            _pendingDisplayIndex = safe;
            SyncDropdownFromIndex(safe);
        }

        /// <summary>测试钩子（internal）：当前 dropdown 显示的字符串。</summary>
        internal string CurrentDropdownValueForTest => _displayDropdown?.value;

        /// <summary>测试钩子（internal）：直接触发 ConfirmDialog 的"保留"。</summary>
        internal void TriggerScaleDialogConfirmForTest() => _scaleDialog?.TriggerConfirmForTest();

        /// <summary>测试钩子（internal）：直接触发 ConfirmDialog 的"还原"。</summary>
        internal void TriggerScaleDialogCancelForTest() => _scaleDialog?.TriggerCancelForTest();

        // ─── 内部 ────────────────────────────────────────────────

        private void OnSliderChanged(ChangeEvent<float> evt)
        {
            _pendingScale    = SnapToStep(evt.newValue);
            if (_valueLabel != null) _valueLabel.text = FormatScale(_pendingScale);
            RefreshProgressFill(_pendingScale);
        }

        private void OnApplyClicked()
        {
            var settings = this.GetModel<ISettingsModel>();
            var current  = settings.UiScale.Value;
            var target   = SnapToStep(_pendingScale);

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

        private void OnDropdownChanged(ChangeEvent<string> evt)
        {
            if (_availableDisplays == null) return;
            int newIndex = -1;
            for (int i = 0; i < _availableDisplays.Count; i++)
            {
                if (_availableDisplays[i].Label == evt.newValue)
                {
                    newIndex = i;
                    break;
                }
            }
            if (newIndex < 0) return;
            HandleDropdownIndex(newIndex);
        }

        private void HandleDropdownIndex(int newIndex)
        {
            if (_availableDisplays == null || _availableDisplays.Count == 0) return;
            int clamped = Mathf.Clamp(newIndex, 0, _availableDisplays.Count - 1);
            int committed = this.GetModel<IPomodoroModel>().TargetMonitorIndex.Value;

            if (clamped == committed) return;

            // 弹窗已存在 —— 拒绝新的预览，并把 dropdown 同步回当前预览值（保持视觉一致）
            if (_scaleDialog.IsVisible)
            {
                int currentPreview = this.GetModel<ISettingsModel>().PreviewTargetDisplay.Value;
                SyncDropdownFromIndex(currentPreview);
                return;
            }

            _pendingDisplayIndex = clamped;
            this.SendCommand(new Cmd_SetPreviewTargetDisplay(clamped));

            string targetLabel  = _availableDisplays[clamped].Label;
            string currentLabel = _availableDisplays[Mathf.Clamp(committed, 0, _availableDisplays.Count - 1)].Label;

            _scaleDialog.Show(
                title:       "切换到该显示器吗？",
                subtitle:    $"目标 {targetLabel}，原 {currentLabel}",
                body:        "如 5 秒内未保留，将自动还原到原显示器。",
                confirmText: "保留",
                cancelText:  "还原",
                onConfirm:   () => this.SendCommand(new Cmd_CommitTargetDisplay()),
                onCancel:    () => this.SendCommand(new Cmd_RevertTargetDisplay()),
                countdownSeconds: CountdownSeconds);
        }

        private void SyncSliderFromModel(float v)
        {
            _pendingScale = v;
            _slider?.SetValueWithoutNotify(v);
            if (_valueLabel != null) _valueLabel.text = FormatScale(v);
            RefreshProgressFill(v);
        }

        private void SyncDropdownFromIndex(int index)
        {
            if (_displayDropdown == null || _availableDisplays == null || _availableDisplays.Count == 0) return;
            int safe = Mathf.Clamp(index, 0, _availableDisplays.Count - 1);
            _displayDropdown.SetValueWithoutNotify(_availableDisplays[safe].Label);
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
