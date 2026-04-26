using System.Collections.Generic;
using APP.Pomodoro;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using APP.Settings.Model;
using APP.Settings.Queries;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Settings.Tests
{
    public sealed class GlobalSettingsPanelControllerTests
    {
        private VisualElement _panelRoot;
        private VisualElement _dialogHost;
        private VisualTreeAsset _panelTemplate;
        private VisualTreeAsset _dialogTemplate;
        private GlobalSettingsPanelController _ctrl;
        private GameObject _lifecycle;
        private ISettingsModel _model;
        private IPomodoroModel _pomo;

        private static readonly IReadOnlyList<DisplayChoice> FakeDisplays = new[]
        {
            new DisplayChoice(0, "显示器 1（1920×1080）"),
            new DisplayChoice(1, "显示器 2（2560×1440）"),
            new DisplayChoice(2, "显示器 3（3840×2160）"),
        };

        [SetUp]
        public void SetUp()
        {
            _ = GameApp.Interface;
            _model = GameApp.Interface.GetModel<ISettingsModel>();
            _pomo  = GameApp.Interface.GetModel<IPomodoroModel>();
            _model.UiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
            _model.PreviewUiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
            _model.PreviewTargetDisplay.SetValueWithoutEvent(0);
            _pomo.TargetMonitorIndex.SetValueWithoutEvent(0);

            #if UNITY_EDITOR
            _panelTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI_V2/Documents/GlobalSettingsPanel.uxml");
            _dialogTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI_V2/Documents/ConfirmDialog.uxml");
            #endif
            Assert.That(_panelTemplate,  Is.Not.Null, "GlobalSettingsPanel.uxml 未找到");
            Assert.That(_dialogTemplate, Is.Not.Null, "ConfirmDialog.uxml 未找到");

            _panelRoot  = _panelTemplate.CloneTree();
            _dialogHost = new VisualElement();
            _lifecycle  = new GameObject("LifecycleOwner");

            _ctrl = new GlobalSettingsPanelController();
            _ctrl.Init(_panelRoot, _dialogHost, _dialogTemplate, _lifecycle);
            _ctrl.OverrideAvailableDisplaysForTest(FakeDisplays);
        }

        [TearDown]
        public void TearDown()
        {
            if (_lifecycle != null) UnityEngine.Object.DestroyImmediate(_lifecycle);
        }

        // ─── 缩放（既有用例）────────────────────────────────────────

        [Test]
        public void Initial_SliderReflectsUiScale()
        {
            Assert.That(_panelRoot.Q<Slider>("gsp-scale-slider").value,
                Is.EqualTo(SettingsModel.DefaultScale));
            Assert.That(_panelRoot.Q<Label>("gsp-scale-value").text,
                Is.EqualTo("1.0×"));
        }

        [Test]
        public void SliderDrag_DoesNotChangeModel()
        {
            var slider = _panelRoot.Q<Slider>("gsp-scale-slider");
            slider.value = 1.5f;  // 触发 ChangeEvent

            Assert.That(_model.UiScale.Value, Is.EqualTo(1.0f));
            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.0f));
        }

        [Test]
        public void Apply_SameValue_DoesNothing()
        {
            // 滑块保持默认 1.0，直接点 Apply
            _ctrl.TriggerApplyForTest();

            Assert.That(_ctrl.IsScaleDialogVisible, Is.False);
            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.0f));
        }

        [Test]
        public void Apply_DifferentValue_WritesPreviewAndShowsDialog()
        {
            // EditMode 下 Slider 无 panel 时 value setter 不派发 ChangeEvent，用内部钩子模拟
            _ctrl.TriggerSliderChangeForTest(1.5f);

            _ctrl.TriggerApplyForTest();

            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.5f));
            Assert.That(_model.UiScale.Value, Is.EqualTo(1.0f));
            Assert.That(_ctrl.IsScaleDialogVisible, Is.True);
        }

        [Test]
        public void Apply_DialogAlreadyVisible_DoesNotReenter()
        {
            _ctrl.TriggerSliderChangeForTest(1.5f);
            _ctrl.TriggerApplyForTest();

            _ctrl.TriggerSliderChangeForTest(1.7f);
            _ctrl.TriggerApplyForTest();

            // PreviewUiScale 仍是第一次的 1.5
            Assert.That(_model.PreviewUiScale.Value, Is.EqualTo(1.5f));
        }

        [Test]
        public void RefreshFromModel_ResetsSliderToUiScale()
        {
            var slider = _panelRoot.Q<Slider>("gsp-scale-slider");
            slider.value = 1.8f;  // 未 apply 的残留
            Assert.That(_model.UiScale.Value, Is.EqualTo(1.0f));

            _ctrl.RefreshFromModel();

            Assert.That(slider.value, Is.EqualTo(1.0f));
        }

        [Test]
        public void UiScaleChange_SyncsSlider()
        {
            _model.UiScale.Value = 1.3f;

            Assert.That(_panelRoot.Q<Slider>("gsp-scale-slider").value, Is.EqualTo(1.3f));
            Assert.That(_panelRoot.Q<Label>("gsp-scale-value").text, Is.EqualTo("1.3×"));
        }

        [Test]
        public void CalculateProgressFillWidth_AlignsToSliderThumbCenter()
        {
            const float TrackWidth = 320f;

            Assert.That(
                GlobalSettingsPanelController.CalculateProgressFillWidth(SettingsModel.MinScale, TrackWidth),
                Is.EqualTo(10f).Within(0.001f));
            Assert.That(
                GlobalSettingsPanelController.CalculateProgressFillWidth(SettingsModel.DefaultScale, TrackWidth),
                Is.EqualTo(108.666672f).Within(0.001f));
            Assert.That(
                GlobalSettingsPanelController.CalculateProgressFillWidth(SettingsModel.MaxScale, TrackWidth),
                Is.EqualTo(306f).Within(0.001f));
        }

        // ─── 目标显示器 ────────────────────────────────────────────

        [Test]
        public void Initial_DropdownReflectsCommittedTarget()
        {
            // SetUp 阶段 TargetMonitorIndex=0 → 应当显示第一项
            Assert.That(_ctrl.CurrentDropdownValueForTest, Is.EqualTo(FakeDisplays[0].Label));
        }

        [Test]
        public void DropdownChange_SameIndex_DoesNothing()
        {
            _ctrl.TriggerDropdownChangeForTest(0);

            Assert.That(_ctrl.IsScaleDialogVisible, Is.False);
            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(0));
            Assert.That(_pomo.TargetMonitorIndex.Value, Is.EqualTo(0));
        }

        [Test]
        public void DropdownChange_DifferentIndex_WritesPreviewAndShowsDialog()
        {
            _ctrl.TriggerDropdownChangeForTest(2);

            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(2));
            // 已提交的目标不应被改动（持久化要等 commit）
            Assert.That(_pomo.TargetMonitorIndex.Value, Is.EqualTo(0));
            Assert.That(_ctrl.IsScaleDialogVisible, Is.True);
            Assert.That(_ctrl.CurrentDropdownValueForTest, Is.EqualTo(FakeDisplays[2].Label));
        }

        [Test]
        public void DropdownChange_DialogVisible_RejectsAndResetsToPreview()
        {
            _ctrl.TriggerDropdownChangeForTest(1);
            // 此时 PreviewTargetDisplay=1, dialog 可见

            _ctrl.TriggerDropdownChangeForTest(2);

            // 第二次选择被拒绝，PreviewTargetDisplay 仍是 1
            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(1));
            Assert.That(_ctrl.CurrentDropdownValueForTest, Is.EqualTo(FakeDisplays[1].Label));
        }

        [Test]
        public void Confirm_CommitsTargetMonitor()
        {
            _ctrl.TriggerDropdownChangeForTest(2);
            Assert.That(_pomo.TargetMonitorIndex.Value, Is.EqualTo(0));

            _ctrl.TriggerScaleDialogConfirmForTest();

            Assert.That(_pomo.TargetMonitorIndex.Value, Is.EqualTo(2));
            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(2));
            Assert.That(_ctrl.IsScaleDialogVisible, Is.False);
        }

        [Test]
        public void Cancel_RevertsPreviewToCommitted()
        {
            _ctrl.TriggerDropdownChangeForTest(2);
            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(2));

            _ctrl.TriggerScaleDialogCancelForTest();

            Assert.That(_pomo.TargetMonitorIndex.Value, Is.EqualTo(0));
            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(0));
            Assert.That(_ctrl.CurrentDropdownValueForTest, Is.EqualTo(FakeDisplays[0].Label));
            Assert.That(_ctrl.IsScaleDialogVisible, Is.False);
        }

        [Test]
        public void RefreshFromModel_ResetsDropdownToCommitted()
        {
            // 模拟用户已切换到显示器 2 并保存
            _pomo.TargetMonitorIndex.Value = 2;
            // 然后开始一次未提交的预览
            _ctrl.TriggerDropdownChangeForTest(1);
            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(1));

            // RefreshFromModel 应丢弃预览残留
            _ctrl.TriggerScaleDialogCancelForTest();   // 先关掉弹窗
            _ctrl.RefreshFromModel();

            Assert.That(_model.PreviewTargetDisplay.Value, Is.EqualTo(2));
            Assert.That(_ctrl.CurrentDropdownValueForTest, Is.EqualTo(FakeDisplays[2].Label));
        }

        [Test]
        public void TargetMonitorIndexChange_SyncsDropdown()
        {
            // 外部直接改 TargetMonitorIndex（例如启动时从持久化恢复），dropdown 应跟随
            _pomo.TargetMonitorIndex.Value = 1;

            Assert.That(_ctrl.CurrentDropdownValueForTest, Is.EqualTo(FakeDisplays[1].Label));
        }
    }
}
