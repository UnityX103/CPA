using APP.Pomodoro.Controller;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityEngine;

namespace APP.Settings.Tests
{
    public sealed class ConfirmDialogControllerTests
    {
        private VisualElement _host;
        private VisualTreeAsset _template;
        private ConfirmDialogController _ctrl;

        [SetUp]
        public void SetUp()
        {
            _host = new VisualElement();
            _template = Resources.Load<VisualTreeAsset>("ConfirmDialog");
            if (_template == null)
            {
                #if UNITY_EDITOR
                _template = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/UI_V2/Documents/ConfirmDialog.uxml");
                #endif
            }
            Assert.That(_template, Is.Not.Null, "ConfirmDialog.uxml 未找到");

            _ctrl = new ConfirmDialogController();
            _ctrl.Init(_host, _template);
        }

        [Test]
        public void InitialState_IsHidden()
        {
            Assert.That(_ctrl.IsVisible, Is.False);
        }

        [Test]
        public void Show_NoCountdown_BecomesVisibleAndHidesCountdownRow()
        {
            _ctrl.Show("T", "S", "B", "Y", "N", null, null, countdownSeconds: 0f);

            Assert.That(_ctrl.IsVisible, Is.True);
            var row = _host.Q<VisualElement>("dlg-countdown");
            Assert.That(row.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void Show_WithCountdown_ShowsCountdownRowAndSetsInitialSeconds()
        {
            _ctrl.Show("T", "S", "B", "Y", "N", null, null, countdownSeconds: 5f);

            var row = _host.Q<VisualElement>("dlg-countdown");
            Assert.That(row.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_ctrl.RemainingSeconds, Is.EqualTo(5f));

            var txt = _host.Q<Label>("dlg-countdown-text");
            StringAssert.Contains("5", txt.text);
        }

        [Test]
        public void TickElapsed_DecrementsRemainingSecondsAndRefreshesLabel()
        {
            _ctrl.Show("T", "S", "B", "Y", "N", null, null, countdownSeconds: 5f);

            _ctrl.TickElapsed(0.5f);

            Assert.That(_ctrl.RemainingSeconds, Is.EqualTo(4.5f).Within(0.001f));
            var txt = _host.Q<Label>("dlg-countdown-text");
            StringAssert.Contains("5", txt.text);  // ceil(4.5) = 5
        }

        [Test]
        public void TickElapsed_ReachesZero_InvokesOnCancelAndHides()
        {
            int cancelCount = 0;
            int confirmCount = 0;
            _ctrl.Show("T", "S", "B", "Y", "N",
                onConfirm: () => confirmCount++,
                onCancel:  () => cancelCount++,
                countdownSeconds: 1f);

            _ctrl.TickElapsed(1.1f);

            Assert.That(_ctrl.IsVisible, Is.False);
            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(confirmCount, Is.EqualTo(0));
        }

        [Test]
        public void ManualConfirm_InvokesOnConfirmAndHides()
        {
            int confirmCount = 0;
            _ctrl.Show("T", "S", "B", "Y", "N",
                onConfirm: () => confirmCount++,
                onCancel:  null,
                countdownSeconds: 0f);

            _ctrl.TriggerConfirmForTest();

            Assert.That(_ctrl.IsVisible, Is.False);
            Assert.That(confirmCount, Is.EqualTo(1));
        }

        [Test]
        public void ManualCancel_InvokesOnCancelAndHides()
        {
            int cancelCount = 0;
            _ctrl.Show("T", "S", "B", "Y", "N",
                onConfirm: null,
                onCancel:  () => cancelCount++,
                countdownSeconds: 0f);

            _ctrl.TriggerCancelForTest();

            Assert.That(_ctrl.IsVisible, Is.False);
            Assert.That(cancelCount, Is.EqualTo(1));
        }

        [Test]
        public void TextsAreApplied()
        {
            _ctrl.Show("标题X", "副标题Y", "正文Z", "确认", "取消", null, null, 0f);

            Assert.That(_host.Q<Label>("dlg-title").text, Is.EqualTo("标题X"));
            Assert.That(_host.Q<Label>("dlg-subtitle").text, Is.EqualTo("副标题Y"));
            Assert.That(_host.Q<Label>("dlg-body").text, Is.EqualTo("正文Z"));
            Assert.That(_host.Q<Button>("dlg-confirm").text, Is.EqualTo("确认"));
            Assert.That(_host.Q<Button>("dlg-cancel").text, Is.EqualTo("取消"));
        }
    }
}
