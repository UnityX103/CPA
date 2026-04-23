using APP.Pomodoro.Command;
using APP.Pomodoro.Model;
using QFramework;
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
        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 私有字段 ────────────────────────────────────────────
        private PomodoroSettingsPanelView _view;
        private IPomodoroModel _model;

        // 基线（与 Model 最近一次同步的值）
        private int  _baseFocusMin;
        private int  _baseBreakMin;
        private bool _baseHint;

        // 草稿（用户当前输入/拨动但尚未应用的值）
        private int  _draftFocusMin;
        private int  _draftBreakMin;
        private bool _draftHint;

        // ─── 公开查询 ────────────────────────────────────────────

        /// <summary>当前是否存在尚未"应用"的草稿改动</summary>
        public bool IsDirty =>
               _draftFocusMin != _baseFocusMin
            || _draftBreakMin != _baseBreakMin
            || _draftHint     != _baseHint;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 初始化面板。容器内已由 UnifiedSettingsPanelController 克隆好 UXML。
        /// </summary>
        public void Init(VisualElement container, IPomodoroModel model, GameObject lifecycleOwner)
        {
            _model = model;

            _view = new PomodoroSettingsPanelView(container);
            _view.OnHintToggleChanged += OnHintToggleChanged;
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
                _model.AutoJumpToTopOnComplete.Register(_ => RefreshFromModel())
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
                _draftHint,
                _model.AutoStartBreak.Value,
                _model.CompletionClipIndex.Value));

            _baseFocusMin = _draftFocusMin;
            _baseBreakMin = _draftBreakMin;
            _baseHint     = _draftHint;
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
            bool hintEnabled = _model.AutoJumpToTopOnComplete.Value;
            string soundName = "柔和铃声"; // 暂用固定文本

            // Model 是权威源，把基线和草稿都同步为 Model 当前值
            _baseFocusMin  = focusMin;
            _baseBreakMin  = breakMin;
            _baseHint      = hintEnabled;
            _draftFocusMin = focusMin;
            _draftBreakMin = breakMin;
            _draftHint     = hintEnabled;

            _view.Refresh(focusMin, breakMin, hintEnabled, soundName);
            _view.SetApplyVisible(false);
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnHintToggleChanged(bool enabled)
        {
            _draftHint = enabled;
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
    }
}
