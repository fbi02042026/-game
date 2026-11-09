using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 看广告送佣兵活动系统（艾拉 H015，2026-10-31 立项）。
///
/// 🔴 计数铁律（主人原话「看广告这个一定要累计好 没有功能的不要累计」）：
///   ① 只有 <see cref="RewardedAdBridge"/> 回调 success（核验通过）才计数——点击按钮、
///      拉起广告、中途关闭统统不算；
///   ② 每看满当天所需的成功广告（2026-11-09 拍板：3 起步每天 +2，按累计已计天数）才 +1 天，**当天再看不累计**
///      （每天最多记 1 天，2026-10-31 主人修正「当天看够3个累计1天再看也不累计」）；
///      累计制【非】连续制——某天没看不清零不扣减，之后哪天看了接着 +1 天（隔天看也算，
///      2026-10-31 主人原话「第二天没看 第三天看了也算第二天」）；
///      当日计数跨天随 adMercEventDayKey 失效归零（只清当日次数，不动累计天数）；
///   ③ 累计天数达到最后一档（10 天）后 <see cref="CanWatchToday"/> 恒 false——
///      后面没有任何可攒的东西，禁止继续累计；
///   ④ 四个里程碑全部领完同样停（双保险，入口按钮同步置灰）；
///   ⑤ 【2026-10-31 主人拍板】看**任意地方**的广告都可累计——金币/体力/复活等任何 placement
///      成功核验都 +1（收口 RewardedAdBridge.RewardedSuccess 全局广播，含编辑器模拟成功；
///      ①~③ 的封顶/停计规则对任来源一视同仁）。
///
/// 奖励发放：玩家点可领礼盒 → <see cref="MailSystem.Enqueue"/> 发邮件（merc / mercfrag /
/// badge 结构化附件），玩家去邮箱领取——**不直接入包**（2026-10-31 主人拍板「点击后会发送邮件给玩家」）。
/// 红点：入口键 <see cref="RedDotKey"/> + 每个礼盒键 MilestoneRedDotKey(i)，
/// 状态变化处统一走 <see cref="RefreshRedDots"/>。
/// </summary>
public static class AdMercEventSystem
{
    /// <summary>活动入口红点键（挂哪里由入口挂接方决定，暂未挂——见任务清单 T4）。</summary>
    public const string RedDotKey = "admerc";

    /// <summary>第 i 个礼盒的红点键（admerc.m0 .. admerc.m3）。</summary>
    public static string MilestoneRedDotKey(int i) => RedDotKey + ".m" + i;

    static SaveData Data => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    /// <summary>本地日历日键（yyyy-MM-dd 意义的 yyyyMMdd；与 ResourceAdRewards 等广告位同口径 0 点刷新）。</summary>
    static string TodayKey() => DateTime.Now.ToString("yyyyMMdd");

    /// <summary>
    /// 【2026-10-31 主人拍板「看任意地方的广告都可累计」】订阅广告桥的全局成功广播——
    /// 金币加号/体力/复活……任何 placement 成功核验都算本活动进度（每档封顶 3 个不变）。
    /// 活动入口自己的广告（placement=merc_event）也走同一收口，不双计。
    /// 静态构造在首次触达时挂上（红点 RefreshCommon 启动即调本类，保证订阅先于任何广告）。
    /// </summary>
    static AdMercEventSystem()
    {
        RewardedAdBridge.RewardedSuccess += OnAnyAdSuccess;
    }

