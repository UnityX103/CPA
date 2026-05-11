using APP.Settings.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Settings.Tests
{
    /// <summary>
    /// BindingKeyModel（多绑定）测试：
    ///   - OnInit 默认值
    ///   - AddEntry/RemoveEntry/TryUpdateEntryKey/IncrementEntry/ResetEntryCount/SetEntryEnabled
    ///   - EntriesRevision 在每次修改后 +1
    ///   - SyncedKeyId 在 Remove 时被清空（如果是当前 synced）
    ///   - Listening 不持久化
    /// </summary>
    public sealed class BindingKeyModelTests
    {
        private const string EnabledKey      = "BindingKey.Enabled";
        private const string SyncedKeyIdKey  = "BindingKey.SyncedKeyId";
        private const string EntriesJsonKey  = "BindingKey.EntriesJson";

        private sealed class TestArch : Architecture<TestArch>
        {
            protected override void Init()
            {
                RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());
            }
        }

        private static BindingKeyModel CreateModel()
        {
            _ = TestArch.Interface;
            var model = new BindingKeyModel();
            ((ICanSetArchitecture)model).SetArchitecture(TestArch.Interface);
            ((ICanInit)model).Init();
            return model;
        }

        [SetUp]
        public void ClearKeys()
        {
            PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.DeleteKey(SyncedKeyIdKey);
            PlayerPrefs.DeleteKey(EntriesJsonKey);
        }

        [TearDown]
        public void CleanupKeys() => ClearKeys();

        [Test]
        public void OnInit_NoSavedValues_HasEmptyListAndDefaults()
        {
            var m = CreateModel();
            Assert.That(m.Enabled.Value, Is.EqualTo(BindingKeyModel.DefaultEnabled));
            Assert.That(m.SyncedKeyId.Value, Is.EqualTo(string.Empty));
            Assert.That(m.ListeningKeyId.Value, Is.EqualTo(string.Empty));
            Assert.That(m.Entries.Count, Is.EqualTo(0));
            Assert.That(m.EntriesRevision.Value, Is.EqualTo(0));
        }

        [Test]
        public void AddEntry_AppendsDefaultEntryAndBumpsRevision()
        {
            var m = CreateModel();
            int rev0 = m.EntriesRevision.Value;
            string id = m.AddEntry();
            Assert.That(id, Is.Not.Empty);
            Assert.That(m.Entries.Count, Is.EqualTo(1));
            Assert.That(m.Entries[0].Id, Is.EqualTo(id));
            Assert.That(m.Entries[0].KeyLabel, Is.EqualTo(BindingKeyModel.DefaultBoundKeyLabel));
            Assert.That(m.Entries[0].KeyCode, Is.EqualTo(BindingKeyModel.DefaultBoundKeyCode));
            // 默认 Enabled=true：UI 当前没有 per-entry 启用 toggle，存在即可被全局 tick 计数
            Assert.That(m.Entries[0].Enabled, Is.True);
            Assert.That(m.EntriesRevision.Value, Is.GreaterThan(rev0));
        }

        [Test]
        public void RemoveEntry_AlsoClearsSyncedIdWhenSame()
        {
            var m = CreateModel();
            string id1 = m.AddEntry();
            string id2 = m.AddEntry();
            m.SyncedKeyId.Value = id1;

            Assert.That(m.RemoveEntry(id1), Is.True);
            Assert.That(m.SyncedKeyId.Value, Is.EqualTo(string.Empty), "Synced entry 被删 → SyncedKeyId 应清空。");
            Assert.That(m.Entries.Count, Is.EqualTo(1));
            Assert.That(m.Entries[0].Id, Is.EqualTo(id2));
        }

        [Test]
        public void TryUpdateEntryKey_WritesCodeAndLabel()
        {
            var m = CreateModel();
            string id = m.AddEntry();
            Assert.That(m.TryUpdateEntryKey(id, (int)KeyCode.Space, "空格"), Is.True);
            Assert.That(m.Entries[0].KeyCode, Is.EqualTo((int)KeyCode.Space));
            Assert.That(m.Entries[0].KeyLabel, Is.EqualTo("空格"));
        }

        [Test]
        public void IncrementEntry_AndReset()
        {
            var m = CreateModel();
            string id = m.AddEntry();
            m.IncrementEntry(id);
            m.IncrementEntry(id);
            m.IncrementEntry(id);
            Assert.That(m.Entries[0].PressCount, Is.EqualTo(3));
            m.ResetEntryCount(id);
            Assert.That(m.Entries[0].PressCount, Is.EqualTo(0));
        }

        [Test]
        public void SetEntryEnabled_TogglesFlag()
        {
            var m = CreateModel();
            string id = m.AddEntry();
            // AddEntry 默认 Enabled=true；先关再开再关，覆盖双向切换
            Assert.That(m.Entries[0].Enabled, Is.True);
            m.SetEntryEnabled(id, false);
            Assert.That(m.Entries[0].Enabled, Is.False);
            m.SetEntryEnabled(id, true);
            Assert.That(m.Entries[0].Enabled, Is.True);
            m.SetEntryEnabled(id, false);
            Assert.That(m.Entries[0].Enabled, Is.False);
        }

        [Test]
        public void EntriesRevision_BumpsOnEachMutation()
        {
            var m = CreateModel();
            int r0 = m.EntriesRevision.Value;
            string id = m.AddEntry();
            int r1 = m.EntriesRevision.Value;
            m.IncrementEntry(id);
            int r2 = m.EntriesRevision.Value;
            m.RemoveEntry(id);
            int r3 = m.EntriesRevision.Value;
            Assert.That(r1, Is.GreaterThan(r0));
            Assert.That(r2, Is.GreaterThan(r1));
            Assert.That(r3, Is.GreaterThan(r2));
        }

        [Test]
        public void Listening_DoesNotPersist()
        {
            var m = CreateModel();
            m.ListeningKeyId.Value = "abc";
            Assert.That(PlayerPrefs.HasKey("BindingKey.ListeningKeyId"), Is.False);
        }
    }
}
