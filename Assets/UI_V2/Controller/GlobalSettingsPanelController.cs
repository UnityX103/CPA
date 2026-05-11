using System.Collections.Generic;
using APP.Pomodoro;
using APP.Pomodoro.Model;
using APP.Settings.Command;
using APP.Settings.Model;
using APP.Settings.Queries;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 全局设置面板控制器。
    /// 缩放：拖动 → pending；点"应用" → 预览 + 倒计时确认；保留/还原走 Cmd_Commit/Revert。
    /// 目标显示器：下拉选择 → 预览 + 弹窗倒计时；保留/还原同上。
    /// 按键计数：全局 toggle + 多绑定列表（每 row：listener + 同步 + 删除）+ 添加按钮。
    ///   监听设置态：root 上 KeyDown/PointerDown 捕到首个事件 → Cmd_CompleteBindingCapture。
    ///   Esc / 点 listener 外 → Cmd_CancelBindingCapture。
    /// </summary>
    public sealed class GlobalSettingsPanelController : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        public const float CountdownSeconds = 5f;

        // ─── 缩放 ─────────────────────────────────────────────────
        private Slider _slider;
        private VisualElement _sliderWrap;
        private VisualElement _progressFill;
        private Label _valueLabel;
        private Button _applyBtn;
        private VisualElement _applyRow;
        private ConfirmDialogController _scaleDialog;
        private float _pendingScale;
        private const float ScaleSliderDraggerSize = 24f;
        private const float ScaleSliderFillLeftInset = 0f;
        private const string ApplyRowHiddenClass = "comp-settings-apply-row--hidden";
        private const float ScaleEpsilon = 0.0001f;

        // ─── 目标显示器 ───────────────────────────────────────────
        private VisualElement _displayDropdown;
        private Label _displayValueLabel;
        private VisualElement _displayMenu;
        private InputDropdownBinding _displayDropdownBinding;
        private IReadOnlyList<DisplayChoice> _availableDisplays;
        private int _pendingDisplayIndex;
        private VisualElement _displayMenuAnchor;

        // ─── 按键计数 ─────────────────────────────────────────────
        private const string RowListeningClass = "comp-binding-key-row--listening";
        private const string RowSyncedClass    = "comp-binding-key-row--synced";
        private Toggle _bindingToggle;
        private VisualElement _bindingList;
        private Button _bindingAddBtn;
        private VisualTreeAsset _bindingRowTemplate;
        private VisualElement _bindingPanelRoot;
        private bool _bindingCaptureHandlersRegistered;
        // 取消捕获挂在 _captureScope 上——比 _bindingPanelRoot 更宽，覆盖整个面板/UIDocument visualTree。
        // 否则用户在监听态点 sidebar tab / 关闭按钮 / overlay 等 GSP 子树外区域时，
        // PointerDown 不会到 GSP root，Cmd_CancelBindingCapture 永远不发，ListeningKeyId 永久挂住，
        // BindingKeyCounterSystem.Tick 因 ListeningKeyId 非空直接 return，按键计数停摆。
        private VisualElement _captureScope;
        // 监听态外部点击取消监听时，临时挂一个 one-shot PointerUp 捕获用来吃掉同一次点击的 release，
        // 避免同一手势的 PointerUp 触发 sync/delete 按钮的 Cmd_SetSynced / Cmd_Remove。
        // 之所以不复用 RegisterBindingCaptureHandlers 那套生命周期，是因为 Cmd_CancelBindingCapture
        // 走 SendCommand 同步把 ListeningKeyId 清空 → OnListeningKeyChanged → UnregisterBindingCaptureHandlers
        // 会先把那批 handler 拆掉，flag/handler 都来不及消费 PointerUp。
        private EventCallback<PointerUpEvent> _outsideCancelPointerUpSuppressor;
        private VisualElement _outsideCancelSuppressorHost;
        // entryId → row element
        private readonly Dictionary<string, VisualElement> _rowsByEntry = new Dictionary<string, VisualElement>();

        public bool IsScaleDialogVisible => _scaleDialog?.IsVisible == true;

        public void Init(
            VisualElement root,
            VisualElement dialogHost,
            VisualTreeAsset confirmDialogTemplate,
            GameObject lifecycleOwner,
            VisualTreeAsset bindingRowTemplate = null)
        {
            var settings = this.GetModel<ISettingsModel>();
            var pomo     = this.GetModel<IPomodoroModel>();
            var binding  = this.GetModel<IBindingKeyModel>();

            _slider     = root.Q<Slider>("gsp-scale-slider");
            _sliderWrap = root.Q<VisualElement>("gsp-scale-slider-wrap");
            _progressFill = root.Q<VisualElement>("gsp-scale-slider-fill");
            _valueLabel = root.Q<Label>("gsp-scale-value");
            _applyBtn   = root.Q<Button>("apply-btn");
            _applyRow   = root.Q<VisualElement>(className: "gsp-apply-row");

            if (_slider != null)
            {
                _slider.lowValue  = SettingsModel.MinScale;
                _slider.highValue = SettingsModel.MaxScale;
            }

            _scaleDialog = new ConfirmDialogController();
            if (dialogHost != null && confirmDialogTemplate != null)
            {
                _scaleDialog.Init(dialogHost, confirmDialogTemplate);
            }

            SyncSliderFromModel(settings.UiScale.Value);

            _slider?.RegisterValueChangedCallback(OnSliderChanged);
            _sliderWrap?.RegisterCallback<GeometryChangedEvent>(_ => RefreshProgressFill(_pendingScale));
            if (_applyBtn != null) _applyBtn.clicked += OnApplyClicked;

            settings.UiScale.Register(SyncSliderFromModel)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);

            // 目标显示器
            _displayDropdown = root.Q<VisualElement>("gsp-display-dropdown");
            _displayValueLabel = root.Q<Label>("gsp-display-dropdown-value");
            _displayMenu = root.Q<VisualElement>("gsp-display-menu");
            _displayMenuAnchor = root;
            if (_displayDropdown != null && _displayValueLabel != null && _displayMenu != null)
            {
                _displayDropdownBinding = new InputDropdownBinding(_displayDropdown, _displayValueLabel, _displayMenu);
                _availableDisplays = this.SendQuery(new Q_GetAvailableDisplays());
                RebuildDisplayMenu();

                int initialIndex = Mathf.Clamp(pomo.TargetMonitorIndex.Value, 0, _availableDisplays.Count - 1);
                _pendingDisplayIndex = initialIndex;
                settings.PreviewTargetDisplay.SetValueWithoutEvent(initialIndex);
                SyncDropdownFromIndex(initialIndex);

                _displayDropdown.RegisterCallback<GeometryChangedEvent>(_ => RepositionDisplayMenu());
                root.RegisterCallback<GeometryChangedEvent>(_ => RepositionDisplayMenu());
            }

            settings.PreviewTargetDisplay.Register(SyncDropdownFromIndex)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            pomo.TargetMonitorIndex.Register(SyncDropdownFromIndex)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);

            // ─── 按键计数 ─────────────────────────────────────
            _bindingToggle  = root.Q<Toggle>("gsp-binding-toggle");
            _bindingList    = root.Q<VisualElement>("gsp-binding-list");
            _bindingAddBtn  = root.Q<Button>("gsp-binding-add-btn");
            _bindingPanelRoot = root;
            _bindingRowTemplate = bindingRowTemplate ?? TryLoadBindingRowTemplate();
            // 面板被 detach（tab 切走 / 关闭 / 场景销毁）时主动 cancel 监听 + 清残留 suppressor。
            root.RegisterCallback<DetachFromPanelEvent>(_ => OnBindingPanelDetached());

            _bindingToggle?.SetValueWithoutNotify(binding.Enabled.Value);
            _bindingToggle?.RegisterValueChangedCallback(e =>
                this.SendCommand(new Cmd_SetBindingEnabled(e.newValue)));

            if (_bindingAddBtn != null)
            {
                _bindingAddBtn.clicked += () =>
                {
                    this.SendCommand<string>(new Cmd_AddBindingKey());
                    // 防御性 rebuild：万一 EntriesRevision 回调因 lifecycleOwner 等原因没触发，
                    // 这里同步重渲一次，保证用户点 "+ 添加按键" 一定能看到新行。
                    RebuildBindingList();
                };
            }

            // 初始构建一次 + 后续订阅 Revision
            RebuildBindingList();

            binding.Enabled.Register(v => _bindingToggle?.SetValueWithoutNotify(v))
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            binding.EntriesRevision.Register(_ => RebuildBindingList())
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            binding.SyncedKeyId.Register(_ => RefreshAllRowsState())
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
            binding.ListeningKeyId.Register(OnListeningKeyChanged)
                .UnRegisterWhenGameObjectDestroyed(lifecycleOwner);
        }

        public void RefreshFromModel()
        {
            var settings = this.GetModel<ISettingsModel>();
            var pomo     = this.GetModel<IPomodoroModel>();
            SyncSliderFromModel(settings.UiScale.Value);
            int committed = pomo.TargetMonitorIndex.Value;
            settings.PreviewTargetDisplay.SetValueWithoutEvent(committed);
            SyncDropdownFromIndex(committed);
            _pendingDisplayIndex = committed;
        }

        internal void TriggerApplyForTest() => OnApplyClicked();

        internal void TriggerSliderChangeForTest(float newValue)
        {
            _pendingScale = SnapToStep(newValue);
            if (_valueLabel != null) _valueLabel.text = FormatScale(_pendingScale);
            _slider?.SetValueWithoutNotify(_pendingScale);
            RefreshProgressFill(_pendingScale);
            RefreshApplyVisibility();
        }

        internal bool IsApplyHiddenForTest =>
            _applyRow != null && _applyRow.ClassListContains(ApplyRowHiddenClass);

        internal void TriggerDropdownChangeForTest(int index) => HandleDropdownIndex(index);

        internal void OverrideAvailableDisplaysForTest(IReadOnlyList<DisplayChoice> displays)
        {
            _availableDisplays = displays;
            RebuildDisplayMenu();
            int committed = this.GetModel<IPomodoroModel>().TargetMonitorIndex.Value;
            int safe = displays.Count == 0 ? 0 : Mathf.Clamp(committed, 0, displays.Count - 1);
            _pendingDisplayIndex = safe;
            SyncDropdownFromIndex(safe);
        }

        internal string CurrentDropdownValueForTest => _displayValueLabel?.text;

        internal void TriggerScaleDialogConfirmForTest() => _scaleDialog?.TriggerConfirmForTest();

        internal void TriggerScaleDialogCancelForTest() => _scaleDialog?.TriggerCancelForTest();

        // ─── 缩放 / 显示器 内部（保持不变） ───────────────────────

        private void OnSliderChanged(ChangeEvent<float> evt)
        {
            _pendingScale    = SnapToStep(evt.newValue);
            if (_valueLabel != null) _valueLabel.text = FormatScale(_pendingScale);
            RefreshProgressFill(evt.newValue);
            RefreshApplyVisibility();
        }

        private void RefreshApplyVisibility()
        {
            if (_applyRow == null) return;
            float committed = this.GetModel<ISettingsModel>().UiScale.Value;
            bool noChange = Mathf.Abs(SnapToStep(_pendingScale) - committed) < ScaleEpsilon;
            _applyRow.EnableInClassList(ApplyRowHiddenClass, noChange);
        }

        private void OnApplyClicked()
        {
            var settings = this.GetModel<ISettingsModel>();
            var current  = settings.UiScale.Value;
            var target   = SnapToStep(_pendingScale);

            if (Mathf.Approximately(current, target)) return;
            if (_scaleDialog.IsVisible) return;

            this.SendCommand(new Cmd_SetPreviewUiScale(target));

            _scaleDialog.Show(
                title:       "保留新缩放吗？",
                subtitle:    $"当前 {FormatScale(target)}，原 {FormatScale(current)}",
                body:        "如 5 秒内未保留，将自动还原到原缩放。",
                confirmText: "保留",
                cancelText:  "还原",
                onConfirm:   () => this.SendCommand(new Cmd_CommitUiScale()),
                onCancel:    () => this.SendCommand(new Cmd_RevertUiScale()),
                countdownSeconds: CountdownSeconds);
        }

        private void HandleDropdownIndex(int newIndex)
        {
            if (_availableDisplays == null || _availableDisplays.Count == 0) return;
            int clamped = Mathf.Clamp(newIndex, 0, _availableDisplays.Count - 1);
            int committed = this.GetModel<IPomodoroModel>().TargetMonitorIndex.Value;

            if (clamped == committed) return;

            if (_scaleDialog.IsVisible)
            {
                int currentPreview = this.GetModel<ISettingsModel>().PreviewTargetDisplay.Value;
                SyncDropdownFromIndex(currentPreview);
                return;
            }

            _pendingDisplayIndex = clamped;
            this.SendCommand(new Cmd_SetPreviewTargetDisplay(clamped));

            string targetLabel  = _availableDisplays[clamped].Label;
            string currentLabel = _availableDisplays[Mathf.Clamp(committed, 0, _availableDisplays.Count - 1)].Label;

            _scaleDialog.Show(
                title:       "切换到该显示器吗？",
                subtitle:    $"目标 {targetLabel}，原 {currentLabel}",
                body:        "如 5 秒内未保留，将自动还原到原显示器。",
                confirmText: "保留",
                cancelText:  "还原",
                onConfirm:   () => this.SendCommand(new Cmd_CommitTargetDisplay()),
                onCancel:    () => this.SendCommand(new Cmd_RevertTargetDisplay()),
                countdownSeconds: CountdownSeconds);
        }

        private void SyncSliderFromModel(float v)
        {
            _pendingScale = v;
            _slider?.SetValueWithoutNotify(v);
            if (_valueLabel != null) _valueLabel.text = FormatScale(v);
            RefreshProgressFill(v);
            RefreshApplyVisibility();
        }

        private void SyncDropdownFromIndex(int index)
        {
            if (_displayDropdownBinding == null || _availableDisplays == null || _availableDisplays.Count == 0) return;
            int safe = Mathf.Clamp(index, 0, _availableDisplays.Count - 1);
            _displayDropdownBinding.SetTriggerText(_availableDisplays[safe].Label);
        }

        private void RebuildDisplayMenu()
        {
            if (_displayDropdownBinding == null || _availableDisplays == null) return;
            string[] choices = new string[_availableDisplays.Count];
            for (int i = 0; i < _availableDisplays.Count; i++)
                choices[i] = _availableDisplays[i].Label;
            int committed = this.GetModel<IPomodoroModel>().TargetMonitorIndex.Value;
            int safeSelected = _availableDisplays.Count == 0
                ? -1
                : Mathf.Clamp(committed, 0, _availableDisplays.Count - 1);
            _displayDropdownBinding.SetItems(choices, safeSelected, HandleDropdownIndex);
        }

        private void RefreshProgressFill(float value)
        {
            if (_progressFill == null) return;
            float trackWidth = _sliderWrap?.resolvedStyle.width ?? 0f;
            if (trackWidth <= 0f) trackWidth = _progressFill.parent?.resolvedStyle.width ?? 0f;
            if (trackWidth <= 0f) { _progressFill.style.width = 0f; return; }
            _progressFill.style.width = CalculateProgressFillWidth(value, trackWidth);
        }

        internal static float CalculateProgressFillWidth(float value, float trackWidth)
        {
            float normalized = Mathf.Clamp01(Mathf.InverseLerp(SettingsModel.MinScale, SettingsModel.MaxScale, value));
            float dragRange = Mathf.Max(0f, trackWidth - ScaleSliderDraggerSize);
            float thumbCenterX = (ScaleSliderDraggerSize * 0.5f) + (dragRange * normalized);
            return Mathf.Max(0f, thumbCenterX);
        }

        internal static float  SnapToStep(float v) => Mathf.Round(v * 10f) / 10f;
        internal static string FormatScale(float v) => $"{v:0.0}×";

        /// <summary>Editor-only fallback：未注入 row 模板时尝试用 AssetDatabase 加载。</summary>
        private static VisualTreeAsset TryLoadBindingRowTemplate()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI_V2/Documents/Components/BindingKeyRow.uxml");
