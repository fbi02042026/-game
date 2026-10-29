using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 竞技场规则 · 换位制（2026-10-29 主人拍板改版，替代早上 v1 的「随机抽 5 个对手」口径）：
/// · 弹窗直接展示完整排行榜（100 机器人 + 玩家 = 101 名，可滚动），不再随机抽对手；
/// · 只能挑战排名上方的 <see cref="ChallengeableCount"/> 名（行右侧有挑战按钮），离得越远积分越多、也越难；
/// · 赢 = 与对手换位（打一次变一次排名）；输 = 不掉名但掉积分（2026-10-29 晚主人拍板）；
/// · 节奏阀门：连输 2~4 次（随机）必赢一把；连赢 3~5 次（随机）必输一把（防升太快）；
/// · 积分 = 吃分差：赢一场你的积分 ≈ 被打者档位的分 + 距离奖励，身边永远有同分的人；
///   机器人积分走分布曲线——垫底一大群 0 分，越往前同分的人越少（主人 2026-10-29 口径）。
///
/// 榜单数据结构（真源 SaveData）：
/// · <see cref="SaveData.arenaBots"/> 列表顺序就是榜单顺序（不含玩家）；
///   列表下标 i &lt; arenaPlayerPos 的机器人排名 = i+1（在玩家上方）；
///   列表下标 i ≥ arenaPlayerPos 的机器人排名 = i+2（在玩家下方）；
///   玩家排名 = arenaPlayerPos + 1。
/// · 跨天：机器人积分漂移（数值味道）+ 机器人之间随机相邻换位（排名会动，不跨玩家位）；
/// · 跨月（自然月赛季）：按最终排名发结算邮件 → 积分清零 → 机器人重新生成。
/// · 每天 10 次挑战 + 每天最多 2 次广告每次 +2。
///
/// 纯静态 Rules 类（照 EndlessRiftRules 口径：只操作 SaveData + Toast，不碰战斗核心）。
/// </summary>
public static class ArenaRules
{
    // ───────────────────────── 可调参数（改这里就够） ─────────────────────────
    /// <summary>排行榜机器人数量（加上玩家共 101 名）。2026-10-29 主人拍板 30→100：怕几天就登顶。</summary>
    public const int BotCount = 100;
    /// <summary>每天免费挑战次数。</summary>
    public const int DailyTries = 10;
    /// <summary>每看 1 次激励视频加的挑战次数。</summary>
    public const int AdBonusTries = 2;
    /// <summary>每天最多看几次激励视频。</summary>
    public const int AdViewCapPerDay = 2;
    /// <summary>只能挑战排名上方的 N 名（主人 2026-10-29 拍板：相隔的上面 3 个人）。</summary>
    public const int ChallengeableCount = 3;

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
    public static readonly int[] ChallengeBonusPoints = { 5, 10, 15 };

    // ── 机器人积分分布（2026-10-29 主人拍板：开局一大群 0 分，越往前同分的人越少）──
    /// <summary>榜首积分（天花板）。</summary>
    public const int SeasonTopScore = 1500;
    /// <summary>分布曲线顶端值（rank 2 处，量化后 1400）。</summary>
    public const float CurveTopScore = 1450f;
    /// <summary>曲线凸度 &gt;1：底部平缓（同分多）、顶部陡（同分少）。</summary>
    public const float CurveExponent = 2.2f;
    /// <summary>排名 ≥ 此值的机器人强制 0 分（加上量化，实际 0 分群约 11 个 + 开局 0 分的玩家）。</summary>
    public const int ZeroZoneStartRank = 97;
    /// <summary>积分分布/规则版本：调分布或积分规则时 +1，老档自动整榜重建（玩家按当前积分重新插位，进度不丢）。
    /// v4（2026-10-29 17:28）：机器人加战力+阵容种子，老档重生成（战力按新曲线、估计值重新起步）。</summary>
    public const int DistVersion = 4;
    /// <summary>输了掉的积分（0 分保护：积分不足只扣到 0，垫底 0 分群不会被扣成负数）。
    /// 2026-10-29 晚主人拍板「积分可以扣的再多点」：2 → 5。</summary>
    public const int LosePoints = 5;

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
    /// <summary>赢一场的金币。</summary>
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
    /// <summary>每天机器人之间随机相邻换位次数（排名会动，但不跨玩家位置）。</summary>
    public const int BotSwapsPerDay = 6;

