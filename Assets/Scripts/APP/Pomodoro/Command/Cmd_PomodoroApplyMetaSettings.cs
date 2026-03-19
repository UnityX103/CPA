using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.Command
{
    /// <summary>
    /// 应用非计时类设置（自动跳顶开关、完成音效索引）
    /// </summary>
    public sealed class Cmd_PomodoroApplyMetaSettings : AbstractCommand
    {
        private readonly bool _autoJumpToTop;
        private readonly bool _autoStartBreak;
        private readonly int _completionClipIndex;

        public Cmd_PomodoroApplyMetaSettings(bool autoJumpToTop, bool autoStartBreak, int completionClipIndex)
        {
            _autoJumpToTop = autoJumpToTop;
            _autoStartBreak = autoStartBreak;
            _completionClipIndex = Mathf.Max(0, completionClipIndex);
        }

        protected override void OnExecute()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.AutoJumpToTopOnComplete.Value = _autoJumpToTop;
            model.AutoStartBreak.Value = _autoStartBreak;
            model.CompletionClipIndex.Value = _completionClipIndex;
        }
    }
}
