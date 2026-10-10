using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 属性系统：四大基础属性派生战斗属性，所有属性计算统一在这里
/// 力量→物理攻击  智力→魔法攻击  敏捷→攻速+暴击  体质→生命+防御
/// </summary>
public class AttrSystem
{
    private Dictionary<AttrType, float> _attr = new Dictionary<AttrType, float>();
    private Dictionary<AttrType, float> _baseAttr = new Dictionary<AttrType, float>();

    public AttrOwnerKind OwnerKind { get; private set; }

    /// <summary>
    /// 构造期保险：MonoBehaviour 的字段初始化器（如 UnitBase.attr = new AttrSystem()）会在反序列化 .ctor 里执行，
    /// 此期间 Resources.Load 会抛 UnityException（Load is not allowed to be called from a MonoBehaviour constructor）。
    /// 构造期一律只读 GameConfig 常量、不读任何静态表；职业表等 SetOwnerKind（Awake 之后）再写。
    /// </summary>
    private bool _inConstruction;

    // 基础属性（来自等级+天赋+遗产）
    public int Strength = 0;
    public int Intelligence = 0;
    public int Agility = 0;
    public int Vitality = 0;

    public AttrSystem() : this(AttrOwnerKind.Unspecified) { }

    public AttrSystem(AttrOwnerKind ownerKind)
    {
        // 只初始化基础字典，不调用 RecalcAllAttr（避免构造期间访问 Singleton）
        _inConstruction = true;
        OwnerKind = ownerKind;
        InitBaseDict();
        _inConstruction = false;
    }

    /// <summary>单位 Awake 后绑定归属。会按 Owner 重写基底（玩家有职业表则直接写表值）。</summary>
    public void SetOwnerKind(AttrOwnerKind kind)
    {
        if (OwnerKind == kind) return;
        OwnerKind = kind;
        InitBaseDict();
    }

    bool UsesPlayerJobTable => !_inConstruction
        && OwnerKind == AttrOwnerKind.Player
        && PlayerJobBaseStats.HasData;

    /// <summary>
    /// 初始化基础属性字典。玩家+职业表：直接写表，不先写 GameConfig.BASE_HP/ATK 再覆盖。
    /// 佣兵/怪物：仍用 GameConfig 基底，再由各自 Init 覆盖。
    /// </summary>
    private void InitBaseDict()
    {
        _baseAttr.Clear();

        Strength = GameConfig.BASE_STRENGTH;
        Intelligence = GameConfig.BASE_INTELLIGENCE;
        Agility = GameConfig.BASE_AGILITY;
        Vitality = GameConfig.BASE_VITALITY;

        bool wroteJob = UsesPlayerJobTable
            && PlayerJobBaseStats.TryWriteCombatBases(this, PlayerJobDefs.GetSelected());
        // 注意：这里不要再写裸的 PlayerJobBaseStats.HasData 之类的表读取——
        // 构造期（_inConstruction）读表会触发 Resources.Load，Unity 会直接抛 UnityException。
        if (!wroteJob)
            WriteGameConfigCombatBases();

        // 2026-09-27：走职业表那条路径目前只写物理防御 → 这里补上魔法防御的**基础真源**，
        // 保证 AttrType.MagicDefense 这个键一定存在（伤害公式取不到键会报 DF-003 错误码）。
        // 这是「属性初始化」不是「兜底换算」：值就是 BASE_MAGIC_DEFENSE，不从物防推导。
        if (!_baseAttr.ContainsKey(AttrType.MagicDefense))
            _baseAttr[AttrType.MagicDefense] = GameConfig.BASE_MAGIC_DEFENSE;

        _attr.Clear();
        foreach (var pair in _baseAttr)
            _attr[pair.Key] = pair.Value;
    }

    void WriteGameConfigCombatBases()
    {
        _baseAttr[AttrType.MaxHp] = GameConfig.BASE_HP;
        _baseAttr[AttrType.Attack] = GameConfig.BASE_ATTACK;
        _baseAttr[AttrType.AttackSpeed] = GameConfig.BASE_ATTACK_SPEED;
        _baseAttr[AttrType.CritRate] = GameConfig.BASE_CRIT_RATE;
        _baseAttr[AttrType.CritDamage] = GameConfig.DefaultCritMultiplier;
        _baseAttr[AttrType.MoveSpeed] = GameConfig.BASE_MOVE_SPEED;
        _baseAttr[AttrType.AttackRange] = GameConfig.BASE_ATTACK_RANGE;
        _baseAttr[AttrType.Defense] = GameConfig.BASE_DEFENSE;
        // 2026-09-27 主人拍板：防御拆成物理 / 魔法两条，魔防是独立属性（不再由物防换算）。
        // 这里写入的是**基础真源**，装备的魔法防御会往上叠。
        _baseAttr[AttrType.MagicDefense] = GameConfig.BASE_MAGIC_DEFENSE;
    }

