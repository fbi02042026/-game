using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 竞技场规则 · 换位制（2026-10-29 主人拍板改版，替代早上 v1 的「随机抽 5 个对手」口径）：
/// · 弹窗直接展示完整排行榜（100 机器人 + 玩家 = 101 名，可滚动），不再随机抽对手；
/// · 只能挑战排名上方的 <see cref="ChallengeableCount"/> 名（行右侧有挑战按钮），离得越远积分越多、也越难；
/// · 名次按积分（2026-10-30 20:00 主人拍板）：积分落账即按分重排，胜局升 / 败局 -10 掉名（换位制退役）；
/// · 【2026-10-31 主人拍板「只有积分有变动的时候排名才会变」】每日随机换位已删，名次只随积分落账/漂移/刷新重排；
/// · 节奏阀门：连输 2~4 次（随机）必赢一把；连赢 3~5 次（随机）必输一把（防升太快）；
/// · 积分 = 吃分差：赢一场你的积分 ≈ 被打者档位的分 + 距离奖励，身边永远有同分的人；
///   机器人积分走分布曲线——底部小分密集（同分多）、越往前同分越少，垫底保底 10 分、不再有 0 分群（2026-10-31 拍板「0 分太多」）。
///
/// 榜单数据结构（真源 SaveData）：
/// · <see cref="SaveData.arenaBots"/> 列表顺序就是榜单顺序（不含玩家）；
///   列表下标 i &lt; arenaPlayerPos 的机器人排名 = i+1（在玩家上方）；
///   列表下标 i ≥ arenaPlayerPos 的机器人排名 = i+2（在玩家下方）；
///   玩家排名 = arenaPlayerPos + 1。
/// · 跨天：机器人积分漂移（数值味道）+ 机器人之间随机相邻换位（排名会动，不跨玩家位）；
/// · 跨月（自然月赛季）：按最终排名发结算邮件 → 积分清零 → 机器人重新生成。
/// · 每天 5 次免费挑战 + 每天最多 2 次广告、每次 +3 次（2026-10-31 主人拍板：10→5、+2→+3）。
///
/// 纯静态 Rules 类（照 EndlessRiftRules 口径：只操作 SaveData + Toast，不碰战斗核心）。
/// </summary>
public static class ArenaRules
{
    // ───────────────────────── 可调参数（改这里就够） ─────────────────────────
    /// <summary>排行榜机器人数量（加上玩家共 101 名）。2026-10-29 主人拍板 30→100：怕几天就登顶。</summary>
    public const int BotCount = 100;
    /// <summary>每天免费挑战次数。【2026-10-31 主人拍板】10 → 5（配广告位：看一次 +3 次）。</summary>
    public const int DailyTries = 5;
    /// <summary>每看 1 次激励视频加的挑战次数。【2026-10-31 主人拍板】2 → 3。</summary>
    public const int AdBonusTries = 3;
    /// <summary>每天最多看几次激励视频。</summary>
    public const int AdViewCapPerDay = 2;
    /// <summary>只能挑战排名上方的 N 名（2026-10-31 主人拍板「能挑战的数量改成上面3个」：1 → 3，上方换位赛窗口）。</summary>
    public const int ChallengeableCount = 3;
    /// <summary>排名下方可挑战的 N 名（积分赛窗口）。2026-10-31 主人拍板「能挑战的数量改成上面3个」：
    /// 原「上下各 1」的下方窗口收掉（= 0），可挑战只剩上面 3 个。</summary>
    public const int ChallengeableBelowCount = 0;

    // ── 战力换算（2026-10-29 17:28 主人拍板：机器人按战力分布 + 玩家战力随输赢自适应 + 档案可视）──
    /// <summary>胜率 = 战力比驱动：0.5 + (我战力-对手战力) / (2 × Span × 对手战力)。
    /// 战力差 ±35% → 胜率 0%/100%；取代旧的固定三档胜率表（越远的对手战力越高，天然「越远越难」）。</summary>
    public const float PowerWinRateSpan = 0.35f;
    /// <summary>胜率下限（贴底也有保底阀门兜着，不至于绝望）。</summary>
    public const float WinRateFloor = 0.15f;
    /// <summary>胜率上限（留 10% 翻车空间，防止稳赢无聊）。</summary>
    public const float WinRateCeil = 0.90f;
    /// <summary>赢一把 → 我的战力估计 +5%（复利：100 把 ≈ ×131，与榜位曲线 ×133 量级匹配）。</summary>
    public const float PowerGainOnWin = 0.05f;
    /// <summary>输一把 → 我的战力估计 -4%（自适应回落，下次更容易赢）。</summary>
    public const float PowerDropOnLose = 0.04f;
    /// <summary>我的战力估计初始化领先榜位 N 名（开局比同段位机器人强一点 → 前期赢得多）。</summary>
    public const int PlayerPowerInitLead = 3;
    /// <summary>垫底（rank 101）机器人战力 ≈ 开局玩家水平（POWER_BASE+前期装备量级）。</summary>
    public const float PowerBottom = 450f;
    /// <summary>榜首机器人战力天花板。</summary>
    public const float PowerTop = 60000f;
    /// <summary>战力榜位曲线凸度（与积分曲线同款 t^2.2：底部平缓、顶部陡峭）。</summary>
    public const float PowerCurveExp = 2.2f;
    /// <summary>
    /// 胜利积分 = 与对手的积分差（吃分差）+ 距离奖励。
    /// 主人口径「每次打都应该有同样积分的」：打完你的积分 ≈ 被打者那个档位的分，
    /// 身边永远有同分的人；「越远越多」由分差（曲线越陡越大）+ 距离奖励双保险。
    /// </summary>
    // 【2026-10-30 拍板作废】原「吃分差 + 距离奖励 5/10/15」积分口径随积分版商店改版退役
    //（新口径 = 赢 +30 基础 + 连赢加成、输 +10，见 WinBasePoints / StreakBonusStep / StreakBonusCap）。
    // public static readonly int[] ChallengeBonusPoints = { 5, 10, 15 };

    // ── 机器人积分分布（2026-10-29 主人拍板：开局一大群 0 分，越往前同分的人越少）──
    /// <summary>榜首积分（天花板）。</summary>
    public const int SeasonTopScore = 1500;
    /// <summary>分布曲线顶端值（rank 2 处，量化后 1400）。</summary>
    public const float CurveTopScore = 1450f;
    /// <summary>曲线凸度 &gt;1：底部平缓（同分多）、顶部陡（同分少）。</summary>
    public const float CurveExponent = 2.2f;
    /// <summary>机器人垫底保底分（2026-10-31 主人拍板「0 分太多」：曲线尾部量化到 0 的由保底接住，机器人不再有 0 分）。</summary>
    public const int BotMinScore = 10;
    /// <summary>积分分布/规则版本：调分布或积分规则时 +1，老档自动整榜重建（玩家按当前积分重新插位，进度不丢）。
    /// v4（2026-10-29 17:28）：机器人加战力+阵容种子，老档重生成（战力按新曲线、估计值重新起步）。</summary>
    public const int DistVersion = 7;   // v7（2026-10-31）：删零分区+垫底保底 10 分（主人拍板「0 分太多」）+删每日随机换位（「只有积分变动排名才变」），老档整榜重建生效。v6（2026-10-30 20:10）：分布天花板随玩家累计场次（开局 200 每场 +15 封顶 1500）。v5：首进竞技场玩家垫底到第 101 名
    /// <summary>输了掉的积分（0 分保护：积分不足只扣到 0，垫底 0 分群不会被扣成负数）。
    /// 2026-10-29 晚主人拍板「积分可以扣的再多点」：2 → 5。</summary>
    // 【2026-10-30 拍板作废】原「输一场 -5」退役：新口径输一场也 +10（安慰奖，鼓励天天来打卡）。
    // public const int LosePoints = 5;

    // ── 积分新口径（2026-10-30 主人拍板「竞技场积分版商店」：积分 = 排名数字 + 商店消费货币）──
    /// <summary>赢一场基础积分。</summary>
    public const int WinBasePoints = 30;
    /// <summary>连赢加成：本场前连赢数 ≥1 时每场 +10。</summary>
    public const int StreakBonusStep = 10;
    /// <summary>连赢加成封顶 +30（本场前连赢 3 场起赢一场 = 60 分）。</summary>
    public const int StreakBonusCap = 30;
    /// <summary>输一场的积分变化（负=扣分；扣到 0 为止的守卫在落账处 Mathf.Max(0,…)）。
    /// 演变：-5 → +10 安慰奖（2026-10-30 拍板作废 -5）→ -10（2026-10-30 20:00 主人拍板
    /// 「失败减10、败局掉名、名次按积分」：+10 按积分排不可能掉名，扣分才掉名；低分保底兜底）。</summary>
    public const int LosePointsDelta = -10;

    // ── 情绪价值保底（2026-10-29 晚主人拍板：新手必赢 + 低积分止损必赢）──
    /// <summary>新赛季/首进竞技场：开局前 N 场必赢（「刚进来的玩家可以赢几次增强情绪价值」）。
    /// 每个赛季开始重置；老档升级到本版本时不动（只有新赛季才享）。</summary>
    public const int NewbieWinsPerSeason = 3;
    /// <summary>低积分保底线：积分 ≤ 此值时，接下来 N 场必赢（「当积分到多少的时候让玩家赢几次」）。
    /// 每场结算前检查 armed；保底打完仍 ≤ 此值会再次触发（橡皮筋，直到爬出低分区）。</summary>
    public const int LowScorePityAt = 30;
    /// <summary>低积分保底连续必赢场数。</summary>
    public const int LowScorePityWins = 3;

