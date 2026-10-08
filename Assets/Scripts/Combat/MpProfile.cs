using UnityEngine;

/// <summary>
/// 蓝条（MP）定位：按「技能本身的类型分类」判定，<b>不按单位</b>。
/// 加技能只要填对它自己的分类，蓝条归属自动成立，不会出现「角色有蓝但技能不耗蓝」的孤儿配置。
/// </summary>
public enum MpArchetype
{
    /// <summary>无蓝：物攻 / 防御 / 被动 —— 纯 CD 制，不参与 MP。</summary>
    None,

    /// <summary>治疗型：merc_skills 类型分类 = 恢复。</summary>
    Heal,

    /// <summary>法术型：merc_skills 类型分类 = 法术。</summary>
    Magic,

    /// <summary>玩家侧（牧师 / 法师职业）：4 个技能共用一条蓝。</summary>
    Player
}

/// <summary>
/// 蓝条（MP）数值的<b>唯一出口</b>：定位判定 + 池上限 + 每秒回复。
/// <para>2026-10-06 主人拍板：自然回复 / 耗蓝进技能表 / CD+MP 双门槛 / 每关开局回满。</para>
/// <para>公式（业务里不许再写数字，全部走这里）：</para>
/// <list type="bullet">
/// <item>池 = 定位基础池 × (1 + MP_GROWTH_PER_STAR × (★-1)) + MP_POOL_PER_LEVEL × (Lv-1)</item>
/// <item>回复 = 定位基础回复 × (1 + MP_GROWTH_PER_STAR × (★-1))　（等级不涨回复）</item>
/// </list>
/// 演算与紧张感 calibration：Docs/蓝条数值设计_职业与佣兵_2026-10-06.md
/// </summary>
public static class MpProfile
{
    /// <summary>
    /// 佣兵技能 → 定位。真源 = merc_skills 的「类型分类」列：
    /// 恢复 → 治疗型；法术 → 法术型；物攻 / 防御 / 被动 → 无蓝。
    /// </summary>
    public static MpArchetype OfMercSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return MpArchetype.None;
        if (!MercSkillTable.TryGet(skillId, out var row)) return MpArchetype.None;
        if (row.IsPassive) return MpArchetype.None;
        switch (row.Category)
        {
            case MercSkillTable.SkillCategory.Heal: return MpArchetype.Heal;
            case MercSkillTable.SkillCategory.Magic: return MpArchetype.Magic;
            default: return MpArchetype.None;
        }
    }

    /// <summary>
    /// 玩家技能 → 定位。真源 = player_skills「耗蓝」列（&gt;0 才有蓝）。
    /// ⚠ 不能按 <c>Kind</c> 判：法师的 thunder_chain 等是 Aoe，和剑盾的 Aoe 混在一起，分不出来。
    /// </summary>
    public static MpArchetype OfPlayerSkill(string skillId)
    {
        float cost = PlayerSkillDefs.MpCostOf(skillId);
        if (cost < 0f) return MpArchetype.None;          // 表里查不到 → 无蓝（不拦释放）
        return cost > 0f ? MpArchetype.Player : MpArchetype.None;
    }

    /// <summary>MP 池上限。定位为「无蓝」时返回 0（调用方据此不显示蓝条、不做 MP 闸门）。</summary>
    public static float Pool(MpArchetype archetype, int level, int star)
    {
        float basis = PoolBase(archetype);
        if (basis <= 0f) return 0f;
        float starMul = 1f + GameConfig.MP_GROWTH_PER_STAR * (Mathf.Max(1, star) - 1);
        return basis * starMul + GameConfig.MP_POOL_PER_LEVEL * (Mathf.Max(1, level) - 1);
    }

    /// <summary>每秒自然回复。定位为「无蓝」时返回 0。</summary>
    public static float Regen(MpArchetype archetype, int star)
    {
        float basis = RegenBase(archetype);
        if (basis <= 0f) return 0f;
        return basis * (1f + GameConfig.MP_GROWTH_PER_STAR * (Mathf.Max(1, star) - 1));
    }

    /// <summary>定位基础池（升级/升星前的那个数）。</summary>
    public static float PoolBase(MpArchetype archetype)
    {
        switch (archetype)
        {
            case MpArchetype.Heal: return GameConfig.MP_POOL_HEAL;
            case MpArchetype.Magic: return GameConfig.MP_POOL_MAGIC;
            case MpArchetype.Player: return GameConfig.MP_POOL_PLAYER;
            default: return 0f;
        }
    }

    /// <summary>定位基础回复（每秒）。</summary>
    public static float RegenBase(MpArchetype archetype)
    {
        switch (archetype)
        {
            case MpArchetype.Heal: return GameConfig.MP_REGEN_HEAL;
            case MpArchetype.Magic: return GameConfig.MP_REGEN_MAGIC;
            case MpArchetype.Player: return GameConfig.MP_REGEN_PLAYER;
            default: return 0f;
        }
    }
}