    public void ResetToBase()
    {
        InitBaseDict();
        // RecalcAllAttr 由外部在合适的时机调用（Awake/Init）
    }

    /// <summary>
    /// 重新计算所有属性（基础属性 + 天赋 + 传说武器 + 装备 + 额外加成）
    /// 注意：此方法会访问 SaveSystem/ConfigManager，只能在 Awake/Init 之后调用
    /// </summary>
    public void RecalcAllAttr(List<AttrBonusData> extraBonus = null)
    {
        _attr.Clear();

        // 1. 基础属性
        foreach (var pair in _baseAttr)
            _attr[pair.Key] = pair.Value;

        // 1b. 仅 Player 套职业表（佣兵/怪物走各自 Init，勿盖成玩家 ATK）
        ApplyPlayerJobBaseIfAny();

        // 2. 四大基础属性加成（来自天赋和遗产）— 仅 Player；佣兵/怪物走各自 Init，勿叠玩家存档
        var saveSys = OwnerKind == AttrOwnerKind.Player ? SaveSystem.Instance : null;
        if (saveSys != null && saveSys.Data != null)
        {
            _attr[AttrType.Strength] = Strength + saveSys.Data.playerStrength;
            _attr[AttrType.Intelligence] = Intelligence + saveSys.Data.playerIntelligence;
            _attr[AttrType.Agility] = Agility + saveSys.Data.playerAgility;
            _attr[AttrType.Vitality] = Vitality + saveSys.Data.playerVitality;

            // 3. 天赋属性加成（TalentDefs 为真源；旧 TalentConfig SO 仅作兼容兜底）
            if (saveSys.Data.talents != null)
            {
                ApplyTalentDefsBonuses(saveSys.Data.talents);
                var cfgMgr = ConfigManager.Instance;
                if (cfgMgr != null)
                {
                    foreach (var talentPair in saveSys.Data.talents)
                    {
                        // L/C/R 已由 TalentDefs 处理，跳过；其余旧 id 仍读 SO
                        string tid = talentPair.Key;
                        if (string.IsNullOrEmpty(tid)) continue;
                        if (tid[0] == 'L' || tid[0] == 'C' || tid[0] == 'R') continue;
                        TalentConfig talent = cfgMgr.GetTalent(tid);
                        if (talent == null) continue;
                        AddAttr(talent.attrType, talent.valuePerLevel * talentPair.Value, false);
                    }
                }
            }

            // 4. 传说武器全局加成
            if (saveSys.Data.unlockedLegendaryWeapons != null)
            {
                var cfgMgr = ConfigManager.Instance;
                if (cfgMgr != null)
                {
                    foreach (string weaponId in saveSys.Data.unlockedLegendaryWeapons)
                    {
                        EquipTemplate weapon = cfgMgr.GetEquipTemplate(weaponId);
                        if (weapon == null || weapon.globalBonus == null) continue;
                        AddAttr(weapon.globalBonus.attrType, weapon.globalBonus.value, weapon.globalBonus.isPercent);
                    }
                }
            }
        }
        else
        {
            // SaveSystem未就绪（怪物等非玩家单位），使用基础值
            _attr[AttrType.Strength] = Strength;
            _attr[AttrType.Intelligence] = Intelligence;
            _attr[AttrType.Agility] = Agility;
            _attr[AttrType.Vitality] = Vitality;
        }

        // 5. 装备属性加成
        if (extraBonus != null)
        {
            foreach (var bonus in extraBonus)
                AddAttr(bonus.attrType, bonus.value, bonus.isPercent);
        }

        // 6. 四大基础属性 → 派生战斗属性
        ApplyDerivedAttributes();

        // 7. 限制值
        _attr[AttrType.CritRate] = Mathf.Clamp01(_attr[AttrType.CritRate]);
        if (_attr.ContainsKey(AttrType.CritDamage))
            _attr[AttrType.CritDamage] = Mathf.Max(1f, _attr[AttrType.CritDamage]);
        _attr[AttrType.AttackSpeed] = Mathf.Max(0.2f, _attr[AttrType.AttackSpeed]);
        _attr[AttrType.Dodge] = Mathf.Clamp01(_attr.ContainsKey(AttrType.Dodge) ? _attr[AttrType.Dodge] : 0);
    }

