using NUnit.Framework;

/// <summary>
/// MercFavorSystem 纯逻辑单测（EditMode，传 SaveData 直测）。
/// 锚定 2026-10-26/27/31 + 2026-11-09 拍板口径：
///   - 好感真源 SaveData.mercTrust，唯一加值出口 AddMercTrust（下限钳 0；上不封顶，解锁线是 ≥60 判定）
///   - 满值 60 = 6 颗心 × 10 分/颗；≥60 开羁绊地图
///   - 对话（11-09 口径）：一天一轮、一轮一问、一次只选一个答案；三槽分别 +10 / +5 / +0，
///     按 天+佣兵 确定性轮换、分值不展示、选完才揭晓；+10 档额外回 1 个普通品质礼物
///   - 礼物三档：普通 2 / 稀有 4 / 传奇 8；20 种礼物；查不到 id = fail closed
///   - 羁绊首通钻石按稀有度：普通 50 / 稀有 100 / 传奇 200（2026-11-03 拍板）
/// 发礼/回礼依赖 SaveSystem 与 Toast，归人工验收（测试文档 §4.3 M6/M7）。
/// </summary>
public class MercFavorSystemTests
{
    const string H001 = "H001";   // 马库斯

    static SaveData NewSave()
    {
        var d = new SaveData();
        MercFavorSystem.EnsureTalkDay(d);
        return d;
    }

    // ---------- 好感账本 ----------

    [Test]
    public void GetFavor_EmptySave_IsZero()
    {
        Assert.AreEqual(0, MercFavorSystem.GetFavor(NewSave(), H001));
        Assert.AreEqual(0, MercFavorSystem.GetFavor(null, H001), "空存档不炸");
    }

    [Test]
    public void FavorMax_IsSixHeartsTimesTen()
    {
        Assert.AreEqual(60, MercFavorDefs.FavorMax, "6 颗心 × 10 分 = 60（10-31 爱心即进度条口径）");
        Assert.AreEqual(6, MercFavorDefs.HeartCount);
    }

    [Test]
    public void IsBondOpen_AtSixtyOrMore()
    {
        var d = NewSave();
        d.AddMercTrust(H001, 59);
        Assert.IsFalse(MercFavorSystem.IsBondOpen(d, H001), "59 分未到解锁线");
        d.AddMercTrust(H001, 1);
        Assert.IsTrue(MercFavorSystem.IsBondOpen(d, H001), "60 分整应解锁羁绊地图");
    }

    [Test]
    public void AddMercTrust_ClampsAtZero_NotAtMax()
    {
        var d = NewSave();
        d.AddMercTrust(H001, -5);
        Assert.AreEqual(0, d.GetMercTrust(H001), "好感下限钳 0");
        d.AddMercTrust(H001, 100);
        Assert.AreEqual(100, d.GetMercTrust(H001), "好感上不钳顶（解锁线是 ≥60 的判定）");
    }

    // ---------- 每日对话 ----------

    [Test]
    public void TalkTiers_OneRoundPerDay_ThreeTiers()
    {
        Assert.AreEqual(1, MercFavorDefs.TalksPerDay, "11-09 口径：一天一轮、一轮一问、一次只选一个答案");
        Assert.AreEqual(3, MercFavorDefs.DialogueOptionCount);
        Assert.AreEqual(10, MercFavorDefs.BestTalkFavor);
        Assert.AreEqual(5, MercFavorDefs.MidTalkFavor);
        Assert.AreEqual(0, MercFavorDefs.NoTalkFavor);
        Assert.AreEqual(10, MercFavorDefs.TalkTierAt(0));
        Assert.AreEqual(5, MercFavorDefs.TalkTierAt(1));
        Assert.AreEqual(0, MercFavorDefs.TalkTierAt(2));
        Assert.AreEqual(10, MercFavorDefs.TalkTierAt(3), "档位序号按 3 循环，不越界");
    }

    [Test]
    public void TalkTierOfSlot_StableWithinDay_AllThreeTiersPresent()
    {
        var d = NewSave();
        int tier0 = MercFavorSystem.TalkTierOfSlot(d, H001, 0);
        Assert.GreaterOrEqual(tier0, 0);
        Assert.Less(tier0, MercFavorDefs.DialogueOptionCount);
        Assert.AreEqual(tier0, MercFavorSystem.TalkTierOfSlot(d, H001, 0), "同一天同佣兵档位应稳定（防刷新换位）");

        var seen = new bool[MercFavorDefs.DialogueOptionCount];
        for (int slot = 0; slot < MercFavorDefs.DialogueOptionCount; slot++)
        {
            int tier = MercFavorSystem.TalkTierOfSlot(d, H001, slot);
            Assert.GreaterOrEqual(tier, 0);
            Assert.Less(tier, MercFavorDefs.DialogueOptionCount);
            Assert.IsFalse(seen[tier], "档位不应重复：" + tier);
            seen[tier] = true;
        }
    }

    [Test]
    public void FavorOfSlot_ThreeTiersExactlyOnce()
    {
        var d = NewSave();
        int best = 0, mid = 0, none = 0;
        for (int slot = 0; slot < MercFavorDefs.DialogueOptionCount; slot++)
        {
            int favor = MercFavorSystem.FavorOfSlot(d, H001, slot);
            if (favor == MercFavorDefs.BestTalkFavor) best++;
            else if (favor == MercFavorDefs.MidTalkFavor) mid++;
            else if (favor == MercFavorDefs.NoTalkFavor) none++;
            else Assert.Fail("未知档位：" + favor);
        }
        Assert.AreEqual(1, best, "+10 档恰 1 槽");
        Assert.AreEqual(1, mid, "+5 档恰 1 槽");
        Assert.AreEqual(1, none, "+0 档恰 1 槽");
    }

