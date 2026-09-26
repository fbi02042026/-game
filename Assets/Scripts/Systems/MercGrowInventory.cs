using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵养成道具仓库：**职业徽记** + **本命碎片**（2026-09-18 新增）。
///
/// 存档口径（SaveData.mercGrowItems，List&lt;StringIntEntry&gt; 双写）：
///   徽记     badge:{职业key}:{档位}   例 badge:剑盾:传奇
///   本命碎片 frag:{hireId}            例 frag:H001
///
/// 来源：
///   徽记     关卡掉落（StageDropTable，见 stage_drop.csv）
///   本命碎片 **商店买重复佣兵转化**（MercGrowInventory.ConvertDuplicateMerc）—— 掉落表里不给
///
/// 消耗口径见 Docs/佣兵养成_随机化与抗性_2026-09-17.md §6（养成面板做的时候再接）。
/// </summary>
public static class MercGrowInventory
{
    public const string BadgePrefix = "badge:";
    public const string FragPrefix = "frag:";

    /// <summary>买重复佣兵转本命碎片的数量（按稀有度）。想调节奏改这三个常量。</summary>
    public const int DUP_FRAG_COMMON = 8;
    public const int DUP_FRAG_RARE = 20;
    public const int DUP_FRAG_LEGENDARY = 50;

    public static string BadgeId(string jobKey, string tier)
    {
        if (string.IsNullOrEmpty(jobKey)) return null;
        return BadgePrefix + jobKey + ":" + (string.IsNullOrEmpty(tier) ? "普通" : tier);
    }

    public static string FragmentId(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return null;
        return FragPrefix + hireId;
    }

    static Dictionary<string, int> Map(bool create = false)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return null;
        if (data.mercGrowItems == null)
        {
            if (!create) return null;
            data.mercGrowItems = new Dictionary<string, int>();
        }
        return data.mercGrowItems;
    }

    public static int Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        var m = Map();
        int v;
        return m != null && m.TryGetValue(id, out v) ? v : 0;
    }

    public static int BadgeCount(string jobKey, string tier) => Get(BadgeId(jobKey, tier));

    public static int FragmentCount(string hireId) => Get(FragmentId(hireId));

    public static void Add(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return;
        var m = Map(true);
        if (m == null) return;
        int cur;
        m.TryGetValue(id, out cur);
        m[id] = cur + count;
    }

    public static bool TrySpend(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return false;
        if (Get(id) < count) return false;
        var m = Map();
        if (m == null) return false;
        m[id] = m[id] - count;
        if (m[id] <= 0) m.Remove(id);
        return true;
    }

    /// <summary>
    /// 关卡结算发放掉落：徽记 / 本命碎片 / 道具。
    /// 返回本次拿到的总个数（0 = 没掉，调用方可以不弹提示）。
    /// firstClear 传「本章是否首次通关」，用来触发徽记保底。
    /// monsterTypeBias 传本关主导怪物类型（null=不限制），供 stage_drop.csv 按类型分池。
    /// </summary>
    public static int GrantStageDrops(int gameChapter, string stageType, bool firstClear)
    {
        return GrantStageDrops(gameChapter, stageType, firstClear, null);
    }

    public static int GrantStageDrops(int gameChapter, string stageType, bool firstClear, string monsterTypeBias)
    {
        var drops = StageDropTable.RollDrops(gameChapter, stageType, firstClear, monsterTypeBias);
        if (drops == null || drops.Count <= 0) return 0;

        int total = 0;
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            if (d.type == StageDropTable.DropType.Item)
            {
                // 道具进背包（jobKey 列复用为 itemId）
                var gb = GridBackpackSystem.Instance;
                if (gb != null && gb.TryAddItemStack(d.id, d.count, out GridBackpackSystem.BackpackItem placed, notify: false))
                    total += d.count;
            }
            else
            {
                // Badge / Fragment 都走本命养成仓库（id 已是 badge:/frag: 口径）
                Add(d.id, d.count);
                total += d.count;
            }
        }
        SaveSystem.Instance?.Save();
        Debug.Log($"[MercGrow] 关卡掉落 ch{gameChapter} {stageType} first={firstClear} → {total} 个（徽记/碎片/道具）");
        return total;
    }

    /// <summary>该佣兵买重复能转多少本命碎片（UI 卡面显示用）。</summary>
    public static int DupFragCountOf(MercRosterDefs.Def def)
    {
        // Def 是 struct，不能判 null —— 用 HireId 空否判断是不是默认（无效）值
        if (string.IsNullOrEmpty(def.HireId)) return 0;
        return def.Rarity == MercRosterDefs.MercRarity.Legendary ? DUP_FRAG_LEGENDARY
             : def.Rarity == MercRosterDefs.MercRarity.Rare ? DUP_FRAG_RARE
             : DUP_FRAG_COMMON;
    }

    /// <summary>
    /// 买重复佣兵 → 转本命碎片（用户 2026-09-18 拍板）。
    /// 在 TavernUnlockUI.TryUnlock 里命中「已解锁」时调用，返回转化的碎片数量。
    /// </summary>
    public static int ConvertDuplicateMerc(MercRosterDefs.Def def)
    {
        int n = DupFragCountOf(def);
        if (n <= 0) return 0;
        Add(FragmentId(def.HireId), n);
        return n;
    }
}