    /// <summary>
    /// 四大基础属性派生战斗属性
    /// 力量→物理攻击(每点+2)  智力→魔法攻击(每点+2)  敏捷→攻速(每点+1%)+暴击(每点+0.5%)
    /// 体质→生命(每点+10)+防御(每点+1)
    /// </summary>
    private void ApplyDerivedAttributes()
    {
        float str = GetRawAttr(AttrType.Strength);
        float intel = GetRawAttr(AttrType.Intelligence);
        float agi = GetRawAttr(AttrType.Agility);
        float vit = GetRawAttr(AttrType.Vitality);

        // 力量→物理攻击。玩家职业表已含 ATK，不再叠力量×2（否则开局总攻虚高）
        if (!UsesPlayerJobTable)
            AddAttr(AttrType.Attack, str * 2f, false);

        // 2026-10-10 主人拍板：PhyPower / MagicPower 的语义是「**加成量**」（0 = 无加成），**不是倍率**。
        // DamageFormula.BuildSkillBase 用的是 baseDamage + attack * mul * (1f + phy + mag)，
        // 所以这两条必须**加算**（isPercent = false）从 0 往上叠，不能乘算。
        // 原写法 isPercent = true 走 _attr[type] *= (1 + value)，而基础值恒为 0 → 0 × 1.85 = 0，整条派生是死代码。
        // 🔴 也不要给 MagicPower 补基础值 1f：那会变成 (1 + 1 + 0.85) 伤害直接翻倍。
        // 🔴 本次只分流这两条：其余派生（体质→MaxHp/Defense、敏捷→攻速/暴击、力量→Attack）是既有行为，
        //    怪物一直在吃，不改、也不动系数（动系数会全局影响怪物血量 / 防御）。
        if (ShouldApplyPowerDerived())
        {
            AddAttr(AttrType.PhyPower, str * 0.01f, false);

            // 智力→魔法强度（+85 智力 → MagicPower = 0.85 → 魔法技能伤害 ×1.85）
            AddAttr(AttrType.MagicPower, intel * 0.01f, false);
        }

        // 敏捷→攻速+暴击
        AddAttr(AttrType.AttackSpeed, agi * 0.01f, true);
        AddAttr(AttrType.CritRate, agi * 0.005f, true);

        // 体质→生命+防御
        AddAttr(AttrType.MaxHp, vit * 10f, false);
        AddAttr(AttrType.Defense, vit * 1f, false);
    }

    /// <summary>
    /// 2026-10-10 主人拍板：PhyPower / MagicPower 派生按 OwnerKind 分流。
    /// RecalcAllAttr / ApplyDerivedAttributes 是玩家 / 佣兵 / 怪物**三方共用**，
    /// 而怪物和佣兵的 Strength / Intelligence 基底都是 GameConfig.BASE_STRENGTH = BASE_INTELLIGENCE = 5，
    /// 不加闸的话派生复活后它们的技能伤害会被隐性调高 ×(1 + 0.05 + 0.05) = ×1.10。
    /// 天赋（左列）和装备词缀目前都只有玩家有，所以**默认只给 Player**。
    /// 若要让佣兵也吃，在这里加 <c>|| OwnerKind == AttrOwnerKind.Merc</c>。
    /// </summary>
    private bool ShouldApplyPowerDerived()
        => OwnerKind == AttrOwnerKind.Player;

    void ApplyPlayerJobBaseIfAny()
    {
        if (OwnerKind != AttrOwnerKind.Player) return;
        PlayerJobBaseStats.ApplyToAttr(this, PlayerJobDefs.GetSelected());
    }

    public void AddAttr(AttrType type, float value, bool isPercent)
    {
        if (!_attr.ContainsKey(type)) _attr[type] = 0;
        if (isPercent) _attr[type] *= (1 + value);
        else _attr[type] += value;
    }

    public float GetAttr(AttrType type)
    {
        float v = _attr.ContainsKey(type) ? _attr[type] : 0;
        return ApplyTimedBuff(type, v);
    }

    /// <summary>
    /// 属性表里**有没有登记**这个属性（区分「没登记」和「登记了但值是 0」）。
    /// 2026-09-27：物理/魔法防御拆开后，伤害公式靠它判断该报错还是正常按 0 结算。
    /// </summary>
    public bool HasAttr(AttrType type) => _attr.ContainsKey(type);

