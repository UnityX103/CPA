using System.Collections;
using System.IO;
using APP.Pomodoro.Controller;
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
    [TestFixture]
    public sealed class SettingsImportPixelImageValidationTests : VisualImageTestBase
    {
        private const string BaselineDirectory = "TestArtifacts/PencilReferences";
        private const string PanelSettingsPath = "Assets/UI_V2/PanelSettings_Settings.asset";
        private const string GlobalSettingsPanelPath = "Assets/UI_V2/Documents/GlobalSettingsPanel.uxml";
        private const string ConfirmDialogPath = "Assets/UI_V2/Documents/ConfirmDialog.uxml";

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
        public IEnumerator SettingsImport_ShouldCaptureExpectedStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            // 创建纯黑背景摄像机，避免上一帧残留 UI 形成残影
            _cameraHost = new GameObject("VisualTestCamera_SettingsImport");
            Camera testCamera = _cameraHost.AddComponent<Camera>();
            testCamera.clearFlags = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = Color.black;
            testCamera.depth = 100f;
            testCamera.orthographic = true;
            testCamera.cullingMask = 0;

            VisualElement panelRoot = CreateRuntimePanelRoot();
            yield return null;

            yield return CaptureGlobalSettingsPanel(panelRoot);
            yield return CaptureGlobalSettingsPanelChangedValue(panelRoot);
            yield return CaptureConfirmDialog(panelRoot);

            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(3), "应登记三个 capture step。");
            AssertCaptureArtifact("01-global-settings-half-actual.png");
            AssertCaptureArtifact("02-global-settings-changed-value-actual.png");
            AssertCaptureArtifact("03-confirm-dialog-no-close-actual.png");
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CaptureGlobalSettingsPanel(VisualElement panelRoot)
        {
            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(GlobalSettingsPanelPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>("gsp-root");
            Assert.That(target, Is.Not.Null, "必须能加载 GlobalSettingsPanel.uxml 的 gsp-root。");
            target.style.width = 572;

            Slider slider = target.Q<Slider>("gsp-scale-slider");
            Assert.That(slider, Is.Not.Null, "必须能加载 gsp-scale-slider。");
            slider.lowValue = 0.5f;
            slider.highValue = 2.0f;
            slider.SetValueWithoutNotify(1.25f);

            SetDisplayDropdownVisualState(target, 0);

            yield return WaitUntilReady(target, 60);
            SetSliderVisualState(container, slider, 1.25f);
            yield return null;
            yield return CaptureVisualStep(
                "global-settings-half",
                target,
                $"{BaselineDirectory}/global-settings-panel-half.png");
        }

        private IEnumerator CaptureConfirmDialog(VisualElement panelRoot)
        {
            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(ConfirmDialogPath);
            panelRoot.Clear();

            ConfirmDialogController controller = new ConfirmDialogController();
            controller.Init(panelRoot, template);
            controller.Show(
                "标题",
                "副标题",
                "正文占位正文占位正文占位",
                "确认",
                "取消",
                null,
                null,
                0f);

            VisualElement target = panelRoot.Query<VisualElement>(className: "dlg-card").First();
            Assert.That(target, Is.Not.Null, "必须能加载 ConfirmDialog.uxml 的 dlg-card。");

            yield return WaitUntilReady(target, 60);
            yield return CaptureVisualStep(
                "confirm-dialog-no-close",
                target,
                $"{BaselineDirectory}/confirm-dialog-no-close.png");
        }

        private IEnumerator CaptureGlobalSettingsPanelChangedValue(VisualElement panelRoot)
        {
            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(GlobalSettingsPanelPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>("gsp-root");
            Assert.That(target, Is.Not.Null, "必须能加载 GlobalSettingsPanel.uxml 的 gsp-root。");
            target.style.width = 572;

            Slider slider = target.Q<Slider>("gsp-scale-slider");
            Assert.That(slider, Is.Not.Null, "必须能加载 gsp-scale-slider。");
            slider.lowValue = 0.5f;
            slider.highValue = 2.0f;
            slider.SetValueWithoutNotify(1.5f);

            Label valueLabel = target.Q<Label>("gsp-scale-value");
            Assert.That(valueLabel, Is.Not.Null, "必须能加载 gsp-scale-value。");
            valueLabel.text = "1.5×";

            Button applyButton = target.Q<Button>("apply-btn");
            Assert.That(applyButton, Is.Not.Null, "必须能加载 apply-btn，用于检查修改值后应用按钮表现。");

            SetDisplayDropdownVisualState(target, 1);

            yield return WaitUntilReady(target, 60);
            SetSliderVisualState(container, slider, 1.5f);
            yield return null;
            yield return CaptureVisualStep(
                "global-settings-changed-value",
                target,
                null);
        }

        private IEnumerator CaptureVisualStep(
            string stepName,
            VisualElement target,
            string baselinePath)
        {
            if (!string.IsNullOrWhiteSpace(baselinePath))
            {
                Assert.That(File.Exists(baselinePath), Is.True, $"Pencil 参考图不存在：{baselinePath}");
            }

            // 等待目标元素布局就绪后截整屏；项目约定：所有视觉测试都用 full-screen 截图，
            // 让审图人能看到 game view 内的完整上下文（侧栏 / 标题 / 同级控件等）。
            Assert.That(target, Is.Not.Null, "待截图的 VisualElement 不能为空。");
            Assert.That(target.worldBound.width, Is.GreaterThan(0f), "目标元素宽度必须大于 0。");
            Assert.That(target.worldBound.height, Is.GreaterThan(0f), "目标元素高度必须大于 0。");

            yield return CaptureScreenStep(stepName, baselinePath, "full-screen");
        }

        private void AssertCaptureArtifact(string fileName)
        {
            string path = Path.Combine(CurrentRunDirectory, fileName);
            Assert.That(File.Exists(path), Is.True, $"截图产物不存在：{path}");
        }

        private static void SetSliderVisualState(VisualElement root, Slider slider, float value)
        {
            slider.SetValueWithoutNotify(value);

            VisualElement fill = root.Q<VisualElement>("gsp-scale-slider-fill");
            VisualElement wrap = root.Q<VisualElement>("gsp-scale-slider-wrap");
            Assert.That(fill, Is.Not.Null, "必须能加载 gsp-scale-slider-fill。");
            Assert.That(wrap, Is.Not.Null, "必须能加载 gsp-scale-slider-wrap。");
            Assert.That(wrap.resolvedStyle.width, Is.GreaterThan(0f), "滑条容器必须已完成布局。");

            fill.style.width = CalculateScaleFillWidth(slider, value, wrap.resolvedStyle.width);
        }

        private static float CalculateScaleFillWidth(Slider slider, float value, float trackWidth)
        {
            const float DraggerSize = 24f;
            const float FillLeftInset = 0f;

            float normalized = Mathf.Clamp01(Mathf.InverseLerp(slider.lowValue, slider.highValue, value));
            float dragRange = Mathf.Max(0f, trackWidth - DraggerSize);
            float thumbCenterX = (DraggerSize * 0.5f) + (dragRange * normalized);
            return Mathf.Max(0f, thumbCenterX);
        }

        /// <summary>
        /// 视觉测试在 PlayMode 创建的面板没有走 Controller，
        /// 所以下拉框默认空，得手动注入 choices 和当前值才能截到目标显示器卡片。
        /// </summary>
        private static void SetDisplayDropdownVisualState(VisualElement root, int index)
        {
            Label valueLabel = root.Q<Label>("gsp-display-dropdown-value");
            VisualElement menu = root.Q<VisualElement>("gsp-display-menu");
            Assert.That(valueLabel, Is.Not.Null, "必须能加载 gsp-display-dropdown-value。");
            Assert.That(menu, Is.Not.Null, "必须能加载 gsp-display-menu。");

            var choices = new System.Collections.Generic.List<string>
            {
                "显示器 1（1920×1080）",
                "显示器 2（2560×1440）",
            };
            int safe = Mathf.Clamp(index, 0, choices.Count - 1);
            valueLabel.text = choices[safe];

            menu.Clear();
            foreach (string choice in choices)
            {
                var item = new VisualElement();
                item.AddToClassList("gsp-display-menu-item");
                var label = new Label(choice);
                label.AddToClassList("gsp-display-menu-item-label");
                item.Add(label);
                menu.Add(item);
            }
            menu.EnableInClassList("gsp-display-menu--hidden", true);
        }

        private VisualElement CreateRuntimePanelRoot()
        {
            _host = new GameObject("SettingsImportPixelImageValidationRoot");
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
#endif
    }
}
