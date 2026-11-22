using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 城镇入口 · 大地图核心（2026-11-22 升级：真实行军 + 头像战斗演出 + 占领倒计时）。
/// <para>流程：Dispatch 建行军单（战力比算好结算快照）→ 真实计时行军（广告可加速）→ 到达后 UI 播
/// 头像血条战斗 + 占领倒计时 → SettleMarch 入账。胜负恒按出发时战力比，血条只是可视化（主人拍板）。</para>
/// <para>闲置定义（主人拍板）：已拥有(unlockedMercIds) − 战斗队列(GetActiveMercHireIds())。
/// 另排除在途行军单占用的队员（否则同一闲兵可无限重派）——属 SLG 内部占用，不回写主线。</para>
/// <para>🔴 独立系统：不接旧玩法、战损不回写主线佣兵、图片全走 Resources.Load 路径（铁律 16）。</para>
/// </summary>
public static class SlgSystem
{
    static SaveData Data => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
    static int NowUnix() => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>大地图是否开启 = 第 1 章第 10 关已通关。</summary>
    public static bool IsUnlocked()
    {
        var d = Data;
        return d != null && d.HasStageCleared(SlgDefs.UnlockChapter, SlgDefs.UnlockStageIndex0);
    }

    /// <summary>开放大陆数 = 已通第 10 关的章数（大陆先后顺序）。</summary>
    public static int OpenContinentCount()
    {
        var d = Data;
        if (d == null) return 0;
        int n = 0;
        for (int ch = 1; ch <= 8; ch++)
            if (d.HasStageCleared(ch, SlgDefs.UnlockStageIndex0)) n++;
        return n;
    }

    public static List<SlgDefs.PointDef> ActivePoints()
    {
        var list = new List<SlgDefs.PointDef>();
        int continents = OpenContinentCount();
        for (int i = 0; i < SlgDefs.Points.Length; i++)
            if (SlgDefs.Points[i].Continent <= continents)
                list.Add(SlgDefs.Points[i]);
        return list;
    }

    public static SlgDefs.PointDef FindPoint(string pointId)
    {
        for (int i = 0; i < SlgDefs.Points.Length; i++)
            if (SlgDefs.Points[i].Id == pointId) return SlgDefs.Points[i];
        return default;
    }

    public static bool IsPointOnCooldown(string pointId)
    {
        var d = Data;
        return d != null && d.slgPointCd != null
               && d.slgPointCd.TryGetValue(pointId, out int until) && until > NowUnix();
    }

    public static int PointCooldownLeft(string pointId)
    {
        var d = Data;
        if (d == null || d.slgPointCd == null || !d.slgPointCd.TryGetValue(pointId, out int until)) return 0;
        return Math.Max(0, until - NowUnix());
    }

    /// <summary>闲置 hireId：已拥有 − 战斗队列 − 在途行军单占用。</summary>
    public static List<string> GetIdleHireIds()
    {
        var result = new List<string>();
        var d = Data;
        if (d == null || d.unlockedMercIds == null) return result;

        var busy = new HashSet<string>();
        var mm = MercenaryManager.Instance;
        if (mm != null)
        {
            var active = mm.GetActiveMercHireIds();
            if (active != null)
                for (int i = 0; i < active.Count; i++)
                    if (!string.IsNullOrEmpty(active[i])) busy.Add(active[i]);
        }
        // 在途（未结算）行军单占用的队员
        if (d.slgMarches != null)
            for (int m = 0; m < d.slgMarches.Count; m++)
            {
                var march = d.slgMarches[m];
                if (march == null || march.settled || march.squad == null) continue;
                for (int i = 0; i < march.squad.Count; i++)
                    if (!string.IsNullOrEmpty(march.squad[i])) busy.Add(march.squad[i]);
            }

        foreach (var id in d.unlockedMercIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (busy.Contains(id)) continue;
            result.Add(id);
        }
        return result;
    }

    /// <summary>该点位是否有在途（未结算）行军单——同一点不允许重复派遣。</summary>
    public static SlgMarchEntry MarchForPoint(string pointId)
    {
        var d = Data;
        if (d == null || d.slgMarches == null) return null;
        for (int i = 0; i < d.slgMarches.Count; i++)
        {
            var m = d.slgMarches[i];
            if (m != null && !m.settled && m.pointId == pointId) return m;
        }
        return null;
    }

    /// <summary>所有在途/待结算行军单。</summary>
    public static List<SlgMarchEntry> Marches()
    {
        var d = Data;
        if (d == null || d.slgMarches == null) return new List<SlgMarchEntry>();
        var list = new List<SlgMarchEntry>();
        for (int i = 0; i < d.slgMarches.Count; i++)
            if (d.slgMarches[i] != null && !d.slgMarches[i].settled) list.Add(d.slgMarches[i]);
        return list;
    }

