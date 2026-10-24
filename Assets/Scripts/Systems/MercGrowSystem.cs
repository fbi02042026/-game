using UnityEngine;

/// <summary>
/// 佣兵升阶（局外养成等级）系统（2026-10-23 施工单 T2）。
///
/// 数据真源：SaveData.mercGrow（hireId → MercGrowEntry，默认无记录 = Lv1 无加成）。
/// 按 hireId 存的理由与 mercOutStars 相同：已拥有真源是 unlockedMercIds，多数佣兵没有 MercenaryData 实例。
///
/// 与升星（MercStarSystem）是两套并行的局外系统：🔴 全程不碰本命碎片（GridBackpackSystem 一行不调），
/// 碎片归升星专供【2026-10-23 主人拍板 C1=A】；养成消耗 = 金币 + 职业徽记 两样。
///
/// 对外接口（下游按此签名调用，签名不能改）：
///   LevelOf(hireIdOrAssetId)              查养成等级（默认 1）
///   Get(hireIdOrAssetId)                  查养成记录（无记录返回 null，调用方按 Lv1 处理）
///   AptitudePct(hireIdOrAssetId, kind)    查累计资质（kind: 0=攻 1=防 2=血；返回 0.06f 代表 +6%）
///   SkillTierOf(hireIdOrAssetId)          查技能强化档（0~3，给战斗侧乘 StrengthenMul 用）
///   CanLevelUp(id, out msg, out gold, out badge, out badgeTier, out jobKey)
///                                         能否升级（全量预检，msg 说明缺什么，fail closed）
///   TryLevelUp(id, out msg)               执行升级（整笔扣费 → 掷资质 → 存档；节点级额外扣徽记×5）
///   TryReset(id, out msg)                 重置（归 1、属性清零；金币返 50%、徽记 100% 返还）
///
/// 数值真源全在 MercGrowDefs，本类只做流程。材料不足 fail closed，禁止静默兜底（项目纪律 12）。
/// </summary>
public static class MercGrowSystem
{
    /// <summary>查养成记录（hireId / AssetId 都吃；非花名册佣兵或无记录返回 null，调用方按 Lv1 处理）。</summary>
    // ⚠ MercGrowEntry 是与 SaveData 平级的顶层类（SaveData.cs:32，与 StringIntEntry 同款），
    //    不是 SaveData 的嵌套类 —— 写成 SaveData.MercGrowEntry 会 CS0234，这里只能写裸类名。
    public static MercGrowEntry Get(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return null;
        return SaveSystem.Instance?.Data?.GetMercGrow(hireId);
    }

    /// <summary>查养成等级（默认 1；无记录 / 未就绪 / 脏数据都按 1）。</summary>
    public static int LevelOf(string hireIdOrAssetId)
    {
        var e = Get(hireIdOrAssetId);
        return e != null && e.level >= 1 ? e.level : 1;
    }

    /// <summary>
    /// 查累计资质百分比（kind: 0=攻 1=防 2=血；返回 0.06f 代表 +6%）。
    /// 数值是升级时掷定即存的累计值，本函数只读不加权。
    /// </summary>
    public static float AptitudePct(string hireIdOrAssetId, int kind)
    {
        var e = Get(hireIdOrAssetId);
        if (e == null) return 0f;
        switch (kind)
        {
            case 0: return e.aptAtk;
            case 1: return e.aptDef;
            case 2: return e.aptHp;
            default: return 0f;
        }
    }

