using System.Collections;
using System.IO;
using NUnit.Framework;
using NZ.VisualTest;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace APP.NetworkIntegration.Tests
{
    /// <summary>
    /// 视觉测试：覆盖 Assets/UI_V2/Documents/Components/ 下全部 16 个原子组件的默认状态截屏。
    ///
    /// 与 OnlineCopyButtonImageValidationTests 等"在 MainV2 场景里截整屏"的集成测试不同，
    /// 本夹具不依赖任何场景：每个测试自行创建临时 GameObject + UIDocument，把单个 .uxml
    /// 挂到一个新的 PanelSettings 渲染根上，并通过 rootVisualElement 的 absolute 居中布局
    /// 让组件出现在 game view 中央，避免组件被挤在左上角看不清。
    ///
    /// 项目硬性规则：所有视觉测试都用 CaptureScreenStep 截整屏，禁用 CaptureStep(target,...)。
    /// baseline 路径形如 TestArtifacts/PencilReferences/components/&lt;kebab-name&gt;.png，
    /// 由 Pencil 导出脚本另行准备，本测试不断言 baseline 文件存在。
    /// </summary>
    [TestFixture]
    public sealed class ComponentImageValidationTests : VisualImageTestBase
    {
        private const string ComponentDirectory = "Assets/UI_V2/Documents/Components";
        private const string PanelSettingsAssetPath = "Assets/UI_V2/PanelSettings_Settings.asset";
        private const string BaselineDirectory = "TestArtifacts/PencilReferences/components";

        // 与 GlobalSettingsScaleSliderImageValidationTests 一致：限定 root 宽度为典型面板内容宽度，
        // 避免 width:100% 的组件被拉到整张 game view 那么宽，导致原生 <ui:Slider> 的
        // drag-container 暴露默认装饰、fill 计算位置错位等"非组件本身"的视觉假象。
        private const float HostWidth = 360f;
        private const float HostHeight = 120f;

        // InputSlider 专用：复刻面板里运行时同步 fill 宽度的逻辑（USS 没法表达跟随 dragger 位置）。
        private const float SliderMinValue = 0.5f;
        private const float SliderMaxValue = 2.0f;
        private const float SliderDraggerSize = 24f;
        private const float SliderFillLeftInset = 0f;

        // 用单一字段持有当前测试 host，TearDownComponentHost 兜底销毁，避免遗漏导致 GameObject 泄漏。
        private GameObject _host;
        private GameObject _cameraHost;

        [TearDown]
        public void TearDownComponentHost()
        {
            if (_host != null)
            {
                // 必须 DestroyImmediate：Object.Destroy 是延迟到帧末，
                // 下一个测试已经 SetUp + Capture 完了，前一个 host 还活着，
                // 会污染 actual 截图（典型表现：ToggleSwitch 截图里出现上一个 SettingsApplyRow 的"应用"按钮）。
                UIDocument doc = _host.GetComponent<UIDocument>();
                if (doc != null && doc.panelSettings != null)
                {
                    Object.DestroyImmediate(doc.panelSettings);
                }
                Object.DestroyImmediate(_host);
                _host = null;
            }
            if (_cameraHost != null)
            {
                Object.DestroyImmediate(_cameraHost);
                _cameraHost = null;
            }
        }

        [UnityTest]
        public IEnumerator ButtonClose_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ButtonClose", "button-close");
        }

        [UnityTest]
        public IEnumerator ButtonCopy_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ButtonCopy", "button-copy");
        }

        [UnityTest]
        public IEnumerator ButtonIcon_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ButtonIcon", "button-icon");
        }

        [UnityTest]
        public IEnumerator ButtonPin_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ButtonPin", "button-pin");
        }

        [UnityTest]
        public IEnumerator ButtonPrimary_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ButtonPrimary", "button-primary");
        }

        [UnityTest]
        public IEnumerator ButtonSecondary_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ButtonSecondary", "button-secondary");
        }

        [UnityTest]
        public IEnumerator ButtonTab_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ButtonTab", "button-tab");
        }

        [UnityTest]
        public IEnumerator InputDropdown_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("InputDropdown", "input-dropdown");
        }

        [UnityTest]
        public IEnumerator InputSlider_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("InputSlider", "input-slider");
        }

        [UnityTest]
        public IEnumerator InputText_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("InputText", "input-text");
        }

        [UnityTest]
        public IEnumerator InputTextSuffix_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("InputTextSuffix", "input-text-suffix");
        }

        [UnityTest]
        public IEnumerator PanelTitle_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("PanelTitle", "panel-title");
        }

        [UnityTest]
        public IEnumerator RoomHistoryItem_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("RoomHistoryItem", "room-history-item");
        }

        [UnityTest]
        public IEnumerator RoomMemberItem_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("RoomMemberItem", "room-member-item");
        }

        [UnityTest]
        public IEnumerator SettingsApplyRow_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("SettingsApplyRow", "settings-apply-row");
        }

        [UnityTest]
        public IEnumerator ToggleSwitch_ShouldCaptureDefaultState()
        {
            yield return MountAndCapture("ToggleSwitch", "toggle-switch");
        }

        /// <summary>
        /// 16 个测试方法的统一执行流程：
        /// 1. 编辑器外直接 Ignore；
        /// 2. 加载 .uxml + PanelSettings；
        /// 3. 建一次性 GameObject + UIDocument 作为渲染宿主；
        /// 4. 把组件根元素居中显示在屏幕中央；
        /// 5. 等若干帧确保布局完成；
        /// 6. 调 CaptureScreenStep 截整屏；
        /// 7. 校验 manifest 与产物 PNG 已落盘。
        /// </summary>
        private IEnumerator MountAndCapture(string componentName, string kebabName)
        {
#if !UNITY_EDITOR
            Assert.Ignore("图片视觉测试仅支持 Unity Editor PlayMode。");
            yield break;
#else
            string uxmlPath = $"{ComponentDirectory}/{componentName}.uxml";
            string baselinePath = $"{BaselineDirectory}/{kebabName}.png";
            string stepName = kebabName;

            // 偏执清理：场景里所有遗留 UIDocument / Camera 全部就地销毁，
            // 避免上一个测试的 host 因 Destroy 延迟生效而残留到 actual 里。
            foreach (UIDocument leftoverDoc in
                     Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (leftoverDoc != null && leftoverDoc.gameObject != null)
                {
                    Object.DestroyImmediate(leftoverDoc.gameObject);
                }
            }
            foreach (Camera leftoverCam in
                     Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (leftoverCam != null && leftoverCam.gameObject != null)
                {
                    Object.DestroyImmediate(leftoverCam.gameObject);
                }
            }

            // 每个测试创建独立的纯黑背景摄像机：
            //   - clearFlags=SolidColor + black 把整张 game view 先刷成纯黑底；
            //   - 这样组件浮在黑底上，跨测试不会出现"上一张白底里残留另一组件"的视觉污染；
            //   - depth=100 确保渲染顺序高于场景里任何残留摄像机。
            _cameraHost = new GameObject($"VisualTestCamera_{stepName}");
            Camera testCamera = _cameraHost.AddComponent<Camera>();
            testCamera.clearFlags = CameraClearFlags.SolidColor;
            testCamera.backgroundColor = Color.black;
            testCamera.depth = 100f;
            testCamera.orthographic = true;
            testCamera.cullingMask = 0; // 摄像机只负责清屏，不渲染场景内容

            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            Assert.That(visualTreeAsset, Is.Not.Null,
                $"无法加载组件 UXML：{uxmlPath}（请确认 Components 目录与文件名）。");

            PanelSettings panelSettingsAsset =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsAssetPath);
            Assert.That(panelSettingsAsset, Is.Not.Null,
                $"无法加载 PanelSettings：{PanelSettingsAssetPath}。");

            // 克隆 PanelSettings 并锁 scale=1，避免被项目运行时的全局缩放污染 actual 截图，
            // 与 GlobalSettingsScaleSliderImageValidationTests 的做法保持一致。
            PanelSettings panelSettings = Object.Instantiate(panelSettingsAsset);
            panelSettings.scale = 1f;

            _host = new GameObject($"VisualTestHost_{stepName}");
            UIDocument uiDocument = _host.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = visualTreeAsset;

            // 关键：root 用固定 360×120 的小盒子放在 game view 左上，
            // 不再 absolute 全屏 + alignItems center。原因——
            //   width:100% / flex-grow:1 的组件（InputSlider/InputText/InputDropdown/PanelTitle 等）
            //   在 1366px 全屏宿主里会被横向拉满，原生 <ui:Slider> 的 drag-container
            //   会露出默认装饰（dragger 旁的"耳朵/凹槽"假象），fill 也无法跟随 dragger 位置。
            //   限定为典型面板内容宽度后，组件按设计稿的 panel-context 宽度渲染。
            VisualElement rootVisualElement = uiDocument.rootVisualElement;
            Assert.That(rootVisualElement, Is.Not.Null,
                "UIDocument.rootVisualElement 不应为空。");

            rootVisualElement.style.position = Position.Absolute;
            rootVisualElement.style.left = 0f;
            rootVisualElement.style.top = 0f;
            rootVisualElement.style.width = HostWidth;
            rootVisualElement.style.height = HostHeight;
            rootVisualElement.style.backgroundColor = Color.clear;
            rootVisualElement.style.alignItems = Align.Center;
            rootVisualElement.style.justifyContent = Justify.Center;

            yield return WaitForFrames(10);

            // 项目里多数组件的 .uxml 根元素带 name="root"，
            // 但 InputSlider/PanelTitle/SettingsApplyRow 等少数组件根元素只有 class。
            // 优先按 name 找，找不到时回退到 rootVisualElement 的第一个子节点（即 UXML 根）。
            VisualElement componentRoot = rootVisualElement.Q<VisualElement>("root");
            if (componentRoot == null && rootVisualElement.childCount > 0)
            {
                componentRoot = rootVisualElement[0];
            }

            Assert.That(componentRoot, Is.Not.Null,
                $"未找到组件 {componentName} 的根 VisualElement（既无 name=\"root\" 也无任何子节点）。");
            Assert.That(componentRoot.worldBound.width, Is.GreaterThan(0f),
                $"组件 {componentName} 渲染宽度必须大于 0。");
            Assert.That(componentRoot.worldBound.height, Is.GreaterThan(0f),
                $"组件 {componentName} 渲染高度必须大于 0。");

            // InputSlider 专用：fill 宽度由 C# 同步到 dragger 中心点（USS 表达不了），
            // 不调用就会出现"fill 横亘整个 wrap 的一半 / dragger 飘在 fill 之外"的假象。
            if (componentName == "InputSlider")
            {
                yield return SyncSliderFill(componentRoot);
            }

            yield return CaptureScreenStep(stepName, baselinePath, "component-default-state");

            Assert.That(CurrentManifest.steps.Count, Is.EqualTo(1),
                $"组件 {componentName} 应只登记 1 个 capture step。");

            string expectedActualImage = $"01-{stepName}-actual.png";
            string actualImagePath = Path.Combine(CurrentRunDirectory, expectedActualImage);
            Assert.That(File.Exists(actualImagePath), Is.True,
                $"组件 {componentName} 的截图产物未生成：{actualImagePath}");
