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
    /// 分派出口：按剧情节点取「敌对乱入」事件，按章取该章强盗组合 AssetId（2026-11-22 递进拍板）。
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
            // 章号取表行真源（与配表 replaceCount 同章），不靠 seed 反推，避免章节判据漂移。
            return IntruderPicker.TryPick(rows[i].chapter, seed, out assetIds);
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
    /// 【强盗投放关位唯一真源（2026-11-23 主人拍板每章第6关）】
    /// 8 章递进组合不变，只把触发关位收敛到 stageIdx0=5（第6关，0-based），
    /// 避开章节剧情关（第1关）、Boss 救援关（第5/10关）、精英关（第3/8关）。
    /// 所有“只在该关触发”的门禁统一读这里；返回 5 不随 chapter 变化。
    /// </summary>
    public static int IntruderStageIndexForChapter(int chapter)
    {
        if (ActProgressionDefs.TypeOfStage(5) != StageType.Normal)
            UnityEngine.Debug.LogError("[IntruderEventDirector] 第6关不再是普通关，强盗投放会撞 Boss/精英剧情，请重选关位并同步 IntruderStageIndexForChapter");
        return 5;
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
