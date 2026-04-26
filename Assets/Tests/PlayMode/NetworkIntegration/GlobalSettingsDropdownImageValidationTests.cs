using System.Collections;
using System.Collections.Generic;
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
    [TestFixture]
    public sealed class GlobalSettingsDropdownImageValidationTests : VisualImageTestBase
    {
        private const string PanelSettingsPath = "Assets/UI_V2/PanelSettings_Settings.asset";
        private const string GlobalSettingsPanelPath = "Assets/UI_V2/Documents/GlobalSettingsPanel.uxml";

        private const string DropdownMenuHiddenClassName = "gsp-display-menu--hidden";
        private const string DropdownMenuItemClassName = "gsp-display-menu-item";

        [UnityTest]
        public IEnumerator GlobalSettingsDropdown_ShouldCaptureExpandedAndSelectedStates()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            VisualElement panelRoot = CreateRuntimePanelRoot();
            yield return null;

            yield return CaptureDropdownSelectedState(panelRoot, 2);
            yield return CaptureDropdownSelectedState(panelRoot, 3);
            yield return CaptureDropdownSelectedState(panelRoot, 5);
            yield return CaptureDropdownExpandedState(panelRoot, 2);
            yield return CaptureDropdownExpandedState(panelRoot, 3);
            yield return CaptureDropdownExpandedState(panelRoot, 5);

            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(6), "应登记 2/3/5 个选项的展开态和选择态。");
            AssertCaptureArtifact("01-global-settings-dropdown-2-selected-actual.png");
            AssertCaptureArtifact("02-global-settings-dropdown-3-selected-actual.png");
            AssertCaptureArtifact("03-global-settings-dropdown-5-selected-actual.png");
            AssertCaptureArtifact("04-global-settings-dropdown-2-expanded-actual.png");
            AssertCaptureArtifact("05-global-settings-dropdown-3-expanded-actual.png");
            AssertCaptureArtifact("06-global-settings-dropdown-5-expanded-actual.png");
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CaptureDropdownSelectedState(VisualElement panelRoot, int optionCount)
        {
            VisualElement target = BuildPanel(panelRoot, optionCount, optionCount - 1);
            Label valueLabel = target.Q<Label>("gsp-display-dropdown-value");
            Assert.That(valueLabel, Is.Not.Null, "必须能加载 gsp-display-dropdown-value。");

            yield return WaitUntilReady(target, 60);
            yield return CaptureScreenStep(
                $"global-settings-dropdown-{optionCount}-selected",
                null,
                $"full-screen; options={optionCount}; selected={valueLabel.text}");
        }

        private IEnumerator CaptureDropdownExpandedState(VisualElement panelRoot, int optionCount)
        {
            VisualElement target = BuildPanel(panelRoot, optionCount, selectedIndex: 0);
            VisualElement dropdown = target.Q<VisualElement>("gsp-display-dropdown");
            VisualElement menu = target.Q<VisualElement>("gsp-display-menu");
            Assert.That(dropdown, Is.Not.Null, "必须能加载 gsp-display-dropdown。");
            Assert.That(menu, Is.Not.Null, "必须能加载 gsp-display-menu。");

            yield return WaitUntilReady(target, 60);
            yield return OpenDropdown(menu, panelRoot, optionCount);
            AssertDropdownMenuMatchesFieldWidth(dropdown, menu);
            yield return CaptureScreenStep(
                $"global-settings-dropdown-{optionCount}-expanded",
                null,
                $"full-screen; options={optionCount}; expanded");
        }

        private static VisualElement BuildPanel(VisualElement panelRoot, int optionCount, int selectedIndex)
        {
            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(GlobalSettingsPanelPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>("gsp-root");
            Assert.That(target, Is.Not.Null, "必须能加载 GlobalSettingsPanel.uxml 的 gsp-root。");
            target.style.width = 572;

            Label valueLabel = target.Q<Label>("gsp-display-dropdown-value");
            VisualElement menu = target.Q<VisualElement>("gsp-display-menu");
            Assert.That(valueLabel, Is.Not.Null, "必须能加载 gsp-display-dropdown-value。");
            Assert.That(menu, Is.Not.Null, "必须能加载 gsp-display-menu。");

            List<string> choices = BuildDisplayChoices(optionCount);
            int safeIndex = Mathf.Clamp(selectedIndex, 0, choices.Count - 1);
            valueLabel.text = choices[safeIndex];
            menu.Clear();
            foreach (string choice in choices)
            {
                var item = new VisualElement();
                item.AddToClassList(DropdownMenuItemClassName);
                var label = new Label(choice);
                label.AddToClassList("gsp-display-menu-item-label");
                item.Add(label);
                menu.Add(item);
            }
            menu.EnableInClassList(DropdownMenuHiddenClassName, true);

            return target;
        }

        private static List<string> BuildDisplayChoices(int optionCount)
        {
            var choices = new List<string>(optionCount);
            for (int i = 0; i < optionCount; i++)
            {
                int width = 1920 + (i * 320);
                int height = 1080 + (i * 180);
                choices.Add($"虚拟显示器 {i + 1}（{width}×{height}）");
            }

            return choices;
        }

        private static IEnumerator OpenDropdown(VisualElement menu, VisualElement panelRoot, int expectedItemCount)
        {
            menu.EnableInClassList(DropdownMenuHiddenClassName, false);
            yield return WaitUntilMenuReady(panelRoot, expectedItemCount, 30);
        }

        private static void AssertDropdownMenuMatchesFieldWidth(VisualElement dropdown, VisualElement menu)
        {
            const float Tolerance = 1f;
            Rect dropdownBounds = dropdown.worldBound;
            Rect menuBounds = menu.worldBound;

            Assert.That(menuBounds.xMin, Is.EqualTo(dropdownBounds.xMin).Within(Tolerance), "下拉选项左边缘必须对齐下拉条。");
            Assert.That(menuBounds.width, Is.EqualTo(dropdownBounds.width).Within(Tolerance), "下拉选项宽度必须等于下拉条宽度。");
        }

        private static IEnumerator WaitUntilMenuReady(
            VisualElement panelRoot,
            int expectedItemCount,
            int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                int itemCount = panelRoot.Query<VisualElement>(className: DropdownMenuItemClassName).ToList().Count;
                VisualElement firstItem = panelRoot.Query<VisualElement>(className: DropdownMenuItemClassName).First();
                if (firstItem != null && firstItem.worldBound.width > 0f && itemCount == expectedItemCount)
                {
                    yield break;
                }

                yield return null;
            }

            int actualCount = panelRoot.Query<VisualElement>(className: DropdownMenuItemClassName).ToList().Count;
            Assert.Fail($"等待下拉菜单展开超时：expected={expectedItemCount}, actual={actualCount}");
        }

        private void AssertCaptureArtifact(string fileName)
        {
            string path = Path.Combine(CurrentRunDirectory, fileName);
            Assert.That(File.Exists(path), Is.True, $"截图产物不存在：{path}");
        }

        private static VisualElement CreateRuntimePanelRoot()
        {
            var go = new GameObject("GlobalSettingsDropdownImageValidationRoot");
            UIDocument document = go.AddComponent<UIDocument>();
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