#else
            return null;
#endif
        }

        private void RepositionDisplayMenu()
        {
            if (_displayMenu == null || _displayMenuAnchor == null || _displayDropdown == null) return;
            Rect anchor = _displayMenuAnchor.worldBound;
            Rect trigger = _displayDropdown.worldBound;
            if (trigger.width <= 0f || anchor.width <= 0f) return;
            _displayMenu.style.left  = trigger.x - anchor.x;
            _displayMenu.style.top   = trigger.y - anchor.y + trigger.height + 4f;
            _displayMenu.style.width = trigger.width;
        }

        // ─── 按键计数列表渲染 ─────────────────────────────────────

        /// <summary>重渲整个列表（按 EntriesRevision 触发）。</summary>
        internal void RebuildBindingList()
        {
            if (_bindingList == null) return;

            // 移除所有旧 row（保留容器自身）
            _bindingList.Clear();
            _rowsByEntry.Clear();

            var binding = this.GetModel<IBindingKeyModel>();
            var entries = binding.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                VisualElement row = CreateBindingRow(e);
                _bindingList.Add(row);
                _rowsByEntry[e.Id] = row;
            }

            RefreshAllRowsState();

            // 防守：确保 add button 永远在 binding-card 末尾
            _bindingAddBtn?.BringToFront();
        }

        private VisualElement CreateBindingRow(BindingKeyEntry e)
        {
            VisualElement row;
            if (_bindingRowTemplate != null)
            {
                // CloneTree 会包一层 TemplateContainer——会破坏父容器的 flex column 排版
                // （多个 TemplateContainer 抢 flex-grow 互相覆盖），所以剥出内部 root 直接挂
                var container = _bindingRowTemplate.CloneTree();
                row = container.Q<VisualElement>(className: "comp-binding-key-row") ?? container;
                if (row != container && row.parent != null) row.parent.Remove(row);
            }
            else
            {
                // Fallback：直接构建（用于测试期未注入模板时）
                row = new VisualElement();
                row.AddToClassList("comp-binding-key-row");
            }

            // 把 Q 操作收紧到 row 内部
            var listenerBtn = row.Q<Button>("bk-row-listener");
            var keyLabel    = row.Q<Label>("bk-row-key");
            var hint        = row.Q<Label>("bk-row-hint");
            var syncBtn     = row.Q<VisualElement>("bk-row-sync-btn");
            var delBtn      = row.Q<VisualElement>("bk-row-del-btn");

            if (keyLabel != null) keyLabel.text = e.KeyLabel ?? string.Empty;
            if (hint != null)     hint.text     = e.Enabled ? "点击重新绑定 · 已启用" : "点击重新绑定";

            string capturedId = e.Id;
            if (listenerBtn != null)
            {
                listenerBtn.clicked += () =>
                {
                    var b = this.GetModel<IBindingKeyModel>();
                    if (b.ListeningKeyId.Value == capturedId) return;
                    this.SendCommand(new Cmd_BeginBindingCapture(capturedId));
                };
            }

            // icon 按钮用 VisualElement + PointerUp，避开 Unity .unity-button 默认样式
            if (syncBtn != null)
            {
                syncBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                syncBtn.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    // sync 仅切 SyncedKeyId（远端同步标记），不再绑定 entry.Enabled。
                    // entry 一被添加就自带 panel（DeskWindowController 按存在性渲染）。
                    this.SendCommand(new Cmd_SetSyncedBindingKey(capturedId));
                });
            }

            if (delBtn != null)
            {
                delBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                delBtn.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    this.SendCommand(new Cmd_RemoveBindingKey(capturedId));
                });
            }

            // entryId 标记到 row 上，方便 capture handler 反查
            row.userData = e.Id;

            return row;
        }

        private void RefreshAllRowsState()
        {
            var binding = this.GetModel<IBindingKeyModel>();
            string syncedId    = binding.SyncedKeyId.Value;
            string listeningId = binding.ListeningKeyId.Value;
            foreach (var kv in _rowsByEntry)
            {
                kv.Value.EnableInClassList(RowSyncedClass,    kv.Key == syncedId);
                kv.Value.EnableInClassList(RowListeningClass, kv.Key == listeningId);
            }
        }

        private void OnListeningKeyChanged(string listeningId)
        {
            RefreshAllRowsState();
            if (!string.IsNullOrEmpty(listeningId))
            {
                // 焦点钉到对应 listener 上，再延一帧注册全局 KeyDown/PointerDown 捕获
                if (_rowsByEntry.TryGetValue(listeningId, out var row))
                {
                    var listenerBtn = row.Q<Button>("bk-row-listener");
                    listenerBtn?.Focus();
                    listenerBtn?.schedule.Execute(RegisterBindingCaptureHandlers).StartingIn(0);
                }
            }
            else
            {
                UnregisterBindingCaptureHandlers();
            }
        }

        private void RegisterBindingCaptureHandlers()
        {
            if (_bindingCaptureHandlersRegistered || _bindingPanelRoot == null) return;
            // 优先用 panel.visualTree——比 GSP root 更宽，能截到 sidebar tab / 关闭按钮等外部点击。
            // 极端情况（_bindingPanelRoot.panel 还没挂上）回退到 _bindingPanelRoot 本身。
            _captureScope = _bindingPanelRoot.panel?.visualTree ?? _bindingPanelRoot;
            _captureScope.RegisterCallback<KeyDownEvent>(OnBindingCaptureKey, TrickleDown.TrickleDown);
            _captureScope.RegisterCallback<PointerDownEvent>(OnBindingCapturePointer, TrickleDown.TrickleDown);
            _bindingCaptureHandlersRegistered = true;
        }

        private void UnregisterBindingCaptureHandlers()
        {
            if (!_bindingCaptureHandlersRegistered) return;
            _captureScope?.UnregisterCallback<KeyDownEvent>(OnBindingCaptureKey, TrickleDown.TrickleDown);
            _captureScope?.UnregisterCallback<PointerDownEvent>(OnBindingCapturePointer, TrickleDown.TrickleDown);
            _captureScope = null;
            _bindingCaptureHandlersRegistered = false;
            // 注意：_outsideCancelPointerUpSuppressor 故意不在这里清——它的生命周期跨 Cancel→PointerUp，
            // 不能因为 ListeningKeyId 清空就被拆掉，否则同手势的 PointerUp 还是会落到 sync/delete。
            // Suppressor 的兜底清理在 OnBindingPanelDetached 里——面板拆掉时无论如何都清掉它。
        }

        private void OnBindingCaptureKey(KeyDownEvent evt)
        {
            string id = this.GetModel<IBindingKeyModel>().ListeningKeyId.Value;
            if (string.IsNullOrEmpty(id)) return;
            KeyCode kc = evt.keyCode;
            if (kc == KeyCode.None) return;
            evt.StopPropagation();
            if (kc == KeyCode.Escape)
            {
                this.SendCommand(new Cmd_CancelBindingCapture());
                return;
            }
            this.SendCommand(new Cmd_CompleteBindingCapture(id, (int)kc, FormatKeyLabel(kc)));
        }

        private void OnBindingCapturePointer(PointerDownEvent evt)
        {
            string id = this.GetModel<IBindingKeyModel>().ListeningKeyId.Value;
            if (string.IsNullOrEmpty(id)) return;

            // PointerDown 通过 TrickleDown 在 _bindingPanelRoot 全捕获——必须先定位事件是否真的发生在
            // 当前监听 row 的 bk-row-listener 内部。否则点删除/同步按钮、空白处、其他控件都会被误判成
            // "完成绑定"并把 entry 改成鼠标键，破坏用户设置。
            VisualElement target = evt.target as VisualElement;
            VisualElement listenerBtn = null;
            if (_rowsByEntry.TryGetValue(id, out var row) && row != null)
            {
                listenerBtn = row.Q<VisualElement>("bk-row-listener");
            }
            if (listenerBtn == null || target == null || !IsAncestorOrSelf(listenerBtn, target))
            {
                // 顺序敏感：先 arm PointerUp suppressor，再 SendCommand。
                // SendCommand 同步走 ListeningKeyId.Value = "" → UnregisterBindingCaptureHandlers，
                // 会把 PointerDown handler 拆掉；但 one-shot suppressor 是单独注册的，不会被拆。
                ArmOutsideCancelPointerUpSuppressor();
                this.SendCommand(new Cmd_CancelBindingCapture());
                evt.StopPropagation();
                return;
            }

            int code; string label;
            switch (evt.button)
            {
                case 0: code = BindingKeyModel.MouseLeft;   label = "鼠标左键"; break;
                case 1: code = BindingKeyModel.MouseRight;  label = "鼠标右键"; break;
                case 2: code = BindingKeyModel.MouseMiddle; label = "鼠标中键"; break;
                default: return;
            }
            evt.StopPropagation();
            this.SendCommand(new Cmd_CompleteBindingCapture(id, code, label));
        }

        private static bool IsAncestorOrSelf(VisualElement ancestor, VisualElement target)
        {
            for (var cur = target; cur != null; cur = cur.parent)
            {
                if (cur == ancestor) return true;
            }
            return false;
        }

        /// <summary>
        /// 监听态外部点击触发取消后，临时挂一个一次性 PointerUp 捕获到 _captureScope 上，
        /// 吃掉同一次点击的 PointerUp（防止 sync/delete 按钮的 PointerUp handler 接力执行）。
        /// 触发一次后立刻自解除注册——下一次合法点击不会被误吞。
        /// 不复用 RegisterBindingCaptureHandlers 那批 handler 的原因：SendCommand 是同步的，
        /// ListeningKeyId 清空会立刻 UnregisterBindingCaptureHandlers，那批 handler 来不及消费 PointerUp。
        /// </summary>
        private void ArmOutsideCancelPointerUpSuppressor()
        {
            // 优先挂到 _captureScope（panel.visualTree）——和 PointerDown 捕获同源，PointerUp 一定能被截到。
            // 如果用户在监听态外部按下、拖到 panel 外释放，suppressor 也会被 OnBindingPanelDetached 兜底清掉。
            VisualElement host = _captureScope ?? _bindingPanelRoot;
            if (host == null) return;
            // 已经挂过了：保持已注册的实例，避免同一手势 PointerDown 多次 arm 后只剩最后一个生效。
            if (_outsideCancelPointerUpSuppressor != null) return;
            _outsideCancelSuppressorHost = host;
            _outsideCancelPointerUpSuppressor = evt =>
            {
                evt.StopPropagation();
                CleanupOutsideCancelSuppressor();
            };
            host.RegisterCallback(_outsideCancelPointerUpSuppressor, TrickleDown.TrickleDown);
        }

        private void CleanupOutsideCancelSuppressor()
        {
            if (_outsideCancelPointerUpSuppressor != null && _outsideCancelSuppressorHost != null)
            {
                _outsideCancelSuppressorHost.UnregisterCallback(_outsideCancelPointerUpSuppressor, TrickleDown.TrickleDown);
            }
            _outsideCancelPointerUpSuppressor = null;
            _outsideCancelSuppressorHost = null;
        }

        /// <summary>
        /// GSP 面板被 detach（切 tab / 关面板 / 场景销毁）时调用：
        /// (1) 主动取消监听，避免 ListeningKeyId 永久挂住把 BindingKeyCounterSystem 停摆；
        /// (2) 清掉残留的 one-shot PointerUp suppressor，避免下次 attach 时仍非空、误吞合法点击。
        /// </summary>
        private void OnBindingPanelDetached()
        {
            var binding = this.GetModel<IBindingKeyModel>();
            if (!string.IsNullOrEmpty(binding.ListeningKeyId.Value))
            {
                this.SendCommand(new Cmd_CancelBindingCapture());
            }
            CleanupOutsideCancelSuppressor();
        }

        private static string FormatKeyLabel(KeyCode kc)
        {
            switch (kc)
            {
                case KeyCode.Space:        return "空格";
                case KeyCode.Return:
                case KeyCode.KeypadEnter:  return "回车";
                case KeyCode.Tab:          return "Tab";
                case KeyCode.Backspace:    return "Backspace";
                case KeyCode.UpArrow:      return "↑";
                case KeyCode.DownArrow:    return "↓";
                case KeyCode.LeftArrow:    return "←";
                case KeyCode.RightArrow:   return "→";
                default:                   return kc.ToString();
            }
        }
    }
}
