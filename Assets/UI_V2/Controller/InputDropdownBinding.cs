using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 共享自定义下拉框绑定。把"触发器 + 兄弟菜单 + 选项列表 + 单选回调"包成一个对象，
    /// PomodoroSettingsPanelView 与 GlobalSettingsPanelController 都用它，避免每个面板各写一遍
    /// 点击切换/构建菜单/隐藏菜单的胶水代码。
    ///
    /// UXML 约定：
    ///   - 触发器 VisualElement 带 class <c>comp-input-dropdown</c>
    ///   - 触发器内部一个 Label 用 class <c>comp-input-dropdown__value</c>（显示当前值）
    ///   - 触发器后面一个兄弟 VisualElement 当菜单容器，带 class
    ///     <c>comp-input-dropdown-menu comp-input-dropdown-menu--hidden</c>
    ///
    /// 菜单项由 <see cref="SetItems"/> 动态构建；点击项 → 关闭菜单 + 调用 onSelect(index)。
    /// 触发器 PointerUp 切换显隐；同时一组 binding 互斥（外部通过 <see cref="Close"/> 协调）。
    /// </summary>
    public sealed class InputDropdownBinding
    {
        private const string MenuClass = "comp-input-dropdown-menu";
        private const string MenuHiddenClass = "comp-input-dropdown-menu--hidden";
        private const string ItemClass = "comp-input-dropdown-menu-item";
        private const string ItemSelectedClass = "comp-input-dropdown-menu-item--selected";
        private const string ItemLabelClass = "comp-input-dropdown-menu-item-label";

        private readonly VisualElement _trigger;
        private readonly Label _valueLabel;
        private readonly VisualElement _menu;

        private bool _isOpen;
        private Action<int> _onSelect;
        private IReadOnlyList<string> _choices = Array.Empty<string>();
        private int _selectedIndex = -1;

        /// <summary>外部协调互斥：开本菜单时回调，方便父级关掉其它菜单。</summary>
        public event Action OnAboutToOpen;

        /// <summary>当前菜单是否处于展开态。</summary>
        public bool IsOpen => _isOpen;

        public InputDropdownBinding(VisualElement trigger, Label valueLabel, VisualElement menu)
        {
            _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            _valueLabel = valueLabel ?? throw new ArgumentNullException(nameof(valueLabel));
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));

            _trigger.RegisterCallback<PointerUpEvent>(OnTriggerPointerUp);
            SetMenuVisible(false);
        }

        /// <summary>设置触发器显示文本（菜单项不动）。</summary>
        public void SetTriggerText(string text)
        {
            _valueLabel.text = text ?? string.Empty;
        }

        /// <summary>
        /// 用新的选项列表重建菜单。<paramref name="selectedIndex"/> 决定哪一项加 selected 高亮，
        /// 越界视为无选中。<paramref name="onSelect"/> 是用户点选后的回调（参数 = 选中下标）。
        /// 调用后菜单默认收起。
        /// </summary>
        public void SetItems(IReadOnlyList<string> choices, int selectedIndex, Action<int> onSelect)
        {
            _choices = choices ?? Array.Empty<string>();
            _selectedIndex = selectedIndex;
            _onSelect = onSelect;
            RebuildMenu();
            SetMenuVisible(false);
        }

        /// <summary>关闭菜单（如果开着）。外部协调互斥时使用。</summary>
        public void Close()
        {
            SetMenuVisible(false);
        }

        // ─── 内部 ─────────────────────────────────────────────────

        private void OnTriggerPointerUp(PointerUpEvent evt)
        {
            evt.StopPropagation();
            bool willOpen = !_isOpen;
            if (willOpen)
            {
                OnAboutToOpen?.Invoke();
            }
            SetMenuVisible(willOpen);
        }

        private void SetMenuVisible(bool visible)
        {
            _isOpen = visible;
            _menu.EnableInClassList(MenuHiddenClass, !visible);
        }

        private void RebuildMenu()
        {
            _menu.Clear();
            for (int i = 0; i < _choices.Count; i++)
            {
                int captured = i;
                string text = _choices[i] ?? string.Empty;
                bool selected = (i == _selectedIndex);

                VisualElement item = new VisualElement();
                item.AddToClassList(ItemClass);
                if (selected)
                {
                    item.AddToClassList(ItemSelectedClass);
                }

                Label label = new Label(text);
                label.AddToClassList(ItemLabelClass);
                item.Add(label);

                item.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    SetMenuVisible(false);
                    _onSelect?.Invoke(captured);
                });

                _menu.Add(item);
            }
        }
    }
}