    // ============================================================
    // 定时增益层（2026-09-15）
    // 技能 Buff（攻击 +50% / 防御 +35% / 暴击 +25%）原来走 AddAttr —— 那是永久写值，
    // 技能表里的 duration 从来没被消费，导致放一次永久生效。能量制下节奏慢不明显，
    // 改纯冷却制后 18 秒一次永久 +50% 攻击，3 分钟能叠 10 层。
    //
    // 这里改为「独立于 _attr 的计时倍率」：
    //   - 到期自动失效，duration 真正生效
    //   - 同类 Buff 后放的直接覆盖先放的，永不叠加
    //   - 不参与 RecalcAttr，换装/升级重算基底不会把 Buff 弄丢或算重
    //   - 不碰任何既有消费点，GetAttr 读到的就是已加成的值
    // ============================================================

    float _buffAtkMul = 1f, _buffAtkUntil = -1f;
    float _buffDefMul = 1f, _buffDefUntil = -1f;
    float _buffCritMul = 1f, _buffCritUntil = -1f;

    /// <summary>施加一个定时增益。同类型已有生效中的会被覆盖（不叠加）。</summary>
    public void ApplyTimedBuff(AttrType type, float value, bool isPercent, float duration)
    {
        if (duration <= 0f) return;
        float until = Time.time + duration;
        // 与 AddAttr 同语义：isPercent = 乘 (1+value)，否则加 value。
        // 但攻击/防御/暴击在表里都是百分比档，统一按乘算存。
        float mul = isPercent ? 1f + value : value;
        if (mul <= 0f) mul = 1f;

        switch (type)
        {
            case AttrType.Attack:   _buffAtkMul = mul;  _buffAtkUntil = until; break;
            case AttrType.Defense:  _buffDefMul = mul;  _buffDefUntil = until; break;
            case AttrType.CritRate: _buffCritMul = mul; _buffCritUntil = until; break;
            default: return;   // 其它属性仍走调用方自己的处理（如攻速走 SkillCastService 计时）
        }
    }

    /// <summary>清掉所有定时增益（换关/撤离/死亡时用，避免跨关残留）。</summary>
    public void ClearTimedBuffs()
    {
        _buffAtkUntil = -1f;
        _buffDefUntil = -1f;
        _buffCritUntil = -1f;
    }

    float ApplyTimedBuff(AttrType type, float raw)
    {
        switch (type)
        {
            case AttrType.Attack:   return Time.time < _buffAtkUntil ? raw * _buffAtkMul : raw;
            case AttrType.Defense:  return Time.time < _buffDefUntil ? raw * _buffDefMul : raw;
            case AttrType.CritRate: return Time.time < _buffCritUntil ? raw * _buffCritMul : raw;
            default: return raw;
        }
    }

    /// <summary>直接设置属性值（覆盖计算值，不改基底；主角 RecalcAttr 后的射程/攻速覆盖走这里）。</summary>
    public void SetAttr(AttrType type, float value)
    {
        _attr[type] = value;
    }

    /// <summary>佣兵花名册等：当前值与基底一起写，避免之后 RecalcAllAttr 回到 GameConfig.BASE_ATTACK。</summary>
    public void SetBaseAndCurrent(AttrType type, float value)
    {
        _baseAttr[type] = value;
        _attr[type] = value;
    }

    private float GetRawAttr(AttrType type)
    {
        return _attr.ContainsKey(type) ? _attr[type] : 0;
    }

    /// <summary>按存档天赋键应用 TalentDefs 效果（战斗属性）。</summary>
    void ApplyTalentDefsBonuses(System.Collections.Generic.Dictionary<string, int> talents)
    {
        if (talents == null) return;
        foreach (var pair in talents)
        {
            string key = pair.Key;
            int val = pair.Value;
            if (string.IsNullOrEmpty(key) || val <= 0) continue;

            // 左列 L 节点
            if (key.Length > 1 && key[0] == 'L' && int.TryParse(key.Substring(1), out int li))
            {
                var node = TalentDefs.GetLeft(li);
                if (node != null) ApplyTalentEffect(node.effect);
                continue;
            }
            // 右列重制节点（id 形如 R_F1 / R_C1 / R_JOB ...）
            var rnode = TalentDefs.GetRightNodeById(key);
            if (rnode != null)
                ApplyRightNodeEffect(rnode, val, TalentDefs.GetRightNodeChosenJob(talents, key));
        }
    }

