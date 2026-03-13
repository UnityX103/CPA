using APP.Pomodoro.Config;
using APP.Pomodoro.Model;
using APP.Pomodoro.System;
using Kirurobo;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 初始化命令：从 PomodoroConfig 读取默认值写入 Model，并初始化系统依赖
    /// 在 Controller.Start() 最先调用
    /// </summary>
    public sealed class Cmd_PomodoroInitialize : AbstractCommand
    {
        private readonly PomodoroConfig _config;
        private readonly UniWindowController _uwc;

        public Cmd_PomodoroInitialize(PomodoroConfig config, UniWindowController uwc)
        {
            _config = config;
            _uwc = uwc;
        }

        protected override void OnExecute()
        {
            // 初始化窗口定位系统（不依赖 config）
            IWindowPositionSystem wps = this.GetSystem<IWindowPositionSystem>();
            float windowHeight = _config?.FixedWindowHeight ?? 120f;
            float verticalMargin = _config?.VerticalMargin ?? 4f;
            wps.Initialize(_uwc, windowHeight, verticalMargin);

            if (_config == null)
            {
                Debug.LogWarning("[PomodoroInitialize] PomodoroConfig 未赋值，使用 Model 默认值。");
                return;
            }

            // 写入 Model 默认配置
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.FocusDurationSeconds.Value = _config.DefaultFocusMinutes * 60;
            model.BreakDurationSeconds.Value = _config.DefaultBreakMinutes * 60;
            model.TotalRounds.Value = _config.DefaultRounds;
            model.RemainingSeconds.Value = _config.DefaultFocusMinutes * 60;
            model.WindowAnchor.Value = _config.DefaultWindowAnchor;
            model.AutoJumpToTopOnComplete.Value = _config.DefaultAutoJumpToTopOnComplete;
            model.CompletionClipIndex.Value = _config.DefaultCompletionClipIndex;
            model.TargetMonitorIndex.Value = 0;

            // 应用初始锚点（更新 Model，CSS class 由 Controller 响应）
            wps.MoveTo(_config.DefaultWindowAnchor);
        }
    }
}
