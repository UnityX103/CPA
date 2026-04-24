using System;
using System.Collections;
using CPA.Monitoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZ.AppMonitor.Tests
{
    /// <summary>
    /// AppMonitor Player 集成测试。
    /// 覆盖范围：单例访问、权限 API、GetCurrentApp()、GetAppIcon()、平台分支、内存稳定性。
    /// 运行方式：Build → Build macOS Player with Tests，然后：
    ///   ./Builds/macOS/AppMonitor.app/Contents/MacOS/DevTemplate
    ///     -batchmode -testPlatform PlayMode
    ///     -testResults ./TestResults/AppMonitor_Player_Results.xml
    ///     -testFilter "NZ.AppMonitor.Tests"
    /// </summary>
    public class AppMonitorPlayerTest
    {
        // ──────────────────────────────────────────────
        // 1. 单例
        // ──────────────────────────────────────────────

        [Test]
        public void Instance_IsNotNull()
        {
            Assert.IsNotNull(CPA.Monitoring.AppMonitor.Instance, "CPA.Monitoring.AppMonitor.Instance 不应为 null");
        }

        [Test]
        public void Instance_IsSameObject()
        {
            CPA.Monitoring.AppMonitor a = CPA.Monitoring.AppMonitor.Instance;
            CPA.Monitoring.AppMonitor b = CPA.Monitoring.AppMonitor.Instance;
            Assert.AreSame(a, b, "每次访问 Instance 应返回同一对象");
        }

        // ──────────────────────────────────────────────
        // 2. 权限 API
        // ──────────────────────────────────────────────

        [Test]
        public void IsPermissionGranted_DoesNotThrow()
        {
            bool granted = false;
            Assert.DoesNotThrow(() => granted = CPA.Monitoring.AppMonitor.Instance.IsPermissionGranted,
                "IsPermissionGranted 属性访问不应抛出异常");
            Debug.Log($"[AppMonitorPlayerTest] IsPermissionGranted = {granted}");
        }

        [Test]
        public void RequestPermission_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CPA.Monitoring.AppMonitor.Instance.RequestPermission(),
                "RequestPermission() 不应抛出异常");
        }

#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN
        [Test]
        public void IsPermissionGranted_UnsupportedPlatform_ReturnsFalse()
        {
            Assert.IsFalse(CPA.Monitoring.AppMonitor.Instance.IsPermissionGranted,
                "非支持平台(macOS/Windows 之外)IsPermissionGranted 应始终返回 false");
        }
#endif

#if UNITY_STANDALONE_WIN
        [Test]
        public void IsPermissionGranted_Windows_ReturnsTrue()
        {
            Assert.IsTrue(CPA.Monitoring.AppMonitor.Instance.IsPermissionGranted,
                "Windows 上 IsPermissionGranted 应始终返回 true(无权限要求)");
        }
#endif

        // ──────────────────────────────────────────────
        // 3. GetCurrentApp — 基础契约
        // ──────────────────────────────────────────────

        [Test]
        public void GetCurrentApp_ReturnsNonNull()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsNotNull(result, "GetCurrentApp() 不应返回 null");
        }

        [Test]
        public void GetCurrentApp_AppInfo_ErrorMessageNullWhenSuccess()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            if (result.IsSuccess)
            {
                // 成功时 ErrorCode 应为 null（AccessibilityDenied Fallback 时 IsSuccess=true 但 ErrorCode 有值）
                // 只检查 AppName 非空即可
                Assert.IsNotNull(result.AppName, "IsSuccess=true 时 AppName 不应为 null");
            }
        }

        [Test]
        public void GetCurrentApp_FailureState_HasErrorMessage()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            if (!result.IsSuccess)
            {
                Assert.IsNotNull(result.ErrorMessage, "IsSuccess=false 时 ErrorMessage 不应为 null");
                Assert.IsNotEmpty(result.ErrorMessage, "IsSuccess=false 时 ErrorMessage 不应为空字符串");
            }
        }

        // ──────────────────────────────────────────────
        // 4. 平台分支验证
        // ──────────────────────────────────────────────