    /// <summary>首次进入补初始粮草（幂等；老档 slgFoodGranted=false 也补，否则粮草=0 永远出不了征）。</summary>
    public static void EnsureInitialFood()
    {
        var d = Data;
        if (d == null || d.slgFoodGranted) return;
        d.slgFood += SlgDefs.InitialFoodGrant;
        d.slgFoodGranted = true;
        SaveSystem.Instance?.Save();
    }

    /// <summary>出发资源是否够（粮草 + 金币）。金币读主线 totalGold，只是校验，不在这里扣。</summary>
    public static bool HasDispatchCost(out string reason)
    {
        var d = Data;
        if (d == null) { reason = "存档未就绪"; return false; }
        if (d.slgFood < SlgDefs.DispatchFoodCost)
        {
            reason = "粮草不足（需要 " + SlgDefs.DispatchFoodCost + "，当前 " + d.slgFood + "）";
            return false;
        }
        long gold = ResourceWallet.Get(d, ResourceWallet.ResourceType.Gold);
        if (gold < SlgDefs.DispatchGoldCost)
        {
            reason = "金币不足（需要 " + SlgDefs.DispatchGoldCost + "，当前 " + gold + "）";
            return false;
        }
        reason = null;
        return true;
    }

    public static bool CanDispatch(string pointId, out string reason, out List<string> idle)
    {
        idle = GetIdleHireIds();
        if (IsPointOnCooldown(pointId))
        {
            reason = "此处刚打过，还需 " + FormatDuration(PointCooldownLeft(pointId));
            return false;
        }
        if (MarchForPoint(pointId) != null)
        {
            reason = "已有队伍正在前往此处";
            return false;
        }
        if (idle.Count < SlgDefs.SquadSize)
        {
            reason = "需要 " + SlgDefs.SquadSize + " 名闲置佣兵（当前 " + idle.Count + " 名）";
            return false;
        }
        if (!HasDispatchCost(out reason)) return false;
        reason = null;
        return true;
    }

    public static float EstimateSquadPower(List<string> hireIds)
    {
        float sum = 0f;
        if (hireIds == null) return sum;
        for (int i = 0; i < hireIds.Count && i < SlgDefs.SquadSize; i++)
        {
            if (!MercRosterDefs.TryGetByHireId(hireIds[i], out var d)) continue;
            sum += SlgDefs.EstimatePower(d.BaseAtk, d.BaseHp, d.BaseDef, d.AtkSpeed);
        }
        return sum;
    }

    public struct DispatchResult
    {
        public bool Ok;
        public string Message;
        public int MarchSec;
    }

