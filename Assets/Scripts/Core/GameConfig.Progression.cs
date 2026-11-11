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
    // 【2026-10-19 主人拍板】交战跟随上限：离玩家超过此距离就「边打边跟」（朝玩家移动，攻距内照常开火），
    // 保证佣兵始终聚在玩家身边。取值介于软回队(2.8)与拴绳强拉(4.2)之间。
    public const float MERC_STICK_RADIUS = 3.0f;

    // 【2026-10-19 主人拍板】佣兵追赶加速：佣兵与玩家同速（10-11 统一移速真源），
    // 同速追赶永远追不上——站桩输出掉一次队，距离差就一直保持，越滚越大
    //（远程佣兵射程长、几乎全程站桩，掉队最明显，主人原话「一会就剩玩家自己在前面打」）。
    public const float MERC_CATCHUP_SPEED_MUL = 1.35f;   // 掉队后追赶速度 = 玩家移速 × 此倍率
    public const float MERC_CATCHUP_ENTER_DIST = 0.8f;   // 落后编队位超过此距离才加速，否则同速跟（防顶着玩家跑）

    // 【2026-11-06 主人拍板】战斗体验：抽到/获得佣兵时的入队冲刺 + 离玩家太远时的掉队追赶加速。
    // 只改临时移速倍率，不改阵型/站位规则；玩家与怪物移速不受影响。
    public const float MERC_RUSH_SPEED_MUL = 2.2f;       // 新入队冲刺倍率（战斗中抽到后冲向玩家）
    public const float MERC_RUSH_STOP_DIST = 2.5f;       // 距玩家 <= 此距离结束冲刺，恢复正常移速/交给编队
    public const float MERC_LAG_ENTER_DIST = 3.0f;       // 距玩家 > 此距离开始掉队追赶加速
    public const float MERC_LAG_EXIT_DIST = 2.0f;        // 距玩家 <= 此距离结束追赶（滞回，防在阈值上来回抖）
    public const float MERC_LAG_SPEED_MUL = 1.5f;        // 掉队追赶倍率

    // 【2026-10-20 主人拍板】牧师「站住等自动奶」的最长定身时长：
    // 施法失败（蓝不够回蓝慢 / merc_skills 表缺耗蓝 fail closed）时 CooldownRemain 一直 <= 0，
    // 原实现会无限站桩脱队 → 超此时限强制恢复跟队（治疗由 MercSkillCaster.Update 自动放，不要求站着）。
    public const float MERC_HEAL_WAIT_MAX_SEC = 1.5f;

    public const int ADVANCED_MERC_GUILD_LEVEL = 5; // 优秀=公会5；稀有10/传奇20 另见 MercQuality

    [Header("隐藏经验等级")]
    public const int HIDDEN_LEVEL_MAX = 60;
    public const int HIDDEN_EXP_KILL_NORMAL = 8;
    public const int HIDDEN_EXP_KILL_ELITE = 20;
    public const int HIDDEN_EXP_KILL_BOSS = 50;
    public const int HIDDEN_EXP_STAGE_CLEAR = 35;
    public const int HIDDEN_EXP_STAGE_CLEAR_PER_CHAPTER = 5;

    // ===== 玩家等级经验（2026-11-05 主人拍板「打完一个小关发一次：1-1升1级、1-2升1级、1-3和1-4合计升1级」）=====
    /// <summary>供给总账（发放点 = BeginRewardSequence 每小关一次；每章小关数 50/80/100，三难度各 730 个）：
    /// 普通 110×730+32×2,780=169,260；困难 226,260；噩梦 283,260；任务（日常 670/天，不动）≈13,400。
    /// 曲线 GetExpForLevel=60+30lv+lv²/2 → cum(80)=183,260：主线 169,260+日常 13,400=182,660
    /// → 主线全通+一两天日常正好 80，**不打困难噩梦也能满级**；打了就是提前满（溢出经验转金币）。
    /// 前期实测（模拟）：1-1 升1级、1-2 升1级、1-3+1-4 合计升1级、1-5 升1级；第1章末 lv16、主线全通 lv77。</summary>
    /// <summary>普通难度通关一小关的基础经验（按章 +PLAYER_EXP_STAGE_CLEAR_PER_CHAPTER 递增）。支线岛同率（可重复刷，属额外加速）。</summary>
    public const int PLAYER_EXP_STAGE_CLEAR = 110;
    public const int PLAYER_EXP_STAGE_CLEAR_PER_CHAPTER = 32;
    /// <summary>困难难度通关一小关的经验，每章都高于普通同章（主人拍板「困难的比普通经验多」）。</summary>
    public const int PLAYER_EXP_HARD_STAGE_CLEAR = 150;
    public const int PLAYER_EXP_HARD_STAGE_CLEAR_PER_CHAPTER = 42;
    /// <summary>噩梦难度通关一小关的经验，每章都高于困难同章（主人拍板「噩梦比困难多」）。</summary>
    public const int PLAYER_EXP_NIGHTMARE_STAGE_CLEAR = 190;
    public const int PLAYER_EXP_NIGHTMARE_STAGE_CLEAR_PER_CHAPTER = 52;
    /// <summary>每日酒馆招募次数上限</summary>
    public const int DAILY_MERC_RECRUIT_MAX = 1;
    /// <summary>背包列数：与美术新画的 GridContainer 一致（4 列，Cell_0_0 ~ Cell_3_2）</summary>
    public const int BACKPACK_WIDTH = 4;
    /// <summary>背包初始/默认解锁行数：3 行，共 12 格。双手武器 2×3 时会占掉一半，属于已知取舍。
    /// 注意：此值只表示「默认解锁几行」，不再作为网格行数上限（见 BACKPACK_HEIGHT_MAX）。</summary>
    public const int BACKPACK_HEIGHT = 3;
    /// <summary>背包行数上限（2026-09-29：默认 3 行 + 天赋 R_BAG 三级各 1 行 = **6 行 = 24 格**）。
    /// 底层网格按此上限分配；实际可用行数受 SaveData.backpackRows / 天赋 R_BAG 钳制到该上限。</summary>
    public const int BACKPACK_HEIGHT_MAX = 6;
    /// <summary>【2026-10-23 主人拍板】单格钻石解锁价格（**唯一出处**）：角色页点击锁格弹确认框，
    /// 确认后扣此数钻石解锁该格；钻石不足 toast「钻石不足」、格子保持锁定（fail closed）。</summary>
    public const int BACKPACK_CELL_UNLOCK_DIAMOND = 5;
    /// <summary>【2026-10-25 主人拍板】每日首次分享奖励钻石数（走局外账，ResourceWallet.Add 唯一出口）。</summary>
    public const int SHARE_REWARD_DIAMOND = 100;
    /// <summary>【待主人提供】TapTap 商店详情页链接（好评引导跳转用，唯一出处）。
    /// 为空 = 好评引导不弹并 LogError 提醒补链接（不静默、不造 URL）。</summary>
    public const string TAPTAP_STORE_URL = "";
    /// <summary>【2026-10-25 主人拍板】好评引导总开关（false = 章末通关不再弹，链路整体停用）。
    /// 恢复时改 true 即可，代码零改动。</summary>
    public const bool TAPTAP_RATE_ENABLED = false;
    /// <summary>每章10关，最后一关是BOSS。
    /// ⚠️ 2026-10-23 关卡结构重构（批 B1）后，「一局打几关」的口径已改走
    /// <see cref="STAGES_PER_GRAND"/>（每大关 5 小关）；本常量只保留给仍按「章」计数的存量引用
    /// （如 MainQuestSystem.ChapterStageTotal 章级任务分母）。新增代码一律用 STAGES_PER_GRAND。</summary>
    public const int STAGES_PER_CHAPTER = 10;
    /// <summary>每大关（= 每局）小关数：旧批 B1 固定 5；【2026-11-01 批 B3】改为按章取
    /// （第 1 章 5 节、第 2 章 8 节、第 3 章起 10 节，真源 <see cref="ActProgressionDefs.SubsPerGrandOf"/>）。
    /// 本常量只代表第 1 章的节数，留给少数无章上下文的旧引用；**新代码一律用 <see cref="StagesPerGrandOf"/>**。</summary>
    public const int STAGES_PER_GRAND = 5;

    /// <summary>
    /// 【2026-11-01 批 B3 主人拍板 方案A】某章一局（关卡）的节数：第 1 章 5、第 2 章 8、第 3 章起 10。
    /// 运行时所有「这一局打几节 / Boss 在第几节」一律走这里，不再用写死的 5。
    /// </summary>
    public static int StagesPerGrandOf(int chapter)
        => ActProgressionDefs.SubsPerGrandOf(chapter);
    public const int SPECIAL_STAGES_PER_CHAPTER = 2; // 每章最多2个特殊关卡（商人/附魔/诅咒/休息）
    public const int MAX_OFFLINE_HOURS = 8; // 最多8小时离线收益
    public const int GOLD_PER_TALENT_POINT = 100; // 每100金币给1天赋点

    [Header("资源上限")]
    /// <summary>通用资源软上限（金币/钻石等）；超出不累加，进邮件</summary>
    public const long RESOURCE_MAX = ResourceWallet.DEFAULT_MAX;
    /// <summary>体力特殊上限（2026-10-20 主人拍板：60 → 100）</summary>
    public const int STAMINA_MAX = 100;
    /// <summary>新号初始体力</summary>
    public const int STAMINA_START = 60;
    /// <summary>每次点「冒险」消耗体力</summary>
    public const int STAMINA_ADVENTURE_COST = StaminaSystem.ADVENTURE_COST;
    /// <summary>回复 1 点体力所需秒数</summary>
    public const int STAMINA_REGEN_SECONDS = StaminaSystem.REGEN_SECONDS_PER_POINT;

    // ===== 广告位经济（2026-10-25 14:47 主人拍板：聚光灯已结束，转商业广告版）=====
    // 旧口径（2026-09-21「聚光灯参赛包不得出现广告」）随聚光灯计划结束作废；
    // 广告总开关恢复，但【SDK 接入并真机验收前必须保持 false】——false 时广告档折叠，
    // 金币/体力 = 2 免费 + 50/100 钻（每日 4 次）、复活 = 10 钻（现状），与线上包行为一致。
    // true = 每日档位切换为广告版（见 ResourceAdRewards.ResolveClaimKind / ReviveQuota），
    //        所有广告调用走 RewardedAdBridge 单一出口，TapADN 接入见 Docs/《TapADN激励视频接入》_待办任务清单.md。
    /// <summary>广告总开关。【2026-10-25 19:2x 主人拍板「翻」】true = 广告版阶梯生效（金币/体力每日 2免费+2广告+50/100钻；复活每日 2广告+50钻+100钻后只能返回）。
    /// 【2026-11-01 主人拍板】TapTap 端放弃、转微信小游戏：翻回 false 过渡期口径 —— 广告档全关、钻石补上
    /// （体力/金币 2免费→50钻→100钻→封顶；复活 10钻一次无上限；结算双倍/佣兵活动/钻石加号/竞技场加次按钮折叠）。
    /// 微信激励视频（P2-B）接入并验证后改回 true。</summary>
    public const bool ADS_ENABLED = false;

    /// <summary>钻石定价：顶栏体力补给一次（体力 +ResourceAdRewards.StaminaPerAd）。2026-09-20 钻石经济调整：20→15。</summary>
    public const int AD_SLOT_STAMINA_DIAMOND = 15;
    /// <summary>钻石定价：顶栏金币补给一次（金币 +ResourceAdRewards.GoldPerAd）。2026-09-20 钻石经济调整：20→10。</summary>
    public const int AD_SLOT_GOLD_DIAMOND = 10;

    /// <summary>
    /// 钻石定价：战斗内复活一次（**仅 ADS_ENABLED=false 期间的现行价**）。
    /// 【2026-10-25 14:47 主人拍板】广告版阶梯 = 每日前 2 次看广告、第 3 次 50 钻、第 4 次 100 钻，
    /// 之后死亡只能返回 —— 档位真源在 ReviveQuota，本常量只服务开关未打开的过渡期。
    /// </summary>
    public const int AD_SLOT_REVIVE_DIAMOND = 10;

    /// <summary>
    /// 战斗内复活血量比例（0.6 = 六成血）。【2026-11-11 主人拍板「只要是死了的都拉起来，恢复60%的血」】
    /// 钻石复活时<b>主角与所有阵亡佣兵</b>一律按此比例拉起，唯一真源在
    /// <c>BattleManager.DoReviveHero</c> / <c>ReviveFallenMercsOnHeroRevive</c>。想调手感只改这一个数。
    /// </summary>
    public const float REVIVE_HP_RATIO = 0.6f;

}