    /// <summary>按右列节点当前等级应用效果；JobChoice 用已选职业选项。</summary>
    void ApplyRightNodeEffect(TalentDefs.TalentRightNode node, int level, int chosenJob)
    {
        if (node == null || level <= 0) return;
        var opt = node.IsJobChoice ? node.ChosenOption(chosenJob)
                                   : (node.options != null && node.options.Length > 0 ? node.options[0] : null);
        if (opt == null) return;
        // 复用左列已有的 Effect→Attr 映射（ApplyTalentEffect 已处理 %/绝对值）。
        // 2026-10-10：右列的物攻 / 魔攻区分由 PhysDamage / MagicDamage 两个**专精**节点承担
        //（玩家进局后主动选物理还是魔法，走 PhyPower / MagicPower，跨系按 CrossPathRatio 打折），
        // 与 AttrKind.Attack（固定值攻击）无关 —— Attack 左右列同口径，都是物攻 / 魔攻两侧都加。
        ApplyTalentEffect(new TalentDefs.Effect { kind = opt.kind, value = node.EffectValue(level, chosenJob) });
    }

    void ApplyTalentEffect(TalentDefs.Effect fx)
    {
        if (fx == null) return;
        switch (fx.kind)
        {
            case TalentDefs.AttrKind.Attack:
                // 2026-10-10：与 Defense 同口径 —— 攻击是**跨局账号成长**，物攻 / 魔攻**两侧都加**，
                // 玩家随时换职业不吃亏（左列跨局、右列局内都一样，左右列同口径）。
                // ⚠️ 不要把「右列保留区分度」理解成这里要切单加：右列的区分度在 PhysDamage / MagicDamage 专精节点上。
                AddAttr(AttrType.Attack, fx.value, false);
                AddAttr(AttrType.MagicAttack, fx.value, false);
                break;
            case TalentDefs.AttrKind.Intelligence:
                // 2026-10-10：左列第 4 槽已改走 AttrKind.Attack，此分支暂无天赋节点使用；保留以防将来配表/节点复用。注意这里加的是基础属性 Intelligence，会走 ApplyDerivedAttributes 派生成 MagicPower，也会喂蓝条。
                // 2026-10-10 主人拍板：口径唯一 = 只喂**基础属性 Intelligence**，系数 1.0。
                // 原写法直接加 MagicAttack，问题有两个：
                //   ① 蓝条现在能吃到 —— BattleManager.PlayerMpPool / PlayerMpRegen 是「曲线 + 攻击项 + 智力项」双叠加
                //      （2026-10-10 追加澄清：池 = MpProfile.Pool + 攻击 × MP_POOL_PER_ATTACK + 智力 × MP_POOL_PER_INTELLIGENCE），
                //      这里的 Intelligence 会走智力项喂蓝条，装备的智力词条照样生效；
                //      天赋左列不再喂智力只是简化天赋侧，不等于砍掉智力的蓝条收益。
                //   ② T1 把派生修好之后，法师的智力会被「派生 MagicPower」和「直接加 MagicAttack」算两遍。
                // 现在只加 Intelligence：魔法强度走 ApplyDerivedAttributes 派生的 MagicPower，
                // 点满（10 节点共 +85）→ 魔法强度 +85%。蓝条则是「攻击项 + 智力项」双叠加，这里的智力照旧进蓝池 / 回蓝。
                AddAttr(AttrType.Intelligence, fx.value, false);
                break;
            case TalentDefs.AttrKind.Hp:
                AddAttr(AttrType.MaxHp, fx.value, false);
                break;
            case TalentDefs.AttrKind.Defense:
                // 2026-09-29：防御已拆成物理 / 魔法两条，天赋**两侧都要加** ——
                // 之前只加 Defense，玩家点满防御天赋后魔法防御一点没涨，进法师章直接被打穿。
                AddAttr(AttrType.Defense, fx.value, false);
                AddAttr(AttrType.MagicDefense, fx.value, false);
                break;
            case TalentDefs.AttrKind.CritRate:
                // TalentDefs 用百分点（0.5 = +0.5%）
                AddAttr(AttrType.CritRate, fx.value * 0.01f, false);
                break;
            case TalentDefs.AttrKind.AtkSpeed:
                AddAttr(AttrType.AttackSpeed, fx.value * 0.01f, true);
                break;
            case TalentDefs.AttrKind.CritDamage:
                AddAttr(AttrType.CritDamage, fx.value * 0.01f, false);
                break;
            case TalentDefs.AttrKind.PhysDamage:
            case TalentDefs.AttrKind.MagicDamage:
                // 2026-09-29 主人拍板：**流派要保留区分度**（给玩家一个培养目标，鼓励专一养物理或法系），
                // 但跨系**不是不能玩、只是要花代价** → 本系满额、跨系按 CrossPathRatio 打折。
                // 例：法师点「物理专精 +25%」实际只拿到 +12.5%，能玩但明显亏。
                {
                    bool wantPhys = fx.kind == TalentDefs.AttrKind.PhysDamage;
                    bool crossPath = wantPhys == IsMagicJobNow();
                    float ratio = crossPath ? GameConfig.TALENT_CROSS_PATH_RATIO : 1f;
                    // 2026-10-10：isPercent true → false。PhyPower / MagicPower 是「加成量」（基础值恒为 0、从不登记），
                    // 乘算走 _attr[type] *= (1 + value) → 0 × 1.6 = 0，右列「物理 / 魔法专精」满级 60% 点了完全没效果。
                    // 改加算后与 ApplyDerivedAttributes 的派生口径一致，真正生效。
                    AddAttr(wantPhys ? AttrType.PhyPower : AttrType.MagicPower,
                            fx.value * 0.01f * ratio, false);
                }
                break;
            case TalentDefs.AttrKind.EliteDamage:
                // 精英猎手：对精英 / Boss 的伤害加成倍率（0.15 = +15%）。
                // 2026-09-29：这里**必须用绝对值**，不能 isPercent —— EliteDamage 没有基础值（InitBaseDict
                // 与 PlayerJobBaseStats 都没写），百分比加会变成 0 * 1.15 = 0，点了等于没点。
                // 消费点 Monster.TakeDamage：inDamage *= (1f + bonus)。
                AddAttr(AttrType.EliteDamage, fx.value * 0.01f, false);
                break;
            case TalentDefs.AttrKind.WeaponSwordShield:
            case TalentDefs.AttrKind.WeaponHeavy:
            case TalentDefs.AttrKind.WeaponRangedMagic:
                // 武器专精：按<b>当前职业</b>落到对应的 Power。
                // 旧代码无条件把剑盾/重装→PhyPower、远程法系→MagicPower，
                // 于是法师点「剑盾伤害 +5%」拿到的是自己根本不用的 PhyPower —— 点了等于没点。
                // 2026-10-10：isPercent true → false —— 同 PhyPower / MagicPower 的加算口径（满级 20% 原先全废）。
                AddAttr(PrimaryPowerAttr(), fx.value * 0.01f, false);
                break;
            case TalentDefs.AttrKind.SkillCooldown:
                AddAttr(AttrType.CooldownReduce, fx.value * 0.01f, false);
                break;
            case TalentDefs.AttrKind.SkillDamage:
                // 2026-09-29：与力量/智力同口径 —— 跨局成长两侧都给，换职业不吃亏。
                AddAttr(AttrType.Attack, fx.value * 0.01f, true);
                AddAttr(AttrType.MagicAttack, fx.value * 0.01f, true);
                break;
            case TalentDefs.AttrKind.MoveSpeed:
                // 移速 +3% = 基础移速的 3%（与攻速/暴击一致按百分比加成）
                AddAttr(AttrType.MoveSpeed, fx.value * 0.01f, true);
                break;
            case TalentDefs.AttrKind.GoldDrop:
                AddAttr(AttrType.GoldBonus, fx.value * 0.01f, true);
                break;
            default:
                // 吞掉的类型：体力恢复 / 商店折扣 / 材料掉落 / 天赋石掉落 / 撤离保留金币。
                // 这些系统目前没有消费点，强行接线会牵动体力、商店、掉落、撤离结算四套逻辑。
                // 处理方式：把这些天赋节点的效果在 TalentDefs 里换成「立刻生效的属性」，
                // 文案同步改对 —— 玩家点任何天赋都应该看得到变化。
                break;
        }
    }

    /// <summary>当前职业的主要伤害属性：法系走 MagicPower，其余走 PhyPower。</summary>
    static AttrType PrimaryPowerAttr()
        => IsMagicJobNow() ? AttrType.MagicPower : AttrType.PhyPower;

    /// <summary>当前职业是否法系（法师/牧师）。与 <see cref="PrimaryPowerAttr"/> 同一口径，不读表。</summary>
    static bool IsMagicJobNow()
    {
        var job = PlayerJobDefs.GetSelected();
        return job == PlayerJobId.Mage || job == PlayerJobId.Priest;
    }

}