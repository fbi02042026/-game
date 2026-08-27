using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特殊/剧情武器。暮火之杖：新手过渡，第一章植物系克制。
/// </summary>
public static class SpecialWeapons
{
    public const string TwilightStaffId = "weapon_twilight_staff";
    public const string DisplayName = "暮火之杖";
    public const string GrantKey = "special:twilight_staff";

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
            _twilightTpl.iconFileName = "New_Weapon_06";
            _twilightTpl.gridWidth = 1;
            _twilightTpl.gridHeight = 2;
            _twilightTpl.baseRarity = Rarity.Rare;
            _twilightTpl.minLevel = 1;
            _twilightTpl.slotType = EquipSlotType.MainHand;
            _twilightTpl.weaponType = WeaponType.OneHand;
            _twilightTpl.weaponAttackType = WeaponAttackType.Magic;
            _twilightTpl.attackRange = GameConfig.RANGE_PX_STAFF;
            _twilightTpl.weaponKindOverride = (int)WeaponCombatTable.WeaponKind.Staff;
            _twilightTpl.spumName = "New_Weapon_06";
            _twilightTpl.baseAttr = new List<AttrBonusData>
            {
                new AttrBonusData { attrType = AttrType.Attack, value = 21f, isPercent = false },
                new AttrBonusData { attrType = AttrType.MagicPower, value = 0.12f, isPercent = true },
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
        inst.equipName = DisplayName;
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
        if (!GridBackpackSystem.Instance.TryAddItem(inst, out _))
        {
            if (showToast)
                UIManager.Instance?.ShowToast("背包已满，清理后再领「暮火之杖」");
            return false;
        }

        data.logMileageGrantedKeys.Add(GrantKey);
        if (showToast)
            UIManager.Instance?.ShowToast("获得过渡武器「暮火之杖」：第一章森林怪伤害提升");

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

    static float _lastFlavorToastTime = -99f;

    /// <summary>第一章装备暮火击杀：偶尔 Toast「暮火正旺！」。</summary>
    public static void TryFlavorToastOnKill()
    {
        if (!IsTwilightStaffEquipped()) return;
        int ch = BattleManager.Instance != null ? BattleManager.Instance.CurrentChapter : 0;
        if (ch != 1) return;
        if (Time.unscaledTime - _lastFlavorToastTime < 6f) return;
        if (Random.value > 0.35f) return;
        _lastFlavorToastTime = Time.unscaledTime;
        UIManager.Instance?.ShowToast("暮火正旺！");
    }
}