    /// <summary>任意广告位成功核验 → 活动进度 +1（「没有功能的不要累计」照旧：记满/攒满/全领完一律不涨）。</summary>
    static void OnAnyAdSuccess(string placement)
    {
        var d = Data;
        if (d == null) return;
        if (IsDayCountedToday()) return;                                  // 今日 1 天已记满，再看看了也不计
        if (d.adMercEventDays >= AdMercEventDefs.MaxDay) return;          // 攒满了，无可攒
        if (AllClaimed()) return;                                         // 全领完，双保险
        // 跨天自动开新的一天计数（adMercEventDayKey 失效即归零重计；只清当日次数，不动累计天数）
        if (d.adMercEventDayKey != TodayKey())
        {
            d.adMercEventDayKey = TodayKey();
            d.adMercEventTodayAds = 0;
            d.adMercEventTodayNeed = 0;
        }
        if (d.adMercEventTodayNeed <= 0)
            d.adMercEventTodayNeed = AdMercEventDefs.AdsNeededForDays(d.adMercEventDays);
        d.adMercEventTodayAds++;
        // 看满当天所需 → 计 1 天（当天只记这一次）
        if (d.adMercEventTodayAds >= TodayNeed()
            && d.adMercEventDays < AdMercEventDefs.MaxDay)
        {
            d.adMercEventDays++;
        }
        SaveSystem.Instance.Save();
        Debug.Log($"[AdMercEvent] 广告累计（placement={placement}）今日 {d.adMercEventTodayAds}/{d.adMercEventTodayNeed} 个，累计天数 = {d.adMercEventDays}（累计制：隔天看也接着算）");
        RefreshRedDots();
    }

    // ============================================================
    // 状态查询（UI / 红点共用）
    // ============================================================

    /// <summary>当日已看成功的活动广告次数（每天最多涨到当天所需——CanWatchToday 在记满 1 天后封口；跨天读 0，不回写存档）。</summary>
    public static int TodayAdCount()
    {
        var d = Data;
        return d != null && d.adMercEventDayKey == TodayKey() ? d.adMercEventTodayAds : 0;
    }

    /// <summary>今天计 1 天所需的成功广告次数：当天已开始计数则读跨天锁定的 adMercEventTodayNeed，否则按累计已计天数现算（老档 need==0 的迁移路径）。</summary>
    public static int TodayNeed()
    {
        var d = Data;
        if (d == null) return AdMercEventDefs.AdsNeededForDays(0);
        return d.adMercEventDayKey == TodayKey() && d.adMercEventTodayNeed > 0
            ? d.adMercEventTodayNeed
            : AdMercEventDefs.AdsNeededForDays(d.adMercEventDays);
    }

    /// <summary>今日轮内已看次数（0..当天所需，即「当天已看广告 x/N」的 x；记满 1 天后停在当天所需不再涨）。</summary>
    public static int TodayAdProgress()
        => Mathf.Min(TodayAdCount(), TodayNeed());

    /// <summary>今天是否已看满当天所需（= 今日的 1 天已记上）。记满后当天再点看广告也不累计。</summary>
    public static bool IsDayCountedToday()
        => TodayAdCount() >= TodayNeed();

    /// <summary>
    /// 累计天数 → 进度条 fillAmount（主人在 AdMercEventPanel 手调的锚点 0.15/0.4/0.65/0.95，
    /// 见 AdMercEventDefs.MilestoneFillAmounts；相邻锚点间按天数线性插值，UI 传入含当日轮内进度的
    /// 小数天数可平滑推进）。
    /// </summary>
    public static float DayToFillAmount(float days)
    {
        var md = AdMercEventDefs.MilestoneDays;
        var mf = AdMercEventDefs.MilestoneFillAmounts;
        if (days <= 0f) return 0f;
        if (days >= md[md.Length - 1]) return mf[mf.Length - 1];
        for (int i = 0; i < md.Length; i++)
        {
            if (days < md[i])
            {
                float prevDay = i == 0 ? 0f : md[i - 1];
                float prevFill = i == 0 ? 0f : mf[i - 1];
                float t = (days - prevDay) / (md[i] - prevDay);
                return Mathf.Lerp(prevFill, mf[i], t);
            }
        }
        return mf[mf.Length - 1];
    }

    /// <summary>累计看广告天数。</summary>
    public static int Days
    {
        get { var d = Data; return d != null ? d.adMercEventDays : 0; }
    }

