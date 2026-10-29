using UnityEngine;

/// <summary>
/// 佣兵局外升星系统（2026-10-22 施工单 T7/T11）。
///
/// 数据真源：SaveData.mercOutStars（hireId → outStar，默认 0）。为什么按 hireId 存而不只挂在
/// MercenaryData.outStar：当前版本「已拥有」的真源是 unlockedMercIds（hireId 集合），
/// 大多数已解锁佣兵并没有 MercenaryData 实例（permanentMercs 只是历史兼容），
/// 按 uid 找不到记录就没法升星 —— 所以落 hireId 键，MercenaryData.outStar 字段保留
/// 供已有实例同步（施工单 T7 口径），两处由 BuildMercData / 入队链路保持一致。
///
/// 对外接口（升星界面主人自己做，T11 只留数据接口）：
///   OutStarOf(hireIdOrAssetId)          查当前星（-1 = 非花名册佣兵）
///   GetBagFragCount(hireIdOrAssetId)    城镇背包里该佣兵本命碎片数（townBagItems，2026-10-23）
///   GetBadgeCount(hireIdOrAssetId)      该佣兵升星所用职业徽记持有数（2026-10-23 新增）
///   CanStarUp(hireIdOrAssetId, out msg) 能否升星（msg 说明缺什么，fail closed）
///   TryStarUp(hireIdOrAssetId)          执行升星（扣职业徽记 → 扣城镇背包碎片 → outStar+1 → 存档；
///                                       2026-10-23 主人拍板扣费顺序：徽记 → 碎片 → 星级）
///
/// 红线：不动局内 star（1~5）与 RunLoadout.MaxMercStar；无城镇等级门。
/// </summary>
public static class MercStarSystem
{
    /// <summary>
    /// 查该佣兵当前局外星级（0~MaxOutStar）。
    /// hireId / AssetId 都吃；非花名册佣兵返回 -1（调用方据此跳过加成，不误当 0 星）。
    /// </summary>
    public static int OutStarOf(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return -1;
        var data = SaveSystem.Instance?.Data;
        return data != null ? data.GetMercOutStar(hireId) : 0;
    }

    /// <summary>
    /// 战斗内抽卡用的局内星级映射（2026-10-22 主人拍板 T13）：
    /// 已拥有 → 局内星级 = outStar；未拥有 = 0 星；0 星直接用现 ★1 档（MpProfile/PowerCalc
    /// 都是 max(1, star)，天然对齐）→ 返回值恒 ≥1，局内 star（1~5）体系不动。
    /// </summary>
    public static int InRunStarOf(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return 1;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return 1;
        if (!data.IsMercUnlocked(hireId)) return 1;   // 未拥有 = 0 星 = ★1 档
        int outStar = data.GetMercOutStar(hireId);
        return outStar < 1 ? 1 : outStar;
    }

    /// <summary>
    /// 该佣兵本命碎片数。【2026-10-23 主人拍板】碎片一律存战斗外背包（SaveData.townBagItems，
    /// 角色页可见），与发放口 MercGrowInventory.GrantFragments 同仓 —— 读写同容器不断链。
    /// </summary>
    public static int GetBagFragCount(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return 0;
        string itemId = MercStarDefs.FragmentItemId(hireId);
        if (string.IsNullOrEmpty(itemId)) return 0;
        var data = SaveSystem.Instance?.Data;
        return data != null ? data.TownBagCount(itemId) : 0;
    }

