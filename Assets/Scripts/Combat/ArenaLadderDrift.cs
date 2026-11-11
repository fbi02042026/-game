using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 竞技场「离线期间榜单会动」结算器（2026-11-11 主人拍板）。
///
/// 主人口径：
/// · 关掉游戏离线一段时间再回来，名次不能原地不动；玩家可以掉分，但**必须解释成「被机器人打了」**（不是凭空扣分）；
/// · 机器人积分随时间只会更高：同一赛季内单调上升，叠加小幅确定性波动（涨多涨少，不每次重摇）；
/// · 离线很短（不足 1 小时）不结算；开关游戏不能刷分（同一段时间只结算一次）。
///
/// 实现要点：
/// 1) 结算基线 = <see cref="SaveData.arenaLadderSettleAt"/>（新增字段，0 时回落到 lastSaveTime = 上次在线），
///    结算后只推进「已消费的天数」，不足 1 天的零头留到下次累计 → 短时间反复开关不会额外触发；
/// 2) 按整天结算（粒度 = 1 天），单次最多累计 <see cref="SettleMaxDays"/> 天；
/// 3) 玩家：每天按 <see cref="PlayerDefenseChancePercent"/> 概率被 1~<see cref="PlayerDefenseMaxPerDay"/> 个上方机器人挑战，
///    每次防守按 <see cref="DefenseSuccessPercent"/>% 概率守住（不扣分，记录章「守」），守不住按既有败局口径
///    <see cref="ArenaRules.LosePointsDelta"/> 扣分（0 分下限，记录章「破」）；两种结果都写对战记录（2026-11-11 主人拍板「有输有赢，要有随机感」）；
/// 4) 机器人：每天 +基础涨幅 + 「积分/250」的段位加成 + ±<see cref="BotDailyJitter"/> 的确定性波动；
///    波动 = FNV(bot.seed, 当天序号)，同一机器人同一时间段结果固定；净涨幅钳到 ≥0 → 积分只会更高；
/// 5) 结算后按积分降序落位（与 ArenaRules.InitLadder 同口径：同分也算在玩家上方）。
///
/// 并发约束：本文件不改 ArenaRules.cs / ArenaEnemySpawner.cs，只读它们的公开常量与只读查询；
/// 接入点 = SaveSystem 读档收口（进游戏）+ ArenaPopupUI.Open（打开竞技场），幂等。
/// </summary>
public static class ArenaLadderDrift
{
    // ───────────────────────── 集中常量（调数值只改这里） ─────────────────────────
    /// <summary>最小结算间隔（秒）：不足 1 小时一律不动，防「关掉再开」刷名次/刷记录。</summary>
    public const int SettleMinSeconds = 3600;

    /// <summary>一天的秒数（按整天结算：不足 1 天不结算，零头留到下次累计）。</summary>
    public const int SecondsPerDay = 86400;

    /// <summary>单次最多累计几天：离线一个月也只按 7 天算，避免回归直接掉穿（下限由 0 分保护兜住）。</summary>
    public const int SettleMaxDays = 7;

    /// <summary>每天「被机器人挑战」的概率（%）。60 → 平均每天 1.2 场防守（0 场 40% / 1~3 场 60%）。</summary>
    public const int PlayerDefenseChancePercent = 60;

    /// <summary>每天最多被几个机器人挑战（每次被破按既有败局口径扣 <see cref="ArenaRules.LosePointsDelta"/>）。</summary>
    public const int PlayerDefenseMaxPerDay = 3;

    /// <summary>防守成功率（%）：被挑战时按此概率守住（不扣分，记录章「守」），守不住才扣分（章「破」）。
    /// 2026-11-11 主人拍板「机器人打玩家要有输有赢，要有随机感」；50 → 平均每天实际掉分约为原来一半。
    /// 成败 roll 用确定性哈希：同 (天, 场次) 恒定，关掉重开不重摇、不可刷。</summary>
    public const int DefenseSuccessPercent = 50;

