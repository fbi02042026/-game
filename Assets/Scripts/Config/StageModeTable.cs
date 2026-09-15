using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡模式表 V3.0：每关进战前抽 1 个模式，模式只定义「原型池 / 波数范围 / 人数倍率」，
/// 具体的每一波在池里按权重<b>现抽</b> —— 关卡可以重复出现，但里面的波次组成与波次种类永远不一样。
///
/// 抽签规则（见 stage_mode.csv 表头）：
///   命中 chapter（* 通配 / 4+ 表示第 4 章起）+ stageType + 关卡索引 ≥ minStage 的候选 →
///   等权重抽 1 个，<b>允许与上一关重复</b>（想提高某模式出现率，在表里多写一行即可）。
///
/// 波次生成（由 <see cref="WavePlanner"/> 调用）：
///   <see cref="RollWaveCount"/> 摇波数，<see cref="RollArchetype"/> 逐波摇原型（受 maxRun 软约束）。
///
/// 容错：缺表 / 无候选行时 <see cref="Draw"/> 返回 null，
/// <see cref="WavePlanner"/> 回退到旧的「均分波次」逻辑 —— 缺表不会让关卡开不起来。
/// </summary>
public static class StageModeTable
{
    /// <summary>原型池里的一条：原型 id + 抽中权重。</summary>
    public class PoolEntry
    {
        public string id = "";
        public int weight = 1;
    }

    public class Mode
    {
        /// <summary>起始章节（1 基）。</summary>
        public int chapterMin = 1;
        /// <summary>true = 该章节及之后全部适用（* 通配 或 4+ 语法）。</summary>
        public bool laterChapters = true;
        public StageType stageType = StageType.Normal;
        /// <summary>模式编号，如 M01。</summary>
        public string id = "";
        public string name = "";
        /// <summary>原型池：每次进关从这个池里按权重抽每一波。</summary>
        public List<PoolEntry> pool = new List<PoolEntry>();
        /// <summary>本关波数范围（含两端）。</summary>
        public int waveMin = 3;
        public int waveMax = 5;
        /// <summary>每波人数倍率，叠在章节系数与关卡进度系数上。</summary>
        public float countMul = 1f;
        /// <summary>该模式最早的关卡索引（0 基）：教学期不上强度。</summary>
        public int minStage = 0;
        /// <summary>播报文案：给玩家 3 秒读条时间，别让压力从屏幕外砸下来。</summary>
        public string telegraph = "";
        public string note = "";

        public bool HasPool => pool != null && pool.Count > 0;
    }

    static readonly List<Mode> _rows = new List<Mode>();
    static bool _loaded;

    public static bool HasData => _loaded && _rows.Count > 0;

    /// <summary>开新一局时调用（保留接口，V3.0 模式允许重复，不再维护去重池）。</summary>
    public static void ResetRun() { }

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

