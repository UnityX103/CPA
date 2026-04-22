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

            var target = new Vector2(100f, 200f);
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
            save.PomodoroPanelPosition.Value = new Vector2(512.5f, 768.25f);

            PomodoroPersistence.Save(save, flushToDisk: true);

            var load = new PomodoroModel();
            ((IModel)load).Init();
            bool ok = PomodoroPersistence.TryLoad(load);

            Assert.That(ok, Is.True, "TryLoad 应成功");
            Assert.That(load.PomodoroPanelPosition.Value.x, Is.EqualTo(512.5f).Within(0.001f));
            Assert.That(load.PomodoroPanelPosition.Value.y, Is.EqualTo(768.25f).Within(0.001f));

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
            APP.Pomodoro.GameApp.Interface.SendCommand(
                new APP.Pomodoro.Command.Cmd_SetPomodoroPanelPosition(new Vector2(7f, 9f)));
            Assert.That(model.PomodoroPanelPosition.Value, Is.EqualTo(new Vector2(7f, 9f)));
        }
    }
}
