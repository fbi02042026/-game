using UnityEngine;

/// <summary>
/// 主界面任务栏 · 统一数据出口（2026-10-26 新增，任务体系单独成模块的 UI 边界）。
///
/// 主人拍板（2026-10-26）：
///  · 任务栏 = 主界面左上角那一套（旧 QuestHudBar 2026-10-11 停用，保持停用不复活）；
///  · 三块常驻：主线 / 支线 / 日常（统一叫「日常」）；
///  · **没有支线时隐藏支线行，日常行上移，黑底跟着缩减**——所以本出口把支线做成可空视图，
///    栏（UI）只管「有就摆、没有就藏」，不自己算支线状态。
///
/// 用法：栏每 0.5s（或 OnChanged 时）调 <see cref="Build"/> 拿快照渲染。
/// 支线行点击跳转建议走 <see cref="QuestJumpRouter"/> / 冒险页支线岛槽（AdventureUI.BranchIslands）。
/// </summary>
public static class QuestBarData
{
    /// <summary>支线行视图（null = 当前无支线，栏隐藏该行）。</summary>
    public struct SideIslandView
    {
        public int islandId;     // 岛 id 1..8（SideIslandDefs）
        public string name;      // 岛名（迷雾屿 / 沉船湾…）
        public string desc;      // 展示文案
    }

    /// <summary>任务栏一帧快照。</summary>
    public struct Snapshot
    {
        public MainQuestView main;        // 主线：当前该做的一条（null = 本章全完成/未配表）
        public SideIslandView? side;      // 支线：当前待打的支线岛（null = 隐藏行）
        public DailyQuestView? daily;     // 日常：第一条未完成的（null = 今日全做完）
    }

    /// <summary>组装当前快照。纯读，不写存档、不触发结算（结算归 MainQuestSystem.Tick）。</summary>
    public static Snapshot Build()
    {
        var s = new Snapshot
        {
            main = MainQuestSystem.CurrentView(),
            side = CurrentSideIsland(),
            daily = MainQuestSystem.FirstOpenDaily()
        };
        return s;
    }

    /// <summary>
    /// 当前待打的支线岛（2026-10-23 合并方案的派定语义）：
    ///   branchRoll1 派定且未通关 → 岛1；
    ///   岛1 已通关且 branchRoll2 派定且未通关 → 岛2；
    ///   其余（未派定 / 两岛都通关 / 通关待结算掷派的空窗）→ null = 隐藏行。
    /// </summary>
    /// <summary>支线数据唯一出口（2026-10-26 开放 public：任务栏 SideIslandGuide / 任务栏 Binder 共用，别再各算一份）。</summary>
    public static SideIslandView? CurrentSideIsland()
    {
        var d = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (d == null) return null;

        int id = 0;
        if (d.branchRoll1 > 0 && !SideIslandDefs.IsCleared(d.branchRoll1, d))
            id = d.branchRoll1;
        else if (d.branchRoll2 > 0 && !SideIslandDefs.IsCleared(d.branchRoll2, d))
            id = d.branchRoll2;
        if (id <= 0) return null;

        var isl = SideIslandDefs.Get(id);
        if (isl == null)
        {
            // 派定了不存在的岛 id —— 存档脏数据，报出来别静默（铁律 12/14）
            Debug.LogError($"[QuestBarData] branchRoll 记了不存在的岛 id={id}，支线行隐藏");
            return null;
        }
        return new SideIslandView
        {
            islandId = id,
            name = isl.name,
            // 【2026-10-27 主人拍板】支线文案只要「通关XXX」，不要前缀与玩法说明
            desc = "通关" + isl.name
        };
    }
}
