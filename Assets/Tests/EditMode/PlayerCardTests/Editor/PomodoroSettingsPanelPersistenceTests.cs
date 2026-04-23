using System.Reflection;
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
    /// 验证番茄钟设置面板的"草稿 → 应用"通路：
    /// TextField.value 修改 + Commit* → 草稿（不落 Model） → TryApply → Cmd_PomodoroApplySettings → Model 更新。
    /// </summary>
    public sealed class PomodoroSettingsPanelPersistenceTests
    {
        private const string PomodoroPanelPath = "Assets/UI_V2/Documents/PomodoroSettingsPanel.uxml";

        private GameObject _lifecycleOwner;
        private IPomodoroModel _model;
        private int _originalFocusSeconds;
        private int _originalBreakSeconds;
        private bool _originalAutoJumpToTop;

        [SetUp]
        public void SetUp()
        {
            _lifecycleOwner = new GameObject(nameof(PomodoroSettingsPanelPersistenceTests));
            _model = GameApp.Interface.GetModel<IPomodoroModel>();
            _originalFocusSeconds = _model.FocusDurationSeconds.Value;
            _originalBreakSeconds = _model.BreakDurationSeconds.Value;
            _originalAutoJumpToTop = _model.AutoJumpToTopOnComplete.Value;
        }

        [TearDown]
        public void TearDown()
        {
            // Model 是单例，恢复初值避免串测
            _model.FocusDurationSeconds.Value = _originalFocusSeconds;
            _model.BreakDurationSeconds.Value = _originalBreakSeconds;
            _model.AutoJumpToTopOnComplete.Value = _originalAutoJumpToTop;
            Object.DestroyImmediate(_lifecycleOwner);
        }

        // ─── 草稿通路 ────────────────────────────────────────────

        [Test]
        public void CommitFocusValue_MutatesDraftOnly_DoesNotWriteModel()
        {
            (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
                = BuildPanel();

            TextField focus = root.Q<TextField>("psp-focus-value");
            Assert.That(focus, Is.Not.Null, "psp-focus-value TextField 必须存在");

            focus.value = "40";
            view.CommitFocusValue();

            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(_originalFocusSeconds),
                "Commit 不再立即写 Model，必须等 TryApply");
            Assert.That(controller.IsDirty, Is.True,
                "草稿与 Model 不一致时 IsDirty 应为 true");
        }

        [Test]
        public void TryApply_DirtyFocus_SendsCommandAndClearsDirty()
        {
            (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
                = BuildPanel();

            TextField focus = root.Q<TextField>("psp-focus-value");
            focus.value = "40";
            view.CommitFocusValue();
            Assert.That(controller.IsDirty, Is.True);

            controller.TryApply();

            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(40 * 60),
                "TryApply 后 Model.FocusDurationSeconds 应为 40 分钟 = 2400 秒");
            Assert.That(controller.IsDirty, Is.False,
                "TryApply 后草稿基线已对齐，IsDirty 应归零");
        }

        [Test]
        public void TryApply_Clean_IsNoop()
        {
            (PomodoroSettingsPanelController controller, _, _) = BuildPanel();

            int before = _model.FocusDurationSeconds.Value;
            controller.TryApply();

            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(before),
                "无草稿改动时 TryApply 不应写 Model");
            Assert.That(controller.IsDirty, Is.False);
        }

        [Test]
        public void CommitBreakValue_MutatesDraftOnly_UntilTryApply()
        {
            (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
                = BuildPanel();

            TextField breakField = root.Q<TextField>("psp-break-value");
            breakField.value = "10";
            view.CommitBreakValue();

            Assert.That(_model.BreakDurationSeconds.Value, Is.EqualTo(_originalBreakSeconds));
            Assert.That(controller.IsDirty, Is.True);

            controller.TryApply();

            Assert.That(_model.BreakDurationSeconds.Value, Is.EqualTo(10 * 60));
            Assert.That(_model.FocusDurationSeconds.Value, Is.EqualTo(_originalFocusSeconds),
                "只改 break 时 FocusDurationSeconds 不应变动");
        }

        [Test]
        public void CommitFocusValue_InvalidInput_DoesNotMarkDirty()
        {
            (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
                = BuildPanel();

            TextField focus = root.Q<TextField>("psp-focus-value");
            string preFocusText = focus.value;

            focus.value = "";
            view.CommitFocusValue();

            Assert.That(controller.IsDirty, Is.False,
                "空字符串属非法输入，草稿不应被污染");
            Assert.That(focus.value, Is.EqualTo(preFocusText),
                "非法输入应回滚 TextField 显示到最近一次 Refresh 的值");

            focus.value = "0";
            view.CommitFocusValue();
            Assert.That(controller.IsDirty, Is.False,
                "focus=0 不合法（下限为 1 分钟），草稿不应被污染");
        }

        [Test]
        public void CommitFocusValue_SameValue_DoesNotBecomeDirty()
        {
            (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
                = BuildPanel();

            TextField focus = root.Q<TextField>("psp-focus-value");

            // 首次设为 40 → dirty
            focus.value = "40";
            view.CommitFocusValue();
            Assert.That(controller.IsDirty, Is.True);

            // 应用后基线对齐
            controller.TryApply();
            Assert.That(controller.IsDirty, Is.False);

            // 再次 Commit 同值 → 不应重新 dirty
            view.CommitFocusValue();
            Assert.That(controller.IsDirty, Is.False, "相同值 Commit 不应再次标脏");
        }

        // ─── 应用按钮显隐 ────────────────────────────────────────

        [Test]
        public void DraftChange_ShowsApplyButton_TryApplyHidesIt()
        {
            (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
                = BuildPanel();
            Button applyBtn = root.Q<Button>("psp-apply-btn");
            Assert.That(applyBtn, Is.Not.Null, "psp-apply-btn 按钮必须存在");

            // 初始隐藏
            Assert.That(applyBtn.ClassListContains("psp-apply-btn--hidden"), Is.True,
                "初始 Refresh 后应用按钮应隐藏");

            TextField focus = root.Q<TextField>("psp-focus-value");
            focus.value = "40";
            view.CommitFocusValue();

            Assert.That(applyBtn.ClassListContains("psp-apply-btn--hidden"), Is.False,
                "草稿与基线不一致时应用按钮应浮出");

            controller.TryApply();

            Assert.That(applyBtn.ClassListContains("psp-apply-btn--hidden"), Is.True,
                "TryApply 后应用按钮应再次隐藏");
        }

        [Test]
        public void HintToggle_FlipsApplyVisibilityAndIsDirty()
        {
            (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
                = BuildPanel();
            Toggle hint = root.Q<Toggle>("psp-hint-toggle");
            Button applyBtn = root.Q<Button>("psp-apply-btn");
            Assert.That(hint, Is.Not.Null);

            bool originalHint = hint.value;
            view.CommitHintToggle(!originalHint);

            Assert.That(controller.IsDirty, Is.True,
                "拨动 hint toggle 应让草稿与基线出现差异");
            Assert.That(applyBtn.ClassListContains("psp-apply-btn--hidden"), Is.False);

            // 拨回去，dirty 应归零
            view.CommitHintToggle(originalHint);
            Assert.That(controller.IsDirty, Is.False,
                "toggle 回到基线值后 IsDirty 应归零");
            Assert.That(applyBtn.ClassListContains("psp-apply-btn--hidden"), Is.True);
        }

        [Test]
        public void ForceCommitPendingEdits_UnfocusedTextFieldFlowsIntoDraft()
        {
            (PomodoroSettingsPanelController controller, _, VisualElement root) = BuildPanel();

            TextField focus = root.Q<TextField>("psp-focus-value");
            // 只改 value，不触发 Blur/回车（模拟用户改了但未失焦）
            focus.value = "33";
            // 此时草稿尚未同步
            Assert.That(controller.IsDirty, Is.False);

            controller.ForceCommitPendingEdits();

            Assert.That(controller.IsDirty, Is.True,
                "ForceCommitPendingEdits 应把 TextField 当前文本拉进草稿");
        }

        // ─── Helpers ─────────────────────────────────────────────────

        private (PomodoroSettingsPanelController controller, PomodoroSettingsPanelView view, VisualElement root)
            BuildPanel()
        {
            VisualTreeAsset template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PomodoroPanelPath);
            Assert.That(template, Is.Not.Null, $"模板缺失：{PomodoroPanelPath}");
            VisualElement root = template.CloneTree();

            var controller = new PomodoroSettingsPanelController();
            controller.Init(root, _model, _lifecycleOwner);

            // Controller 里的 View 是私有字段；反射取出供测试直接调 Commit*。
            FieldInfo viewField = typeof(PomodoroSettingsPanelController)
                .GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(viewField, Is.Not.Null, "PomodoroSettingsPanelController._view 不应被移除");
            var view = (PomodoroSettingsPanelView)viewField.GetValue(controller);
            Assert.That(view, Is.Not.Null, "Init 后 _view 不应为 null");

            return (controller, view, root);
        }
    }

}
