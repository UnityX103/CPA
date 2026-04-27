using System.Collections;
using System.IO;
using NZ.VisualTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace APP.NetworkIntegration.Tests
{
    /// <summary>
    /// 全局设置面板 — 界面缩放滑动条三态视觉验证。
    /// 0%（0.5×）/ 50%（1.25×）/ 100%（2.0×）三档分别截图，
    /// 用于断言导出后的 InputSlider 组件与 Pencil 设计稿一致。
    /// </summary>
    [TestFixture]
    public sealed class GlobalSettingsScaleSliderImageValidationTests : VisualImageTestBase
    {
        private const string PanelSettingsPath = "Assets/UI_V2/PanelSettings_Settings.asset";
        private const string GlobalSettingsPanelPath = "Assets/UI_V2/Documents/GlobalSettingsPanel.uxml";

        private const float MinScale = 0.5f;
        private const float MaxScale = 2.0f;
        private const float DraggerSize = 24f;
        private const float FillLeftInset = 0f;

        private GameObject _host;
        private GameObject _cameraHost;

        [TearDown]
        public void TearDownComponentHost()
        {
            if (_host != null)
            {
                UIDocument doc = _host.GetComponent<UIDocument>();
                if (doc != null && doc.panelSettings != null)
                {
                    Object.DestroyImmediate(doc.panelSettings);
                }
                Object.DestroyImmediate(_host);
                _host = null;
            }
            if (_cameraHost != null)
            {
                Object.DestroyImmediate(_cameraHost);
                _cameraHost = null;
            }
        }

        [UnityTest]
        public IEnumerator GlobalSettingsScaleSlider_ShouldCaptureZeroHalfFullStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            // 创建纯黑背景摄像机，避免上一帧残留 UI 形成残影
            _cameraHost = new GameObject("VisualTestCamera_ScaleSlider");
            Camera testCamera = _cameraHost.AddComponent<Camera>();
            testCamera.clearFlags = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = Color.black;
            testCamera.depth = 100f;
            testCamera.orthographic = true;
            testCamera.cullingMask = 0;

            VisualElement panelRoot = CreateRuntimePanelRoot();
            yield return null;

            yield return CaptureSliderState(panelRoot, percent: 0f,   label: "0percent");
            yield return CaptureSliderState(panelRoot, percent: 0.5f, label: "50percent");
            yield return CaptureSliderState(panelRoot, percent: 1f,   label: "100percent");

            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3), "应登记 0%/50%/100% 三态截图。");
            AssertCaptureArtifact("01-global-settings-scale-slider-0percent-actual.png");
            AssertCaptureArtifact("02-global-settings-scale-slider-50percent-actual.png");
            AssertCaptureArtifact("03-global-settings-scale-slider-100percent-actual.png");
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CaptureSliderState(VisualElement panelRoot, float percent, string label)
        {
            VisualElement target = BuildPanel(panelRoot);
            yield return WaitUntilReady(target, 60);

            Slider slider = target.Q<Slider>("gsp-scale-slider");
            VisualElement wrap = target.Q<VisualElement>("gsp-scale-slider-wrap");
            VisualElement fill = target.Q<VisualElement>("gsp-scale-slider-fill");
            Label valueLabel = target.Q<Label>("gsp-scale-value");
            Assert.That(slider, Is.Not.Null, "必须能加载 gsp-scale-slider。");
            Assert.That(wrap, Is.Not.Null, "必须能加载 gsp-scale-slider-wrap。");
            Assert.That(fill, Is.Not.Null, "必须能加载 gsp-scale-slider-fill。");
            Assert.That(valueLabel, Is.Not.Null, "必须能加载 gsp-scale-value。");

            float value = Mathf.Lerp(MinScale, MaxScale, percent);
            value = Mathf.Round(value * 10f) / 10f;
            slider.SetValueWithoutNotify(value);
            valueLabel.text = $"{value:0.0}×";

            yield return WaitUntilLayoutResolved(wrap, 30);
            ApplyFillWidth(fill, wrap, value);

            yield return CaptureScreenStep(
                $"global-settings-scale-slider-{label}",
                null,
                $"full-screen; value={value:0.00}; percent={percent:0.00}");
        }

        private static void ApplyFillWidth(VisualElement fill, VisualElement wrap, float value)
        {
            float trackWidth = wrap.resolvedStyle.width;
            if (trackWidth <= 0f) trackWidth = wrap.parent?.resolvedStyle.width ?? 0f;
            float normalized = Mathf.Clamp01(Mathf.InverseLerp(MinScale, MaxScale, value));
            float dragRange = Mathf.Max(0f, trackWidth - DraggerSize);
            float thumbCenterX = (DraggerSize * 0.5f) + (dragRange * normalized);
            fill.style.width = Mathf.Max(0f, thumbCenterX);
        }

        private static VisualElement BuildPanel(VisualElement panelRoot)
        {
            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(GlobalSettingsPanelPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>("gsp-root");
            Assert.That(target, Is.Not.Null, "必须能加载 GlobalSettingsPanel.uxml 的 gsp-root。");
            target.style.width = 572;
            return target;
        }

        private static IEnumerator WaitUntilReady(VisualElement target, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (target.panel != null && target.worldBound.width > 0f && target.worldBound.height > 0f)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"等待元素就绪超时：{target.name}, worldBound={target.worldBound}");
        }

        private static IEnumerator WaitUntilLayoutResolved(VisualElement wrap, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (wrap.resolvedStyle.width > 0f) yield break;
                yield return null;
            }
        }

        private void AssertCaptureArtifact(string fileName)
        {
            string path = Path.Combine(CurrentRunDirectory, fileName);
            Assert.That(File.Exists(path), Is.True, $"截图产物不存在：{path}");
        }

        private VisualElement CreateRuntimePanelRoot()
        {
            _host = new GameObject("GlobalSettingsScaleSliderImageValidationRoot");
            UIDocument document = _host.AddComponent<UIDocument>();
            PanelSettings panelSettings = UnityEngine.Object.Instantiate(LoadAsset<PanelSettings>(PanelSettingsPath));
            panelSettings.scale = 1f;
            document.panelSettings = panelSettings;

            VisualElement root = document.rootVisualElement;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.width = 800;
            root.style.height = 600;
            root.style.backgroundColor = Color.clear;
            return root;
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"资源加载失败：{path}");
            return asset;
        }
#endif
    }
}
