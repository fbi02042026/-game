using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// 轻量埋点门面（微信小游戏 / We 分析）。
///
/// 设计纪律（严格遵守）：
/// 1. 字段初始化器绝不碰任何静态配置表（不调 Resources.Load，不读 AttrSystem / UnitBase 等）。
///    所有缓冲懒初始化，避免 Unity 反序列化 .ctor 期间触发 UnityException。
/// 2. 所有上报都 try/catch 包住，禁止因埋点抛异常而中断游戏逻辑。
/// 3. 上报目标可随时替换（We 分析 / TapDB / 自建），门面接口不变；当前微信上报走
///    <see cref="WeChatAnalyticsBridge"/>（2026-11-27 已接官方 WX.ReportEvent，不再用占位）。
/// 4. 内部做批处理 / 节流：每 FlushBatchSize 条或每 FlushIntervalSec 秒 flush 一次，不每次都上报。
/// 5. 会话时长自己算：记住 SessionStart 的时间戳，SessionEnd 时算差值。
/// </summary>
public static class Analytics
{
    /// <summary>总开关：默认开启，可一键关掉全部上报。</summary>
    public static bool Enabled { get; set; } = true;

    const int FlushBatchSize = 20;
    const float FlushIntervalSec = 30f;

    // 批处理缓冲：全部懒初始化，静态 .ctor 期间不做任何重活（不读配置表）。
    static List<KeyValuePair<string, string>> _queue;
    static float _lastFlushTime;
    static bool _sessionActive;
    static double _sessionStartEpoch;

    // ============================================================
    // 通用上报
    // ============================================================

