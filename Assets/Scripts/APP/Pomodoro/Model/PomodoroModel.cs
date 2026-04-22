using System;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Model
{
    public sealed class PomodoroModel : AbstractModel, IPomodoroModel
    {
        public BindableProperty<int> FocusDurationSeconds { get; } = new BindableProperty<int>(25 * 60);
        public BindableProperty<int> BreakDurationSeconds { get; } = new BindableProperty<int>(5 * 60);
        public BindableProperty<int> TotalRounds { get; } = new BindableProperty<int>(4);
        public BindableProperty<int> CurrentRound { get; } = new BindableProperty<int>(1);
        public BindableProperty<int> RemainingSeconds { get; } = new BindableProperty<int>(25 * 60);
        public BindableProperty<PomodoroPhase> CurrentPhase { get; } =
            new BindableProperty<PomodoroPhase>(PomodoroPhase.Focus);
        public BindableProperty<bool> IsRunning { get; } = new BindableProperty<bool>(false);
        public BindableProperty<bool> IsTopmost { get; } = new BindableProperty<bool>(false);
        public BindableProperty<PomodoroWindowAnchor> WindowAnchor { get; } =
            new BindableProperty<PomodoroWindowAnchor>(PomodoroWindowAnchor.Bottom);
        public BindableProperty<bool> AutoJumpToTopOnComplete { get; } = new BindableProperty<bool>(true);
        public BindableProperty<bool> AutoStartBreak { get; } = new BindableProperty<bool>(true);
        public BindableProperty<int> TargetMonitorIndex { get; } = new BindableProperty<int>(0);
        public BindableProperty<int> CompletionClipIndex { get; } = new BindableProperty<int>(0);
        public BindableProperty<Vector2> PomodoroPanelPosition { get; }
            = new BindableProperty<Vector2>(new Vector2(float.NegativeInfinity, float.NegativeInfinity));

        protected override void OnInit()
        {
            // 初始剩余时间 = 专注时长
            RemainingSeconds.Value = FocusDurationSeconds.Value;
        }
    }

    [Serializable]
    internal sealed class PomodoroPersistentState
    {
        public int FocusDurationSeconds = 25 * 60;
        public int BreakDurationSeconds = 5 * 60;
        public int TotalRounds = 4;
        public int CurrentRound = 1;
        public int RemainingSeconds = 25 * 60;
        public int CurrentPhase;
        public bool IsRunning;
        public bool IsTopmost;
        public int WindowAnchor = (int)PomodoroWindowAnchor.Bottom;
        public bool AutoJumpToTopOnComplete = true;
        public bool AutoStartBreak = true;
        public int TargetMonitorIndex;
        public int CompletionClipIndex;
    }

    public static class PomodoroPersistence
    {
        private const string SaveKey = "APP.Pomodoro.PersistentState.v1";
        private static string _cachedJson;

        public static bool TryLoad(IPomodoroModel model)
        {
            if (model == null || !PlayerPrefs.HasKey(SaveKey))
            {
                return false;
            }

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                PlayerPrefs.DeleteKey(SaveKey);
                return false;
            }

            PomodoroPersistentState state;
            try
            {
                state = JsonUtility.FromJson<PomodoroPersistentState>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PomodoroPersistence] 读取持久化数据失败：{ex.Message}");
                PlayerPrefs.DeleteKey(SaveKey);
                _cachedJson = null;
                return false;
            }

            if (state == null)
            {
                PlayerPrefs.DeleteKey(SaveKey);
                return false;
            }

            // 兼容旧版本持久化数据：缺少字段时保持默认自动开始休息
            if (!json.Contains("\"AutoStartBreak\""))
            {
                state.AutoStartBreak = true;
            }

            ApplyState(model, state);
            _cachedJson = json;
            return true;
        }

        public static void Save(IPomodoroModel model, bool flushToDisk)
        {
            if (model == null)
            {
                return;
            }

            var state = new PomodoroPersistentState
            {
                FocusDurationSeconds = Mathf.Max(60, model.FocusDurationSeconds.Value),
                BreakDurationSeconds = Mathf.Max(0, model.BreakDurationSeconds.Value),
                TotalRounds = Mathf.Max(1, model.TotalRounds.Value),
                CurrentRound = Mathf.Max(1, model.CurrentRound.Value),
                RemainingSeconds = Mathf.Max(0, model.RemainingSeconds.Value),
                CurrentPhase = (int)model.CurrentPhase.Value,
                IsRunning = model.IsRunning.Value,
                IsTopmost = model.IsTopmost.Value,
                WindowAnchor = (int)model.WindowAnchor.Value,
                AutoJumpToTopOnComplete = model.AutoJumpToTopOnComplete.Value,
                AutoStartBreak = model.AutoStartBreak.Value,
                TargetMonitorIndex = Mathf.Max(0, model.TargetMonitorIndex.Value),
                CompletionClipIndex = Mathf.Max(0, model.CompletionClipIndex.Value),
            };

            string json = JsonUtility.ToJson(state);
            if (!string.Equals(json, _cachedJson, StringComparison.Ordinal))
            {
                PlayerPrefs.SetString(SaveKey, json);
                _cachedJson = json;
            }

            if (flushToDisk)
            {
                PlayerPrefs.Save();
            }
        }

        private static void ApplyState(IPomodoroModel model, PomodoroPersistentState state)
        {
            int focusSeconds = Mathf.Max(60, state.FocusDurationSeconds);
            int breakSeconds = Mathf.Max(0, state.BreakDurationSeconds);
            int totalRounds = Mathf.Max(1, state.TotalRounds);
            PomodoroPhase phase = ParsePhase(state.CurrentPhase);
            int currentRound = Mathf.Clamp(state.CurrentRound, 1, totalRounds);
            int remainingSeconds = ResolveRemainingSeconds(state.RemainingSeconds, phase, focusSeconds, breakSeconds);
            bool isRunning = phase == PomodoroPhase.Completed ? false : state.IsRunning;
            PomodoroWindowAnchor anchor = ParseAnchor(state.WindowAnchor);

            model.FocusDurationSeconds.Value = focusSeconds;
            model.BreakDurationSeconds.Value = breakSeconds;
            model.TotalRounds.Value = totalRounds;
            model.CurrentRound.Value = phase == PomodoroPhase.Completed ? totalRounds : currentRound;
            model.RemainingSeconds.Value = remainingSeconds;
            model.CurrentPhase.Value = phase;
            model.IsRunning.Value = isRunning;
            model.IsTopmost.Value = state.IsTopmost;
            model.WindowAnchor.Value = anchor;
            model.AutoJumpToTopOnComplete.Value = state.AutoJumpToTopOnComplete;
            model.AutoStartBreak.Value = state.AutoStartBreak;
            model.TargetMonitorIndex.Value = Mathf.Max(0, state.TargetMonitorIndex);
            model.CompletionClipIndex.Value = Mathf.Max(0, state.CompletionClipIndex);
        }

        private static int ResolveRemainingSeconds(
            int savedRemainingSeconds,
            PomodoroPhase phase,
            int focusSeconds,
            int breakSeconds)
        {
            return phase switch
            {
                PomodoroPhase.Break => Mathf.Clamp(savedRemainingSeconds, 0, breakSeconds),
                PomodoroPhase.Completed => 0,
                _ => Mathf.Clamp(savedRemainingSeconds, 0, focusSeconds),
            };
        }

        private static PomodoroPhase ParsePhase(int value)
        {
            return Enum.IsDefined(typeof(PomodoroPhase), value)
                ? (PomodoroPhase)value
                : PomodoroPhase.Focus;
        }

        private static PomodoroWindowAnchor ParseAnchor(int value)
        {
            return Enum.IsDefined(typeof(PomodoroWindowAnchor), value)
                ? (PomodoroWindowAnchor)value
                : PomodoroWindowAnchor.Bottom;
        }
    }
}
