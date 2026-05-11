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
    /// 视觉回归（多绑定改版后）：
    ///   1) GlobalSettingsPanel 默认态：toggle on + 描述 + 空列表 + 添加按钮
    ///   2) DisplayMenu 展开不被 binding-card 覆盖（z 序）
    ///   3) PlayerCard 用 KeyCounterPill 组件实例
    /// 单 listener / listening 的旧视觉测试已删除：UI 改成 list，相关结构归 BindingKeyRow 组件单测覆盖。
    /// </summary>
    [TestFixture]
    public sealed class BindingKeyAndPlayerCardImageValidationTests : VisualImageTestBase
    {
        private const string PanelSettingsPath        = "Assets/UI_V2/PanelSettings_Settings.asset";
        private const string GlobalSettingsPanelPath  = "Assets/UI_V2/Documents/GlobalSettingsPanel.uxml";
        private const string PlayerCardPath           = "Assets/UI_V2/Documents/PlayerCard.uxml";
        private const string MenuHiddenClass          = "comp-input-dropdown-menu--hidden";

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
        public IEnumerator GlobalSettingsPanel_ShouldRenderBindingListEmpty()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            CreateBlackBackgroundCamera("VisualTestCamera_BindingListEmpty");
            VisualElement root = CreateRuntimePanelRoot();
            yield return null;

            VisualElement target = LoadGspPanel(root);

            // 新结构断言：toggle + list 容器 + 添加按钮
            Toggle toggle         = target.Q<Toggle>("gsp-binding-toggle");
            VisualElement list    = target.Q<VisualElement>("gsp-binding-list");
            Button addBtn         = target.Q<Button>("gsp-binding-add-btn");
            Assert.That(toggle, Is.Not.Null);
            Assert.That(list,   Is.Not.Null);
            Assert.That(addBtn, Is.Not.Null);
            Assert.That(list.childCount, Is.EqualTo(0), "默认无 row 渲染（测试期未跑 Controller）。");

            yield return WaitUntilReady(target, 60);
            yield return CaptureScreenStep(
                "global-settings-binding-list-empty",
                null,
                "full-screen; binding list 空 + 添加按钮可见");

            AssertCaptureArtifact("01-global-settings-binding-list-empty-actual.png");
#endif
        }

        [UnityTest]
        public IEnumerator GlobalSettingsPanel_BindingList_ShouldStackRowsVertically()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            CreateBlackBackgroundCamera("VisualTestCamera_BindingListRows");
            VisualElement root = CreateRuntimePanelRoot();
            yield return null;

            VisualElement target = LoadGspPanel(root);
            VisualElement list = target.Q<VisualElement>("gsp-binding-list");
            Assert.That(list, Is.Not.Null);

            // 模拟 Controller 行为：加载 BindingKeyRow 模板，剥 TemplateContainer 后直接挂到 list
            VisualTreeAsset rowTpl = LoadAsset<VisualTreeAsset>("Assets/UI_V2/Documents/Components/BindingKeyRow.uxml");
            string[] keys   = { "鼠标左键", "Space", "F" };
            string[] hints  = { "点击重新绑定", "点击重新绑定 · 已启用", "点击重新绑定" };
            bool[]   synced = { false, true,  false };
            for (int i = 0; i < keys.Length; i++)
            {
                var container = rowTpl.CloneTree();
                var row = container.Q<VisualElement>(className: "comp-binding-key-row") ?? container;
                if (row != container && row.parent != null) row.parent.Remove(row);
                row.Q<Label>("bk-row-key").text  = keys[i];
                row.Q<Label>("bk-row-hint").text = hints[i];
                if (synced[i]) row.AddToClassList("comp-binding-key-row--synced");
                list.Add(row);
            }

            yield return WaitUntilReady(target, 60);

            // 行高 36 + margin-bottom 8 → 每行 ~44px；3 行总高 ~132px。粗略校验 list 实际渲染了正确高度。
            Assert.That(list.resolvedStyle.height, Is.GreaterThanOrEqualTo(36f * 3f),
                $"3 行应至少叠 108px 高，实测 {list.resolvedStyle.height:F1}。");

            yield return CaptureScreenStep(
                "global-settings-binding-list-3-rows",
                null,
                "full-screen; 3 个 BindingKeyRow（鼠标左键/Space[synced]/F），应纵向堆叠");

            AssertCaptureArtifact("01-global-settings-binding-list-3-rows-actual.png");
