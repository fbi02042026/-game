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
///    <see cref="WeChatAnalyticsBridge"/>（占位 + TODO，缺 .jslib / WX SDK 封装）。
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

    public static void StageStart(int chapter, int stageIndex)
        => Track("stage_start", ("chapter", chapter), ("stage_index", stageIndex));

    public static void StageEnd(int chapter, int stageIndex, bool win, double durationSec)
        => Track("stage_end",
            ("chapter", chapter),
            ("stage_index", stageIndex),
            ("win", win),
            ("duration_sec", Math.Round(durationSec, 1)));

    public static void ChapterReach(int chapter)
        => Track("chapter_reach", ("chapter", chapter));

    public static void DiamondChange(string direction, long amount, long balance)
        => Track("diamond_change",
            ("direction", direction),
            ("amount", amount),
            ("balance", balance));

    public static void AdSlotClick(string slot, bool success)
        => Track("ad_slot_click", ("slot", slot), ("success", success));

    public static void TutorialStep(string stepName, int stepIndex)
        => Track("tutorial_step", ("step", stepName), ("index", stepIndex));

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
