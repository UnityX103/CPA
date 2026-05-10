using System;
using System.Collections;
using System.Text;
using APP.Pomodoro.Model;
using Kirurobo;
using QFramework;
using UnityEngine;

namespace APP.Pomodoro.System
{
    public sealed class WindowPositionSystem : AbstractSystem, IWindowPositionSystem
    {
        private UniWindowController _uwc;
        private Coroutine _refitCoroutine;

        public bool IsTopmost => _uwc != null && _uwc.isTopmost;

        protected override void OnInit() { }

        public void Initialize(UniWindowController uwc)
        {
            _uwc = uwc;

            // 我们自己控制窗口几何（铺满目标显示器），必须关掉 UniWindowController 自带的
            // shouldFitMonitor —— 它会在每次 SetWindowSize 触发的 ObserveWindowStyleChanged
            // 后启动 ForceZoomed 协程，0.5s 后 SetZoomed(true) 把窗口最大化，吞掉我们的自定义尺寸。
            if (_uwc != null && _uwc.shouldFitMonitor)
            {
                Debug.Log("[WindowPositionSystem][init] 检测到 shouldFitMonitor=true，强制关闭以避免 UWC 自动 zoom 覆盖我们的 size");
                _uwc.shouldFitMonitor = false;
            }

            Debug.Log("[WindowPositionSystem][init] 初始化完成（铺满目标显示器模式）");
            DumpAllMonitorRects("init");
            DumpScreenState("init/post");
            DumpUwcState("init/post");
        }

        public void MoveToMonitor(int monitorIndex)
        {
            IPomodoroModel model = this.GetModel<IPomodoroModel>();
            int monitorCount = UniWindowController.GetMonitorCount();
            Debug.Log($"[WindowPositionSystem][MoveToMonitor] 入参 monitorIndex={monitorIndex}, monitorCount={monitorCount}, " +
                      $"model.TargetMonitorIndex={model.TargetMonitorIndex.Value}");

            if (monitorCount <= 0)
            {
                Debug.LogWarning("[WindowPositionSystem][MoveToMonitor] monitorCount<=0, 重置 TargetMonitorIndex=0 后返回");
                model.TargetMonitorIndex.Value = 0;
                return;
            }

            int safeIndex = Mathf.Clamp(monitorIndex, 0, monitorCount - 1);
            int previousIndex = model.TargetMonitorIndex.Value;
            model.TargetMonitorIndex.Value = safeIndex;

            // 不短路 safeIndex==previousIndex —— 冷启动时 model.TargetMonitorIndex 默认就是 0，
            // 若短路会跳过 ApplyMonitorRect，窗口保持 Editor / macOS 默认几何，直到用户首次切屏
            // 才被塑形。ApplyMonitorRect 幂等，重复调用只是多跑一次 refit 协程，无害。
            ApplyMonitorRect(safeIndex, $"MoveToMonitor({previousIndex} → {safeIndex})");
        }

        public void PreviewMoveToMonitor(int monitorIndex)
        {
            int monitorCount = UniWindowController.GetMonitorCount();
            Debug.Log($"[WindowPositionSystem][PreviewMoveToMonitor] 入参 monitorIndex={monitorIndex}, monitorCount={monitorCount}");
            if (monitorCount <= 0)
            {
                Debug.LogWarning("[WindowPositionSystem][PreviewMoveToMonitor] monitorCount<=0, 直接返回");
                return;
            }

            int safeIndex = Mathf.Clamp(monitorIndex, 0, monitorCount - 1);
            ApplyMonitorRect(safeIndex, $"PreviewMoveToMonitor({safeIndex})");
        }

        private void ApplyMonitorRect(int safeIndex, string logTag)
        {
            if (_uwc == null)
            {
                Debug.LogError($"[WindowPositionSystem][{logTag}] _uwc==null, 无法 apply rect");
                return;
            }

            // ─── 切屏前：dump 当前状态 ───
            Debug.Log($"[WindowPositionSystem][{logTag}] === 切屏前快照 ===");
            DumpScreenState($"{logTag}/pre");
            DumpUwcState($"{logTag}/pre");
            DumpAllMonitorRects($"{logTag}/pre");

            Rect monitorRect = UniWindowController.GetMonitorRect(safeIndex);
            Debug.Log($"[WindowPositionSystem][{logTag}] 目标显示器 [{safeIndex}] rect=" +
                      $"(x={monitorRect.x}, y={monitorRect.y}, w={monitorRect.width}, h={monitorRect.height})");

            if (monitorRect.width <= 0f || monitorRect.height <= 0f)
            {
                Debug.LogWarning($"[WindowPositionSystem][{logTag}] 无法获取显示器 {safeIndex} 的有效区域。");
                return;
            }

            float x = monitorRect.x;
            float y = monitorRect.y;
            float w = monitorRect.width;
            float h = monitorRect.height;

            Debug.Log($"[WindowPositionSystem][{logTag}] 铺满目标显示器 → position=({x},{y}), size=({w},{h})");

            _uwc.windowPosition = new Vector2(x, y);
            _uwc.windowSize = new Vector2(w, h);

            // ─── 同帧 readback：看 UWC 是否吃下了这个尺寸 ───
            Debug.Log($"[WindowPositionSystem][{logTag}] 写入后立即 readback:");
            DumpUwcState($"{logTag}/write/readback");
            DumpScreenState($"{logTag}/write/readback");

            // 跨显示器（尤其 Retina ↔ 非 Retina）切换时，Unity 的 Screen.width/height/dpi
            // 需要 1~2 帧才能绑定到新显示器的像素网格；同帧设置的 size 会用旧 DPI 上下文落点，
            // 导致 UI Toolkit 面板布局与窗口实际像素尺寸错位。延迟一帧后再次写入位置/尺寸，
            // 让窗口在 Unity 完成显示器重新绑定后再做一次"对齐校正"。
            if (_refitCoroutine != null)
            {
                _uwc.StopCoroutine(_refitCoroutine);
            }
            _refitCoroutine = _uwc.StartCoroutine(RefitAfterMonitorBound(x, y, w, h, logTag));
        }

