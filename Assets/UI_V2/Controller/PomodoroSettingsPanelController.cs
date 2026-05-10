using APP.Pomodoro.Command;
using APP.Pomodoro.Config;
using APP.Pomodoro.Model;
using QFramework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 番茄钟设置面板控制器（纯 C# 类，无 MonoBehaviour）。
    /// 接收 VisualElement 容器，管理番茄钟设置的 UI 绑定与 Model 订阅。
    /// 用户输入先进入私有草稿，点击"应用"按钮才发 Command 写回 Model。
    /// </summary>
    public sealed class PomodoroSettingsPanelController : IController
    {
        public static Func<string> VideoFilePicker;

        private const string SoundName = "柔和铃声";

        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 私有字段 ────────────────────────────────────────────
        private PomodoroSettingsPanelView _view;
        private IPomodoroModel _model;

        // 内置视频配置（Init 时加载一次缓存）
        private PomodoroBuiltInVideosConfig _builtInVideosConfig;
        private IReadOnlyList<string> _builtInDisplayNames = Array.Empty<string>();

        // 基线（与 Model 最近一次同步的值）
        private int _baseFocusMin;
        private int _baseBreakMin;
        private PomodoroEndActionMode _baseEndActionMode;
        private string _baseVideoPath = string.Empty;
        private int _baseVideoIndex;

        // 草稿（用户当前输入/拨动但尚未应用的值）
        private int _draftFocusMin;
        private int _draftBreakMin;
        private PomodoroEndActionMode _draftEndActionMode;
        private string _draftVideoPath = string.Empty;
        private int _draftVideoIndex;

        // ─── 公开查询 ────────────────────────────────────────────

        /// <summary>当前是否存在尚未"应用"的草稿改动</summary>
        public bool IsDirty =>
               _draftFocusMin != _baseFocusMin
            || _draftBreakMin != _baseBreakMin
            || _draftEndActionMode != _baseEndActionMode
            || !string.Equals(_draftVideoPath, _baseVideoPath, StringComparison.Ordinal)
            || _draftVideoIndex != _baseVideoIndex;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 初始化面板。容器内已由 UnifiedSettingsPanelController 克隆好 UXML。
        /// </summary>
        public void Init(VisualElement container, IPomodoroModel model, GameObject lifecycleOwner)
        {
            _model = model;

            // 加载内置视频配置（Resources/PomodoroBuiltInVideos.asset），缓存显示名列表
            _builtInVideosConfig = PomodoroBuiltInVideosConfig.LoadFromResources();
            _builtInDisplayNames = BuildBuiltInDisplayNames(_builtInVideosConfig);

            _view = new PomodoroSettingsPanelView(container);
            _view.OnEndActionModeSelected += OnEndActionModeSelected;
            _view.OnVideoSelectionChanged += OnVideoSelectionChanged;
            _view.OnVideoCustomRowClicked += OnVideoCustomRowClicked;
            _view.OnFocusMinutesChanged += OnFocusMinutesChanged;
            _view.OnBreakMinutesChanged += OnBreakMinutesChanged;
            _view.OnApplyClicked += TryApply;

            if (_model != null)
            {
                _model.FocusDurationSeconds.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                _model.BreakDurationSeconds.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                _model.CompletionClipIndex.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                _model.EndActionMode.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                _model.EndActionVideoPath.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                _model.EndActionVideoIndex.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            }

            RefreshFromModel();
        }

        // ─── 公开操作 ────────────────────────────────────────────

        /// <summary>
        /// 把当前草稿应用到 Model。若无改动则直接返回。
        /// 发 Cmd_PomodoroApplySettings(resetProgress:true) 让计时器立即按新时长重置。
        /// </summary>
        public void TryApply()
        {
            if (!IsDirty || _model == null)
            {
                return;
            }

            this.SendCommand(new Cmd_PomodoroApplySettings(
                _draftFocusMin,
                _draftBreakMin,
                _model.TotalRounds.Value,
                resetProgress: true));

            this.SendCommand(new Cmd_PomodoroApplyMetaSettings(
                _model.AutoJumpToTopOnComplete.Value,
                _model.AutoStartBreak.Value,
                _model.CompletionClipIndex.Value,
                _draftEndActionMode,
                _draftVideoPath ?? string.Empty,
                _draftVideoIndex));

            _baseFocusMin = _draftFocusMin;
            _baseBreakMin = _draftBreakMin;
            _baseEndActionMode = _draftEndActionMode;
            _baseVideoPath = _draftVideoPath ?? string.Empty;
            _baseVideoIndex = _draftVideoIndex;
            _view?.SetApplyVisible(false);
        }

        /// <summary>
        /// 强制 Commit 尚未失焦的 TextField，把当前文本同步到草稿。
        /// 关闭/切 tab 前调用，避免"光标还在输入框内"时的改动遗失。
        /// </summary>
        public void ForceCommitPendingEdits()
        {
            _view?.ForceCommitDrafts();
        }

        // ─── 数据刷新 ────────────────────────────────────────────

        public void RefreshFromModel()
        {
            if (_view == null || _model == null)
            {
                return;
            }

            int focusMin = _model.FocusDurationSeconds.Value / 60;
            int breakMin = _model.BreakDurationSeconds.Value / 60;
            PomodoroEndActionMode mode = _model.EndActionMode.Value;
            string videoPath = _model.EndActionVideoPath.Value ?? string.Empty;
            int videoIndex = _model.EndActionVideoIndex.Value;

            // Model 是权威源，把基线和草稿都同步为 Model 当前值
            _baseFocusMin = focusMin;
            _baseBreakMin = breakMin;
            _baseEndActionMode = mode;
            _baseVideoPath = videoPath;
            _baseVideoIndex = videoIndex;
            _draftFocusMin = focusMin;
            _draftBreakMin = breakMin;
            _draftEndActionMode = mode;
            _draftVideoPath = videoPath;
            _draftVideoIndex = videoIndex;

            _view.Refresh(focusMin, breakMin, SoundName, mode, videoIndex, _builtInDisplayNames, videoPath);
            _view.SetApplyVisible(false);
        }

        // ─── 事件回调 ────────────────────────────────────────────

        /// <summary>
        /// 计时结束 mode 下拉变化。View 端 choices 顺序与 PomodoroEndActionMode 枚举值对齐：
        /// 0=TopWindow, 1=PlayVideo —— 直接 cast 即可。
        /// </summary>
        private void OnEndActionModeSelected(int modeIndex)
        {
            if (!Enum.IsDefined(typeof(PomodoroEndActionMode), modeIndex))
            {
                return;
            }

            PomodoroEndActionMode newMode = (PomodoroEndActionMode)modeIndex;
            if (_draftEndActionMode == newMode)
            {
                return;
            }
            _draftEndActionMode = newMode;

            RefreshView();
            EvaluateDirty();
        }

        /// <summary>
        /// 视频选择下拉变化。View 已经把"末项=自定义"翻译成 videoIndex=-1，其余 0..N-1 是内置项。
        /// </summary>
        private void OnVideoSelectionChanged(int videoIndex)
        {
            int builtInCount = _builtInDisplayNames?.Count ?? 0;
            // 防御：videoIndex 必须是 -1 或 0..(builtInCount-1)
            if (videoIndex != -1 && (videoIndex < 0 || videoIndex >= builtInCount))
            {
                return;
            }

            if (_draftVideoIndex == videoIndex)
            {
                return;
            }
            _draftVideoIndex = videoIndex;

            RefreshView();
            EvaluateDirty();
        }

        private void OnVideoCustomRowClicked()
        {
            UnityEngine.Debug.Log($"[PomodoroSettingsPanel] OnVideoCustomRowClicked 进入。draftEndActionMode={_draftEndActionMode}, draftVideoIndex={_draftVideoIndex}, picker={(VideoFilePicker != null ? "non-null" : "null")}");

            if (_draftEndActionMode != PomodoroEndActionMode.PlayVideo || _draftVideoIndex != -1)
            {
                UnityEngine.Debug.LogWarning("[PomodoroSettingsPanel] OnVideoCustomRowClicked：当前不在 PlayVideo + 自定义 模式，忽略点击");
                return;
            }

            Func<string> picker = VideoFilePicker;
            if (picker == null)
            {
                UnityEngine.Debug.LogError("[PomodoroSettingsPanel] OnVideoCustomRowClicked：VideoFilePicker == null，Bootstrap 没跑或被覆盖成 null。");
                return;
            }

            string selectedPath;
            try
            {
                selectedPath = picker();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[PomodoroSettingsPanel] OnVideoCustomRowClicked：picker 抛异常：{ex}");
                return;
            }

            UnityEngine.Debug.Log($"[PomodoroSettingsPanel] picker 返回 selectedPath='{selectedPath ?? "<null>"}'");

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                UnityEngine.Debug.Log("[PomodoroSettingsPanel] selectedPath 为空（用户取消或失败），不写草稿");
                return;
            }

            _draftVideoPath = selectedPath;
            UnityEngine.Debug.Log($"[PomodoroSettingsPanel] 已写入 _draftVideoPath='{_draftVideoPath}'，刷新视图 + EvaluateDirty");
            RefreshView();
            EvaluateDirty();
        }

        private void OnFocusMinutesChanged(int focusMinutes)
        {
            _draftFocusMin = focusMinutes;
            EvaluateDirty();
        }

        private void OnBreakMinutesChanged(int breakMinutes)
        {
            _draftBreakMin = breakMinutes;
            EvaluateDirty();
        }

        private void EvaluateDirty()
        {
            _view?.SetApplyVisible(IsDirty);
        }

        private void RefreshView()
        {
            _view?.Refresh(
                _draftFocusMin,
                _draftBreakMin,
                SoundName,
                _draftEndActionMode,
                _draftVideoIndex,
                _builtInDisplayNames,
                _draftVideoPath);
        }

        private static IReadOnlyList<string> BuildBuiltInDisplayNames(PomodoroBuiltInVideosConfig config)
        {
            if (config == null || config.Entries == null || config.Entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] names = new string[config.Entries.Count];
            for (int i = 0; i < config.Entries.Count; i++)
            {
                PomodoroBuiltInVideosConfig.Entry entry = config.Entries[i];
                names[i] = entry == null || string.IsNullOrEmpty(entry.DisplayName)
                    ? $"内置视频 {i + 1}"
                    : entry.DisplayName;
            }
            return names;
        }
    }
}
