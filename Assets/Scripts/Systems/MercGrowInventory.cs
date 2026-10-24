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

    /// <summary>
    /// 徽记职业 key 全集（6 职业等权，key 与 MercGrowSprites.JobKey 返回值同套字符串）。
    /// 【2026-10-24 主人拍板】原 GrantBadgeDungeonDrops 内联数组提为公共真源，福利邮件（DevGiftMail）共用。
    /// </summary>
    public static readonly string[] BadgeJobKeys = { "剑盾", "狂战", "游侠", "法师", "牧师", "重武" };

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
                var gb = BattleBagSystem.Instance;
                if (gb != null && gb.TryAddItemStack(d.id, d.count, out BattleBagSystem.BackpackItem placed, notify: false))
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

    // ── 徽记副本发放（2026-10-23 主人拍板「活动副本一拆二」，施工单 T6/T8）──────────
    //
    // 🔴 概率/数量真源只在这一个函数内（T8 选「硬编码在 T6 函数内」，未提 GameConfig 常量）。
    // 🔴 只发徽记：不给金币、不给碎片、不给道具（金币本专属金币，徽记本专属徽记）。
    // 档位概率表（difficulty：0=普通 / 1=困难 / 2=噩梦，越界按 0 处理；BattleDifficulty 已在
    // BattleManager.StartNewRunInternal 被 Clamp 到 0~2）：
    //   普通 → 90% 普通档 / 10% 稀有档
    //   困难 → 55% / 40% / 5%（普通/稀有/传奇）
    //   噩梦 → 25% / 60% / 15%
    // 数量区间（含端点）：普通档 3~5、稀有档 2~4、传奇档 1~2。
    // 职业 key 与 MercGrowSprites.JobKey 同套字符串（badge:{职业key}:{档位}，档位 = 普通/稀有/传奇）。

    /// <summary>
    /// 徽记副本通关发放：随机一个职业（6 职业等权）→ 按难度掷档位 → 按档位区间随机数量，
    /// Add 进养成仓库 + 落盘 + Toast。返回本次发放个数（0 = 异常，调用方可以不弹）。
    /// </summary>
    public static int GrantBadgeDungeonDrops(int difficulty)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return 0;
        if (difficulty < 0 || difficulty > 2) difficulty = 0;   // 2026-10-23 拍板：越界按普通难度处理

        // 6 职业等权随机（真源 = BadgeJobKeys，2026-10-24 提为公共字段供 DevGiftMail 共用）
        string jobKey = BadgeJobKeys[Random.Range(0, BadgeJobKeys.Length)];

        // 档位概率（§〇-2 表，真源仅此一处）
        string tier;
        float roll = Random.value;
        if (difficulty == 2)        // 噩梦 25/60/15
            tier = roll < 0.25f ? "普通" : roll < 0.85f ? "稀有" : "传奇";
        else if (difficulty == 1)   // 困难 55/40/5
            tier = roll < 0.55f ? "普通" : roll < 0.95f ? "稀有" : "传奇";
        else                        // 普通 90/10
            tier = roll < 0.90f ? "普通" : "稀有";

        // 数量区间随机（UnityEngine.Random.Range 整数版含下不含上，故 max +1 取到含端点）
        int n = tier == "传奇" ? Random.Range(1, 3)
              : tier == "稀有" ? Random.Range(2, 5)
              : Random.Range(3, 6);

        Add(BadgeId(jobKey, tier), n);
        SaveSystem.Instance?.Save();
        GlobalToastUI.Show("获得 " + jobKey + "徽记 ×" + n, true);   // 2026-10-23 拍板：掉落获得 force 弹（照 GrantStageDrops 风格）
        Debug.Log($"[MercGrow] 徽记副本掉落 难度{difficulty} → {jobKey}徽记({tier}) ×{n}");
        return n;
    }

    // ── 卷轴副本发放（2026-10-24 主人拍板「第三章通关开启 · 每周五 · 每次15体力 · 每日2次」）──────────
    //
    // 🔴 只发招募卷轴：不给金币、不给碎片、不给道具、不给装备（金币本专属金币、徽记本专属徽记、卷轴本专属卷轴）。
    // 档位口径（2026-10-24 主人拍板「普通只掉普通的3-5个 困难掉3-5普通 0-1高级 噩梦1-3高级」）：
    //   普通(0) → 普通招募卷轴 ×3~5
    //   困难(1) → 普通招募卷轴 ×3~5 + 高级招募卷轴 ×0~1
    //   噩梦(2) → 高级招募卷轴 ×1~3（只掉高级，不给普通）
    // （difficulty 越界按 0 处理，照 GrantBadgeDungeonDrops 口径。）

    /// <summary>
    /// 卷轴副本通关发放：按难度掉普通/高级招募卷轴（档位表见上，真源仅此一处）。
    /// 加存档计数 + 落盘 + Toast。
    /// </summary>
    public static void GrantScrollDungeonDrops(int difficulty)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[MercGrow] 卷轴副本掉落失败：存档未就绪（缺真值不发，铁律 14）");
            return;
        }
        if (difficulty < 0 || difficulty > 2) difficulty = 0;   // 越界按普通难度处理（照徽记本口径）

        // UnityEngine.Random.Range 整数版含下不含上，故 max +1 取到含端点
        int common = 0, legendary = 0;
        string msg;
        if (difficulty == 2)          // 噩梦：高级 ×1~3
        {
            legendary = Random.Range(1, 4);
            msg = "获得 高级招募卷轴 ×" + legendary;
        }
        else if (difficulty == 1)     // 困难：普通 ×3~5 + 高级 ×0~1
        {
            common = Random.Range(3, 6);
            legendary = Random.Range(0, 2);
            msg = legendary > 0
                ? "获得 普通招募卷轴 ×" + common + "、高级招募卷轴 ×" + legendary
                : "获得 普通招募卷轴 ×" + common;
        }
        else                          // 普通：普通 ×3~5
        {
            common = Random.Range(3, 6);
            msg = "获得 普通招募卷轴 ×" + common;
        }

        if (common > 0) data.mercScrollCommon += common;
        if (legendary > 0) data.mercScrollLegendary += legendary;
        SaveSystem.Instance?.Save();
        GlobalToastUI.Show(msg, true);
        Debug.Log($"[MercGrow] 卷轴副本掉落 难度{difficulty} → 普通×{common} 高级×{legendary}");
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
    //    入口，禁止各写各的。【2026-10-23 主人拍板】口径 = 一律进战斗外背包（townBagItems，
    //    无容量上限），碎片不许作废；旧存档 frag:{hireId} 迁移也走这里。

    /// <summary>
    /// 发佣兵本命碎片（唯一入口）。【2026-10-23 主人拍板】碎片一律进<b>战斗外背包</b>
    /// （SaveData.townBagItems，角色页可见）——不再进局内战斗背包、不再走邮件溢出
    /// （城镇背包是字典无容量上限，不存在装不下）。升星（MercStarSystem）读写同仓，不再断链。
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

        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError($"[MercGrow] 发碎片失败：存档未就绪（reason={reason}），本次碎片未发放");
            return r;
        }
        data.TownBagAdd(itemId, amount);
        r.bagCount = amount;
        SaveSystem.Instance?.Save();
        UIManager.Instance?.ShowToast($"获得 {def.Name}的本命碎片 ×{amount}", true);   // 2026-10-06 主人拍板：物品获得 force 弹
        Debug.Log($"[MercGrow] 发碎片 {def.Name}({hireId}) ×{amount} → 城镇背包（reason={reason}）");
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
