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

    // 基础属性（来自等级+天赋+遗产）
    public int Strength = 0;
    public int Intelligence = 0;
    public int Agility = 0;
    public int Vitality = 0;

    public AttrSystem()
    {
        // 只初始化基础字典，不调用 RecalcAllAttr
        // RecalcAllAttr 会在 Awake/Init 时由外部调用（避免构造期间访问Singleton）
        InitBaseDict();
    }

    /// <summary>
    /// 初始化基础属性字典（不访问任何Singleton，可在构造期间安全调用）
    /// </summary>
    private void InitBaseDict()
    {
        _baseAttr.Clear();
        _baseAttr[AttrType.MaxHp] = GameConfig.BASE_HP;
        _baseAttr[AttrType.Attack] = GameConfig.BASE_ATTACK;
        _baseAttr[AttrType.AttackSpeed] = GameConfig.BASE_ATTACK_SPEED;
        _baseAttr[AttrType.CritRate] = GameConfig.BASE_CRIT_RATE;
        _baseAttr[AttrType.MoveSpeed] = GameConfig.BASE_MOVE_SPEED;
        _baseAttr[AttrType.AttackRange] = GameConfig.BASE_ATTACK_RANGE;
        _baseAttr[AttrType.Defense] = GameConfig.BASE_DEFENSE;

        Strength = GameConfig.BASE_STRENGTH;
        Intelligence = GameConfig.BASE_INTELLIGENCE;
        Agility = GameConfig.BASE_AGILITY;
        Vitality = GameConfig.BASE_VITALITY;

        // 复制基础属性到当前属性（不做派生计算）
        _attr.Clear();
        foreach (var pair in _baseAttr)
            _attr[pair.Key] = pair.Value;
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

        // 2. 四大基础属性加成（来自天赋和遗产）— 仅玩家相关，SaveSystem可能未初始化
        var saveSys = SaveSystem.Instance;
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

        // 力量→物理攻击
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

    public void AddAttr(AttrType type, float value, bool isPercent)
    {
        if (!_attr.ContainsKey(type)) _attr[type] = 0;
        if (isPercent) _attr[type] *= (1 + value);
        else _attr[type] += value;
    }

    public float GetAttr(AttrType type)
    {
        return _attr.ContainsKey(type) ? _attr[type] : 0;
    }

    /// <summary>直接设置属性值（覆盖计算值）</summary>
    public void SetAttr(AttrType type, float value)
    {
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
            case TalentDefs.AttrKind.PhysDamage:
            case TalentDefs.AttrKind.WeaponSwordShield:
            case TalentDefs.AttrKind.WeaponHeavy:
                AddAttr(AttrType.PhyPower, fx.value * 0.01f, true);
                break;
            case TalentDefs.AttrKind.MagicDamage:
            case TalentDefs.AttrKind.WeaponRangedMagic:
                AddAttr(AttrType.MagicPower, fx.value * 0.01f, true);
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
                break;
        }
    }
}