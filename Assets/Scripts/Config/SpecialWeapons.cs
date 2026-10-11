using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特殊/剧情武器。暮火之杖：新手过渡，第一章植物系克制。
/// </summary>
public static class SpecialWeapons
{
    public const string TwilightStaffId = "weapon_twilight_staff";
    /// <summary>模板纯名（不带稀有度前缀）。只用于给模板 / 贴图对齐用，**不要拿它当装备显示名**。</summary>
    public const string DisplayName = "暮火之杖";
    public const string GrantKey = "special:twilight_staff";

    /// <summary>
    /// 2026-10-11 主人拍板：「以后都统一这种命名规则，不管是什么」——
    /// 剧情 / 特殊武器也一律 = 稀有度前缀 + 名字，暮火之杖不再例外。
    /// 实例显示名走这里（<see cref="WeaponAffixSystem.RarityPrefix"/> 是全项目唯一出口），
    /// 别再直接拿 <see cref="DisplayName"/>，否则背包里只有它一件没前缀、看着像低一档。
    /// </summary>
    public static string InstanceName => WeaponAffixSystem.RarityPrefix(Rarity.Rare) + DisplayName;

    /// <summary>第一章全体怪物伤害倍率。</summary>
    public const float Chapter1DamageMul = 1.4f;
    /// <summary>第一章 Boss 额外倍率（叠乘）。</summary>
    public const float Chapter1BossExtraMul = 1.2f;
    /// <summary>命中额外火伤。</summary>
    public const float FlatFireOnHit = 8f;

    static EquipTemplate _twilightTpl;

    public static bool IsTwilightStaff(EquipInstance inst)
    {
        return inst != null && inst.templateId == TwilightStaffId;
    }

    public static bool IsTwilightStaffEquipped()
    {
        var bag = GridBackpackSystem.Instance;
        if (bag == null) return false;
        foreach (var item in bag.GetEquippedItems())
        {
            if (item != null && item.slotType == EquipSlotType.MainHand && IsTwilightStaff(item))
                return true;
        }
        return false;
    }

    public static EquipTemplate EnsureTwilightTemplate()
    {
        if (_twilightTpl != null) return _twilightTpl;

        _twilightTpl = Resources.Load<EquipTemplate>(ContentPaths.Config.Equips + "/" + TwilightStaffId);
        if (_twilightTpl == null && ConfigManager.Instance != null)
            _twilightTpl = ConfigManager.Instance.GetEquipTemplate(TwilightStaffId);

        if (_twilightTpl == null)
        {
            _twilightTpl = ScriptableObject.CreateInstance<EquipTemplate>();
            _twilightTpl.templateId = TwilightStaffId;
            _twilightTpl.equipName = DisplayName;
            // 2026-09-27 主人要求「真源对齐」：暮火之杖是**法杖**，图标/外观必须指 5_Wand 的法杖图。
            // 旧值 New_Weapon_06 是 0_Sword 的剑图 —— 名字叫杖、身上拿剑，只能靠 PlayerJobDefs 的
            // MainSpumOverride="Ward_1" 打补丁盖住。这里与 weapon_twilight_staff.asset 必须同值（单入口）。
            _twilightTpl.iconFileName = "New_Weapon_03";
            _twilightTpl.gridWidth = 1;
            _twilightTpl.gridHeight = 2;
            _twilightTpl.baseRarity = Rarity.Rare;
            _twilightTpl.minLevel = 1;
            _twilightTpl.slotType = EquipSlotType.MainHand;
            _twilightTpl.weaponType = WeaponType.OneHand;
            _twilightTpl.weaponAttackType = WeaponAttackType.Magic;
            _twilightTpl.attackRange = GameConfig.RANGE_PX_STAFF;
            _twilightTpl.weaponKindOverride = (int)WeaponCombatTable.WeaponKind.Staff;
            _twilightTpl.spumName = "New_Weapon_03";
            _twilightTpl.baseAttr = new List<AttrBonusData>
            {
                // 2026-09-29：暮火之杖是魔法武器（weaponAttackType=Magic），21 点基础攻击
                // 之前挂在 Attack(物攻) 上 → 法师拿法杖一点魔攻都不涨。改挂 MagicAttack。
                new AttrBonusData { attrType = AttrType.MagicAttack, value = 21f, isPercent = false },
                // 2026-10-10：isPercent true → false。MagicPower 是「加成量」（基础值恒为 0、乘算恒等于 0），
                // 暮火之杖这条 +12% 魔法强度原来完全无效。改加算后与派生口径一致。
                new AttrBonusData { attrType = AttrType.MagicPower, value = 0.12f, isPercent = false },
                new AttrBonusData { attrType = AttrType.FireDamage, value = 8f, isPercent = false },
                new AttrBonusData { attrType = AttrType.AttackSpeed, value = 0.2f, isPercent = true },
            };
            _twilightTpl.ResolveIcon();
            ConfigManager.Instance?.RegisterRuntimeEquip(_twilightTpl);
        }
        else
        {
            if (_twilightTpl.weaponKindOverride < 0)
                _twilightTpl.weaponKindOverride = (int)WeaponCombatTable.WeaponKind.Staff;
            _twilightTpl.ResolveIcon();
            ConfigManager.Instance?.RegisterRuntimeEquip(_twilightTpl);
        }

        return _twilightTpl;
    }

