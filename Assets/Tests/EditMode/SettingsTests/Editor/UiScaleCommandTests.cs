using APP.Pomodoro;  // GameApp
using APP.Settings.Command;
using APP.Settings.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Settings.Tests
{
    public sealed class UiScaleCommandTests
    {
        private ISettingsModel Model => GameApp.Interface.GetModel<ISettingsModel>();

        [SetUp]
        public void ResetModel()
        {
            PlayerPrefs.DeleteKey("Settings.UiScale");
            // 让 Architecture 初始化
            _ = GameApp.Interface;
            Model.UiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
            Model.PreviewUiScale.SetValueWithoutEvent(SettingsModel.DefaultScale);
        }

        [Test]
        public void SetPreview_ClampsToBounds()
        {
            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(10f));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.MaxScale));

            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(-1f));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.MinScale));
        }

        [Test]
        public void SetPreview_InvalidNumber_DoesNotWrite()
        {
            Model.PreviewUiScale.SetValueWithoutEvent(1.2f);

            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(float.NaN));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.2f));

            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(float.PositiveInfinity));
            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.2f));
        }

        [Test]
        public void SetPreview_DoesNotChangeUiScale()
        {
            GameApp.Interface.SendCommand(new Cmd_SetPreviewUiScale(1.7f));

            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.7f));
            Assert.That(Model.UiScale.Value, Is.EqualTo(SettingsModel.DefaultScale));
        }

        [Test]
        public void Commit_CopiesPreviewToUiScale()
        {
            Model.PreviewUiScale.SetValueWithoutEvent(1.4f);

            GameApp.Interface.SendCommand(new Cmd_CommitUiScale());

            Assert.That(Model.UiScale.Value, Is.EqualTo(1.4f));
        }

        [Test]
        public void Revert_CopiesUiScaleToPreview()
        {
            Model.UiScale.SetValueWithoutEvent(1.0f);
            Model.PreviewUiScale.SetValueWithoutEvent(1.9f);

            GameApp.Interface.SendCommand(new Cmd_RevertUiScale());

            Assert.That(Model.PreviewUiScale.Value, Is.EqualTo(1.0f));
        }
    }
}
