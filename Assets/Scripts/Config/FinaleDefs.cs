using UnityEngine;

/// <summary>
/// 终章追查线 · 关卡数据层（2026-10-23 主人拍板「最后一关后剧情二选一，选完再出几关进完整剧情」）。
/// 流程：主线最后一关通关 → N8 结局演出 → 终章余音剧情二选一（回家 / 追查真相）→
///       选「追查」解锁本表 2 个旁挂关（冒险页入口）→ 打完接完整揭底演出。
/// 设计真源：Docs/终章分支追查线_待办任务清单.md
///
/// · 两关都走单 Boss 战（stageIndex=0 + StageType.Boss，与支线岛/金币本同款已验证链）；
///   act=8 复用第 8 篇背景与怪池（P0 专属素材没到位前的占位口径，改 act 即换）；
/// · 关卡产出照主线普通关（材料/关卡金），不掉装备、不发局内抽奖币；
/// · ⚠️ 名字为草稿（2026-10-23），主人可全部重起（命名调性参照 SideIslandDefs）。
/// </summary>
public static class FinaleDefs
{
    /// <summary>追查关定义。</summary>
    public class Stage
    {
        public int id;          // 追查关序号 1..2
        public string name;     // 草稿名，主人可改
        public int act;         // 背景篇（怪池/背景资产同源复用）
    }

    /// <summary>
    /// 追查关表（<b>真源</b>）。第 1 关打完插揭底中段剧情，第 2 关打完接完整揭底演出。
    /// </summary>
    static readonly Stage[] Stages =
    {
        new Stage { id = 1, name = "装备科的灯", act = 8 },
        new Stage { id = 2, name = "装箱人的账", act = 8 },
    };

    public static int Count => Stages.Length;

    /// <summary>取追查关定义（id 1..2）。越界返回 null —— 调用方自己 LogError，不静默兜底（铁律 14）。</summary>
    public static Stage Get(int id)
    {
        if (id < 1 || id > Stages.Length) return null;
        return Stages[id - 1];
    }
}
