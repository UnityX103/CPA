using APP.Network.Model;
using APP.Pomodoro;
using APP.Pomodoro.Event;
using APP.Pomodoro.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 独立 UIDocument（UnifiedSettingsPanel GameObject）的驱动 MonoBehaviour。
    /// 订阅 E_OpenUnifiedSettings / E_CloseUnifiedSettings 切换设置面板显隐。
    /// 与 DeskWindowController 解耦，通过 QFramework Event 通信。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UnifiedSettingsPanelDriver : MonoBehaviour, IController
    {
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        [Header("子面板 UXML 模板（番茄钟 / 联机 / 宠物）")]
        [SerializeField] private VisualTreeAsset _pomodoroSettingsTemplate;
        [SerializeField] private VisualTreeAsset _onlineSettingsTemplate;
        [SerializeField] private VisualTreeAsset _petSettingsTemplate;

        [Header("未保存更改提示对话框模板")]
        [SerializeField] private VisualTreeAsset _unsavedChangesDialogTemplate;

        private UIDocument _doc;
        private UnifiedSettingsPanelController _controller;
        private VisualElement _root;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void Start()
        {
            // 确保 Architecture 初始化
            _ = GameApp.Interface;

            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogError("[UnifiedSettingsPanelDriver] rootVisualElement is null");
                return;
            }

            // 全屏透明 + flex 居中，承载居中的 .settings-overlay 浮窗
            _root.AddToClassList("usp-root-anchor");

            // 初始隐藏（UXML 本身已设 style display:none，再保险一次）
            _root.style.display = DisplayStyle.None;

            var pomodoroModel = this.GetModel<IPomodoroModel>();
            var roomModel = this.GetModel<IRoomModel>();

            _controller = new UnifiedSettingsPanelController();
            _controller.Init(
                _root,
                pomodoroModel,
                roomModel,
                _pomodoroSettingsTemplate,
                _onlineSettingsTemplate,
                _petSettingsTemplate,
                _unsavedChangesDialogTemplate,
                gameObject);

            this.RegisterEvent<E_OpenUnifiedSettings>(_ => OpenPanel())
                .UnRegisterWhenGameObjectDestroyed(gameObject);
            this.RegisterEvent<E_CloseUnifiedSettings>(_ => ClosePanel())
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void OpenPanel()
        {
            if (_root == null || _controller == null) return;
            _root.style.display = DisplayStyle.Flex;
            _controller.Show();
        }

        private void ClosePanel()
        {
            if (_root == null || _controller == null) return;
            _controller.RequestClose(() =>
            {
                _root.style.display = DisplayStyle.None;
            });
        }
    }
}
