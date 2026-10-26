using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战力换算唯一真源：抽卡卡面显示的「+N 战力」与本局实际入账（RunLoadout.TotalPower）
/// 必须共用同一套函数，禁止各写各的。
/// <para>口径（主人 2026-10-12 拍板，2026-10-17 修订）：
/// DPS = 攻击 × (1 + 暴击率) × 每秒攻击次数；
/// Power = POWER_BASE + POWER_PER_DPS × DPS + POWER_PER_HP × 血量 + POWER_PER_DEF × (物防+魔防)。
/// 底盘 POWER_BASE 是纯常数（无等级）；血量/防御 2026-10-17 起计入权重（主人：涨的少不能不涨），
/// 暴击伤害/移速等其余属性仍不计。</para>
/// <para>2026-10-20 方案 B：装备类提示值升级为换装真值 <see cref="EquipSwapPowerDelta"/>
/// （副本 AttrSystem 全链路模拟，与头顶飘字/TotalPower 同源）；<see cref="EquipPowerDelta"/>
/// 保留为城镇等非战斗环境的卡面预估分支与技能/佣兵权重的底座。</para>
/// </summary>
public static class PowerCalc
{
    /// <summary>战力系数：1 点 DPS 折合多少战力。与 RunLoadout.TotalPower 共用同一份字面量，不许两处各写。</summary>
    public const float POWER_PER_DPS = 4.42267f;

    /// <summary>战力系数：1 点生命上限折合多少战力（2026-10-17 主人拍板：血量计入战力）。</summary>
    public const float POWER_PER_HP = 0.15f;

    /// <summary>战力系数：1 点防御（物防/魔防同系数）折合多少战力（2026-10-17 主人拍板）。</summary>
    public const float POWER_PER_DEF = 2.5f;

    /// <summary>战力底盘（纯常数，无等级）。与 RunLoadout.TotalPower 共用同一份字面量。</summary>
    public const float POWER_BASE = 130.50f;

    /// <summary>
    /// 吸血折等效生命用的「一场战斗秒数」假设（2026-10-26 加进战力口径时定）。
    /// 依据：主线单场战斗 30~60 秒，取中位 45。吸血每秒回 DPS×ratio，T 秒累计 = 等效生命。
    /// 只影响吸血词条的战力读数，不影响战斗本身。
    /// </summary>
    public const float POWER_LIFESTEAL_BATTLE_SEC = 45f;

    // 统计的属性：四类输出属性 + 血量/防御生存属性（2026-10-17 起血防计入）。
    // 2026-10-26 主人拍板「加了属性肯定得加战力」两批放行：
    //   力量/智力 → Accumulate 只累计平量，折算段按职业 ×2 折进攻击/魔攻（与 AttrSystem 派生同口径）；
    //   减伤/闪避/吸血/暴伤/火冰 → 折算段按真实结算折等效防御/等效血量/期望乘区（与 PlayerPowerSegment 同式）。
    static bool IsCountedAttr(AttrType t)
        => t == AttrType.Attack || t == AttrType.MagicAttack
        || t == AttrType.CritRate || t == AttrType.AttackSpeed
        || t == AttrType.MaxHp || t == AttrType.Defense || t == AttrType.MagicDefense
        || t == AttrType.Vitality // 体质派生 +10 血 +1 防（AttrSystem.ApplyDerivedAttributes），按增量折算计入
        || t == AttrType.Strength || t == AttrType.Intelligence // 2026-10-26 起成长段 ×2 折攻/魔攻
        || t == AttrType.DamageReduce || t == AttrType.Dodge || t == AttrType.LifeSteal
        || t == AttrType.CritDamage || t == AttrType.FireDamage || t == AttrType.IceDamage; // 2026-10-26 生存/输出乘区批

