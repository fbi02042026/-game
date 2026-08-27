using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 锻造/附魔关最小可用逻辑：升星一件、或加一条局内附魔词条。
/// </summary>
public static class CraftStageApply
{
    public static bool TryForgeUpgrade(out string msg)
    {
        msg = null;
        var bag = GridBackpackSystem.Instance;
        if (bag == null)
        {
            msg = "背包未就绪";
            return false;
        }

        // 优先走设计文档强化 +1～+10（有强化石时）
        EquipInstance enhanceTarget = PickEnhanceTarget(bag);
        if (enhanceTarget != null)
        {
            int next = enhanceTarget.enhanceLevel + 1;
            int matsNeed = EquipEnhanceSystem.MatCost(next);
            int goldNeed = EquipEnhanceSystem.GoldCost(next);
            var data = SaveSystem.Instance?.Data;
            bool canAfford = data != null
                && ResourceWallet.Get(data, ResourceWallet.ResourceType.DecomposeMat) >= matsNeed
                && (goldNeed <= 0 || ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold) >= goldNeed);
            if (canAfford)
                return EquipEnhanceSystem.TryEnhance(enhanceTarget, out msg);
        }

        EquipInstance best = null;
        var all = bag.GetAllItemsForLegacy();
        if (all != null)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null) continue;
                int maxStar = (int)e.rarity;
                if (e.star >= maxStar) continue;
                if (best == null || e.rarity > best.rarity || (e.rarity == best.rarity && e.star < best.star))
                    best = e;
            }
        }

        if (best == null)
        {
            if (string.IsNullOrEmpty(msg))
                msg = "没有可强化的装备";
            return false;
        }

        best.star = Mathf.Min((int)best.rarity, best.star + 1);
        if (best.attrBonus == null) best.attrBonus = new List<AttrBonusData>();
        best.attrBonus.Add(new AttrBonusData
        {
            attrType = AttrType.Attack,
            value = 0.03f,
            isPercent = true
        });
        Hero.Instance?.RecalcAttr();
        AdventureLogAchievements.OnEnhanced();
        msg = $"升星成功：{DisplayName(best)} → ★{best.star}";
        return true;
    }

    static EquipInstance PickEnhanceTarget(GridBackpackSystem bag)
    {
        EquipInstance best = null;
        var all = bag.GetAllItemsForLegacy();
        if (all == null) return null;
        for (int i = 0; i < all.Count; i++)
        {
            var e = all[i];
            if (e == null || e.enhanceLevel >= EquipEnhanceSystem.MaxLevel) continue;
            if (best == null
                || e.rarity > best.rarity
                || (e.rarity == best.rarity && e.enhanceLevel < best.enhanceLevel))
                best = e;
        }
        return best;
    }

    public static bool TryEnchantRandom(out string msg)
    {
        msg = null;
        var bag = GridBackpackSystem.Instance;
        if (bag == null)
        {
            msg = "背包未就绪";
            return false;
        }

        EquipInstance target = null;
        var all = bag.GetAllItemsForLegacy();
        if (all != null && all.Count > 0)
            target = all[0];
        if (target == null)
        {
            msg = "没有可附魔的装备";
            return false;
        }

        var roll = RollEnchant();
        if (target.enchants == null) target.enchants = new List<EnchantData>();
        target.enchants.Add(roll);
        if (target.attrBonus == null) target.attrBonus = new List<AttrBonusData>();
        target.attrBonus.Add(new AttrBonusData
        {
            attrType = roll.attrType,
            value = roll.value,
            isPercent = roll.isPercent
        });
        Hero.Instance?.RecalcAttr();
        msg = $"附魔成功：{DisplayName(target)} +{roll.enchantName}";
        return true;
    }

    static EnchantData RollEnchant()
    {
        int r = Random.Range(0, 4);
        switch (r)
        {
            case 0:
                return new EnchantData { enchantName = "锋利", attrType = AttrType.Attack, value = 0.08f, isPercent = true };
            case 1:
                return new EnchantData { enchantName = "坚韧", attrType = AttrType.MaxHp, value = 0.1f, isPercent = true };
            case 2:
                return new EnchantData { enchantName = "疾步", attrType = AttrType.AttackSpeed, value = 0.08f, isPercent = true };
            default:
                return new EnchantData { enchantName = "铁壁", attrType = AttrType.Defense, value = 2f, isPercent = false };
        }
    }

    static string DisplayName(EquipInstance e)
    {
        if (e == null) return "装备";
        if (!string.IsNullOrEmpty(e.equipName)) return e.equipName;
        return e.templateId ?? "装备";
    }
}