    /// <summary>数据/次数变化事件（挑战结算、广告加次）。ArenaPopupUI 订阅刷新。</summary>
    public static event Action Changed;
    static void FireChanged() { try { Changed?.Invoke(); } catch (Exception e) { Debug.LogError("[Arena] Changed 回调异常：" + e.Message); } }

    static SaveData D => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    // ───────────────────────── 周期结算（每日 + 每月） ─────────────────────────

    /// <summary>每日 + 每月 + 换位链自检（读档收口 / 弹窗打开 / 入口点击都会调，幂等）。</summary>
    public static void EnsurePeriods(SaveData data)
    {
        ArenaSeasonWeapons.EnsureRegistered();   // 冠军武器模板注册（幂等；邮件领取/出战选装都依赖查表）
        EnsureMonth(data);
        EnsureDay(data);
        EnsureLadder(data);
    }

    /// <summary>
    /// 跨天：清零今日次数与广告计数，机器人积分漂移 + 机器人之间随机相邻换位。
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
        }
        else
        {
            DriftBots(data);
            ShuffleBots(data);
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

    /// <summary>机器人每日积分漂移：涨多跌少（只动数值不动名次，名次由 ShuffleBots 动）。
    /// 0 分垫底群不漂移——保持「很多人是 0」（主人 2026-10-29 口径）。</summary>
    static void DriftBots(SaveData data)
    {
        if (data?.arenaBots == null) return;
        for (int i = 0; i < data.arenaBots.Count; i++)
        {
            var b = data.arenaBots[i];
            if (b == null || b.points <= 0) continue;
            b.points = Mathf.Max(0, b.points + UnityEngine.Random.Range(BotDriftMin, BotDriftMax + 1));
        }
    }

    /// <summary>
    /// 机器人之间随机相邻换位（排名会动）：只在机器人内部换，不跨玩家位置
    /// （a == arenaPlayerPos-1 的那一对会一上一下跨过玩家，跳过不换）。
    /// </summary>
    static void ShuffleBots(SaveData data)
    {
        if (data?.arenaBots == null || data.arenaBots.Count < 2) return;
        int attempts = BotSwapsPerDay * 3;   // 有效换位最多 BotSwapsPerDay 次，防死循环
        int done = 0;
        while (done < BotSwapsPerDay && attempts-- > 0)
        {
            int a = UnityEngine.Random.Range(0, data.arenaBots.Count - 1);
            if (a == data.arenaPlayerPos - 1) continue;
            var t = data.arenaBots[a];
            data.arenaBots[a] = data.arenaBots[a + 1];
            data.arenaBots[a + 1] = t;
            done++;
        }
    }

    /// <summary>重新生成整批机器人（新赛季 / 首次初始化 / 分布版本升级）。
    /// 积分按排名走分布曲线：列表下标 i = 排名 i+1（榜首在前）——
    /// 垫底一大群 0 分 → 中段密集小分 → 越往前分越高越稀（主人 2026-10-29 口径）。
    /// 战力同榜位曲线（PowerForRank）；名字整批不重复（主人 2026-10-29 拍板）；
    /// 头像 22 个洗牌袋循环取样——任意连续 22 行内不重复，同屏（约 8 行）必然不重复。</summary>
    public static void RegenerateBots(SaveData data)
    {
        if (data == null) return;
        if (data.arenaBots == null) data.arenaBots = new List<ArenaBotEntry>();
        data.arenaBots.Clear();
        var bag = ShuffledAvatarBag();
        var usedNames = new HashSet<string>();
        for (int i = 0; i < BotCount; i++)
            data.arenaBots.Add(MakeBot(bag, i, ScoreForRank(i + 1), PowerForRank(i + 1), usedNames));
    }

    /// <summary>
    /// 老档扩容：BotCount 常量调大后（30→100，2026-10-29 主人拍板），已有存档机器人不够时自动补齐。
    /// 新机器人**追加到榜单末位**（玩家下方），积分按其排名走分布曲线（垫底=0 分），玩家名次不变。
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
            data.arenaBots.Add(MakeBot(bag, seq++, ScoreForRank(rank), PowerForRank(rank), usedNames));
        }
        Debug.Log($"[Arena] 榜单扩容：补到 {BotCount} 个机器人（新增的排在末位）");
    }

    /// <summary>
    /// 榜单第 rank 名的标准积分（rank 1=榜首 … BotCount=末位机器人；玩家位延伸按 0/低值处理）。
    /// 曲线：score = CurveTopScore × t^CurveExponent，t = 离榜首的归一化距离——
    /// 底部平缓（同分多）、顶部陡（同分少），rank ≥ ZeroZoneStartRank 强制 0 分。
    /// </summary>
    public static int ScoreForRank(int rank)
    {
        if (rank >= ZeroZoneStartRank) return 0;
        if (rank <= 1) return SeasonTopScore;
        float t = Mathf.Clamp01((ZeroZoneStartRank - 1 - rank) / (float)(ZeroZoneStartRank - 2));
        float raw = CurveTopScore * Mathf.Pow(t, CurveExponent);
        return QuantizeScore(raw);
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

    // ───────────────────────── 战力 → 佣兵/技能/装备 档案换算（详情弹窗展示用） ─────────────────────────

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
    /// 用战力反推机器人阵容档案（佣兵/技能/装备），seed 确定性生成——同一机器人每次打开看到的阵容一致。
    /// 权重口径对齐 PowerCalc（佣兵 lv×70+★×110、技能 rar×90+★×140），纯展示不求总和精确等于战力。
    /// 品质概率随战力上移：垫底机器人多普通/精良，前排满屏传说（「战力换算成佣兵技能和装备」）。
    /// </summary>
    public static List<LoadoutEntry> BuildBotLoadout(ArenaBotEntry bot)
    {
        var list = new List<LoadoutEntry>();
        if (bot == null) return list;
        int power = bot.power > 0 ? bot.power : (int)PowerBottom;
        var rng = new System.Random(bot.seed);

        // ── 佣兵：数量随战力 1→4；等级/星级按平摊预算反推（PowerCalc.MercPowerDelta = lv×70 + ★×110）──
        int mercCount = power < 800 ? 1 : power < 3000 ? 2 : power < 12000 ? 3 : 4;
        float mercBudget = power * 0.45f / mercCount;
        for (int i = 0; i < mercCount; i++)
        {
            int level = Mathf.Clamp(Mathf.FloorToInt(mercBudget * 0.65f / 70f), 1, 60);
            int star = Mathf.Clamp(Mathf.FloorToInt(mercBudget * 0.35f / 110f), 1, 5);
            // 佣兵稀有度口径（MercSkillMapping.StarToRarity）：★≥5 传说 / ★≥3 稀有 / 其余普通
            var rar = star >= 5 ? MercRosterDefs.MercRarity.Legendary
                    : star >= 3 ? MercRosterDefs.MercRarity.Rare
                    : MercRosterDefs.MercRarity.Common;
            list.Add(new LoadoutEntry
            {
                section = 0,
                label = $"Lv.{level} ★{star}",
                sub = "佣兵",
                color = RarityPalette.Get(rar),
                iconId = rng.Next(1, 23),
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
            if (data.arenaBots[i] != null && data.arenaBots[i].points > data.arenaPoints) pos++;
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

    /// <summary>榜单第 rowIdx 名（0 基行号）能不能挑战：在玩家上方且相隔 ≤ ChallengeableCount。</summary>
    public static bool IsChallengeable(SaveData data, int rowIdx)
    {
        if (data == null) return false;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, BotCount);
        int d = pos - rowIdx;
        return d >= 1 && d <= ChallengeableCount;
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

    /// <summary>
    /// 掷骰定输赢并结算（输赢看战力比 + 两个节奏阀门，主人 2026-10-29 拍板）：
    /// · 基础胜率 = 战力比驱动：我的战力<b>估计值</b> vs 对手战力——打赢强敌估计值上调（+5%），
    ///   输了下调（-4%），自动贴合玩家真实水平（「根据输赢情况进行调整」）；越远的对手战力越高，天然越难；
    /// · 保底：连输 PityMin~PityMax 次（随机）后下一把必赢，赢后重掷目标；
    /// · 情绪保底（2026-10-29 晚主人拍板）：新赛季开局前 NewbieWinsPerSeason 场必赢；
    ///   积分 ≤ LowScorePityAt 时接下来 LowScorePityWins 场必赢（止损回升，优先级高于熔断）；
    /// · 熔断：连赢 WinCapMin~WinCapMax 次（随机）后下一把必输，防一路平推升太快，输后重掷目标。
    /// 赢：与对手换位，积分 = 吃分差 + 距离奖励，金币入账（不弹钱包 toast，结果面板自己展示）。
    /// 输：不掉名但**掉积分**（0 分保护扣不到负数）。
    /// 返回 (won, pointsGain, goldGain, powerDelta)：powerDelta = 本局战力估计变化（结果面板显示）。
    /// </summary>
    public static bool ResolveChallenge(SaveData data, int ladderIdx, out int pointsGain, out int goldGain, out int powerDelta)
    {
        pointsGain = 0;
        goldGain = 0;
        powerDelta = 0;
        if (data == null || data.arenaBots == null) return false;
        EnsureDay(data);

        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, data.arenaBots.Count);
        int d = pos - ladderIdx;
        if (d < 1 || d > ChallengeableCount || ladderIdx < 0 || ladderIdx >= data.arenaBots.Count)
        {
            Debug.LogError($"[Arena] 非法挑战目标 ladderIdx={ladderIdx}，playerPos={pos}");
            return false;
        }

        // 保底/熔断目标懒初始化（老档字段默认 0 → 第一次挑战掷出）
        if (data.arenaPityTarget <= 0) data.arenaPityTarget = UnityEngine.Random.Range(PityMin, PityMax + 1);
        if (data.arenaWinCapTarget <= 0) data.arenaWinCapTarget = UnityEngine.Random.Range(WinCapMin, WinCapMax + 1);

        int myPower = GetPlayerPower(data);
        int botPower = GetBotPower(data, ladderIdx);
        float winRate = Mathf.Clamp(
            0.5f + (myPower - botPower) / (2f * PowerWinRateSpan * Mathf.Max(1f, botPower)),
            WinRateFloor, WinRateCeil);

        bool forceWin = data.arenaLossStreak >= data.arenaPityTarget;    // 连输够数 → 保底必赢
        bool forceLose = data.arenaWinStreak >= data.arenaWinCapTarget;  // 连赢够数 → 熔断必输
        // 必赢优先级（2026-10-29 晚主人拍板加两层情绪保底）：
        // 新赛季新手必赢 > 低积分止损必赢 > 连输保底 > 连赢熔断 > 战力掷骰。
        // 前两层必赢期间覆盖熔断（保底窗口内不许再强制输）。
        bool won;
        if (data.arenaFreshWinsLeft > 0)
        {
            won = true;
        }
        else
        {
            if (data.arenaPoints <= LowScorePityAt && data.arenaLowPityLeft <= 0)
                data.arenaLowPityLeft = LowScorePityWins;   // 掉到低分线 → 接下来 N 场必赢（止损回升）
            if (data.arenaLowPityLeft > 0) won = true;
            else if (forceWin) won = true;
            else if (forceLose) won = false;
            else won = UnityEngine.Random.value < winRate;
        }

        var opponent = data.arenaBots[ladderIdx];   // 对手引用（记录用；赢的换位分支里另有局部 bot，同一对象）

        if (won)
        {
            // 吃分差 + 距离奖励：打完你的积分 ≈ 被打者档位的分（同分的人永远在身边）
            int beatenScore = ScoreForRank(ladderIdx + 1);   // 被打者排名 = ladderIdx+1（在玩家上方）
            int myCurveScore = ScoreForRank(pos + 1);        // 我排名处的标准分（曲线对玩家位延伸为低值/0）
            pointsGain = Mathf.Max(1, beatenScore - myCurveScore + ChallengeBonusPoints[d - 1]);
            goldGain = WinGold;
            // ⚠ ResourceType 是 ResourceWallet 的嵌套枚举（ResourceWallet.cs:13），必须带前缀
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, goldGain, true, false);

            // 换位：机器人从 ladderIdx 挪到玩家旧位；中间的机器人各退一位；玩家顶到 ladderIdx
            // （RemoveAt 之后目标位下标 = 旧 playerPos - 1；玩家新位 = ladderIdx）
            var bot = data.arenaBots[ladderIdx];
            data.arenaBots.RemoveAt(ladderIdx);
            data.arenaBots.Insert(pos - 1, bot);
            data.arenaPlayerPos = ladderIdx;

            powerDelta = Mathf.RoundToInt(myPower * PowerGainOnWin);
            data.arenaPlayerPower = Mathf.Max(100, myPower + powerDelta);
            data.arenaWinStreak++;
            data.arenaLossStreak = 0;
            if (data.arenaFreshWinsLeft > 0)
                data.arenaFreshWinsLeft--;               // 新手必赢消耗一场
            else if (data.arenaLowPityLeft > 0)
                data.arenaLowPityLeft--;                 // 低分保底消耗一场
            else if (forceWin)
                data.arenaPityTarget = UnityEngine.Random.Range(PityMin, PityMax + 1);   // 保底到账 → 新一轮目标
        }
        else
        {
            pointsGain = -LosePoints;   // 掉积分（0 分保护在下面 Max）
            powerDelta = -Mathf.RoundToInt(myPower * PowerDropOnLose);
            data.arenaPlayerPower = Mathf.Max(100, myPower + powerDelta);
            data.arenaLossStreak++;
            data.arenaWinStreak = 0;
            if (forceLose)
                data.arenaWinCapTarget = UnityEngine.Random.Range(WinCapMin, WinCapMax + 1);   // 熔断执行 → 新一轮目标
        }
        data.arenaPoints = Mathf.Max(0, data.arenaPoints + pointsGain);
        AddBattleRecord(data, opponent, won, pointsGain);   // 对战记录（2026-10-29 晚主人拍板：对手/胜败/分值/时间）
        SaveSystem.Instance?.Save();
        FireChanged();
        return won;
    }

    /// <summary>记一条对战记录（新→旧，超出 BattleRecordMax 裁尾）。只记 对手头像/名字/胜败/分值/时间。</summary>
    static void AddBattleRecord(SaveData data, ArenaBotEntry bot, bool won, int points)
    {
        if (bot == null) return;
        data.arenaBattleRecords ??= new List<ArenaBattleRecord>();
        data.arenaBattleRecords.Insert(0, new ArenaBattleRecord
        {
            name = bot.name,
            avatar = bot.avatar,
            won = won,
            points = points,   // 输为负（掉积分），显示侧直接带符号
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