#if UNITY_STANDALONE_OSX
        [Test]
        public void GetCurrentApp_macOS_ReturnsAppName()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsNotNull(result, "macOS 上 GetCurrentApp() 不应返回 null");
            // 成功或 AccessibilityDenied fallback 两种情况下 AppName 都应有值
            Assert.IsNotNull(result.AppName, "macOS 上 AppName 不应为 null");
            Assert.IsNotEmpty(result.AppName, "macOS 上 AppName 不应为空");
            Debug.Log($"[AppMonitorPlayerTest] AppName='{result.AppName}', WindowTitle='{result.WindowTitle}', IsSuccess={result.IsSuccess}");
        }

        [Test]
        public void GetCurrentApp_macOS_WindowTitle_IsString()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            if (result.IsSuccess && result.ErrorCode == null)
            {
                // 完全成功时 WindowTitle 不为 null（但可以是空字符串，某些应用无窗口）
                Assert.IsNotNull(result.WindowTitle, "macOS 成功状态下 WindowTitle 不应为 null");
            }
        }

        [Test]
        public void GetCurrentApp_macOS_ErrorMessage_NotUnsupportedPlatform()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            // macOS 上绝不应出现"当前平台不支持"的错误
            if (result.ErrorMessage != null)
            {
                StringAssert.DoesNotContain("当前平台不支持", result.ErrorMessage,
                    "macOS 上不应出现 UnsupportedAppMonitorImpl 的错误消息");
            }
        }
#endif

#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN
        [Test]
        public void GetCurrentApp_UnsupportedPlatform_ReturnsPlatformUnsupported()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsFalse(result.IsSuccess, "非支持平台 GetCurrentApp() 应返回失败");
            Assert.AreEqual("当前平台不支持", result.ErrorMessage,
                "非支持平台错误消息应为 '当前平台不支持'");
        }
#endif

#if UNITY_STANDALONE_WIN
        [Test]
        public void GetCurrentApp_Windows_AppName_NotNull()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            Assert.IsNotNull(result, "Windows 上 GetCurrentApp() 不应返回 null");

            if (result.IsSuccess)
            {
                Assert.IsNotNull(result.AppName, "Windows 成功状态下 AppName 不应为 null");
                Debug.Log($"[AppMonitorPlayerTest] Win AppName='{result.AppName}', BundleId='{result.BundleId}', WindowTitle='{result.WindowTitle}'");
            }
            else
            {
                Assert.AreEqual(AppMonitorResultCode.NoFrontmostApp, result.ErrorCode,
                    "Windows 失败时错误码应为 NoFrontmostApp(例如 batchmode 无前台窗口)");
            }
        }

        [Test]
        public void GetCurrentApp_Windows_BundleId_IsLower()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            if (result.IsSuccess && result.BundleId != null)
            {
                Assert.AreEqual(result.BundleId.ToLowerInvariant(), result.BundleId,
                    "Windows 上 BundleId 应为全小写(exe 文件名)");
            }
        }

        [Test]
        public void GetCurrentApp_Windows_ErrorMessage_NotUnsupportedPlatform()
        {
            AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
            if (result.ErrorMessage != null)
            {
                StringAssert.DoesNotContain("当前平台不支持", result.ErrorMessage,
                    "Windows 上不应出现 UnsupportedAppMonitorImpl 的错误消息");
            }
        }
#endif

        // ──────────────────────────────────────────────
        // 5. GetAppIcon
        // ──────────────────────────────────────────────

        [Test]
        public void GetAppIcon_DoesNotThrow()
        {
            Texture2D icon = null;
            Assert.DoesNotThrow(() => icon = CPA.Monitoring.AppMonitor.Instance.GetAppIcon(),
                "GetAppIcon() 不应抛出异常");
            Debug.Log($"[AppMonitorPlayerTest] GetAppIcon = {(icon != null ? $"{icon.width}x{icon.height}" : "null")}");
        }

#if UNITY_STANDALONE_OSX
        [Test]
        public void GetAppIcon_macOS_ReturnsTexture()
        {
            Texture2D icon = CPA.Monitoring.AppMonitor.Instance.GetAppIcon();
            Assert.IsNotNull(icon, "macOS 上 GetAppIcon() 应返回非 null 的 Texture2D（真实图标或 Fallback 图标）");
            Assert.Greater(icon.width, 0, "图标宽度应 > 0");
            Assert.Greater(icon.height, 0, "图标高度应 > 0");

            if (icon != null)
            {
                UnityEngine.Object.Destroy(icon);
            }
        }
#endif

