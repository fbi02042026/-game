using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能碎片系统（2026-09-15 新增）。
///
/// 存在的意义：让「史诗 / 传说」技能不是一次性买断，而是**多次小额累积**，
/// 同时给抽卡一个后期出口——抽到已解锁的技能会转成该技能的碎片。
///
/// 碎片是**每技能独立**的（不共用），玩家可以定向追求想要的技能。
/// 传说技能碎片极难凑齐，所以它主要仍靠章节 / 成就白给，碎片只是加速路径。
/// </summary>
public static class SkillFragmentSystem
{
    public static int Get(string skillId)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.skillFragments == null) return 0;
        if (string.IsNullOrEmpty(skillId)) return 0;
        data.skillFragments.TryGetValue(skillId, out int n);
        return n;
    }

    /// <summary>合成该技能还需要的碎片总数。</summary>
    public static int Needed(string skillId) =>
        SkillFragmentDefs.CostToCraft(SkillDraftMeta.Rarity(skillId));

    /// <summary>合成进度文本，如「32/80」。</summary>
    public static string ProgressText(string skillId) => $"{Get(skillId)}/{Needed(skillId)}";

    public static bool IsComplete(string skillId) => Get(skillId) >= Needed(skillId);

    public static void Add(string skillId, int amount, bool save = true)
    {
        if (string.IsNullOrEmpty(skillId) || amount <= 0) return;
        var sys = SaveSystem.Instance;
        if (sys == null || sys.Data == null) return;
        var data = sys.Data;
        data.skillFragments ??= new Dictionary<string, int>();
        data.skillFragments.TryGetValue(skillId, out int cur);
        data.skillFragments[skillId] = cur + amount;
        if (save) sys.Save();
    }

    /// <summary>
    /// 抽卡抽到「已解锁」的技能 → 转成该技能碎片。
    /// 返回本次转化的数量（0 = 规则不允许，比如未登记的技能）。
    /// </summary>
    public static int GrantFromDuplicate(string skillId)
    {
        var def = PlayerSkillDefs.GetById(skillId);
        if (def == null) return 0;
        int n = SkillFragmentDefs.ConvertFromDuplicate(SkillDraftMeta.Rarity(skillId));
        if (n <= 0) return 0;
        Add(skillId, n, save: false);
        return n;
    }

    public static bool CanCraft(string skillId, out string reason)
    {
        reason = "";
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "存档未就绪"; return false; }
        if (string.IsNullOrEmpty(skillId)) { reason = "技能不存在"; return false; }

        var def = PlayerSkillDefs.GetById(skillId);
        if (def == null) { reason = "技能不存在"; return false; }
        if (PlayerSkillDefs.IsUnlocked(def, data)) { reason = "已拥有"; return false; }

        int have = Get(skillId);
        int need = Needed(skillId);
        if (have < need) { reason = $"碎片不足（{have}/{need}）"; return false; }
        return true;
    }

    public static bool TryCraft(string skillId, out string msg)
    {
        if (!CanCraft(skillId, out string reason)) { msg = reason; return false; }

        var sys = SaveSystem.Instance;
        var data = sys.Data;
        int need = Needed(skillId);
        data.skillFragments.TryGetValue(skillId, out int have);
        int left = have - need;
        if (left > 0) data.skillFragments[skillId] = left;
        else data.skillFragments.Remove(skillId);

        PlayerSkillDefs.Unlock(skillId, data);
        sys.Save();

        var def = PlayerSkillDefs.GetById(skillId);
        msg = def != null ? $"已合成「{def.displayName}」" : "已合成技能";
        return true;
    }

    /// <summary>商店买碎片：一次买 count 片，花金币。返回是否成功。</summary>
    public static bool TryBuyFragments(string skillId, int count, out string msg)
    {
        msg = "";
        var def = PlayerSkillDefs.GetById(skillId);
        if (def == null) { msg = "技能不存在"; return false; }

        var rar = SkillDraftMeta.Rarity(skillId);
        if (!SkillFragmentDefs.CanBuyFragment(rar)) { msg = "传说碎片不出售"; return false; }

        var sys = SaveSystem.Instance;
        if (sys == null || sys.Data == null) { msg = "存档未就绪"; return false; }
        var data = sys.Data;
        if (PlayerSkillDefs.IsUnlocked(def, data)) { msg = "已拥有"; return false; }

        long unit = SkillFragmentDefs.GoldPerFragment(rar);
        long cost = unit * count;
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, cost, save: false, notify: false))
        { msg = "金币不足"; return false; }

        Add(skillId, count, save: false);
        sys.Save();
        msg = $"获得 {SkillFragmentDefs.FragmentName(skillId)} ×{count}";
        return true;
    }
}
