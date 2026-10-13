using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数据分析（TapTap 数据分析 / TapDB）语义化封装。
///
/// 【设计意图】业务代码只调本类的方法，不直接碰 TapSDK 类型 →
/// 将来SDK 升版或更换统计方案，只改这一个文件。
///
/// 【能力来源】v4 SDK 的 TapTapEvent 定义在 TapSDK.Core 里，装 core 即自带，
/// 不需要额外的 analytics 模块（官方 Unity 模块清单里也没有这一项）。
///
/// 【埋点铁律】
///  1. 事件名与属性名一律以 # 开头（官方规定）；
///  2. 属性必须先在 TapTap 后台「配置 → 事件属性管理」预登记，否则会被丢；
///  3.属性值：字符串 ≤ 256 字符，数值 ∈ [-9E15, 9E15]；
///  4. 禁止上报任何个人信息（密码 / 支付明细 / 位置 / 相册 / 通讯录）；
///  5. 关卡一律上报 ID，不上报中文标题（否则数据维度爆炸）。
///
/// 【失败口径】一律 Debug.LogError，不静默（项目纪律第 12 条）。
/// </summary>
public static class GameAnalytics
{
    // ==================== 事件名常量（与TapTap 后台预登记一一对应） ====================

    public const string EvRunStart = "#run_start";
    public const string EvRunEnd = "#run_end";
    public const string EvStageEnter = "#stage_enter";
    public const string EvChapterClear = "#chapter_clear";
    public const string EvDraftPull = "#draft_pull";
    public const string EvEquipReplace = "#equip_replace";
    public const string EvTalentPick = "#talent_pick";
    public const string EvGuideStep = "#guide_step";
    public const string EvAppLaunch = "#app_launch";

    // ==================== 通用属性（每条事件自动携带） ====================

    static bool _commonRegistered;

    static void EnsureCommonProperties()
    {
        if (_commonRegistered) return;
        _commonRegistered = true;

#if TAPSDK_ENABLED
        try
        {
            // 平台标识：编译期常量，不采集任何设备指纹
            RegisterCommonPropertySafe("#platform", GetPlatformName());
            RegisterCommonPropertySafe("#game_version", Application.version);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] 注册通用属性失败：{e.Message}");
        }
#endif
    }

    static string GetPlatformName()
    {
#if UNITY_ANDROID
        return "android";
#elif UNITY_IOS
        return "ios";
#elif UNITY_WEBGL
        return "wechat_webgl";
#else
        return "other";
#endif
    }

    // ==================== 账号身份 ====================

    /// <summary>设置上报用户 ID。TapTap 账号传 openId，游客传本地 UUID。</summary>
    public static void SetUserId(string userId, bool isTapAccount)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogError("[Analytics] SetUserId 收到空 userId");
            return;
        }

        EnsureCommonProperties();

#if TAPSDK_ENABLED
        try
        {
            var props = new Dictionary<string, object>
            {
                { "#is_tap_account", isTapAccount ? 1 : 0 },
            };
            TapSDK.Core.TapTapEvent.SetUserID(userId, props);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] SetUserID 失败：{e.Message}");
            return;
        }
#endif
        Debug.Log($"[Analytics] 已绑定上报用户：{(isTapAccount ? "TapTap账号" : "游客")}");
    }

    /// <summary>登出时清除账号 ID，之后上报不再带账号。</summary>
    public static void ClearUser()
    {
#if TAPSDK_ENABLED
        try
        {
            TapSDK.Core.TapTapEvent.ClearUser();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] ClearUser 失败：{e.Message}");
        }
#endif
    }

    static void RegisterCommonPropertySafe(string key, string value)
    {
        try
        {
            TapSDK.Core.TapTapEvent.AddCommonProperty(key, value);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] AddCommonProperty({key}) 失败：{e.Message}");
        }
    }

    // ==================== 事件上报 ====================

    /// <summary>统一出口。所有语义方法最终都走这里。</summary>
    static void Log(string eventName, Dictionary<string, object> props)
    {
        EnsureCommonProperties();

#if TAPSDK_ENABLED
        try
        {
            TapSDK.Core.TapTapEvent.LogEvent(eventName, props);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] 上报 {eventName} 失败：{e.Message}");
        }