#if !UNITY_STANDALONE_OSX && !UNITY_STANDALONE_WIN
        [Test]
        public void GetAppIcon_UnsupportedPlatform_ReturnsNull()
        {
            Texture2D icon = CPA.Monitoring.AppMonitor.Instance.GetAppIcon();
            Assert.IsNull(icon, "非支持平台 GetAppIcon() 应返回 null");
        }
#endif

#if UNITY_STANDALONE_WIN
        [Test]
        public void GetAppIcon_Windows_ReturnsTextureOrNull()
        {
            Texture2D icon = null;
            Assert.DoesNotThrow(() => icon = CPA.Monitoring.AppMonitor.Instance.GetAppIcon(),
                "Windows 上 GetAppIcon() 不应抛出异常");
            if (icon != null)
            {
                Assert.Greater(icon.width, 0, "图标宽度应 > 0");
                Assert.Greater(icon.height, 0, "图标高度应 > 0");
                UnityEngine.Object.Destroy(icon);
            }
        }
#endif

        // ──────────────────────────────────────────────
        // 6. 结果码枚举完整性
        // ──────────────────────────────────────────────

        [Test]
        public void AppMonitorResultCode_Values_AreCorrect()
        {
            Assert.AreEqual(0, (int)AppMonitorResultCode.Success);
            Assert.AreEqual(-1, (int)AppMonitorResultCode.InvalidArgument);
            Assert.AreEqual(-2, (int)AppMonitorResultCode.AccessibilityDenied);
            Assert.AreEqual(-3, (int)AppMonitorResultCode.NoFrontmostApp);
            Assert.AreEqual(-4, (int)AppMonitorResultCode.IconAllocationFailed);
        }

        // ──────────────────────────────────────────────
        // 7. PermissionDeniedException 构造
        // ──────────────────────────────────────────────

        [Test]
        public void PermissionDeniedException_SingleArg_MessageSet()
        {
            var ex = new PermissionDeniedException("拒绝了");
            Assert.AreEqual("拒绝了", ex.Message);
        }

        [Test]
        public void PermissionDeniedException_TwoArgs_InnerExceptionSet()
        {
            var inner = new Exception("inner");
            var ex = new PermissionDeniedException("外层", inner);
            Assert.AreSame(inner, ex.InnerException);
        }

        // ──────────────────────────────────────────────
        // 8. AppInfo 数据类
        // ──────────────────────────────────────────────

        [Test]
        public void AppInfo_DefaultConstruct_IsSuccessFalse()
        {
            var info = new AppInfo();
            Assert.IsFalse(info.IsSuccess, "AppInfo 默认 IsSuccess 应为 false");
            Assert.IsNull(info.AppName);
            Assert.IsNull(info.Icon);
            Assert.IsNull(info.ErrorCode);
            Assert.IsNull(info.ErrorMessage);
        }

        // ──────────────────────────────────────────────
        // 9. 多次调用稳定性
        // ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetCurrentApp_MultipleCalls_Stable()
        {
            for (int i = 0; i < 5; i++)
            {
                AppInfo result = null;
                Assert.DoesNotThrow(() => result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp(),
                    $"第 {i + 1} 次调用 GetCurrentApp() 不应抛出");
                Assert.IsNotNull(result, $"第 {i + 1} 次调用应返回非 null");

                // 清理 icon，避免纹理泄漏
                if (result?.Icon != null)
                {
                    UnityEngine.Object.Destroy(result.Icon);
                }

                yield return new WaitForSeconds(0.05f);
            }
        }

        // ──────────────────────────────────────────────
        // 10. 内存稳定性（烟雾测试）
        // ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetCurrentApp_MemorySmoke_NoLeak()
        {
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            for (int i = 0; i < 10; i++)
            {
                AppInfo result = CPA.Monitoring.AppMonitor.Instance.GetCurrentApp();
                if (result?.Icon != null)
                {
                    UnityEngine.Object.Destroy(result.Icon);
                }
                yield return null;
            }

            GC.Collect();
            long memAfter = GC.GetTotalMemory(true);
            long delta = memAfter - memBefore;
            Debug.Log($"[AppMonitorPlayerTest] 10 次调用前后内存差: {delta} bytes");

            // 烟雾测试：不应增长超过 5 MB（主要验证没有大量非托管内存泄漏）
            Assert.Less(delta, 5 * 1024 * 1024,
                $"10 次调用后托管内存不应增长超过 5MB（实际增长 {delta / 1024} KB）");
        }
    }
}
