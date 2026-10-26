using UnityEngine;

/// <summary>
/// 【2026-10-23 主人拍板】支线岛屿系统 · 数据层（批 S1 → 合并方案定稿）。
/// 设计真源：<c>Docs/支线岛屿系统_设计方案_待办任务清单.md</c>
///
/// 【2026-10-23 合并方案（主人拍板「岛壳用那边 + 内容用我的」）】
/// · **岛槽是壳（2 个）**：冒险页主线窗口旁摆 2 个岛槽（UI 见 AdventureUI.BranchIslands），
///   引导完成后随机派第一个岛（branchRoll1 记岛 id，2026-10-26 主人拍板「第一章可以出现支线」）、
///   通关该岛后自动派第二个（branchRoll2）；
///   派定真源 = ChapterRouteTable.EnsureForkRoll（洗牌袋只掷没通关过的岛）。
/// · **岛内容是池（8 种）**：每岛 = **单 Boss 关**，**一次性**（打完锁，跨周目持久）；
///   8 岛全通后洗牌袋重置（清通关记录，开启新一轮，见 SaveData.ResetSideIslandClears）。
/// · 与主线**完全旁挂**：主线已改线性 1→8（ChapterRouteTable 合并方案），岛不占主线解锁链；
/// · 进岛扣 <see cref="SIDE_ISLAND_STAMINA_COST"/> 体力；奖励 = 固定局外金币 + 天赋石 + Boss 装备掉落（批 S3 接掉落链）；
/// · 命名不用数字（主人原话「名字不用数字了 可以改成特殊点的」）；下表草稿名主人可全部重起。
/// ⚠️ Boss/怪物配置（「换个 Boss 或怪」）批 S3 接，本文件先立岛表与奖励真源。
/// </summary>
public static class SideIslandDefs
{
    /// <summary>进一个支线岛消耗的体力（2026-10-23 主人拍板：10；与无限裂隙同价）。想改就改这一个数。</summary>
    public const int SIDE_ISLAND_STAMINA_COST = 10;

    /// <summary>支线岛定义。</summary>
    public class Island
    {
        public int id;              // 岛序号 1..8（= 全局链式解锁顺序）
        public string name;         // 特殊名字（非数字）
        public int act;             // 所属篇（1..8，每篇 1 岛）
        public int goldReward;      // 通关固定局外金币
        public int talentReward;    // 通关天赋石
    }

    /// <summary>
    /// 岛表（<b>真源</b>）。奖励数值为初始值，主人可调（只改这里的数）；
    /// 名字为草稿（2026-10-23），主人可全部重起。
    /// </summary>
    static readonly Island[] Islands =
    {
        new Island { id = 1, name = "迷雾屿",   act = 1, goldReward = 400,  talentReward = 10 },
        new Island { id = 2, name = "沉船湾",   act = 2, goldReward = 600,  talentReward = 12 },
        new Island { id = 3, name = "翡翠秘境", act = 3, goldReward = 800,  talentReward = 14 },
        new Island { id = 4, name = "晨曦草原", act = 4, goldReward = 1000, talentReward = 16 },
        new Island { id = 5, name = "搁浅地",   act = 5, goldReward = 1200, talentReward = 18 },
        new Island { id = 6, name = "王座间",   act = 6, goldReward = 1400, talentReward = 20 },
        new Island { id = 7, name = "深渊口",   act = 7, goldReward = 1600, talentReward = 22 },
        new Island { id = 8, name = "黑曜祭坛", act = 8, goldReward = 1800, talentReward = 24 },
    };

    public static int Count => Islands.Length;

    /// <summary>取岛定义（id 1..8）。越界返回 null —— 调用方自己 LogError，不静默兜底（铁律 14）。</summary>
    public static Island Get(int id)
    {
        if (id < 1 || id > Islands.Length) return null;
        return Islands[id - 1];
    }

    /// <summary>
    /// 【2026-10-23 合并方案退役】原「全局链式解锁」判据（岛 1=通关第 1 篇、岛 n=岛 n-1 通关）——
    /// 合并后解锁改由岛槽派定制（branchRoll1/2 记岛 id，UI 见 AdventureUI.BranchIslands），
    /// 本方法不再被调用，保留不删（下线≠删除）。
    /// </summary>
    public static bool IsUnlocked(int id, SaveData data)
    {
        if (data == null || Get(id) == null) return false;
        if (id == 1) return data.HasClearedChapter(1);
        return data.HasClearedSideIsland(id - 1);
    }

    /// <summary>岛是否已通关（一次性，打完锁，跨周目持久）。纯查询。合并方案后仍有效（岛槽已通关态用它判）。</summary>
    public static bool IsCleared(int id, SaveData data)
        => Get(id) != null && data != null && data.HasClearedSideIsland(id);

    /// <summary>【2026-10-23 合并方案退役】原三态（0/1/2）按岛 id 判——合并后岛槽状态由 UI 按 branchRoll 派定算，本方法保留不删。</summary>
    public static int StateOf(int id, SaveData data)
    {
        if (IsCleared(id, data)) return 2;
        if (IsUnlocked(id, data)) return 1;
        return 0;
    }

    /// <summary>
    /// 自检（改完本文件跑一次）：打印 8 岛配置 + 对账。不对就 LogError，不静默（铁律 14）。
    /// Editor 菜单入口见 Assets/Editor/SideIslandSelfCheck.cs（批 S1）。
    /// </summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[支线岛自检] 共 " + Count + " 岛（每篇 1 岛 · 单 Boss 关 · 一次性 · 链式解锁）");
        for (int i = 1; i <= Count; i++)
        {
            var isl = Get(i);
            if (isl == null) { Debug.LogError($"[支线岛自检] 岛 {i} 定义缺失"); continue; }
            sb.AppendLine($"  岛{i}「{isl.name}」篇{isl.act}　金币 {isl.goldReward}　天赋石 {isl.talentReward}");
            if (isl.act != i)
                Debug.LogError($"[支线岛自检] 岛 {i} 所属篇应为 {i}，实为 {isl.act}（每篇 1 岛口径坏了）");
            if (isl.goldReward <= 0 || isl.talentReward <= 0)
                Debug.LogError($"[支线岛自检] 岛 {i} 奖励非法（gold={isl.goldReward} talent={isl.talentReward}）");
        }
        Debug.Log(sb.ToString());
    }
}
