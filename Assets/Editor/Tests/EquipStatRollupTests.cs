using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// EquipStatRollup 纯逻辑单测（EditMode，手工构造 EquipInstance，不依赖模板/Resources）。
/// 锚定拍板口径：
///   - 强化倍率只乘 attrBonus 前 baseAttrCount 条（基础属性），随机词条/globalBonus 永不乘（2026-10-09）
///   - 副手平砍封顶 = 主手攻击 × 0.85（OffHandAttackCapRatio）
///   - 「固定值攻击」判定物/魔两侧都认（2026-09-29 拍板：法师副手吃封顶、汇总不少算）
///   - globalBonus 是模板固定值，随装备在场直接生效
/// </summary>
public class EquipStatRollupTests
{
    static AttrBonusData Bonus(AttrType type, float value, bool isPercent = false)
        => new AttrBonusData { attrType = type, value = value, isPercent = isPercent };

    static EnchantData Enchant(AttrType type, float value, bool isPercent = false)
        => new EnchantData { attrType = type, value = value, isPercent = isPercent };

    /// <summary>双手武器（非盾）→ 逻辑主手。attrBonus 一条基础攻 + 可选随机词条。</summary>
    static EquipInstance MainWeapon(float baseAtk, params AttrBonusData[] extras)
    {
        var e = new EquipInstance { weaponType = WeaponType.TwoHand, baseAttrCount = 1 };
        e.attrBonus.Add(Bonus(AttrType.Attack, baseAtk));
        foreach (var b in extras) e.attrBonus.Add(b);
        return e;
    }

    /// <summary>盾（OffHand + weaponType=None）→ 逻辑副手。</summary>
    static EquipInstance Shield(AttrBonusData global = null, params EnchantData[] enchants)
    {
        var e = new EquipInstance { weaponHand = WeaponHandSlot.OffHand, weaponType = WeaponType.None };
        e.globalBonus = global;
        foreach (var b in enchants) e.enchants.Add(b);
        return e;
    }

    static float FindFlat(List<AttrBonusData> list, AttrType type)
    {
        var hit = list.Where(b => b.attrType == type && !b.isPercent).ToList();
        Assert.AreEqual(1, hit.Count, $"平砍 {type} 条目应恰好 1 条（实际 {hit.Count}）");
        return hit[0].value;
    }

    // ---------- 空输入 ----------

