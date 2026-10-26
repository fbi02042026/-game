using System.Collections.Generic;
using UnityEngine;

/// <summary>精英 / Boss 词缀（V6 新增的敌人机制，普通小怪不参与）。</summary>
public enum MonsterAffixId
{
    None = 0,
    Frenzy,   // 狂暴：攻速 +30%
    Bulwark,  // 铁壁：受到伤害 -25%
    Thorns,   // 荆棘：反弹近战伤害
    Swift,    // 迅捷：移速 +40%
    Leech,    // 吸血：造成伤害回血
    Split,    // 分裂：死亡时分裂出小怪
    Crit,     // 暴击：攻击概率暴击（2026-10-26 词缀怪新增）
}

/// <summary>
/// 词缀表：全部是<b>纯数值乘子 + 一个死亡事件</b>，不引入新 AI、不需要新美术。
/// 词缀名会挂在屏幕 Boss 血条上（BattleBossHpBar）作为后缀，如「森林守卫·狂暴」。
/// </summary>
public static class MonsterAffixDefs
{
    public struct Def
    {
        public MonsterAffixId Id;
        /// <summary>血条后缀（不要带「·」）</summary>
        public string Name;
        /// <summary>后缀颜色</summary>
        public Color Tint;

        public float AtkSpeedMul;
        public float MoveSpeedMul;
        /// <summary>受到伤害的乘子（&lt;1 = 减伤）</summary>
        public float DamageTakenMul;
        /// <summary>反弹给近战攻击者的比例（按实际造成的伤害）</summary>
        public float ThornsRatio;
        /// <summary>造成伤害时回血的比例</summary>
        public float LeechRatio;
        /// <summary>死亡时分裂出的小怪数量</summary>
        public int SplitCount;
        /// <summary>攻击暴击概率（0~1，2026-10-26 词缀怪「暴击」用）</summary>
        public float CritChance;
        /// <summary>暴击伤害倍率（暴击词缀生效时实际伤害 × 此值）；运行时组件 MonsterAffix 默认 1f，非 Crit 词缀不进入乘算分支，故此处不写初始化器以兼容 C# 9.0</summary>
        public float CritDamageMul;
    }

    public static readonly Def[] All =
    {
        new Def { Id = MonsterAffixId.Frenzy,  Name = "狂暴", Tint = new Color(1.00f, 0.42f, 0.32f),
                  AtkSpeedMul = 1.30f, MoveSpeedMul = 1f,   DamageTakenMul = 1f },
        new Def { Id = MonsterAffixId.Bulwark, Name = "铁壁", Tint = new Color(0.62f, 0.78f, 1.00f),
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 0.75f },
        new Def { Id = MonsterAffixId.Thorns,  Name = "荆棘", Tint = new Color(0.55f, 0.95f, 0.55f),
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f, ThornsRatio = 0.15f },
        new Def { Id = MonsterAffixId.Swift,   Name = "迅捷", Tint = new Color(0.98f, 0.85f, 0.40f),
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1.40f, DamageTakenMul = 1f },
        new Def { Id = MonsterAffixId.Leech,   Name = "吸血", Tint = new Color(0.85f, 0.45f, 0.90f),
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f, LeechRatio = 0.12f },
        new Def { Id = MonsterAffixId.Split,   Name = "分裂", Tint = new Color(0.75f, 0.75f, 0.80f),
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f, SplitCount = 2 },
        // 2026-10-26 词缀怪「暴击」：20% 概率 ×1.6，玩家侧看得见暴击飘字
        new Def { Id = MonsterAffixId.Crit,    Name = "暴击", Tint = new Color(1.00f, 0.55f, 0.10f),
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f,
                  CritChance = 0.20f,  CritDamageMul = 1.6f },
    };

    static readonly Dictionary<MonsterAffixId, Def> _map = BuildMap();

    static Dictionary<MonsterAffixId, Def> BuildMap()
    {
        var d = new Dictionary<MonsterAffixId, Def>();
        for (int i = 0; i < All.Length; i++) d[All[i].Id] = All[i];
        return d;
    }

    public static Def Get(MonsterAffixId id)
    {
        return _map.TryGetValue(id, out var v) ? v : default;
    }

    public static string Name(MonsterAffixId id)
    {
        return _map.TryGetValue(id, out var v) ? v.Name : "";
    }

    /// <summary>
    /// 解析「| 分隔的词缀枚举名」为列表（monster_stats.affix 列 / WaveData 偏好共用此口径）。
    /// 空串 / null / 全部解析失败 → 返回空列表（调用方按「无词缀」处理，不兜底造词缀）。
    /// </summary>
    public static List<MonsterAffixId> ParseAffixList(string raw)
    {
        var list = new List<MonsterAffixId>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        var parts = raw.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (string.IsNullOrEmpty(p)) continue;
            if (System.Enum.TryParse(p, true, out MonsterAffixId id) && id != MonsterAffixId.None)
                list.Add(id);
        }
        return list;
    }

    /// <summary>本次要出的词缀数量：第 4 章起有概率出 2 个。</summary>
    public static int RollCount(int chapter)
    {
        int baseCount = 1;
        if (chapter >= GameConfig.EnemyTuning.AffixSecondFromChapter
            && Random.value < GameConfig.EnemyTuning.AffixSecondChance)
            baseCount = 2;
        return baseCount;
    }

    /// <summary>
    /// 下一次 <see cref="Roll"/> 使用的词缀偏好（| 分隔的枚举名，如 "Split"、"Bulwark|Thorns"）。
    /// 由 WavePlanner 在实例化精英前挂上、Roll 消费后立即清空 —— 不给别的怪漏味。
    /// </summary>
    static string _pendingBias = "";

    public static void SetPendingBias(string bias) { _pendingBias = bias ?? ""; }
    public static void ClearPendingBias() { _pendingBias = ""; }

    /// <summary>随机抽 n 个不重复的词缀。</summary>
    public static List<MonsterAffixId> Roll(int chapter)
    {
        int n = RollCount(chapter);
        var pool = new List<MonsterAffixId>();

        // 有偏好就只在偏好里抽（解析失败则退回全池，绝不因为配错表而崩）
        string bias = _pendingBias;
        _pendingBias = "";
        if (!string.IsNullOrEmpty(bias))
        {
            var parts = bias.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (string.IsNullOrEmpty(p)) continue;
                if (System.Enum.TryParse(p, true, out MonsterAffixId bid) && bid != MonsterAffixId.None)
                    pool.Add(bid);
            }
        }
        if (pool.Count == 0)
        {
            for (int i = 0; i < All.Length; i++) pool.Add(All[i].Id);
        }

        var picked = new List<MonsterAffixId>(n);
        for (int k = 0; k < n && pool.Count > 0; k++)
        {
            int idx = Random.Range(0, pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }
}