    // ── 积分一律 10 的整数倍（2026-11-11 主人拍板：榜上不能出现 1 点 2 点这种假数）──
    /// <summary>所有竞技场积分（玩家/机器人/记录）统一量化步长，纯 10 倍数。</summary>
    public const int PointsQuantize = 10;

    /// <summary>机器人每日基础涨幅（10 起步，保持 10 的倍数；另有「积分/500」段位加成）。</summary>
    public const int BotDailyGrowthBase = 10;

    /// <summary>机器人每多少积分每天多涨 10 分：500 → 500 分 +10/天、1000 分 +20/天，榜整体水涨船高。</summary>
    public const int BotDailyGrowthPerPoints = 500;

    /// <summary>机器人每日确定性波动幅度（±10 或 ±20；纯 10 倍数）。净涨幅钳 ≥0 → 积分只会更高。</summary>
    public const int BotDailyJitter = 10;

    /// <summary>玩家被挑战时的稳定盐（FNV 种子，换个值 → 每天的挨打节奏整体换一套，一般不用改）。</summary>
    const int PlayerSalt = 0x4152454E;   // "AREN"

    /// <summary>「中途退出判负」待落账标记（只读判断；键名与 ArenaRules.PendingLossKey 一致，那边是 private）。</summary>
    const string PendingLossKey = "ArenaPendingLossIdx";

    // ───────────────────────── 对外入口 ─────────────────────────

    /// <summary>
    /// 按离线时间结算一次（幂等：同一段时间只结一次；不足 1 小时 / 不足 1 天 / 结算冻结期都直接返回）。
    /// </summary>
    /// <param name="nowUnix">测试注入用；≤0 取当前 UTC 秒。</param>
    /// <returns>是否真的发生了结算（用于日志/自测断言）。</returns>
    public static bool SettleIfDue(SaveData data, long nowUnix = 0)
    {
        if (data == null || data.arenaBots == null || data.arenaBots.Count == 0) return false;
        if (data.arenaPlayerPos < 0) return false;   // 换位链还没初始化（交给 ArenaRules.EnsureLadder）

        long now = nowUnix > 0 ? nowUnix : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 结算冻结期：榜单一动不动（不推进基线，解冻后再按剩余时间结算）
        if (ArenaSeasonClock.IsFrozen(now)) return false;

        // 有「中途退出判负」待落账时先不漂移：那份账要用原榜位落（ArenaRules.SettlePendingQuitLoss）,
        // 漂移可能把对手挤出可挑战窗口 → 账会被 fail-closed 丢掉。等它落完，下一次打开竞技场再结离线时间。
        if (PlayerPrefs.HasKey(PendingLossKey)) return false;

        long baseline = data.arenaLadderSettleAt;
        if (baseline <= 0) baseline = data.lastSaveTime;   // 老档兜底：上次存档时间 ≈ 上次在线
        if (baseline <= 0 || baseline >= now)
        {
            data.arenaLadderSettleAt = now;
            return false;
        }

        // 跨赛季：赛季已（或即将）重置榜单，离线时间由赛季结算消化，不再二次扣
        if (ArenaSeasonClock.CrossSeason(baseline, now))
        {
            data.arenaLadderSettleAt = now;
            return false;
        }

        long elapsed = now - baseline;
        if (elapsed < SettleMinSeconds) return false;   // 不足 1 小时：不结算也不推进基线，时间继续累计

        int fullDays = (int)(elapsed / SecondsPerDay);
        if (fullDays <= 0) return false;                // 按天粒度：不足 1 天不结算

        int days = Mathf.Min(fullDays, SettleMaxDays);
        int rankBefore = ArenaRules.PlayerRank(data);
        int pointsBefore = data.arenaPoints;
        int holds = 0;
        int losses = 0;
        int lostPoints = 0;

        for (int d = 0; d < days; d++)
        {
            long dayIndex = (baseline + (long)d * SecondsPerDay) / SecondsPerDay;
            SettlePlayerDefense(data, dayIndex, now, ref holds, ref losses, ref lostPoints);
            GrowBotsOneDay(data, dayIndex);
        }

        int newPos = ReorderByPoints(data);
        int rankAfter = newPos + 1;
        data.arenaLadderSettleAt = fullDays > SettleMaxDays
            ? now                                        // 超出上限：一次结清，不留下未结算的尾巴
            : baseline + (long)days * SecondsPerDay;     // 未满部分留到下次

        Debug.Log($"[Arena] 离线结算 {days} 天：被挑战 {holds + losses} 次（守住 {holds} / 被破 {losses}），" +
                  $"积分 {pointsBefore}→{data.arenaPoints}（-{lostPoints}），名次 {rankBefore}→{rankAfter}");
        SaveSystem.Instance?.Save();
        return true;
    }

