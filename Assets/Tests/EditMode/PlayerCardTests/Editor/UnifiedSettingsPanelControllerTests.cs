using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    public sealed class UnifiedSettingsPanelControllerTests
    {
        // Task 15 重构后 settings-overlay 已从 DeskWindow 迁到独立 UnifiedSettingsPanel.uxml
        private const string UnifiedSettingsPanelPath = "Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml";
        private const string PomodoroPanelPath = "Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml";
        private const string OnlinePanelPath = "Assets/UI_V2/Documents/OnlineSettingsPanel.uxml";
        private const string PetPanelPath = "Assets/UI_V2/Documents/PetSettingsPanel.uxml";
        private const string UnsavedDialogPath = "Assets/UI_V2/Documents/UnsavedChangesDialog.uxml";

        private GameObject _lifecycleOwner;

        [SetUp]
        public void SetUp()
        {
            _lifecycleOwner = new GameObject("UnifiedSettingsPanelControllerTests");
            _ = APP.Pomodoro.GameApp.Interface;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_lifecycleOwner);
        }

        [Test]
        public void Init_LoadsPomodoroPanelIntoSingleHost()
        {
            var controller = new UnifiedSettingsPanelController();
            VisualElement root = CreateUnifiedSettingsPanelRoot();

            controller.Init(
                root,
                GameApp.Interface.GetModel<IPomodoroModel>(),
                GameApp.Interface.GetModel<IRoomModel>(),
                LoadTemplate(PomodoroPanelPath),
                LoadTemplate(OnlinePanelPath),
                LoadTemplate(PetPanelPath),
                LoadTemplate(UnsavedDialogPath),
                _lifecycleOwner);

            VisualElement host = root.Q<VisualElement>("settings-content-host");

            Assert.That(host, Is.Not.Null, "UnifiedSettingsPanel 必须提供单一 settings-content-host 作为动态内容挂载槽。");
            Assert.That(host.childCount, Is.EqualTo(1), "初始化后只应挂载一个当前 tab 面板。");
            Assert.That(host.Q<VisualElement>("psp-root"), Is.Not.Null, "默认应加载番茄钟设置面板。");
            Assert.That(host.Q<VisualElement>("osp-root"), Is.Null, "联机面板不应在初始化时同时常驻。");
            Assert.That(host.Q<VisualElement>("pet-root"), Is.Null, "宠物面板不应在初始化时同时常驻。");
        }

        [Test]
        public void SelectTab_ReplacesCurrentHostContent()
        {
            var controller = new UnifiedSettingsPanelController();
            VisualElement root = CreateUnifiedSettingsPanelRoot();

            controller.Init(
                root,
                GameApp.Interface.GetModel<IPomodoroModel>(),
                GameApp.Interface.GetModel<IRoomModel>(),
                LoadTemplate(PomodoroPanelPath),
                LoadTemplate(OnlinePanelPath),
                LoadTemplate(PetPanelPath),
                LoadTemplate(UnsavedDialogPath),
                _lifecycleOwner);

            controller.SelectTab("online");

            VisualElement host = root.Q<VisualElement>("settings-content-host");
            Assert.That(host, Is.Not.Null);
            Assert.That(host.childCount, Is.EqualTo(1), "切换到联机 tab 后仍应只保留一个面板实例。");
            Assert.That(host.Q<VisualElement>("psp-root"), Is.Null, "切换后旧的番茄钟面板应被移除。");
            Assert.That(host.Q<VisualElement>("osp-root"), Is.Not.Null, "切换到联机 tab 后应挂载联机设置面板。");
            Assert.That(root.Q<VisualElement>("tab-online").ClassListContains("sidebar-tab--active"), Is.True);
            Assert.That(root.Q<VisualElement>("tab-pomodoro").ClassListContains("sidebar-tab--active"), Is.False);

            controller.SelectTab("pet");

            Assert.That(host.childCount, Is.EqualTo(1), "切换到宠物 tab 后仍应只保留一个面板实例。");
            Assert.That(host.Q<VisualElement>("osp-root"), Is.Null, "切换后旧的联机面板应被移除。");
            Assert.That(host.Q<VisualElement>("pet-root"), Is.Not.Null, "切换到宠物 tab 后应挂载宠物设置面板。");
            Assert.That(root.Q<VisualElement>("tab-pet").ClassListContains("sidebar-tab--active"), Is.True);
            Assert.That(root.Q<VisualElement>("tab-online").ClassListContains("sidebar-tab--active"), Is.False);
        }

        private static VisualElement CreateUnifiedSettingsPanelRoot()
        {
            var root = new VisualElement();
            LoadTemplate(UnifiedSettingsPanelPath).CloneTree(root);
            return root;
        }

        private static VisualTreeAsset LoadTemplate(string path)
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, $"无法加载模板：{path}");
            return asset;
        }
    }
}
