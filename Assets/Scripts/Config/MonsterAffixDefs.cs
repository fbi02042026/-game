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

    /// <summary>本次要出的词缀数量：第 4 章起有概率出 2 个。</summary>
    public static int RollCount(int chapter)
    {
        int baseCount = 1;
        if (chapter >= GameConfig.EnemyTuning.AffixSecondFromChapter
            && Random.value < GameConfig.EnemyTuning.AffixSecondChance)
            baseCount = 2;
        return baseCount;
    }

    /// <summary>随机抽 n 个不重复的词缀。</summary>
    public static List<MonsterAffixId> Roll(int chapter)
    {
        int n = RollCount(chapter);
        var pool = new List<MonsterAffixId>();
        for (int i = 0; i < All.Length; i++) pool.Add(All[i].Id);

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