    /// <summary>累计天数是否达到第 i 个里程碑（i 越界返回 false）。</summary>
    public static bool IsReached(int i)
        => i >= 0 && i < AdMercEventDefs.MilestoneCount && Days >= AdMercEventDefs.MilestoneDays[i];

    /// <summary>第 i 个里程碑是否已领（已发过邮件）。</summary>
    public static bool IsClaimed(int i)
        => i >= 0 && i < AdMercEventDefs.MilestoneCount && ((Data?.adMercEventClaimedMask ?? 0) >> i & 1) != 0;

    /// <summary>可领取 = 天数到了 + 还没领。</summary>
    public static bool IsClaimable(int i) => IsReached(i) && !IsClaimed(i);

    /// <summary>还有没有任何可领的里程碑（入口红点口径）。</summary>
    public static bool HasClaimable()
    {
        for (int i = 0; i < AdMercEventDefs.MilestoneCount; i++)
            if (IsClaimable(i)) return true;
        return false;
    }

    /// <summary>活动是否已彻底走完（全领完 = 界面/入口可以收摊）。</summary>
    public static bool AllClaimed()
    {
        var d = Data;
        if (d == null) return false;
        for (int i = 0; i < AdMercEventDefs.MilestoneCount; i++)
            if (((d.adMercEventClaimedMask >> i) & 1) == 0) return false;
        return true;
    }

    /// <summary>
    /// 还能不能看广告攒进度。false 的四种情形：广告总开关未开（过渡期与其它广告位同口径）/
    /// 今日已看满 3 个（1 天已记上，再看看了也不累计）/ 已攒满最后一档天数 / 四档全领完——
    /// 后三条就是「没有功能的不要累计」：没有任何可攒的东西，不再放广告。
    /// 其它广告位（金币/体力/钻石加号等）与本计数互不影响。
    /// </summary>
    public static bool CanWatchToday()
    {
        if (!GameConfig.ADS_ENABLED) return false;   // 广告未接入期间活动整体折叠（入口也点不动）
        var d = Data;
        if (d == null) return false;
        if (d.adMercEventDayKey == TodayKey() && d.adMercEventTodayAds >= TodayNeed())
            return false;   // 今日 1 天已记满，明天再来看（当天多看不计）
        if (d.adMercEventDays >= AdMercEventDefs.MaxDay) return false;   // 攒满了，无可攒
        if (AllClaimed()) return false;                                  // 全领完，双保险
        return true;
    }

    // ============================================================
    // 看广告 +1 天
    // ============================================================

    /// <summary>
    /// 从活动面板点「立即观看」：只负责拉自家广告位 + 上报结果。
    /// 【2026-10-31 改】计数统一走 <see cref="RewardedAdBridge.RewardedSuccess"/> 广播
    /// （看任意地方的广告都可累计，本 placement 成功同样广播且先于本回调落账）——
    /// 这里不再自行计数，杜绝双计。onDone(ok)：true = 本次观看核验通过。
    /// </summary>
    public static void WatchAd(Action<bool> onDone = null)
    {
        if (!CanWatchToday())
        {
            // 今日已记满 / 攒满 / 全领完：UI 侧会先拦下弹 toast，这里是防御外部误调
            onDone?.Invoke(false);
            return;
        }
        RewardedAdBridge.ShowRewarded(AdMercEventDefs.AdPlacement, onDone);
    }

    // ============================================================
    // 领取里程碑 → 发邮件
    // ============================================================