    // ── 保底 & 熔断（主人 2026-10-29 拍板：输了也掉分但别绝望；赢了别一路平推升太快）──
    /// <summary>连输保底下限：连输 N 次（N 在 PityMin~PityMax 随机）后下一把必赢。</summary>
    public const int PityMin = 2;
    /// <summary>连输保底上限（每轮触发保底后重掷，让节奏不可预测）。</summary>
    public const int PityMax = 4;
    /// <summary>连赢熔断下限：连赢 N 次（N 在 WinCapMin~WinCapMax 随机）后下一把必输，防上升太快。</summary>
    public const int WinCapMin = 3;
    /// <summary>连赢熔断上限（每轮触发熔断后重掷）。</summary>
    public const int WinCapMax = 5;

    // ── 阀门战力系数（2026-10-30 晚拍板「真战斗版阀门」：不掷骰判胜负，改对手战力压制/放水）──
    /// <summary>必赢/保底放水系数：新手必赢 / 连输保底 / 低分保底时敌方战力 × 此值（校准到 ≈95% 赢，可调）。</summary>
    public const float ValveEasePowerMul = 0.6f;
    /// <summary>熔断加压系数：连赢到熔断目标时敌方战力 × 此值（校准到 ≈95% 输，防升太快，可调）。</summary>
    public const float ValveHardPowerMul = 1.9f;
    /// <summary>赢一场的金币（2026-10-30 16:45 主人拍板恢复照发；走 ResourceWallet 入账，胜局结算面板有金币格）。</summary>
    public const int WinGold = 200;
    /// <summary>对战记录条数上限（新→旧存，超出裁尾；弹窗展示用，2026-10-29 晚主人拍板新增）。</summary>
    public const int BattleRecordMax = 30;

    // ── 赛季结算奖励（2026-10-29 晚主人拍板「每月奖励给的有点少」+「第 1 名给月轮换限定传奇武器」）──
    // 【2026-10-29 深夜主人再拍板】档位按主预制体里手摆的「排行奖励」面板来——5 档：1 / 2 / 3 / 4~10 / 11~50。
    // 两处改动：① 原「第 2~3 名」合并档拆成第 2 名、第 3 名两档（第 3 名 1200 金 + 80 钻）；
    //          ② 原「第 11 名及以后」收成「第 11~50 名」，<see cref="SeasonRewardTopRank"/> 名开外不发奖。
    // 面板展示文案已烤在预制体里（档位两边必须同值，改档位要一起动）。
    /// <summary>第 1 名金币。</summary>
    public const int SeasonGoldRank1 = 3000;
    /// <summary>第 1 名钻石。</summary>
    public const int SeasonDiamondRank1 = 300;
    /// <summary>第 2 名金币（只有金币+钻石，不给武器）。</summary>
    public const int SeasonGoldRank2 = 1600;
    /// <summary>第 2 名钻石。</summary>
    public const int SeasonDiamondRank2 = 100;
    /// <summary>第 3 名金币。</summary>
    public const int SeasonGoldRank3 = 1200;
    /// <summary>第 3 名钻石。</summary>
    public const int SeasonDiamondRank3 = 80;
    /// <summary>第 4~10 名金币。</summary>
    public const int SeasonGoldRank4_10 = 1000;
    /// <summary>第 4~10 名钻石。</summary>
    public const int SeasonDiamondRank4_10 = 50;
    /// <summary>第 11~50 名金币。</summary>
    public const int SeasonGoldRank11_50 = 600;
    /// <summary>第 11~50 名钻石。</summary>
    public const int SeasonDiamondRank11_50 = 0;
    /// <summary>发奖名次上限：第 11~50 名有奖，此名次开外不发（与预制体「11-50」档同值）。</summary>
    public const int SeasonRewardTopRank = 50;
    /// <summary>机器人每日积分漂移下限（有涨有跌，纯数值味道，不影响换位链）。</summary>
    public const int BotDriftMin = -3;
    /// <summary>机器人每日积分漂移上限（涨多跌少）。</summary>
    public const int BotDriftMax = 12;
    // 【2026-10-31 主人拍板「只有积分有变动的时候排名才会变」】BotSwapsPerDay / ShuffleBots（每日随机换位）已删。

    // ── 机器人分数随游玩进度水涨船高（2026-10-30 20:10 主人拍板：开局分差别太大、游玩中分数定期刷）──
    /// <summary>游玩中机器人分数刷新间隔（秒）：Open/结算时到点就刷，同一次游玩里榜单也是活的。</summary>
    public const int BotRefreshSeconds = 180;
    /// <summary>开局榜首分数（0 场时的天花板；榜单贴近玩家，避免开局被 1500 分吓退）。</summary>
    public const int StartTopPoints = 200;
    /// <summary>每打一场（累计）榜首天花板涨的分（≈87 场到顶 1500）。</summary>
    public const int TopPerBattle = 15;
    /// <summary>每次刷新机器人分数向目标曲线收敛的比例（0~1）。</summary>
    public const float BotRefreshLerp = 0.25f;

    /// <summary>数据/次数变化事件（挑战结算、广告加次）。ArenaPopupUI 订阅刷新。</summary>
    public static event Action Changed;
    static void FireChanged() { try { Changed?.Invoke(); } catch (Exception e) { Debug.LogError("[Arena] Changed 回调异常：" + e.Message); } }

    /// <summary>
    /// 战斗结算名次动效标记（2026-10-30 主人拍板「结算回来播条动效」；唯一消费口 = ArenaPopupUI.Open，读后即清零）：
    /// &gt;0 = 胜局升名位数（玩家条放大1.5倍从旧位上移砸落）；&lt;0 = 败局掉名位数（条隐藏下移到新位后出现）；0 = 没动不播。
    /// 静态字段：同进程内有效，跨进程重启自然清零（正好不播）。
    /// </summary>
    public static int LastBattleAnim;

    static SaveData D => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    // ───────────────────────── 周期结算（每日 + 每月） ─────────────────────────

    /// <summary>每日 + 每月 + 换位链自检（读档收口 / 弹窗打开 / 入口点击都会调，幂等）。</summary>
    public static void EnsurePeriods(SaveData data)
    {
        ArenaSeasonWeapons.EnsureRegistered();   // 冠军武器模板注册（幂等；邮件领取/出战选装都依赖查表）
        ArenaShopGoods.EnsureRegistered();   // 竞技场商店限定武器（逐风长弓）运行时注册（幂等；Resources 掉落池扫不到）
        EnsureMonth(data);
        EnsureDay(data);
        EnsureLadder(data);
    }

    /// <summary>
    /// 跨天：清零今日次数与广告计数，机器人积分漂移（积分变 → 按分重排）。
    /// 【2026-10-31 主人拍板「只有积分有变动的时候排名才会变」】原「机器人之间随机相邻换位 ShuffleBots」删除——
    /// 换位不动积分只动名次，正是「我没打排名就变了」的元凶；现在名次只会随积分落账/漂移/刷新重排。
    /// 首次初始化（机器人列表为空）在这里顺手生成。
    /// </summary>
    public static void EnsureDay(SaveData data)
    {
        if (data == null) return;
        string today = ShopDefs.TodayKey();
        if (data.arenaDayKey == today) return;
        data.arenaDayKey = today;
        data.arenaTriesToday = 0;
        data.arenaAdViewsToday = 0;
        if (data.arenaBots == null || data.arenaBots.Count == 0)
        {
            RegenerateBots(data);
            InitLadder(data);
            // 首进竞技场 = 「刚进来的玩家」：开局送 N 场必赢（情绪价值，2026-10-29 晚主人拍板）
            data.arenaFreshWinsLeft = NewbieWinsPerSeason;
            data.arenaLowPityLeft = 0;
            EnsureValveTargets(data);   // 保底/熔断目标开局各掷一次（真战斗版阀门，2026-10-30 晚）
        }
        else
        {
            DriftBots(data);
        }
    }

    /// <summary>
    /// 跨月（自然月赛季）：结算上赛季 → 发奖励邮件 → 积分清零 → 机器人重新生成 → 换位链重建。
    /// 首次（无机器人）只做初始化，不发结算。
    /// </summary>
    public static void EnsureMonth(SaveData data)
    {
        if (data == null) return;
        string month = DateTime.Now.ToString("yyyyMM");
        if (data.arenaMonthKey == month) return;

        bool hadSeason = data.arenaBots != null && data.arenaBots.Count > 0;
        if (hadSeason)
        {
            int finalRank = PlayerRank(data);
            var rw = RewardFor(finalRank);
            string lastMonth = DateTime.Now.AddMonths(-1).ToString("M月");
            var mail = new MailEntry
            {
                title = $"竞技场·{lastMonth}赛季结算",
                body = $"上赛季最终排名：第 {finalRank} 名\n奖励已附上，新赛季再冲！",
                sender = "竞技场组委会",
                type = MailType.Reward,
                gold = rw.gold,
                diamond = rw.diamond,
            };
            // 【2026-10-29 晚主人拍板】冠军限定武器：第 1 名随邮件发当月轮换传奇武器（仅竞技场可获得）；
            // equip 附件领取时进随身仓库，出战时若职业匹配自动替换初始主手（ArenaSeasonWeapons 口径）。
            if (finalRank <= 1)
            {
                string weaponId = ArenaSeasonWeapons.CurrentSeasonWeaponId();
                string wname = ArenaSeasonWeapons.WeaponName(weaponId);
                mail.body = $"上赛季最终排名：第 {finalRank} 名——恭喜夺冠！\n" +
                            $"冠军限定武器「{wname}」已随信附上（仅竞技场赛季冠军可获得，每月轮换），\n" +
                            "出战该职业时将自动替换初始武器。新赛季再冲！";
                mail.attachments = new List<MailAttachment>
                {
                    new MailAttachment
                    {
                        type = "equip",
                        amount = 1,
                        defId = weaponId,
                        rarity = 4,   // 邮件装备附件口径 0~4：4 = 最高档（传奇）
                        iconOverride = ArenaSeasonWeapons.MailIconPath(weaponId),
                    },
                };
            }
            if (rw.gold > 0 || rw.diamond > 0 || mail.attachments != null)
                MailSystem.Enqueue(mail, notifyToast: false);   // 读档期不发提示，进竞技场时看结算横幅
            data.arenaSeasonNotice = $"上月赛季结算：第 {finalRank} 名" +
                (rw.gold > 0 || rw.diamond > 0 ? "，奖励已发到邮箱。" : "。");
        }
        data.arenaMonthKey = month;
        data.arenaPoints = 0;
        // 新赛季开局：新手必赢重置（「刚进来的玩家可以赢几次」），低分保底清零
        data.arenaFreshWinsLeft = NewbieWinsPerSeason;
        data.arenaLowPityLeft = 0;
        EnsureValveTargets(data);   // 新赛季重掷保底/熔断目标（真战斗版阀门，2026-10-30 晚）
        RegenerateBots(data);
        InitLadder(data);
    }

