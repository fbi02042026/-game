using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 无限裂隙奖励分配表（endless_reward）。2026-11-14 主人拍板「直接配个表，以后直接改表就行」——
/// 把裂隙三档奖励（金币倍率 / 复刷倍率 / 宝箱金币 / 天赋石 / 徽记轮换 / 钻石 / 招募卷）全部收编进表。
/// <para>源表 <c>Assets/Data/Source/Tables/endless_reward.csv</c>，Cook 后走
/// <see cref="ContentPaths.Data.EndlessReward"/>；列语义见源表头注释。</para>
/// <para>回退口径（照 CombatTuningTable 先例）：缺表 / 缺行 / 构造期读表被拒时，
/// 回退 <see cref="FallbackRow"/> 公式行（= 当前拍板硬编码值），运行时行为与改表前完全一致，可随时回退。</para>
/// <para>表尾外推：层数超出表内最大行时，按表尾同档位行外推——
/// 徽记按轮换顺序继续循环、档位升到传奇封顶、钻石每 BOSS 档 +5、其余字段取表尾同档位行。</para>
/// </summary>
public static class EndlessRewardTable
{
    /// <summary>单档奖励行。所有数值都可直接从表读。</summary>
    public class Row
    {
        public int stage;
        /// <summary>首通金币倍率（× 30+2×层+0.25×层²；0 = 不发）</summary>
        public float firstGoldMul = 1f;
        /// <summary>复刷金币倍率（× 同一公式；0 = 不发）</summary>
        public float repeatMul = 0.3f;
        /// <summary>宝箱金币：-1 = 走公式 6+2×层；0 = 不发；&gt;0 = 每层固定发这个数</summary>
        public int chestGold = -1;
        /// <summary>首通天赋石倍率（× 2+层/10；0 = 不发）</summary>
        public float talentMul = 1f;
        public string badgeJob = "";
        public string badgeTier = "";
        public int badgeCount;
        public int diamond;
        /// <summary>每 10 层固定发的普通招募卷数量</summary>
        public int scroll;
        /// <summary>【2026-11-15】50 层起每 10 层额外加发的高级（传奇）招募卷数量</summary>
        public int scrollAdvanced;
        public bool HasBadge => !string.IsNullOrEmpty(badgeJob) && !string.IsNullOrEmpty(badgeTier) && badgeCount > 0;
    }

    /// <summary>徽记职业轮换顺序（2026-11-14 主人拍板「剑盾 弓手 法师 牧师 重武 狂战」；「弓手」= 系统职业 key「游侠」）。</summary>
    public static readonly string[] BadgeRotation = { "剑盾", "游侠", "法师", "牧师", "重武", "狂战" };

    /// <summary>BOSS 档钻石的每档增量（表尾外推用）。</summary>
    public const int DiamondStepPerTier = 5;

    static readonly Dictionary<int, Row> _byStage = new Dictionary<int, Row>();
    static bool _loaded;
    static Row _fallbackNormal, _fallbackElite, _fallbackBoss;

