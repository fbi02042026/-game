using UnityEngine;

/// <summary>
/// 属性与形态：近战判定、射程档、属性初始化、升星/养成/本局临时加成、攻击类型与特效。
/// 【2026-10-23 主人拍板】原 Mercenary.cs 已 1025 行（项目约定 >600 行必须拆），
/// 按职责切成 4 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class Mercenary : UnitBase
{
    public static bool IsMeleeRoleId(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        // 2026-10-23 主人拍板：前缀/职业名判定唯一出口 = MercRosterDefs.JobKindOf，本类不再抄第二份。
        var kind = MercRosterDefs.JobKindOf(id);
        // 【2026-10-24 主人拍板】Berserker 也算近战（拆档只改射程，不改近战/远程判定与特效）。
        return kind == MercRosterDefs.MercJobKind.Melee
            || kind == MercRosterDefs.MercJobKind.Polearm
            || kind == MercRosterDefs.MercJobKind.Berserker;
    }

    /// <summary>
    /// 该佣兵是否用法术攻击（魔攻）。hireId / assetId 都吃。
    /// <para>「获得佣兵」弹窗属性行二选一（攻击/魔攻、防御/魔防）的<b>唯一判据</b>——
    /// 与 <see cref="GetAttackType"/> 同源，UI 侧不许自己复制前缀表。</para>
    /// </summary>
    public static bool UsesMagicAttack(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        // 2026-10-23 主人拍板：与 IsMeleeRoleId 同源，都走 JobKindOf。
        return MercRosterDefs.JobKindOf(id) == MercRosterDefs.MercJobKind.Magic;
    }

    static bool IsMeleeMercId(string id) => IsMeleeRoleId(id);

    void ApplyMeleeRangeVsPlayer()
    {
        if (!IsMeleeMercId(mercId)) return;
        float playerRange = GameConfig.RangeSword;
        if (Hero.Instance?.attr != null)
            playerRange = Hero.Instance.attr.GetAttr(AttrType.AttackRange);
        if (GameConfig.IsRangedAttackRange(playerRange)) return;
        attr.SetAttr(AttrType.AttackRange, playerRange * 0.8f);
    }

    // Face() 已在 UnitBase

    void SetupAttributes(string id, int level)
    {
        attr.ResetToBase();
        _outStarApplied = false;   // 【2026-10-22 T8】基底重建后加成要重挂（SetHireId/下方出口会再调）
        _growApplied = false;      // 【2026-10-23 施工单 T4】养成资质加成同款幂等闸，与升星并联互不干扰
        _bondApplied = false;      // 【2026-10-27 羁绊】基底重建后羁绊乘区要重挂
        level = Mathf.Max(1, level);

        if (MercRosterDefs.TryGetByAssetId(id, out _))
        {
            MercRosterDefs.ApplyCombatStats(id, level,
                out float hp, out float atk, out float def, out float atkSpd, out float move, out float range);
            attr.SetBaseAndCurrent(AttrType.MaxHp, hp);
            attr.SetBaseAndCurrent(AttrType.Attack, atk);
            attr.SetBaseAndCurrent(AttrType.Defense, def);
            // 2026-09-27 主人拍板：防御拆物/魔两条，佣兵同样登记魔防键（伤害公式缺键会报 DF-003）
            attr.SetBaseAndCurrent(AttrType.MagicDefense, def);
            attr.SetBaseAndCurrent(AttrType.MagicAttack, GetAttackType() == WeaponAttackType.Magic ? atk : 0f);
            attr.SetBaseAndCurrent(AttrType.AttackSpeed, atkSpd);
            attr.SetBaseAndCurrent(AttrType.MoveSpeed, move);
            attr.SetBaseAndCurrent(AttrType.AttackRange, range);
            attr.SetBaseAndCurrent(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);
            currentHp = attr.GetAttr(AttrType.MaxHp);
            ApplyMeleeRangeVsPlayer();
            // 【2026-10-22 主人拍板 v4.1】石像赐福等局内 buff（tempBuffs）佣兵也要吃到：
            // 装配时机 = 属性基底写完后统一叠加，与主角 Hero.RecalcAttr 的 extraBonus 同款口径。
            ApplyRunTempBuffs();
            ApplyOutStarBonus();   // 【2026-10-22 施工单 T8】局外星攻防加成（hireId 已设时在此生效，未设等 SetHireId）
            ApplyGrowBonus();      // 【2026-10-23 施工单 T4】养成资质加成（外层乘区，与升星并联）
            ApplyBondClearBonus(); // 【2026-10-27 羁绊】通关羁绊后全属性 ×1.5（与升星/养成并联）
            return;
        }

        bool advanced = GameConfig.GetMercTier(id) == MercTier.Advanced;
        float baseHp, baseAtk, baseDef, atkInterval;
        // 【2026-10-24 主人拍板】射程唯一出口 = MercRosterDefs.ResolveRange，与上方花名册路径同源；
        // AttackRangeTable.GetMercWorld 是它的旧孪生实现（同一语义两个出口，违反铁律13），已删。
        float atkRange = MercRosterDefs.ResolveRange(id);

        if (id.StartsWith("dunbing"))
        {
            if (advanced) { baseHp = 550; baseAtk = 18; baseDef = 20; atkInterval = 1.1f; }
            else { baseHp = 300; baseAtk = 10; baseDef = 10; atkInterval = 1.2f; }
        }
        else if (id.StartsWith("gongshou"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
        }
        else if (id.StartsWith("kuangzhan"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
        }
        else if (id.StartsWith("naima") || id.StartsWith("fashi") || id.StartsWith("mushi"))
        {
            if (advanced) { baseHp = 320; baseAtk = 15; baseDef = 8; atkInterval = 1.3f; }
            else { baseHp = 180; baseAtk = 8; baseDef = 4; atkInterval = 1.5f; }
        }
        else if (id.StartsWith("zhongzhan"))
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
        }
        else
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
        }

        float hpMul = 1f + (level - 1) * 0.1f;
        float atkAdd = (level - 1) * 2f;
        attr.SetBaseAndCurrent(AttrType.MaxHp, baseHp * hpMul);
        attr.SetBaseAndCurrent(AttrType.Attack, baseAtk + atkAdd);
        attr.SetBaseAndCurrent(AttrType.Defense, baseDef);
        // 2026-09-27 主人拍板：防御拆物/魔两条，佣兵同样登记魔防键（伤害公式缺键会报 DF-003）
        attr.SetBaseAndCurrent(AttrType.MagicDefense, baseDef);
        attr.SetBaseAndCurrent(AttrType.MagicAttack, GetAttackType() == WeaponAttackType.Magic ? (baseAtk + atkAdd) : 0f);
        attr.SetBaseAndCurrent(AttrType.AttackSpeed, 1f / Mathf.Max(0.2f, atkInterval));
        attr.SetBaseAndCurrent(AttrType.MoveSpeed, GameConfig.BASE_MOVE_SPEED);
        attr.SetBaseAndCurrent(AttrType.AttackRange, atkRange);
        attr.SetBaseAndCurrent(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);

        currentHp = attr.GetAttr(AttrType.MaxHp);
        ApplyMeleeRangeVsPlayer();
        // 【2026-10-22 主人拍板 v4.1】石像赐福等局内 buff（tempBuffs）佣兵也要吃到（与上面花名册分支同口径）。
        ApplyRunTempBuffs();
        ApplyOutStarBonus();
        ApplyGrowBonus();      // 【2026-10-23 施工单 T4】养成资质加成（外层乘区，与升星并联）
        ApplyBondClearBonus(); // 【2026-10-27 羁绊】通关羁绊后全属性 ×1.5（与升星/养成并联）
    }

    // ============================================================
    // 【2026-10-22 施工单 T8】局外升星加成进战斗
    // ============================================================

    /// <summary>本次属性基底是否已挂过局外星加成（SetupAttributes 置 false，ApplyOutStarBonus 置 true，防重复乘区）。</summary>
    bool _outStarApplied;

    /// <summary>
    /// 局外星（outStar）加成：每星 物攻(Attack)/物防(Defense) +8%、魔攻(MagicAttack)/魔防(MagicDefense) +7%
    /// （2026-10-22 三次拍板「先按8%加吧 魔按7%」）。外层乘区（attr.AddAttr isPercent，与局内 tempBuffs
    /// 同一条乘法链），血/移速等不动；不改 MercRosterDefs.ApplyCombatStats 内部。
    /// hireId 未设（Init 时序）或非花名册佣兵 → 跳过且<b>不置位</b>，等 SetHireId 再来；
    /// 0 星佣兵数值与现在完全一致。数值唯一真源 = MercStarDefs。
    /// </summary>
    public void ApplyOutStarBonus()
    {
        if (attr == null || _outStarApplied) return;
        string hid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        int outStar = MercStarSystem.OutStarOf(hid);
        if (outStar < 0) return;   // 解析不到（hireId 未设 / 非花名册）：不置位，等 SetHireId 再补
        _outStarApplied = true;
        if (outStar <= 0) return;

        attr.AddAttr(AttrType.Attack,      MercStarDefs.AtkPctPerStar * outStar, true);
        attr.AddAttr(AttrType.Defense,     MercStarDefs.AtkPctPerStar * outStar, true);
        attr.AddAttr(AttrType.MagicAttack, MercStarDefs.MagicPctPerStar * outStar, true);
        attr.AddAttr(AttrType.MagicDefense, MercStarDefs.MagicPctPerStar * outStar, true);
        Debug.Log($"[Mercenary] 局外星加成生效：{hid} ★{outStar} → 攻/防 +{MercStarDefs.AtkPctPerStar * outStar:0%}，魔攻/魔防 +{MercStarDefs.MagicPctPerStar * outStar:0%}");
    }

    // ============================================================
    // 【2026-10-23 主人拍板 C2=A / 施工单 T4】佣兵升阶（局外养成等级）资质加成进战斗
    // ============================================================

    /// <summary>本次属性基底是否已挂过养成资质加成（与 _outStarApplied 同款幂等闸，互不干扰）。</summary>
    bool _growApplied;

    /// <summary>本次属性基底是否已挂过羁绊通关乘区（与上面同款幂等闸）。</summary>
    bool _bondApplied;

    /// <summary>
    /// 【2026-10-23 主人拍板 C2=A / 施工单 T4】佣兵升阶（局外养成等级）资质加成：
    /// 按 MercGrowSystem 累计资质（升级时掷定即存，本函数只读乘一次）做<b>外层乘区</b>
    /// （attr.AddAttr isPercent，与升星加成并联叠乘、互不干扰）；
    /// 🔴 不改 MercRosterDefs.ApplyCombatStats 内部公式（施工红线）。
    /// 口径：物攻/魔攻 ← aptAtk（魔攻与物攻同口径，Q7 拍板）；物防/魔防 ← aptDef；血 ← aptHp。
    /// 数值为 0 的项不调 AddAttr（省日志噪音）；无养成记录（Lv1）→ 数值与改动前完全一致。
    /// hireId 未设 / 非花名册（Get 返回 null）→ 跳过且<b>不置位</b>，等 SetHireId 再补。
    /// 数值唯一真源 = MercGrowSystem（存档累计值）。
    /// </summary>
    public void ApplyGrowBonus()
    {
        if (attr == null || _growApplied) return;
        string hid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        var entry = MercGrowSystem.Get(hid);
        if (entry == null) return;   // 解析不到 hireId（Init 时序）或无养成记录：不置位，等 SetHireId 再补
        _growApplied = true;

        float atkPct = MercGrowSystem.AptitudePct(hid, 0);
        float defPct = MercGrowSystem.AptitudePct(hid, 1);
        float hpPct  = MercGrowSystem.AptitudePct(hid, 2);
        if (atkPct != 0f)
        {
            attr.AddAttr(AttrType.Attack,      atkPct, true);
            attr.AddAttr(AttrType.MagicAttack, atkPct, true);   // 魔攻与物攻同口径（Q7 拍板）
        }
        if (defPct != 0f)
        {
            attr.AddAttr(AttrType.Defense,      defPct, true);
            attr.AddAttr(AttrType.MagicDefense, defPct, true);
        }
        if (hpPct != 0f)
            attr.AddAttr(AttrType.MaxHp, hpPct, true);
        Debug.Log($"[Mercenary] 养成资质加成生效：{hid} Lv{MercGrowSystem.LevelOf(hid)} → 攻/魔攻 +{atkPct:0%}，防/魔防 +{defPct:0%}，血 +{hpPct:0%}");
    }

    // ============================================================
    // 【2026-10-27 主人拍板】羁绊地图通关：该佣兵全属性永久 ×1.5（BondClearStatMul）
    // ============================================================

    /// <summary>
    /// 【2026-10-27 主人拍板】羁绊地图通关后，该佣兵全属性（血/攻/防/魔攻/魔防/攻速/移速）永久 ×1.5。
    /// 外层乘区（attr.AddAttr isPercent，与升星 / 养成并联叠乘），不改 MercRosterDefs 内部公式（红线）。
    /// 判据唯一出口 = SaveData.IsMercBondCleared（存档 favorBondClearedIds）。
    /// hireId 未设（Init 时序）→ 不置位、不报错，等 SetHireId 再补；未通关羁绊 → 不置位（轻量重查，无副作用）。
    /// 数值唯一真源 = MercFavorDefs.BondClearStatMul。
    /// </summary>
    public void ApplyBondClearBonus()
    {
        if (attr == null || _bondApplied) return;
        string hid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        var data = SaveSystem.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(hid)) return;   // hireId 未设（Init 时序）→ 等 SetHireId 再补
        if (!data.IsMercBondCleared(hid)) return;                 // 未通关羁绊：不置位，下次 SetupAttributes/SetHireId 再查
        _bondApplied = true;
        float mul = MercFavorDefs.BondClearStatMul - 1f;          // 1.5 → +50%
        attr.AddAttr(AttrType.MaxHp,        mul, true);
        attr.AddAttr(AttrType.Attack,       mul, true);
        attr.AddAttr(AttrType.Defense,      mul, true);
        attr.AddAttr(AttrType.MagicAttack,  mul, true);
        attr.AddAttr(AttrType.MagicDefense, mul, true);
        attr.AddAttr(AttrType.AttackSpeed,  mul, true);
        attr.AddAttr(AttrType.MoveSpeed,    mul, true);
        Debug.Log($"[Mercenary] 羁绊属性加成生效：{hid} 全属性 ×{MercFavorDefs.BondClearStatMul}");
    }

    // ============================================================
    // 【2026-10-22 主人拍板 v4.1】石像赐福：佣兵也吃 BattleManager.tempBuffs
    // ============================================================

    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】把 BattleManager.tempBuffs（石像赐福 / 局内强化卡，与主角同款口径：
    /// 读同一份 tempBuffs 列表，逐条 AttrBonusData 叠加）叠进佣兵属性。
    /// 装配时机 = SetupAttributes 基底写完后，所以中途抽奖入队 / 阵亡复活（SpawnMercenary → Init）
    /// 的新佣兵生成时自然带上，无需额外接线。
    /// </summary>
    void ApplyRunTempBuffs()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.tempBuffs == null || bm.tempBuffs.Count == 0 || attr == null) return;
        for (int i = 0; i < bm.tempBuffs.Count; i++)
        {
            var b = bm.tempBuffs[i];
            if (b == null) continue;
            attr.AddAttr(b.attrType, b.value, b.isPercent);
        }
        currentHp = Mathf.Min(currentHp, attr.GetAttr(AttrType.MaxHp));
    }

    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】赐福瞬间对在场佣兵触发的重算入口：
    /// 全量重建基底（SetupAttributes）后由 ApplyRunTempBuffs 统一叠加 tempBuffs —— 幂等，
    /// 不会与既有加成算重；当前血量保持绝对值（与 Hero.RecalcAttr 同口径，只钳上限不回满）。
    /// 死亡/复活不回退：复活链 SpawnMercenary → Init → SetupAttributes 会重新带上全部 tempBuffs。
    /// </summary>
    public void RecalcAttr()
    {
        if (attr == null || string.IsNullOrEmpty(mercId)) return;
        float hpBefore = currentHp;
        SetupAttributes(mercId, Mathf.Max(1, mercLevel));
        currentHp = Mathf.Min(hpBefore, attr.GetAttr(AttrType.MaxHp));
    }

    protected override WeaponAttackType GetAttackType()
        => UsesMagicAttack(mercId) ? WeaponAttackType.Magic : WeaponAttackType.Physical;

    protected override AttackVfxKit GetAttackVfxKit()
    {
        // 2026-10-23 主人拍板：形态 → 特效只在这一处映射，判定走 JobKindOf。
        switch (MercRosterDefs.JobKindOf(mercId))
        {
            case MercRosterDefs.MercJobKind.Bow: return AttackVfxKit.Bow;
            case MercRosterDefs.MercJobKind.Magic: return AttackVfxKit.Orb;
            default: return AttackVfxKit.MeleeSlash;
        }
    }

    /// <summary>
    /// 队形车道（相对玩家 0）。【2026-10-11 主人拍板】原值 -0.35 / +0.35：
    /// 0 号站在最下面那条，被底部 UI（背包 / 技能条）挡住，主人原话「招募到的佣兵位置往上移点 现在在最下面被挡住了」。
    /// 现在整体往上抬到 -0.15 / +0.45（仍在可行走区 -0.809 ~ 0.526 内，超出由 BattleLaneBounds 自己钳）。
    /// 想再调只改这两个数，别在别处另写一套队形。
    /// </summary>
}
