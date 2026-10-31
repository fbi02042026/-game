using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 称号系统（2026-10-31 新增，主人拍板）：
/// 称号解锁后<b>自动出现在战斗形象的头顶</b>（<see cref="PlayerTitleHud"/>），文字颜色按稀有度。
///
/// <para><b>同时只展示一个</b>，切换规则（主人原话「只保持最高等级的称号，同等级保留最新的」）：</para>
/// <list type="bullet">
/// <item>新称号稀有度 <b>更高</b> → 换成新的；</item>
/// <item>新称号稀有度 <b>同级</b> → 也换成新的（保留最新的）；</item>
/// <item>新称号稀有度 <b>更低</b> → 保留原的，不降级。</item>
/// </list>
/// <para>判定只写在 <see cref="TryActivate"/> 一处，改规则只动它。</para>
///
/// <para><b>发放唯一出口</b>：<see cref="Unlock"/>（成就 A012「森林无伤者」、里程 Lv6「传说记录者」都走它）。
/// 别处不要再直接写 <c>SaveData.unlockedTitleIds</c> —— 那样只解锁不换展示。</para>
/// </summary>
public static class TitleSystem
{
    /// <summary>当前展示的称号 id（空 = 没称号）。</summary>
    public static string ActiveId => SaveSystem.Instance?.Data?.activeTitleId ?? "";

    /// <summary>是否真的有可展示的称号（id 非空且能在称号表里查到）。</summary>
    public static bool HasActive => TitleDefs.TryGet(ActiveId, out _);

    public static string ActiveName => TitleDefs.NameOf(ActiveId);

    public static Color ActiveColor => TitleDefs.ColorOf(ActiveId);

    public static bool IsUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var set = SaveSystem.Instance?.Data?.unlockedTitleIds;
        return set != null && set.Contains(id);
    }

    /// <summary>
    /// 【发放唯一出口】解锁一个称号，并按规则决定要不要换成它来展示。
    /// <paramref name="notify"/> = false 时静默（批量领取 / 里程结算用，外面自己弹汇总）。
    /// </summary>
    public static bool Unlock(string id, bool notify = true)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(id)) return false;
        if (!TitleDefs.TryGet(id, out var def))
        {
            // 称号表里没有 = 代码写错了 id（不要静默吞，出错了要看得见）
            Debug.LogError($"[TitleSystem] 称号表 TitleDefs 里没有 id={id}，本次发放跳过");
            return false;
        }

        data.unlockedTitleIds ??= new HashSet<string>();
        bool isNew = data.unlockedTitleIds.Add(id);
        bool switched = TryActivate(data, id);

        if (isNew || switched)
        {
            SaveSystem.Instance.Save();
            PlayerTitleHud.Refresh();     // 战斗中立刻换（不在战斗里就是空操作）
        }

        if (notify && isNew)
            UIManager.Instance?.ShowToast(
                $"获得称号「{def.Name}」（{TitleDefs.RarityName(def.Rarity)}）");
        return true;
    }

    /// <summary>
    /// 切换规则（唯一判定处）：稀有度更高或同级 → 换成新的；更低 → 不动。
    /// 返回是否真的改了展示。
    /// </summary>
    static bool TryActivate(SaveData data, string id)
    {
        if (data == null || !TitleDefs.TryGet(id, out var def)) return false;

        string cur = data.activeTitleId;
        if (string.IsNullOrEmpty(cur) || !TitleDefs.TryGet(cur, out var curDef))
        {
            data.activeTitleId = id;
            return true;
        }
        if (TitleDefs.Rank(def.Rarity) >= TitleDefs.Rank(curDef.Rarity))
        {
            data.activeTitleId = id;      // 同级也换 = 「同等级保留最新的」
            return true;
        }
        return false;                     // 更低 → 保留原称号，不降级
    }

    /// <summary>
    /// 老档 / 异常自愈：<c>activeTitleId</c> 为空、或指向一个已经不在解锁集合里的 id 时，
    /// 从已解锁称号里挑稀有度最高的那个（同级取称号表里靠后的 = 视为更新的）。
    /// 只在真要改的时候写盘，正常情况零开销。
    /// </summary>
    public static void EnsureActive()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        var set = data.unlockedTitleIds;
        if (set == null || set.Count == 0)
        {
            if (!string.IsNullOrEmpty(data.activeTitleId))
            {
                data.activeTitleId = "";
                SaveSystem.Instance.Save();
                PlayerTitleHud.Refresh();
            }
            return;
        }

        string cur = data.activeTitleId;
        if (!string.IsNullOrEmpty(cur) && set.Contains(cur) && TitleDefs.TryGet(cur, out _))
            return;      // 正常，不用动

        string best = "";
        int bestRank = int.MinValue;
        var all = TitleDefs.AllDefs;
        for (int i = 0; i < all.Length; i++)
        {
            if (!set.Contains(all[i].Id)) continue;
            int r = TitleDefs.Rank(all[i].Rarity);
            if (r >= bestRank) { bestRank = r; best = all[i].Id; }   // >= 让同级取表里靠后的
        }

        if (best == cur) return;
        data.activeTitleId = best;
        SaveSystem.Instance.Save();
        PlayerTitleHud.Refresh();
    }
}
