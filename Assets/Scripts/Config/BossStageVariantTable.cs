using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 关变体 V1.0：进 Boss 波之前抽 1 个变体，只定义「首领亲卫 / 软性限时涌怪」。
///
/// 为什么要有这张表：小怪波已经有 stage_mode + wave_archetype 逐波现摇，
/// 但 Boss 关的最后一波永远是「孤零零一个 Boss」——骨架恒定是重复疲劳的主因之一。
/// 变体只加「随从」与「限时压力」两类外挂参数，不碰 Boss 本体数值、不改战斗公式。
///
/// 范式照抄 StageEventTable：缺表 / 关闭 / 无候选 时 <see cref="Draw"/> 返回 null，
/// 调用方回退「只有 Boss 本体」，行为与接入前完全一致（不让关卡开不起来）。
/// </summary>
public static class BossStageVariantTable
{
    public enum EffectType { None, GUARD, TIMED }

    public class VariantDef
    {
        public string id = "";
        public string name = "";
        public int weight = 1;
        public int startChapter = 1;
        public string telegraph = "";
        public EffectType effectType = EffectType.None;
        public int guardCount = 0;      // GUARD：Boss 波额外带的精英亲卫只数
        public float delaySec = 0f;     // TIMED：开打到第一次涌怪的秒数
        public float intervalSec = 0f;  // TIMED：两次涌怪的间隔秒数
        public int cap = 0;             // TIMED：涌怪总数上限
    }

    /// <summary>总开关；关掉后所有 Boss 关行为与改动前完全一致。</summary>
    public static bool VariantsEnabled = true;

    static readonly List<VariantDef> _rows = new List<VariantDef>();
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

        string raw = GameTableStore.LoadText(ContentPaths.Data.BossStageVariant);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 5) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id) || id.StartsWith("#")) continue;

            var v = new VariantDef
            {
                id = id,
                name = c[1].Trim(),
                weight = GameTableCsv.TryInt(c, 2, 1),
                startChapter = GameTableCsv.TryInt(c, 3, 1),
                telegraph = c.Length > 4 ? c[4].Trim() : ""
            };
            if (v.weight <= 0) v.weight = 1;
            if (v.startChapter <= 0) v.startChapter = 1;

            ParseEffect(c.Length > 5 ? c[5].Trim() : "", v);
            _rows.Add(v);
        }
        Debug.Log($"[BossVariant] 已加载 {_rows.Count} 条变体");
    }

    /// <summary>解析 effect 字段：TYPE|key=val;key=val。</summary>
    static void ParseEffect(string raw, VariantDef v)
    {
        if (string.IsNullOrEmpty(raw)) return;
        var parts = raw.Split('|');
        if (parts.Length == 0) return;
        switch (parts[0].Trim().ToUpperInvariant())
        {
            case "GUARD": v.effectType = EffectType.GUARD; break;
            case "TIMED": v.effectType = EffectType.TIMED; break;
            default: v.effectType = EffectType.None; return;
        }
        for (int i = 1; i < parts.Length; i++)
        {
            var kv = parts[i].Split('=');
            if (kv.Length != 2) continue;
            string key = kv[0].Trim().ToLowerInvariant();
            string val = kv[1].Trim();
            switch (key)
            {
                case "count": int.TryParse(val, out v.guardCount); break;
                case "delay": float.TryParse(val, out v.delaySec); break;
                case "interval": float.TryParse(val, out v.intervalSec); break;
                case "cap": int.TryParse(val, out v.cap); break;
            }
        }
        if (v.guardCount < 0) v.guardCount = 0;
        if (v.delaySec < 0f) v.delaySec = 0f;
        if (v.intervalSec <= 0f) v.intervalSec = 0f;
        if (v.cap < 0) v.cap = 0;
    }

    /// <summary>
    /// 抽 1 个本章可用的 Boss 关变体。无候选（章节未解锁 / 关闭 / 缺表）返回 null。
    /// </summary>
    public static VariantDef Draw(int chapter)
    {
        if (!VariantsEnabled) return null;
        EnsureLoaded();
        if (_rows.Count == 0) return null;

        var cand = new List<VariantDef>();
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
}
