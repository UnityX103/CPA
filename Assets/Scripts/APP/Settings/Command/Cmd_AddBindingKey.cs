using APP.Settings.Model;
using QFramework;

namespace APP.Settings.Command
{
    /// <summary>
    /// 新增一个默认绑定 entry（"鼠标左键"，per-entry Enabled=true）。
    /// Entry 一进 list，DeskWindowController 就会保证单一 InputCounterPanel 存在并在 pill-list 里克隆出对应 pill；
    /// 计数仍受全局 BindingKeyModel.Enabled 门槛控制——全局关时所有 entry 都不 tick。
    /// </summary>
    public sealed class Cmd_AddBindingKey : AbstractCommand<string>
    {
        protected override string OnExecute()
        {
            return this.GetModel<IBindingKeyModel>().AddEntry();
        }
    }
}
