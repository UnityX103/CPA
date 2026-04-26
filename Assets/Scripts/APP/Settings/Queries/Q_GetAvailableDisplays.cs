using System.Collections.Generic;
using Kirurobo;
using QFramework;
using UnityEngine;

namespace APP.Settings.Queries
{
    /// <summary>显示器条目：序号 + 友好显示名（含分辨率）。</summary>
    public readonly struct DisplayChoice
    {
        public readonly int    Index;
        public readonly string Label;
        public DisplayChoice(int index, string label)
        {
            Index = index;
            Label = label;
        }
    }

    /// <summary>
    /// 列出当前系统可用的所有显示器。Editor / 运行时统一通过 UniWindowController 拿数据；
    /// 失败 / 无显示器时返回单条占位（"显示器 1"）以避免下拉为空。
    /// </summary>
    public sealed class Q_GetAvailableDisplays : AbstractQuery<IReadOnlyList<DisplayChoice>>
    {
        protected override IReadOnlyList<DisplayChoice> OnDo()
        {
            var list = new List<DisplayChoice>();
            int count = 0;
            try { count = UniWindowController.GetMonitorCount(); }
            catch { count = 0; }

            if (count <= 0)
            {
                list.Add(new DisplayChoice(0, "显示器 1"));
                return list;
            }

            for (int i = 0; i < count; i++)
            {
                Rect rect;
                try { rect = UniWindowController.GetMonitorRect(i); }
                catch { rect = default; }

                string label = rect.width > 0 && rect.height > 0
                    ? $"显示器 {i + 1}（{Mathf.RoundToInt(rect.width)}×{Mathf.RoundToInt(rect.height)}）"
                    : $"显示器 {i + 1}";
                list.Add(new DisplayChoice(i, label));
            }
            return list;
        }
    }
}
