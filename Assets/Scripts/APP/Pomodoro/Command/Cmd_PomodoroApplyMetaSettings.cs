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
        private readonly int _completionClipIndex;

        public Cmd_PomodoroApplyMetaSettings(bool autoJumpToTop, int completionClipIndex)
        {
            _autoJumpToTop = autoJumpToTop;
            _completionClipIndex = Mathf.Max(0, completionClipIndex);
        }

        protected override void OnExecute()
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            model.AutoJumpToTopOnComplete.Value = _autoJumpToTop;
            model.CompletionClipIndex.Value = _completionClipIndex;
        }
    }
}
