using UnityEngine;
#if UNITY_WECHATMINIGAME
using WeChatWASM;
#endif

/// <summary>
/// 微信小游戏「场景分析（自定义分析）」上报桥（C# 门面 → 官方 WX SDK）。
///
/// 【2026-11-27 接入】对应微信公众平台「统计 → 基础数据 → 场景分析」：
/// 官方接口 wx.reportUserBehaviorBranchAnalytics（基础库 2.12.0+），SDK 签名为
/// WXBase.ReportUserBehaviorBranchAnalytics(string branchId, string branchDim, int eventType)。
/// 用途：看「新手引导通过率 / 第 N 章通过率」这类自定义漏斗的人数与百分比（后台「漏斗分析」直接出）。
///
/// 设计（沿用 WeChatAd / WeChatAnalyticsBridge 口径）：
/// - 仅微信小游戏构建（UNITY_WECHATMINIGAME）真正上报；其它平台只打日志。
/// - branchId 由后台建场景后分配，本文件里用常量占位；**未填（空串）时拒绝上报并打警告**，
///   不静默发脏数据（项目「不要兜底、出错要看见」口径）。
/// - branchDim = 自定义维度，必须是 1~100 的整数字符串（官方硬约束）；关卡 8 章各一个 branchId、引导一个，故 dim 统一固定 1（曝光/点击同维度才配得成对）。
/// - eventType：1=曝光，2=点击。漏斗思路：入口报曝光(1)、完成报点击(2)，后台按 曝光→点击 看转化率。
/// - 所有上报 try/catch 包住，禁止因埋点抛异常中断游戏。
/// </summary>
public static class WeChatSceneAnalyticsBridge
{
    /// <summary>新手引导场景的分支 ID（mp 后台「新手引导」场景分配，2026-11-27）。</summary>
    public const string BranchTutorial = "BCBgAAoXHx5d1g2oaP3wEP";

    /// <summary>
    /// 关卡推进：8 章各建一个场景，章号 → 分支 ID（下标 = 章号 - 1，[0]=第1章 … [7]=第8章）。
    /// 【2026-11-27 主人拍板「8 个章节都建一个场景」】8 个 branchId 已全部填齐（引导另见 BranchTutorial）。
    /// 下标 = 章号 - 1，勿 off-by-one；若将来有章留空，桥内打警告跳过（不静默发脏数据）。
    /// </summary>
    static readonly string[] StageBranchByChapter = new string[8]
    {
        "BCBgAAoXHx5d1g2oaP3wEM", // 第 1 章（mp 后台「第1章通关」场景分配，2026-11-27）
        "BCBgAAoXHx5d1g2oaP3wEN", // 第 2 章
        "BCBgAAoXHx5d1g2oaP3wEK", // 第 3 章
        "BCBgAAoXHx5d1g2oaP3wEL", // 第 4 章
        "BCBgAAoXHx5d1g2oaP3wEI", // 第 5 章
        "BCBgAAoXHx5d1g2oaP3wEJ", // 第 6 章
        "BCBgAAoXHx5d1g2oaP3wEG", // 第 7 章
        "BCBgAAoXHx5d1g2oaP3wEH", // 第 8 章
    };

    /// <summary>按章号（1~8）取关卡场景分支 ID；越界或未配置返回空串。</summary>
    public static string StageBranch(int chapter)
    {
        int i = chapter - 1;
        return (i >= 0 && i < StageBranchByChapter.Length) ? StageBranchByChapter[i] : "";
    }

    /// <summary>事件类型：曝光（漏斗起点）。</summary>
    public const int EventExposure = 1;

    /// <summary>事件类型：点击（漏斗终点）。</summary>
    public const int EventClick = 2;

    /// <summary>是否处于微信小游戏运行时（编译期宏，非微信构建恒 false）。</summary>
    public static bool Available
    {
        get
        {
#if UNITY_WECHATMINIGAME
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 上报一条场景分析事件。branchId 未配置时只打警告并返回（不静默发脏数据）。
    /// dim 必须是 1~100 的整数；非法值钳到 1，避免脏数据被后台丢弃却不报错。
    /// </summary>
    public static void Report(string branchId, int eventType, int dim)
    {
        if (SpotlightBuild.Enabled)
        {
            Debug.Log($"[WeChatScene] Spotlight：跳过场景分析上报 {branchId}/{eventType}/{dim}");
            return;
        }
        if (!Available) return;
        if (string.IsNullOrEmpty(branchId))
        {
            Debug.LogWarning($"[WeChatScene] 场景分支 ID 未配置（BranchTutorial 或 StageBranchByChapter[章号-1] 为空），跳过上报。请先到 mp 后台 统计→基础数据→场景分析 建场景拿 branchId。");
            return;
        }
        if (eventType != EventExposure && eventType != EventClick)
        {
            Debug.LogError($"[WeChatScene] 非法 eventType={eventType}（仅 1 曝光 / 2 点击），本次拒绝上报");
            return;
        }

        string branchDim = Mathf.Clamp(dim, 1, 100).ToString();
#if UNITY_WECHATMINIGAME
        try
        {
            WXBase.ReportUserBehaviorBranchAnalytics(branchId, branchDim, eventType);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WeChatScene] 场景分析上报失败（已吞掉）: {branchId}/{eventType}/{dim} {e.Message}");
        }
#endif
    }
}
