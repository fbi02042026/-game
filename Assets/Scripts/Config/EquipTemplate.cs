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
    public int gridWidth = 1;
    public int gridHeight = 1;
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