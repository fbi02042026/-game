using UnityEngine;

/// <summary>
/// 统一伤害结算：ATK/技能伤害 → 暴击 → 减伤/防御 → 下限 1。
/// 普攻与 SkillSystem 都走这里，避免多处公式漂移。
/// </summary>
public static class DamageFormula
{
    public const float MinDamage = 1f;

    /// <summary>
    /// 乘法减伤公式的常数 K：reduction = def / (def + DefenseK)。
    /// 2026-10-09 主人拍板：当前取 100（主人原话「为什么要加几百，先加100看看」）。
    /// 想调平衡**只改这一个数**：调小 → 同样防御减伤更多；调大 → 减伤更少、更平缓。
    /// </summary>
    public const float DefenseK = 100f;

    /// <summary>
    /// 减伤率上限 0.75（最多减掉 75%，剩下 25% 的伤害必打进去）。
    /// 2026-09-28 主人拍板定的口径，防御再高也不会免疫。
    /// </summary>
    public const float MaxReductionRatio = 0.75f;

    /// <summary>
    /// 伤害结算错误码。2026-09-27 主人拍板：**不要静默兜底，有问题直接报错码**。
    /// 出现这些码说明配置/初始化缺真值，按码去补，不要改公式绕过去。
    /// </summary>
    public static class Err
    {
        /// <summary>DF-001：要结算减防，但 defender（目标属性系统）是空。</summary>
        public const string NoDefender = "DF-001";
        /// <summary>DF-002：物理伤害结算，但目标属性表里没有 Defense 键（属性系统没初始化该属性）。</summary>
        public const string NoDefense = "DF-002";
        /// <summary>DF-003：魔法伤害结算，但目标属性表里没有 MagicDefense 键（属性系统没初始化该属性）。</summary>
        public const string NoMagicDefense = "DF-003";
        /// <summary>DF-004：魔法单位起手，但攻击者属性表里没有 MagicAttack（或值为 0）。</summary>
        public const string NoMagicAttack = "DF-004";
    }

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
    /// magicAttack：本次是魔法伤害（法师/牧师、法球怪）→ 取 MagicAttack；取不到（未登记或为 0）报 DF-004 错误码。
    /// </summary>
    public static float BuildAttackRaw(AttrSystem attacker, out bool isCrit, float critRateBonus = 0f, bool magicAttack = false)
    {
        isCrit = false;
        if (attacker == null) return MinDamage;
        float damage = attacker.GetAttr(AttrType.Attack);
        // 2026-09-27：魔法伤害单位取魔法攻击力。取不到（没登记 / 为 0）时**不再静默沿用物攻**，
        // 报 DF-004 让主人看到——按主人「不要兜底、有问题直接给错误码」的口径。
        // 这里仍以 Attack 继续算（否则伤害会掉到下限 1，战斗直接崩坏），但错误一定会打出来。
        if (magicAttack)
        {
            float mag = attacker.HasAttr(AttrType.MagicAttack) ? attacker.GetAttr(AttrType.MagicAttack) : 0f;
            if (mag > 0f) damage = mag;
            else
                Debug.LogError($"[{Err.NoMagicAttack}] 魔法单位起手但没有 MagicAttack（Attack={damage}）。" +
                               "已暂时用物理攻击力继续结算，**这不是正常状态**：请把该单位的魔法攻击力配进属性/配表。");
        }
        isCrit = RollCrit(attacker, critRateBonus);
        if (isCrit)
            damage *= CritMultiplier(attacker);
        return Mathf.Max(MinDamage, damage);
    }

    /// <summary>
    /// 技能基础伤害（未暴击）：base + ATK * mul * (1+物魔强)。
    /// <para>2026-09-29：新增 <paramref name="magic"/> —— 魔法伤害技能取 <see cref="AttrType.MagicAttack"/>
    /// 作基底，与普攻（<see cref="BuildAttackRaw"/>）口径一致。之前无条件取 Attack(物攻)，
    /// 攻击力分流到 MagicAttack 之后法师/牧师的技能会直接塌成 baseDamage。</para>
    /// </summary>
    public static float BuildSkillBase(float baseDamage, float atkMul, AttrSystem attacker, bool magic = false)
    {
        if (attacker == null) return Mathf.Max(MinDamage, baseDamage);
        float attack = attacker.GetAttr(magic ? AttrType.MagicAttack : AttrType.Attack);
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

    /// <summary>
    /// 最终扣血量：raw 已含暴击；再减防御。
    /// <para>2026-09-27 主人拍板 —— **物理与魔法彻底两条，不走一套、不兜底**：</para>
    /// <list type="bullet">
    /// <item>物理伤害 → 只扣 <see cref="AttrType.Defense"/>，与魔防无关；</item>
    /// <item>魔法伤害 → 只扣 <see cref="AttrType.MagicDefense"/>，与物防无关；
    ///       目标魔防是 0 就是 0（全额吃伤害），**不再拿物理防御换算**。</item>
    /// </list>
    /// 属性压根没登记（不是"登记了但值为 0"）→ 打 DF-002 / DF-003 错误码，按 0 结算，绝不静默套用另一套防御。
    /// ignoreDefense：引导等特殊命中，直接免减防。
    /// </summary>
    public static float FinalHit(float rawDamage, AttrSystem defender, bool ignoreDefense = false, bool isMagic = false)
    {
        float dmg = Mathf.Max(0f, rawDamage);
        if (ignoreDefense) return Mathf.Max(MinDamage, dmg);

        if (defender == null)
        {
            Debug.LogError($"[{Err.NoDefender}] 结算伤害时目标属性系统为空（raw={dmg}, isMagic={isMagic}），" +
                           "无法取防御。已按不减防处理 —— 请检查调用方是不是漏传了目标。");
            return Mathf.Max(MinDamage, dmg);
        }

        float def;
        if (isMagic)
        {
            if (!defender.HasAttr(AttrType.MagicDefense))
                Debug.LogError($"[{Err.NoMagicDefense}] 魔法伤害结算，但目标属性表里**没有 MagicDefense 这个属性**" +
                               "（属性系统初始化没写入）。已按 0 结算，不沿用物理防御 —— 请把魔防补进属性初始化/配表。");
            def = defender.GetAttr(AttrType.MagicDefense);
        }
        else
        {
            if (!defender.HasAttr(AttrType.Defense))
                Debug.LogError($"[{Err.NoDefense}] 物理伤害结算，但目标属性表里**没有 Defense 这个属性**" +
                               "（属性系统初始化没写入）。已按 0 结算 —— 请把物防补进属性初始化/配表。");
            def = defender.GetAttr(AttrType.Defense);
        }

        // 2026-10-09 主人拍板：减法减伤改成**乘法减伤**（reduction = def / (def + DefenseK)），
        // 解决「防御 ≥ 怪物伤害时一律保底掉 1 血」的问题。
        // def 理论上非负，这里用 Max(0f, def) 兜住：负数会算出负减伤率，反而把伤害放大。
        float defSafe = Mathf.Max(0f, def);
        float reduction = Mathf.Min(MaxReductionRatio, defSafe / (defSafe + DefenseK));
        return Mathf.Max(MinDamage, dmg * (1f - reduction));
    }

    /// <summary>主角特殊武器对目标的倍率（暮火之杖等）。</summary>
    public static float ApplyAttackerSpecials(float damage, UnitBase caster, UnitBase target)
    {
        if (caster is Hero)
            return Mathf.Max(MinDamage, damage * SpecialWeapons.GetDamageMultiplier(target));
        return damage;
    }
}