    /// <summary>
    /// 领取第 i 个里程碑：构造邮件入箱 + 记领取位。返回是否成功。
    /// 邮件附件（按里程碑）：
    ///   0：艾拉本体（merc）；已拥有则折算艾拉碎片×10（mercfrag，仿每日登录口径）；
    ///   1：艾拉碎片×10；
    ///   2：艾拉碎片×20 + 中级徽记×10（badge，职业/档位从花名册 H015 反查）；
    ///   3：艾拉碎片×40 + 中级徽记×20。
    /// </summary>
    public static bool Claim(int i)
    {
        var d = Data;
        if (d == null) return false;
        if (i < 0 || i >= AdMercEventDefs.MilestoneCount) return false;
        if (!IsClaimable(i)) return false;   // 未达标 / 已领过：一律拒绝

        string mercName = MercRosterDefs.TryGetByHireId(AdMercEventDefs.EventMercId, out var mdef)
            && !string.IsNullOrEmpty(mdef.Name) ? mdef.Name : AdMercEventDefs.EventMercId;

        var mail = new MailEntry
        {
            id = Guid.NewGuid().ToString("N"),
            title = $"看广告活动·第{i + 1}阶段奖励",
            body = $"冒险者，感谢连续 {AdMercEventDefs.MilestoneDays[i]} 天捧场！\n这是「看广告送佣兵」活动第{i + 1}阶段的奖励，请查收。",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            claimed = false,
            type = MailType.Reward,
            sender = "活动",
            attachments = BuildAttachments(i),
        };
        MailSystem.Enqueue(mail);

        d.adMercEventClaimedMask |= (1 << i);
        SaveSystem.Instance.Save();
        RefreshRedDots();
        Debug.Log($"[AdMercEvent] 第{i + 1}阶段奖励已入邮（天数={d.adMercEventDays}，mask={d.adMercEventClaimedMask}）");
        return true;
    }

    /// <summary>第 i 个里程碑的邮件附件（只构造，不入箱；Claim 与 UI 预览共用口径）。</summary>
    static List<MailAttachment> BuildAttachments(int i)
    {
        var list = new List<MailAttachment>();
        string headIcon = ContentPaths.Icons.MercHead + "/" + AdMercEventDefs.EventMercId;

        switch (i)
        {
            case 0:
                if (IsMercOwned())
                {
                    // 已拥有 → 折算碎片（仿每日登录 mercOwned→折算口径，2026-10-31 默认拍板）
                    list.Add(new MailAttachment
                    {
                        type = "mercfrag",
                        amount = AdMercEventDefs.OwnedFallbackFrag,
                        hireId = AdMercEventDefs.EventMercId,
                        iconOverride = headIcon,
                    });
                }
                else
                {
                    list.Add(new MailAttachment
                    {
                        type = "merc",
                        amount = 1,
                        hireId = AdMercEventDefs.EventMercId,
                        iconOverride = headIcon,
                    });
                }
                break;

            case 1:
                list.Add(MakeFragAtt(AdMercEventDefs.FragStage1, headIcon));
                break;

            case 2:
                list.Add(MakeFragAtt(AdMercEventDefs.FragStage2, headIcon));
                AddBadge(list, AdMercEventDefs.BadgeStage2);
                break;

            case 3:
                list.Add(MakeFragAtt(AdMercEventDefs.FragStage3, headIcon));
                AddBadge(list, AdMercEventDefs.BadgeStage3);
                break;
        }
        return list;
    }

    static MailAttachment MakeFragAtt(int amount, string icon) => new MailAttachment
    {
        type = "mercfrag",
        amount = amount,
        hireId = AdMercEventDefs.EventMercId,
        iconOverride = icon,
    };

    /// <summary>
    /// 升星徽记附件：职业/档位一律从花名册 H015 行反查（游侠 + MercStarDefs.BadgeTierOf），
    /// 缺配置 = LogError 不发不吞（铁律 fail closed，与 MailSystem badge 分支同口径）。
    /// </summary>
    /// <summary>构造徽记附件并追加（构造失败已 LogError，不追加 null 进列表）。</summary>
    static void AddBadge(List<MailAttachment> list, int amount)
    {
        var att = MakeBadgeAtt(amount);
        if (att != null) list.Add(att);
    }