    /// <summary>
    /// 一件装备带来的战力增量（不含底盘 POWER_BASE，底盘是固定的，涨跌只算 ΔDPS 部分）。
    /// <para>ΔDPS = (A+ΔA)×(1+Cr+ΔCr)×(S+ΔS) − A×(1+Cr)×S。</para>
    /// <para>基础值 A / Cr / S：
    /// ① 优先用 <see cref="Hero.Instance"/>.attr 实时取（与 RunLoadout.TotalPower 同口径，法系自动走 MagicAttack）；
    /// ② 取不到时（城镇抽卡等非战斗场景——这是<b>正常设计分支，不是异常</b>）改用职业表静态值，
    ///    攻击再叠上本装备自身攻击增量作为 A 的估算。此分支<b>不 LogError</b>，注释已标明是卡面预估、非运行时兜底。</para>
    /// <para>ΔA / ΔCr / ΔS 从 eq.attrBonus（含 globalBonus）累加；比例条目按「对基础值加成」折算成等效平量（卡面预估）。
    /// 力量/智力 2026-10-26 起按职业 ×2 折进攻击（与 AttrSystem 成长段派生同口径），城镇预估与战斗真值一致。</para>
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
        float flatHp = 0f, pctHp = 0f;
        float flatDef = 0f, pctDef = 0f;
        float flatVit = 0f;
        float flatStr = 0f, flatInt = 0f;
        // 2026-10-26 生存/输出乘区批（全部加算小数/点值，掉落与模板两条路径口径已统一）
        float flatRed = 0f, flatDodge = 0f, flatLifeSteal = 0f, flatCritDmg = 0f, flatFire = 0f, flatIce = 0f;

        Accumulate(eq.globalBonus, isOffHand, ref flatA, ref pctA, ref flatCr, ref pctCr, ref flatS, ref pctS,
            ref flatHp, ref pctHp, ref flatDef, ref pctDef, ref flatVit, ref flatStr, ref flatInt,
            ref flatRed, ref flatDodge, ref flatLifeSteal, ref flatCritDmg, ref flatFire, ref flatIce);
        if (eq.attrBonus != null)
        {
            for (int i = 0; i < eq.attrBonus.Count; i++)
                Accumulate(eq.attrBonus[i], isOffHand, ref flatA, ref pctA, ref flatCr, ref pctCr, ref flatS, ref pctS,
                    ref flatHp, ref pctHp, ref flatDef, ref pctDef, ref flatVit, ref flatStr, ref flatInt,
                    ref flatRed, ref flatDodge, ref flatLifeSteal, ref flatCritDmg, ref flatFire, ref flatIce);
        }

