using APP.Settings.Command;
using APP.Settings.Model;
using APP.Utility;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Settings.Tests
{
    /// <summary>
    /// 9 个 Cmd_*Binding* 行为：
    ///   - SetBindingEnabled
    ///   - AddBindingKey / RemoveBindingKey
    ///   - SetSyncedBindingKey (toggle)
    ///   - SetBindingEntryEnabled
    ///   - BeginBindingCapture / CompleteBindingCapture / CancelBindingCapture
    ///   - IncrementBindingCount / ResetBindingCount
    /// </summary>
    public sealed class BindingKeyCommandTests
    {
        private const string EnabledKey      = "BindingKey.Enabled";
        private const string SyncedKeyIdKey  = "BindingKey.SyncedKeyId";
        private const string EntriesJsonKey  = "BindingKey.EntriesJson";

        private sealed class TestArch : Architecture<TestArch>
        {
            protected override void Init()
            {
                RegisterUtility<IStorageUtility>(new PlayerPrefsStorageUtility());
                RegisterModel<IBindingKeyModel>(new BindingKeyModel());
            }
        }

        [SetUp]
        public void Clear()
        {
            PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.DeleteKey(SyncedKeyIdKey);
            PlayerPrefs.DeleteKey(EntriesJsonKey);
        }

        [TearDown]
        public void Cleanup() => Clear();

        private static IBindingKeyModel ResetModel()
        {
            _ = TestArch.Interface;
            var m = TestArch.Interface.GetModel<IBindingKeyModel>();
            // 把 list 清空（架构是单例，多 [Test] 共用）
            for (int i = m.Entries.Count - 1; i >= 0; i--)
            {
                m.RemoveEntry(m.Entries[i].Id);
            }
            m.Enabled.Value = false;
            m.SyncedKeyId.Value = string.Empty;
            m.ListeningKeyId.Value = string.Empty;
            return m;
        }

        [Test]
        public void Cmd_SetBindingEnabled_TogglesGlobal()
        {
            var m = ResetModel();
            TestArch.Interface.SendCommand(new Cmd_SetBindingEnabled(true));
            Assert.That(m.Enabled.Value, Is.True);
            TestArch.Interface.SendCommand(new Cmd_SetBindingEnabled(false));
            Assert.That(m.Enabled.Value, Is.False);
        }

        [Test]
        public void Cmd_AddBindingKey_ReturnsIdAndAppendsEntry()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            Assert.That(id, Is.Not.Empty);
            Assert.That(m.Entries.Count, Is.EqualTo(1));
            Assert.That(m.Entries[0].Id, Is.EqualTo(id));
        }

        [Test]
        public void Cmd_RemoveBindingKey_RemovesEntry()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            TestArch.Interface.SendCommand(new Cmd_RemoveBindingKey(id));
            Assert.That(m.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void Cmd_SetSyncedBindingKey_TogglesSamePathClearsOnRepeat()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            TestArch.Interface.SendCommand(new Cmd_SetSyncedBindingKey(id));
            Assert.That(m.SyncedKeyId.Value, Is.EqualTo(id));
            // 同 id 再次调用 → 取消
            TestArch.Interface.SendCommand(new Cmd_SetSyncedBindingKey(id));
            Assert.That(m.SyncedKeyId.Value, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Cmd_SetBindingEntryEnabled_Toggles()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            TestArch.Interface.SendCommand(new Cmd_SetBindingEntryEnabled(id, true));
            Assert.That(m.Entries[0].Enabled, Is.True);
            TestArch.Interface.SendCommand(new Cmd_SetBindingEntryEnabled(id, false));
            Assert.That(m.Entries[0].Enabled, Is.False);
        }

        [Test]
        public void Cmd_BeginCapture_SetsListeningKeyIdToTargetEntry()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            TestArch.Interface.SendCommand(new Cmd_BeginBindingCapture(id));
            Assert.That(m.ListeningKeyId.Value, Is.EqualTo(id));
        }

        [Test]
        public void Cmd_CompleteCapture_WritesEntryKeyAndClearsListening()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            TestArch.Interface.SendCommand(new Cmd_BeginBindingCapture(id));
            TestArch.Interface.SendCommand(new Cmd_CompleteBindingCapture(id, (int)KeyCode.Space, "空格"));
            Assert.That(m.ListeningKeyId.Value, Is.EqualTo(string.Empty));
            Assert.That(m.Entries[0].KeyCode, Is.EqualTo((int)KeyCode.Space));
            Assert.That(m.Entries[0].KeyLabel, Is.EqualTo("空格"));
        }

        [Test]
        public void Cmd_CancelCapture_ClearsListeningOnly()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            TestArch.Interface.SendCommand(new Cmd_CompleteBindingCapture(id, (int)KeyCode.F, "F"));
            TestArch.Interface.SendCommand(new Cmd_BeginBindingCapture(id));
            TestArch.Interface.SendCommand(new Cmd_CancelBindingCapture());
            Assert.That(m.ListeningKeyId.Value, Is.EqualTo(string.Empty));
            Assert.That(m.Entries[0].KeyCode, Is.EqualTo((int)KeyCode.F));
            Assert.That(m.Entries[0].KeyLabel, Is.EqualTo("F"));
        }

        [Test]
        public void Cmd_IncrementAndReset_BindingCount()
        {
            var m = ResetModel();
            string id = TestArch.Interface.SendCommand<string>(new Cmd_AddBindingKey());
            TestArch.Interface.SendCommand(new Cmd_IncrementBindingCount(id));
            TestArch.Interface.SendCommand(new Cmd_IncrementBindingCount(id));
            TestArch.Interface.SendCommand(new Cmd_IncrementBindingCount(id));
            Assert.That(m.Entries[0].PressCount, Is.EqualTo(3));
            TestArch.Interface.SendCommand(new Cmd_ResetBindingCount(id));
            Assert.That(m.Entries[0].PressCount, Is.EqualTo(0));
        }
    }
}