    /// <summary>下一星碎片消耗；已满星（含超界）或非花名册返回 0。</summary>
    public static int GetNextStarCost(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return 0;
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def)) return 0;
        int cur = OutStarOf(hireId);
        if (cur < 0 || cur >= MercStarDefs.MaxOutStar) return 0;
        return MercStarDefs.StarUpCost(def.Rarity, cur + 1);
    }

    /// <summary>
    /// 该佣兵升星所用**职业徽记**的持有数（2026-10-23 主人拍板：升星 2/3 星加徽记消耗，传奇 1 星起）。
    /// 徽记档位随稀有度：普通=低级、稀有=中级、传奇=高级（MercStarDefs.BadgeTierOf）。
    /// </summary>
    public static int GetBadgeCount(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def)) return 0;
        string jobKey = MercGrowSprites.JobKey(def.JobName);
        if (string.IsNullOrEmpty(jobKey))
        {
            // 项目纪律 12：不静默兜底，识别不出就报出来人工补映射
            Debug.LogError($"[MercStar] {def.Name}({hireId}) 职业名「{def.JobName}」识别不出徽记职业 key —— 请补 MercGrowSprites.JobKey 映射");
            return 0;
        }
        return MercGrowInventory.Get(MercGrowInventory.BadgeId(jobKey, MercStarDefs.BadgeTierOf(def.Rarity)));
    }

    /// <summary>
    /// 徽记档位显示名（"普通"→低级、"稀有"→中级、"传奇"→高级；2026-10-23 文案口径）。
    /// 【2026-10-23 主人拍板】全项目唯一出口：MercAscendUI 徽记行、MercGrowSystem 升级提示都走这里，
    /// MercGrowSystem 原先那份私有副本已删除，映射规则不许再抄第二遍。
    /// </summary>
    public static string BadgeTierDisplayName(string tier)
    {
        switch (tier)
        {
            case "普通": return "低级";
            case "稀有": return "中级";
            case "传奇": return "高级";
            default: return tier ?? "";
        }
    }

    /// <summary>能否升星。不能时 msg 说明原因（材料不足要说清缺什么——fail closed）。</summary>
    public static bool CanStarUp(string hireIdOrAssetId, out string msg)
    {
        msg = "";
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def))
        {
            msg = "花名册里查不到这名佣兵";
            return false;
        }
        var data = SaveSystem.Instance?.Data;
        if (data == null) { msg = "存档未就绪"; return false; }
        if (!data.IsMercUnlocked(hireId)) { msg = "尚未获得该佣兵"; return false; }

        int cur = data.GetMercOutStar(hireId);
        if (cur >= MercStarDefs.MaxOutStar)
        {
            msg = $"{def.Name} 已满星（{MercStarDefs.MaxOutStar} 星）";
            return false;
        }
        int need = MercStarDefs.StarUpCost(def.Rarity, cur + 1);
        int have = GetBagFragCount(hireId);
        if (have < need)
        {
            msg = $"{def.Name}的本命碎片不足：还需 {need - have} 个（持有 {have}/{need}）";
            return false;
        }

        // 徽记预检（2026-10-23 主人拍板：升星 2/3 星加该职业徽记消耗，传奇 1 星起就要；need>0 才查）
        int badgeNeed = MercStarDefs.BadgeCost(def.Rarity, cur + 1);
        if (badgeNeed > 0)
        {
            int badgeHave = GetBadgeCount(hireId);
            if (badgeHave < badgeNeed)
            {
                msg = $"{BadgeTierDisplayName(MercStarDefs.BadgeTierOf(def.Rarity))}徽记不足：还需 {badgeNeed - badgeHave}（持有 {badgeHave}/{badgeNeed}）";
                return false;
            }
        }

        // 资金预检（2026-10-23 主人拍板：金币/钻石并入权威校验，与 TryStarUp 的扣费同事务前置）。
        // 规则只有 FundEnough 这一处 —— MercAscendUI.Fill 的消耗数字变色也转调它，不抄第二遍。
        if (!FundEnough(def.Rarity, cur + 1, out string fundMsg))
        {
            msg = fundMsg;
            return false;
        }
        return true;
    }

    /// <summary>
    /// 升星资金预检 —— 【2026-10-23 主人拍板】全项目唯一出口（原先只在 MercAscendUI 里，
    /// 与 TryStarUp 的扣费脱节 → 出现过「预检过了、升完星却扣不动钱」的窗口）。
    /// 够 → true；不够 → false + msg（缺哪种、还差多少）。
    /// </summary>
    public static bool FundEnough(MercRosterDefs.MercRarity rarity, int targetStar, out string msg)
    {
        long goldCost = MercStarDefs.GoldCost(rarity, targetStar);
        long diaCost = MercStarDefs.DiamondCost(rarity, targetStar);
        var data = SaveSystem.Instance?.Data;
        if (goldCost > 0)
        {
            long have = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
            if (have < goldCost)
            {
                msg = "金币不足：还需 " + (goldCost - have) + "（持有 " + have + "/" + goldCost + "）";
                return false;
            }
        }
        if (diaCost > 0)
        {
            long have = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);
            if (have < diaCost)
            {
                msg = "钻石不足：还需 " + (diaCost - have) + "（持有 " + have + "/" + diaCost + "）";
                return false;
            }
        }
        msg = null;
        return true;
    }

    /// <summary>
    /// 执行升星：查表校验 → ①扣职业徽记 → ②扣背包碎片 → ③扣金币/钻石 → ④outStar+1 → 存档。
    /// 【2026-10-23 主人拍板】四步同一事务：任一步扣不动就整笔回滚，星级不动（旧实现由 MercAscendUI
    /// 在「已升星已存档」之后才扣钱，扣不动只剩 LogError，留下「星升了、钱没扣」的脏数据）。
    /// 成功返回 true；失败原因见 msg（材料不足要能说出缺什么）。
    /// </summary>
    public static bool TryStarUp(string hireIdOrAssetId, out string msg)
    {
        msg = "";
        if (!CanStarUp(hireIdOrAssetId, out msg)) return false;

        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        var data = SaveSystem.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def))
        {
            msg = "存档未就绪";
            return false;
        }

        int cur = data.GetMercOutStar(hireId);
        int target = cur + 1;
        int need = MercStarDefs.StarUpCost(def.Rarity, target);
        int badgeNeed = MercStarDefs.BadgeCost(def.Rarity, target);

        // ① 先扣职业徽记（2026-10-23 拍板扣费顺序：徽记 → 碎片 → outStar+1）。
        //    此时还没扣任何别的东西，扣失败直接返回、无需回滚。
        string jobKey = MercGrowSprites.JobKey(def.JobName);
        if (string.IsNullOrEmpty(jobKey))
        {
            Debug.LogError($"[MercStar] {def.Name}({hireId}) 职业名「{def.JobName}」识别不出徽记职业 key —— 请补 MercGrowSprites.JobKey 映射，未升星");
            msg = "徽记扣除失败，请重试";
            return false;
        }
        string badgeId = MercGrowInventory.BadgeId(jobKey, MercStarDefs.BadgeTierOf(def.Rarity));
        if (badgeNeed > 0 && !MercGrowInventory.TrySpend(badgeId, badgeNeed))
        {
            // fail closed：预检过了还扣不动，说明仓库状态与预检不一致，报出来人工看
            Debug.LogError($"[MercStar] {hireId} 升星扣徽记失败（预检通过但扣除失败，need={badgeNeed}）——数据异常，未升星");
            msg = "徽记扣除失败，请重试";
            return false;
        }

        // ② 再扣碎片（【2026-10-23 主人拍板】碎片存战斗外背包 townBagItems，与发放口同仓）；
        //    扣失败要把已扣的徽记退回去（内存态回滚，随后下一次存档自然落盘）
        if (!data.TownBagTryConsume(MercStarDefs.FragmentItemId(hireId), need))
        {
            if (badgeNeed > 0) MercGrowInventory.Add(badgeId, badgeNeed);
            // fail closed：预检过了还扣不动，说明背包状态与预检不一致，报出来人工看
            Debug.LogError($"[MercStar] {hireId} 升星扣碎片失败（预检通过但城镇背包扣除失败，need={need}），已退还徽记 ×{badgeNeed} —— 数据异常，未升星");
            msg = "碎片扣除失败，请重试";
            return false;
        }

        // ③ 扣资金（金币 → 钻石）。【2026-10-23 主人拍板】挪进 TryStarUp 与徽记/碎片同事务：
        //    原先由 MercAscendUI 在「已升星已存档」之后才扣，扣不动就只剩一句 LogError，
        //    留下「星升了、钱没扣」的脏数据；现在扣不动就整笔回滚，星级不会变。
        long goldCost = MercStarDefs.GoldCost(def.Rarity, target);
        long diaCost = MercStarDefs.DiamondCost(def.Rarity, target);
        bool spentGold = false;
        if (goldCost > 0)
        {
            if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, goldCost, true, false))
            {
                RollbackMaterials(data, hireId, need, badgeId, badgeNeed);
                Debug.LogError($"[MercStar] {hireId} 升星扣金币失败（预检通过但扣除失败，cost={goldCost}），已退还徽记/碎片 —— 数据异常，未升星");
                msg = "金币扣除失败，请重试";
                return false;
            }
            spentGold = true;
        }
        if (diaCost > 0)
        {
            if (!ResourceWallet.TrySpendDiamond(diaCost, "佣兵升星", true, false))
            {
                if (spentGold) ResourceWallet.Add(ResourceWallet.ResourceType.Gold, goldCost, true, false, false);
                RollbackMaterials(data, hireId, need, badgeId, badgeNeed);
                Debug.LogError($"[MercStar] {hireId} 升星扣钻石失败（预检通过但扣除失败，cost={diaCost}），已退还金币/徽记/碎片 —— 数据异常，未升星");
                msg = "钻石扣除失败，请重试";
                return false;
            }
        }

        // ④ outStar+1 → 存档（走到这里说明徽记/碎片/资金全部扣成功）
        data.SetMercOutStar(hireId, target);
        SaveSystem.Instance.Save();
        string name = def.Name ?? hireId;
        Debug.Log($"[MercStar] {name}({hireId}) 升星：{cur} → {target} 星，消耗徽记 ×{badgeNeed}、本命碎片 ×{need}、金币 {goldCost}、钻石 {diaCost}");
        data.CountMercStarUp();   // 【2026-10-29】周任务「百战精锐」计数（升星成功才计，失败/回滚不计）
        msg = $"{name} 升星成功（{target} 星）";
        return true;
    }

    /// <summary>升星事务回滚：把已扣的碎片与徽记退回（资金由调用方各自退回）。</summary>
    static void RollbackMaterials(SaveData data, string hireId, int fragNeed, string badgeId, int badgeNeed)
    {
        if (data == null) return;
        if (fragNeed > 0) data.TownBagAdd(MercStarDefs.FragmentItemId(hireId), fragNeed);
        if (badgeNeed > 0) MercGrowInventory.Add(badgeId, badgeNeed);
    }

    /// <summary>
    /// 满星降权判定（T9）：背包碎片 ≥ 满星总消耗（普通90/稀有70/传奇10）→ 招募掷点降权。
    /// 只看碎片存量（主人拍板口径），不看当前星级；非花名册恒 false。
    /// </summary>
    public static bool IsFullStarFragReady(string hireIdOrAssetId)
    {
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def)) return false;
        return GetBagFragCount(hireId) >= MercStarDefs.FullStarFragTotal(def.Rarity);
    }
}