#endif
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }
        }

#if UNITY_EDITOR
        // 复刻 GlobalSettingsScaleSliderImageValidationTests.ApplyFillWidth：
        // 把 #gsp-scale-slider-fill 的 width 与 dragger 中心点对齐，
        // 否则 USS 写死的 width:50% 会和 slider value 脱钩，画面"fill 不跟随"很怪。
        private static IEnumerator SyncSliderFill(VisualElement sliderRoot)
        {
            VisualElement wrap = sliderRoot.Q<VisualElement>("gsp-scale-slider-wrap");
            VisualElement fill = sliderRoot.Q<VisualElement>("gsp-scale-slider-fill");
            Slider slider = sliderRoot.Q<Slider>("gsp-scale-slider");
            if (wrap == null || fill == null || slider == null)
            {
                yield break;
            }

            // 等 wrap 完成布局拿到真实宽度
            for (int frame = 0; frame < 30; frame++)
            {
                if (wrap.resolvedStyle.width > 0f)
                {
                    break;
                }
                yield return null;
            }

            float trackWidth = wrap.resolvedStyle.width;
            if (trackWidth <= 0f)
            {
                yield break;
            }

            float normalized = Mathf.Clamp01(
                Mathf.InverseLerp(SliderMinValue, SliderMaxValue, slider.value));
            float dragRange = Mathf.Max(0f, trackWidth - SliderDraggerSize);
            float thumbCenterX = (SliderDraggerSize * 0.5f) + (dragRange * normalized);
            fill.style.width = Mathf.Max(0f, thumbCenterX);

            yield return null;
        }
#endif
    }
}