    [Test]
    public void Talk_OneRoundThenBlocked()
    {
        var d = NewSave();
        // 选 +5 档槽：好感确实增加，又不触发 +10 档回礼（EditMode 无存档，不依赖 SaveSystem 状态）
        int slot = -1;
        for (int s = 0; s < MercFavorDefs.DialogueOptionCount; s++)
        {
            if (MercFavorSystem.FavorOfSlot(d, H001, s) == MercFavorDefs.MidTalkFavor) { slot = s; break; }
        }
        Assert.GreaterOrEqual(slot, 0, "三槽中应存在 +5 档");

        int expected = MercFavorSystem.FavorOfSlot(d, H001, slot);
        Assert.IsTrue(MercFavorSystem.CanTalk(d, H001), "当天第一轮应可聊");
        int delta = MercFavorSystem.Talk(d, H001, slot, out string gift);
        Assert.AreEqual(expected, delta, "本次增量应等于所选槽分值");
        Assert.IsNull(gift, "非最高档不发回礼");
        Assert.AreEqual(expected, d.GetMercTrust(H001), "一轮对话累计好感应一致");
        Assert.IsFalse(MercFavorSystem.CanTalk(d, H001), "1 轮后当天不可再聊");
        Assert.AreEqual(-1, MercFavorSystem.Talk(d, H001, slot, out _), "超次对话应拒绝并返回 -1");
    }

    [Test]
    public void Talk_InvalidSlot_Rejected()
    {
        var d = NewSave();
        Assert.AreEqual(-1, MercFavorSystem.Talk(d, H001, -1, out _), "负槽位应拒绝");
        Assert.AreEqual(-1, MercFavorSystem.Talk(d, H001, MercFavorDefs.DialogueOptionCount, out _), "越界槽位应拒绝");
        Assert.AreEqual(0, d.GetMercTrust(H001), "拒绝的对话不加好感");
    }

    [Test]
    public void EnsureTalkDay_NewDayClearsTalkLog()
    {
        var d = NewSave();
        MercFavorSystem.Talk(d, H001, 0, out _);
        Assert.AreEqual(1, MercFavorSystem.TalkedCount(d, H001));
        d.favorTalkDayKey = "19990101";   // 模拟跨天
        Assert.AreEqual(0, MercFavorSystem.TalkedCount(d, H001), "跨天后已聊记录应清零（TalkedCount 内部懒刷新）");
        Assert.IsTrue(MercFavorSystem.CanTalk(d, H001), "跨天后可重新对话");
    }

    // ---------- 礼物 ----------

    [Test]
    public void GiftTable_ThreeTiers_TwentyGifts()
    {
        Assert.AreEqual(20, MercFavorDefs.Gifts.Length, "礼物 20 种（2026-10-26 供图口径）");
        foreach (var g in MercFavorDefs.Gifts)
        {
            Assert.IsTrue(g.Favor == 2 || g.Favor == 4 || g.Favor == 8, $"{g.Id} 分值应落在 2/4/8 三档");
            if (g.Favor == 8) Assert.IsTrue(MercFavorDefs.IsLegendary(g), $"{g.Id} 8 分应判为传奇");
        }
        Assert.AreEqual("普通", MercFavorSystemTests_QualityName(MercFavorDefs.GiftQuality.Common));
        Assert.AreEqual("稀有", MercFavorSystemTests_QualityName(MercFavorDefs.GiftQuality.Rare));
        Assert.AreEqual("传奇", MercFavorSystemTests_QualityName(MercFavorDefs.GiftQuality.Legendary));
    }

    static string MercFavorSystemTests_QualityName(MercFavorDefs.GiftQuality q) => MercFavorDefs.QualityName(q);

    [Test]
    public void GiveGift_AddsFavorAndConsumes()
    {
        var d = NewSave();
        d.AddFavorGift("gift_301", 2);   // 面包（普通档 +2）
        int delta = MercFavorSystem.GiveGift(d, H001, "gift_301");
        Assert.AreEqual(2, delta, "面包 +2 好感");
        Assert.AreEqual(2, d.GetMercTrust(H001));
        // 再送一次后库存空 → 拒绝
        int delta2 = MercFavorSystem.GiveGift(d, H001, "gift_301");
        Assert.AreEqual(2, delta2, "第二个面包仍 +2");
        Assert.AreEqual(-1, MercFavorSystem.GiveGift(d, H001, "gift_301"), "库存空应拒绝");
        Assert.AreEqual(4, d.GetMercTrust(H001), "拒绝的送礼不加好感");
    }

    [Test]
    public void GiveGift_UnknownId_FailsClosed()
    {
        var d = NewSave();
        Assert.AreEqual(-1, MercFavorSystem.GiveGift(d, H001, "gift_999"), "表里没有的 id = fail closed");
        Assert.AreEqual(-1, MercFavorSystem.GiveGift(d, H001, null));
        Assert.AreEqual(-1, MercFavorSystem.GiveGift(d, "", "gift_301"), "空 hireId 应拒绝");
        Assert.AreEqual(0, d.GetMercTrust(H001));
    }

    // ---------- 羁绊首通钻石 ----------

    [Test]
    public void BondClearDiamond_ByRarity()
    {
        Assert.AreEqual(50, MercFavorDefs.BondClearDiamondOf(MercRosterDefs.MercRarity.Common), "普通 50（2026-11-03 拍板）");
        Assert.AreEqual(100, MercFavorDefs.BondClearDiamondOf(MercRosterDefs.MercRarity.Rare), "稀有 100");
        Assert.AreEqual(200, MercFavorDefs.BondClearDiamondOf(MercRosterDefs.MercRarity.Legendary), "传奇 200");
    }
}
