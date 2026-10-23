using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本局雇佣的佣兵（下本结束离队）。图鉴见 AdventureCodex / seenMerc。
///
/// 图标口径提醒：职业相关的图有三套，各走各的入口，取图前先看清
/// <see cref="LoadMercJobBadge"/> 上方那段说明，别再拿玩家职业 icon 画佣兵。
/// </summary>
public static class MercHireSession
{
    public const int RefreshCooldownSeconds = 3 * 60 * 60;   // 2026-10-17 主人拍板：手动刷新冷却 30 分钟 → 3 小时

    public static List<MercenaryData> GetHired()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return new List<MercenaryData>();
        data.hiredMercs ??= new List<MercenaryData>();
        return data.hiredMercs;
    }

    public static int HiredCount()
    {
        var list = GetHired();
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && !string.IsNullOrEmpty(list[i].mercId)) n++;
        return n;
    }

    public static bool CanHireMore()
    {
        int max = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetMaxMercSlots()
            : Mathf.Clamp(SaveSystem.Instance?.Data?.townLevel?.tavern ?? 0, 0, 2);
        if (max < 1) max = 1; // 酒馆至少允许招 1 人进临时队
        return HiredCount() < max;
    }

    public static void AddHired(MercenaryData m)
    {
        if (m == null) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.hiredMercs ??= new List<MercenaryData>();
        data.hiredMercs.Add(m);
        if (data.townLevel == null) data.townLevel = new TownLevel();
        if (data.townLevel.tavern < 1) data.townLevel.tavern = 1;
        AdventureCodex.MarkMercSeen(m.mercId);
        AdventureLogAchievements.OnMercRecruited();
        SaveSystem.Instance.Save();
    }

    /// <summary>下本结束回城：记下上局出战 hireId 后清空临时雇佣。</summary>
    public static void ClearHired(bool save = true)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.lastRunMercHireIds ??= new List<string>();
        data.lastRunMercHireIds.Clear();
        if (data.hiredMercs != null)
        {
            for (int i = 0; i < data.hiredMercs.Count; i++)
            {
                var m = data.hiredMercs[i];
                if (m == null) continue;
                string id = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
                if (string.IsNullOrEmpty(id)) continue;
                if (!data.lastRunMercHireIds.Contains(id))
                    data.lastRunMercHireIds.Add(id);
            }
            data.hiredMercs.Clear();
        }
        if (save) SaveSystem.Instance.Save();
    }

    public static bool WasInLastRun(string hireIdOrMercId)
    {
        if (string.IsNullOrEmpty(hireIdOrMercId)) return false;
        var data = SaveSystem.Instance?.Data;
        var list = data?.lastRunMercHireIds;
        if (list == null) return false;
        return list.Contains(hireIdOrMercId);
    }

    public static bool IsAlreadyHired(MercenaryData offer)
    {
        if (offer == null) return false;
        var list = GetHired();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (!string.IsNullOrEmpty(offer.hireId) && m.hireId == offer.hireId)
                return true;
            if (!string.IsNullOrEmpty(offer.mercId) && m.mercId == offer.mercId
                && (string.IsNullOrEmpty(offer.hireId) || string.IsNullOrEmpty(m.hireId)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 该招募候选对应的佣兵是否已永久解锁（进「已拥有」）。
    /// 【2026-10-19 主人拍板】酒馆招募购买口径 = 永久解锁进已拥有，与雇佣队列 / 临时出战名单无关；
    /// 已拥有的候选不再可买（点击只提示）。
    /// </summary>
    public static bool IsMercOwned(MercenaryData offer)
    {
        if (offer == null) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        string hid = offer.hireId;
        if (string.IsNullOrEmpty(hid) && MercRosterDefs.TryGetByAssetId(offer.mercId, out var def))
            hid = def.HireId;
        return !string.IsNullOrEmpty(hid) && data.IsMercUnlocked(hid);
    }

    public static int GoldCost(MercenaryData offer)
    {
        if (offer == null) return 500;
        if (MercRosterDefs.TryGetByAssetId(offer.mercId, out var def) && def.RecruitGold > 0)
            return def.RecruitGold;
        // 【2026-10-22 主人拍板 T13】招募零星后 offer.star 恒 0，稀有度改走 OfferRarity（花名册优先）
        var rarity = OfferRarity(offer);
        if (rarity == MercRosterDefs.MercRarity.Legendary) return 5000;
        if (rarity == MercRosterDefs.MercRarity.Rare) return 1500;
        return 500;
    }

    public static MercRosterDefs.MercRarity OfferRarity(MercenaryData offer)
    {
        if (offer == null) return MercRosterDefs.MercRarity.Common;
        // 【2026-10-22 主人拍板 T13】招募零星：offer.star 恒 0，StarToRarity(star) 会把稀有/传奇全判成普通
        //（卡面稀有度与卷轴/金币支付口径全错）—— 改从花名册解析（ hireId → AssetId），StarToRarity 只作兜底。
        if (!string.IsNullOrEmpty(offer.hireId) && MercRosterDefs.TryGetByHireId(offer.hireId, out var byHire))
            return byHire.Rarity;
        if (!string.IsNullOrEmpty(offer.mercId) && MercRosterDefs.TryGetByAssetId(offer.mercId, out var byAsset))
            return byAsset.Rarity;
        return MercSkillMapping.StarToRarity(offer.star);
    }

    /// <summary>
    /// 卷轴口径（2026-10-19 主人拍板）：普通佣兵=金币雇佣（不走卷）；
    /// 稀有佣兵用【普通卷轴】（mercScrollCommon）；传奇佣兵用【稀有卷轴】（存档字段 mercScrollLegendary）。
    /// </summary>
    public static bool HasScrollFor(MercRosterDefs.MercRarity rarity)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (rarity == MercRosterDefs.MercRarity.Legendary) return data.mercScrollLegendary > 0;
        if (rarity == MercRosterDefs.MercRarity.Rare) return data.mercScrollCommon > 0;
        return false;
    }

    public static bool TrySpendScroll(MercRosterDefs.MercRarity rarity)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (rarity == MercRosterDefs.MercRarity.Legendary)
        {
            if (data.mercScrollLegendary <= 0) return false;
            data.mercScrollLegendary--;
            return true;
        }
        if (rarity == MercRosterDefs.MercRarity.Rare)
        {
            if (data.mercScrollCommon <= 0) return false;
            data.mercScrollCommon--;
            return true;
        }
        return false;
    }

    public static void EnsureDailyOfferRefresh()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        // 本地日历日 0 点
        string today = DateTime.Now.ToString("yyyyMMdd");
        if (data.mercOfferDayKey == today) return;
        data.mercOfferDayKey = today;
        data.mercOfferRefreshUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        data.mercOfferDirty = true;
    }

    public static bool CanManualRefresh(out int remainSec)
    {
        remainSec = 0;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return true;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long last = data.mercOfferRefreshUtc;
        long elapsed = now - last;
        if (last <= 0 || elapsed >= RefreshCooldownSeconds) return true;
        remainSec = (int)(RefreshCooldownSeconds - elapsed);
        return false;
    }

    public static void MarkRefreshed()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.mercOfferRefreshUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        data.mercOfferDayKey = DateTime.Now.ToString("yyyyMMdd");
        data.mercOfferDirty = false;
        SaveSystem.Instance.Save();
    }

    // ==========================================================================
    // 酒馆三选一「钻石刷新」（2026-10-23 主人拍板）
    // 冷却中也能刷：每天限 2 次，第 1 次 50 钻、第 2 次 100 钻；用完当天只能等免费冷却。
    // ==========================================================================

    /// <summary>每天钻石刷新次数上限。</summary>
    public const int DiamondRefreshPerDay = 2;
    /// <summary>第 1 次钻石刷新价格。</summary>
    public const int DiamondRefreshCost1 = 50;
    /// <summary>第 2 次钻石刷新价格。</summary>
    public const int DiamondRefreshCost2 = 100;

    /// <summary>第 N 次（0 起）钻石刷新的价格：第 1 次 50、第 2 次 100。</summary>
    public static int DiamondRefreshCost(int usedToday)
        => usedToday <= 0 ? DiamondRefreshCost1 : DiamondRefreshCost2;

    /// <summary>
    /// 跨天清零并返回今天是否还能钻石刷新。<paramref name="usedToday"/> = 已用次数，
    /// <paramref name="cost"/> = 下一次的价格。
    /// </summary>
    public static bool CanDiamondRefresh(out int usedToday, out int cost)
    {
        usedToday = 0;
        cost = 0;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        string today = DateTime.Now.ToString("yyyyMMdd");
        if (data.mercDiamondRefreshDayKey != today)
        {
            data.mercDiamondRefreshDayKey = today;
            data.mercDiamondRefreshUsed = 0;
        }
        usedToday = Mathf.Clamp(data.mercDiamondRefreshUsed, 0, DiamondRefreshPerDay);
        cost = DiamondRefreshCost(usedToday);
        return usedToday < DiamondRefreshPerDay;
    }

    /// <summary>
    /// 花钻石立即刷新酒馆三选一（冷却中可用）。成功 = 已扣钻 + 已重置冷却（内部落盘），返回 true；
    /// 次数用完或钻石不足返回 false（不足时 TrySpend 已弹「钻石不足」提示，调用方不必重复弹）。
    /// </summary>
    public static bool TryDiamondRefresh()
    {
        if (!CanDiamondRefresh(out int usedToday, out int cost)) return false;
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Diamond, cost, save: false, notify: true))
            return false;
        MarkRefreshed();   // 内部 Save，扣钻一并落盘
        return true;
    }

    // ==========================================================================
    // ★★★ 三套「职业图标」的口径，别再互相搞混（2026-09-26 主人反馈「总和玩家职业icon搞混」）★★★
    //
    // 1) 佣兵职业分类徽标（四分类）—— Assets/Art/UI/Icons/职业icon/{物攻,法术,防御,恢复}.png
    //    用途：战斗左下角角色栏的「职业 icon」（玩家槽和佣兵槽都要这一套）、招募三选一卡的 Role 角标。
    //    取图唯一入口：JobIconResolver.CombatBadge（2026-09-26 收口，本类只保留转调）。
    //    注意：Resources 副本目前放在 Icons/Job（与 Art 源同名同图），所以两个路径都试。
    //
    // 2) 玩家职业立绘头像 —— Assets/Art/UI/Icons/职业头像icon/（PlayerJobDefs.IconArtFolder）
    //    用途：职业选择 / 三选一卡面那种「人像」。入口：PlayerJobDefs.TryLoadJobIcon。
    //    ⚠ 不要拿它当左下角的职业分类 icon。
    //
    // 3) 玩家职业 icon（6 张）—— Assets/Art/UI/Icons/玩家职业icon/{剑盾,法师,游侠,牧师,狂战,重武}.png
    //    ⚠ 这是玩家职业的，不要拿它画佣兵，也不要拿它当左下角分类 icon。
    //
    // 另有一套「佣兵养成·职业徽记」（6 职业 × 普通/稀有/传奇，见 MercGrowSprites.LoadJobBadge），
    // 那是徽章不是分类图，同样别混进来。
    // ==========================================================================

    /// <summary>佣兵职业分类徽标（四分类）Resources 主目录：主人指定的美术源是 Icons/职业icon。</summary>
    public const string MercJobBadgeRes = JobIconResolver.BadgeRes;

    /// <summary>
    /// 佣兵职业分类徽标（四分类：物攻 / 法术 / 防御 / 恢复）。
    /// 2026-09-26：取图实现已收口到唯一入口 <see cref="JobIconResolver.CombatBadge"/>，这里只做转调，
    /// 旧调用点不用改也不会断；换图 / 改路径一律改 JobIconResolver 那一处。逻辑与收口前完全一致。
    /// </summary>
    public static Sprite LoadMercJobBadge(string jobName) => JobIconResolver.CombatBadge(jobName);

    /// <summary>旧名，已废弃。请用 <see cref="JobIconResolver.CombatBadge"/>。</summary>
    [System.Obsolete("职业图标已收口到单一入口，请用 JobIconResolver.CombatBadge")]
    public static Sprite LoadJobIcon(string jobName) => JobIconResolver.CombatBadge(jobName);

    /// <summary>佣兵职业名 → 四分类文件名。实现在 <see cref="JobIconResolver.BadgeFile"/>（唯一入口）。</summary>
    public static string MercJobBadgeFile(string jobName) => JobIconResolver.BadgeFile(jobName);

    /// <summary>旧名，已废弃。请用 <see cref="JobIconResolver.BadgeFile"/>。</summary>
    [System.Obsolete("职业图标已收口到单一入口，请用 JobIconResolver.BadgeFile")]
    public static string JobIconFile(string jobName) => JobIconResolver.BadgeFile(jobName);

    public static Material LoadScrollButtonMaterial(MercRosterDefs.MercRarity rarity)
    {
        if (rarity == MercRosterDefs.MercRarity.Legendary)
            return Resources.Load<Material>("Materials/btn10_chuanqi");
        if (rarity == MercRosterDefs.MercRarity.Rare)
            return Resources.Load<Material>("Materials/btn09_xiyou");
        return null;
    }

    // ⚠️ 这是「三选一招募卡」自己的边框，美术已调好，不要再改成头像框那套。
    public static Sprite LoadRarityFrame(MercRosterDefs.MercRarity rarity)
    {
        string name = rarity == MercRosterDefs.MercRarity.Legendary ? "frame_legendary"
            : rarity == MercRosterDefs.MercRarity.Rare ? "frame_rare" : "frame_common";
        var sp = Resources.Load<Sprite>("UI/Recruit/" + name);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>("UI/Recruit/" + name);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    /// <summary>
    /// 酒馆卡组「卡框」三档（2026-10-16 主人拍板：抽奖池列表卡与池格同框）：传奇金 / 稀有蓝 / 普通银。
    /// 真源 Resources/Art/UI/Tavern/酒馆头像/（204×248，池格预制体序列化的就是这套，走路径加载）。
    /// ⚠️ 别混用 <see cref="LoadRarityFrame"/>——那是三选一高卡（219×789 插画卡底），压到 200×300 卡上会变形。
    /// </summary>
    public const string TavernCardFrameDir = "Art/UI/Tavern/酒馆头像";

    public static Sprite LoadTavernCardFrame(MercRosterDefs.MercRarity rarity)
    {
        string name = rarity == MercRosterDefs.MercRarity.Legendary ? "传奇头像框"
            : rarity == MercRosterDefs.MercRarity.Rare ? "稀有头像框" : "普通头像框";
        var sp = Resources.Load<Sprite>(TavernCardFrameDir + "/" + name);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(TavernCardFrameDir + "/" + name);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    /// <summary>
    /// 酒馆卡组「名字底牌」三档（2026-10-16 主人拍板：名字跟稀有度走，换图不代码改色）：
    /// 传奇名字框 / 稀有名字框 / 普通名字框，同目录 <see cref="TavernCardFrameDir"/>。
    /// </summary>
    public static Sprite LoadTavernNamePlate(MercRosterDefs.MercRarity rarity)
    {
        string name = rarity == MercRosterDefs.MercRarity.Legendary ? "传奇名字框"
            : rarity == MercRosterDefs.MercRarity.Rare ? "稀有名字框" : "普通名字框";
        var sp = Resources.Load<Sprite>(TavernCardFrameDir + "/" + name);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(TavernCardFrameDir + "/" + name);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    /// <summary>
    /// 战斗 HUD 的**头像框**（三档）：普通灰白 / 稀有蓝 / 传奇（史诗）橙金。
    /// 与三选一招募卡的边框是两套东西，别混用。
    /// 美术原图在 Assets/Art/UI/Common/头像框，运行时副本在 Resources/UI/Common/头像框
    /// （两处都要放，Resources 那份才是 Resources.Load 能拿到的）。
    /// </summary>
    public const string PortraitFrameDir = "UI/Common/头像框";

    public static Sprite LoadPortraitFrame(MercRosterDefs.MercRarity rarity)
    {
        // 史诗头像框 = 最高档，对应枚举 Legendary
        string name = rarity == MercRosterDefs.MercRarity.Legendary ? "史诗头像框"
            : rarity == MercRosterDefs.MercRarity.Rare ? "稀有头像框" : "普通头像框";
        var sp = LoadFrameSprite(PortraitFrameDir + "/" + name);
        if (sp != null) return sp;
        // 副本缺失时退回三选一那套，避免开天窗
        return LoadRarityFrame(rarity);
    }

    /// <summary>玩家头像框稀有度。默认普通，日后「大厅考证」提档只需改这里。</summary>
    public static MercRosterDefs.MercRarity PlayerFrameRarity = MercRosterDefs.MercRarity.Common;

    /// <summary>玩家头像框（当前一律普通，等大厅考证系统接入后按存档取值）。</summary>
    public static Sprite LoadPlayerPortraitFrame() => LoadPortraitFrame(PlayerFrameRarity);

    static Sprite LoadFrameSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(path);
        return all != null && all.Length > 0 ? all[0] : null;
    }
}