        float A, Cr, S, HP, DF;
        var hero = Hero.Instance;
        if (hero != null && hero.attr != null)
        {
            // 优先：实时属性（与 RunLoadout.TotalPower 同口径，法系自动走 MagicAttack）
            A = hero.attr.GetAttr(PlayerJobBaseStats.CurrentAttackAttr());
            Cr = hero.attr.GetAttr(AttrType.CritRate);
            S = hero.attr.GetAttr(AttrType.AttackSpeed);
            HP = hero.attr.GetAttr(AttrType.MaxHp);
            DF = hero.attr.GetAttr(AttrType.Defense) + hero.attr.GetAttr(AttrType.MagicDefense);
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
                HP = row.BaseHp;
                DF = row.BaseDef + row.BaseMagicDef;
            }
            else
            {
                // 表都没加载（极端情况）：给一个不崩的保守估计，仍不 LogError（非异常分支）。
                A = 1f; Cr = 0f; S = 1f; HP = 0f; DF = 0f;
            }
        }

        // 比例增量折算到基础值（近似真实属性系统的折算，卡面预估）
        // 【2026-10-26 主人拍板「加了属性肯定得加战力」】力量/智力按 AttrSystem.ApplyDerivedAttributes 同口径
        // 折进攻击：成长段 ×2（装备词条全是成长段，不存在基底部分）。物理系折力量、法系折智力 ——
        // 装备池已按职业过滤（equip_attr_ranges 第 12 列）两侧互斥，但模板件路径仍做个分流保险。
        // ⚠ 必须与 AttrSystem 派生系数同步改：那边 ×2 这边才 ×2，否则城镇预估与战斗真值两套数字。
        bool magicJob = PlayerJobBaseStats.IsMagicJob(PlayerJobDefs.GetSelected());
        float dA = flatA + pctA * A + (magicJob ? flatInt : flatStr) * 2f;
        float dCr = flatCr + pctCr * Cr;
        float dS = flatS + pctS * S;
        // 血量/防御增量（2026-10-17 起计入战力）；体质平量按派生口径折算（+10 血 +1 防）
        float dHp = flatHp + pctHp * HP + flatVit * 10f;
        float dDef = flatDef + pctDef * DF + flatVit * 1f;

        // 【2026-10-26 乘区批】与 PlayerPowerSegment 四个乘区同式（城镇无 Hero → 本装备增量≈全量，近似成立）：
        // 暴击期望（暴伤词条加算在默认倍率上）；元素（火/冰取大）；减伤/闪避折等效防御；吸血折等效生命。
        float critMulBase = GameConfig.DefaultCritMultiplier;
        float dpsMulBefore = 1f + Cr * (critMulBase - 1f);
        float dpsMulAfter = 1f + (Cr + dCr) * (critMulBase + flatCritDmg - 1f);
        float eleAfter = Mathf.Max(flatFire, flatIce) * GameConfig.FIRE_BONUS_PER_POINT;
        float dpsBefore = A * dpsMulBefore * S;
        float dpsAfter = (A + dA) * dpsMulAfter * (S + dS) * (1f + eleAfter);
        float dDps = dpsAfter - dpsBefore;

        float rd = Mathf.Min(0.5f, Mathf.Clamp01(flatRed) + Mathf.Clamp01(flatDodge));
        float defBaseEq = DF + DamageFormula.DefenseK;
        float defEq = defBaseEq * defBaseEq / 100f * rd;
        float hpEq = dpsAfter * Mathf.Max(0f, flatLifeSteal) * POWER_LIFESTEAL_BATTLE_SEC;

        return Mathf.RoundToInt(POWER_PER_DPS * dDps
            + POWER_PER_HP * (dHp + hpEq) + POWER_PER_DEF * (dDef + defEq));
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

    // ============================================================
    // 战力真值（2026-10-20 主人拍板「提示增加的战力必须与实际一致」）
    // ============================================================

    /// <summary>
    /// 穿上 newEq（顶掉同槽旧件）后的战力差 —— <b>与头顶飘字 / RunLoadout.TotalPower 同一真值</b>。
    /// 用一次性 <see cref="AttrSystem"/> 副本按 Hero.RecalcAttr 完整链路重算：
    /// 职业表 + 天赋 + 传说武器 + 装备表（强化倍率/副手封顶/技能被动）+ tempBuffs + 武器类型攻速乘区，
    /// 玩家段与 RunLoadout.TotalPower 同式同取整。副本算完即弃，不碰 Hero / 背包本体状态
    /// （这是抽卡/弹窗时的确定性模拟，不是运行时兜底）。
    /// <para>非战斗环境（城镇等取不到 Hero/背包）回退卡面预估 <see cref="EquipPowerDelta"/>
    /// —— 正常设计分支，非异常。</para>
    /// <para>已知残余差异：玩家头顶挂着定时攻击 buff 时，飘字读数含 buff 缩放，本函数给的是
    /// 去 buff 真值；buff 过期后两者一致。</para>
    /// </summary>
    public static int EquipSwapPowerDelta(EquipInstance newEq)
    {
        if (newEq == null) return 0;
        var bag = BattleBagSystem.Instance;
        var hero = Hero.Instance;
        if (bag == null || hero == null || hero.attr == null)
            return EquipPowerDelta(newEq);   // 城镇等非战斗场景：卡面预估分支（正常设计，不是异常）

        int segAfter = SimPlayerSegment(bag, newEq);    // 换上新件（快照内部顶掉同槽旧件）
        int segBefore = SimPlayerSegment(bag, null);    // 当前穿戴（同一模拟器，口径自洽）
        return segAfter - segBefore;
    }

    /// <summary>
    /// 用副本 AttrSystem 重算给定换装方案下的玩家属性段战力。
    /// 链路必须与 Hero.RecalcAttr 逐项对齐：BuildBonusListForSet（装备表）→ tempBuffs →
    /// RecalcAllAttr → 武器类型攻速乘区。两侧都用副本，天然剔除定时 buff 层的随机干扰。
    /// </summary>
    static int SimPlayerSegment(BattleBagSystem bag, EquipInstance newEq)
    {
        var set = bag.SnapshotEquippedAfterSwap(newEq, out _);
        var bonus = EquipStatRollup.BuildBonusListForSet(set);
        var bm = BattleManager.Instance;
        if (bm != null && bm.tempBuffs != null)
            bonus.AddRange(bm.tempBuffs);               // 与 Hero.RecalcAttr 同链路（强化卡参与重算）

        var replica = new AttrSystem(AttrOwnerKind.Player);
        // 【2026-10-26】补等级倾向（基底 + level-1）：之前副本只写 BASE_*，高等级玩家（狂战力量/
        // 法师智力/游侠敏捷/剑盾体质）模拟侧吃不到 → 随属性放大的词条净差偏低。
        // 与 LevelSystem.RestoreHeroLevel 对 hero.attr 的回填同源同口径。
        LevelSystem.ApplyTendencyToAttr(replica, LevelSystem.CurrentLevel);
        replica.RecalcAllAttr(bonus);
        ApplyWeaponAttackSpeedMul(replica, set);
        return PlayerPowerSegment(replica);
    }

    /// <summary>
    /// 玩家属性段战力：与 <see cref="RunLoadout.TotalPower"/> 玩家段同一函数（TotalPower 直接调这里，不许再抄一份）。
    /// <para>2026-10-26 主人拍板「加了属性肯定得加战力」升级，四个乘区全部对齐真实结算（DamageFormula / UnitBase.Combat）：
    /// ① 暴击期望 = 1 + Cr×(暴伤倍率-1) —— 暴伤=默认 2.0 时退化为旧式 (1+Cr)，装备「暴击伤害」词条从此计入；
    /// ② 元素 = ×(1 + max(火,冰)×0.01) —— UnitBase.Combat.ResolveElementalBonus 只取较大一系，这里同样 max 不相加；
    /// ③ 减伤/闪避折等效防御 —— 二者都是受击期望乘区，EHP ×1/(1-r)，一阶等效防御点数 = (def+100)²/100 × r，
    ///    加进防御段（def=DamageFormula.DefenseK=100 的减伤公式基准，def 取当前双防合计，r 上限闸 0.5 防极端堆叠）；
    /// ④ 吸血折等效生命 = DPS×ratio×一场战斗秒数（POWER_LIFESTEAL_BATTLE_SEC），加进血量段。</para>
    /// <para>🔴 向后兼容：不穿减伤/闪避/吸血/暴伤/元素装备时，①③④ 全部退化为旧式，数字与旧口径完全一致。</para>
    /// </summary>
    public static int PlayerPowerSegment(AttrSystem a)
    {
        float attack = a.GetAttr(PlayerJobBaseStats.CurrentAttackAttr());
        float critRate = a.GetAttr(AttrType.CritRate);
        float attackSpeed = a.GetAttr(AttrType.AttackSpeed);

        // ① 暴击期望乘区（CritMultiplier 的战力侧复刻：基底默认 2.0，装备暴伤加算在其上）
        float critMul = a.GetAttr(AttrType.CritDamage);
        if (critMul <= 1.01f) critMul = GameConfig.DefaultCritMultiplier;

        // ② 元素乘区（火/冰取大，与 ResolveElementalBonus 同口径）
        float ele = Mathf.Max(a.GetAttr(AttrType.FireDamage), a.GetAttr(AttrType.IceDamage))
                    * GameConfig.FIRE_BONUS_PER_POINT;

        float dps = attack * (1f + critRate * (critMul - 1f)) * attackSpeed * (1f + ele);

        float defTotal = a.GetAttr(AttrType.Defense) + a.GetAttr(AttrType.MagicDefense);

        // ③ 减伤/闪避 → 等效防御（一阶：r+d ≤ 0.5 内误差可忽略；闸住防极端堆叠把战力顶飞）
        float rd = Mathf.Min(0.5f,
            Mathf.Clamp01(a.GetAttr(AttrType.DamageReduce)) + Mathf.Clamp01(a.GetAttr(AttrType.Dodge)));
        float defBase = defTotal + DamageFormula.DefenseK;
        float defEq = defBase * defBase / 100f * rd;

        // ④ 吸血 → 等效生命
        float lifeSteal = Mathf.Max(0f, a.GetAttr(AttrType.LifeSteal));
        float hpEq = dps * lifeSteal * POWER_LIFESTEAL_BATTLE_SEC;

        float hp = a.GetAttr(AttrType.MaxHp);
        return Mathf.RoundToInt(POWER_BASE + POWER_PER_DPS * dps
            + POWER_PER_HP * (hp + hpEq) + POWER_PER_DEF * (defTotal + defEq));
    }

    /// <summary>复刻 Hero.RecalcAttr 的武器类型攻速乘区（SetAttr 覆盖，Hero.cs L259-265），模拟侧必须同样打上。</summary>
    static void ApplyWeaponAttackSpeedMul(AttrSystem attr, List<EquipInstance> set)
    {
        var weapon = TryWeaponInSet(set);
        if (weapon?.template == null) return;
        float swordSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.WeaponKind.Sword);
        float kindSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.ResolveKind(weapon));
        float mul = swordSpd > 0.01f ? kindSpd / swordSpd : 1f;
        attr.SetAttr(AttrType.AttackSpeed, Mathf.Max(0.2f, attr.GetAttr(AttrType.AttackSpeed) * mul));
    }

    /// <summary>复刻 Hero.TryGetEquippedWeaponInstance 的挑武器规则（主手优先、副手兜底），作用在模拟穿戴集上。</summary>
    static EquipInstance TryWeaponInSet(List<EquipInstance> set)
    {
        EquipStatRollup.PickLogicalWeapons(set, out var main, out var off);
        if (main?.template != null && (main.weaponType != WeaponType.None || main.slotType == EquipSlotType.MainHand))
            return main;
        if (off?.template != null && off.weaponType != WeaponType.None) return off;
        return null;
    }

    static void Accumulate(AttrBonusData b, bool isOffHand,
        ref float flatA, ref float pctA, ref float flatCr, ref float pctCr, ref float flatS, ref float pctS,
        ref float flatHp, ref float pctHp, ref float flatDef, ref float pctDef, ref float flatVit,
        ref float flatStr, ref float flatInt,
        ref float flatRed, ref float flatDodge, ref float flatLifeSteal, ref float flatCritDmg,
        ref float flatFire, ref float flatIce)
    {
        if (b == null || !IsCountedAttr(b.attrType)) return;
        bool isAttack = b.attrType == AttrType.Attack || b.attrType == AttrType.MagicAttack;
        if (b.isPercent)
        {
            if (isAttack) pctA += b.value;
            else if (b.attrType == AttrType.CritRate) pctCr += b.value;
            else if (b.attrType == AttrType.AttackSpeed) pctS += b.value;
            else if (b.attrType == AttrType.MaxHp) pctHp += b.value;
            else if (b.attrType == AttrType.Defense || b.attrType == AttrType.MagicDefense) pctDef += b.value;
            // 减伤/闪避/吸血/暴伤/火冰/力/智装备池不出百分比档（掉落 TryMapAttr 与 ArmorAffixSystem.IsPercent
            // 都强制这些加算），万一配出会被静默丢进这里不计 —— 要放行先去 AttrSystem 补派生/结算口径。
        }
        else
        {
            if (isAttack) flatA += b.value;
            else if (b.attrType == AttrType.CritRate) flatCr += b.value;
            else if (b.attrType == AttrType.AttackSpeed) flatS += b.value;
            else if (b.attrType == AttrType.MaxHp) flatHp += b.value;
            else if (b.attrType == AttrType.Defense || b.attrType == AttrType.MagicDefense) flatDef += b.value;
            else if (b.attrType == AttrType.Vitality) flatVit += b.value;
            else if (b.attrType == AttrType.Strength) flatStr += b.value;
            else if (b.attrType == AttrType.Intelligence) flatInt += b.value;
            else if (b.attrType == AttrType.DamageReduce) flatRed += b.value;
            else if (b.attrType == AttrType.Dodge) flatDodge += b.value;
            else if (b.attrType == AttrType.LifeSteal) flatLifeSteal += b.value;
            else if (b.attrType == AttrType.CritDamage) flatCritDmg += b.value;
            else if (b.attrType == AttrType.FireDamage) flatFire += b.value;
            else if (b.attrType == AttrType.IceDamage) flatIce += b.value;
        }
    }
}