    /// <summary>查技能强化档（0~3，给战斗侧乘 MercGrowDefs.StrengthenMul 用；无记录 = 0 档）。</summary>
    public static int SkillTierOf(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def)) return 0;
        return MercGrowDefs.SkillTier(LevelOf(hireId), def.Rarity);
    }

    /// <summary>
    /// 能否升级（全量预检，含节点加码）。不能时 msg 说明缺什么（fail closed，说清缺哪个材料）。
    /// 出参供 UI 显示消耗：gold=金币；badge/badgeTier=徽记数量与档位（节点级已并入）；
    /// jobKey=徽记职业 key（识别失败时为 null 且返回 false）。
    /// </summary>
    public static bool CanLevelUp(string id, out string msg, out int gold, out int badge, out string badgeTier, out string jobKey)
    {
        msg = ""; gold = 0; badge = 0; badgeTier = null; jobKey = null;

        string hireId = MercRosterDefs.ResolveHireId(id);
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def))
        {
            msg = "花名册里查不到这名佣兵";
            return false;
        }

        // 徽记职业 key：用 MercGrowSprites 的「佣兵职业名 → 徽记文件前缀」映射（6 大类）。
        // 取不到必须报错，不许静默兜底（项目纪律 12）。
        jobKey = MercGrowSprites.JobKey(def.JobName);
        if (string.IsNullOrEmpty(jobKey))
        {
            Debug.LogError($"[MercGrow] {def.Name}({hireId}) 职业名「{def.JobName}」识别不出徽记大类（JobKey=null），无法养成——请补 MercGrowSprites.JobKey 映射");
            msg = "职业识别失败，无法养成（详见日志）";
            return false;
        }

        var data = SaveSystem.Instance?.Data;
        if (data == null) { msg = "存档未就绪"; return false; }
        if (!data.IsMercUnlocked(hireId)) { msg = "尚未获得该佣兵"; return false; }

        var e = data.GetMercGrow(hireId);
        int cur = e != null && e.level >= 1 ? e.level : 1;

        int maxLevel = MercGrowDefs.MaxLevel(def.Rarity);
        if (cur >= maxLevel)
        {
            msg = $"{def.Name} 已满级";
            return false;
        }

        // 本级消耗（升到 cur+1 这级）：金币 + 当级徽记
        gold = MercGrowDefs.GoldCost(def.Rarity, cur);
        int perLevelBadge = MercGrowDefs.BadgeCountForLevel(cur);
        string perLevelTier = MercGrowDefs.BadgeTierForLevel(cur);

        // 升阶节点（10/20/30/40/50）：额外扣当段徽记 ×5（没有碎片项，C1=A）
        int newLevel = cur + 1;
        int nodeBadge = MercGrowDefs.IsAscendNode(newLevel) ? MercGrowDefs.AscendNodeBadgeCount : 0;
        string nodeTier = MercGrowDefs.AscendNodeBadgeTier(newLevel);

        // 聚合徽记出参：当级段与节点段按阶梯对齐时同档直接相加；当级免徽记时只出节点档
        if (perLevelBadge > 0 && nodeBadge > 0)
        {
            badge = perLevelBadge + nodeBadge;
            badgeTier = perLevelTier;
        }
        else if (perLevelBadge > 0) { badge = perLevelBadge; badgeTier = perLevelTier; }
        else if (nodeBadge > 0) { badge = nodeBadge; badgeTier = nodeTier; }
        else { badge = 0; badgeTier = null; }

        // —— 全量预检：金币 + 徽记，任一不足整笔不动 ——
        long haveGold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        if (haveGold < gold)
        {
            msg = $"金币不足：还需 {gold - haveGold}（持有 {haveGold}/{gold}）";
            return false;
        }
        if (badge > 0)
        {
            string badgeId = MercGrowInventory.BadgeId(jobKey, badgeTier);
            int haveBadge = MercGrowInventory.Get(badgeId);
            if (haveBadge < badge)
            {
                string tierName = MercStarSystem.BadgeTierDisplayName(badgeTier);
                msg = $"{tierName}徽记不足：还需 {badge - haveBadge}（持有 {haveBadge}/{badge}）";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 执行升级：全量预检 → 扣金币 → 扣当级徽记 →（节点级）扣节点徽记×5 → level+1 → 掷资质 → 存档。
    /// 🔴 扣费顺序：先全量预检，任一不足整笔不动；节点徽记扣不动则整笔回滚（已扣的金币/徽记还回去）。
    /// 🔴 全程不碰背包碎片（碎片归升星专供，C1=A）。
    /// </summary>
    public static bool TryLevelUp(string id, out string msg)
    {
        msg = "";
        if (!CanLevelUp(id, out msg, out int gold, out int badge, out string badgeTier, out string jobKey)) return false;

        string hireId = MercRosterDefs.ResolveHireId(id);
        var data = SaveSystem.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(hireId)) { msg = "存档未就绪"; return false; }
        MercRosterDefs.TryGetByHireId(hireId, out var def);

        var e = data.GetMercGrow(hireId);
        if (e == null)
        {
            e = new MercGrowEntry { hireId = hireId };
            data.mercGrow ??= new System.Collections.Generic.Dictionary<string, MercGrowEntry>();
            data.mercGrow[hireId] = e;
        }
        int cur = e.level >= 1 ? e.level : 1;
        int newLevel = cur + 1;

        // 当级徽记与节点徽记分开记（聚合值在预检里已验过）
        int perLevelBadge = MercGrowDefs.BadgeCountForLevel(cur);
        string perLevelTier = MercGrowDefs.BadgeTierForLevel(cur);
        bool isNode = MercGrowDefs.IsAscendNode(newLevel);
        int nodeBadge = isNode ? MercGrowDefs.AscendNodeBadgeCount : 0;
        string nodeTier = MercGrowDefs.AscendNodeBadgeTier(newLevel);

        // ① 扣金币（save:false，最后统一存一次，保证整笔原子性）
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, gold, save: false, notify: false))
        {
            // fail closed：预检过了还扣不动，说明钱包状态与预检不一致，报出来人工看
            Debug.LogError($"[MercGrow] {def.Name}({hireId}) 升级扣金币失败（预检通过但扣除失败，need={gold}）——数据异常，未升级");
            msg = "金币扣除失败，请重试";
            return false;
        }

        // ② 扣当级徽记
        if (perLevelBadge > 0)
        {
            if (!MercGrowInventory.TrySpend(MercGrowInventory.BadgeId(jobKey, perLevelTier), perLevelBadge))
            {
                msg = RollbackAndFail(hireId, def.Name, gold, perLevelBadge, perLevelTier, jobKey, $"扣{MercStarSystem.BadgeTierDisplayName(perLevelTier)}徽记失败");
                return false;
            }
        }

        // ③ 节点级：额外扣当段徽记 ×5；扣不动整笔回滚
        if (nodeBadge > 0)
        {
            if (!MercGrowInventory.TrySpend(MercGrowInventory.BadgeId(jobKey, nodeTier), nodeBadge))
            {
                msg = RollbackAndFail(hireId, def.Name, gold, perLevelBadge, perLevelTier, jobKey, $"升阶节点扣{MercStarSystem.BadgeTierDisplayName(nodeTier)}徽记失败");
                return false;
            }
        }

        // ④ 升级 + 掷资质（保底三项 + 额外一项，lastRolled 保底轨道；掷定即存防 SL 刷）
        e.level = newLevel;
        RollAptitudes(e, cur, newLevel);
        if (isNode) e.ascended++;   // 过节点 +1 阶（一期不发技能，Lv20/Lv40 只加码消耗，C3=A）

        SaveSystem.Instance.Save();
        string name = def.Name ?? hireId;
        string nodeNote = isNode ? $"，升阶节点：{MercStarSystem.BadgeTierDisplayName(nodeTier)}徽记 ×{nodeBadge}（第 {e.ascended} 阶）" : "";
        Debug.Log($"[MercGrow] {name}({hireId}) 升级：Lv{cur} → Lv{newLevel}，消耗金币 {gold}{(perLevelBadge > 0 ? $" + {MercStarSystem.BadgeTierDisplayName(perLevelTier)}徽记 ×{perLevelBadge}" : "")}{nodeNote}");
        msg = isNode ? $"{name} 升级成功（Lv{newLevel}，升阶！第 {e.ascended} 阶）" : $"{name} 升级成功（Lv{newLevel}）";
        return true;
    }

    /// <summary>
    /// 重置养成：等级归 1、资质/轨道/阶位清零；金币返还 50%（向下取整）、徽记 100% 返还。
    /// 属性不重掷（归零即可，下次升级自然重掷）。🔴 不涉及任何碎片。
    /// </summary>
    public static bool TryReset(string id, out string msg)
    {
        msg = "";
        string hireId = MercRosterDefs.ResolveHireId(id);
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def))
        {
            msg = "花名册里查不到这名佣兵";
            return false;
        }

        string jobKey = MercGrowSprites.JobKey(def.JobName);
        if (string.IsNullOrEmpty(jobKey))
        {
            // 取不到就 LogError，不许静默兜底（项目纪律 12）
            Debug.LogError($"[MercGrow] {def.Name}({hireId}) 职业名「{def.JobName}」识别不出徽记大类（JobKey=null），无法重置——请补 MercGrowSprites.JobKey 映射");
            msg = "职业识别失败，无法重置（详见日志）";
            return false;
        }

        var data = SaveSystem.Instance?.Data;
        if (data == null) { msg = "存档未就绪"; return false; }
        var e = data.GetMercGrow(hireId);
        if (e == null || e.level <= 1) { msg = $"{def.Name} 尚未养成（Lv1），无需重置"; return false; }
        if (!data.IsMercUnlocked(hireId)) { msg = "尚未获得该佣兵"; return false; }

        // 逐级反推总消耗（与升级扣费同源，保证返还口径一致）
        long totalGold = 0;
        var badgeRefund = new System.Collections.Generic.Dictionary<string, int>();   // key = BadgeId
        for (int lv = 1; lv < e.level; lv++)
        {
            totalGold += MercGrowDefs.GoldCost(def.Rarity, lv);
            int cnt = MercGrowDefs.BadgeCountForLevel(lv);
            string tier = MercGrowDefs.BadgeTierForLevel(lv);
            if (cnt > 0 && !string.IsNullOrEmpty(tier))
            {
                string bid = MercGrowInventory.BadgeId(jobKey, tier);
                badgeRefund.TryGetValue(bid, out int c);
                badgeRefund[bid] = c + cnt;
            }
            if (MercGrowDefs.IsAscendNode(lv + 1))
            {
                string nTier = MercGrowDefs.AscendNodeBadgeTier(lv + 1);
                string nBid = MercGrowInventory.BadgeId(jobKey, nTier);
                badgeRefund.TryGetValue(nBid, out int c);
                badgeRefund[nBid] = c + MercGrowDefs.AscendNodeBadgeCount;
            }
        }

        int oldLevel = e.level;
        int oldAscended = e.ascended;

        // 清零（属性不重掷，归零即可）
        e.level = 1;
        e.lastRolled = -1;
        e.aptAtk = e.aptDef = e.aptHp = 0f;
        e.ascended = 0;

        // 返还：金币 50%（向下取整）；徽记 100%。save:false 统一最后存
        long goldBack = totalGold / 2;
        if (goldBack > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, goldBack, save: false, notify: true, overflowToMail: false);
        foreach (var kv in badgeRefund)
            MercGrowInventory.Add(kv.Key, kv.Value);

        SaveSystem.Instance.Save();
        Debug.Log($"[MercGrow] {def.Name}({hireId}) 重置：Lv{oldLevel} → Lv1（第 {oldAscended} 阶清零），返还金币 {goldBack}/{totalGold}（50%）+ 徽记 100%");
        msg = $"{def.Name} 重置成功（返还金币 {goldBack}，徽记全额返还）";
        return true;
    }

    // ============================ 内部 ============================

    /// <summary>
    /// 掷资质（升级时调用）：三项各加保底 FloorPctFor(cur)；再等权抽 1 项额外 +3~5%；
    /// newLevel 是 5 的倍数时，额外项先从「非上轮项」里抽（lastRolled 保底轨道，防一路全落同一项）。
    /// 掷定即存（防 SL 刷）。数值口径全在 MercGrowDefs。
    /// </summary>
    static void RollAptitudes(MercGrowEntry e, int currentLevel, int newLevel)
    {
        float floorPct = MercGrowDefs.FloorPctFor(currentLevel);
        e.aptAtk += floorPct;
        e.aptDef += floorPct;
        e.aptHp += floorPct;

        // 额外项候选：默认三项等权；5 的倍数级且上轮有记录 → 只从非上轮项里抽
        int last = e.lastRolled;
        bool avoidLast = MercGrowDefs.ExtraRollAvoidsLast(newLevel) && last >= 0 && last < MercGrowDefs.AptitudeKindCount;
        int pick;
        if (avoidLast)
        {
            // 三项里排除 last，剩两项等权
            int a = -1, b = -1;
            for (int k = 0; k < MercGrowDefs.AptitudeKindCount; k++)
            {
                if (k == last) continue;
                if (a < 0) a = k; else b = k;
            }
            pick = Random.Range(0f, 1f) < 0.5f ? a : b;
        }
        else
        {
            pick = Random.Range(0, MercGrowDefs.AptitudeKindCount);
        }

        float extra = Random.Range(MercGrowDefs.ExtraRollPctMin, MercGrowDefs.ExtraRollPctMax);
        switch (pick)
        {
            case 0: e.aptAtk += extra; break;
            case 1: e.aptDef += extra; break;
            default: e.aptHp += extra; break;
        }
        e.lastRolled = pick;   // 保底轨道记录（下次 5 的倍数级避着抽）
    }

    /// <summary>升级中途扣费失败 → 整笔回滚（金币 + 已扣的徽记还回去），返回 fail closed 文案。</summary>
    static string RollbackAndFail(string hireId, string name, int gold, int badge, string badgeTier, string jobKey, string reason)
    {
        // 金币还回去（不弹 toast 不邮件溢出——异常路径，日志里看）
        if (gold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: false, notify: false, overflowToMail: false);
        if (badge > 0)
            MercGrowInventory.Add(MercGrowInventory.BadgeId(jobKey, badgeTier), badge);
        SaveSystem.Instance?.Save();
        Debug.LogError($"[MercGrow] {name}({hireId}) 升级失败已整笔回滚：{reason}（金币 {gold} 已退还，徽记已退还）——数据异常，请人工核查");
        return "升级失败，材料已退还，请重试";
    }
}
