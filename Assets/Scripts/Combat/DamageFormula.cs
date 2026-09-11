using UnityEngine;

/// <summary>
/// 统一伤害结算：ATK/技能伤害 → 暴击 → 减伤/防御 → 下限 1。
/// 普攻与 SkillSystem 都走这里，避免多处公式漂移。
/// </summary>
public static class DamageFormula
{
    public const float MinDamage = 1f;

    /// <summary>
    /// 暴击倍率：优先攻击者 AttrType.CritDamage（职业表 150%→1.5）；
    /// 未写入时回退 1.5+BASE_CRIT_DAMAGE。critDamageBonus 为额外加算。
    /// </summary>
    public static float CritMultiplier(AttrSystem attacker = null, float critDamageBonus = 0f)
    {
        float fromAttr = attacker != null ? attacker.GetAttr(AttrType.CritDamage) : 0f;
        float baseMul = fromAttr > 0.01f ? fromAttr : GameConfig.DefaultCritMultiplier;
        return baseMul + Mathf.Max(0f, critDamageBonus);
    }

    /// <summary>是否暴击。</summary>
    public static bool RollCrit(AttrSystem attacker)
    {
        if (attacker == null) return false;
        return Random.value < attacker.GetAttr(AttrType.CritRate);
    }

    /// <summary>
    /// 从攻击者攻击力生成「击中前」伤害（已含暴击；尚未扣防）。
    /// </summary>
    public static float BuildAttackRaw(AttrSystem attacker, out bool isCrit)
    {
        isCrit = false;
        if (attacker == null) return MinDamage;
        float damage = attacker.GetAttr(AttrType.Attack);
        isCrit = RollCrit(attacker);
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
    /// </summary>
    public static float FinalHit(float rawDamage, AttrSystem defender, bool ignoreDefense = false)
    {
        float dmg = Mathf.Max(0f, rawDamage);
        if (!ignoreDefense && defender != null)
        {
            float def = defender.GetAttr(AttrType.Defense);
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
