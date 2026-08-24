using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天赋解锁、消耗与存档写入。
/// </summary>
public static class TalentSystem
{
    public enum Branch
    {
        Left,
        /// <summary>右列首行 C1（物理/魔法专精），不占用 R1-R10 顺序。</summary>
        RightExtra,
        Right
    }

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
        return true;
    }

    public static bool CanUnlockChoice(Branch branch, int index1Based, int option1Based, out string reason)
    {
        reason = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "无存档"; return false; }

        var node = GetChoiceNode(branch, index1Based);
        if (node == null) { reason = "无效节点"; return false; }
        if (option1Based < 1 || option1Based > node.options.Length) { reason = "无效选项"; return false; }

        if (data.talents.TryGetValue(node.id, out int picked) && picked > 0)
        {
            reason = "已选择，不可更改";
            return false;
        }

        if (branch == Branch.Right)
        {
            int seq = GetChoiceUnlockedCount(branch, data.talents);
            if (index1Based != seq + 1) { reason = "请按顺序解锁"; return false; }
        }

        int leftN = TalentDefs.LeftUnlockedCount(data.talents);
        if (leftN < node.requireLeftIndex) { reason = $"需左侧 L{node.requireLeftIndex}"; return false; }

        if (data.talentPoints < node.stoneCost) { reason = "天赋石不足"; return false; }
        return true;
    }

    public static bool TryUnlockChoice(Branch branch, int index1Based, int option1Based, out string reason)
    {
        if (!CanUnlockChoice(branch, index1Based, option1Based, out reason)) return false;
        var data = SaveSystem.Instance.Data;
        var node = GetChoiceNode(branch, index1Based);
        data.talentPoints -= node.stoneCost;
        data.talents[node.id] = option1Based;
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        Hero.Instance?.RecalcAttr();
        return true;
    }

    public static bool CanReset(out string reason)
    {
        reason = "当前版本不可重置天赋";
        return false;
    }

    public static int GetLeftGoldCost(TalentDefs.LeftNode node, IDictionary<string, int> talents)
    {
        if (node == null) return 0;
        float discount = 0f;
        if (talents != null &&
            talents.TryGetValue("R9", out int r9) && r9 == 1 &&
            TalentDefs.GetRight(9)?.options.Length >= 1)
            discount = 0.05f;
        return Mathf.Max(1, Mathf.RoundToInt(node.goldCost * (1f - discount)));
    }

    public static TalentDefs.ChoiceNode GetChoiceNode(Branch branch, int index1Based)
    {
        switch (branch)
        {
            case Branch.RightExtra: return index1Based == 1 ? TalentDefs.RightExtra : null;
            case Branch.Right: return TalentDefs.GetRight(index1Based);
            default: return null;
        }
    }

    public static int GetChoiceUnlockedCount(Branch branch, IDictionary<string, int> talents)
    {
        if (branch != Branch.Right) return 0;
        return TalentDefs.RightUnlockedCount(talents);
    }

    public static bool IsChoiceRowUpgradeable(ChoiceRowState row, IDictionary<string, int> talents)
    {
        if (talents == null || row.def == null) return false;
        if (talents.TryGetValue(row.def.id, out int picked) && picked > 0) return false;

        int leftN = TalentDefs.LeftUnlockedCount(talents);
        if (leftN < row.def.requireLeftIndex) return false;

        if (row.branch == TalentSystem.Branch.Right)
        {
            int seq = TalentDefs.RightUnlockedCount(talents);
            if (row.def.index - 1 != seq) return false;
        }

        var data = SaveSystem.Instance?.Data;
        return data != null && data.talentPoints >= row.def.stoneCost;
    }

    public static bool IsLeftUpgradeable(int index0, IDictionary<string, int> talents)
    {
        if (talents == null) return false;
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        if (index0 != unlocked) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        return data.totalGold >= GetLeftGoldCost(TalentDefs.Left[index0], talents);
    }

    public struct ChoiceRowState
    {
        public Branch branch;
        public TalentDefs.ChoiceNode def;
    }
}
