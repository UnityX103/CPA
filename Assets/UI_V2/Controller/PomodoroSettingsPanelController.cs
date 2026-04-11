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
    /// </summary>
    public sealed class PomodoroSettingsPanelController : IController
    {
        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 私有字段 ────────────────────────────────────────────
        private PomodoroSettingsPanelView _view;
        private IPomodoroModel _model;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 初始化面板。容器内已由 UnifiedSettingsPanelController 克隆好 UXML。
        /// </summary>
        public void Init(VisualElement container, IPomodoroModel model, GameObject lifecycleOwner)
        {
            _model = model;

            _view = new PomodoroSettingsPanelView(container);
            _view.OnEnabledChanged += OnEnabledToggleChanged;
            _view.OnHintToggleChanged += OnHintToggleChanged;

            if (_model != null)
            {
                _model.FocusDurationSeconds.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                _model.BreakDurationSeconds.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
                _model.CompletionClipIndex.Register(_ => RefreshFromModel())
                    .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            }

            RefreshFromModel();
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
            bool isEnabled = true; // 番茄钟始终启用
            bool hintEnabled = _model.AutoJumpToTopOnComplete.Value;
            string soundName = "柔和铃声"; // 暂用固定文本

            _view.Refresh(focusMin, breakMin, isEnabled, hintEnabled, soundName);
        }

        // ─── 事件回调 ────────────────────────────────────────────

        private void OnEnabledToggleChanged(bool enabled)
        {
            // 预留：未来可发 Command 切换番茄钟启用状态
        }

        private void OnHintToggleChanged(bool enabled)
        {
            if (_model == null)
            {
                return;
            }

            this.SendCommand(new Cmd_PomodoroApplyMetaSettings(
                enabled,
                _model.AutoStartBreak.Value,
                _model.CompletionClipIndex.Value));
        }
    }
}
