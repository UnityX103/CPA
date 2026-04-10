namespace APP.Pomodoro.Controller
{
    /// <summary>
    /// 设置面板通用接口。
    /// 每个设置面板（番茄钟/联机/宠物）各自拥有独立 UIDocument，
    /// DeskWindowController 通过此接口统一管理显隐。
    /// </summary>
    public interface ISettingsPanel
    {
        void Show();
        void Hide();
        bool IsVisible { get; }
    }
}
