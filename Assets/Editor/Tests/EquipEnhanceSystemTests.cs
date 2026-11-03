using NUnit.Framework;

/// <summary>
/// EquipEnhanceSystem 纯逻辑单测（EditMode，公式与造价表锚定）。
///   - 强化倍率 GetMultiplier = 1 + enhanceLevel × 成长率；0 级恒 1
///   - 成长率按品质：Common 0.05 / 级、Rare 0.06 / 级（更高品质档以源码为准，本文件只锚已读全的两档）
///   - 造价：材料 MatCost（+1~+5 = 1+next；+6 起 = 3+next）、金币 GoldCost（+1~+5 免费；+6 起 = 50×next）
/// 概率失败（+8 起小概率失败不掉级）含随机，归人工验收（测试文档 §4.6 I3）。
/// </summary>
public class EquipEnhanceSystemTests
{
    [Test]
    public void GrowthRate_Pinned()
    {
        Assert.AreEqual(0.05f, EquipEnhanceSystem.GrowthRate(Rarity.Common), 0.0001f, "Common +5%/级");
        Assert.AreEqual(0.06f, EquipEnhanceSystem.GrowthRate(Rarity.Rare), 0.0001f, "Rare +6%/级");
    }

    [Test]
    public void GetMultiplier_Formula()
    {
        Assert.AreEqual(1f, EquipEnhanceSystem.GetMultiplier(null), 0.0001f, "空实例不炸，恒 1");
        var e = new EquipInstance { rarity = Rarity.Common };
        Assert.AreEqual(1f, EquipEnhanceSystem.GetMultiplier(e), 0.0001f, "0 级恒 1");
        e.enhanceLevel = 10;
        Assert.AreEqual(1.5f, EquipEnhanceSystem.GetMultiplier(e), 0.0001f, "Common +10 = 1 + 10×0.05");
        e.rarity = Rarity.Rare;
        e.enhanceLevel = 5;
        Assert.AreEqual(1.3f, EquipEnhanceSystem.GetMultiplier(e), 0.0001f, "Rare +5 = 1 + 5×0.06");
    }

    [Test]
    public void MatCost_TwoSegments()
    {
        Assert.AreEqual(2, EquipEnhanceSystem.MatCost(1), "+1 需 2 材料");
        Assert.AreEqual(6, EquipEnhanceSystem.MatCost(5), "+5 需 6 材料（1+5）");
        Assert.AreEqual(9, EquipEnhanceSystem.MatCost(6), "+6 起 3+next");
        Assert.AreEqual(13, EquipEnhanceSystem.MatCost(10), "+10 需 13 材料");
    }

    [Test]
    public void GoldCost_FreeUntilPlusSix()
    {
        Assert.AreEqual(0, EquipEnhanceSystem.GoldCost(5), "+1~+5 不收金币");
        Assert.AreEqual(300, EquipEnhanceSystem.GoldCost(6), "+6 = 50×6");
        Assert.AreEqual(500, EquipEnhanceSystem.GoldCost(10), "+10 = 50×10");
    }
}