    [Test]
    public void BuildBonusListForSet_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(0, EquipStatRollup.BuildBonusListForSet(null).Count);
        Assert.AreEqual(0, EquipStatRollup.BuildBonusListForSet(new List<EquipInstance>()).Count);
    }

    // ---------- 主手基础 ----------

    [Test]
    public void MainHandOnly_SingleFlatAttack()
    {
        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { MainWeapon(100f) });
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(100f, FindFlat(list, AttrType.Attack), 0.001f);
    }

    // ---------- 强化乘区 ----------

    [Test]
    public void Enhance_MultipliesBaseAttrsOnly()
    {
        // +10 Common：倍率 = 1 + 10 × 0.05 = 1.5，只吃基础段（baseAttrCount=1）
        var main = MainWeapon(100f, Bonus(AttrType.Attack, 20f));   // 第二条是随机词条
        main.enhanceLevel = 10;
        main.rarity = Rarity.Common;
        main.globalBonus = Bonus(AttrType.Attack, 50f);

        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { main });
        // 三条 Attack 平砍并存（global 50 / 词条 20 / 强化后基础 150），不能走 FindFlat（要求恰 1 条），
        // 直接校验三条的值：
        var atkFlats = list.Where(b => b.attrType == AttrType.Attack && !b.isPercent).Select(b => b.value).OrderBy(v => v).ToList();
        CollectionAssert.AreEqual(new[] { 20f, 50f, 150f }, atkFlats, "三条 Attack 应为 20(词条)/50(global)/150(强化后基础)");
    }

    [Test]
    public void EnhanceZeroLevel_NoMultiplier()
    {
        var main = MainWeapon(100f);
        main.enhanceLevel = 0;
        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { main });
        Assert.AreEqual(100f, FindFlat(list, AttrType.Attack), 0.001f);
    }

    // ---------- 副手封顶 ----------

    [Test]
    public void OffHand_FlatAttack_CappedAt85PercentOfMain()
    {
        var main = MainWeapon(100f);
        var shield = Shield(Bonus(AttrType.Attack, 200f));   // 平砍 200 → 封顶 85
        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { main, shield });
        var atkFlats = list.Where(b => b.attrType == AttrType.Attack && !b.isPercent).Select(b => b.value).ToList();
        CollectionAssert.AreEqual(new[] { 85f, 100f }, atkFlats.OrderBy(v => v).ToList(),
            "副手平砍应封顶 主手×0.85 = 85（升序：85, 100）");
        Assert.AreEqual(EquipStatRollup.OffHandAttackCapRatio, 0.85f, 0.0001f, "封顶系数锚定");
    }

    [Test]
    public void OffHand_MagicAttack_AlsoCapped()
    {
        // 2026-09-29 拍板：固定值攻击判定物/魔两侧都认 —— 法师副手魔攻也要吃封顶
        var main = MainWeapon(100f);
        var shield = Shield(Bonus(AttrType.MagicAttack, 200f));
        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { main, shield });
        Assert.AreEqual(85f, FindFlat(list, AttrType.MagicAttack), 0.001f,
            "副手魔攻平砍应同样封顶 85（否则法师副手少算/失控）");
    }

    [Test]
    public void OffHand_PercentAttack_NotCapped()
    {
        // 百分比攻击不是「固定值攻击」，不吃封顶
        var main = MainWeapon(100f);
        var shield = Shield(Bonus(AttrType.Attack, 0.2f, isPercent: true));
        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { main, shield });
        var pct = list.Where(b => b.attrType == AttrType.Attack && b.isPercent).ToList();
        Assert.AreEqual(1, pct.Count);
        Assert.AreEqual(0.2f, pct[0].value, 0.0001f, "百分比攻击条目应原样保留");
    }

    [Test]
    public void OffHand_NonAttackBonuses_NotCapped()
    {
        var main = MainWeapon(100f);
        var shield = Shield(Bonus(AttrType.MagicDefense, 300f));
        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { main, shield });
        Assert.AreEqual(300f, FindFlat(list, AttrType.MagicDefense), 0.001f, "非攻击属性不吃封顶");
    }

    // ---------- 附魔/技能被动 ----------

    [Test]
    public void ShieldAlone_GlobalEnchantsSkillPassives_AllAppended()
    {
        // 无主手 → mainAttack=0 → 封顶不生效，条目原样
        var shield = Shield(
            Bonus(AttrType.Defense, 30f),
            Enchant(AttrType.Attack, 100f),
            Enchant(AttrType.MaxHp, 500f));
        shield.skillPassives.Add(Bonus(AttrType.CritRate, 0.05f));

        var list = EquipStatRollup.BuildBonusListForSet(new List<EquipInstance> { shield });
        Assert.AreEqual(4, list.Count, "global 1 + 附魔 2 + 技能被动 1 = 4 条");
        Assert.AreEqual(100f, FindFlat(list, AttrType.Attack), 0.001f);
        Assert.AreEqual(500f, FindFlat(list, AttrType.MaxHp), 0.001f);
        Assert.AreEqual(0.05f, list.First(b => b.attrType == AttrType.CritRate).value, 0.0001f);
    }

    // ---------- 主/副手判定 ----------

    [Test]
    public void PickLogicalWeapons_TwoHandIsMain_ShieldIsOff()
    {
        var sword = MainWeapon(100f);
        var shield = Shield();
        EquipStatRollup.PickLogicalWeapons(new List<EquipInstance> { sword, shield }, out var main, out var off);
        Assert.AreSame(sword, main, "双手武器应判为主手");
        Assert.AreSame(shield, off, "盾应判为副手");
    }

    [Test]
    public void PickLogicalWeapons_ShieldAlone_IsOffWithNoMain()
    {
        var shield = Shield();
        EquipStatRollup.PickLogicalWeapons(new List<EquipInstance> { shield }, out var main, out var off);
        Assert.IsNull(main);
        Assert.AreSame(shield, off);
    }

    [Test]
    public void PickLogicalWeapons_TwoSwords_OnlyMainPicked()
    {
        // 两把双手武器：第一把主手；第二把既非盾也非副手武器 → 副手位不收（不产生封顶/副手逻辑）
        var s1 = MainWeapon(100f);
        var s2 = MainWeapon(80f);
        EquipStatRollup.PickLogicalWeapons(new List<EquipInstance> { s1, s2 }, out var main, out var off);
        Assert.AreSame(s1, main);
        Assert.IsNull(off, "第二把双手武器不应占用副手位");
    }
}
