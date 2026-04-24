using APP.Settings.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Settings.Tests
{
    public sealed class SettingsModelTests
    {
        private const string UiScaleKey = "Settings.UiScale";

        // 最小 Architecture，仅注册 PlayerPrefsStorageUtility，供 SettingsModel.OnInit 使用
        private sealed class TestArch : Architecture<TestArch>
        {
            protected override void Init()
            {
                RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());
            }
        }

        private SettingsModel CreateModel()
        {
            _ = TestArch.Interface;  // 确保 Init 执行过一次
            var model = new SettingsModel();
            ((ICanSetArchitecture)model).SetArchitecture(TestArch.Interface);
            ((ICanInit)model).Init();
            return model;
        }

        [SetUp]
        public void ClearKey() => PlayerPrefs.DeleteKey(UiScaleKey);

        [TearDown]
        public void CleanupKey() => PlayerPrefs.DeleteKey(UiScaleKey);

        [Test]
        public void OnInit_NoSavedValue_DefaultsTo1()
        {
            var model = CreateModel();

            Assert.That(model.UiScale.Value, Is.EqualTo(SettingsModel.DefaultScale));
            Assert.That(model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.DefaultScale));
        }

        [Test]
        public void OnInit_LoadsSavedValue_IntoBothFields()
        {
            PlayerPrefs.SetFloat(UiScaleKey, 1.5f);
            var model = CreateModel();

            Assert.That(model.UiScale.Value, Is.EqualTo(1.5f));
            Assert.That(model.PreviewUiScale.Value, Is.EqualTo(1.5f));
        }

        [Test]
        public void OnInit_ClampsOutOfRange_ToBounds()
        {
            PlayerPrefs.SetFloat(UiScaleKey, 10f);  // 超上限
            var model = CreateModel();

            Assert.That(model.UiScale.Value, Is.EqualTo(SettingsModel.MaxScale));
            Assert.That(model.PreviewUiScale.Value, Is.EqualTo(SettingsModel.MaxScale));
        }

        [Test]
        public void UiScaleChange_PersistsToPlayerPrefs()
        {
            var model = CreateModel();

            model.UiScale.Value = 1.3f;

            Assert.That(PlayerPrefs.GetFloat(UiScaleKey, -1f), Is.EqualTo(1.3f));
        }

        [Test]
        public void PreviewUiScaleChange_DoesNotPersist()
        {
            var model = CreateModel();
            PlayerPrefs.DeleteKey(UiScaleKey);  // 清除 OnInit 可能写入的默认值

            model.PreviewUiScale.Value = 1.8f;

            // 预览变化不触发持久化：PlayerPrefs 中应不存在该 key（或仍是旧值）
            Assert.That(PlayerPrefs.HasKey(UiScaleKey), Is.False);
        }
    }
}