#endif
        GamePerf.Log($"[Analytics] {eventName} <- {Describe(props)}");
    }

    static string Describe(Dictionary<string, object> props)
    {
        if (props == null || props.Count == 0) return "{}";
        var parts = new List<string>(props.Count);
        foreach (var kv in props) parts.Add($"{kv.Key}={kv.Value}");
        return "{" + string.Join(",", parts) + "}";
    }

    // ==================== 语义化接口（业务层只调这些） ====================

    /// <summary>游戏启动（登录页显示后）。</summary>
    public static void ReportAppLaunch()
    {
        Log(EvAppLaunch, new Dictionary<string, object>());
    }

    /// <summary>进入一局冒险。</summary>
    public static void ReportRunStart(int chapter, int stageId, int runIndex, int gold)
    {
        Log(EvRunStart, new Dictionary<string, object>
        {
            { "#chapter", chapter },
            { "#stage_id", stageId },
            { "#run_index", runIndex },
            { "#gold", gold },
        });
    }

    /// <summary>
    /// 一局结束（胜利/阵亡都上报）。参数全部来自 BattleManager 结算快照，不额外采集。
    /// goldGained 与 BattleRunStats.GoldGained 一致为 long（此处转 long 传，避免溢出）。
    /// </summary>
    public static void ReportRunEnd(
        bool isDeath, bool isVictory, int chapter, int stageId,
        long goldGained, int talentGained, int equipCount,
        int stoneDelta, int decomposeDelta, string mvp, float durationSec)
    {
        Log(EvRunEnd, new Dictionary<string, object>
        {
            { "#is_death", isDeath ? 1 : 0 },
            { "#is_victory", isVictory ? 1 : 0 },
            { "#chapter", chapter },
            { "#stage_id", stageId },
            { "#gold_gained", goldGained },
            { "#talent_gained", talentGained },
            { "#equip_count", equipCount },
            { "#stone_delta", stoneDelta },
            { "#decompose_delta", decomposeDelta },
            { "#mvp", string.IsNullOrEmpty(mvp) ? "none" : mvp },
            { "#duration_sec", Mathf.Max(0, Mathf.RoundToInt(durationSec)) },
        });
    }

    /// <summary>进入某一关。</summary>
    public static void ReportStageEnter(int chapter, int stageId)
    {
        Log(EvStageEnter, new Dictionary<string, object>
        {
            { "#chapter", chapter },
            { "#stage_id", stageId },
        });
    }

    /// <summary>章节通关。</summary>
    public static void ReportChapterClear(int chapter, int runCount, int deathCount)
    {
        Log(EvChapterClear, new Dictionary<string, object>
        {
            { "#chapter", chapter },
            { "#run_count", runCount },
            { "#death_count", deathCount },
        });
    }

    /// <summary>抽奖。poolType：0=随机 1=定向；rarity 见 RarityPalette。</summary>
    public static void ReportDraftPull(int poolType, int rarity, int goldCost)
    {
        Log(EvDraftPull, new Dictionary<string, object>
        {
            { "#pool_type", poolType },
            { "#rarity", rarity },
            { "#gold_cost", goldCost },
        });
    }

    /// <summary>装备替换确认。powerDelta 为正=变强，负=变弱。</summary>
    public static void ReportEquipReplace(int powerDelta, int sameAttrCount)
    {
        Log(EvEquipReplace, new Dictionary<string, object>
        {
            { "#power_delta", powerDelta },
            { "#same_attr_count", sameAttrCount },
        });
    }

    /// <summary>天赋选择。</summary>
    public static void ReportTalentPick(string talentId, int talentCost)
    {
        Log(EvTalentPick, new Dictionary<string, object>
        {
            { "#talent_id", talentId ?? "unknown" },
            { "#talent_cost", talentCost },
        });
    }

    /// <summary>新手引导进度（14 步）。</summary>
    public static void ReportGuideStep(int step)
    {
        Log(EvGuideStep, new Dictionary<string, object>
        {
            { "#step", step },
        });
    }

    // ==================== 账号属性（低频累计） ====================

    public static void AddRunCount()
    {
        AddUserProperty("#total_runs");
    }

    public static void AddDeathCount()
    {
        AddUserProperty("#total_deaths");
    }

    public static void AddLoginDay()
    {
        AddUserProperty("#total_login_days");
    }

    static void AddUserProperty(string key)
    {
#if TAPSDK_ENABLED
        try
        {
            // 官方 UserAdd 签名为 JSON 字符串（非 Dictionary）
            TapSDK.Core.TapTapEvent.UserAdd($"{{\"{key}\":1}}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] UserAdd({key}) 失败：{e.Message}");
        }
#endif
    }

    public static void UpdateHighestChapter(int chapter)
    {
#if TAPSDK_ENABLED
        try
        {
            TapSDK.Core.TapTapEvent.UserUpdate($"{{\"#highest_chapter\":{chapter}}}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] UserUpdate(#highest_chapter) 失败：{e.Message}");
        }
#endif
    }
}