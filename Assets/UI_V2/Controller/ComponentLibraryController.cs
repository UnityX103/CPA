using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// ComponentLibrary 冒烟面板的 runtime 绑定层，让 InputSlider / InputDropdown 在
    /// 组件仓库视图里可以真实交互，而不只是静态展示。
    ///
    /// 与 GlobalSettingsPanelController 不同，这里不接 QFramework Model / Command，
    /// 仅维护本面板内部的 UI 状态——纯演示用途。
    ///
    /// 使用方式：把本组件挂在 UIDocument(source=ComponentLibrary.uxml) 同一 GameObject 上。
    ///
    /// Slider：
    ///   - low/high = 0.5 / 2.0，与 Pencil YwCv6 一致；
    ///   - 拖动时按 dragger 中心算 .comp-input-slider__fill 宽度，避免 fill 和 thumb 错位；
    ///   - 标签按 0.1 步进 snap 显示，例如 "1.2×"。
    ///
    /// Dropdown：
    ///   - 点击切换 .comp-input-dropdown--open（带橙字 + 雪佛龙翻转）；
    ///   - 弹出 .cl-dropdown-menu，提供"显示器 1 / 显示器 2"两个选项；
    ///   - 选中后回写 .comp-input-dropdown__value 文本，关闭菜单；
    ///   - 点击面板任意空白处自动收起。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ComponentLibraryController : MonoBehaviour
    {
        private const float SliderMin   = 0.5f;
        private const float SliderMax   = 2.0f;
        private const float ThumbSize   = 24f;
        private const float StepSize    = 0.1f;

        private static readonly string[] DropdownOptions = { "显示器 1", "显示器 2" };

        private UIDocument _doc;
        private VisualElement _openMenu;
        private VisualElement _openDropdown;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) return;

            // UIDocument 在 Awake/OnEnable 这一帧 rootVisualElement 可能尚未就绪
            var root = _doc.rootVisualElement;
            if (root != null)
            {
                BindAll(root);
            }
            else
            {
                StartCoroutine(BindNextFrame());
            }
        }

        private void OnDisable()
        {
            CloseMenu();
        }

        private IEnumerator BindNextFrame()
        {
            yield return null;
            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root != null) BindAll(root);
        }

        private void BindAll(VisualElement root)
        {
            BindSliders(root);
            BindDropdowns(root);

            // 任意空白处点一下自动收菜单（捕获阶段判定，dropdown / menu 自己 StopPropagation 优先）
            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        // ─── Slider ───────────────────────────────────────────────────────

        private void BindSliders(VisualElement root)
        {
            foreach (var slider in root.Query<Slider>(className: "comp-input-slider__control").ToList())
            {
                BindSingleSlider(slider);
            }
        }

        private void BindSingleSlider(Slider slider)
        {
            slider.lowValue  = SliderMin;
            slider.highValue = SliderMax;
            // 强制复位到 1.0 baseline。绑定瞬间若 wrap 宽度 = 0，Unity Slider 会从 0 宽
            // drag-container 读回 lowValue（0.5），label 就锁死在"0.5×"。SetValueWithoutNotify
            // 不触发 ChangeEvent，避免与首次 Refresh 重复刷新。
            slider.SetValueWithoutNotify(Mathf.Clamp(1.0f, SliderMin, SliderMax));

            var wrap       = slider.parent;
            var fill       = wrap?.Q<VisualElement>(className: "comp-input-slider__fill");
            var sliderRoot = wrap?.parent;
            var label      = sliderRoot?.Q<Label>(className: "comp-input-slider__value");

            void Refresh(float v)
            {
                if (label != null)
                {
                    float snapped = Mathf.Round(v / StepSize) * StepSize;
                    label.text = $"{snapped:0.0}×";
                }
                if (fill == null || wrap == null) return;

                float trackWidth = wrap.resolvedStyle.width;
                if (trackWidth <= 0f) return;

                float t = Mathf.InverseLerp(SliderMin, SliderMax, v);
                float dragRange = Mathf.Max(0f, trackWidth - ThumbSize);
                fill.style.width = ThumbSize * 0.5f + dragRange * t;
            }

            slider.RegisterValueChangedCallback(evt => Refresh(evt.newValue));
            wrap?.RegisterCallback<GeometryChangedEvent>(_ => Refresh(slider.value));
            Refresh(slider.value);
        }

        // ─── Dropdown ─────────────────────────────────────────────────────

        private void BindDropdowns(VisualElement root)
        {
            foreach (var dd in root.Query<VisualElement>(className: "comp-input-dropdown").ToList())
            {
                var valueLabel = dd.Q<Label>(className: "comp-input-dropdown__value");
                if (valueLabel == null) continue;

                if (string.IsNullOrEmpty(valueLabel.text))
                {
                    valueLabel.text = DropdownOptions[0];
                }

                var captured = dd;
                var capturedLabel = valueLabel;
                dd.RegisterCallback<PointerUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (_openDropdown == captured)
                    {
                        CloseMenu();
                        return;
                    }
                    CloseMenu();
                    OpenMenu(captured, capturedLabel);
                });
            }
        }

        private void OpenMenu(VisualElement dd, Label valueLabel)
        {
            var menu = new VisualElement { name = "cl-dropdown-menu" };
            menu.AddToClassList("cl-dropdown-menu");

            foreach (var opt in DropdownOptions)
            {
                string captured = opt;
                var item = new Label(captured) { name = "cl-dropdown-menu-item" };
                item.AddToClassList("cl-dropdown-menu__item");
                item.RegisterCallback<PointerUpEvent>(e =>
                {
                    e.StopPropagation();
                    valueLabel.text = captured;
                    CloseMenu();
                });
                menu.Add(item);
            }

            dd.Add(menu);
            dd.AddToClassList("comp-input-dropdown--open");
            _openMenu = menu;
            _openDropdown = dd;
        }

        private void CloseMenu()
        {
            if (_openMenu != null)
            {
                _openMenu.RemoveFromHierarchy();
                _openMenu = null;
            }
            if (_openDropdown != null)
            {
                _openDropdown.RemoveFromClassList("comp-input-dropdown--open");
                _openDropdown = null;
            }
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (_openMenu == null) return;

            var target = evt.target as VisualElement;
            if (target == null) { CloseMenu(); return; }

            if (IsAncestor(target, _openDropdown) || IsAncestor(target, _openMenu)) return;
            CloseMenu();
        }

        private static bool IsAncestor(VisualElement target, VisualElement ancestor)
        {
            if (target == null || ancestor == null) return false;
            for (var cur = target; cur != null; cur = cur.parent)
            {
                if (cur == ancestor) return true;
            }
            return false;
        }
    }
}
