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
        AddAttr(AttrType.PhyPower, str * 0.01f, true);

        // 智力→魔法攻击
        AddAttr(AttrType.MagicPower, intel * 0.01f, true);

        // 敏捷→攻速+暴击
        AddAttr(AttrType.AttackSpeed, agi * 0.01f, true);
        AddAttr(AttrType.CritRate, agi * 0.005f, true);

        // 体质→生命+防御
        AddAttr(AttrType.MaxHp, vit * 10f, false);
        AddAttr(AttrType.Defense, vit * 1f, false);
    }

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

            TalentDefs.Effect fx = null;
            if (key.Length > 1 && key[0] == 'L' && int.TryParse(key.Substring(1), out int li))
            {
                var node = TalentDefs.GetLeft(li);
                if (node != null) fx = node.effect;
            }
            else if (key == "C1")
            {
                var node = TalentDefs.RightExtra;
                if (node?.options != null && val >= 1 && val <= node.options.Length)
                    fx = node.options[val - 1].effect;
            }
            else if (key.Length > 1 && key[0] == 'R' && int.TryParse(key.Substring(1), out int ri))
            {
                var node = TalentDefs.GetRight(ri);
                if (node?.options != null && val >= 1 && val <= node.options.Length)
                    fx = node.options[val - 1].effect;
            }

            if (fx != null)
                ApplyTalentEffect(fx);
        }
    }

    void ApplyTalentEffect(TalentDefs.Effect fx)
    {
        if (fx == null) return;
        switch (fx.kind)
        {
            case TalentDefs.AttrKind.Attack:
                AddAttr(AttrType.Attack, fx.value, false);
                break;
            case TalentDefs.AttrKind.Hp:
                AddAttr(AttrType.MaxHp, fx.value, false);
                break;
            case TalentDefs.AttrKind.Defense:
                AddAttr(AttrType.Defense, fx.value, false);
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
                AddAttr(AttrType.PhyPower, fx.value * 0.01f, true);
                break;
            case TalentDefs.AttrKind.MagicDamage:
                AddAttr(AttrType.MagicPower, fx.value * 0.01f, true);
                break;
            case TalentDefs.AttrKind.WeaponSwordShield:
            case TalentDefs.AttrKind.WeaponHeavy:
            case TalentDefs.AttrKind.WeaponRangedMagic:
                // 武器专精：按<b>当前职业</b>落到对应的 Power。
                // 旧代码无条件把剑盾/重装→PhyPower、远程法系→MagicPower，
                // 于是法师点「剑盾伤害 +5%」拿到的是自己根本不用的 PhyPower —— 点了等于没点。
                AddAttr(PrimaryPowerAttr(), fx.value * 0.01f, true);
                break;
            case TalentDefs.AttrKind.SkillCooldown:
                AddAttr(AttrType.CooldownReduce, fx.value * 0.01f, false);
                break;
            case TalentDefs.AttrKind.SkillDamage:
                AddAttr(AttrType.Attack, fx.value * 0.01f, true);
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
    {
        var job = PlayerJobDefs.GetSelected();
        bool magic = job == PlayerJobId.Mage || job == PlayerJobId.Priest;
        return magic ? AttrType.MagicPower : AttrType.PhyPower;
    }
}