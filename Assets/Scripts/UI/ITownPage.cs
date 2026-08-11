/// <summary>
/// Town 场景内功能页（公会/酒馆/角色/日志…）统一接口。
/// 进 Town 时 Preload，点击时只 Show/Hide，禁止在点击时再 Instantiate。
/// </summary>
public interface ITownPage
{
    MainNavTab Tab { get; }
    /// <summary>进 Town 后调用一次：建 UI、绑字体、关着待命</summary>
    void PreloadOnce();
    /// <summary>切到此页：轻量，禁止 Resources.Load / Instantiate</summary>
    void ShowPage();
    /// <summary>切走：轻量</summary>
    void HidePage();
}
