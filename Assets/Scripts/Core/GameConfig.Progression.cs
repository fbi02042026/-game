using UnityEngine;

/// <summary>
/// GameConfig 的 佣兵解锁 / 隐藏经验 / 资源上限 部分（2026-09-26 从 GameConfig.cs 按域拆出，partial 同类型）。
/// 只搬位置，**数值与调用点一字未改**；改这里的常量 = 改原 GameConfig。
/// </summary>
public static partial class GameConfig
{
    [Header("佣兵解锁")]

    // 佣兵跟队/脱队（本地 WIP + Mercenary.cs 使用）
    public const float MERC_LEASH_RADIUS = 4.2f;
    public const float MERC_REJOIN_RADIUS = 2.5f;
    public const float MERC_SOFT_AWAY_RADIUS = 2.8f;
    public const float MERC_AWAY_FIGHT_SEC = 2.8f;

    public const int ADVANCED_MERC_GUILD_LEVEL = 5; // 优秀=公会5；稀有10/传奇20 另见 MercQuality

    [Header("隐藏经验等级")]
    public const int HIDDEN_LEVEL_MAX = 60;
    public const int HIDDEN_EXP_KILL_NORMAL = 8;
    public const int HIDDEN_EXP_KILL_ELITE = 20;
    public const int HIDDEN_EXP_KILL_BOSS = 50;
    public const int HIDDEN_EXP_STAGE_CLEAR = 35;
    public const int HIDDEN_EXP_STAGE_CLEAR_PER_CHAPTER = 5;
    /// <summary>每日酒馆招募次数上限</summary>
    public const int DAILY_MERC_RECRUIT_MAX = 1;
    /// <summary>背包列数：与美术新画的 GridContainer 一致（4 列，Cell_0_0 ~ Cell_3_2）</summary>
    public const int BACKPACK_WIDTH = 4;
    /// <summary>背包初始/默认解锁行数：3 行，共 12 格。双手武器 2×3 时会占掉一半，属于已知取舍。
    /// 注意：此值只表示「默认解锁几行」，不再作为网格行数上限（见 BACKPACK_HEIGHT_MAX）。</summary>
    public const int BACKPACK_HEIGHT = 3;
    /// <summary>背包行数上限（本期扩容到 4 行 = 16 格）。
    /// 底层网格按此上限分配；实际可用行数受 SaveData.backpackRows / 天赋 R_BAG 钳制到该上限。</summary>
    public const int BACKPACK_HEIGHT_MAX = 4;
    public const int STAGES_PER_CHAPTER = 10; // 每章10关，最后一关是BOSS
    public const int SPECIAL_STAGES_PER_CHAPTER = 2; // 每章最多2个特殊关卡（商人/附魔/诅咒/休息）
    public const int MAX_OFFLINE_HOURS = 8; // 最多8小时离线收益
    public const int GOLD_PER_TALENT_POINT = 100; // 每100金币给1天赋点

    [Header("资源上限")]
    /// <summary>通用资源软上限（金币/钻石等）；超出不累加，进邮件</summary>
    public const long RESOURCE_MAX = ResourceWallet.DEFAULT_MAX;
    /// <summary>体力特殊上限</summary>
    public const int STAMINA_MAX = 100;
    /// <summary>新号初始体力</summary>
    public const int STAMINA_START = 100;
    /// <summary>每次点「冒险」消耗体力</summary>
    public const int STAMINA_ADVENTURE_COST = StaminaSystem.ADVENTURE_COST;
    /// <summary>回复 1 点体力所需秒数</summary>
    public const int STAMINA_REGEN_SECONDS = StaminaSystem.REGEN_SECONDS_PER_POINT;

    // ===== 广告位 → 钻石消耗位（2026-09-19 主人拍板）=====
    // 2026-09-21 主人要求：**广告相关全部停用**（聚光灯计划参赛包不得出现任何广告）。
    // 原「广告总开关」ADS_ENABLED_BEFORE_SPOTLIGHT 已注释停用，广告路径（RewardedAdBridge）
    // 的两处调用点同步注释，见 ResourceAdRewards.TryClaimStamina / TryClaimGold。
    // 现在顶栏的体力 / 金币补给是**纯钻石消耗位**（每日前 2 次免费），不再有任何「看广告」分支。
    // 将来真要恢复广告：把下面这段常量与 ResourceAdRewards 里注释掉的广告分支一起解注释即可。
    //
    // /// <summary>
    // /// 广告总开关。**聚光灯功能正式上线前必须保持 false。**
    // /// false = 所有原本「看广告」获得奖励 / 开启的入口一律走钻石
    // ///        （ResourceAdRewards 的体力/金币补给）；
    // /// true  = 恢复「看广告」路径。
    // /// </summary>
    // public const bool ADS_ENABLED_BEFORE_SPOTLIGHT = false;

    /// <summary>钻石定价：顶栏体力补给一次（体力 +ResourceAdRewards.StaminaPerAd）。2026-09-20 钻石经济调整：20→15。</summary>
    public const int AD_SLOT_STAMINA_DIAMOND = 15;
    /// <summary>钻石定价：顶栏金币补给一次（金币 +ResourceAdRewards.GoldPerAd）。2026-09-20 钻石经济调整：20→10。</summary>
    public const int AD_SLOT_GOLD_DIAMOND = 10;

}
