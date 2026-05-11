using QFramework;

namespace APP.Settings.System
{
    /// <summary>
    /// 按键计数系统：每帧检测 BoundKey 是否按下，命中即派 Cmd_IncrementBindingCount。
    /// 由 DeskWindowController.Update 驱动 Tick(Time.deltaTime)。
    /// </summary>
    public interface IBindingKeyCounterSystem : ISystem
    {
        /// <summary>每帧调用一次。Enabled=false 或 Listening=true 时直接返回。</summary>
        void Tick(float deltaTime);
    }
}
