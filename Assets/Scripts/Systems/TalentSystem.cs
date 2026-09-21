using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天赋解锁、消耗与存档写入。
/// 左列（金币，线性必点）；右列（天赋石，可升级、分批开放、互斥、可洗点）。
/// </summary>
public static class TalentSystem
{
    public enum Branch
    {
        Left,
        Right
    }

    // ===== 左列（金币，保持不变）=====
    public static bool CanUnlockLeft(int index1Based, out string reason)
    {
        reason = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }
        if (index1Based < 1 || index1Based > TalentDefs.Left.Length) { reason = "无效节点"; return false; }

        int unlocked = TalentDefs.LeftUnlockedCount(data.talents);
        if (index1Based != unlocked + 1) { reason = "请按顺序解锁"; return false; }

        var node = TalentDefs.Left[index1Based - 1];
        int cost = GetLeftGoldCost(node, data.talents);
        if (data.totalGold < cost) { reason = "金币不足"; return false; }
        return true;
    }

    public static bool TryUnlockLeft(int index1Based, out string reason)
    {
        if (!CanUnlockLeft(index1Based, out reason)) return false;
        var data = SaveSystem.Instance.Data;
        var node = TalentDefs.Left[index1Based - 1];
        int cost = GetLeftGoldCost(node, data.talents);
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, cost, save: false, notify: true))
        {
            reason = "金币不足";
            return false;
        }
        data.talents[node.id] = 1;
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        AnnounceTalentGain(node != null ? node.name : null, node != null ? node.effect : null);
        return true;
    }

    public static int GetLeftGoldCost(TalentDefs.LeftNode node, IDictionary<string, int> talents)
    {
        if (node == null) return 0;
        // 右列已无「左列金币折扣」节点，故不再有折扣分支。
        return Mathf.Max(1, Mathf.RoundToInt(node.goldCost));
    }

    /// <summary>左列天赋购买成功后的「恭喜获得」庆祝提示（2026-09-21 用户要求，样式对齐装备的 EquipDropPopupUI.ShowEquipGain）。</summary>
    static void AnnounceTalentGain(string nodeName, TalentDefs.Effect fx)
    {
        if (fx == null || string.IsNullOrEmpty(fx.display)) return;
        string text = string.IsNullOrEmpty(nodeName)
            ? $"恭喜获得【{fx.display}】，实力大增！"
            : $"恭喜获得【{nodeName}】，{fx.display}，实力大增！";
        UIManager.Instance?.ShowToast(text);
    }

    static void AnnounceRightGain(TalentDefs.TalentRightNode node, int level, int chosenJob)
    {
        if (node == null) return;
        string text = node.name + " Lv" + level;
        if (level > 0)
        {
            string eff = node.EffectSummary(level, chosenJob);
            if (!string.IsNullOrEmpty(eff) && eff != node.name) text += " · " + eff;
        }
        UIManager.Instance?.ShowToast(text);
    }

    /// <summary>
    /// 把「背包可用行数」的真值写回存档（唯一权威写入点，2026-09-21 主人要求）。
    /// R_BAG（背包扩容）解锁 → 4 行；洗点把 R_BAG 清掉 → 回落默认 3 行。
    /// 这样 GameConfig.GetUnlockedBackpackRows 里的天赋字典兜底只承担旧存档兼容，
    /// 不再是「字段没落盘也能用」的主路径。
    /// </summary>
    static void SyncBackpackRows(SaveData data)
    {
        if (data == null) return;
        data.backpackRows = Mathf.Clamp(
            GameConfig.BACKPACK_DEFAULT_ROWS + TalentDefs.CountBagRowUnlocks(data.talents),
            1, GameConfig.BACKPACK_HEIGHT_MAX);
    }

    // ===== 右列（天赋石，重制后模型）=====

    /// <summary>能否解锁某右列节点（level 0 → 1）。互斥/分批/双修前置都在这里判定。</summary>
    public static bool CanUnlockRightNode(string id, out string reason)
    {
        reason = null;
        var node = TalentDefs.GetRightNodeById(id);
        if (node == null) { reason = "无效节点"; return false; }
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }

        int level = TalentDefs.GetRightNodeLevel(data.talents, id);
        if (level > 0) { reason = "已解锁"; return false; }

        // 任务4：互斥组已有其它节点则点不了
        if (TalentDefs.IsRightNodeMutexLocked(node, data.talents)) { reason = "互斥：已选其它流派"; return false; }

        // R_DUAL 前置：主流派（R_F1/R_F2）需满 5 级
        if (node.id == "R_DUAL")
        {
            int p1 = TalentDefs.GetRightNodeLevel(data.talents, "R_F1");
            int p2 = TalentDefs.GetRightNodeLevel(data.talents, "R_F2");
            if (p1 < 5 && p2 < 5) { reason = "需主流派满 5 级"; return false; }
        }

        // 任务3：分批开放——未达左列门槛不可解锁
        int leftN = TalentDefs.LeftUnlockedCount(data.talents);
        if (leftN < node.unlockLeftIndex) { reason = $"左列 L{node.unlockLeftIndex} 解锁"; return false; }

        if (node.costByLevel == null || node.costByLevel.Length < 1) { reason = "配置错误"; return false; }
        if (data.talentPoints < node.costByLevel[0]) { reason = "天赋石不足"; return false; }
        return true;
    }

    public static bool TryUnlockRightNode(string id, int chosenJob1Based, out string reason)
    {
        if (!CanUnlockRightNode(id, out reason)) return false;
        var node = TalentDefs.GetRightNodeById(id);
        var data = SaveSystem.Instance.Data;

        if (node.IsJobChoice)
        {
            if (chosenJob1Based < 1 || chosenJob1Based > (node.options != null ? node.options.Length : 0))
            { reason = "请先选择职业"; return false; }
            data.talents[id + "_job"] = chosenJob1Based;
        }

        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.TalentPoint, node.costByLevel[0], save: false, notify: true))
        { reason = "天赋石不足"; return false; }

        data.talents[id] = 1;
        SyncBackpackRows(data);   // R_BAG 解锁 → SaveData.backpackRows = 4
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        AnnounceRightGain(node, 1, TalentDefs.GetRightNodeChosenJob(data.talents, id));
        return true;
    }

    /// <summary>能否把某右列节点从当前等级 +1。受等级上限（任务3）与天赋石限制。</summary>
    public static bool CanUpgradeRightNode(string id, out string reason)
    {
        reason = null;
        var node = TalentDefs.GetRightNodeById(id);
        if (node == null) { reason = "无效节点"; return false; }
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }

        int level = TalentDefs.GetRightNodeLevel(data.talents, id);
        if (level <= 0) { reason = "请先解锁"; return false; }
        if (level >= node.maxLevel) { reason = "已满级"; return false; }

        // 任务3：等级上限 = min(maxLevel, 1 + floor((左列已点 - 门槛)/5))
        int leftN = TalentDefs.LeftUnlockedCount(data.talents);
        int cap = TalentDefs.RightNodeLevelCap(node, leftN);
        if (level >= cap) { reason = $"左列等级不足（上限 Lv{cap}）"; return false; }

        if (node.costByLevel == null || level >= node.costByLevel.Length) { reason = "配置错误"; return false; }
        if (data.talentPoints < node.costByLevel[level]) { reason = "天赋石不足"; return false; }
        return true;
    }

    public static bool TryUpgradeRightNode(string id, out string reason)
    {
        if (!CanUpgradeRightNode(id, out reason)) return false;
        var node = TalentDefs.GetRightNodeById(id);
        var data = SaveSystem.Instance.Data;
        int level = TalentDefs.GetRightNodeLevel(data.talents, id);

        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.TalentPoint, node.costByLevel[level], save: false, notify: true))
        { reason = "天赋石不足"; return false; }

        data.talents[id] = level + 1;
        SyncBackpackRows(data);
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        AnnounceRightGain(node, level + 1, TalentDefs.GetRightNodeChosenJob(data.talents, id));
        return true;
    }

    // ===== 任务5：洗点（仅右列，退 80%）=====
    /// <summary>能否洗右列：有任意已投右列节点即可。</summary>
    public static bool CanRespecRight(out string reason)
    {
        reason = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            int lv = TalentDefs.GetRightNodeLevel(data.talents, TalentDefs.RightNodes[i].id);
            if (lv > 0) return true;
        }
        reason = "右列暂无可洗天赋";
        return false;
    }

    /// <summary>
    /// 洗右列：清除所有右列节点的等级（含 JobChoice 的已选职业），退还已投入天赋石的 80%（向下取整）。
    /// 左列不可洗。respecCount 由另一子代理负责写入，这里不引用该字段。
    /// </summary>
    public static bool TryRespecRight(out string reason)
    {
        if (!CanRespecRight(out reason)) return false;
        var data = SaveSystem.Instance.Data;

        int totalSpent = 0;
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var node = TalentDefs.RightNodes[i];
            int lv = TalentDefs.GetRightNodeLevel(data.talents, node.id);
            if (lv > 0) totalSpent += TalentDefs.RightNodeTotalSpent(node, lv);
            data.talents.Remove(node.id);
            data.talents.Remove(node.id + "_job");
        }

        int refund = (int)(totalSpent * 0.8f); // 向下取整
        if (refund > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, refund, save: true, notify: true);

        SyncBackpackRows(data);   // 洗点清掉 R_BAG → SaveData.backpackRows 回落 3
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        UIManager.Instance?.ShowToast(refund > 0 ? $"洗点完成，返还天赋石 {refund}" : "洗点完成");
        return true;
    }

    // UI 复用：旧 CanReset / TryReset 改为洗右列入口
    public static bool CanReset(out string reason) => CanRespecRight(out reason);
    public static bool TryReset(out string reason) => TryRespecRight(out reason);

    public static bool IsLeftUpgradeable(int index0, IDictionary<string, int> talents)
    {
        if (talents == null) return false;
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        if (index0 != unlocked) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        return data.totalGold >= GetLeftGoldCost(TalentDefs.Left[index0], talents);
    }
}