    public static EquipInstance CreateTwilightStaff(int heroLevel = 1)
    {
        var tpl = EnsureTwilightTemplate();
        var inst = EquipInstance.GenerateFromTemplate(tpl, 0, Mathf.Max(1, heroLevel));
        inst.rarity = Rarity.Rare;
        inst.equipName = InstanceName;
        inst.templateId = TwilightStaffId;
        return inst;
    }

    /// <summary>教程结束 / 里程 Lv2：各路径可调，内部防重发。</summary>
    public static bool TryGrantTwilightStaff(bool showToast = true)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        data.logMileageGrantedKeys ??= new HashSet<string>();
        if (data.logMileageGrantedKeys.Contains(GrantKey)) return false;

        if (GridBackpackSystem.Instance == null)
        {
            Debug.LogWarning("[SpecialWeapons] 背包未就绪，暂缓发放暮火之杖");
            return false;
        }

        int lv = Hero.Instance != null ? Hero.Instance.level : 1;
        var inst = CreateTwilightStaff(lv);
        // 2026-09-15：装备不再进背包，直接穿上（换下来的自动分解）
        if (!GridBackpackSystem.Instance.TryEquipDirect(inst))
        {
            // 2026-10-11：Toast 里的名字跟装备实际显示名保持一致（带「稀有·」前缀），
            // 写死「暮火之杖」会出现「背包里叫稀有·暮火之杖、Toast 里叫暮火之杖」的错位。
            if (showToast)
                UIManager.Instance?.ShowToast($"无法装备「{inst.equipName}」");
            return false;
        }

        data.logMileageGrantedKeys.Add(GrantKey);
        if (showToast)
            UIManager.Instance?.ShowToast($"获得过渡武器「{inst.equipName}」：第一章森林怪伤害提升");

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return true;
    }

    /// <summary>对目标的伤害倍率（仅主角装备暮火时）。</summary>
    public static float GetDamageMultiplier(UnitBase target)
    {
        if (!IsTwilightStaffEquipped()) return 1f;
        int ch = BattleManager.Instance != null ? BattleManager.Instance.CurrentChapter : 1;
        if (ch != 1) return 1f;

        float mul = Chapter1DamageMul;
        var mon = target as Monster;
        if (mon != null && mon.IsBossUnit)
            mul *= Chapter1BossExtraMul;
        return mul;
    }

    public static float GetFlatFireBonus()
    {
        return IsTwilightStaffEquipped() ? FlatFireOnHit : 0f;
    }

    // 2026-10-11 主人拍板：删除击杀 Toast「暮火正旺！」——玩家看不懂（「暮火」是暮光之杖的设定词，
    // 界面上没有任何地方告诉玩家这把杖叫暮火），击杀弹这句只会让人以为是 bug。
    // 原 TryFlavorToastOnKill() 与调用点（BattleManager.cs 击杀处理）已整段删除，不留开关。
}
