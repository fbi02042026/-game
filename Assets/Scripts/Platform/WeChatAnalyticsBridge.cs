using UnityEngine;

/// <summary>
/// 微信小游戏「We 分析」自定义事件上报桥（占位 + TODO）。
///
/// 现状：本项目目前<b>没有任何现成的 .jslib 或 WX SDK 封装</b>
/// （Platform 目录下只有 RewardedAdBridge / CloudSaveBridge / WeChatMiniGameConfig / SpotlightBuild，
/// 全部是 Debug.Log 占位 + TODO）。因此这里沿用 CloudSaveBridge.UseWeChatCloud 的同一套写法：
/// 用一个开关 + 占位日志，真实发送前必须在 Unity 工程里补一个桥。
///
/// 真接入时需要补的拼图（重要，勿凭空编造 API 名）：
///   · 微信 We 分析自定义事件的客户端接口文档为 <c>wx.reportEvent(eventId, object)</c>；
///   · 但 Unity C# 端没有现成封装，需要二选一：
///       (1) 写一个 .jslib（如 WeChatAnalytics.jslib）暴露一个 C 侧函数，内部调 wx.reportEvent；或
///       (2) 引入官方微信小游戏 SDK（WX SDK）后用其上报封装。
///   · 之后把下面 UseWeChatReport=true 分支里的 TODO 行替换成对该桥的调用即可，门面接口不变。
/// </summary>
public static class WeChatAnalyticsBridge
{
    /// <summary>是否已接入真实微信上报（需在构建/启动期由 WeChatMiniGameConfig 置 true）。</summary>
    public static bool UseWeChatReport { get; set; }

    /// <summary>
    /// 上报一条事件。jsonProps 为 Analytics 拼好的 JSON 字符串。
    /// 所有上报都 try/catch 包住，禁止因埋点抛异常中断游戏。
    /// </summary>
    public static void ReportEvent(string eventName, string jsonProps)
    {
        if (SpotlightBuild.Enabled)
        {
            Debug.Log($"[WeChatAnalyticsBridge] Spotlight：跳过上报 {eventName}");
            return;
        }

        if (UseWeChatReport)
        {
            // TODO: 接入微信 We 分析自定义事件上报。
            // 文档接口为 wx.reportEvent(eventId, object)，但本工程目前没有 .jslib 或 WX SDK 封装，
            // 需要在 Unity 工程内补一个桥（.jslib 暴露 WXReportEvent，或引入微信 SDK）后才能真正发送。
            // 例（伪代码，待补桥后启用）：
            //   WXReportEvent(eventName, jsonProps);
            Debug.Log($"[WeChatAnalyticsBridge] UseWeChatReport=true，但缺少 .jslib/WX SDK 封装，尚未真正发送: {eventName} {jsonProps}");
            return;
        }

        // 非微信平台（编辑器 / Android / 未接入）：走日志，方便联调，不报错。
        Debug.Log($"[Analytics] {eventName} {jsonProps}");
    }
}
