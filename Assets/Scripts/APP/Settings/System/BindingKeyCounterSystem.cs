using System.Collections.Generic;
using APP.Settings.Model;
using CPA.Monitoring;
using QFramework;
using UnityEngine;

namespace APP.Settings.System
{
    /// <summary>
    /// 多绑定按键计数系统：每帧遍历所有 entry，对每个 Enabled=true 的条目
    /// 检查命中即 IncrementEntry。
    ///
    /// 双轨入数（fix for 失焦失效 bug）：
    /// 1) <see cref="GlobalKeyMonitor"/>（macOS NSEvent 全局监听）—— Unity 窗口失焦
    ///    时仍能拿到按键事件，覆盖透明桌宠 + 点击穿透下 99% 的实际使用场景。
    /// 2) Unity Input.GetKeyDown 兜底 —— 当 GlobalKeyMonitor 启动失败（无 Accessibility
    ///    权限 / 非 macOS / 库缺失）时退化到只读 Unity Input；以及覆盖某些 macOS
    ///    版本下 NSEvent localMonitor 可能漏的边角事件。
    ///
    /// 防双计数：Application.isFocused 时只用 Input 路径，否则只用 GlobalKeyMonitor，
    /// 避开 NSEvent localMonitor + Unity Input 同帧重复触发。
    /// </summary>
    public sealed class BindingKeyCounterSystem : AbstractSystem, IBindingKeyCounterSystem
    {
        // 每帧最多排出 64 个按键事件——一次 drain 排空，防失控积累。
        // 真实用户即使狂按 30Hz × 失焦 2 秒也才 60 个，64 完全够。
        private const int PollBufferSize = 64;
        private readonly int[] _pollBuffer = new int[PollBufferSize];

        // 本 Tick 从 GlobalKeyMonitor 排出的"KeyCode → 出现次数"。
        // 用 count（不是 bool）准确反映用户狂按节奏：失焦下 60ms 内按了 3 次 Space，
        // 应当 +3 而不是 +1。Unity Input.GetKeyDown 本身只能一帧一次，无此问题。
        private readonly Dictionary<int, int> _globalHitCounts = new Dictionary<int, int>();

        // GlobalKeyMonitor.TryStart 在 OnInit 调用一次；失败仅记日志、不阻塞 Tick。
        private bool _globalMonitorRunning;

        protected override void OnInit()
        {
            _globalMonitorRunning = GlobalKeyMonitor.TryStart();
            Debug.Log("[BindingKeyCounterSystem] GlobalKeyMonitor "
                      + (_globalMonitorRunning ? "已启动（失焦也能计数）" : "未启动（仅聚焦时计数；可能缺辅助功能权限）"));
        }

        protected override void OnDeinit()
        {
            if (_globalMonitorRunning)
            {
                GlobalKeyMonitor.Stop();
                _globalMonitorRunning = false;
            }
        }

        public void Tick(float deltaTime)
        {
            var m = this.GetModel<IBindingKeyModel>();
            if (!m.Enabled.Value) return;

            // 监听设置态优先：不计入数（避免点击某 row 时把"列出 listener"也算作一次输入）
            if (!string.IsNullOrEmpty(m.ListeningKeyId.Value)) return;

            _globalHitCounts.Clear();

            // ── 计数策略：GlobalKeyMonitor 跑 → 完全走队列；否则 fallback Unity Input ──
            // codex review D 点教训：之前按 Application.isFocused 切换"走队列 vs 走 Input"会有 race ——
            // focused 状态相对 NSEvent 事件分发滞后一帧时，焦点切换的那个瞬间按键会同时被两边漏掉。
            // 现在让 NSEvent localMonitor + globalMonitor 全权负责事件源（focused 时 localMonitor
            // 也会捕获到 Unity 的按键），Tick 直接消费队列即可。
            // - 跑通：NSEvent local+global 是同一原生事件分发的两个钩子，互斥不会双触发
            // - Unity Input 路径只在 monitor 启动失败（无权限 / 非 macOS）时使用，焦点 gate 由 Unity 自己负责
            if (_globalMonitorRunning)
            {
                int drained = GlobalKeyMonitor.Poll(_pollBuffer);
                for (int i = 0; i < drained; i++)
                {
                    int unityCode = GlobalKeyMonitor.TranslateMacKeyCode(_pollBuffer[i]);
                    if (unityCode == 0) continue; // CGKeyCode 不在映射表，丢弃
                    if (_globalHitCounts.TryGetValue(unityCode, out int existing))
                    {
                        _globalHitCounts[unityCode] = existing + 1;
                    }
                    else
                    {
                        _globalHitCounts[unityCode] = 1;
                    }
                }
            }

            // 对每个 enabled entry 累加本帧命中次数；
            // 两个 entry 绑定同一 keyCode 是合法的，循环里各算各的，没有去重。
            var entries = m.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!e.Enabled) continue;

                int hitCount = 0;
                if (_globalHitCounts.TryGetValue(e.KeyCode, out int globalC))
                {
                    hitCount = globalC;
                }
                else if (!_globalMonitorRunning && CheckUnityInputKeyDown(e.KeyCode))
                {
                    // fallback：globalMonitor 没起来才用 Unity Input；Input.GetKeyDown 每帧至多 +1
                    hitCount = 1;
                }

                for (int k = 0; k < hitCount; k++)
                {
                    m.IncrementEntry(e.Id);
                }
            }
        }

        private static bool CheckUnityInputKeyDown(int code)
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
