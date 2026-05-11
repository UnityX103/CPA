using APP.Network.System;
using APP.Pomodoro;
using APP.Settings.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 输入计数面板控制器（多 entry 版）。
    /// 面板与 BindingKeyEntry 是 1:N：单一面板里 pill-list 按 entryId 缓存每个 pill。
    /// SyncPillsFromEntries 增量同步：
    ///   新 id → 克隆 KeyCounterPill 模板插入；丢失 id → 移除；已有 id → 仅刷新 KeyLabel / PressCount。
    /// 面板自身的生命周期（entries.Count==0 时整面板销毁）由 DeskWindowController 负责，
    /// 本控制器不订阅 EntriesRevision——由 DeskWindow 在 RebuildInputCounterPanels 中调用 SyncPillsFromEntries。
    /// pin 按钮目前只切视觉态；app 信息走 IActiveAppSystem.Changed。
    /// </summary>
    public sealed class InputCounterPanelController : IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private const string UnpinnedClass     = "icp-pin-btn--unpinned";
        private const string PillStackedClass  = "icp-key-counter--stacked";
        private const string PillBaseClass     = "icp-key-counter";

        private VisualElement   _root;
        private VisualElement   _pinBtn;
        private VisualElement   _pillList;
        private Label           _appLabel;
        private VisualElement   _appIcon;
        private Texture2D       _appIconTexture;
        // 不在 controller 层做去重——ActiveAppSystem.Tick 在 bundleId+pngBytes 同时未变时不会发 Changed，
        // 真到 Changed handler 就一定有事要做。少一份字段，省一份签名碰撞静默 stale 的风险。
        private VisualTreeAsset _pillTemplate;
        private IActiveAppSystem _activeApp;
        private bool _pinned = true;

        // entryId → pill 元素；pill 内部有 key-counter-pill-key / -count Label
        private readonly global::System.Collections.Generic.Dictionary<string, VisualElement> _pillsById =
            new global::System.Collections.Generic.Dictionary<string, VisualElement>();

        public bool IsPinnedForTest => _pinned;
        public int PillCountForTest => _pillsById.Count;
        public VisualElement GetPillForTest(string entryId) =>
            _pillsById.TryGetValue(entryId ?? string.Empty, out var pill) ? pill : null;

        /// <summary>
        /// 初始化面板。UXML 里默认带一个 KeyCounterPill 用作设计稿占位 + 视觉测试默认态，
        /// 运行时 pill-list 会被清空再按 Entries 重建。
        /// </summary>
        public void Init(VisualElement root, GameObject lifecycleOwner, VisualTreeAsset pillTemplate)
        {
            _root         = root;
            _pillTemplate = pillTemplate;
            _pinBtn       = root.Q<VisualElement>("icp-pin-btn");
            _pillList     = root.Q<VisualElement>("icp-pill-list");
            _appLabel     = root.Q<Label>("icp-app-text");
            _appIcon      = root.Q<VisualElement>("icp-app-icon");

            _activeApp = this.GetSystem<IActiveAppSystem>();

            // 清空 UXML 自带的占位 pill；后续 SyncPillsFromEntries 重建。
            _pillList?.Clear();

            SyncPillsFromEntries();
            SyncFromActiveApp(_activeApp?.Current ?? ActiveAppSnapshot.Empty);

            if (_activeApp != null) _activeApp.Changed += SyncFromActiveApp;

            if (_pinBtn != null)
            {
                _pinBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                _pinBtn.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    SetPinned(!_pinned);
                });
            }
        }

        /// <summary>
        /// 面板销毁前调用：取消 IActiveAppSystem 事件订阅、清空内部状态。
        /// </summary>
        public void Dispose()
        {
            if (_activeApp != null) _activeApp.Changed -= SyncFromActiveApp;
            _activeApp = null;
            DestroyAppIconTexture();
            _appIcon = null;
            _pillsById.Clear();
            _pillList = null;
            _root = null;
            _pinBtn = null;
            _appLabel = null;
        }

        internal void TogglePinForTest() => SetPinned(!_pinned);
        internal void OverrideActiveAppForTest(string name) =>
            SyncFromActiveApp(new ActiveAppSnapshot(name, string.Empty, null));

        /// <summary>
        /// 按 Model.Entries 顺序增量同步 pill 列表：
        /// - 不再存在的 entry → 移除 pill 节点
        /// - 新 entry → 克隆 KeyCounterPill 模板、插入对应位置
        /// - 已有 entry → 刷新 KeyLabel / PressCount + 调整顺序
        /// 首 pill 去 stacked class（无 margin-top），其余加 stacked（有 8px 间距）。
        /// </summary>
        public void SyncPillsFromEntries()
        {
            if (_pillList == null) return;
            var binding = this.GetModel<IBindingKeyModel>();
            var entries = binding.Entries;

            var keepIds = new global::System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                var id = entries[i].Id;
                if (!string.IsNullOrEmpty(id)) keepIds.Add(id);
            }

            // 1) 移除不再需要的 pill
            var toRemove = new global::System.Collections.Generic.List<string>();
            foreach (var kv in _pillsById)
            {
                if (!keepIds.Contains(kv.Key)) toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                var id = toRemove[i];
                var pill = _pillsById[id];
                if (pill != null && pill.parent == _pillList) _pillList.Remove(pill);
                _pillsById.Remove(id);
            }

            // 2) 顺序对齐 + 文本刷新；缺失则克隆模板
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.Id)) continue;

                if (!_pillsById.TryGetValue(entry.Id, out var pill) || pill == null)
                {
                    pill = ClonePill();
                    if (pill == null) continue;
                    _pillsById[entry.Id] = pill;
                    _pillList.Add(pill);
                }

                if (_pillList.IndexOf(pill) != i)
                {
                    if (pill.parent == _pillList) _pillList.Remove(pill);
                    if (i >= _pillList.childCount) _pillList.Add(pill);
                    else _pillList.Insert(i, pill);
                }

                pill.EnableInClassList(PillStackedClass, i > 0);

                var keyLabel   = pill.Q<Label>("key-counter-pill-key");
                var countLabel = pill.Q<Label>("key-counter-pill-count");
                var badge      = pill.Q<VisualElement>("key-counter-pill-badge");
                if (keyLabel != null)   keyLabel.text   = entry.KeyLabel ?? string.Empty;
                if (countLabel != null) countLabel.text = entry.PressCount.ToString();
                PlayerCardView.ApplyKeyBadgeMouseClass(badge, entry.KeyLabel);
            }
        }

        private VisualElement ClonePill()
        {
            if (_pillTemplate == null) return null;
            // CloneTree 返回 TemplateContainer，直接用作 pill 容器（与 UXML 中 <ui:Instance> 行为一致）。
            VisualElement pill = _pillTemplate.CloneTree();
            pill.name = "icp-key-counter";
            if (!pill.ClassListContains(PillBaseClass)) pill.AddToClassList(PillBaseClass);
            return pill;
        }

        private void SyncFromActiveApp(ActiveAppSnapshot snap)
        {
            if (_appLabel != null)
            {
                _appLabel.text = string.IsNullOrEmpty(snap.Name) ? "—" : snap.Name;
            }
            ApplyAppIcon(snap.BundleId, snap.IconPngBytes);
        }

        /// <summary>
        /// 把当前前台 App 的原生图标 PNG 解码到 icp-app-icon 的 backgroundImage。
        /// - bundleId 没变 → 直接复用已构造的 Texture，避免每次 Changed 都重建（即便事件 fire，
        ///   ActiveAppSystem 也是只在 BundleId 变化时 Changed，这里再加一道防御）。
        /// - pngBytes 空 / LoadImage 失败 → 把 backgroundImage 清成 StyleKeyword.Null，
        ///   USS 默认 app-window.png 回填，至少不留旧 App 的图标。
        /// </summary>
        private void ApplyAppIcon(string bundleId, byte[] pngBytes)
        {
            if (_appIcon == null) return;

            // 没拿到图标数据 / 没 BundleId（如 Accessibility 拒绝时 ActiveAppSystem 已重置为 Empty）→ 回落 USS 默认图标
            if (pngBytes == null || pngBytes.Length == 0)
            {
                DestroyAppIconTexture();
                _appIcon.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
                // 恢复 USS 默认 tint（让 app-window.png 回到设计稿的灰褐色）
                _appIcon.style.unityBackgroundImageTintColor = new StyleColor(StyleKeyword.Null);
                return;
            }

            // 不再做 controller 层去重：ActiveAppSystem 已经在 bundleId+pngBytes 都没变时不发 Changed，
            // 到这里就说明图标真有变化，直接重建 Texture 即可。这样既避开签名碰撞静默 stale 的风险，
            // 也避免依赖 bundleId 这种 macOS 换版仍然不变的不可靠 key。bundleId 参数保留便于后续扩展/日志。
            _ = bundleId;
            DestroyAppIconTexture();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "ICPAppIcon" };
            if (!tex.LoadImage(pngBytes))
            {
                DestroyTexture(tex);
                _appIcon.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
                _appIcon.style.unityBackgroundImageTintColor = new StyleColor(StyleKeyword.Null);
                return;
            }
            tex.Apply();
            _appIconTexture = tex;
            _appIcon.style.backgroundImage = new StyleBackground(tex);
            // 真实应用图标本身有彩色像素，USS 的灰褐色 tint 会把它染脏，强制 white 让原色透出。
            _appIcon.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
        }

        private void DestroyAppIconTexture()
        {
            DestroyTexture(_appIconTexture);
            _appIconTexture = null;
        }

        private static void DestroyTexture(Texture2D tex)
        {
            if (tex == null) return;
            if (Application.isPlaying) Object.Destroy(tex);
            else Object.DestroyImmediate(tex);
        }

        private void SetPinned(bool pinned)
        {
            _pinned = pinned;
            _pinBtn?.EnableInClassList(UnpinnedClass, !pinned);
        }
    }
}
