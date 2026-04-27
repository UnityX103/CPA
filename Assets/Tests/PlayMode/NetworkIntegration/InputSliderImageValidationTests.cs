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
    /// InputSlider 组件独立视觉验证 — 三态截屏（0%/50%/100%）。
    /// 只挂载 InputSlider.uxml 组件自身，不依赖任何面板模板，
    /// 用于验证组件结构与 Pencil 设计稿 (YwCv6) 的一致性。
    /// </summary>
    [TestFixture]
    public sealed class InputSliderImageValidationTests : VisualImageTestBase
    {
        private const string PanelSettingsPath = "Assets/UI_V2/PanelSettings_Settings.asset";
        private const string ComponentPath = "Assets/UI_V2/Documents/Components/InputSlider.uxml";

        private const float MinScale = 0.5f;
        private const float MaxScale = 2.0f;
        private const float DraggerSize = 24f;
        private const float FillLeftInset = 0f;

        // 限定宿主宽度为 Pencil 组件设计宽度（360px），
        // 避免 width:100% 组件被全屏撑开导致原生 Slider 暴露默认装饰。
        private const float HostWidth = 360f;
        private const float HostHeight = 120f;

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
        public IEnumerator InputSlider_ShouldCaptureZeroHalfFullStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            // 创建纯黑背景摄像机，避免上一帧残留 UI 形成残影
            _cameraHost = new GameObject("VisualTestCamera_InputSlider");
            Camera testCamera = _cameraHost.AddComponent<Camera>();
            testCamera.clearFlags = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = Color.black;
            testCamera.depth = 100f;
            testCamera.orthographic = true;
            testCamera.cullingMask = 0;

            VisualElement hostRoot = CreateComponentHost();
            yield return null;

            yield return CaptureSliderState(hostRoot, percent: 0f,   label: "0percent");
            yield return CaptureSliderState(hostRoot, percent: 0.5f, label: "50percent");
            yield return CaptureSliderState(hostRoot, percent: 1f,   label: "100percent");

            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3),
                "应登记 0%/50%/100% 三态截图。");
            AssertCaptureArtifact("01-input-slider-0percent-actual.png");
            AssertCaptureArtifact("02-input-slider-50percent-actual.png");
            AssertCaptureArtifact("03-input-slider-100percent-actual.png");
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CaptureSliderState(VisualElement hostRoot, float percent, string label)
        {
            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(ComponentPath);
            TemplateContainer container = template.CloneTree();
            hostRoot.Clear();
            hostRoot.Add(container);

            yield return WaitForFrames(10);

            Slider slider = container.Q<Slider>("gsp-scale-slider");
            VisualElement wrap = container.Q<VisualElement>("gsp-scale-slider-wrap");
            VisualElement fill = container.Q<VisualElement>("gsp-scale-slider-fill");
            Label valueLabel = container.Q<Label>("gsp-scale-value");

            Assert.That(slider, Is.Not.Null, "必须能加载 gsp-scale-slider。");
            Assert.That(wrap, Is.Not.Null, "必须能加载 gsp-scale-slider-wrap。");
            Assert.That(fill, Is.Not.Null, "必须能加载 gsp-scale-slider-fill。");
            Assert.That(valueLabel, Is.Not.Null, "必须能加载 gsp-scale-value。");

            float value = Mathf.Lerp(MinScale, MaxScale, percent);
            value = Mathf.Round(value * 10f) / 10f;
            slider.SetValueWithoutNotify(value);
            valueLabel.text = $"{value:0.0}×";

            // 等 wrap 完成布局拿到真实宽度
            for (int frame = 0; frame < 30; frame++)
            {
                if (wrap.resolvedStyle.width > 0f)
                {
                    break;
                }
                yield return null;
            }

            float trackWidth = wrap.resolvedStyle.width;
            if (trackWidth > 0f)
            {
                float normalized = Mathf.Clamp01(Mathf.InverseLerp(MinScale, MaxScale, value));
                float dragRange = Mathf.Max(0f, trackWidth - DraggerSize);
                float thumbCenterX = (DraggerSize * 0.5f) + (dragRange * normalized);
                fill.style.width = Mathf.Max(0f, thumbCenterX - FillLeftInset);
            }

            yield return CaptureScreenStep(
                $"input-slider-{label}",
                null,
                $"component-only full-screen; value={value:0.00}; percent={percent:0.00}");
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }
        }

        private void AssertCaptureArtifact(string fileName)
        {
            string path = Path.Combine(CurrentRunDirectory, fileName);
            Assert.That(File.Exists(path), Is.True, $"截图产物不存在：{path}");
        }

        private VisualElement CreateComponentHost()
        {
            _host = new GameObject("InputSliderImageValidationHost");
            UIDocument document = _host.AddComponent<UIDocument>();
            PanelSettings panelSettings = Object.Instantiate(
                LoadAsset<PanelSettings>(PanelSettingsPath));
            panelSettings.scale = 1f;
            document.panelSettings = panelSettings;

            VisualElement root = document.rootVisualElement;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.width = HostWidth;
            root.style.height = HostHeight;
            root.style.backgroundColor = Color.clear;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            return root;
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"资源加载失败：{path}");
            return asset;
        }
#endif
    }
}
