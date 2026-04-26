using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        private const string DropdownMenuClassName = "unity-base-dropdown";
        private const string DropdownMenuOuterClassName = "unity-base-dropdown__container-outer";
        private const string DropdownMenuInnerClassName = "unity-base-dropdown__container-inner";
        private const string DropdownMenuItemClassName = "unity-base-dropdown__item";

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
            DropdownField dropdown = target.Q<DropdownField>("gsp-display-dropdown");
            Assert.That(dropdown, Is.Not.Null, "必须能加载 gsp-display-dropdown。");

            yield return WaitUntilReady(target, 60);
            yield return CaptureScreenStep(
                $"global-settings-dropdown-{optionCount}-selected",
                null,
                $"full-screen; options={optionCount}; selected={dropdown.value}");
        }

        private IEnumerator CaptureDropdownExpandedState(VisualElement panelRoot, int optionCount)
        {
            VisualElement target = BuildPanel(panelRoot, optionCount, selectedIndex: 0);
            DropdownField dropdown = target.Q<DropdownField>("gsp-display-dropdown");
            Assert.That(dropdown, Is.Not.Null, "必须能加载 gsp-display-dropdown。");

            yield return WaitUntilReady(target, 60);
            yield return OpenDropdown(dropdown, panelRoot, optionCount);
            yield return CaptureScreenStep(
                $"global-settings-dropdown-{optionCount}-expanded",
                null,
                $"full-screen; options={optionCount}; expanded");
        }

        private static VisualElement BuildPanel(VisualElement panelRoot, int optionCount, int selectedIndex)
        {
            RemoveDropdownMenus(panelRoot);

            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(GlobalSettingsPanelPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>("gsp-root");
            Assert.That(target, Is.Not.Null, "必须能加载 GlobalSettingsPanel.uxml 的 gsp-root。");
            target.style.width = 572;

            DropdownField dropdown = target.Q<DropdownField>("gsp-display-dropdown");
            Assert.That(dropdown, Is.Not.Null, "必须能加载 gsp-display-dropdown。");

            List<string> choices = BuildDisplayChoices(optionCount);
            dropdown.choices = choices;
            int safeIndex = Mathf.Clamp(selectedIndex, 0, choices.Count - 1);
            dropdown.SetValueWithoutNotify(choices[safeIndex]);

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

        private static IEnumerator OpenDropdown(DropdownField dropdown, VisualElement panelRoot, int expectedItemCount)
        {
            MethodInfo showMenu = typeof(DropdownField).GetMethod(
                "ShowMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showMenu, Is.Not.Null, "Unity DropdownField 必须能通过 ShowMenu 展开菜单。");

            showMenu.Invoke(dropdown, null);
            yield return WaitUntilMenuReady(panelRoot, expectedItemCount, 30);
        }

        private static IEnumerator WaitUntilMenuReady(
            VisualElement panelRoot,
            int expectedItemCount,
            int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                VisualElement menu = panelRoot.Query<VisualElement>(className: DropdownMenuClassName).First();
                int itemCount = panelRoot.Query<VisualElement>(className: DropdownMenuItemClassName).ToList().Count;
                if (menu != null && menu.worldBound.width > 0f && itemCount == expectedItemCount)
                {
                    yield break;
                }

                yield return null;
            }

            int actualCount = panelRoot.Query<VisualElement>(className: DropdownMenuItemClassName).ToList().Count;
            Assert.Fail($"等待下拉菜单展开超时：expected={expectedItemCount}, actual={actualCount}");
        }

        private static void RemoveDropdownMenus(VisualElement panelRoot)
        {
            VisualElement searchRoot = GetPanelVisualTree(panelRoot) ?? panelRoot;
            RemoveDropdownElements(searchRoot, DropdownMenuItemClassName);
            RemoveDropdownElements(searchRoot, DropdownMenuInnerClassName);
            RemoveDropdownElements(searchRoot, DropdownMenuOuterClassName);
            RemoveDropdownElements(searchRoot, DropdownMenuClassName);
        }

        private static VisualElement GetPanelVisualTree(VisualElement element)
        {
            if (element.panel == null)
            {
                return null;
            }

            PropertyInfo visualTree = element.panel.GetType().GetProperty(
                "visualTree",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return visualTree?.GetValue(element.panel) as VisualElement;
        }

        private static void RemoveDropdownElements(VisualElement searchRoot, string className)
        {
            List<VisualElement> elements = searchRoot.Query<VisualElement>(className: className).ToList();
            foreach (VisualElement element in elements)
            {
                if (element.hierarchy.parent != null)
                {
                    element.RemoveFromHierarchy();
                }
            }
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
