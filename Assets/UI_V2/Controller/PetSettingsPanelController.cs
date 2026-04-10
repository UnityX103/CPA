using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 宠物设置面板控制器（独立 UIDocument）。
    /// 管理 PetSettingsPanel.uxml 的 UI 绑定。
    /// 当前为开发中占位状态。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetSettingsPanelController : MonoBehaviour, IController, ISettingsPanel
    {
        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 私有字段 ────────────────────────────────────────────
        private UIDocument _uiDocument;

        // ─── ISettingsPanel ──────────────────────────────────────
        public bool IsVisible => _uiDocument != null && _uiDocument.enabled;

        public void Show()
        {
            if (_uiDocument == null)
            {
                return;
            }

            _uiDocument.enabled = true;
            BindUI();
        }

        public void Hide()
        {
            if (_uiDocument == null)
            {
                return;
            }

            _uiDocument.enabled = false;
        }

        // ─── Unity 生命周期 ──────────────────────────────────────

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void Start()
        {
            if (_uiDocument != null)
            {
                _uiDocument.enabled = false;
            }
        }

        // ─── UI 绑定 ─────────────────────────────────────────────

        /// <summary>
        /// 每次 Show 时重新绑定 UI 元素。
        /// UIDocument.enabled false→true 会重建 visual tree，旧回调全部失效。
        /// </summary>
        private void BindUI()
        {
            if (_uiDocument == null)
            {
                return;
            }

            VisualElement root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            // 返回按钮（每次重建都需重新绑定）
            root.Q<Button>("back-btn")?.RegisterCallback<PointerUpEvent>(_ => Hide());
        }
    }
}
