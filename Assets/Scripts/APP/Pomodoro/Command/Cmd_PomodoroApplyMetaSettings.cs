using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 应用非计时类设置（自动跳顶开关、完成音效索引、计时结束提示动作 + 视频路径 + 视频索引）。
    /// 旧字段 <c>autoJumpToTop</c> 仍写入 Model（前端已无 UI 入口，保留持久化以便兼容旧存档）。
    /// </summary>
    public sealed class Cmd_PomodoroApplyMetaSettings : AbstractCommand
    {
        private readonly bool _autoJumpToTop;
        private readonly bool _autoStartBreak;
        private readonly int _completionClipIndex;
        private readonly PomodoroEndActionMode _endActionMode;
        private readonly string _endActionVideoPath;
        private readonly int _endActionVideoIndex;

        public Cmd_PomodoroApplyMetaSettings(
            bool autoJumpToTop,
            bool autoStartBreak,
            int completionClipIndex,
            PomodoroEndActionMode endActionMode,
            string endActionVideoPath,
            int endActionVideoIndex)
        {
            _autoJumpToTop = autoJumpToTop;
            _autoStartBreak = autoStartBreak;
            _completionClipIndex = Mathf.Max(0, completionClipIndex);
            _endActionMode = endActionMode;
            _endActionVideoPath = endActionVideoPath ?? string.Empty;
            // -1 表示"自定义"；其余非法值都收敛到 0
            _endActionVideoIndex = endActionVideoIndex < -1 ? 0 : endActionVideoIndex;
        }

        protected override void OnExecute()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.AutoJumpToTopOnComplete.Value = _autoJumpToTop;
            model.AutoStartBreak.Value = _autoStartBreak;
            model.CompletionClipIndex.Value = _completionClipIndex;
            model.EndActionMode.Value = _endActionMode;
            model.EndActionVideoPath.Value = _endActionVideoPath;
            model.EndActionVideoIndex.Value = _endActionVideoIndex;
        }
    }
}