    public static void Reload()
    {
        _loaded = false;
        _byStage.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw;
        try
        {
            // 大坑（照 CombatTuningTable）：构造期 / 反序列化期调 Resources.Load 会抛
            // UnityException: Load is not allowed。宁可回退公式行也不能抛。
            raw = GameTableStore.LoadText(ContentPaths.Data.EndlessReward);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EndlessRewardTable] 表加载被拒（极可能在构造/反序列化期调用）: "
                + ContentPaths.Data.EndlessReward + " -> " + e.Message + "（回退公式行）");
            return;
        }

        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[EndlessRewardTable] 表加载失败或缺失: Resources/" + ContentPaths.Data.EndlessReward
                + "（空或缺失），回退公式行。");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 0; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c == null || c.Length < 1) continue;
            string first = c[0].Trim();
            if (string.IsNullOrEmpty(first)) continue;
            if (first.StartsWith("#") || first.StartsWith("//")) continue;   // 注释行
            if (!GameTableCsv.TryFloat(first, out float stageVal)) continue; // 表头行（stage 非数字）
            var row = ParseRow(c);
            if (row != null) _byStage[row.stage] = row;
        }

        if (_byStage.Count <= 0)
            Debug.LogWarning("[EndlessRewardTable] 表解析 0 条: Resources/" + ContentPaths.Data.EndlessReward
                + "，回退公式行。");
        else
            Debug.Log($"[EndlessRewardTable] 已加载 {_byStage.Count} 行");
    }

    static Row ParseRow(string[] c)
    {
        // 列顺序见 endless_reward.csv 表头：
        // stage,firstGoldMul,repeatMul,chestGold,talentMul,badgeJob,badgeTier,badgeCount,diamond,scroll,scrollAdvanced
        if (!GameTableCsv.TryFloat(c[0], out float st)) return null;
        var r = new Row { stage = Mathf.RoundToInt(st) };
        if (c.Length > 1) r.firstGoldMul = GameTableCsv.TryFloat(c[1], out float v1) ? v1 : 1f;
        if (c.Length > 2) r.repeatMul = GameTableCsv.TryFloat(c[2], out float v2) ? v2 : 0.3f;
        if (c.Length > 3) r.chestGold = GameTableCsv.TryFloat(c[3], out float v3) ? Mathf.RoundToInt(v3) : -1;
        if (c.Length > 4) r.talentMul = GameTableCsv.TryFloat(c[4], out float v4) ? v4 : 1f;
        if (c.Length > 5) r.badgeJob = c[5].Trim();
        if (c.Length > 6) r.badgeTier = c[6].Trim();
        if (c.Length > 7) r.badgeCount = GameTableCsv.TryFloat(c[7], out float v5) ? Mathf.RoundToInt(v5) : 0;
        if (c.Length > 8) r.diamond = GameTableCsv.TryFloat(c[8], out float v6) ? Mathf.RoundToInt(v6) : 0;
        if (c.Length > 9) r.scroll = GameTableCsv.TryFloat(c[9], out float v7) ? Mathf.RoundToInt(v7) : 0;
        if (c.Length > 10) r.scrollAdvanced = GameTableCsv.TryFloat(c[10], out float v8) ? Mathf.RoundToInt(v8) : 0;
        if (r.stage < 0) return null;
        return r;
    }

    /// <summary>
    /// 取某层的奖励行。命中规则：5 倍数层取精确行；其余层取 stage=0 默认行；
    /// 表里没有的深层按表尾同档位行外推；整表缺失回退公式行（= 当前拍板硬编码）。
    /// 恒返回非 null，内部 try/catch 保证不抛。
    /// </summary>
    public static Row RowOf(int riftStage)
    {
        try
        {
            EnsureLoaded();
            int s = Mathf.Max(1, riftStage);
            Row row = null;
            if (_byStage.Count > 0)
            {
                int key = s % 5 == 0 ? s : 0;
                if (_byStage.TryGetValue(key, out row)) return row;
                row = null;
            }
            return row ?? ExtrapolateOrFallback(s);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EndlessRewardTable] 读取层 " + riftStage + " 异常，回退公式行: " + e.Message);
            return FallbackRow(riftStage);
        }
    }

    /// <summary>表内没这一层：层号 ≤ 表尾 → 公式行（表只可能缺中间行，罕见）；超过表尾 → 外推。</summary>
    static Row ExtrapolateOrFallback(int s)
    {
        if (_byStage.Count <= 0) return FallbackRow(s);
        int maxStage = 0;
        foreach (var k in _byStage.Keys)
            if (k > maxStage) maxStage = k;
        if (maxStage <= 0 || s <= maxStage) return FallbackRow(s);

        // —— 表尾外推（150 层后的兜底；想改深层数值直接在表里加行，别依赖这里）——
        int tierDiff = (s - maxStage) / 5;                       // 差几个 5 层档
        if (s % 10 == 0)
        {
            // BOSS 档外推：取表尾 BOSS 行，钻石每档 +5，其余照抄
            Row tail = TailRow(r => r.stage % 10 == 0 && r.stage > 0);
            var r = CloneRow(tail ?? FallbackRow(s), s);
            r.diamond = (tail != null ? tail.diamond : 5 + maxStage / 10 * 5) + (tierDiff / 2) * DiamondStepPerTier;
            r.scroll = tail != null ? tail.scroll : 2;                     // 每 10 层固定 2 个普通卷
            r.scrollAdvanced = tail != null ? tail.scrollAdvanced : 1;     // 50 层起表尾档恒 1 个高级卷
            return r;
        }
        // 精英档外推：取表尾精英行，徽记按轮换表续推、档位升到传奇封顶，其余照抄
        Row tailE = TailRow(r => r.HasBadge);
        var er = CloneRow(tailE ?? FallbackRow(s), s);
        int tailIdx = System.Array.IndexOf(BadgeRotation, er.badgeJob);
        if (tailIdx < 0) tailIdx = 0;
        er.badgeJob = BadgeRotation[(tailIdx + tierDiff) % BadgeRotation.Length];
        int tailRound = TierRound(er.badgeTier);
        er.badgeTier = TierName(Mathf.Min(2, tailRound + tierDiff / 6));   // 每轮 6 个精英层，升到传奇封顶
        return er;
    }

    static Row TailRow(System.Func<Row, bool> pred)
    {
        Row best = null;
        foreach (var kv in _byStage)
            if (pred(kv.Value) && (best == null || kv.Key > best.stage))
                best = kv.Value;
        return best;
    }

    static Row CloneRow(Row src, int stage)
    {
        if (src == null) return FallbackRow(stage);
        return new Row
        {
            stage = stage,
            firstGoldMul = src.firstGoldMul,
            repeatMul = src.repeatMul,
            chestGold = src.chestGold,
            talentMul = src.talentMul,
            badgeJob = src.badgeJob,
            badgeTier = src.badgeTier,
            badgeCount = src.badgeCount,
            diamond = src.diamond,
            scroll = src.scroll,
            scrollAdvanced = src.scrollAdvanced,
        };
    }

    static int TierRound(string tier) => tier == "传奇" ? 2 : tier == "稀有" ? 1 : 0;
    static string TierName(int round) => round <= 0 ? "普通" : round == 1 ? "稀有" : "传奇";

    /// <summary>
    /// 公式行（缺表 / 非表内层的回退口径）＝ 2026-11-14 拍板的硬编码值：
    /// 普通（金币 ×1 / 复刷 30% / 宝箱公式 / 天赋石 ×0，2026-11-15 口径「其他层都是金币」）、
    /// 精英（金币 ×2 / 徽记轮换 ×5 / 天赋石 0）、
    /// BOSS（金币 0 / 天赋石 ×3 / 钻石 5+档×5 / 普通卷 2 / 50 层起高级卷 1 / 宝箱 0）。
    /// </summary>
    public static Row FallbackRow(int riftStage)
    {
        int s = Mathf.Max(1, riftStage);
        if (GameConfig.IsEndlessBossStage(s))
        {
            return new Row
            {
                stage = s,
                firstGoldMul = 0f,
                repeatMul = 0f,
                chestGold = 0,
                talentMul = 3f,
                badgeJob = "",
                badgeTier = "",
                badgeCount = 0,
                diamond = 5 + s / 10 * DiamondStepPerTier,
                scroll = 2,
                scrollAdvanced = s >= 50 ? 1 : 0,
            };
        }
        if (GameConfig.IsEndlessEliteStage(s))
        {
            int k = (s - 5) / 10 + 1;                            // 第几个精英层（1 基）
            return new Row
            {
                stage = s,
                firstGoldMul = 2f,
                repeatMul = 0.3f,
                chestGold = -1,
                talentMul = 0f,
                badgeJob = BadgeRotation[Mathf.Abs(k - 1) % BadgeRotation.Length],
                badgeTier = TierName(Mathf.Max(0, (k - 1) / 6)),
                badgeCount = 5,
                diamond = 0,
                scroll = 0,
            };
        }
        return new Row { stage = 0, talentMul = 0f };            // 普通层默认：只发金币，天赋石只归 10 层档
    }
}
