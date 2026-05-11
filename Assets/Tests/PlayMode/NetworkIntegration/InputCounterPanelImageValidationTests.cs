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
    /// <summary>
    /// 视觉回归：本次新增的 InputCounterPanel（Pencil ZmuFh）。
    /// 默认状态：KeyCounterPill(Space/47) + pin(置顶) + footer(VS Code)。
    /// 渲染整屏，由 unity-visual-image-validation 离线对比设计稿。
    /// </summary>
    [TestFixture]
    public sealed class InputCounterPanelImageValidationTests : VisualImageTestBase
    {
        private const string PanelSettingsPath      = "Assets/UI_V2/PanelSettings_Settings.asset";
        private const string InputCounterPanelPath  = "Assets/UI_V2/Documents/InputCounterPanel.uxml";

        private GameObject _host;
        private GameObject _cameraHost;

        [TearDown]
        public void TearDownHost()
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
        public IEnumerator InputCounterPanel_ShouldRenderDefault()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            CreateBlackBackgroundCamera("VisualTestCamera_InputCounter");
            VisualElement panelRoot = CreateRuntimePanelRoot();
            yield return null;

            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(InputCounterPanelPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>("icp-root");
            Assert.That(target, Is.Not.Null, "必须能加载 InputCounterPanel.uxml 的 icp-root。");

            // 结构断言：KeyCounterPill 实例 + pin + app
            Label keyLabel   = target.Q<Label>("key-counter-pill-key");
            Label countLabel = target.Q<Label>("key-counter-pill-count");
            VisualElement pin = target.Q<VisualElement>("icp-pin-btn");
            VisualElement appIcon = target.Q<VisualElement>("icp-app-icon");
            Label appText = target.Q<Label>("icp-app-text");

            Assert.That(keyLabel,   Is.Not.Null);
            Assert.That(countLabel, Is.Not.Null);
            Assert.That(pin,        Is.Not.Null);
            Assert.That(appIcon,    Is.Not.Null);
            Assert.That(appText,    Is.Not.Null);
            Assert.That(keyLabel.text,   Is.EqualTo("Space"));
            Assert.That(countLabel.text, Is.EqualTo("47"));
            Assert.That(appText.text,    Is.EqualTo("VS Code"));

            yield return WaitUntilReady(target, 60);
            yield return CaptureScreenStep(
                "input-counter-panel-default",
                null,
                "full-screen; InputCounterPanel: Space/47 + pin + VS Code");

            AssertCaptureArtifact("01-input-counter-panel-default-actual.png");
#endif
        }

        [UnityTest]
        public IEnumerator InputCounterPanel_ShouldRenderMouseLeftIcon()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            yield return RenderMouseVariant("鼠标左键", "input-counter-panel-mouse-left",
                "01-input-counter-panel-mouse-left-actual.png");
#endif
        }

        [UnityTest]
        public IEnumerator InputCounterPanel_ShouldRenderMouseRightIcon()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            yield return RenderMouseVariant("鼠标右键", "input-counter-panel-mouse-right",
                "01-input-counter-panel-mouse-right-actual.png");
#endif
        }

        [UnityTest]
        public IEnumerator InputCounterPanel_ShouldRenderMouseMiddleIcon()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            yield return RenderMouseVariant("鼠标中键", "input-counter-panel-mouse-middle",
                "01-input-counter-panel-mouse-middle-actual.png");
#endif
        }

#if UNITY_EDITOR
        // 加载 InputCounterPanel.uxml，把 KeyCounterPill 的 keyLabel 设成指定鼠标按键
        // 并调用与 Controller 相同的 ApplyKeyBadgeMouseClass，再整屏截图。
        private IEnumerator RenderMouseVariant(string keyLabel, string stepName, string artifact)
        {
            CreateBlackBackgroundCamera("VisualTestCamera_InputCounter_" + stepName);
            VisualElement panelRoot = CreateRuntimePanelRoot();
            yield return null;

            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(InputCounterPanelPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>("icp-root");
            Assert.That(target, Is.Not.Null, "必须能加载 InputCounterPanel.uxml 的 icp-root。");

            Label key   = target.Q<Label>("key-counter-pill-key");
            Label count = target.Q<Label>("key-counter-pill-count");
            VisualElement badge = target.Q<VisualElement>("key-counter-pill-badge");
            VisualElement icon  = target.Q<VisualElement>("key-counter-pill-icon");
            Assert.That(badge, Is.Not.Null, "keyBadge 元素必须存在。");
            Assert.That(icon,  Is.Not.Null, "keyBadge 应包含 key-counter-pill-icon 占位。");

            // 与 Controller 一致：写 keyLabel + 切类
            key.text   = keyLabel;
            count.text = "47";
            PlayerCardView.ApplyKeyBadgeMouseClass(badge, keyLabel);

            yield return WaitUntilReady(target, 60);
            yield return CaptureScreenStep(stepName, null,
                $"full-screen; InputCounterPanel: {keyLabel} → mouse icon + 47");

            AssertCaptureArtifact(artifact);
        }

#endif

#if UNITY_EDITOR
        private void CreateBlackBackgroundCamera(string name)
        {
            _cameraHost = new GameObject(name);
            Camera cam = _cameraHost.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.depth = 100f;
            cam.orthographic = true;
            cam.cullingMask = 0;
        }

        private VisualElement CreateRuntimePanelRoot()
        {
            _host = new GameObject("InputCounterPanelImageValidationRoot");
            UIDocument document = _host.AddComponent<UIDocument>();
            PanelSettings panelSettings = Object.Instantiate(LoadAsset<PanelSettings>(PanelSettingsPath));
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

        private void AssertCaptureArtifact(string fileName)
        {
            string path = Path.Combine(CurrentRunDirectory, fileName);
            Assert.That(File.Exists(path), Is.True, $"截图产物不存在：{path}");
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