    struct SeasonReward { public int from; public long gold; public int diamond; }

    /// <summary>赛季结算奖励档（按最终排名）：第 1 / 2 / 3 / 4~10 / 11~50 名，50 名开外不发奖。
    /// 数值 2026-10-29 晚上调（主人拍板「每月奖励给的有点少」）、深夜按预制体面板档位细分；
    /// 第 1 名另有月轮换限定传奇武器（见 EnsureMonth）。</summary>
    static SeasonReward RewardFor(int rank)
    {
        if (rank <= 1) return new SeasonReward { from = 1, gold = SeasonGoldRank1, diamond = SeasonDiamondRank1 };
        if (rank <= 2) return new SeasonReward { from = 2, gold = SeasonGoldRank2, diamond = SeasonDiamondRank2 };
        if (rank <= 3) return new SeasonReward { from = 3, gold = SeasonGoldRank3, diamond = SeasonDiamondRank3 };
        if (rank <= 10) return new SeasonReward { from = 4, gold = SeasonGoldRank4_10, diamond = SeasonDiamondRank4_10 };
        if (rank <= SeasonRewardTopRank)
            return new SeasonReward { from = 11, gold = SeasonGoldRank11_50, diamond = SeasonDiamondRank11_50 };
        return new SeasonReward { from = SeasonRewardTopRank + 1, gold = 0, diamond = 0 };   // 50 名开外不发
    }

    /// <summary>排行榜奖励说明文案（数值侧唯一出口，与预制体「排行奖励」面板 5 档同值，改档位只动常量区）。
    /// 2026-10-29 深夜主人拍板：面板展示改走预制体里手摆的档位行（文案已烤在预制体里），
    /// 本函数保留作数值侧文案出口（发奖文案/两边同值校验用）。
    /// 武器图标与基础属性由 <see cref="ArenaSeasonWeapons.CurrentWeaponInfoText"/>/
    /// <see cref="ArenaSeasonWeapons.CurrentWeaponIcon"/> 提供。</summary>
    public static string SeasonRewardInfoText()
    {
        var now = DateTime.Now;
        return $"每月 1 日按最终排名发奖（当前 {now.Year}年{now.Month}月赛季）：\n" +
               $"第 1 名　金币×{SeasonGoldRank1} + 钻石×{SeasonDiamondRank1} + 冠军限定武器\n" +
               $"第 2 名　金币×{SeasonGoldRank2} + 钻石×{SeasonDiamondRank2}\n" +
               $"第 3 名　金币×{SeasonGoldRank3} + 钻石×{SeasonDiamondRank3}\n" +
               $"第 4~10 名　金币×{SeasonGoldRank4_10} + 钻石×{SeasonDiamondRank4_10}\n" +
               $"第 11~50 名　金币×{SeasonGoldRank11_50}";
    }

    /// <summary>
    /// 机器人每日积分漂移：涨多跌少（2026-10-31 主人拍板「只有积分有变动的时候排名才会变」：
    /// 漂移 = 积分变动 → 漂移完立即按分重排，名次恒=积分序；重排位移丢弃——机器人涨分导致的
    /// 玩家位被动变化不播动效，动效只跟战斗结算走）。原「名次由 ShuffleBots 动」口径随换位制删除作废。
    /// 0 分不漂移——守卫保留（垫底保底分之下没有 0 分机器人，此分支正常走不到）。
    /// </summary>
    static void DriftBots(SaveData data)
    {
        if (data?.arenaBots == null) return;
        for (int i = 0; i < data.arenaBots.Count; i++)
        {
            var b = data.arenaBots[i];
            if (b == null || b.points <= 0) continue;
            b.points = Mathf.Max(0, b.points + UnityEngine.Random.Range(BotDriftMin, BotDriftMax + 1));
        }
        RepositionByPoints(data);   // 积分变 → 名次才变（唯一重排口复用）
    }

    /// <summary>
    /// 机器人分数天花板：开局 StartTopPoints，每打一场（累计）+TopPerBattle，封顶 SeasonTopScore。
    /// 场次只增不减 → 天花板单调不减；开局榜单贴近玩家分数，避免「一进来榜首 1500」的失落感
    /// （2026-10-30 20:10 主人拍板「一开始分差别太大…根据玩家玩的次数改变」）。
    /// </summary>
    public static int TopTargetFor(SaveData data)
    {
        int battles = data != null ? data.arenaBattlesTotal : 0;
        return Mathf.Min(SeasonTopScore, StartTopPoints + battles * TopPerBattle);
    }

    /// <summary>
    /// 游玩中机器人分数刷新（2026-10-30 20:10 主人拍板「机器人分值要有变化，可以一个时间刷一下，不要不变」）：
    /// 距上次刷新超过 BotRefreshSeconds 就把每个非垫底机器人向「当前天花板下的标准曲线」收敛一步
    /// （BotRefreshLerp；每日漂移 DriftBots 照旧，收敛保证整体不离曲线），再按分重排——名次恒=积分序，
    /// 机器人涨分导致玩家位被动变化不写动效标记（动效只跟战斗结算走）。
    /// 调用口 = ArenaPopupUI.Open / ApplyBattleOutcome（到点才刷，幂等）。垫底保底分以下不再有 0 分（2026-10-31 主人拍板「0 分太多」）。
    /// </summary>
    public static void RefreshBotScoresIfDue(SaveData data)
    {
        if (data?.arenaBots == null || data.arenaBots.Count == 0) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (data.arenaBotRefreshAt > 0 && now - data.arenaBotRefreshAt < BotRefreshSeconds) return;
        data.arenaBotRefreshAt = now;

        int top = TopTargetFor(data);
        for (int i = 0; i < data.arenaBots.Count; i++)
        {
            var b = data.arenaBots[i];
            if (b == null || b.points <= 0) continue;
            int target = ScoreForRankScaled(i + 1, top);   // 按当前链序排名取目标分（刷新末尾重排，下一轮自然对齐）
            b.points = Mathf.RoundToInt(Mathf.Lerp(b.points, target, BotRefreshLerp));
        }
        RepositionByPoints(data);   // 名次恒=积分序（唯一重排口复用）
        SaveSystem.Instance?.Save();
        FireChanged();
    }

    /// <summary>
    /// 重新生成整批机器人（新赛季 / 首次初始化 / 分布版本升级）。
    /// 积分按排名走分布曲线：列表下标 i = 排名 i+1（榜首在前）——
    /// 底部小分密集（同分多）→ 越往前分越高越稀（2026-10-31 主人拍板「0 分太多」后垫底最低 10 分，不再有 0 分群）。
    /// 战力同榜位曲线（PowerForRank）；名字整批不重复（主人 2026-10-29 拍板）；
    /// 头像 22 个洗牌袋循环取样——任意连续 22 行内不重复，同屏（约 8 行）必然不重复。</summary>
    public static void RegenerateBots(SaveData data)
    {
        if (data == null) return;
        if (data.arenaBots == null) data.arenaBots = new List<ArenaBotEntry>();
        data.arenaBots.Clear();
        var bag = ShuffledAvatarBag();
        var usedNames = new HashSet<string>();
        int top = TopTargetFor(data);   // 天花板随玩家累计场次（2026-10-30 20:10 主人拍板：开局贴近玩家，别一进来 1500）
        for (int i = 0; i < BotCount; i++)
            data.arenaBots.Add(MakeBot(bag, i, ScoreForRankScaled(i + 1, top), PowerForRank(i + 1), usedNames));
    }

    /// <summary>
    /// 老档扩容：BotCount 常量调大后（30→100，2026-10-29 主人拍板），已有存档机器人不够时自动补齐。
    /// 新机器人**追加到榜单末位**（玩家下方），积分按其排名走分布曲线（垫底=保底 10 分），玩家名次不变。
    /// 名字避开已有机器人的（不与现有重复）。
    /// </summary>
    static void TopUpBots(SaveData data)
    {
        var bag = ShuffledAvatarBag();
        var usedNames = new HashSet<string>();
        for (int i = 0; i < data.arenaBots.Count; i++)
            if (data.arenaBots[i] != null) usedNames.Add(data.arenaBots[i].name);
        int seq = 0;
        while (data.arenaBots.Count < BotCount)
        {
            int rank = data.arenaBots.Count + 1;
            data.arenaBots.Add(MakeBot(bag, seq++, ScoreForRankScaled(rank, TopTargetFor(data)), PowerForRank(rank), usedNames));
        }
        Debug.Log($"[Arena] 榜单扩容：补到 {BotCount} 个机器人（新增的排在末位）");
    }

