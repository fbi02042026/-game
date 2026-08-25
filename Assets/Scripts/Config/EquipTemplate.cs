using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 装备品质，固定不可升级
/// </summary>
public enum Rarity
{
    Common = 1, // 白 最多1星
    Uncommon = 2, // 绿 最多2星
    Rare = 3, // 蓝 最多3星
    Epic = 4, // 紫 最多4星
    Legendary = 5 // 橙 最多5星
}

/// <summary>
/// 附魔词条
/// </summary>
[Serializable]
public class EnchantData
{
    public string enchantName;
    public AttrType attrType;
    public float value;
    public bool isPercent;
}

/// <summary>
/// 装备模板：固定图标/基础属性/占格/标签，品质固定，只能局内升星
/// </summary>
[CreateAssetMenu(fileName = "EquipTemplate", menuName = "Config/EquipTemplate")]
public class EquipTemplate : ScriptableObject
{
    public string templateId;
    public string equipName;
    public Sprite icon; // 图标可以复用
    [Tooltip("EquipIcons 文件夹内文件名（不含 .png），如 New_Weapon_01")]
    public string iconFileName;
    public int gridWidth = 1;
    public int gridHeight = 1;

    /// <summary>优先按 iconFileName 从 EquipIcons 加载，覆盖损坏/丢失的序列化引用。</summary>
    public void ResolveIcon()
    {
        if (string.IsNullOrEmpty(iconFileName))
            iconFileName = templateId;
        if (string.IsNullOrEmpty(iconFileName)) return;

        var loaded = EquipIcons.Get(iconFileName);
        if (loaded != null)
            icon = loaded;
    }

    /// <summary>按槽位/武器类型给出默认占格（可在 Inspector 再改）。</summary>
    public void ApplyDefaultGridSize()
    {
        if (slotType == EquipSlotType.Chest)
        {
            gridWidth = 2;
            gridHeight = 1;
            return;
        }

        if (slotType == EquipSlotType.MainHand)
        {
            if (weaponType == WeaponType.TwoHand)
            {
                gridWidth = 2;
                gridHeight = 3;
            }
            else
            {
                gridWidth = 1;
                gridHeight = 2;
            }
            return;
        }

        if (slotType == EquipSlotType.OffHand)
        {
            gridWidth = 2;
            gridHeight = 2;
            return;
        }

        gridWidth = 1;
        gridHeight = 1;
    }
    public List<string> tags = new List<string>();
    public Rarity baseRarity; // 基础品质，星级提升品质上限
    public int minLevel = 1; // 最低等级要求
    public List<AttrBonusData> baseAttr = new List<AttrBonusData>(); // 基础属性
    public AttrBonusData globalBonus; // 传说模板的全局加成
    public bool isLegendary; // 是否是传说模板

    [Header("装备槽位")]
    public EquipSlotType slotType = EquipSlotType.Head; // 装备槽位

    [Header("武器专属")]
    public WeaponType weaponType = WeaponType.None; // 武器类型（非武器为None）
    public WeaponAttackType weaponAttackType = WeaponAttackType.Physical; // 攻击属性
    public float attackRange = 96f; // 数值表「攻击范围(像素)」；运行时用 GameConfig.NormalizeAttackRange

    [Header("防具专属")]
    public ArmorPrefix armorPrefix = ArmorPrefix.None; // 防具前缀

    [Header("SPUM换装")]
    public string spumName; // SPUM精灵图名称，用于换装映射
}