    /// <summary>通用事件上报。props 为键值对；内部 try/catch，失败只打警告。</summary>
    public static void Track(string eventName, params (string, object)[] props)
    {
        if (!Enabled) return;
        try
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var json = BuildPropsJson(props);
            Enqueue(eventName, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] Track 失败（已吞掉，不影响游戏）: {e.Message}");
        }
    }

    // ============================================================
    // 语义方法（内部转调 Track）
    // ============================================================

    public static void SessionStart()
    {
        if (!Enabled) return;
        try
        {
            _sessionActive = true;
            _sessionStartEpoch = EpochSeconds();
            Track("session_start");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] SessionStart 失败: {e.Message}");
        }
    }

    public static void SessionEnd()
    {
        if (!Enabled) return;
        try
        {
            double dur = _sessionActive ? (EpochSeconds() - _sessionStartEpoch) : 0d;
            _sessionActive = false;
            Track("session_end", ("duration_sec", Math.Round(dur, 1)));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] SessionEnd 失败: {e.Message}");
        }
        finally
        {
            // 会话结束顺手把缓冲清空上报，避免丢尾数据
            Flush();
        }
    }

    // 【2026-11-27 场景分析「章通关率」口径】曝光=进入本章首关，点击=打完本章大Boss。
    // 只统计正式主线（Normal），副本/竞技场/裂隙/支线岛的 stageIndex 也可能是 0，必须挡掉，否则刷假曝光。
    // 判据真源：章内关号 stageIndex 0-based（关1=0、关10=大Boss=9），见 ActProgressionDefs.BossKindOfStage。
    static bool IsFormalMainRun() => BattleManager.Instance != null && BattleManager.Instance.IsFormalMainRun;

    public static void StageStart(int chapter, int stageIndex)
    {
        Track("stage_start", ("chapter", chapter), ("stage_index", stageIndex));
        // 场景分析漏斗起点：仅正式主线·本章首关(stageIndex==0)报一次曝光（dim=1）。
        if (stageIndex == 0 && IsFormalMainRun())
            WeChatSceneAnalyticsBridge.Report(WeChatSceneAnalyticsBridge.StageBranch(chapter),
                WeChatSceneAnalyticsBridge.EventExposure, 1);
    }

    public static void StageEnd(int chapter, int stageIndex, bool win, double durationSec)
    {
        Track("stage_end",
            ("chapter", chapter),
            ("stage_index", stageIndex),
            ("win", win),
            ("duration_sec", Math.Round(durationSec, 1)));
        // 场景分析漏斗终点：仅正式主线·打完本章大Boss(stageIdx0==9,BossKind==2)且胜利报点击（dim=1），与首关曝光成对。
        // ⚠ 用传入的 stageIndex 快照，不能现读 currentStageIndex——OnStageComplete 已把它重置为 0。
        if (win && ActProgressionDefs.BossKindOfStage(stageIndex) == 2 && IsFormalMainRun())
            WeChatSceneAnalyticsBridge.Report(WeChatSceneAnalyticsBridge.StageBranch(chapter),
                WeChatSceneAnalyticsBridge.EventClick, 1);
    }

    public static void ChapterReach(int chapter)
        => Track("chapter_reach", ("chapter", chapter));

    public static void DiamondChange(string direction, long amount, long balance)
        => Track("diamond_change",
            ("direction", direction),
            ("amount", amount),
            ("balance", balance));

    public static void AdSlotClick(string slot, bool success)
        => Track("ad_slot_click", ("slot", slot), ("success", success));

    // —— 广告漏斗四事件（2026-10-28 T5 补齐，placement 维度，供开广告后看渗透率/流失点）——
    /// <summary>广告请求已发出（可展示尝试，漏斗第 1 格）。真桥 RequestRewarded 时上报。</summary>
    public static void AdAvailable(string placement)
        => Track("ad_available", ("placement", placement));

    /// <summary>广告展示成功（漏斗第 2 格）。OnAdShow 时上报。</summary>
    public static void AdShow(string placement)
        => Track("ad_show", ("placement", placement));

    /// <summary>激励完成且核验通过（漏斗第 3 格 = 转化终点，之后才是发奖）。OnRewardVerify(IsVerified) 时上报。</summary>
    public static void AdReward(string placement)
        => Track("ad_reward", ("placement", placement));

    /// <summary>广告失败/未完成（含原因：SDK 错误码、核验未通过、未核验先关闭）。</summary>
    public static void AdFail(string placement, string reason)
        => Track("ad_fail", ("placement", placement), ("reason", reason));

    public static void TutorialStep(string stepName, int stepIndex)
    {
        Track("tutorial_step", ("step", stepName), ("index", stepIndex));
        // 场景分析漏斗：引导进入=曝光（dim 固定 1，与 TutorialDone 点击同维度，后台才能按同 dim 配对算整体通过率）。
        WeChatSceneAnalyticsBridge.Report(WeChatSceneAnalyticsBridge.BranchTutorial,
            WeChatSceneAnalyticsBridge.EventExposure, 1);
    }

    /// <summary>引导全部完成=漏斗终点（点击）。由 FirstRunGuide 通关落盘处调用一次。</summary>
    public static void TutorialDone()
        => WeChatSceneAnalyticsBridge.Report(WeChatSceneAnalyticsBridge.BranchTutorial,
            WeChatSceneAnalyticsBridge.EventClick, 1);   // dim=1 与 TutorialStep 曝光同维度，保证漏斗配对

    public static void QuestComplete(string questId)
        => Track("quest_complete", ("quest_id", questId));

    // ============================================================
    // 批处理 / 节流
    // ============================================================

    /// <summary>会话开始：初始化缓冲并记 session_start。在 Boot 生命周期入口调用一次。</summary>
    public static void Init()
    {
        if (!Enabled) return;
        try
        {
            if (_queue == null) _queue = new List<KeyValuePair<string, string>>(FlushBatchSize);
            _lastFlushTime = Time.unscaledTime;
            // 微信构建用官方 WX.ReportEvent 真正上报；其它平台桥内只走日志，开关保持 false 语义一致。
            WeChatAnalyticsBridge.UseWeChatReport = WeChatAnalyticsBridge.Available;
            SessionStart();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] Init 失败: {e.Message}");
        }
    }

    /// <summary>把缓冲里的事件真正发出去（走微信桥或 Debug.Log）。空缓冲直接返回。</summary>
    public static void Flush()
    {
        if (!Enabled) return;
        try
        {
            if (_queue == null || _queue.Count == 0) return;
            for (int i = 0; i < _queue.Count; i++)
            {
                var kv = _queue[i];
                WeChatAnalyticsBridge.ReportEvent(kv.Key, kv.Value);
            }
            _queue.Clear();
            _lastFlushTime = Time.unscaledTime;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] Flush 失败: {e.Message}");
        }
    }

    static void Enqueue(string eventName, string json)
    {
        if (_queue == null) _queue = new List<KeyValuePair<string, string>>(FlushBatchSize);
        _queue.Add(new KeyValuePair<string, string>(eventName, json));
        if (_queue.Count >= FlushBatchSize) Flush();
        else if (Time.unscaledTime - _lastFlushTime >= FlushIntervalSec) Flush();
    }

    // ============================================================
    // JSON 拼接（最小实现，不引第三方库）
    // ============================================================

    static string BuildPropsJson((string, object)[] props)
    {
        if (props == null || props.Length == 0) return "{}";
        var sb = new StringBuilder();
        sb.Append('{');
        for (int i = 0; i < props.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var p = props[i];
            sb.Append('"').Append(p.Item1).Append('"').Append(':');
            AppendJsonValue(sb, p.Item2);
        }
        sb.Append('}');
        return sb.ToString();
    }

    static void AppendJsonValue(StringBuilder sb, object v)
    {
        if (v == null) { sb.Append("null"); return; }
        if (v is string s)
        {
            sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")).Append('"');
            return;
        }
        if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
        sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
    }

    static double EpochSeconds()
    {
        // 运行时调用（非 .ctor），安全。用 DateTime 计算 Unix 时间戳。
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (DateTime.UtcNow - epoch).TotalSeconds;
    }
}
