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

            IPomodoroModel model = this.GetModel<IPomodoroModel>();

            if (_config != null)
            {
                // 写入 Model 默认配置
                model.FocusDurationSeconds.Value = _config.DefaultFocusMinutes * 60;
                model.BreakDurationSeconds.Value = _config.DefaultBreakMinutes * 60;
                model.TotalRounds.Value = _config.DefaultRounds;
                model.RemainingSeconds.Value = _config.DefaultFocusMinutes * 60;
                model.WindowAnchor.Value = _config.DefaultWindowAnchor;
                model.AutoJumpToTopOnComplete.Value = _config.DefaultAutoJumpToTopOnComplete;
                model.AutoStartBreak.Value = _config.DefaultAutoStartBreak;
                model.CompletionClipIndex.Value = _config.DefaultCompletionClipIndex;
                model.TargetMonitorIndex.Value = 0;
            }
            else
            {
                Debug.LogWarning("[PomodoroInitialize] PomodoroConfig 未赋值，使用 Model 默认值。");
            }

            // 若存在持久化状态，用持久化覆盖默认值
            if (PomodoroPersistence.TryLoad(model))
            {
                Debug.Log("[PomodoroInitialize] 已加载持久化状态。");
            }

            // 按恢复后的偏好应用显示器/置顶/锚点
            wps.MoveToMonitor(model.TargetMonitorIndex.Value);
            wps.SetTopmost(model.IsTopmost.Value);
            wps.MoveTo(model.WindowAnchor.Value);
        }
    }
}
