namespace APP.Pomodoro.Event
{
    /// <summary>请求打开统一设置面板（独立 UIDocument 的 Driver 订阅并显示）。</summary>
    public readonly struct E_OpenUnifiedSettings { }

    /// <summary>请求关闭统一设置面板。</summary>
    public readonly struct E_CloseUnifiedSettings { }
}
