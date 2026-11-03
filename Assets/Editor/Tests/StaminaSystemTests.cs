using NUnit.Framework;

/// <summary>
/// StaminaSystem 可离线测面（EditMode）。Tick/Current 依赖 SaveSystem 单例与时钟，归 PlayMode/人工验收；
/// 这里只锁常量口径与纯函数 FormatCountdown。
/// 锚定拍板口径：
///   - 恢复 600 秒/点（10-29 收紧 300→600）、上限 100、攒满约 16.7 小时
///   - 主线小关 5 体力（10-22「打一关回一次」10→5）、裂隙 15（10-29 同日二调）、支线岛 10（10-23）
///   - 药水单瓶 20（10-18 30→20，超出上限作废）
/// </summary>
public class StaminaSystemTests
{
    [Test]
    public void Constants_Pinned()
    {
        Assert.AreEqual(600, StaminaSystem.REGEN_SECONDS_PER_POINT, "10 分钟回 1 点（10-29 拍板）");
        Assert.AreEqual(5, StaminaSystem.ADVENTURE_COST, "主线小关 5 体力（10-22 拍板）");
        Assert.AreEqual(15, StaminaSystem.ENDLESS_RIFT_COST, "裂隙 15 体力（10-29 同日二调）");
        Assert.AreEqual(10, StaminaSystem.SIDE_ISLAND_COST, "支线岛 10 体力（10-23 拍板）");
        Assert.AreEqual(20, StaminaSystem.POTION_RESTORE, "药水单瓶 20（10-18 拍板）");
    }

    [Test]
    public void Max_NoSave_BaseOnly()
    {
        // 空测试环境无存档：上限 = GameConfig.STAMINA_MAX + 0（staminaBonusMax ?? 0）
        Assert.AreEqual(GameConfig.STAMINA_MAX, StaminaSystem.Max, "无存档时上限=基础值");
        Assert.AreEqual(100, GameConfig.STAMINA_MAX, "体力上限锚定 100（10-31 两套上限已统一走 ResourceWallet.GetMax）");
    }

    [Test]
    public void FormatCountdown_MinutesSeconds()
    {
        Assert.AreEqual("", StaminaSystem.FormatCountdown(0), "0 秒 → 空串（已满不显示倒计时）");
        Assert.AreEqual("", StaminaSystem.FormatCountdown(-5), "负数 → 空串");
        Assert.AreEqual("00:59", StaminaSystem.FormatCountdown(59));
        Assert.AreEqual("01:00", StaminaSystem.FormatCountdown(60));
        Assert.AreEqual("59:59", StaminaSystem.FormatCountdown(3599));
    }

    [Test]
    public void FormatCountdown_Hours()
    {
        Assert.AreEqual("01:00:00", StaminaSystem.FormatCountdown(3600));
        Assert.AreEqual("01:01:01", StaminaSystem.FormatCountdown(3661));
        Assert.AreEqual("16:40:00", StaminaSystem.FormatCountdown(60000), "100 点攒满 ≈ 16.7 小时（10-29 拍板口径自洽）");
    }
}
