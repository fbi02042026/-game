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

    // 【2026-10-22 施工单 T3】旧 DUP_FRAG_*（8/20/50）已被 MercStarDefs.DupFragCount（10/5/1）替代，原常量删除。

    /// <summary>统一发碎片入口的结果：进了背包多少、溢出转邮件多少（碎片不许作废）。</summary>
    public struct FragGrantResult
    {
        public int bagCount;
        public int mailCount;
        public int Total => bagCount + mailCount;
    }

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
            else if (d.id != null && d.id.StartsWith(FragPrefix))
            {
                // 本命碎片：2026-10-22 起改走统一发碎片入口（背包优先→邮件），不再进 mercGrowItems
                GrantFragments(d.id.Substring(FragPrefix.Length), d.count, "stage-drop");
                total += d.count;
            }
            else
            {
                // Badge / 其余走本命养成仓库（id 已是 badge: 口径）
                Add(d.id, d.count);
                total += d.count;
            }
        }
        SaveSystem.Instance?.Save();
        Debug.Log($"[MercGrow] 关卡掉落 ch{gameChapter} {stageType} first={firstClear} → {total} 个（徽记/碎片/道具）");
        return total;
    }

    /// <summary>该佣兵买重复能转多少本命碎片（UI 卡面显示用）。数值真源 = MercStarDefs（2026-10-22 拍板 10/5/1）。</summary>
    public static int DupFragCountOf(MercRosterDefs.Def def)
    {
        // Def 是 struct，不能判 null —— 用 HireId 空否判断是不是默认（无效）值
        if (string.IsNullOrEmpty(def.HireId)) return 0;
        return MercStarDefs.DupFragCount(def.Rarity);
    }

    // ── 统一发碎片入口（2026-10-22 施工单 T5）──────────────────────────────
    //
    // 🔴 红线：所有碎片来源（酒馆重复转化 / 每日登录 / 关卡掉落 / 旧存档迁移）只准走这一个
    //    入口，禁止各写各的。口径 = 背包优先 → 装不下的整份余量发邮件（MailSystem 碎片邮件，
    //    领取时背包仍满则不标已领 —— 碎片不许作废）。

    /// <summary>
    /// 发佣兵本命碎片（唯一入口）：背包优先，装不下的余量发邮件。
    /// hireId 非法或 item_defs.csv 缺对应碎片行时 fail closed（LogError 带路径，不发不吞）。
    /// </summary>
    public static FragGrantResult GrantFragments(string hireId, int amount, string reason)
    {
        var r = new FragGrantResult();
        if (string.IsNullOrEmpty(hireId) || amount <= 0) return r;
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def))
        {
            Debug.LogError($"[MercGrow] 发碎片失败：花名册查不到 {hireId}（reason={reason}），本次碎片未发放");
            return r;
        }
        string itemId = MercStarDefs.FragmentItemId(hireId);
        if (ItemDefs.Get(itemId) == null)
        {
            Debug.LogError($"[MercGrow] 发碎片失败：item_defs.csv 缺行 {itemId}（reason={reason}）——请补表并 Cook，本次碎片未发放");
            return r;
        }

        var bag = GridBackpackSystem.Instance;
        if (bag != null && bag.TryAddItemStackLeftover(itemId, amount, out _, out int leftover, notify: false))
        {
            r.bagCount = amount - leftover;
            if (r.bagCount > 0)
                UIManager.Instance?.ShowToast($"获得 {def.Name}的本命碎片 ×{r.bagCount}", true);   // 2026-10-06 主人拍板：物品获得 force 弹
        }
        else
        {
            r.bagCount = 0;
            r.mailCount = amount;
        }
        if (r.mailCount > 0)
        {
            MailSystem.EnqueueMercFragments(hireId, r.mailCount);
            Debug.Log($"[MercGrow] 碎片溢出转邮件：{def.Name}({hireId}) ×{r.mailCount}（reason={reason}）");
        }
        Debug.Log($"[MercGrow] 发碎片 {def.Name}({hireId}) ×{amount}：入包 {r.bagCount} / 邮件 {r.mailCount}（reason={reason}）");
        return r;
    }

    /// <summary>
    /// 买重复佣兵 → 转本命碎片（2026-10-22 新口径：走统一发碎片入口，背包优先→邮件；满星不给，调用方先拦）。
    /// 在 TavernUnlockUI.TryUnlock 里命中「已解锁」时调用。
    /// </summary>
    public static FragGrantResult ConvertDuplicateMerc(MercRosterDefs.Def def)
    {
        int n = DupFragCountOf(def);
        if (n <= 0) return default;
        return GrantFragments(def.HireId, n, "tavern-duplicate");
    }

    /// <summary>
    /// 旧存档迁移（2026-10-22 施工单 T6）：mercGrowItems 里的 frag:{hireId} 存量一次性换算成
    /// 背包碎片（溢出走邮件）；badge: 徽记条目不动。迁移标记 = SaveData.mercFragMigrated（防重复执行）。
    /// 进城镇后调（TownSceneBootstrap），此时背包系统已就绪、碎片能真正落格。
    /// </summary>
    public static void MigrateLegacyFragmentsToBag()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.mercFragMigrated) return;

        var items = data.mercGrowItems;
        int migrated = 0, mails = 0;
        if (items != null && items.Count > 0)
        {
            // 先收集再删，避免遍历中改字典
            var legacy = new List<KeyValuePair<string, int>>();
            foreach (var kv in items)
            {
                if (kv.Key == null || !kv.Key.StartsWith(FragPrefix)) continue;
                if (kv.Value <= 0) continue;
                legacy.Add(new KeyValuePair<string, int>(kv.Key.Substring(FragPrefix.Length), kv.Value));
            }
            foreach (var kv in legacy)
            {
                var r = GrantFragments(kv.Key, kv.Value, "legacy-migrate");
                migrated += r.bagCount;
                mails += r.mailCount;
                items.Remove(FragPrefix + kv.Key);
            }
        }
        data.mercFragMigrated = true;   // 只跑一次：即使本次一条存量都没有也置位
        SaveSystem.Instance.Save();
        if (migrated > 0 || mails > 0)
            Debug.Log($"[MercGrow] 旧存档碎片迁移完成：入包 {migrated} / 转邮件 {mails}（标记 mercFragMigrated=true）");
        else
            Debug.Log("[MercGrow] 旧存档碎片迁移：无 frag: 存量，仅置迁移标记");
    }
}
