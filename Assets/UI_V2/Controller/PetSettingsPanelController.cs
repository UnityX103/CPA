using QFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 宠物设置面板控制器（纯 C# 类，无 MonoBehaviour）。
    /// 当前为开发中占位状态。
    /// </summary>
    public sealed class PetSettingsPanelController : IController
    {
        // ─── QFramework ──────────────────────────────────────────
        IArchitecture IBelongToArchitecture.GetArchitecture() => GameApp.Interface;

        // ─── 初始化 ──────────────────────────────────────────────

        /// <summary>
        /// 初始化面板。容器内已由 UnifiedSettingsPanelController 克隆好 UXML。
        /// </summary>
        public void Init(VisualElement container, GameObject lifecycleOwner)
        {
            // 当前为开发中占位状态，暂无业务逻辑
        }
    }
}
