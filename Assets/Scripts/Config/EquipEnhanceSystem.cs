using UnityEngine;

/// <summary>
/// 装备强化 +1～+10（对齐核心规则成长率；材料用分解材料=强化石，+6 起加金币）。
/// </summary>
public static class EquipEnhanceSystem
{
    public const int MaxLevel = 10;

    public static float GrowthRate(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendary: return 0.08f;
            case Rarity.Epic: return 0.07f;
            case Rarity.Rare: return 0.06f;
            default: return 0.05f;
        }
    }

    public static float GetMultiplier(EquipInstance inst)
    {
        if (inst == null || inst.enhanceLevel <= 0) return 1f;
        return 1f + inst.enhanceLevel * GrowthRate(inst.rarity);
    }

    public static int MatCost(int nextLevel)
    {
        if (nextLevel <= 5) return 1 + nextLevel;          // 2~6
        return 3 + nextLevel;                               // 9~13
    }

    public static int GoldCost(int nextLevel)
    {
        if (nextLevel <= 5) return 0;
        return 50 * nextLevel;
    }

    /// <summary>+8 起小概率失败（不掉级）。</summary>
    public static float FailChance(int nextLevel)
    {
        if (nextLevel < 8) return 0f;
        if (nextLevel <= 9) return 0.12f;
        return 0.18f; // → +10
    }

    public static bool CanEnhance(EquipInstance inst, out string reason)
    {
        reason = null;
        if (inst == null)
        {
            reason = "无装备";
            return false;
        }
        if (inst.enhanceLevel >= MaxLevel)
        {
            reason = "已达强化上限 +10";
            return false;
        }
        return true;
    }

    public static bool TryEnhance(EquipInstance inst, out string msg)
    {
        msg = null;
        if (!CanEnhance(inst, out string reason))
        {
            msg = reason;
            return false;
        }

        int next = inst.enhanceLevel + 1;
        int mats = MatCost(next);
        int gold = GoldCost(next);

        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            msg = "存档未就绪";
            return false;
        }

        if (ResourceWallet.Get(data, ResourceWallet.ResourceType.DecomposeMat) < mats)
        {
            msg = $"强化石不足（需{mats}）";
            return false;
        }
        if (gold > 0 && ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold) < gold)
        {
            msg = $"金币不足（需{gold}）";
            return false;
        }

        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.DecomposeMat, mats, save: false, notify: false))
        {
            msg = $"强化石不足（需{mats}）";
            return false;
        }
        if (gold > 0 && !ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, gold, save: false, notify: false))
        {
            // 退回材料
            ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, mats, save: false, notify: false);
            msg = $"金币不足（需{gold}）";
            return false;
        }

        float fail = FailChance(next);
        if (fail > 0f && Random.value < fail)
        {
            SaveSystem.Instance?.Save();
            msg = $"{Display(inst)} 强化失败（未掉级），材料已消耗";
            return false;
        }

        inst.enhanceLevel = next;
        Hero.Instance?.RecalcAttr();
        AdventureLogAchievements.OnEnhanced();
        SaveSystem.Instance?.Save();
        msg = $"{Display(inst)} 强化成功 → +{inst.enhanceLevel}";
        return true;
    }

    static string Display(EquipInstance e)
    {
        if (e == null) return "装备";
        if (!string.IsNullOrEmpty(e.equipName)) return e.equipName;
        return e.templateId ?? "装备";
    }
}
