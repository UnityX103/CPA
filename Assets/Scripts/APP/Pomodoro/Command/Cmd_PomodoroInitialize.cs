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

            // 初始化窗口定位系统
            IWindowPositionSystem wps = this.GetSystem<IWindowPositionSystem>();
            wps.Initialize(_uwc, _config.FixedWindowHeight, _config.VerticalMargin);

            // 应用初始窗口位置
            wps.MoveTo(_config.DefaultWindowAnchor);
        }
    }
}