    // ───────────────────────── 玩家：被机器人打 ─────────────────────────

    /// <summary>某一天的防守结算：按确定性的「挨打次数」从玩家上方挑机器人；
    /// 每次防守按 <see cref="DefenseSuccessPercent"/> 判守住/被破（2026-11-11 主人拍板「有输有赢，要有随机感」）：
    /// 守住 = 不扣分，记录章「守」；被破 = 按既有败局口径扣分，记录章「破」。</summary>
    static void SettlePlayerDefense(SaveData data, long dayIndex, long now, ref int holds, ref int losses, ref int lostPoints)
    {
        int h = StableHash(PlayerSalt, dayIndex);
        int roll = h % 100;
        if (roll < 100 - PlayerDefenseChancePercent) return;   // 今天没人来打
        int attacks = 1 + (roll % Mathf.Max(1, PlayerDefenseMaxPerDay));

        for (int i = 0; i < attacks; i++)
        {
            if (data.arenaPoints <= 0) break;   // 0 分下限：扣穿保护（垫底段位不出现负分）

            int idx = PickAttacker(data, StableHash((int)(dayIndex & 0x7FFFFFFF), i + 1));
            // 防守成败 roll：period 用 1000+i 避开挑对手的 i+1，同 (天, 场次) 恒定 → 随机感但不重摇
            bool held = StableHash((int)(dayIndex & 0x7FFFFFFF), 1000 + i) % 100 < DefenseSuccessPercent;
            if (held)
            {
                holds++;
                AddDefenseRecord(data, data.arenaBots[idx], 0, now, held: true);
                continue;
            }
            int delta = ArenaRules.LosePointsDelta < 0 ? ArenaRules.LosePointsDelta : -10;
            int actual = Mathf.Max(delta, -data.arenaPoints);   // 只扣到 0
            data.arenaPoints += actual;
            losses++;
            lostPoints += -actual;
            AddDefenseRecord(data, data.arenaBots[idx], actual, now, held: false);
        }
    }

