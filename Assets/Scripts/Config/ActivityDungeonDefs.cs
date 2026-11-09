// 活动副本（资源副本）专属配置 —— 2026-11-09 主人拍板。
//
// ① 副本怪显示名：金币本 / 徽记本三种怪（小怪/精英/Boss）各一套；
//    卷轴 / 礼物本用普通关卡的怪，「该叫什么就叫什么」，不进本表（主人原话）。
//    消费点 = Monster.HudDisplayName()（技能通知条 + Boss 血条名牌的唯一出口），
//    修的是「金币副本宝箱怪放技能显示走路菇」（原逻辑直读章节模板 config.monsterName）。
// ② 金币本逐怪分摊权重：普通 1 / 精英 2 / Boss 5。总额 = GameConfig.GetGoldDungeonClearGold
//    文案三档（普通 500 / 困难 800 / 噩梦 1200），按累计权重占比逐只发放、末只补齐零头，
//    保证「顶栏金币条累计 = 结果界面总额 = 详情文案」。要调单价比例只改下面三个数。
public static class ActivityDungeonDefs
{
    // —— 金币本（专属图：Resources/Config/MonsterSpriteRegistry/金币/）——
    public const string GoldNormalName = "宝箱怪";
    public const string GoldEliteName = "金宝箱";
    public const string GoldBossName = "宝箱王";

    // —— 徽记本（专属图：Resources/Config/MonsterSpriteRegistry/徽记/）——
    public const string BadgeNormalName = "徽记怪";
    public const string BadgeEliteName = "金徽记";
    public const string BadgeBossName = "徽记王";

    // —— 金币本逐怪分摊权重（2026-11-09，可调）——
    public const int GoldWeightNormal = 1;
    public const int GoldWeightElite = 2;
    public const int GoldWeightBoss = 5;

    /// <summary>
    /// 副本怪显示名单一出口：金币/徽记本按槽位（Boss 优先于精英）返回专属名；
    /// 其余（卷轴/礼物本、主线、裂隙、竞技场…）返回 null，调用方回退模板怪物名。
    /// </summary>
    public static string DisplayName(bool goldDungeon, bool badgeDungeon, bool bossUnit, bool eliteWave)
    {
        if (goldDungeon)
            return bossUnit ? GoldBossName : eliteWave ? GoldEliteName : GoldNormalName;
        if (badgeDungeon)
            return bossUnit ? BadgeBossName : eliteWave ? BadgeEliteName : BadgeNormalName;
        return null;
    }
}