    /// <summary>
    /// 派遣（主人 2026-11-22 拍板：玩家在选兵界面自选 3 名闲置佣兵）：
    /// 建行军单 + 出发时算好结算快照（战力比），扣粮草 + 金币，不立即入账奖励。
    /// 到达 + 战斗演出 + 占领倒计时完成后由 SettleMarch 入账。
    /// </summary>
    public static DispatchResult Dispatch(string pointId, bool peaceful, List<string> chosen)
    {
        var result = new DispatchResult();
        var d = Data;
        if (d == null) { result.Message = "存档未就绪"; return result; }

        var def = FindPoint(pointId);
        if (string.IsNullOrEmpty(def.Id)) { result.Message = "找不到该点位"; return result; }
        if (!CanDispatch(pointId, out string reason, out var idle))
        { result.Message = reason; return result; }

        // 自选队伍校验：必须 3 人、全在闲置集合内、不重复（防界面传入主线在队/在途队员）
        if (chosen == null || chosen.Count != SlgDefs.SquadSize)
        { result.Message = "请选择 " + SlgDefs.SquadSize + " 名闲置佣兵"; return result; }
        var idleSet = new HashSet<string>(idle);
        var picked = new HashSet<string>();
        for (int i = 0; i < chosen.Count; i++)
        {
            if (string.IsNullOrEmpty(chosen[i]) || !idleSet.Contains(chosen[i]) || !picked.Add(chosen[i]))
            { result.Message = "所选佣兵不可派遣（需在战斗队列外且未在途）"; return result; }
        }

        // 出发消耗：金币走 ResourceWallet 唯一出口（失败即中止，不发奖不建单）；粮草走独立 SLG 字段
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, SlgDefs.DispatchGoldCost, save: false, notify: true))
        { result.Message = "金币不足，无法出发"; return result; }
        d.slgFood -= SlgDefs.DispatchFoodCost;

        if (d.slgMarches == null) d.slgMarches = new List<SlgMarchEntry>();

        // 结算快照（出发即算定，演出不改数值）
        var march = new SlgMarchEntry
        {
            pointId = pointId,
            departUnix = NowUnix(),
            peaceful = peaceful,
            settled = false,
        };
        for (int i = 0; i < SlgDefs.SquadSize; i++) march.squad.Add(chosen[i]);

        int marchSec = SlgDefs.MarchSecFor(def.Pos, def.Continent);
        march.arriveUnix = march.departUnix + marchSec;
        result.MarchSec = marchSec;

        if (peaceful && def.Type == SlgDefs.PointType.City)
        {
            march.ore = def.Ore / 2;
            march.stones = Math.Max(1, def.Stones / 2);
            march.food = def.Food / 2;
            march.merit = 0;
            march.myHpFinal = 1f; march.enemyHpFinal = 1f;   // 通商不演战斗
        }
        else
        {
            float myPower = EstimateSquadPower(march.squad);
            float rate = myPower / Mathf.Max(1f, def.GuardPower);
            march.powerRate = rate;
            float ratio = SlgDefs.RewardRatio(rate);
            march.merit = Mathf.RoundToInt(def.Merit * ratio);
            march.ore = Mathf.RoundToInt(def.Ore * ratio);
            march.stones = Mathf.RoundToInt(def.Stones * ratio);
            march.food = Mathf.RoundToInt(def.Food * ratio);
            // 血量演出系数（胜负已由 ratio 定死，血条只是画出来）
            if (rate >= 1.5f)      { march.myHpFinal = 0.85f; march.enemyHpFinal = 0f; }
            else if (rate >= 1f)   { march.myHpFinal = 0.55f; march.enemyHpFinal = 0f; }
            else if (rate >= 0.8f) { march.myHpFinal = 0.30f; march.enemyHpFinal = 0f; }
            else                   { march.myHpFinal = 0.12f; march.enemyHpFinal = 0.35f; }
        }

        // 掉落快照：出发即按 slg_drop.csv 掷定（行军期间改表不影响本次结果）。
        // 近/远池不同；道具 ID 与主线 item_defs 同源；战败（战力比<0.8）不掷，通商概率减半。
        bool won = peaceful || SlgDefs.RewardRatio(march.powerRate) > 0f;
        if (won)
        {
            var drops = SlgDropTable.RollDrops(SlgDefs.DistTierFor(def.Pos), def.Continent,
                                               def.Type.ToString(), peaceful ? 0.5f : 1f);
            for (int i = 0; i < drops.Count; i++)
                march.dropItems.Add(new StringIntEntry { id = drops[i].id, value = drops[i].value });
        }

        d.slgMarches.Add(march);
        SaveSystem.Instance?.Save();
        result.Ok = true;
        result.Message = "已出发，约 " + FormatDuration(marchSec) + " 后抵达";
        return result;
    }

    /// <summary>行军是否已到达（未结算 + 到达时间已过）。</summary>
    public static bool IsArrived(SlgMarchEntry m)
        => m != null && !m.settled && NowUnix() >= m.arriveUnix;

    /// <summary>行军剩余秒（到达=0）。</summary>
    public static int MarchLeftSec(SlgMarchEntry m)
        => m == null ? 0 : Math.Max(0, m.arriveUnix - NowUnix());

    /// <summary>广告加速：把到达时间提前到现在（立即抵达）。回调由 RewardedAdBridge 单一出口驱动。</summary>
    public static bool SpeedUpMarch(string pointId)
    {
        var m = MarchForPoint(pointId);
        var d = Data;
        if (m == null || d == null || m.settled) return false;
        m.arriveUnix = NowUnix();
        SaveSystem.Instance?.Save();
        return true;
    }

    /// <summary>
    /// 结算入账（演出 + 占领倒计时完成后调用）：快照奖励入账 + 点位冷却 + 标记已结算。
    /// 幂等：settled 的单直接返回 false，不重复发奖。
    /// </summary>
    public static bool SettleMarch(string pointId)
    {
        var d = Data;
        if (d == null) return false;
        SlgMarchEntry target = null;
        if (d.slgMarches != null)
            for (int i = 0; i < d.slgMarches.Count; i++)
            {
                var m = d.slgMarches[i];
                if (m != null && !m.settled && m.pointId == pointId) { target = m; break; }
            }
        if (target == null || !IsArrived(target)) return false;

        d.slgMerit += target.merit;
        d.slgOre += target.ore;
        d.slgStones += target.stones;
        d.slgFood += target.food;

        // 道具掉落：唯一出口 ItemGrantSystem → 角色页随身仓库（与主线道具同池，不新造体系）
        if (target.dropItems != null)
            for (int i = 0; i < target.dropItems.Count; i++)
            {
                var e = target.dropItems[i];
                if (e != null && !string.IsNullOrEmpty(e.id) && e.value > 0)
                    ItemGrantSystem.GrantItem(e.id, e.value);
            }

        target.settled = true;

        if (d.slgPointCd == null) d.slgPointCd = new Dictionary<string, int>();
        d.slgPointCd[pointId] = NowUnix() + SlgDefs.PointCooldownSec;

        SaveSystem.Instance?.Save();
        return true;
    }

    static string FormatDuration(int sec)
    {
        if (sec >= 3600)
        {
            int h = sec / 3600, m = (sec % 3600) / 60;
            return m > 0 ? h + " 小时 " + m + " 分" : h + " 小时";
        }
        if (sec >= 60) return (sec / 60) + " 分钟";
        return sec + " 秒";
    }
}
