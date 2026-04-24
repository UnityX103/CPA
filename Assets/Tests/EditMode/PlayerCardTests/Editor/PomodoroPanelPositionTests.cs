using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Tests
{
    public sealed class PomodoroPanelPositionTests
    {
        [Test]
        public void DefaultValue_IsNegativeInfinity_ActingAsSentinel()
        {
            var model = new PomodoroModel();
            ((IModel)model).Init();

            Vector2 pos = model.PomodoroPanelPosition.Value;
            Assert.That(float.IsNegativeInfinity(pos.x), "x 应为 -Infinity");
            Assert.That(float.IsNegativeInfinity(pos.y), "y 应为 -Infinity");
        }

        [Test]
        public void SetValue_PersistsInMemory()
        {
            var model = new PomodoroModel();
            ((IModel)model).Init();

            // 归一化比例值（0..1），与新语义一致
            var target = new Vector2(0.25f, 0.75f);
            model.PomodoroPanelPosition.Value = target;

            Assert.That(model.PomodoroPanelPosition.Value, Is.EqualTo(target));
        }

        [Test]
        public void Persistence_SaveAndLoad_RestoresPomodoroPanelPosition()
        {
            // 清 key 避免跨用例污染
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");

            var save = new PomodoroModel();
            ((IModel)save).Init();
            // 合法比例值：Save 原样写入，Load 原样读回
            save.PomodoroPanelPosition.Value = new Vector2(0.5f, 0.75f);

            PomodoroPersistence.Save(save, flushToDisk: true);

            var load = new PomodoroModel();
            ((IModel)load).Init();
            bool ok = PomodoroPersistence.TryLoad(load);

            Assert.That(ok, Is.True, "TryLoad 应成功");
            Assert.That(load.PomodoroPanelPosition.Value.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(load.PomodoroPanelPosition.Value.y, Is.EqualTo(0.75f).Within(0.001f));

            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [Test]
        public void Persistence_LegacyPixelValues_AreDiscardedAsSentinel()
        {
            // 模拟旧版持久化：以像素为单位写入（>1），Load 时应判定为脏数据并回落 sentinel
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
            const string legacyJson =
                "{\"FocusDurationSeconds\":1500,\"BreakDurationSeconds\":300,\"TotalRounds\":4," +
                "\"CurrentRound\":1,\"RemainingSeconds\":1500,\"CurrentPhase\":0,\"IsRunning\":false," +
                "\"IsPinned\":false,\"WindowAnchor\":1,\"AutoJumpToTopOnComplete\":true," +
                "\"AutoStartBreak\":true,\"TargetMonitorIndex\":0,\"CompletionClipIndex\":0," +
                "\"PomodoroPanelPositionX\":512.5,\"PomodoroPanelPositionY\":768.25," +
                "\"HasPomodoroPanelPosition\":true}";
            PlayerPrefs.SetString("APP.Pomodoro.PersistentState.v1", legacyJson);
            PlayerPrefs.Save();

            var load = new PomodoroModel();
            ((IModel)load).Init();
            bool ok = PomodoroPersistence.TryLoad(load);

            Assert.That(ok, Is.True);
            Assert.That(float.IsNegativeInfinity(load.PomodoroPanelPosition.Value.x),
                "越界像素值应被丢弃，保持 sentinel，由 View 首帧重算默认位置");
            Assert.That(float.IsNegativeInfinity(load.PomodoroPanelPosition.Value.y),
                "越界像素值应被丢弃，保持 sentinel");

            // 同时校验 PlayerPrefs 已被清洁版本覆盖——下次启动不再命中越界分支
            string cleaned = PlayerPrefs.GetString("APP.Pomodoro.PersistentState.v1", string.Empty);
            Assert.That(cleaned, Does.Contain("\"HasPomodoroPanelPosition\":false"),
                "越界数据应触发 PlayerPrefs 重写，持久化中不再存在越界值");

            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [Test]
        public void Persistence_LegacySave_MissingField_LeavesSentinel()
        {
            // 模拟旧版无该字段的 JSON
            const string legacyJson =
                "{\"FocusDurationSeconds\":1500,\"BreakDurationSeconds\":300,\"TotalRounds\":4," +
                "\"CurrentRound\":1,\"RemainingSeconds\":1500,\"CurrentPhase\":0,\"IsRunning\":false," +
                "\"IsTopmost\":false,\"WindowAnchor\":1,\"AutoJumpToTopOnComplete\":true," +
                "\"AutoStartBreak\":true,\"TargetMonitorIndex\":0,\"CompletionClipIndex\":0}";
            PlayerPrefs.SetString("APP.Pomodoro.PersistentState.v1", legacyJson);
            PlayerPrefs.Save();

            var model = new PomodoroModel();
            ((IModel)model).Init();
            PomodoroPersistence.TryLoad(model);

            Assert.That(float.IsNegativeInfinity(model.PomodoroPanelPosition.Value.x),
                "旧存档缺字段时应保持 sentinel，由 View 首帧算默认位置");

            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [Test]
        public void Cmd_SetPomodoroPanelPosition_WritesModel()
        {
            var model = APP.Pomodoro.GameApp.Interface.GetModel<IPomodoroModel>();
            // 合法比例值：Command 原样写入
            APP.Pomodoro.GameApp.Interface.SendCommand(
                new APP.Pomodoro.Command.Cmd_SetPomodoroPanelPosition(new Vector2(0.3f, 0.4f)));
            Assert.That(model.PomodoroPanelPosition.Value, Is.EqualTo(new Vector2(0.3f, 0.4f)));
        }

        [Test]
        public void Cmd_SetPomodoroPanelPosition_ClampsOutOfRangeValues()
        {
            var model = APP.Pomodoro.GameApp.Interface.GetModel<IPomodoroModel>();
            // >1 → 1, <0 → 0
            APP.Pomodoro.GameApp.Interface.SendCommand(
                new APP.Pomodoro.Command.Cmd_SetPomodoroPanelPosition(new Vector2(1.7f, -0.5f)));
            Assert.That(model.PomodoroPanelPosition.Value.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(model.PomodoroPanelPosition.Value.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void IsPinned_RoundTripsThroughPersistence()
        {
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");

            IPomodoroModel save = new PomodoroModel();
            save.IsPinned.Value = true;
            PomodoroPersistence.Save(save, flushToDisk: true);

            IPomodoroModel load = new PomodoroModel();
            bool ok = PomodoroPersistence.TryLoad(load);

            Assert.IsTrue(ok);
            Assert.IsTrue(load.IsPinned.Value);

            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");
        }

        [Test]
        public void IsPinned_DefaultFalse_WhenNoSavedState()
        {
            PlayerPrefs.DeleteKey("APP.Pomodoro.PersistentState.v1");

            IPomodoroModel model = new PomodoroModel();

            Assert.IsFalse(model.IsPinned.Value);
        }
    }
}
