/// <summary>
/// 乱入事件·「按剧情节点取事件」的唯一分派出口（T2/T3 及后续 T4/T5/T6 共用）。
/// 职责：查 IntruderEventTable.GetByStoryNode(storyNode) → 找 type=="Intruder" 的事件
/// → 调 IntruderPicker.TryPick 取固定反派三人组 AssetId（批次6 拍板，非随机）。
/// 🔴 只做查询 / 分派：不生成单位、不播剧情（那是 T2/T3 的事）；构造期禁读表（走表懒加载）。
/// 🔴 T4/T5/T6 后续复用本出口按同一 storyNode 口径取 Rescue / Entry 事件，别在各节点各写查表。
/// 无匹配返回 false 属正常（大多数关没有乱入），不报 Error。
/// </summary>
public static class IntruderEventDirector
{
    /// <summary>门禁查询：storyNode 上是否挂了指定 type 的事件。</summary>
    public static bool HasEvent(string storyNode, string type)
    {
        var rows = IntruderEventTable.GetByStoryNode(storyNode);
        for (int i = 0; i < rows.Count; i++)
            if (string.Equals(rows[i].type, type, System.StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>
    /// 分派出口：按剧情节点取「敌对乱入」事件，取固定反派三人组 AssetId（批次6，非随机）。
    /// 无匹配 / 抽取失败都返回 false（正常流程，不报 Error）；assetIds 为 null 时调用方不替换。
    /// </summary>
    public static bool TryPickIntruderForNode(string storyNode, int seed, out string[] assetIds)
    {
        assetIds = null;
        var rows = IntruderEventTable.GetByStoryNode(storyNode);
        for (int i = 0; i < rows.Count; i++)
        {
            if (!string.Equals(rows[i].type, "Intruder", System.StringComparison.Ordinal))
                continue;
            return IntruderPicker.TryPick(seed, out assetIds);
        }
        return false;
    }

    /// <summary>
    /// 查询出口（本出口的查询侧）：按剧情节点取第一条「敌对乱入」事件 Row，
    /// 供 T2 的 Plan 阶段读 chapter / triggerNode / replaceCount / eventId 来定「哪一波、换几只」。
    /// 仅返回配置数据，不生成、不播剧情。
    /// </summary>
    public static bool TryGetIntruderForNode(string storyNode, out IntruderEventTable.Row row)
    {
        var rows = IntruderEventTable.GetByStoryNode(storyNode);
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].type, "Intruder", System.StringComparison.Ordinal))
            {
                row = rows[i];
                return true;
            }
        }
        row = default;
        return false;
    }

    /// <summary>
    /// 查询出口（T4 友军救场·主线）：按章号取第一条 type=="Rescue" 的事件（Row.chapter 匹配）。
    /// 无匹配返回 false 属正常（该章没配救场），不报 Error。
    /// </summary>
    public static bool TryGetRescueForChapter(int chapter, out IntruderEventTable.Row row)
    {
        var rows = IntruderEventTable.GetAll();
        for (int i = 0; i < rows.Count; i++)
        {
            if (!string.Equals(rows[i].type, "Rescue", System.StringComparison.Ordinal))
                continue;
            if (rows[i].chapter != chapter)
                continue;
            row = rows[i];
            return true;
        }
        row = default;
        return false;
    }

    /// <summary>
    /// 查询出口（T4 友军救场·支线岛）：按岛 id 取第一条 type=="Rescue" 的事件（Row.islandId 匹配）。
    /// islandId 传字符串（SideIslandId.ToString()），内部 TryInt 解析；解析失败返回 false。
    /// </summary>
    public static bool TryGetRescueForIsland(string islandId, out IntruderEventTable.Row row)
    {
        row = default;
        if (string.IsNullOrEmpty(islandId) || !int.TryParse(islandId, out int id))
            return false;
        var rows = IntruderEventTable.GetAll();
        for (int i = 0; i < rows.Count; i++)
        {
            if (!string.Equals(rows[i].type, "Rescue", System.StringComparison.Ordinal))
                continue;
            if (rows[i].islandId != id)
                continue;
            row = rows[i];
            return true;
        }
        return false;
    }

    /// <summary>
    /// 查询出口（T6 入场救援）：按剧情节点（preBattle:章）取第一条 type=="Entry" 的事件。
    /// 与 Intruder 节点同口径：Entry 一律挂 preBattle:章（2026-11-20 拍板⑥）。
    /// </summary>
    public static bool TryGetEntryForNode(string storyNode, out IntruderEventTable.Row row)
    {
        var rows = IntruderEventTable.GetByStoryNode(storyNode);
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].type, "Entry", System.StringComparison.Ordinal))
            {
                row = rows[i];
                return true;
            }
        }
        row = default;
        return false;
    }
}
