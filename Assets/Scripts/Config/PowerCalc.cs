using UnityEngine;

/// <summary>
/// 战力换算唯一真源：抽卡卡面显示的「+N 战力」与本局实际入账（RunLoadout.TotalPower）
/// 必须共用同一套函数，禁止各写各的。
/// <para>口径（主人 2026-10-12 拍板）：战力 = 对敌方造成伤害的能力系数。
/// DPS = 攻击 × (1 + 暴击率) × 每秒攻击次数；Power = POWER_BASE + POWER_PER_DPS × DPS。
/// 底盘 POWER_BASE 是纯常数（无等级）；血量/防御/暴击伤害一律不计权重（纯输出口径）。</para>
/// </summary>
public static class PowerCalc
{
    /// <summary>战力系数：1 点 DPS 折合多少战力。与 RunLoadout.TotalPower 共用同一份字面量，不许两处各写。</summary>
    public const float POWER_PER_DPS = 4.42267f;

    /// <summary>战力底盘（纯常数，无等级）。与 RunLoadout.TotalPower 共用同一份字面量。</summary>
    public const float POWER_BASE = 130.50f;

    // 只统计这四类输出属性，其余（血量/防御/移速/元素伤害等）不影响输出，一律不计。
    static bool IsOutputAttr(AttrType t)
        => t == AttrType.Attack || t == AttrType.MagicAttack
        || t == AttrType.CritRate || t == AttrType.AttackSpeed;

    /// <summary>
    /// 一件装备带来的战力增量（不含底盘 POWER_BASE，底盘是固定的，涨跌只算 ΔDPS 部分）。
    /// <para>ΔDPS = (A+ΔA)×(1+Cr+ΔCr)×(S+ΔS) − A×(1+Cr)×S。</para>
    /// <para>基础值 A / Cr / S：
    /// ① 优先用 <see cref="Hero.Instance"/>.attr 实时取（与 RunLoadout.TotalPower 同口径，法系自动走 MagicAttack）；
    /// ② 取不到时（城镇抽卡等非战斗场景——这是<b>正常设计分支，不是异常</b>）改用职业表静态值，
    ///    攻击再叠上本装备自身攻击增量作为 A 的估算。此分支<b>不 LogError</b>，注释已标明是卡面预估、非运行时兜底。</para>
    /// <para>ΔA / ΔCr / ΔS 从 eq.attrBonus（含 globalBonus）累加；比例条目按「对基础值加成」折算成等效平量（卡面预估）。</para>
    /// </summary>
    public static int EquipPowerDelta(EquipInstance eq)
    {
        if (eq == null) return 0;

        // 副手(OffHand)的攻击在生成期已按 EquipStatRollup.OffHandAttackCapRatio 封顶
        // （见 EquipInstance.ApplyTableDrivenWeaponAttack），运行时汇总再走 EquipStatRollup.CapOffHandAttack，
        // 因此这里直接读 eq.attrBonus 不重复乘封顶比例——否则会与 RunLoadout 实际入账对不上。
        // （OffHandAttackCapRatio 常量由 EquipStatRollup 统一定义，本函数不另写 0.85 字面量。）
        bool isOffHand = eq.slotType == EquipSlotType.OffHand;

        float flatA = 0f, pctA = 0f;
        float flatCr = 0f, pctCr = 0f;
        float flatS = 0f, pctS = 0f;

        Accumulate(eq.globalBonus, isOffHand, ref flatA, ref pctA, ref flatCr, ref pctCr, ref flatS, ref pctS);
        if (eq.attrBonus != null)
        {
            for (int i = 0; i < eq.attrBonus.Count; i++)
                Accumulate(eq.attrBonus[i], isOffHand, ref flatA, ref pctA, ref flatCr, ref pctCr, ref flatS, ref pctS);
        }

        float A, Cr, S;
        var hero = Hero.Instance;
        if (hero != null && hero.attr != null)
        {
            // 优先：实时属性（与 RunLoadout.TotalPower 同口径，法系自动走 MagicAttack）
            A = hero.attr.GetAttr(PlayerJobBaseStats.CurrentAttackAttr());
            Cr = hero.attr.GetAttr(AttrType.CritRate);
            S = hero.attr.GetAttr(AttrType.AttackSpeed);
        }
        else
        {
            // 城镇等非战斗场景：Hero 取不到是正常设计分支（抽卡在城镇进行），不是异常，不 LogError。
            // 这是卡面预估分支、非运行时兜底：用职业表静态基础值，攻击再叠上本装备自身攻击增量作为 A 的估算。
            if (PlayerJobBaseStats.TryGet(PlayerJobDefs.GetSelected(), out var row))
            {
                A = Mathf.Max(1f, row.BaseAtk + GameConfig.HERO_BASE_ATTACK_OFFSET);
                Cr = row.CritRate;
                S = row.AttackInterval > 0.05f ? 1f / row.AttackInterval : GameConfig.BASE_ATTACK_SPEED;
            }
            else
            {
                // 表都没加载（极端情况）：给一个不崩的保守估计，仍不 LogError（非异常分支）。
                A = 1f; Cr = 0f; S = 1f;
            }
        }

        // 比例增量折算到基础值（近似真实属性系统的折算，卡面预估）
        float dA = flatA + pctA * A;
        float dCr = flatCr + pctCr * Cr;
        float dS = flatS + pctS * S;

        float dpsBefore = A * (1f + Cr) * S;
        float dpsAfter = (A + dA) * (1f + Cr + dCr) * (S + dS);
        float dDps = dpsAfter - dpsBefore;
        return Mathf.RoundToInt(POWER_PER_DPS * dDps);
    }

    /// <summary>
    /// 技能带来的战力增量：稀有度权重 90，星级权重 140（与 RunLoadout.TotalPower 技能段同口径）。
    /// 新技能 star 传 1；技能升星传下一星。
    /// </summary>
    public static int SkillPowerDelta(SkillRarity rar, int star)
        => (int)rar * 90 + Mathf.Max(1, star) * 140;

    /// <summary>
    /// 佣兵带来的战力增量：等级权重 70，星级权重 110（与 RunLoadout.TotalPower 佣兵段同口径）。
    /// </summary>
    public static int MercPowerDelta(int level, int star)
        => Mathf.Max(1, level) * 70 + Mathf.Max(1, star) * 110;

    static void Accumulate(AttrBonusData b, bool isOffHand,
        ref float flatA, ref float pctA, ref float flatCr, ref float pctCr, ref float flatS, ref float pctS)
    {
        if (b == null || !IsOutputAttr(b.attrType)) return;
        bool isAttack = b.attrType == AttrType.Attack || b.attrType == AttrType.MagicAttack;
        if (b.isPercent)
        {
            if (isAttack) pctA += b.value;
            else if (b.attrType == AttrType.CritRate) pctCr += b.value;
            else if (b.attrType == AttrType.AttackSpeed) pctS += b.value;
        }
        else
        {
            if (isAttack) flatA += b.value;
            else if (b.attrType == AttrType.CritRate) flatCr += b.value;
            else if (b.attrType == AttrType.AttackSpeed) flatS += b.value;
        }
    }
}