        string raw = GameTableStore.LoadText(ContentPaths.Data.StageMode);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 8) continue;
            string id = c[2].Trim();
            if (string.IsNullOrEmpty(id) || id.StartsWith("#")) continue;

            var m = new Mode
            {
                stageType = ParseStageType(c[1]),
                id = id,
                name = c[3].Trim(),
                pool = ParsePool(c[4]),
                waveMin = GameTableCsv.TryInt(c, 5, 3),
                waveMax = GameTableCsv.TryInt(c, 6, 5),
                countMul = GameTableCsv.TryFloat(c, 7, 1f),
                minStage = GameTableCsv.TryInt(c, 8, 0),
                telegraph = c.Length > 9 ? c[9].Trim() : "",
                note = c.Length > 10 ? c[10].Trim() : ""
            };
            if (m.countMul <= 0f) m.countMul = 1f;
            if (m.waveMin <= 0) m.waveMin = 3;
            if (m.waveMax < m.waveMin) m.waveMax = m.waveMin;

            // chapter 列：* = 全章；4+ = 第 4 章起；4 = 仅第 4 章
            string rawChapter = c[0].Trim();
            if (string.IsNullOrEmpty(rawChapter) || rawChapter == "*")
            {
                m.chapterMin = 1;
                m.laterChapters = true;
            }
            else if (rawChapter.EndsWith("+"))
            {
                int v;
                m.chapterMin = int.TryParse(rawChapter.Substring(0, rawChapter.Length - 1), out v) ? v : 1;
                m.laterChapters = true;
            }
            else
            {
                int v;
                m.chapterMin = int.TryParse(rawChapter, out v) ? v : 1;
                m.laterChapters = false;
            }
            _rows.Add(m);
        }
        Debug.Log($"[StageMode] 已加载 {_rows.Count} 条（V3.0 池化，模式允许重复）");
    }

    static StageType ParseStageType(string raw)
    {
        string s = string.IsNullOrEmpty(raw) ? "" : raw.Trim();
        if (Enum.TryParse(s, true, out StageType t)) return t;
        return StageType.Normal;
    }

    /// <summary>解析原型池：A02:6|A01:3|A06:1 → 三条带权重的条目。</summary>
    static List<PoolEntry> ParsePool(string raw)
    {
        var list = new List<PoolEntry>();
        if (string.IsNullOrEmpty(raw)) return list;
        var parts = raw.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (string.IsNullOrEmpty(p)) continue;
            int w = 1;
            int colon = p.IndexOf(':');
            if (colon >= 0)
            {
                int.TryParse(p.Substring(colon + 1).Trim(), out w);
                p = p.Substring(0, colon).Trim();
            }
            if (string.IsNullOrEmpty(p)) continue;
            list.Add(new PoolEntry { id = p, weight = Mathf.Max(0, w) });
        }
        return list;
    }

    /// <summary>
    /// 抽本关的模式。无候选返回 null（调用方回退旧逻辑）。V3.0 起允许与上一关重复。
    /// </summary>
    /// <param name="chapter">章节（1 基）</param>
    /// <param name="stageIdx">关卡索引（0 基）</param>
    public static Mode Draw(int chapter, int stageIdx, StageType type)
    {
        EnsureLoaded();
        if (_rows.Count == 0) return null;

        var cand = new List<Mode>();
        for (int i = 0; i < _rows.Count; i++)
        {
            var m = _rows[i];
            if (m.chapterMin > chapter) continue;
            if (!m.laterChapters && m.chapterMin != chapter) continue;
            if (m.stageType != type) continue;
            if (stageIdx < m.minStage) continue;
            cand.Add(m);
        }
        if (cand.Count == 0) return null;

        // 模式之间等权重（表里多写一行 = 提高该模式出现率）
        return cand[UnityEngine.Random.Range(0, cand.Count)];
    }

    /// <summary>本关波数：模式范围内随机，再叠加压力阀 / 职业补偿的修正，最后夹到 [1, hardMax]。</summary>
    public static int RollWaveCount(Mode mode, int waveBonus, int hardMax)
    {
        if (mode == null) return 0;
        int count = UnityEngine.Random.Range(mode.waveMin, mode.waveMax + 1) + waveBonus;
        // 波数不得超过刷怪点数量：否则多出来的波会共用一个 anchor，触发 X 撞车
        if (hardMax > 0) count = Mathf.Min(count, hardMax);
        return Mathf.Max(1, count);
    }

    /// <summary>
    /// 逐波摇原型：按池权重抽，并遵守「同一原型连续出现 ≤ maxRun」的软约束。
    /// </summary>
    /// <param name="mode">当前模式</param>
    /// <param name="recentIds">已经摇出的原型序列（用于判定连续次数）</param>
    public static string RollArchetype(Mode mode, List<string> recentIds)
    {
        if (mode == null || !mode.HasPool) return "";

        string last = (recentIds != null && recentIds.Count > 0) ? recentIds[recentIds.Count - 1] : "";
        int runLen = 0;
        if (!string.IsNullOrEmpty(last))
        {
            for (int i = (recentIds != null ? recentIds.Count - 1 : -1); i >= 0; i--)
            {
                if (recentIds[i] == last) runLen++;
                else break;
            }
        }

        var arch = WaveArchetypeTable.Get(last);
        int maxRun = arch != null ? arch.maxRun : 2;
        bool blockLast = runLen >= maxRun;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            string pick = WeightedPick(mode.pool, blockLast ? last : null);
            if (!string.IsNullOrEmpty(pick)) return pick;
            // 被 ban 后没得选（池里只有这一个）→ 放宽约束再抽
            blockLast = false;
        }
        return "";
    }

    static string WeightedPick(List<PoolEntry> pool, string banId)
    {
        int total = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (!string.IsNullOrEmpty(banId) && pool[i].id == banId) continue;
            total += pool[i].weight;
        }
        if (total <= 0) return "";

        int roll = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < pool.Count; i++)
        {
            if (!string.IsNullOrEmpty(banId) && pool[i].id == banId) continue;
            roll -= pool[i].weight;
            if (roll < 0) return pool[i].id;
        }
        return "";
    }

    /// <summary>
    /// 职业 × 模式难度矩阵（V3.0 §4.3）里标记为「高」的组合 → 该关波数 −1。
    /// 只给缓冲，不给保送。
    /// </summary>
    public static bool IsHardForJob(string modeId, PlayerJobId job)
    {
        if (string.IsNullOrEmpty(modeId)) return false;
        switch (job)
        {
            case PlayerJobId.Berserker: // 射程 1.00：够不着，最怕远程压制
                return modeId == "M02";
            case PlayerJobId.Mage:      // 血最薄：最怕同帧合围
                return modeId == "M04" || modeId == "M10";
            case PlayerJobId.Priest:    // 输出最低：最怕厚皮与精英连发
                return modeId == "M04" || modeId == "M05" || modeId == "M08" || modeId == "M09";
            default:
                return false;
        }
    }

    /// <summary>调试用：列出某章某类型可用的模式。</summary>
    public static List<Mode> GetCandidates(int chapter, StageType type)
    {
        EnsureLoaded();
        var list = new List<Mode>();
        for (int i = 0; i < _rows.Count; i++)
        {
            var m = _rows[i];
            if (m.chapterMin > chapter) continue;
            if (!m.laterChapters && m.chapterMin != chapter) continue;
            if (m.stageType != type) continue;
            list.Add(m);
        }
        return list;
    }
}