    static MailAttachment MakeBadgeAtt(int amount)
    {
        if (!MercRosterDefs.TryGetByHireId(AdMercEventDefs.EventMercId, out var def))
        {
            Debug.LogError($"[AdMercEvent] 花名册查不到 {AdMercEventDefs.EventMercId}，徽记附件构造失败");
            return null;
        }
        string jobKey = MercGrowSprites.JobKey(def.JobName);
        if (string.IsNullOrEmpty(jobKey))
        {
            Debug.LogError($"[AdMercEvent] 职业名「{def.JobName}」识别不出徽记职业 key，徽记附件构造失败");
            return null;
        }
        string tier = MercStarDefs.BadgeTierOf(def.Rarity);
        return new MailAttachment
        {
            type = "badge",
            amount = amount,
            defId = MercGrowInventory.BadgeId(jobKey, tier),
        };
    }

    static bool IsMercOwned()
    {
        var d = Data;
        return d != null && d.unlockedMercIds != null && d.unlockedMercIds.Contains(AdMercEventDefs.EventMercId);
    }

    // ============================================================
    // 详情文案（UI 弹窗与邮件标题共用一套口径）
    // ============================================================

    /// <summary>第 i 个阶段的短标题（礼盒下方 / 弹窗标题用）。</summary>
    public static string StageTitle(int i)
        => $"看广告满{AdMercEventDefs.MilestoneDays[i]}天";

    /// <summary>第 i 个阶段的奖励明细行（弹窗/礼盒用；与 BuildAttachments 同源，改附件必同步改这里）。
    /// 徽记档位名从花名册 H015 反查（稀有→中级），不写死——T5 改稀有度后文案自动跟着走。</summary>
    public static List<string> GetRewardLines(int i)
    {
        var lines = new List<string>();
        string tierName = GetBadgeTierDisplayName();
        switch (i)
        {
            case 0:
                lines.Add(IsMercOwned()
                    ? $"艾拉碎片 ×{AdMercEventDefs.OwnedFallbackFrag}（已拥有折算）"
                    : "艾拉（稀有·游侠）本体");
                break;
            case 1:
                lines.Add($"艾拉碎片 ×{AdMercEventDefs.FragStage1}");
                break;
            case 2:
                lines.Add($"艾拉碎片 ×{AdMercEventDefs.FragStage2}");
                lines.Add($"{tierName}徽记 ×{AdMercEventDefs.BadgeStage2}");
                break;
            case 3:
                lines.Add($"艾拉碎片 ×{AdMercEventDefs.FragStage3}");
                lines.Add($"{tierName}徽记 ×{AdMercEventDefs.BadgeStage3}");
                break;
        }
        return lines;
    }

    /// <summary>徽记档位显示名（普通→低级/稀有→中级/传奇→高级）；查不到时按稀有档默认「中级」并 LogError。</summary>
    static string GetBadgeTierDisplayName()
    {
        if (MercRosterDefs.TryGetByHireId(AdMercEventDefs.EventMercId, out var def))
        {
            string tier = MercStarDefs.BadgeTierOf(def.Rarity);
            string display = MercStarSystem.BadgeTierDisplayName(tier);
            if (!string.IsNullOrEmpty(display)) return display;
            Debug.LogError($"[AdMercEvent] 徽记档位「{tier}」拿不到显示名");
        }
        else
        {
            Debug.LogError($"[AdMercEvent] 花名册查不到 {AdMercEventDefs.EventMercId}，徽记档位名按稀有档默认");
        }
        return "中级";
    }

    // ============================================================
    // 红点
    // ============================================================

    /// <summary>
    /// 刷新本活动全部红点（入口 + 4 礼盒）。启动/看广告/领取后都要调。
    /// 【2026-10-31 主人拍板】入口红点：**只要今天还没看满就不消失**（今天还能攒就是进来的理由），
    /// 另外有可领里程碑时也亮；今天看满了且没有可领的才灭。礼盒红点仍只看「可领取」。
    /// </summary>
    public static void RefreshRedDots()
    {
        for (int i = 0; i < AdMercEventDefs.MilestoneCount; i++)
            RedDot.Set(MilestoneRedDotKey(i), IsClaimable(i));
        RedDot.Set(RedDotKey, HasClaimable() || CanWatchToday());
    }
}