    /// <summary>挑一个「玩家上方 1~3 名」里的机器人来挑战（叙事上就是榜上更靠前的人来打你）；榜首则从上榜者里随机挑。</summary>
    static int PickAttacker(SaveData data, int h)
    {
        int count = data.arenaBots.Count;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, count - 1);
        if (pos <= 0) return h % count;
        int above = Mathf.Min(ArenaRules.ChallengeableCount, pos);
        return Mathf.Clamp(pos - 1 - (h % above), 0, count - 1);
    }

    /// <summary>写一条「防守」对战记录（held=true=守住不扣分章「守」；false=被破扣分章「破」。
    /// 玩家在【对战记录】里能看到是谁打的他，不是凭空掉分）。</summary>
    static void AddDefenseRecord(SaveData data, ArenaBotEntry bot, int delta, long now, bool held)
    {
        if (bot == null) return;
        if (data.arenaBattleRecords == null) data.arenaBattleRecords = new List<ArenaBattleRecord>();
        data.arenaBattleRecords.Insert(0, new ArenaBattleRecord
        {
            name = bot.name,
            avatar = bot.avatar,
            won = held,
            points = delta,
            time = now,
            isDefense = true,
        });
        while (data.arenaBattleRecords.Count > ArenaRules.BattleRecordMax)
            data.arenaBattleRecords.RemoveAt(data.arenaBattleRecords.Count - 1);
    }

    // ───────────────────────── 机器人：随时间只涨 ─────────────────────────

    /// <summary>机器人在某一天的成长：基础涨幅（10 起步 + 段位×10 加成） + 确定性波动（-10/0/+10），
    /// 净涨幅钳到 ≥0（积分只会更高），最终积分量化到 10 倍数（2026-11-11 主人拍板：榜上不准出现 1 点 2 点）。</summary>
    static void GrowBotsOneDay(SaveData data, long dayIndex)
    {
        for (int i = 0; i < data.arenaBots.Count; i++)
        {
            var b = data.arenaBots[i];
            if (b == null) continue;
            // baseGrow 必然 10 的倍数：BotDailyGrowthBase=10 + (points / 500) * 10
            int baseGrow = BotDailyGrowthBase + Mathf.Max(0, b.points) / BotDailyGrowthPerPoints * PointsQuantize;
            // 波动：三档 -10 / 0 / +10（hash % 3 → 0/1/2，减 1 后乘 10）
            int jitter = (StableHash(SeedOf(b, i), dayIndex) % 3 - 1) * PointsQuantize;
            int netGrow = Mathf.Max(0, baseGrow + jitter);   // 净涨幅 ≥0：积分只会更高
            b.points = QuantizeDown(b.points + netGrow);
        }
    }

    /// <summary>向下量化到 10 的倍数（2026-11-11 主人拍板：所有竞技场积分纯 10 整数倍）。</summary>
    static int QuantizeDown(int v) => (v / PointsQuantize) * PointsQuantize;

    /// <summary>机器人确定性种子（老档 seed 缺失时退回名字哈希 + 下标，保证同机器人同时间段结果稳定）。</summary>
    static int SeedOf(ArenaBotEntry bot, int index)
    {
        if (bot.seed != 0) return bot.seed;
        return StableHash(StableHashString(bot.name), index);
    }

    // ───────────────────────── 排序落位 ─────────────────────────

    /// <summary>
    /// 按积分降序重排 + 玩家落到「积分真实应得位次」。
    /// （ArenaRules.RepositionByPoints 已是 public；离线漂移只需要"按当前积分自然落位"，
    /// 不需要 desiredGain 硬驱动——所以直接 sort + 数 count 即可，结果和 RepositionByPoints(data,0) 一致）。
    /// </summary>
    static int ReorderByPoints(SaveData data)
    {
        data.arenaBots.Sort((a, b) => (b == null ? 0 : b.points).CompareTo(a == null ? 0 : a.points));
        int pos = 0;
        for (int i = 0; i < data.arenaBots.Count; i++)
            if (data.arenaBots[i] != null && data.arenaBots[i].points >= data.arenaPoints) pos++;
        data.arenaPlayerPos = Mathf.Clamp(pos, 0, data.arenaBots.Count);
        return data.arenaPlayerPos;
    }

    // ───────────────────────── 确定性哈希（跨平台/跨帧稳定，不用 Random） ─────────────────────────

    /// <summary>FNV-1a 32 位：同 (seed, period) 永远同结果。</summary>
    public static int StableHash(int seed, long period)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)(period & 0xFFFFFFFFL)) * 16777619u;
            h = (h ^ (uint)((period >> 32) & 0xFFFFFFFFL)) * 16777619u;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    static int StableHashString(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            if (!string.IsNullOrEmpty(s))
                for (int i = 0; i < s.Length; i++) h = (h ^ (uint)s[i]) * 16777619u;
            return (int)(h & 0x7FFFFFFF);
        }
    }
}
