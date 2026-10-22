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
///   GetBagFragCount(hireIdOrAssetId)    背包里该佣兵本命碎片数
///   GetNextStarCost(hireIdOrAssetId)    下一星消耗（0 = 已满星/查不到）
///   CanStarUp(hireIdOrAssetId, out msg) 能否升星（msg 说明缺什么，fail closed）
///   TryStarUp(hireIdOrAssetId)          执行升星（扣背包碎片 → outStar+1 → 存档）
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
        string hireId = ResolveHireId(hireIdOrAssetId);
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
        string hireId = ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return 1;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return 1;
        if (!data.IsMercUnlocked(hireId)) return 1;   // 未拥有 = 0 星 = ★1 档
        int outStar = data.GetMercOutStar(hireId);
        return outStar < 1 ? 1 : outStar;
    }

    /// <summary>背包里该佣兵本命碎片数（碎片现口径只在背包，不经 mercGrowItems）。</summary>
    public static int GetBagFragCount(string hireIdOrAssetId)
    {
        string hireId = ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return 0;
        string itemId = MercStarDefs.FragmentItemId(hireId);
        var bag = GridBackpackSystem.Instance;
        if (bag == null || string.IsNullOrEmpty(itemId)) return 0;
        return bag.CountItemStack(itemId);
    }

    /// <summary>下一星碎片消耗；已满星（含超界）或非花名册返回 0。</summary>
    public static int GetNextStarCost(string hireIdOrAssetId)
    {
        string hireId = ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return 0;
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def)) return 0;
        int cur = OutStarOf(hireId);
        if (cur < 0 || cur >= MercStarDefs.MaxOutStar) return 0;
        return MercStarDefs.StarUpCost(def.Rarity, cur + 1);
    }

    /// <summary>能否升星。不能时 msg 说明原因（材料不足要说清缺什么——fail closed）。</summary>
    public static bool CanStarUp(string hireIdOrAssetId, out string msg)
    {
        msg = "";
        string hireId = ResolveHireId(hireIdOrAssetId);
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
        return true;
    }

    /// <summary>
    /// 执行升星：查表校验 → 从背包扣对应本命碎片 → outStar+1 → 存档。
    /// 成功返回 true；失败原因见 msg（材料不足要能说出缺什么）。
    /// </summary>
    public static bool TryStarUp(string hireIdOrAssetId, out string msg)
    {
        msg = "";
        if (!CanStarUp(hireIdOrAssetId, out msg)) return false;

        string hireId = ResolveHireId(hireIdOrAssetId);
        var data = SaveSystem.Instance?.Data;
        if (data == null || string.IsNullOrEmpty(hireId)) { msg = "存档未就绪"; return false; }

        int cur = data.GetMercOutStar(hireId);
        int need = MercStarDefs.StarUpCost(MercRosterDefs.TryGetByHireId(hireId, out var def) ? def.Rarity : MercRosterDefs.MercRarity.Common, cur + 1);
        var bag = GridBackpackSystem.Instance;
        if (bag == null || !bag.ConsumeItemStack(MercStarDefs.FragmentItemId(hireId), need))
        {
            // fail closed：预检过了还扣不动，说明背包状态与预检不一致，报出来人工看
            Debug.LogError($"[MercStar] {hireId} 升星扣碎片失败（预检通过但背包扣除失败，need={need}）——数据异常，未升星");
            msg = "碎片扣除失败，请重试";
            return false;
        }

        data.SetMercOutStar(hireId, cur + 1);
        SaveSystem.Instance.Save();
        string name = def.Name ?? hireId;
        Debug.Log($"[MercStar] {name}({hireId}) 升星：{cur} → {cur + 1} 星，消耗本命碎片 ×{need}");
        msg = $"{name} 升星成功（{cur + 1} 星）";
        return true;
    }

    /// <summary>
    /// 满星降权判定（T9）：背包碎片 ≥ 满星总消耗（普通90/稀有70/传奇10）→ 招募掷点降权。
    /// 只看碎片存量（主人拍板口径），不看当前星级；非花名册恒 false。
    /// </summary>
    public static bool IsFullStarFragReady(string hireIdOrAssetId)
    {
        string hireId = ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId) || !MercRosterDefs.TryGetByHireId(hireId, out var def)) return false;
        return GetBagFragCount(hireId) >= MercStarDefs.FullStarFragTotal(def.Rarity);
    }

    static string ResolveHireId(string hireIdOrAssetId)
    {
        if (string.IsNullOrEmpty(hireIdOrAssetId)) return null;
        if (MercRosterDefs.TryGetByHireId(hireIdOrAssetId, out var byHire))
            return byHire.HireId;
        if (MercRosterDefs.TryGetByAssetId(hireIdOrAssetId, out var byAsset))
            return byAsset.HireId;
        return null;
    }
}
