using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 日志碎片：关卡掉落 + 合成解锁隐藏内容（计划三期 §7.4）。
/// </summary>
public static class AdventureLogFragments
{
    public struct Recipe
    {
        public string FragmentId;
        public int Cost;
        public string UnlockSideId;
        public string UnlockWorldId;
        public string UnlockMainId;
        public string Label;
        public string RecipeKey => FragmentId + "@" + Cost;
    }

    public static readonly Recipe[] Recipes =
    {
        new Recipe { FragmentId = "frag_forest", Cost = 3, UnlockSideId = "S014", Label = "森林残页 → 隐藏支线「小美的信物」" },
        new Recipe { FragmentId = "frag_guild", Cost = 5, UnlockWorldId = "W008", Label = "公会密函 → 世界「空洞之喉」" },
        new Recipe { FragmentId = "frag_rift", Cost = 4, UnlockSideId = "S016", Label = "裂隙碎片 → 支线「最初的裂隙」" },
    };

    public static string DisplayName(string fragmentId)
    {
        switch (fragmentId)
        {
            case "frag_forest": return "森林残页";
            case "frag_guild": return "公会密函";
            case "frag_rift": return "裂隙碎片";
            default: return fragmentId ?? "碎片";
        }
    }

    public static int GetCount(string fragmentId)
    {
        var dict = SaveSystem.Instance?.Data?.logFragments;
        if (dict == null || string.IsNullOrEmpty(fragmentId)) return 0;
        return dict.TryGetValue(fragmentId, out int n) ? n : 0;
    }

    public static void Add(string fragmentId, int amount = 1, bool save = true)
    {
        if (string.IsNullOrEmpty(fragmentId) || amount <= 0) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.logFragments ??= new Dictionary<string, int>();
        data.logFragments.TryGetValue(fragmentId, out int cur);
        data.logFragments[fragmentId] = cur + amount;
        if (save) SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
    }

    /// <summary>关卡胜利时小概率掉落。</summary>
    public static void TryDropOnStageClear(int chapter, bool isBossStage)
    {
        float chance = isBossStage ? 0.35f : 0.12f;
        if (Random.value > chance) return;
        string id = chapter <= 2 ? "frag_forest" : (chapter <= 4 ? "frag_guild" : "frag_rift");
        Add(id, 1);
        UIManager.Instance?.ShowToast("获得日志碎片「" + DisplayName(id) + "」×1");
    }

    public static bool IsCrafted(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length) return false;
        var set = SaveSystem.Instance?.Data?.craftedFragmentRecipes;
        return set != null && set.Contains(Recipes[recipeIndex].RecipeKey);
    }

    public static bool CanCraft(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= Recipes.Length) return false;
        if (IsCrafted(recipeIndex)) return false;
        var r = Recipes[recipeIndex];
        return GetCount(r.FragmentId) >= r.Cost;
    }

    public static bool HasAnyCraftable()
    {
        for (int i = 0; i < Recipes.Length; i++)
        {
            if (CanCraft(i)) return true;
        }
        return false;
    }

    public static bool TryCraft(int recipeIndex, out string msg)
    {
        msg = null;
        if (IsCrafted(recipeIndex))
        {
            msg = "已合成过该配方";
            return false;
        }
        if (!CanCraft(recipeIndex))
        {
            msg = "碎片不足";
            return false;
        }
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            msg = "存档未就绪";
            return false;
        }
        var r = Recipes[recipeIndex];
        data.logFragments[r.FragmentId] = GetCount(r.FragmentId) - r.Cost;
        data.craftedFragmentRecipes ??= new HashSet<string>();
        data.craftedFragmentRecipes.Add(r.RecipeKey);

        if (!string.IsNullOrEmpty(r.UnlockSideId))
            AdventureCodex.CompleteSide(r.UnlockSideId);
        if (!string.IsNullOrEmpty(r.UnlockWorldId))
            AdventureCodex.UnlockWorld(r.UnlockWorldId);
        if (!string.IsNullOrEmpty(r.UnlockMainId))
            AdventureCodex.CompleteMain(r.UnlockMainId);

        // 合成奖励：支线完成本身已发里程；额外小额金币
        ResourceWallet.Add(ResourceWallet.ResourceType.Gold, 200, save: false, notify: false);
        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        msg = "合成成功：" + r.Label;
        return true;
    }

    public static string FormatInventory()
    {
        return $"{DisplayName("frag_forest")}×{GetCount("frag_forest")}  "
             + $"{DisplayName("frag_guild")}×{GetCount("frag_guild")}  "
             + $"{DisplayName("frag_rift")}×{GetCount("frag_rift")}";
    }

    public static string FormatRecipeLine(int index)
    {
        if (index < 0 || index >= Recipes.Length) return "";
        var r = Recipes[index];
        int have = GetCount(r.FragmentId);
        if (IsCrafted(index)) return $"{r.Label}（已合成）";
        return $"{r.Label}  [{DisplayName(r.FragmentId)} {have}/{r.Cost}]";
    }
}
