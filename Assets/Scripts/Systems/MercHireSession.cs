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
    public const int RefreshCooldownSeconds = 30 * 60;

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

    public static int GoldCost(MercenaryData offer)
    {
        if (offer == null) return 500;
        if (MercRosterDefs.TryGetByAssetId(offer.mercId, out var def) && def.RecruitGold > 0)
            return def.RecruitGold;
        var rarity = MercSkillMapping.StarToRarity(offer.star);
        if (rarity == MercRosterDefs.MercRarity.Legendary) return 5000;
        if (rarity == MercRosterDefs.MercRarity.Rare) return 1500;
        return 500;
    }

    public static MercRosterDefs.MercRarity OfferRarity(MercenaryData offer)
    {
        if (offer == null) return MercRosterDefs.MercRarity.Common;
        return MercSkillMapping.StarToRarity(offer.star);
    }

    public static bool HasScrollFor(MercRosterDefs.MercRarity rarity)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (rarity == MercRosterDefs.MercRarity.Legendary) return data.mercScrollLegendary > 0;
        if (rarity == MercRosterDefs.MercRarity.Rare) return data.mercScrollRare > 0;
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
            if (data.mercScrollRare <= 0) return false;
            data.mercScrollRare--;
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
    // ★★★ 三套「职业图标」的口径，别再互相搞混（2026-09-26 主人反馈「总和玩家职业icon搞混」）★★★
    //
    // 1) 佣兵职业分类徽标（四分类）—— Assets/Art/UI/Icons/职业icon/{物攻,法术,防御,恢复}.png
    //    用途：战斗左下角角色栏的「职业 icon」（玩家槽和佣兵槽都要这一套）、招募三选一卡的 Role 角标。
    //    取图入口：MercHireSession.LoadMercJobBadge。
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
    public const string MercJobBadgeRes = "Icons/职业icon";
    /// <summary>四分类图的现有 Resources 副本目录（与 Art 源同名同图）。主目录缺资源时回退这里。</summary>
    const string MercJobBadgeResFallback = "Icons/Job";

    /// <summary>
    /// 佣兵职业分类徽标（四分类：物攻 / 法术 / 防御 / 恢复）。
    /// 旧名 LoadJobIcon —— 改名只为和「玩家职业 icon」区分开，旧名保留为转调。
    /// </summary>
    public static Sprite LoadMercJobBadge(string jobName)
    {
        string file = MercJobBadgeFile(jobName);
        if (string.IsNullOrEmpty(file)) return null;
        var sp = LoadJobBadgeSprite(MercJobBadgeRes + "/" + file);
        if (sp != null) return sp;
        return LoadJobBadgeSprite(MercJobBadgeResFallback + "/" + file);
    }

    /// <summary>旧名，保留为转调，避免既有调用点断编译。新代码请用 <see cref="LoadMercJobBadge"/>。</summary>
    public static Sprite LoadJobIcon(string jobName) => LoadMercJobBadge(jobName);

    static Sprite LoadJobBadgeSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(path);
        if (all != null && all.Length > 0) return all[0];
        // 兜底：从 Assets/Art 拷进 Resources 的 png 若被团结按「默认贴图」导入（meta 里
        // textureType=0 / spriteMode=0，spriteSheet 为空），Resources.Load<Sprite> 取不到，
        // 左下角职业 icon 就会空白。这里退一步读 Texture2D 现造 Sprite，不改 .meta 也能显示。
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>佣兵职业名 → 四分类文件名。旧名 JobIconFile，改名只为区分口径。</summary>
    public static string MercJobBadgeFile(string jobName)
    {
        if (string.IsNullOrEmpty(jobName)) return "物攻";
        // 重武(重武者) 走物攻分支（2026-09-17 用户纠正：重武也是物攻，不是防御）
        if (jobName.Contains("盾") || jobName.Contains("卫") || jobName.Contains("防御"))
            return "防御";
        if (jobName.Contains("牧") || jobName.Contains("恢复") || jobName.Contains("圣"))
            return "恢复";
        if (jobName.Contains("法") || jobName.Contains("术") || jobName.Contains("水系") || jobName.Contains("雷系") || jobName.Contains("火系"))
            return "法术";
        return "物攻";
    }

    /// <summary>旧名，保留为转调，避免既有调用点断编译。新代码请用 <see cref="MercJobBadgeFile"/>。</summary>
    public static string JobIconFile(string jobName) => MercJobBadgeFile(jobName);

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
