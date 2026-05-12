using CPA.Monitoring;
using NUnit.Framework;
using UnityEngine;

namespace APP.Settings.Tests
{
    /// <summary>
    /// 锁定 <see cref="GlobalKeyMonitor.TranslateMacKeyCode"/> 的 CGKeyCode → Unity KeyCode 映射。
    /// BindingKey 数据库里存的是 Unity KeyCode 数值，原生 NSEvent 给的是 macOS CGKeyCode；
    /// 映射出错 → 用户按 Space 计成 None / 按 A 计成 Z 等，必须用单测锁死。
    /// 不调用原生库，纯静态映射验证，所以放在 EditMode。
    /// </summary>
    [TestFixture]
    public sealed class GlobalKeyMonitorMappingTests
    {
        [TestCase(0x31, KeyCode.Space, TestName = "Space (0x31)")]
        [TestCase(0x24, KeyCode.Return, TestName = "Return (0x24)")]
        [TestCase(0x35, KeyCode.Escape, TestName = "Escape (0x35)")]
        [TestCase(0x30, KeyCode.Tab, TestName = "Tab (0x30)")]
        [TestCase(0x33, KeyCode.Backspace, TestName = "Backspace (0x33)")]
        [TestCase(0x75, KeyCode.Delete, TestName = "Forward Delete (0x75)")]
        [TestCase(0x00, KeyCode.A, TestName = "Letter A (0x00)")]
        [TestCase(0x06, KeyCode.Z, TestName = "Letter Z (0x06)")]
        [TestCase(0x12, KeyCode.Alpha1, TestName = "Number 1 (0x12)")]
        [TestCase(0x1D, KeyCode.Alpha0, TestName = "Number 0 (0x1D)")]
        [TestCase(0x7A, KeyCode.F1, TestName = "F1 (0x7A)")]
        [TestCase(0x6F, KeyCode.F12, TestName = "F12 (0x6F)")]
        [TestCase(0x7B, KeyCode.LeftArrow, TestName = "LeftArrow (0x7B)")]
        [TestCase(0x7E, KeyCode.UpArrow, TestName = "UpArrow (0x7E)")]
        // ── 修饰键（FlagsChanged 上升沿入队，codex F 点）──
        [TestCase(0x38, KeyCode.LeftShift,    TestName = "LeftShift (0x38)")]
        [TestCase(0x3C, KeyCode.RightShift,   TestName = "RightShift (0x3C)")]
        [TestCase(0x37, KeyCode.LeftCommand,  TestName = "LeftCommand (0x37)")]
        [TestCase(0x36, KeyCode.RightCommand, TestName = "RightCommand (0x36)")]
        [TestCase(0x3A, KeyCode.LeftAlt,      TestName = "LeftOption (0x3A)")]
        [TestCase(0x3D, KeyCode.RightAlt,     TestName = "RightOption (0x3D)")]
        [TestCase(0x3B, KeyCode.LeftControl,  TestName = "LeftControl (0x3B)")]
        [TestCase(0x3E, KeyCode.RightControl, TestName = "RightControl (0x3E)")]
        [TestCase(0x39, KeyCode.CapsLock,     TestName = "CapsLock (0x39)")]
        // ── 小键盘（codex F 点）──
        [TestCase(0x52, KeyCode.Keypad0, TestName = "Keypad0 (0x52)")]
        [TestCase(0x53, KeyCode.Keypad1, TestName = "Keypad1 (0x53)")]
        [TestCase(0x5C, KeyCode.Keypad9, TestName = "Keypad9 (0x5C)")]
        [TestCase(0x45, KeyCode.KeypadPlus, TestName = "Keypad+ (0x45)")]
        [TestCase(0x4E, KeyCode.KeypadMinus, TestName = "Keypad- (0x4E)")]
        public void TranslateMacKeyCode_KnownKeys_MapToUnityKeyCode(int macCode, KeyCode expectedUnity)
        {
            int actual = GlobalKeyMonitor.TranslateMacKeyCode(macCode);
            Assert.That(actual, Is.EqualTo((int)expectedUnity),
                $"CGKeyCode 0x{macCode:X2} 应映射为 {expectedUnity}({(int)expectedUnity})，实测 {(KeyCode)actual}({actual})");
        }

        [TestCase(-1, TestName = "MouseLeft 透传 (-1)")]
        [TestCase(-2, TestName = "MouseRight 透传 (-2)")]
        [TestCase(-3, TestName = "MouseMiddle 透传 (-3)")]
        public void TranslateMacKeyCode_MouseSentinels_PassThrough(int sentinel)
        {
            // BindingKeyModel.MouseLeft / Right / Middle 用 -1/-2/-3 编码，
            // GlobalKeyMonitor 必须原样透传不能误映射成 KeyCode.None。
            int actual = GlobalKeyMonitor.TranslateMacKeyCode(sentinel);
            Assert.That(actual, Is.EqualTo(sentinel),
                $"鼠标 sentinel {sentinel} 应透传，实测 {actual}");
        }

        [TestCase(0xFF)]   // 不在映射表
        [TestCase(0xAA)]
        [TestCase(127)]
        public void TranslateMacKeyCode_UnknownCode_ReturnsZero(int macCode)
        {
            // 未覆盖键（如媒体键、Fn+xxx 组合）应返回 0（=KeyCode.None），
            // 调用方据此 skip 不入数。
            int actual = GlobalKeyMonitor.TranslateMacKeyCode(macCode);
            Assert.That(actual, Is.EqualTo(0),
                $"未知 CGKeyCode 0x{macCode:X2} 应返回 0，实测 {actual}");
        }

        [Test]
        public void MouseSentinelConstants_MatchBindingKeyModelValues()
        {
            // 与 BindingKeyModel.MouseLeft/Right/Middle 数值一致是协议契约：
            // 原生侧编码 -1 → C# 透传 -1 → BindingKeyCounterSystem 用 BindingKeyModel.MouseLeft 比较。
            Assert.That(GlobalKeyMonitor.MouseLeftSentinel,   Is.EqualTo(APP.Settings.Model.BindingKeyModel.MouseLeft));
            Assert.That(GlobalKeyMonitor.MouseRightSentinel,  Is.EqualTo(APP.Settings.Model.BindingKeyModel.MouseRight));
            Assert.That(GlobalKeyMonitor.MouseMiddleSentinel, Is.EqualTo(APP.Settings.Model.BindingKeyModel.MouseMiddle));
        }
    }
}
