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

        [UnityTest]
        public IEnumerator SettingsImport_ShouldCaptureExpectedStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
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
            SetSliderVisualState(container, slider, 1.25f);

            SetDisplayDropdownVisualState(target, 0);

            yield return WaitUntilReady(target, 60);
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
            SetSliderVisualState(container, slider, 1.5f);

            Label valueLabel = target.Q<Label>("gsp-scale-value");
            Assert.That(valueLabel, Is.Not.Null, "必须能加载 gsp-scale-value。");
            valueLabel.text = "1.5×";

            Button applyButton = target.Q<Button>("apply-btn");
            Assert.That(applyButton, Is.Not.Null, "必须能加载 apply-btn，用于检查修改值后应用按钮表现。");

            SetDisplayDropdownVisualState(target, 1);

            yield return WaitUntilReady(target, 60);
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
            Assert.That(fill, Is.Not.Null, "必须能加载 gsp-scale-slider-fill。");

            float normalized = Mathf.InverseLerp(slider.lowValue, slider.highValue, value);
            fill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
        }

        /// <summary>
        /// 视觉测试在 PlayMode 创建的面板没有走 Controller，
        /// 所以下拉框默认空，得手动注入 choices 和当前值才能截到目标显示器卡片。
        /// </summary>
        private static void SetDisplayDropdownVisualState(VisualElement root, int index)
        {
            DropdownField dropdown = root.Q<DropdownField>("gsp-display-dropdown");
            Assert.That(dropdown, Is.Not.Null, "必须能加载 gsp-display-dropdown。");

            var choices = new System.Collections.Generic.List<string>
            {
                "显示器 1（1920×1080）",
                "显示器 2（2560×1440）",
            };
            dropdown.choices = choices;
            int safe = Mathf.Clamp(index, 0, choices.Count - 1);
            dropdown.SetValueWithoutNotify(choices[safe]);
        }

        private static VisualElement CreateRuntimePanelRoot()
        {
            var go = new GameObject("SettingsImportPixelImageValidationRoot");
            UIDocument document = go.AddComponent<UIDocument>();
            document.panelSettings = LoadAsset<PanelSettings>(PanelSettingsPath);

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
