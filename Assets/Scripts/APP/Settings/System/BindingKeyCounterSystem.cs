using APP.Settings.Model;
using QFramework;
using UnityEngine;

namespace APP.Settings.System
{
    /// <summary>
    /// 多绑定按键计数系统：每帧遍历所有 entry，对每个 Enabled=true 的条目
    /// 检查 Input.GetKeyDown / GetMouseButtonDown，命中即 IncrementEntry。
    ///
    /// 旧版 Unity Input 仅在游戏窗口聚焦时能捕获；透明窗口点击穿透下的
    /// 全局监听需要 macOS 原生 CGEventTap（follow-up）。
    /// </summary>
    public sealed class BindingKeyCounterSystem : AbstractSystem, IBindingKeyCounterSystem
    {
        protected override void OnInit() { }

        public void Tick(float deltaTime)
        {
            var m = this.GetModel<IBindingKeyModel>();
            if (!m.Enabled.Value) return;

            // 监听设置态优先：不计入数（避免点击某 row 时把"列出 listener"也算作一次输入）
            if (!string.IsNullOrEmpty(m.ListeningKeyId.Value)) return;

            // 注意：Entries 是 IReadOnlyList<BindingKeyEntry>；ToArray 副本避免遍历过程中可能的修改
            var entries = m.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.Enabled) continue;
                if (CheckKeyDown(e.KeyCode))
                {
                    m.IncrementEntry(e.Id);
                }
            }
        }

        private static bool CheckKeyDown(int code)
        {
            switch (code)
            {
                case BindingKeyModel.MouseLeft:   return Input.GetMouseButtonDown(0);
                case BindingKeyModel.MouseRight:  return Input.GetMouseButtonDown(1);
                case BindingKeyModel.MouseMiddle: return Input.GetMouseButtonDown(2);
                default:
                    if (code > 0 && code < (int)KeyCode.JoystickButton0)
                    {
                        return Input.GetKeyDown((KeyCode)code);
                    }
                    return false;
            }
        }
    }
}
