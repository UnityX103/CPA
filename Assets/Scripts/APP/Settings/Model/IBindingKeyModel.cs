using System.Collections.Generic;
using QFramework;

namespace APP.Settings.Model
{
    /// <summary>
    /// 按键计数 Model（多绑定）。
    /// 全局 Enabled 控制整个功能；Entries 是所有绑定条目；
    /// SyncedKeyId 标记唯一一个被同步到远端的条目（""=不同步）；
    /// ListeningKeyId 标记当前哪个条目处于"监听输入设置态"（""=无）。
    ///
    /// 修改 Entries 必须经由 AddEntry / RemoveEntry / TryUpdateEntry /
    /// IncrementEntry / SetEntryEnabled —— 每次成功修改后 EntriesRevision++，
    /// UI 侧通过订阅 EntriesRevision 来触发整表 rebuild。
    /// </summary>
    public interface IBindingKeyModel : IModel
    {
        /// <summary>全局按键计数功能开关。持久化。</summary>
        BindableProperty<bool> Enabled { get; }

        /// <summary>所有绑定条目（只读快照）。修改请用方法 API。</summary>
        IReadOnlyList<BindingKeyEntry> Entries { get; }

        /// <summary>条目集合的"版本号"，每次集合或条目内容变化 +1。订阅它即可重渲列表。</summary>
        BindableProperty<int> EntriesRevision { get; }

        /// <summary>当前同步到远端的条目 Id（""=不同步）。持久化。</summary>
        BindableProperty<string> SyncedKeyId { get; }

        /// <summary>当前监听输入设置态的条目 Id（""=无）。不持久化（ephemeral）。</summary>
        BindableProperty<string> ListeningKeyId { get; }

        // ── 列表方法 ──────────────────────────────────────────

        /// <summary>新增一个默认绑定（"鼠标左键"+enabled=false），返回新 Id。</summary>
        string AddEntry();

        /// <summary>移除指定 Id；若是 SyncedKeyId 也会被清空。返回是否成功。</summary>
        bool RemoveEntry(string id);

        /// <summary>更新指定条目的键。返回是否成功。</summary>
        bool TryUpdateEntryKey(string id, int keyCode, string keyLabel);

        /// <summary>条目计数 +1。</summary>
        bool IncrementEntry(string id);

        /// <summary>重置条目计数到 0。</summary>
        bool ResetEntryCount(string id);

        /// <summary>切换条目激活态。</summary>
        bool SetEntryEnabled(string id, bool enabled);
    }
}
