using System;
using System.Collections.Generic;
using System.Reflection;
using APP.Pomodoro.Native;
using NUnit.Framework;

namespace APP.Pomodoro.Tests
{
    /// <summary>
    /// 验证视频文件选择桥接层在原生选择器前后正确包裹 topmost 守护，
    /// 并允许 EditMode 测试完全离线注入伪 picker。
    /// </summary>
    public sealed class NativeFilePickerTests
    {
        private const string TopmostGuardFactoryPropertyName = "TopmostGuardFactory";
        private const string PickerOverridePropertyName = "PickerOverride";

        [TearDown]
        public void TearDown()
        {
            SetTopmostGuardFactory(null);
            SetPickerOverride(null);
        }

        [Test]
        public void PickVideoFile_ExecutesPickerInsideTopmostGuard()
        {
            var events = new List<string>();

            SetTopmostGuardFactory(() =>
            {
                events.Add("enter");
                return new CallbackDisposable(() => events.Add("leave"));
            });

            SetPickerOverride(() =>
            {
                events.Add("pick");
                return "/tmp/focus-video.mov";
            });

            string selectedPath = NativeFilePicker.PickVideoFile();

            Assert.That(selectedPath, Is.EqualTo("/tmp/focus-video.mov"));
            CollectionAssert.AreEqual(new[] { "enter", "pick", "leave" }, events);
        }

        [Test]
        public void PickVideoFile_DisposesTopmostGuardWhenPickerThrows()
        {
            var events = new List<string>();

            SetTopmostGuardFactory(() =>
            {
                events.Add("enter");
                return new CallbackDisposable(() => events.Add("leave"));
            });

            SetPickerOverride(() =>
            {
                events.Add("pick");
                throw new InvalidOperationException("boom");
            });

            string selectedPath = null;

            Assert.DoesNotThrow(() => selectedPath = NativeFilePicker.PickVideoFile());
            Assert.That(selectedPath, Is.Null, "异常路径应继续保持失败返回 null 的契约");
            CollectionAssert.AreEqual(new[] { "enter", "pick", "leave" }, events);
        }

        [Test]
        public void PickVideoFile_AllowsNullTopmostGuardFactory()
        {
            SetTopmostGuardFactory(null);
            SetPickerOverride(() => "/tmp/break-video.mp4");

            string selectedPath = null;

            Assert.DoesNotThrow(() => selectedPath = NativeFilePicker.PickVideoFile());
            Assert.That(selectedPath, Is.EqualTo("/tmp/break-video.mp4"));
        }

        private static void SetPickerOverride(Func<string> picker)
        {
            PropertyInfo pickerOverrideProperty = typeof(NativeFilePicker)
                .GetProperty(PickerOverridePropertyName, BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(
                pickerOverrideProperty,
                Is.Not.Null,
                "NativeFilePicker 必须暴露 internal PickerOverride 供 EditMode 测试离线注入");

            pickerOverrideProperty.SetValue(null, picker);
        }

        private static void SetTopmostGuardFactory(Func<IDisposable> topmostGuardFactory)
        {
            PropertyInfo topmostGuardFactoryProperty = typeof(NativeFilePicker)
                .GetProperty(TopmostGuardFactoryPropertyName, BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                topmostGuardFactoryProperty,
                Is.Not.Null,
                "NativeFilePicker 必须暴露 TopmostGuardFactory 供窗口置顶守护注入");

            topmostGuardFactoryProperty.SetValue(null, topmostGuardFactory);
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private readonly Action _onDispose;
            private bool _disposed;

            public CallbackDisposable(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _onDispose?.Invoke();
            }
        }
    }
}
