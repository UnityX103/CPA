using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using NZ.VisualTest.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace NZ.VisualTest.Tests.Runtime
{
    [TestFixture]
    public class AppMonitorVisualTest : VisualTestBase
    {
        private const string TestSceneName = "AppMonitorTestScene";
        private const string UiRootObjectName = "AppMonitorUI";

        [UnityTest]
        public IEnumerator Test_AppMonitorScene_CanReadFocusedAppNameAndIcon()
        {
            LogInputAction($"加载场景: {TestSceneName}");
            SceneManager.LoadScene(TestSceneName, LoadSceneMode.Single);
            yield return null;
            yield return new WaitForSeconds(1f);

            Scene activeScene = SceneManager.GetActiveScene();
            Assert.AreEqual(TestSceneName, activeScene.name, "测试未加载到预期场景。");

            GameObject uiRoot = GameObject.Find(UiRootObjectName);
            Assert.IsNotNull(uiRoot, $"场景中未找到对象: {UiRootObjectName}");

            UIDocument uiDocument = uiRoot.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument, "AppMonitorUI 缺少 UIDocument 组件。");

            VisualTreeAsset visualTreeAsset = uiDocument.visualTreeAsset;
            Assert.IsNotNull(visualTreeAsset, "UIDocument 未绑定 UXML（AppMonitorSection）。");
            Assert.IsTrue(
                visualTreeAsset.name.Contains("AppMonitorSection", StringComparison.OrdinalIgnoreCase),
                $"UIDocument 绑定的 UXML 不是 AppMonitorSection，当前为: {visualTreeAsset.name}");
            LogInputAction("AppMonitorSection 组件存在");

            object appInfo = GetCurrentAppInfoViaReflection(out string reflectionError);
            Assert.IsNotNull(appInfo, $"获取 AppInfo 失败: {reflectionError}");

            bool isSuccess = GetPublicFieldValue<bool>(appInfo, "IsSuccess", false);
            string errorMessage = GetPublicFieldValue<string>(appInfo, "ErrorMessage", string.Empty);
            string appName = GetPublicFieldValue<string>(appInfo, "AppName", string.Empty);
            Texture2D icon = GetPublicFieldValue<Texture2D>(appInfo, "Icon", null);

            Assert.IsTrue(isSuccess, $"macOS 应用监控返回失败: {errorMessage}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(appName), "当前聚焦应用名称为空。");
            Assert.IsNotNull(icon, "当前聚焦应用图标为空。");

            Debug.Log($"[AppMonitorVisualTest] 成功获取应用名称: {appName}");
            Debug.Log($"[AppMonitorVisualTest] 成功获取应用图标: {icon.width}x{icon.height}");
            LogInputAction($"成功获取应用名称: {appName}");
            LogInputAction("成功获取应用图标");

            if (icon != null)
            {
                UnityEngine.Object.Destroy(icon);
            }

            yield return new WaitForSeconds(0.5f);
            LogInputAction("测试完成");
        }

        private static object GetCurrentAppInfoViaReflection(out string error)
        {
            error = string.Empty;

            try
            {
                Type monitorType = Type.GetType("CPA.Monitoring.MacOSAppMonitor, Assembly-CSharp");
                if (monitorType == null)
                {
                    error = "未找到类型 CPA.Monitoring.MacOSAppMonitor（Assembly-CSharp）。";
                    return null;
                }

                PropertyInfo instanceProperty = monitorType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty == null)
                {
                    error = "未找到 MacOSAppMonitor.Instance 属性。";
                    return null;
                }

                object monitorInstance = instanceProperty.GetValue(null);
                if (monitorInstance == null)
                {
                    error = "MacOSAppMonitor.Instance 返回 null。";
                    return null;
                }

                MethodInfo getCurrentAppMethod = monitorType.GetMethod("GetCurrentApp", BindingFlags.Public | BindingFlags.Instance);
                if (getCurrentAppMethod == null)
                {
                    error = "未找到 MacOSAppMonitor.GetCurrentApp 方法。";
                    return null;
                }

                return getCurrentAppMethod.Invoke(monitorInstance, null);
            }
            catch (TargetInvocationException targetInvocationException)
            {
                error = targetInvocationException.InnerException?.Message ?? targetInvocationException.Message;
                return null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return null;
            }
        }

        private static T GetPublicFieldValue<T>(object target, string fieldName, T defaultValue)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return defaultValue;
            }

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                return defaultValue;
            }

            object value = field.GetValue(target);
            if (value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }
    }
}