    /// <summary>
    /// 榜单第 rank 名在给定天花板 top 下的标准积分。曲线：score = top×(CurveTopScore/SeasonTopScore) × t^CurveExponent，
    /// t = 离榜首的归一化距离（全榜铺满 rank2~BotCount）——底部平缓（同分多）、顶部陡（同分少）。
    /// 【2026-10-31 主人拍板「0 分太多」】原「rank ≥ ZeroZoneStartRank 强制 0 分」的零分区删除，
    /// 曲线改铺满全榜 + 垫底保底 <see cref="BotMinScore"/> 分（量化到 0 的尾巴由保底接住），机器人不再有 0 分。
    /// 整榜生成（RegenerateBots/TopUpBots）与游玩中刷新（RefreshBotScoresIfDue）都用它，天花板随玩家累计场次（TopTargetFor）。
    /// （原 ScoreForRank 满天花板版零引用已删，2026-10-30 20:10 主人拍板天花板参数化。）
    /// </summary>
    static int ScoreForRankScaled(int rank, int top)
    {
        if (rank <= 1) return top;
        float t = Mathf.Clamp01((BotCount + 1 - rank) / (float)(BotCount - 1));   // rank2≈0.99 → rankBotCount=0
        float raw = top * (CurveTopScore / SeasonTopScore) * Mathf.Pow(t, CurveExponent);
        return Mathf.Max(BotMinScore, QuantizeScore(raw));
    }

    /// <summary>按分数段取整制造同分：低分段步子小（同分多）、高分段步子大（同分少）。</summary>
    static int QuantizeScore(float v)
    {
        int step = v < 50f ? 5 : v < 200f ? 10 : v < 500f ? 25 : v < 1000f ? 50 : 100;
        return Mathf.RoundToInt(v / step) * step;
    }

