using System.Collections;
using CPA.Monitoring;
using NUnit.Framework;
using NZ.VisualTest.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace NZ.VisualTest.Tests.Player
{
    /// <summary>
    /// macOS Player build tests for MacOSAppMonitor API
    /// 验证原生插件在发布环境中的集成
    /// </summary>
    [TestFixture]
    public sealed class MacOSAppMonitorPlayerTest 
    {
        /// <summary>
        /// 测试核心 API：GetCurrentApp() 返回有效的 AppInfo
        /// </summary>
        [UnityTest]
        public IEnumerator Test_GetCurrentApp_ReturnsValidAppInfo()
        {
            Debug.Log("开始测试: GetCurrentApp() API");

            AppInfo result = null;
            bool apiCallCompleted = false;

            yield return new WaitForEndOfFrame();
            
            try
            {
                result = MacOSAppMonitor.Instance.GetCurrentApp();
                apiCallCompleted = true;
            }
            catch (System.Exception ex)
            {
                Debug.Log($"API 调用异常: {ex.Message}");
                Assert.Fail($"GetCurrentApp() 抛出异常: {ex.Message}");
            }

            yield return null;

            Assert.IsTrue(apiCallCompleted, "API 调用应该完成");
            Assert.IsNotNull(result, "AppInfo 不应为 null");

            Debug.Log($"测试结果 - IsSuccess: {result.IsSuccess}");

            Assert.IsTrue(result.IsSuccess, 
                $"IsSuccess 应该为 true. 错误信息: {result.ErrorMessage}");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result.AppName),
                "AppName 不应为空或空白");
            
            Debug.Log($"检测到的应用: {result.AppName}");

            Assert.IsNotNull(result.Icon, "Icon 不应为 null");

            Assert.Greater(result.Icon.width, 0, "图标宽度应大于 0");
            Assert.Greater(result.Icon.height, 0, "图标高度应大于 0");

            Debug.Log($"图标尺寸: {result.Icon.width}x{result.Icon.height}");

            yield return new WaitForSeconds(0.5f);

            if (result.Icon != null)
            {
                Object.Destroy(result.Icon);
            }

            Debug.Log("测试完成: GetCurrentApp() 验证通过");
        }

        /// <summary>
        /// 测试平台检测：验证 Player 环境正确识别
        /// </summary>
        [UnityTest]
        public IEnumerator Test_PlatformDetection_PlayerEnvironment()
        {
            Debug.Log("开始测试: 平台检测");

            Assert.IsFalse(Application.isEditor,
                "Application.isEditor 应为 false (Player 环境)");

            Assert.AreEqual(RuntimePlatform.OSXPlayer, Application.platform,
                $"Application.platform 应为 OSXPlayer. 实际: {Application.platform}");

            bool canCallNative = CanCallNativeMonitor();
            Assert.IsTrue(canCallNative,
                "在 macOS Player 中应该可以调用原生监控");

            Debug.Log($"平台验证通过 - Editor: {Application.isEditor}, " +
                          $"Platform: {Application.platform}");

            yield return null;
        }

        /// <summary>
        /// 测试 Accessibility 权限处理：优雅降级
        /// </summary>
        [UnityTest]
        public IEnumerator Test_GetCurrentApp_HandlesAccessibilityGracefully()
        {
            Debug.Log("开始测试: Accessibility 权限处理");

            AppInfo result = MacOSAppMonitor.Instance.GetCurrentApp();

            Assert.IsNotNull(result, "即使权限被拒绝也应返回 AppInfo");

            if (result.ErrorCode == AppMonitorResultCode.AccessibilityDenied)
            {
                Debug.Log("Accessibility 权限被拒绝 - 验证回退机制");

                Assert.IsFalse(string.IsNullOrWhiteSpace(result.AppName),
                    "权限被拒绝时仍应提供应用名称（回退值）");

                Assert.IsNotNull(result.Icon, "权限被拒绝时仍应提供图标（回退图标）");

                Assert.IsTrue(result.IsSuccess,
                    "回退模式下 IsSuccess 应为 true");
                
                Debug.Log("回退机制工作正常");
            }
            else
            {
                Debug.Log("Accessibility 权限已授予 - 完整功能可用");
                
                Assert.IsTrue(result.IsSuccess, "权限已授予时 IsSuccess 应为 true");
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.AppName),
                    "应获取到前台应用名称");
            }

            if (result.Icon != null)
            {
                Object.Destroy(result.Icon);
            }

            Debug.Log("Accessibility 处理测试完成");
            yield return null;
        }

        /// <summary>
        /// 测试 AppInfo 数据完整性
        /// </summary>
        [UnityTest]
        public IEnumerator Test_AppInfo_DataIntegrity()
        {
            Debug.Log("开始测试: AppInfo 数据完整性");

            AppInfo result = MacOSAppMonitor.Instance.GetCurrentApp();
            Assert.IsNotNull(result, "AppInfo 不应为 null");

            Assert.DoesNotThrow(() =>
            {
                var _ = result.AppName;
                var __ = result.WindowTitle;
                var ___ = result.IsSuccess;
                var ____ = result.ErrorCode;
                var _____ = result.ErrorMessage;
            }, "访问所有 AppInfo 字段不应抛出异常");

            if (result.IsSuccess && result.ErrorCode.HasValue)
            {
                Assert.AreEqual(AppMonitorResultCode.AccessibilityDenied, result.ErrorCode.Value,
                    "成功状态下唯一的错误码应为 AccessibilityDenied (回退模式)");
            }

            Debug.Log($"数据验证通过 - AppName: {result.AppName}, " +
                          $"ErrorCode: {(result.ErrorCode.HasValue ? result.ErrorCode.Value.ToString() : "null")}");

            if (result.Icon != null)
            {
                Object.Destroy(result.Icon);
            }

            yield return null;
        }

        private static bool CanCallNativeMonitor()
        {
            return !Application.isEditor && Application.platform == RuntimePlatform.OSXPlayer;
        }
    }
}
