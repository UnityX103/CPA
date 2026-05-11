using System;

namespace APP.Settings.Model
{
    /// <summary>
    /// 单个按键绑定条目。结构体语义，不要在 BindableProperty 里持引用比较。
    /// JSON 序列化由 BindingKeyModel 负责。
    /// </summary>
    [Serializable]
    public struct BindingKeyEntry
    {
        public string Id;          // 稳定唯一标识（GUID），UI list key
        public int    KeyCode;     // 同旧编码（>0=KeyCode, -1/-2/-3=鼠标左/右/中）
        public string KeyLabel;    // 显示文本（"鼠标左键"/"Space"/...）
        public int    PressCount;  // 该条目累计按下次数
        public bool   Enabled;     // 是否激活（影响计数 + InputCounterPanel 是否弹出）
    }
}