#endif
        }

        [UnityTest]
        public IEnumerator GlobalSettingsPanel_DisplayMenuExpanded_ShouldNotBeBlockedByBindingCard()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            CreateBlackBackgroundCamera("VisualTestCamera_DisplayMenuOnTop");
            VisualElement root = CreateRuntimePanelRoot();
            yield return null;

            VisualElement target = LoadGspPanel(root);
            VisualElement menu  = target.Q<VisualElement>("gsp-display-menu");
            VisualElement trigg = target.Q<VisualElement>("gsp-display-dropdown");
            Assert.That(menu,  Is.Not.Null);
            Assert.That(trigg, Is.Not.Null);

            Assert.That(menu.parent, Is.EqualTo(target),
                "gsp-display-menu 必须挂在 gsp-root 下（popup layer）。");
            int menuIndex = target.IndexOf(menu);
            Assert.That(menuIndex, Is.EqualTo(target.childCount - 1),
                "gsp-display-menu 必须是 gsp-root 最后一个子节点。");

            yield return WaitUntilReady(target, 60);

            menu.Clear();
            for (int i = 1; i <= 3; i++)
            {
                VisualElement item = new VisualElement();
                item.AddToClassList("comp-input-dropdown-menu-item");
                if (i == 1) item.AddToClassList("comp-input-dropdown-menu-item--selected");
                Label l = new Label($"显示器 {i}");
                l.AddToClassList("comp-input-dropdown-menu-item-label");
                item.Add(l);
                menu.Add(item);
            }

            Label valueLabel = target.Q<Label>("gsp-display-dropdown-value");
            if (valueLabel != null) valueLabel.text = "显示器 1";

            menu.RemoveFromClassList(MenuHiddenClass);

            yield return RepositionMenuInTest(menu, trigg, target, 60);

            yield return CaptureScreenStep(
                "global-settings-display-menu-expanded-on-top",
                null,
                "full-screen; display-menu 展开不被 binding-card 覆盖");

            AssertCaptureArtifact("01-global-settings-display-menu-expanded-on-top-actual.png");
#endif
        }

        [UnityTest]
        public IEnumerator PlayerCard_ShouldRenderKeyCounterPill()
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            CreateBlackBackgroundCamera("VisualTestCamera_PlayerCard");
            VisualElement panelRoot = CreateRuntimePanelRoot();
            yield return null;

            VisualTreeAsset template = LoadAsset<VisualTreeAsset>(PlayerCardPath);
            TemplateContainer container = template.CloneTree();
            panelRoot.Clear();
            panelRoot.Add(container);

            VisualElement target = container.Q<VisualElement>(className: "pc-root");
            Assert.That(target, Is.Not.Null);

            Label keyLabel    = target.Q<Label>("key-counter-pill-key");
            Label countLabel  = target.Q<Label>("key-counter-pill-count");
            VisualElement pin = target.Q<VisualElement>("pc-pin-btn");
            Assert.That(keyLabel,   Is.Not.Null);
            Assert.That(countLabel, Is.Not.Null);
            Assert.That(pin,        Is.Not.Null);
            Assert.That(keyLabel.text,   Is.EqualTo("Space"));
            Assert.That(countLabel.text, Is.EqualTo("47"));

            yield return WaitUntilReady(target, 60);
            yield return CaptureScreenStep(
                "player-card-key-counter-pill",
                null,
                "full-screen; PlayerCard keyCounterPill (Space / 47)");

            AssertCaptureArtifact("01-player-card-key-counter-pill-actual.png");
#endif
        }

#if UNITY_EDITOR
        private VisualElement LoadGspPanel(VisualElement panelRoot)
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

        private static IEnumerator RepositionMenuInTest(
            VisualElement menu, VisualElement trigger, VisualElement anchor, int frameLimit)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                if (trigger.worldBound.width > 0f && anchor.worldBound.width > 0f)
                {
                    Rect tb = trigger.worldBound;
                    Rect ab = anchor.worldBound;
                    menu.style.left  = tb.x - ab.x;
                    menu.style.top   = tb.y - ab.y + tb.height + 4f;
                    menu.style.width = tb.width;
                    yield return null;
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("等待 trigger / anchor 布局就绪超时。");
        }

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
            _host = new GameObject("BindingKeyAndPlayerCardImageValidationRoot");
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