        private IEnumerator RefitAfterMonitorBound(float x, float y, float width, float height, string logTag)
        {
            // ─── 第 1 帧后：观察 UWC/Screen 是否已经反映出新的显示器 ───
            yield return null;
            if (_uwc == null) yield break;
            Debug.Log($"[WindowPositionSystem][{logTag}] === +1 帧 ===");
            DumpUwcState($"{logTag}/+1f");
            DumpScreenState($"{logTag}/+1f");

            // ─── 第 2 帧后：再写一次（refit），并 readback ───
            yield return null;
            if (_uwc == null) yield break;

            Debug.Log($"[WindowPositionSystem][{logTag}] === +2 帧（refit 写入） ===");
            DumpUwcState($"{logTag}/+2f/pre-refit");
            DumpScreenState($"{logTag}/+2f/pre-refit");

            _uwc.windowPosition = new Vector2(x, y);
            _uwc.windowSize = new Vector2(width, height);
            Debug.Log($"[WindowPositionSystem][{logTag}] refit 写入: position=({x},{y}), size=({width},{height})");

            DumpUwcState($"{logTag}/+2f/post-refit");
            DumpScreenState($"{logTag}/+2f/post-refit");

            // ─── 再观察 5 帧，确认没有"被改回去" ───
            for (int i = 1; i <= 5; i++)
            {
                yield return null;
                if (_uwc == null) yield break;
                Debug.Log($"[WindowPositionSystem][{logTag}] === refit +{i} 帧 观测 ===");
                DumpUwcState($"{logTag}/refit+{i}f");
                DumpScreenState($"{logTag}/refit+{i}f");
            }

            _refitCoroutine = null;
            Debug.Log($"[WindowPositionSystem][{logTag}] === 切屏流程结束 ===");
        }

        public void SetTopmost(bool isTopmost)
        {
            if (_uwc != null)
            {
                _uwc.isTopmost = isTopmost;
                Debug.Log($"[WindowPositionSystem] SetTopmost({isTopmost})");
            }
        }

        // ─── 日志辅助 ─────────────────────────────────────────────

        private void DumpUwcState(string tag)
        {
            if (_uwc == null) { Debug.Log($"[WindowPositionSystem][{tag}] uwc=null"); return; }
            try
            {
                Vector2 pos = _uwc.windowPosition;
                Vector2 size = _uwc.windowSize;
                Debug.Log($"[WindowPositionSystem][{tag}] uwc.windowPosition=({pos.x},{pos.y}), " +
                          $"uwc.windowSize=({size.x},{size.y}), " +
                          $"shouldFitMonitor={_uwc.shouldFitMonitor}, isTopmost={_uwc.isTopmost}, " +
                          $"isTransparent={_uwc.isTransparent}, isZoomed={_uwc.isZoomed}, " +
                          $"monitorIndex(uwc)={_uwc.monitorToFit}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WindowPositionSystem][{tag}] DumpUwcState 失败: {ex.Message}");
            }
        }

        private static void DumpScreenState(string tag)
        {
            try
            {
                Debug.Log($"[WindowPositionSystem][{tag}] Screen.width={Screen.width}, Screen.height={Screen.height}, " +
                          $"Screen.dpi={Screen.dpi}, Screen.currentResolution={Screen.currentResolution}, " +
                          $"Screen.fullScreen={Screen.fullScreen}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WindowPositionSystem][{tag}] DumpScreenState 失败: {ex.Message}");
            }
        }

        private static void DumpAllMonitorRects(string tag)
        {
            try
            {
                int count = UniWindowController.GetMonitorCount();
                var sb = new StringBuilder();
                sb.Append($"[WindowPositionSystem][{tag}] 显示器列表 count={count}: ");
                for (int i = 0; i < count; i++)
                {
                    Rect r = UniWindowController.GetMonitorRect(i);
                    sb.Append($"[{i}](x={r.x},y={r.y},w={r.width},h={r.height}) ");
                }
                Debug.Log(sb.ToString());
            }
            catch 
            {
                Debug.LogWarning($"[WindowPositionSystem][{tag}] DumpAllMonitorRects 失败:");
            }
        }
    }
}
