using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡事件层 V1.0：波与波之间随机插入 1~2 个「有取舍感、有预告」的事件。
/// 表驱动，照抄 StageModeTable 的范式（缺表 / 关闭时返回 null，调用方回退到无事件逻辑）。
///
/// 硬约束（由 WavePlanner 落实，表只提供候选）：
///   · 第 1~2 章恒不触发（候选起始章节都 ≥ 3，且 PlanStageEvents 直接拦截）。
///   · 事件是「额外的」：<see cref="EventsEnabled"/>=false 时本关与改动前完全一致。
///
/// 容错：缺表 / 无候选 / 关闭 → <see cref="Draw"/> 返回 null，不抛异常、不让关卡开不起来。
/// </summary>
public static class StageEventTable
{
    public enum EffectType { None, REINFORCE, ELITE, SUPPLY, HAZARD }

    public class EventDef
    {
        public string id = "";
        public string name = "";
        public int weight = 1;
        public int startChapter = 3;
        public string telegraph = "";
        public EffectType effectType = EffectType.None;
        public float countMul = 1f;      // REINFORCE：下一波人数倍率
        public float goldMul = 1f;       // REINFORCE / HAZARD：本关金币倍率
        public int eliteAdd = 0;         // ELITE：额外精英数
        public int stone = 0;            // ELITE：通关发放强化石数
        public float healPct = 0f;       // SUPPLY：全队回血比例
        public float movePenalty = 0f;   // HAZARD：英雄移速惩罚(0~1)
    }

    /// <summary>总开关；关掉后所有关卡行为与改动前完全一致。</summary>
    public static bool EventsEnabled = true;

    static readonly List<EventDef> _rows = new List<EventDef>();
    static bool _loaded;

    public static bool HasData => _loaded && _rows.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.StageEvent);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 5) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id) || id.StartsWith("#")) continue;

            var e = new EventDef
            {
                id = id,
                name = c[1].Trim(),
                weight = GameTableCsv.TryInt(c, 2, 1),
                startChapter = GameTableCsv.TryInt(c, 3, 3),
                telegraph = c.Length > 4 ? c[4].Trim() : "",
                effectType = EffectType.None,
                countMul = 1f, goldMul = 1f, eliteAdd = 0, stone = 0, healPct = 0f, movePenalty = 0f
            };
            if (e.weight <= 0) e.weight = 1;
            if (e.startChapter <= 0) e.startChapter = 3;

            ParseEffect(c.Length > 5 ? c[5].Trim() : "", e);
            _rows.Add(e);
        }
        Debug.Log($"[StageEvent] 已加载 {_rows.Count} 条事件");
    }

    /// <summary>解析 effect 字段：TYPE|key=val;key=val。</summary>
    static void ParseEffect(string raw, EventDef e)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var parts = raw.Split('|');
        if (parts.Length == 0) return;
        switch (parts[0].Trim().ToUpperInvariant())
        {
            case "REINFORCE": e.effectType = EffectType.REINFORCE; break;
            case "ELITE":     e.effectType = EffectType.ELITE; break;
            case "SUPPLY":    e.effectType = EffectType.SUPPLY; break;
            case "HAZARD":    e.effectType = EffectType.HAZARD; break;
            default:          e.effectType = EffectType.None; return;
        }
        for (int i = 1; i < parts.Length; i++)
        {
            var kv = parts[i].Split('=');
            if (kv.Length != 2) continue;
            string k = kv[0].Trim().ToLowerInvariant();
            string v = kv[1].Trim();
            switch (k)
            {
                case "countmul":      float.TryParse(v, out e.countMul); break;
                case "goldmul":       float.TryParse(v, out e.goldMul); break;
                case "eliteadd":      int.TryParse(v, out e.eliteAdd); break;
                case "stone":         int.TryParse(v, out e.stone); break;
                case "healpct":       float.TryParse(v, out e.healPct); break;
                case "movepenalty":   float.TryParse(v, out e.movePenalty); break;
            }
        }
        if (e.countMul <= 0f) e.countMul = 1f;
        if (e.goldMul <= 0f) e.goldMul = 1f;
    }

    /// <summary>
    /// 抽 1 个本关可触发的事件。无候选（章节未解锁 / 关闭 / 缺表）返回 null。
    /// 权重抽：命中「起始章节 ≤ chapter」的所有候选等概率分布（按 weight 加权）。
    /// </summary>
    public static EventDef Draw(int chapter, int stageIdx)
    {
        if (!EventsEnabled) return null;
        EnsureLoaded();
        if (_rows.Count == 0) return null;

        var cand = new List<EventDef>();
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r.startChapter > chapter) continue;
            cand.Add(r);
        }
        if (cand.Count == 0) return null;

        int total = 0;
        for (int i = 0; i < cand.Count; i++) total += Mathf.Max(1, cand[i].weight);
        int roll = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < cand.Count; i++)
        {
            roll -= Mathf.Max(1, cand[i].weight);
            if (roll < 0) return cand[i];
        }
        return cand[cand.Count - 1];
    }

    /// <summary>调试用：列出某章可用事件 id。</summary>
    public static List<string> GetCandidateIds(int chapter)
    {
        EnsureLoaded();
        var list = new List<string>();
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i].startChapter <= chapter) list.Add(_rows[i].id);
        return list;
    }
}
