using APP.Pomodoro;
using APP.Pomodoro.Controller;
using APP.Settings.Model;
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

        [SetUp]
        public void SetUp()
        {
            _ = GameApp.Interface;
            _model = GameApp.Interface.GetModel<ISettingsModel>();
            _model.UiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
            _model.PreviewUiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);

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
        }

        [TearDown]
        public void TearDown()
        {
            if (_lifecycle != null) UnityEngine.Object.DestroyImmediate(_lifecycle);
        }

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
    }
}
