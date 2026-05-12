using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CPA.Monitoring
{
    /// <summary>
    /// 系统级全局按键监听桥接（macOS NSEvent global+local monitor）。
    ///
    /// 用途：让 BindingKeyCounterSystem 在 Unity 窗口失焦时仍能收到按键事件——
    /// 透明桌宠 + 点击穿透下 Unity 几乎永远不是 active app，Unity Input.GetKeyDown
    /// 只在 active 时触发，所以按键计数功能此前完全失效。
    ///
    /// 协议：
    /// - <see cref="TryStart"/> 在 macOS 上调原生 KeyMonitor_Start；其它平台或权限不足
    ///   直接返回 false，调用方应当降级到只用 Unity Input（focused-only）。
    /// - <see cref="Poll"/> 一次调用排出累积事件；返回的每个 int 经
    ///   <see cref="TranslateMacKeyCode"/> 翻译成 UnityEngine.KeyCode 的 int。
    /// - 鼠标键由原生侧用 -1 / -2 / -3 编码，匹配 BindingKeyModel.MouseLeft/Right/Middle。
    ///
    /// 线程：Poll 在 Unity 主线程调用即可；原生侧用 NSLock 保护队列。
    /// </summary>
    public static class GlobalKeyMonitor
    {
        public const int MouseLeftSentinel   = -1;
        public const int MouseRightSentinel  = -2;
        public const int MouseMiddleSentinel = -3;

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string DllName = "AppMonitor";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int KeyMonitor_Start();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void KeyMonitor_Stop();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int KeyMonitor_IsRunning();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int KeyMonitor_Poll([Out] int[] buffer, int capacity);
#endif

        /// <summary>原生监听是否已启动。失败启动 / 非 macOS 平台均为 false。</summary>
        public static bool IsRunning
        {
            get
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                try { return KeyMonitor_IsRunning() != 0; }
                catch { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 启动全局监听。需要 Accessibility 权限（AppMonitor 启动时会触发系统弹窗）。
        /// 返回 false → 调用方必须降级到 Unity Input（仅在窗口聚焦时能数到按键）。
        /// </summary>
        public static bool TryStart()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try { return KeyMonitor_Start() != 0; }
            catch (DllNotFoundException ex)
            {
                Debug.LogWarning("[GlobalKeyMonitor] AppMonitor 原生库未找到：" + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GlobalKeyMonitor] 启动失败：" + ex.Message);
                return false;
            }
#else
            return false;
#endif
        }

        public static void Stop()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try { KeyMonitor_Stop(); }
            catch { /* swallow：Stop 只是清理，不应该抛 */ }
#endif
        }

        /// <summary>
        /// 排出原生侧自上次调用以来累积的所有事件，依次填入 <paramref name="buffer"/>。
        /// 返回实际写入数量；超出 buffer 容量的部分本次调用拿不到，下次再 drain。
        /// 返回的 int 是 macOS 原生编码（CGKeyCode 或 -1/-2/-3 鼠标 sentinel），
        /// 调用方通常再过 <see cref="TranslateMacKeyCode"/> 转 Unity KeyCode。
        /// </summary>
        public static int Poll(int[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return 0;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try { return KeyMonitor_Poll(buffer, buffer.Length); }
            catch { return 0; }
#else
            return 0;
#endif
        }

        /// <summary>
        /// 把原生侧编码翻译成 Unity KeyCode 的 int 值（直接和
        /// <see cref="UnityEngine.KeyCode"/> enum 兼容，可强转）。
        /// 鼠标 sentinel 原样透传（-1/-2/-3 与 BindingKeyModel 常量一致）。
        /// 不在映射表里的 CGKeyCode 返回 0（KeyCode.None），调用方应当 if (==0) skip。
        /// </summary>
        public static int TranslateMacKeyCode(int macCode)
        {
            // 鼠标 sentinel 直接透传，BindingKeyCounterSystem 的 switch 已能处理 -1/-2/-3
            if (macCode < 0) return macCode;

            if (MacToUnityKeyCode.TryGetValue(macCode, out KeyCode unity))
            {
                return (int)unity;
            }
            return 0;
        }

        // ─── CGKeyCode → UnityEngine.KeyCode 静态映射 ─────────────────────
        // 数值取自 macOS Carbon HIToolbox/Events.h kVK_* 常量。
        // 只覆盖普通按键 + 主区数字 + 功能键 + 方向键 + 编辑键，不覆盖：
        //   - 修饰键单独按下（NSEventTypeFlagsChanged，需要单独路径，本期暂不支持）
        //   - 小键盘数字（可补，但项目里没人绑定）
        //   - 媒体键 / 特殊扩展键
        // 缺失键返回 KeyCode.None，调用方丢弃。
        private static readonly Dictionary<int, KeyCode> MacToUnityKeyCode = new Dictionary<int, KeyCode>
        {
            // ── 字母 ──
            {0x00, KeyCode.A}, {0x01, KeyCode.S}, {0x02, KeyCode.D}, {0x03, KeyCode.F},
            {0x04, KeyCode.H}, {0x05, KeyCode.G}, {0x06, KeyCode.Z}, {0x07, KeyCode.X},
            {0x08, KeyCode.C}, {0x09, KeyCode.V}, {0x0B, KeyCode.B}, {0x0C, KeyCode.Q},
            {0x0D, KeyCode.W}, {0x0E, KeyCode.E}, {0x0F, KeyCode.R}, {0x10, KeyCode.Y},
            {0x11, KeyCode.T}, {0x1F, KeyCode.O}, {0x20, KeyCode.U}, {0x22, KeyCode.I},
            {0x23, KeyCode.P}, {0x25, KeyCode.L}, {0x26, KeyCode.J}, {0x28, KeyCode.K},
            {0x2D, KeyCode.N}, {0x2E, KeyCode.M},

            // ── 数字主区 ──
            {0x12, KeyCode.Alpha1}, {0x13, KeyCode.Alpha2}, {0x14, KeyCode.Alpha3},
            {0x15, KeyCode.Alpha4}, {0x17, KeyCode.Alpha5}, {0x16, KeyCode.Alpha6},
            {0x1A, KeyCode.Alpha7}, {0x1C, KeyCode.Alpha8}, {0x19, KeyCode.Alpha9},
            {0x1D, KeyCode.Alpha0},

            // ── 符号 ──
            {0x18, KeyCode.Equals}, {0x1B, KeyCode.Minus},
            {0x1E, KeyCode.RightBracket}, {0x21, KeyCode.LeftBracket},
            {0x27, KeyCode.Quote}, {0x29, KeyCode.Semicolon},
            {0x2A, KeyCode.Backslash}, {0x2B, KeyCode.Comma},
            {0x2C, KeyCode.Slash}, {0x2F, KeyCode.Period},
            {0x32, KeyCode.BackQuote},

            // ── 控制 / 编辑 ──
            {0x24, KeyCode.Return}, {0x30, KeyCode.Tab}, {0x31, KeyCode.Space},
            {0x33, KeyCode.Backspace}, {0x35, KeyCode.Escape},
            {0x75, KeyCode.Delete},  // forward delete
            {0x73, KeyCode.Home}, {0x77, KeyCode.End},
            {0x74, KeyCode.PageUp}, {0x79, KeyCode.PageDown},

            // ── 方向键 ──
            {0x7B, KeyCode.LeftArrow}, {0x7C, KeyCode.RightArrow},
            {0x7D, KeyCode.DownArrow}, {0x7E, KeyCode.UpArrow},

            // ── 功能键 ──
            {0x7A, KeyCode.F1}, {0x78, KeyCode.F2}, {0x63, KeyCode.F3}, {0x76, KeyCode.F4},
            {0x60, KeyCode.F5}, {0x61, KeyCode.F6}, {0x62, KeyCode.F7}, {0x64, KeyCode.F8},
            {0x65, KeyCode.F9}, {0x6D, KeyCode.F10}, {0x67, KeyCode.F11}, {0x6F, KeyCode.F12},

            // ── 小键盘 ──
            {0x52, KeyCode.Keypad0}, {0x53, KeyCode.Keypad1}, {0x54, KeyCode.Keypad2},
            {0x55, KeyCode.Keypad3}, {0x56, KeyCode.Keypad4}, {0x57, KeyCode.Keypad5},
            {0x58, KeyCode.Keypad6}, {0x59, KeyCode.Keypad7}, {0x5B, KeyCode.Keypad8},
            {0x5C, KeyCode.Keypad9},
            {0x41, KeyCode.KeypadPeriod},
            {0x43, KeyCode.KeypadMultiply},
            {0x45, KeyCode.KeypadPlus},
            {0x4E, KeyCode.KeypadMinus},
            {0x4B, KeyCode.KeypadDivide},
            {0x51, KeyCode.KeypadEquals},
            {0x4C, KeyCode.KeypadEnter},

            // ── 修饰键（NSEventTypeFlagsChanged 路径触发）──
            // Unity KeyCode 用 LeftShift / LeftControl 等区分 Left/Right，与 macOS 一致。
            {0x37, KeyCode.LeftCommand},   {0x36, KeyCode.RightCommand},
            {0x38, KeyCode.LeftShift},     {0x3C, KeyCode.RightShift},
            {0x3A, KeyCode.LeftAlt},       {0x3D, KeyCode.RightAlt},     // Option
            {0x3B, KeyCode.LeftControl},   {0x3E, KeyCode.RightControl},
            {0x39, KeyCode.CapsLock},
        };
    }
}
