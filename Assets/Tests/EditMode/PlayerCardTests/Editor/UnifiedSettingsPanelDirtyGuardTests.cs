using System.Reflection;
using APP.Network.Model;
using APP.Pomodoro.Controller;
using APP.Pomodoro.Model;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Tests
{
    /// <summary>
    /// 验证 UnifiedSettingsPanelController 的"未保存更改"守卫：
    /// - 关闭 / 切 tab 前检查 PomodoroSettingsPanelController.IsDirty
    /// - dirty 时弹 UnsavedChangesDialog，用户选择"保存并继续"执行原动作，选"取消"则留在当前面板
    /// </summary>
    public sealed class UnifiedSettingsPanelDirtyGuardTests
    {
        private const string UnifiedSettingsPanelPath = "Assets/UI_V2/Documents/UnifiedSettingsPanel.uxml";
        private const string PomodoroPanelPath = "Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml";
        private const string OnlinePanelPath = "Assets/UI_V2/Documents/OnlineSettingsPanel.uxml";
        private const string PetPanelPath = "Assets/UI_V2/Documents/PetSettingsPanel.uxml";
        private const string UnsavedDialogPath = "Assets/UI_V2/Documents/UnsavedChangesDialog.uxml";

        private GameObject _lifecycleOwner;
        private IPomodoroModel _model;
        private int _originalFocusSeconds;
        private int _originalBreakSeconds;
        private bool _originalAutoJumpToTop;

        [SetUp]
        public void SetUp()
        {
            _lifecycleOwner = new GameObject(nameof(UnifiedSettingsPanelDirtyGuardTests));
            _model = GameApp.Interface.GetModel<IPomodoroModel>();
            _originalFocusSeconds = _model.FocusDurationSeconds.Value;
            _originalBreakSeconds = _model.BreakDurationSeconds.Value;
            _originalAutoJumpToTop = _model.AutoJumpToTopOnComplete.Value;
        }

        [TearDown]
        public void TearDown()
        {
            _model.FocusDurationSeconds.Value = _originalFocusSeconds;
            _model.BreakDurationSeconds.Value = _originalBreakSeconds;
            _model.AutoJumpToTopOnComplete.Value = _originalAutoJumpToTop;
            Object.DestroyImmediate(_lifecycleOwner);
        }

        // ─── 关闭路径 ────────────────────────────────────────────

        [Test]
        public void RequestClose_Clean_HidesImmediatelyAndRunsCallback()
        {
            (UnifiedSettingsPanelController controller, VisualElement _) = BuildPanel();
            controller.Show();
            Assert.That(controller.IsVisible, Is.True);

            bool closed = false;
            controller.RequestClose(() => closed = true);

            Assert.That(closed, Is.True, "Clean 状态下应立即回调 onCloseConfirmed");
            Assert.That(controller.IsVisible, Is.False, "Clean 状态下应立即隐藏 overlay");
            Assert.That(controller.IsUnsavedDialogVisible, Is.False, "不应弹出未保存对话框");
        }

        [Test]
        public void RequestClose_Dirty_ShowsDialogAndKeepsOverlayVisible()
        {
            (UnifiedSettingsPanelController controller, VisualElement root) = BuildPanel();
            controller.Show();
            MarkPomodoroDirty(controller, root);

            bool closed = false;
            controller.RequestClose(() => closed = true);

            Assert.That(controller.IsUnsavedDialogVisible, Is.True, "dirty 时应弹出未保存对话框");
            Assert.That(controller.IsVisible, Is.True, "用户未抉择前 overlay 不应被隐藏");
            Assert.That(closed, Is.False, "回调在确认前不应触发");
        }

        [Test]
        public void RequestClose_DialogConfirm_AppliesDraftAndHidesAndRunsCallback()
        {
            (UnifiedSettingsPanelController controller, VisualElement root) = BuildPanel();
            controller.Show();
            MarkPomodoroDirty(controller, root);

            bool closed = false;
            controller.RequestClose(() => closed = true);
            InvokeDialogConfirm(controller);

            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(40 * 60),
                "点保存并继续应把草稿 Apply 到 Model");
            Assert.That(controller.IsVisible, Is.False, "确认后 overlay 应关闭");
            Assert.That(closed, Is.True, "确认后 onCloseConfirmed 应被调");
            Assert.That(controller.IsUnsavedDialogVisible, Is.False);
        }

        [Test]
        public void RequestClose_DialogCancel_KeepsOverlayAndDraft()
        {
            (UnifiedSettingsPanelController controller, VisualElement root) = BuildPanel();
            controller.Show();
            MarkPomodoroDirty(controller, root);

            bool closed = false;
            controller.RequestClose(() => closed = true);
            InvokeDialogCancel(controller);

            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(_originalFocusSeconds),
                "取消不应写 Model");
            Assert.That(controller.IsVisible, Is.True, "取消后 overlay 应保持浮现");
            Assert.That(closed, Is.False, "取消时 onCloseConfirmed 不应被调");
            Assert.That(controller.IsUnsavedDialogVisible, Is.False, "对话框自身在取消后应关闭");
            Assert.That(GetPomodoroController(controller).IsDirty, Is.True,
                "取消不应清除草稿");
        }

        // ─── Tab 切换路径 ────────────────────────────────────────

        [Test]
        public void SelectTab_Clean_SwitchesImmediately()
        {
            (UnifiedSettingsPanelController controller, VisualElement root) = BuildPanel();
            controller.SelectTab("online");

            VisualElement host = root.Q<VisualElement>("settings-content-host");
            Assert.That(host.Q<VisualElement>("osp-root"), Is.Not.Null);
            Assert.That(controller.IsUnsavedDialogVisible, Is.False);
        }

        [Test]
        public void SelectTab_DirtyPomodoro_ShowsDialogAndDeferredSwitch()
        {
            (UnifiedSettingsPanelController controller, VisualElement root) = BuildPanel();
            MarkPomodoroDirty(controller, root);

            controller.SelectTab("online");

            Assert.That(controller.IsUnsavedDialogVisible, Is.True,
                "从 dirty 的 pomodoro tab 切到其它 tab 应弹出对话框");

            VisualElement host = root.Q<VisualElement>("settings-content-host");
            Assert.That(host.Q<VisualElement>("psp-root"), Is.Not.Null,
                "用户抉择前 tab 不应切换");
            Assert.That(host.Q<VisualElement>("osp-root"), Is.Null);

            InvokeDialogConfirm(controller);

            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(40 * 60),
                "确认后草稿应被 Apply 到 Model");
            Assert.That(host.Q<VisualElement>("osp-root"), Is.Not.Null,
                "确认后 tab 应切到联机");
            Assert.That(host.Q<VisualElement>("psp-root"), Is.Null);
        }

        [Test]
        public void SelectTab_DirtyPomodoro_Cancel_KeepsPomodoroTab()
        {
            (UnifiedSettingsPanelController controller, VisualElement root) = BuildPanel();
            MarkPomodoroDirty(controller, root);

            controller.SelectTab("online");
            InvokeDialogCancel(controller);

            VisualElement host = root.Q<VisualElement>("settings-content-host");
            Assert.That(host.Q<VisualElement>("psp-root"), Is.Not.Null,
                "取消后仍应留在番茄钟 tab");
            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(_originalFocusSeconds));
            Assert.That(GetPomodoroController(controller).IsDirty, Is.True);
        }

        // ─── Helpers ─────────────────────────────────────────────

        private (UnifiedSettingsPanelController controller, VisualElement root) BuildPanel()
        {
            var controller = new UnifiedSettingsPanelController();
            var root = new VisualElement();
            LoadTemplate(UnifiedSettingsPanelPath).CloneTree(root);

            controller.Init(
                root,
                GameApp.Interface.GetModel<IPomodoroModel>(),
                GameApp.Interface.GetModel<IRoomModel>(),
                LoadTemplate(PomodoroPanelPath),
                LoadTemplate(OnlinePanelPath),
                LoadTemplate(PetPanelPath),
                LoadTemplate(UnsavedDialogPath),
                _lifecycleOwner);

            return (controller, root);
        }

        private static VisualTreeAsset LoadTemplate(string path)
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, $"无法加载模板：{path}");
            return asset;
        }

        /// <summary>把番茄钟面板的 focus 草稿改成 40 分钟，使其 dirty。</summary>
        private static void MarkPomodoroDirty(UnifiedSettingsPanelController unified, VisualElement root)
        {
            TextField focus = root.Q<TextField>("psp-focus-value");
            Assert.That(focus, Is.Not.Null, "psp-focus-value 必须存在");
            focus.value = "40";

            PomodoroSettingsPanelController pomo = GetPomodoroController(unified);
            pomo.ForceCommitPendingEdits();
            Assert.That(pomo.IsDirty, Is.True, "MarkPomodoroDirty 后 Controller 应 dirty");
        }

        private static PomodoroSettingsPanelController GetPomodoroController(UnifiedSettingsPanelController unified)
        {
            FieldInfo field = typeof(UnifiedSettingsPanelController)
                .GetField("_pomodoroSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "_pomodoroSettings 字段必须存在");
            var pomo = (PomodoroSettingsPanelController)field.GetValue(unified);
            Assert.That(pomo, Is.Not.Null, "pomodoro 子控制器应已初始化");
            return pomo;
        }

        private static void InvokeDialogConfirm(UnifiedSettingsPanelController unified)
        {
            UnsavedChangesDialogController dlg = GetDialog(unified);
            MethodInfo handle = typeof(UnsavedChangesDialogController)
                .GetMethod("HandleConfirm", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handle, Is.Not.Null, "HandleConfirm 方法应存在");
            handle.Invoke(dlg, null);
        }

        private static void InvokeDialogCancel(UnifiedSettingsPanelController unified)
        {
            UnsavedChangesDialogController dlg = GetDialog(unified);
            MethodInfo handle = typeof(UnsavedChangesDialogController)
                .GetMethod("HandleCancel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handle, Is.Not.Null, "HandleCancel 方法应存在");
            handle.Invoke(dlg, null);
        }

        private static UnsavedChangesDialogController GetDialog(UnifiedSettingsPanelController unified)
        {
            FieldInfo field = typeof(UnifiedSettingsPanelController)
                .GetField("_unsavedDialog", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "_unsavedDialog 字段应存在");
            var dlg = (UnsavedChangesDialogController)field.GetValue(unified);
            Assert.That(dlg, Is.Not.Null, "对话框子控制器应已初始化");
            return dlg;
        }
    }
}
