using APP.Pomodoro;
using APP.Settings.Model;
using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 订阅 ISettingsModel.PreviewUiScale，把值同步写入所有关联 PanelSettings 的 scale。
    /// 挂在 DeskWindow 场景常驻 GameObject 上，Inspector 里把两个 PanelSettings 资源拖入数组。
    /// </summary>
    public sealed class PanelScaleApplier : MonoBehaviour, IController
    {
        [SerializeField]
        [Tooltip("拖入所有需要同步缩放的 PanelSettings 资源（PanelSettings_Settings、PomodoroPanelSettings）")]
        private PanelSettings[] _panelSettings;

        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        private void Start()
        {
            _ = GameApp.Interface;  // 确保 Architecture 初始化

            this.GetModel<ISettingsModel>().PreviewUiScale
                .RegisterWithInitValue(ApplyToAll)
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        private void ApplyToAll(float s)
        {
            if (_panelSettings == null || _panelSettings.Length == 0)
            {
                Debug.LogWarning("[PanelScaleApplier] _panelSettings 数组为空，UI 缩放不会生效。");
                return;
            }
            foreach (var ps in _panelSettings)
            {
                if (ps != null) ps.scale = s;
            }
        }
    }
}
