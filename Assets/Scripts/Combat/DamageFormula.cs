using UnityEngine;

/// <summary>
/// 统一伤害结算：ATK/技能伤害 → 暴击 → 减伤/防御 → 下限 1。
/// 普攻与 SkillSystem 都走这里，避免多处公式漂移。
/// </summary>
public static class DamageFormula
{
    public const float MinDamage = 1f;

    /// <summary>
    /// 暴击倍率：优先攻击者 AttrType.CritDamage；未写入时回退 GameConfig.CRIT_MULTIPLIER。
    /// 2026-09-26 主人拍板统一 = 2（PlayerJobBaseStats 不再写职业表的 150%~180%）。critDamageBonus 为额外加算。
    /// </summary>
    public static float CritMultiplier(AttrSystem attacker = null, float critDamageBonus = 0f)
    {
        float fromAttr = attacker != null ? attacker.GetAttr(AttrType.CritDamage) : 0f;
        float baseMul = fromAttr > 0.01f ? fromAttr : GameConfig.DefaultCritMultiplier;
        return baseMul + Mathf.Max(0f, critDamageBonus);
    }

    /// <summary>是否暴击。</summary>
    public static bool RollCrit(AttrSystem attacker, float bonus = 0f)
    {
        if (attacker == null) return false;
        return Random.value < attacker.GetAttr(AttrType.CritRate) + bonus;
    }

    /// <summary>
    /// 从攻击者攻击力生成「击中前」伤害（已含暴击；尚未扣防）。
    /// magicAttack：本次是魔法伤害（法师/牧师、法球怪）→ 优先取 MagicAttack，没配就回退 Attack。
    /// </summary>
    public static float BuildAttackRaw(AttrSystem attacker, out bool isCrit, float critRateBonus = 0f, bool magicAttack = false)
    {
        isCrit = false;
        if (attacker == null) return MinDamage;
        float damage = attacker.GetAttr(AttrType.Attack);
        // 2026-09-26：魔法伤害单位用魔法攻击力；未配 MagicAttack（=0）时沿用物理攻击力，行为不变
        if (magicAttack)
        {
            float mag = attacker.GetAttr(AttrType.MagicAttack);
            if (mag > 0f) damage = mag;
        }
        isCrit = RollCrit(attacker, critRateBonus);
        if (isCrit)
            damage *= CritMultiplier(attacker);
        return Mathf.Max(MinDamage, damage);
    }

    /// <summary>
    /// 技能基础伤害（未暴击）：base + ATK * mul * (1+物魔强)。
    /// </summary>
    public static float BuildSkillBase(float baseDamage, float atkMul, AttrSystem attacker)
    {
        if (attacker == null) return Mathf.Max(MinDamage, baseDamage);
        float attack = attacker.GetAttr(AttrType.Attack);
        float phy = attacker.GetAttr(AttrType.PhyPower);
        float mag = attacker.GetAttr(AttrType.MagicPower);
        return Mathf.Max(MinDamage, baseDamage + attack * atkMul * (1f + phy + mag));
    }

    /// <summary>对技能基础伤害应用暴击。</summary>
    public static float ApplyCrit(float baseDamage, AttrSystem attacker, out bool isCrit)
    {
        isCrit = RollCrit(attacker);
        if (!isCrit) return Mathf.Max(MinDamage, baseDamage);
        return Mathf.Max(MinDamage, baseDamage * CritMultiplier(attacker));
    }

    /// <summary>最终扣血量：raw 已含暴击；再减 DEF。
    /// ignoreDefense：引导等特殊命中。
    /// isMagic：2026-09-26 主人口径 —— 魔法伤害走魔法防御（MagicDefense），物理伤害走物理防御（Defense）。
    /// 目标没写 MagicDefense 时，按 Defense × GameConfig.MAGIC_DEFENSE_FALLBACK_RATIO 兜底（沿用物理防御）。
    /// </summary>
    public static float FinalHit(float rawDamage, AttrSystem defender, bool ignoreDefense = false, bool isMagic = false)
    {
        float dmg = Mathf.Max(0f, rawDamage);
        if (!ignoreDefense && defender != null)
        {
            float def = defender.GetAttr(AttrType.Defense);
            if (isMagic)
            {
                float magicDef = defender.GetAttr(AttrType.MagicDefense);
                // 没配魔法防御 → 等比沿用物理防御，等配表补真值
                def = magicDef > 0f ? magicDef : def * GameConfig.MAGIC_DEFENSE_FALLBACK_RATIO;
            }
            dmg = Mathf.Max(MinDamage, dmg - def);
        }
        return Mathf.Max(MinDamage, dmg);
    }

    /// <summary>主角特殊武器对目标的倍率（暮火之杖等）。</summary>
    public static float ApplyAttackerSpecials(float damage, UnitBase caster, UnitBase target)
    {
        if (caster is Hero)
            return Mathf.Max(MinDamage, damage * SpecialWeapons.GetDamageMultiplier(target));
        return damage;
    }
}