    /// <summary>头像袋：H001~H022 洗牌，循环取避免相邻重图。</summary>
    static List<int> ShuffledAvatarBag()
    {
        var bag = new List<int>();
        for (int i = 1; i <= 22; i++) bag.Add(i);
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int t = bag[i]; bag[i] = bag[j]; bag[j] = t;
        }
        return bag;
    }

    /// <summary>造一个机器人：名字重掷避开 usedNames（名字池 ~1855 组合，100 个必够）；
    /// 头像按袋序循环取样；战力按榜位曲线；阵容种子给详情弹窗做确定性生成。</summary>
    static ArenaBotEntry MakeBot(List<int> avatarBag, int seq, int score, int power, HashSet<string> usedNames)
    {
        string name = PlayerNameGen.Roll();
        for (int tries = 0; tries < 40 && !usedNames.Add(name); tries++)
            name = PlayerNameGen.Roll();   // 撞名重掷（40 次全撞的概率可忽略）
        return new ArenaBotEntry
        {
            name = name,
            avatar = avatarBag[seq % avatarBag.Count],
            points = score,
            power = power,
            seed = UnityEngine.Random.Range(1, int.MaxValue),
        };
    }

    // ───────────────────────── 战力曲线（2026-10-29 17:28 主人拍板：机器人数据按战力换算） ─────────────────────────

    /// <summary>
    /// 榜单第 rank 名的标准战力：rank 1 = 榜首天花板，rank BotCount+1 = 垫底。
    /// 曲线与积分同款（t^凸度）：底部平缓（开局上方对手差距小 → 前期赢得多）、顶部陡峭（登顶冲刺难）。
    /// </summary>
    public static int PowerForRank(int rank)
    {
        if (rank <= 1) return Mathf.RoundToInt(PowerTop);
        if (rank >= BotCount + 1) return Mathf.RoundToInt(PowerBottom);
        float t = Mathf.Clamp01((BotCount + 1 - rank) / (float)BotCount);
        float raw = PowerBottom + (PowerTop - PowerBottom) * Mathf.Pow(t, PowerCurveExp);
        return QuantizePower(raw);
    }

    /// <summary>战力量化取整：低段步子小（相邻差距小，前期赢得多）、高段步子大。</summary>
    static int QuantizePower(float v)
    {
        int step = v < 1000f ? 10 : v < 5000f ? 50 : v < 20000f ? 100 : 500;
        return Mathf.RoundToInt(v / step) * step;
    }

    /// <summary>
    /// 我的战力估计（懒初始化：按当前排名领先 PlayerPowerInitLead 名起步——
    /// 主人口径「玩家大概什么样的战力可以根据输赢情况进行调整」，之后每次赢/输按百分比浮动）。
    /// </summary>
    public static int GetPlayerPower(SaveData data)
    {
        if (data == null) return 0;
        if (data.arenaPlayerPower <= 0)
            data.arenaPlayerPower = PowerForRank(Mathf.Max(1, PlayerRank(data) - PlayerPowerInitLead));
        return data.arenaPlayerPower;
    }

    /// <summary>机器人战力（老档 power≤0 时懒补榜位曲线值 + 补阵容种子；DistVersion 4 重建后一般走不到）。</summary>
    public static int GetBotPower(SaveData data, int ladderIdx)
    {
        if (data?.arenaBots == null || ladderIdx < 0 || ladderIdx >= data.arenaBots.Count) return 0;
        var b = data.arenaBots[ladderIdx];
        if (b == null) return 0;
        if (b.power <= 0) b.power = PowerForRank(RankOfIndex(data, ladderIdx));
        if (b.seed == 0) b.seed = UnityEngine.Random.Range(1, int.MaxValue);
        return b.power;
    }

    // ───────────────────────── 玩家队伍模板（2026-10-31 主人拍板） ─────────────────────────
    // 「竞技场机器人一定要有玩家的形象 不能只有佣兵；玩家形象+技能/装备/佣兵 任意根据战力的组合；
    //   设计几种玩家的队伍模板，根据玩家的抽奖战力抽取可赢或可输的模板。」
    // 模板 = 1 个「玩家形象」单位（视觉 = Units/wanjia，属性按职业 101 佣兵表）+ 模板池里的佣兵组合；
    // 阵容由 seed 确定性选模板，强度由战斗战力（GetBotBattlePower，可赢=锚玩家战力放水 / 可输=加压）反推。

    /// <summary>一个「玩家队伍模板」：玩家形象的职业属性来源 + 佣兵池（按序取）。</summary>
    public struct BotTeamTemplate
    {
        public string name;          // 模板名（日志/可读性）
        public string heroAssetId;   // 玩家形象单位的属性真源 mercId（职业 101 佣兵，与玩家同职业属性口径）
        public string[] mercAssetIds; // 模板佣兵池（按序取 unitCount-1 个；AssetId 必须在 MercRosterDefs 花名册内）
    }

    /// <summary>玩家形象单位的视觉预制体（与玩家本体同一份 SPUM 资源；属性走 mercId 查表，形象与数值解耦）。</summary>
    public const string HeroFigurePrefab = "Units/wanjia";

    /// <summary>
    /// 6 种玩家队伍模板（2026-10-31 主人拍板「不要出现没有的职业 按目前的三个职业来」）：
    /// 玩家形象职业只出当前开放的三个职业（<see cref="PlayerJobDefs.Playable"/> = 剑盾 dunbing101 /
    /// 法师 fashi101 / 游侠 gongshou101），狂战/重武/牧师形象全部下架；佣兵池只用花名册可招的同三系
    /// 佣兵 + 牧师佣兵（naima 系玩家可招募，保留）。佣兵 AssetId 全部经 MercRosterDefs 花名册核验。
    /// </summary>
    public static readonly BotTeamTemplate[] BotTeamTemplates =
    {
        new BotTeamTemplate { name = "剑盾守心", heroAssetId = "dunbing101",  mercAssetIds = new[] { "dunbing201",  "naima101" } },
        new BotTeamTemplate { name = "法师轰击", heroAssetId = "fashi101",    mercAssetIds = new[] { "naima201",    "dunbing202" } },
        new BotTeamTemplate { name = "游侠风筝", heroAssetId = "gongshou101", mercAssetIds = new[] { "gongshou201", "dunbing101" } },
        new BotTeamTemplate { name = "剑盾护卫", heroAssetId = "dunbing101",  mercAssetIds = new[] { "dunbing102",  "gongshou201" } },
        new BotTeamTemplate { name = "法师续航", heroAssetId = "fashi101",    mercAssetIds = new[] { "naima102",    "dunbing201" } },
        new BotTeamTemplate { name = "游侠游击", heroAssetId = "gongshou101", mercAssetIds = new[] { "dunbing202",  "naima101" } },
    };

    // ───────────────────────── 战力 → 佣兵/技能/装备 档案换算（详情弹窗展示用） ─────────────────────────

    /// <summary>
    /// 战力 → 单位等级/星级（2026-10-31 主人报「怎么算的战力」修正，<see cref="BuildBotLoadout"/> /
    /// <see cref="BuildBotBattleSquad"/> 共用唯一出口）：按每单位预算直接解 lv×70 + ★×110 ≈ 预算。
    /// 旧公式（预算×0.45 → 再 ×0.65/70、×0.35/110 两段打折）把 710 战力反推成 Lv2★1=250，
    /// 实际强度只有标称 3 折，「必赢阀门」因此保不住。现等级吃预算 55%，星级吃剩余，误差 ≤ 半星。
    /// </summary>
    static void DeriveUnitLevelStar(float perUnit, out int level, out int star)
    {
        float budget = Mathf.Max(perUnit, 70f);   // 下限 = 保底 1 级
        level = Mathf.Clamp(Mathf.FloorToInt(budget * 0.55f / 70f), 1, 60);
        star = Mathf.Clamp(Mathf.RoundToInt((budget - level * 70f) / 110f), 1, 5);
    }

    /// <summary>档案条目：文案 + 稀有度色（颜色一律取 RarityPalette，不私抄色值）。</summary>
    public struct LoadoutEntry
    {
        public int section;    // 0=佣兵阵容 1=技能 2=装备（详情弹窗分组渲染用）
        public string label;   // 主文案（佣兵=Lv/星；技能/装备=品质名）
        public string sub;     // 副文案（预留）
        public Color color;    // 稀有度色
        public int iconId;     // 佣兵头像编号（1~22）；技能/装备无图标填 0
    }

    /// <summary>
    /// 用战力反推机器人阵容档案（玩家形象+佣兵/技能/装备），seed 确定性生成——同一机器人每次打开看到的阵容一致。
    /// 【2026-10-31 主人拍板「机器人一定要有玩家的形象」】阵容 = 玩家形象（第 0 条，sub=玩家形象）+ 模板佣兵，
    /// 模板从 <see cref="BotTeamTemplates"/> 按 seed 掷一个；人数档随战力（&lt;3000=2（含原 &lt;800 档，2026-10-31
    /// 主人报「敌方怎么就一个人」抬到 2 人）/ 其余=3，封顶镜像玩家队形）。
    /// 权重口径对齐 PowerCalc（佣兵 lv×70+★×110、技能 rar×90+★×140），纯展示不求总和精确等于战力。
    /// 品质概率随战力上移：垫底机器人多普通/精良，前排满屏传说（「战力换算成佣兵技能和装备」）。
    /// ⚠ 与 <see cref="BuildBotBattleSquad"/> 逐行同源（同 seed、模板掷点各消耗一次 rng.Next，其余掷点顺序一致），
    /// 改动必须两边同步，否则详情与实战阵容分叉。
    /// </summary>
    public static List<LoadoutEntry> BuildBotLoadout(ArenaBotEntry bot)
    {
        var list = new List<LoadoutEntry>();
        if (bot == null) return list;
        int power = bot.power > 0 ? bot.power : (int)PowerBottom;
        var rng = new System.Random(bot.seed);

        // 模板掷点（与 BuildBotBattleSquad 同序同耗）
        int tplIdx = rng.Next(BotTeamTemplates.Length);
        var tpl = BotTeamTemplates[tplIdx];

        // ── 玩家形象 + 模板佣兵：人数随战力 1→3（封顶镜像玩家队形 = 形象 + ≤2 佣兵）；
        //    【2026-10-31 主人报「敌方怎么就一个人」】低分段（<800）也从 1 人抬到 2 人
        //    （玩家形象+1 佣兵），垫底对手不再光杆司令；
        //    等级/星级走 DeriveUnitLevelStar（lv×70 + ★×110 ≈ 预算，PowerCalc 口径）──
        int unitCount = power < 800 ? 2 : power < 3000 ? 2 : 3;
        for (int i = 0; i < unitCount; i++)
        {
            DeriveUnitLevelStar(power / (float)unitCount, out int level, out int star);
            // 佣兵稀有度口径（MercSkillMapping.StarToRarity）：★≥5 传说 / ★≥3 稀有 / 其余普通
            var rar = star >= 5 ? MercRosterDefs.MercRarity.Legendary
                    : star >= 3 ? MercRosterDefs.MercRarity.Rare
                    : MercRosterDefs.MercRarity.Common;
            // 第 0 位 = 玩家形象（属性真源 = 模板职业佣兵，头像也用它）；其余 = 模板佣兵池按序
            string assetId = i == 0 ? tpl.heroAssetId : tpl.mercAssetIds[(i - 1) % tpl.mercAssetIds.Length];
            int iconId = 0;
            string hireId = MercRosterDefs.ResolveHireId(assetId);
            if (!string.IsNullOrEmpty(hireId) && hireId.Length >= 4 && hireId[0] == 'H')
                iconId = int.TryParse(hireId.Substring(1), out var n) ? n : 0;
            list.Add(new LoadoutEntry
            {
                section = 0,
                // 玩家形象位在详情里点名（sub 预留字段同标，弹窗目前只渲染 label）
                label = i == 0 ? $"玩家形象 Lv.{level} ★{star}" : $"Lv.{level} ★{star}",
                sub = i == 0 ? "玩家形象" : "佣兵",
                color = RarityPalette.Get(rar),
                iconId = iconId,
            });
        }

        // ── 技能：0~3 个，稀有度档随战力上移（PowerCalc.SkillPowerDelta = rar×90 + ★×140）──
        int skillCount = power < 1500 ? rng.Next(0, 2) : power < 6000 ? rng.Next(1, 3) : rng.Next(2, 4);
        float skillUp = Mathf.Clamp01((power - 1500f) / 45000f);
        for (int i = 0; i < skillCount; i++)
        {
            int star = rng.Next(1, 4);
            double r = rng.NextDouble();
            var rar = r < skillUp * 0.35 ? SkillRarity.Legendary
                    : r < skillUp * 0.8 ? SkillRarity.Rare
                    : SkillRarity.Common;   // 三档口径：不产 Epic（主人 2026-10-05 拍板）
            list.Add(new LoadoutEntry
            {
                section = 1,
                label = $"{RarityName(rar)}技能 · {star}星",
                sub = "",
                color = RarityPalette.Get(rar),
                iconId = 0,
            });
        }

        // ── 装备：1~6 件，部位不重复，品质档随战力上移 ──
        int equipCount = power < 800 ? rng.Next(1, 3) : power < 3000 ? rng.Next(2, 4)
                       : power < 12000 ? rng.Next(3, 6) : rng.Next(4, 7);
        var slots = new List<string> { "武器", "副手", "头盔", "铠甲", "护腕", "靴子" };
        for (int i = slots.Count - 1; i > 0; i--)   // 洗牌取前 N → 部位不重复
        {
            int j = rng.Next(i + 1);
            var t = slots[i]; slots[i] = slots[j]; slots[j] = t;
        }
        float equipUp = Mathf.Clamp01((power - 800f) / 30000f);
        for (int i = 0; i < equipCount && i < slots.Count; i++)
        {
            double r = rng.NextDouble();
            var rar = r < equipUp * 0.22 ? Rarity.Legendary
                    : r < equipUp * 0.45 ? Rarity.Epic
                    : r < equipUp * 0.7 ? Rarity.Rare
                    : r < equipUp ? Rarity.Uncommon
                    : Rarity.Common;
            list.Add(new LoadoutEntry
            {
                section = 2,
                label = $"{RarityName(rar)}{slots[i]}",
                sub = "",
                color = RarityPalette.Get(rar),
                iconId = 0,
            });
        }
        return list;
    }

    /// <summary>稀有度中文名（装备五档 / 技能三档共用叫法，RarityPalette 注释同口径）。</summary>
    static string RarityName(SkillRarity r)
        => r == SkillRarity.Legendary ? "传说" : r == SkillRarity.Rare ? "稀有" : "普通";

    static string RarityName(Rarity r)
        => r == Rarity.Legendary ? "传说" : r == Rarity.Epic ? "史诗" : r == Rarity.Rare ? "稀有"
         : r == Rarity.Uncommon ? "精良" : "普通";

    // ───────────────────────── 战力倒推 · 可生成实体版（2026-10-30「竞技场真战斗」） ─────────────────────────

    /// <summary>机器人战斗阵容条目：字段可直接生成敌方实体（ArenaEnemySpawner 消费）。</summary>
    public struct BotSquadEntry
    {
        public string mercId;    // 真实 mercId = 花名册 AssetId（如 dunbing101，预制体加载/属性查表用）
        public string hireId;    // H 编号（H001~H022，头像/日志用；仅记录，敌方实体不 SetHireId 防吃到玩家养成加成）
        public int level;        // 等级（Init 直接吃）
        public int star;         // 星级 1~5（Init 重载吃，★ 进属性按 PowerCalc lv×70+★×110 口径折算）
        public string skillTier; // 技能档文案（如「传说技能 · 3星」，与详情弹窗同真源；战斗实体当前用花名册默认技能）
        public string equipQuality; // 装备品质档文案（如「史诗武器」，与详情弹窗同真源；展示对齐字段）
        /// <summary>【2026-10-31 主人拍板】true = 玩家形象单位（视觉 = Units/wanjia，属性按 mercId 查表；
        /// ArenaEnemySpawner 走 SpawnEnemyHeroFigure 生成），false = 普通敌方佣兵。</summary>
        public bool isHeroFigure;
    }

    /// <summary>
    /// 战力倒推出可生成实体的战斗阵容（玩家形象+佣兵+技能档+装备品质），seed 确定性——
    /// 同一机器人每次打阵容一致，与详情弹窗 <see cref="BuildBotLoadout"/> 同真源（主人拍板「详情看到的=打起来遇到的」）。
    /// 【2026-10-31 主人拍板】阵容 = 玩家形象（第 0 位，视觉 = <see cref="HeroFigurePrefab"/>，
    /// 属性/技能按模板职业佣兵查表）+ 模板佣兵（<see cref="BotTeamTemplates"/> 按 seed 掷一个）；
    /// 人数档按<b>展示战力</b>（bot.power，与详情弹窗同口径 → 阀门只动数值不动阵容），
    /// 等级/星级按<b>战斗战力</b>反推。
    /// <paramref name="battlePower"/> = 战斗战力（唯一传入方 ArenaEnemySpawner，取 <see cref="GetBotBattlePower"/>；
    /// ≤0 = 不缩放，直接用 bot.power）。放水/加压锚玩家抽奖战力 → 敌方数值低于/高于玩家当前构筑。
    /// ⚠ 与 BuildBotLoadout 逐行同源（同 seed、模板掷点各消耗一次 rng.Next，其余掷点顺序一致），改动必须两边同步。
    /// 映射不到的 AssetId 跳过并 LogError 不静默（铁律 14）。
    /// </summary>
    public static List<BotSquadEntry> BuildBotBattleSquad(ArenaBotEntry bot, int battlePower = 0)
    {
        var squad = new List<BotSquadEntry>();
        if (bot == null) return squad;
        int displayPower = bot.power > 0 ? bot.power : (int)PowerBottom;
        if (battlePower <= 0) battlePower = displayPower;   // 不传 = 无阀门，按榜位战力原样
        var rng = new System.Random(bot.seed);

        // 模板掷点（与 BuildBotLoadout 同序同耗）
        int tplIdx = rng.Next(BotTeamTemplates.Length);
        var tpl = BotTeamTemplates[tplIdx];

        // ── 玩家形象 + 模板佣兵：人数档按展示战力（详情=实战，低分段同样 ≥2 人，见 BuildBotLoadout），
        //    强度按战斗战力反推（DeriveUnitLevelStar，lv×70+★×110 ≈ 预算）──
        int unitCount = displayPower < 800 ? 2 : displayPower < 3000 ? 2 : 3;
        for (int i = 0; i < unitCount; i++)
        {
            DeriveUnitLevelStar(battlePower / (float)unitCount, out int level, out int star);
            string assetId = i == 0 ? tpl.heroAssetId : tpl.mercAssetIds[(i - 1) % tpl.mercAssetIds.Length];
            if (!MercRosterDefs.TryGetByAssetId(assetId, out var def) || string.IsNullOrEmpty(def.AssetId))
            {
                // 模板表配错（AssetId 不在花名册）：报出来不静默（铁律 14）；英雄位缺失直接断言级别报错
                Debug.LogError($"[Arena] 机器人阵容模板「{tpl.name}」第 {i} 位 AssetId={assetId} 在 MercRosterDefs 查不到，该单位跳过");
                continue;
            }
            squad.Add(new BotSquadEntry
            {
                mercId = def.AssetId,
                hireId = def.HireId,
                level = level,
                star = star,
                isHeroFigure = i == 0,
                // 技能档/装备品质与 BuildBotLoadout 的展示文案同源（下面技能/装备段的掷点也保持同序）
                skillTier = null,
                equipQuality = null,
            });
        }

        // ── 技能：0~3 个，稀有度档随战力上移（掷点顺序与 BuildBotLoadout 完全一致；战力用战斗战力）──
        int skillCount = battlePower < 1500 ? rng.Next(0, 2) : battlePower < 6000 ? rng.Next(1, 3) : rng.Next(2, 4);
        float skillUp = Mathf.Clamp01((battlePower - 1500f) / 45000f);
        var skillTiers = new List<string>();
        for (int i = 0; i < skillCount; i++)
        {
            int star = rng.Next(1, 4);
            double r = rng.NextDouble();
            var rar = r < skillUp * 0.35 ? SkillRarity.Legendary
                    : r < skillUp * 0.8 ? SkillRarity.Rare
                    : SkillRarity.Common;   // 三档口径：不产 Epic（主人 2026-10-05 拍板）
            skillTiers.Add($"{RarityName(rar)}技能 · {star}星");
        }

        // ── 装备：1~6 件，部位不重复，品质档随战力上移（掷点顺序与 BuildBotLoadout 完全一致）──
        int equipCount = battlePower < 800 ? rng.Next(1, 3) : battlePower < 3000 ? rng.Next(2, 4)
                       : battlePower < 12000 ? rng.Next(3, 6) : rng.Next(4, 7);
        var slots = new List<string> { "武器", "副手", "头盔", "铠甲", "护腕", "靴子" };
        for (int i = slots.Count - 1; i > 0; i--)   // 洗牌取前 N → 部位不重复
        {
            int j = rng.Next(i + 1);
            var t = slots[i]; slots[i] = slots[j]; slots[j] = t;
        }
        float equipUp = Mathf.Clamp01((battlePower - 800f) / 30000f);
        var equipTiers = new List<string>();
        for (int i = 0; i < equipCount && i < slots.Count; i++)
        {
            double r = rng.NextDouble();
            var rar = r < equipUp * 0.22 ? Rarity.Legendary
                    : r < equipUp * 0.45 ? Rarity.Epic
                    : r < equipUp * 0.7 ? Rarity.Rare
                    : r < equipUp ? Rarity.Uncommon
                    : Rarity.Common;
            equipTiers.Add($"{RarityName(rar)}{slots[i]}");
        }

        // 技能档/装备品质按顺序回填（展示对齐字段：详情弹窗第 N 条技能/装备 = 这里第 N 条）
        for (int i = 0; i < squad.Count; i++)
        {
            var e = squad[i];
            if (i < skillTiers.Count) e.skillTier = skillTiers[i];
            if (i < equipTiers.Count) e.equipQuality = equipTiers[i];
            squad[i] = e;
        }
        return squad;
    }

    // ───────────────────────── 阀门战力系数（2026-10-30 晚「真战斗版」） ─────────────────────────

    /// <summary>保底/熔断目标懒初始化（老档 ≤0 / 新档首次挑战时各掷一次；只掷「目标场次」，不定任何一场胜负）。</summary>
    static void EnsureValveTargets(SaveData data)
    {
        if (data == null) return;
        if (data.arenaPityTarget <= 0)
            data.arenaPityTarget = UnityEngine.Random.Range(PityMin, PityMax + 1);
        if (data.arenaWinCapTarget <= 0)
            data.arenaWinCapTarget = UnityEngine.Random.Range(WinCapMin, WinCapMax + 1);
    }

    /// <summary>
    /// 阀门战力系数（消费口 = <see cref="GetBotBattlePower"/>，间接只服务 ArenaEnemySpawner 组队；🔴 严禁进 IsChallengeable/CanEnter 等查询函数）：
    /// 按门状态给敌方战力乘系数——新手必赢 / 连输保底 / 低分保底 → 放水（<see cref="ValveEasePowerMul"/>）；
    /// 连赢到熔断目标 → 加压（<see cref="ValveHardPowerMul"/>）。胜负仍由真实战斗产出，系数只调敌我难度
    /// （主人 2026-10-30 16:45 拍板「不要掷骰的，就是真实战斗」）。
    /// </summary>
    public static float GetValvePowerMultiplier(SaveData data)
    {
        if (data == null) return 1f;
        EnsureValveTargets(data);
        if (data.arenaFreshWinsLeft > 0) return ValveEasePowerMul;                        // 新手必赢（前 N 场）
        if (data.arenaLossStreak >= data.arenaPityTarget) return ValveEasePowerMul;       // 连输保底（连输 2~4 必回血）
        if (data.arenaLowPityLeft > 0) return ValveEasePowerMul;                          // 低分保底（LowScorePityAt 口径）
        if (data.arenaWinStreak >= data.arenaWinCapTarget) return ValveHardPowerMul;      // 连赢熔断（连赢 3~5 必断）
        return 1f;
    }

    /// <summary>
    /// 机器人的「战斗战力」（唯一消费口 = ArenaEnemySpawner 组队，喂 <see cref="BuildBotBattleSquad"/>）：
    /// 【2026-10-31 主人拍板「根据玩家的抽奖战力抽取可赢或可输的模板」】阀门生效时（放水/加压），
    /// 锚点从机器人榜位战力改成<b>玩家本局抽奖战力</b>（RunLoadout.TotalPower，抽卡构筑的真实战力）——
    /// · 可赢门（新手必赢/连输保底/低分保底）：战斗战力 = 玩家抽奖战力 × 0.6（必低于玩家当前构筑，可赢）；
    /// · 可输门（连赢熔断）：战斗战力 = 玩家抽奖战力 × 1.9（必高于玩家当前构筑，可输）；
    /// · 无阀门：机器人榜位战力原样（势均力敌，照榜位曲线走）。
    /// 玩家战力取不到（≤0，异常局）→ 退回机器人榜位战力 × 系数（旧口径，不静默造数）。
    /// </summary>
    public static int GetBotBattlePower(SaveData data, ArenaBotEntry bot)
    {
        if (bot == null) return 0;
        int botPower = bot.power > 0 ? bot.power : (int)PowerBottom;
        float mul = GetValvePowerMultiplier(data);
        if (mul == 1f) return botPower;
        // 唯一消费口在战斗内（RunLoadout 已装配）；城镇等局外不该走到阀门分支——
        // TotalPower 自带「取不到按 0 + LogError」的纪律分支，这里直接认它的返回值。
        int playerPower = RunLoadout.TotalPower();
        int anchor = playerPower > 0 ? playerPower : botPower;
        return Mathf.Max(1, Mathf.RoundToInt(anchor * mul));
    }

    // ───────────────────────── 换位链（榜单顺序） ─────────────────────────

    /// <summary>
    /// 换位链初始化/迁移：arenaPlayerPos 非法（&lt;0 或 &gt;机器人总数）时，
    /// 按积分从高到低排机器人、玩家按当前积分插到对应位（0 分垫底）。
    /// 早上 v1 只存过积分的老档读进来也走这里，进度不丢。
    /// </summary>
    static void EnsureLadder(SaveData data)
    {
        if (data == null) return;
        if (data.arenaBots == null) data.arenaBots = new List<ArenaBotEntry>();

        // 分布/规则版本升级（v2：曲线分布 + 吃分差）→ 整榜重建，玩家按当前积分重新插位（进度不丢）
        if (data.arenaDistVersion < DistVersion)
        {
            RegenerateBots(data);
            InitLadder(data);
            data.arenaDistVersion = DistVersion;
            SaveSystem.Instance?.Save();
            return;
        }

        int count = data.arenaBots.Count;
        if (count > 0 && count < BotCount) TopUpBots(data);   // 改 BotCount 后老档自动补齐（同版本内）
        count = data.arenaBots.Count;
        if (data.arenaPlayerPos >= 0 && data.arenaPlayerPos <= count) return;
        InitLadder(data);
    }

    static void InitLadder(SaveData data)
    {
        if (data?.arenaBots == null) return;
        data.arenaBots.Sort((a, b) => b.points.CompareTo(a.points));
        int pos = 0;
        for (int i = 0; i < data.arenaBots.Count; i++)
            if (data.arenaBots[i] != null && data.arenaBots[i].points >= data.arenaPoints) pos++;   // 2026-10-30 主人拍板：同分也算——首进 0 分垫底到第 101 名（最后一名），不再插到 0 分群上面
        data.arenaPlayerPos = Mathf.Clamp(pos, 0, data.arenaBots.Count);
    }

    /// <summary>我的当前排名 = 换位链位置 + 1。</summary>
    public static int PlayerRank(SaveData data)
    {
        if (data == null) return 1;
        int count = data.arenaBots != null ? data.arenaBots.Count : 0;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, count);
        return pos + 1;
    }

    /// <summary>列表下标 ladderIdx 的机器人当前排名（1 = 榜首；下标跨过玩家位后差一位）。</summary>
    public static int RankOfIndex(SaveData data, int ladderIdx)
    {
        if (data == null) return BotCount;
        return ladderIdx < data.arenaPlayerPos ? ladderIdx + 1 : ladderIdx + 2;
    }

    /// <summary>
    /// 榜单第 rowIdx 名（0 基行号）能不能挑战：
    /// · 上方相隔 ≤ <see cref="ChallengeableCount"/> 名（2026-10-31 拍板改 3 个）= 换位赛（赢 = 换位）；
    /// · 下方相邻 ≤ <see cref="ChallengeableBelowCount"/> 名 = 积分赛（2026-10-31 拍板「能挑战的改成上面3个」
    ///   后窗口收为 0，下方一律不可挑战）。
    /// </summary>
    public static bool IsChallengeable(SaveData data, int rowIdx)
    {
        if (data == null) return false;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, BotCount);
        if (rowIdx == pos) return false;
        int d = pos - rowIdx;
        if (d >= 1 && d <= ChallengeableCount) return true;    // 上方：换位赛
        return ChallengeableBelowCount > 0
            && rowIdx - pos >= 1 && rowIdx - pos <= ChallengeableBelowCount;   // 下方：积分赛（现窗口 0 = 关）
    }

    // ───────────────────────── 挑战 / 次数 / 广告 ─────────────────────────

    /// <summary>今日总次数 = 免费 + 广告加成；剩余 = 总 - 已用。</summary>
    public static int TriesLeft(SaveData data)
    {
        if (data == null) return 0;
        EnsureDay(data);
        int total = DailyTries + data.arenaAdViewsToday * AdBonusTries;
        return Mathf.Max(0, total - data.arenaTriesToday);
    }

    /// <summary>进本扣次数（选完职业确认出战时调用）。次数不足弹提示返回 false。</summary>
    public static bool TryConsumeChallenge()
    {
        var data = D;
        if (data == null) return false;
        EnsureDay(data);
        if (TriesLeft(data) <= 0)
        {
            GlobalToastUI.Show($"今天的挑战次数用完了（每天 {DailyTries} 次，看广告还能加）", true);
            return false;
        }
        data.arenaTriesToday++;
        SaveSystem.Instance?.Save();
        return true;
    }

    // ───────────────────────── 挑战上下文（真战斗进本快照，2026-10-30） ─────────────────────────

    /// <summary>一次竞技场挑战的对手快照（进本前由 <see cref="PrepareChallenge"/> 存，战斗侧 ArenaEnemySpawner 读）。</summary>
    public struct ChallengeContext
    {
        public bool Valid;      // 是否有进行中的挑战上下文
        public int LadderIdx;   // 榜单行号（= 机器人列表下标，与 BattleManager.ArenaChallengeRowIdx 同值）
        public string BotName;  // 对手名字（敌方佣兵名字牌用）
        public int Avatar;      // 对手头像编号（H001~H022 的数字段）
        public int Power;       // 对手战力
        public int Seed;        // 对手阵容种子（确定性）
    }

    static ChallengeContext _challenge;

    /// <summary>中途退出判负标记的 PlayerPrefs 键（值 = ladderIdx+1；0 = 无标记）。写入口 = PrepareChallenge，消费口 = ApplyBattleOutcome / SettlePendingQuitLoss。</summary>
    const string PendingLossKey = "ArenaPendingLossIdx";

    /// <summary>当前挑战上下文（战斗侧只读）。Valid=false = 没有进行中的挑战。</summary>
    public static ChallengeContext CurrentChallenge => _challenge;

    /// <summary>
    /// 进本前快照对手（2026-10-30「竞技场真战斗」新链）：把 ladderIdx + 对手（名字/头像/战力/种子）
    /// 存进静态 <see cref="_challenge"/> 供战斗侧（ArenaEnemySpawner）读，替代原「OnJobPicked 内当场掷骰」。
    /// 快照只读不改档——胜负结算（换位/积分）推迟到战斗结束由 <see cref="ApplyBattleOutcome"/> 落账。
    /// </summary>
    public static void PrepareChallenge(SaveData data, int rowIdx)
    {
        if (data == null || data.arenaBots == null || rowIdx < 0 || rowIdx >= data.arenaBots.Count)
        {
            Debug.LogError($"[Arena] PrepareChallenge 被拦：data={(data == null ? "空" : "有")} rowIdx={rowIdx}");
            _challenge = default;
            return;
        }
        var bot = data.arenaBots[rowIdx];
        if (bot == null)
        {
            Debug.LogError($"[Arena] PrepareChallenge 被拦：rowIdx={rowIdx} 机器人条目为空");
            _challenge = default;
            return;
        }
        _challenge = new ChallengeContext
        {
            Valid = true,
            LadderIdx = rowIdx,
            BotName = bot.name,
            Avatar = bot.avatar,
            Power = GetBotPower(data, rowIdx),
            Seed = bot.seed,
        };
        // 中途退出判负标记（2026-10-31 主人拍板「直接算输」）：进本即记下挑战目标，
        // 战斗正常落账（ApplyBattleOutcome 胜/负双口）时消费；没落账（中途退游戏/退战斗）→
        // 下次开竞技场弹窗 SettlePendingQuitLoss 补判负。存 PlayerPrefs 跨进程有效；+1 存储（0 行也能表达，0 = 无标记）。
        PlayerPrefs.SetInt(PendingLossKey, rowIdx + 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 真战斗结算（2026-10-30 主人拍板「竞技场真战斗」，替代原掷骰制 <c>ResolveChallenge</c>——已删）：
    /// 【2026-10-30 晚积分版商店拍板】积分新口径：赢 = +30 基础 + 连赢加成（本场前连赢 ≥1 时
    ///     min(30, 连赢数×10)——本场前连赢 0/1/2/3 场 = 30/40/50/60）；
    /// 【2026-10-30 20:00 主人拍板】输 = -10（扣分掉名；原「输 +10 不掉名」口径随「名次按积分」一并作废）；
    /// 【2026-10-30 16:45 主人拍板】金币照发 +200（WinGold，走 ResourceWallet 入账；
    ///     胜局结算面板金币格在 BattleManager.SettleArenaVictoryCells）。
    /// 【2026-10-30 晚拍板「真战斗版阀门」】阀门字段全部恢复驱动：arenaWinStreak/arenaLossStreak/
    ///     arenaFreshWinsLeft/arenaLowPityLeft/arenaWinCapTarget/arenaPityTarget——
    ///     不掷骰判胜负，门状态改由 <see cref="GetValvePowerMultiplier"/> 在组队路径给敌方战力乘系数
    ///     （🔴 严禁进 IsChallengeable/CanEnter 等查询函数）；胜负仍由真实战斗产出。
    /// 【2026-10-30 20:00 主人拍板「名次根据积分改变」】换位制退役：胜/输积分落账后统一
    ///     <see cref="RepositionByPoints"/> 按分重排（机器人按积分降序、玩家按分插位），位移写 LastBattleAnim 动效标记。
    /// 公共：AddBattleRecord / CountArenaBattle / Save / FireChanged。
    /// </summary>
    public static void ApplyBattleOutcome(SaveData data, int ladderIdx, bool won, out int pts, out int powerDelta)
    {
        pts = 0;
        powerDelta = 0;
        LastBattleAnim = 0;   // 名次动效标记每场重写（非法目标提前 return 时保持 0 = 不播）
        // 中途退出判负标记消费（2026-10-31 主人拍板「直接算输」）：胜负任一口落账 = 这场战斗已正常打完，
        // 标记就地销毁（哪怕下方目标校验失败——败了不该反复补扣）。
        if (PlayerPrefs.HasKey(PendingLossKey)) PlayerPrefs.DeleteKey(PendingLossKey);
        if (data == null || data.arenaBots == null) return;
        EnsureDay(data);
        EnsureValveTargets(data);   // 老档/新档阀门目标懒初始化（幂等）

        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, data.arenaBots.Count);
        int d = pos - ladderIdx;
        bool fromBelow = ladderIdx > pos;   // 下方目标 = 积分赛（IsChallengeable 同窗口：ChallengeableBelowCount）
        if (ladderIdx < 0 || ladderIdx >= data.arenaBots.Count
            || !(d >= 1 && d <= ChallengeableCount)
               && !(fromBelow && ChallengeableBelowCount > 0 && ladderIdx - pos <= ChallengeableBelowCount))
        {
            Debug.LogError($"[Arena] ApplyBattleOutcome 非法挑战目标 ladderIdx={ladderIdx}，playerPos={pos}");
            return;
        }

        var opponent = data.arenaBots[ladderIdx];   // 对手引用（记录用；赢的换位分支里另有局部 bot，同一对象）
        int myPower = GetPlayerPower(data);

        if (won)
        {
            // 上一把若是踩着保底线赢的（来时连输已达标）→ 保底消费完毕，重掷目标（只掷「场次」，不定胜负）
            bool pityConsumed = data.arenaLossStreak >= data.arenaPityTarget;

            // 连赢加成用「本场前」的连赢数：0/1/2/3 场 → 30/40/50/60（封顶 60）
            int streakBonus = data.arenaWinStreak >= 1
                ? Mathf.Min(StreakBonusCap, data.arenaWinStreak * StreakBonusStep)
                : 0;
            pts = WinBasePoints + streakBonus;

            // 换位制退役（2026-10-30 20:00 主人拍板「名次按积分」）：不再 RemoveAt/Insert 换位，
            // 名次统一由积分落账后的 RepositionByPoints 按分重排（上下方目标同口径）。

            // 金币照发 +200（2026-10-30 16:45 主人拍板恢复）：走 ResourceWallet 入账（save 随函数末尾统一落盘，
            // notify:false 防与结算面板金币格双提示）。败局不发金，败局 toast 现状不动。
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, WinGold, save: false, notify: false);

            powerDelta = Mathf.RoundToInt(myPower * PowerGainOnWin);
            data.arenaPlayerPower = Mathf.Max(100, myPower + powerDelta);
            data.arenaWinStreak++;                                        // 连赢计数（积分加成 + 熔断门共用）
            data.arenaLossStreak = 0;                                     // 赢即断连输链
            if (data.arenaFreshWinsLeft > 0) data.arenaFreshWinsLeft--;   // 新手必赢消耗
            if (data.arenaLowPityLeft > 0) data.arenaLowPityLeft--;       // 低分保底消耗
            if (pityConsumed)
                data.arenaPityTarget = UnityEngine.Random.Range(PityMin, PityMax + 1);   // 保底打完重掷（橡皮筋同款节奏）
        }
        else
        {
            pts = LosePointsDelta;   // 输一场 -10（2026-10-30 20:00 主人拍板「失败减10」：扣分后按积分重排自然掉名；低分保底兜底）
            powerDelta = -Mathf.RoundToInt(myPower * PowerDropOnLose);
            data.arenaPlayerPower = Mathf.Max(100, myPower + powerDelta);
            data.arenaWinStreak = 0;    // 连赢清零（输一把断连）
            data.arenaLossStreak++;     // 连输计数（保底门用）
            data.arenaWinCapTarget = UnityEngine.Random.Range(WinCapMin, WinCapMax + 1);   // 连赢链断 → 重掷熔断目标
        }
        // 低分保底（armed 口径）：积分落账后仍 ≤ LowScorePityAt 且无进行中保底 → 再送 N 场必赢（橡皮筋，直到爬出低分区）
        data.arenaPoints = Mathf.Max(0, data.arenaPoints + pts);   // 守卫：积分永不扣成负数
        LastBattleAnim = RepositionByPoints(data);   // 积分定名次（2026-10-30 20:00 主人拍板）：落账即重排，位移写动效标记（正=升/负=降/0=没动）
        data.arenaBattlesTotal++;                    // 累计场次（机器人分数天花板驱动，2026-10-30 20:10 主人拍板「根据玩家玩的次数改变」）
        RefreshBotScoresIfDue(data);                 // 游玩中到点刷机器人分（Open/结算双口幂等；到点才刷）
        if (data.arenaPoints <= LowScorePityAt && data.arenaLowPityLeft <= 0)
            data.arenaLowPityLeft = LowScorePityWins;
        AddBattleRecord(data, opponent, won, pts);   // 对战记录（对手/胜败/分值/时间；败局也记 +10）
        data.CountArenaBattle();   // 每日任务「对战 N 场」计数：一场一计，不论胜负
        SaveSystem.Instance?.Save();
        FireChanged();
    }

    /// <summary>
    /// 积分定名次（2026-10-30 20:00 主人拍板「名次根据积分改变，玩家只要积分有变动就会改变名次」唯一重排口）：
    /// 机器人按积分降序重排链序，玩家按积分插位（同分玩家排同分机器人之后，垫底 0 分群口径与既有分布一致）。
    /// 返回名次位移（正=上升位数 / 负=下降 / 0=没动）。两个调用口：战斗结算（位移写 LastBattleAnim 播名次动效）、
    /// 游玩中机器人分刷新 RefreshBotScoresIfDue（位移丢弃——机器人涨分导致玩家位被动变化不播动效，动效只跟战斗结算走）。
    /// </summary>
    static int RepositionByPoints(SaveData data)
    {
        int oldPos = Mathf.Clamp(data.arenaPlayerPos, 0, data.arenaBots.Count);
        data.arenaBots.Sort((a, b) => b.points.CompareTo(a.points));
        int newPos = 0;
        for (int i = 0; i < data.arenaBots.Count; i++)
        {
            if (data.arenaBots[i].points > data.arenaPoints) newPos++;
            else break;
        }
        newPos = Mathf.Clamp(newPos, 0, data.arenaBots.Count);
        data.arenaPlayerPos = newPos;
        return oldPos - newPos;
    }

    /// <summary>
    /// 中途退出判负补账（2026-10-31 主人拍板「是不是应该算跳过战斗或者直接算输」→ 直接算输）：
    /// 有未落账的挑战标记（PrepareChallenge 写、战斗正常落账消费）→ 按 ApplyBattleOutcome 败局口径
    /// 结算（-10 分掉名 / 连赢断链 / 战力回落，唯一出口不另写第二套败局账）+ toast 告知。
    /// 调用口 = ArenaPopupUI.Open（进弹窗先补账再刷榜）。返回是否实际结算。
    /// 注意：跨天漂移后目标可能不在可挑战窗口内 → ApplyBattleOutcome 自校验 LogError 拒绝，
    /// 此时标记已消费不再补扣（fail closed，不软锁）。
    /// </summary>
    public static bool SettlePendingQuitLoss()
    {
        if (!PlayerPrefs.HasKey(PendingLossKey)) return false;
        int idx = PlayerPrefs.GetInt(PendingLossKey) - 1;
        PlayerPrefs.DeleteKey(PendingLossKey);
        var data = D;
        if (data == null || data.arenaBots == null || idx < 0 || idx >= data.arenaBots.Count)
        {
            Debug.LogError($"[Arena] 中途退出判负补账被拦：data={(data == null ? "空" : "有")} idx={idx}");
            return false;
        }
        ApplyBattleOutcome(data, idx, false, out int pts, out _);
        GlobalToastUI.Show($"上次挑战中途退出，判负 {pts} 分", true);
        return true;
    }

    /// <summary>
    /// 竞技场商店扣积分（2026-10-30 积分版商店唯一扣费出口）：
    /// 余额不足返回 false（调用方弹「积分不足」），守卫禁负数；扣积分<b>不动换位链/排名</b>
    /// （排名真源 = arenaPlayerPos，EnsureLadder 只在版本升级/新赛季重建时按分插位，平时不读积分）。
    /// </summary>
    public static bool TrySpendPoints(SaveData data, int amount)
    {
        if (data == null || amount <= 0) return false;
        if (data.arenaPoints < amount) return false;   // 守卫：不足不扣，禁负数
        data.arenaPoints -= amount;
        SaveSystem.Instance?.Save();
        FireChanged();
        return true;
    }

    /// <summary>记一条对战记录（新→旧，超出 BattleRecordMax 裁尾）。只记 对手头像/名字/胜败/分值/时间。
    /// 【2026-10-30 20:00 拍板】points = 当场积分变化（胜 30~60 正 / 败 -10 负），显示侧统一带符号。</summary>
    static void AddBattleRecord(SaveData data, ArenaBotEntry bot, bool won, int points)
    {
        if (bot == null) return;
        data.arenaBattleRecords ??= new List<ArenaBattleRecord>();
        data.arenaBattleRecords.Insert(0, new ArenaBattleRecord
        {
            name = bot.name,
            avatar = bot.avatar,
            won = won,
            points = points,   // 胜 30~60（正）/ 败 -10（负），显示侧带符号（2026-10-30 20:00 拍板）
            time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        if (data.arenaBattleRecords.Count > BattleRecordMax)
            data.arenaBattleRecords.RemoveRange(BattleRecordMax, data.arenaBattleRecords.Count - BattleRecordMax);
    }

    /// <summary>
    /// 看激励视频加挑战次数（每天最多 AdViewCapPerDay 次，每次 +AdBonusTries）。
    /// 回调内重新对日 + 重验上限，防跨天/连点超发（照 ResourceAdRewards 口径）。
    /// </summary>
    public static void RequestAdBonus()
    {
        var data = D;
        if (data == null) return;
        EnsureDay(data);
        if (data.arenaAdViewsToday >= AdViewCapPerDay)
        {
            GlobalToastUI.Show("今天的广告次数已经看完啦", true);
            return;
        }
        if (AdKillSwitch.AdsOff)
        {
            GlobalToastUI.Show("广告维护中，请稍后再试", true);
            return;
        }
        GlobalToastUI.Show("广告加载中…");
        RewardedAdBridge.ShowRewarded("arena_tries", ok =>
        {
            if (!ok)
            {
                GlobalToastUI.Show("激励未完成，次数未发放", true);
                return;
            }
            EnsureDay(data);   // 广告窗口期可能跨天，回调内重对
            if (data.arenaAdViewsToday >= AdViewCapPerDay) return;
            data.arenaAdViewsToday++;
            SaveSystem.Instance?.Save();
            GlobalToastUI.Show($"挑战次数 +{AdBonusTries}！", false);
            FireChanged();
        });
    }

    /// <summary>取走并清空赛季结算横幅（打开弹窗时消费一次）。</summary>
    public static string TakeSeasonNotice(SaveData data)
    {
        if (data == null || string.IsNullOrEmpty(data.arenaSeasonNotice)) return "";
        string s = data.arenaSeasonNotice;
        data.arenaSeasonNotice = "";
        SaveSystem.Instance?.Save();
        return s;
    }
}
