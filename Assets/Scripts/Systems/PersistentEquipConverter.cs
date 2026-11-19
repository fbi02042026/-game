using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备持久化转换原语（T1）。
/// 运行时 EquipInstance ↔ 存档 EquipmentData 双向全字段映射；字段清单照抄
/// EndlessRiftCheckpoint.ToSnapshot / FromSnapshot，漏字段会导致带出后属性缩水。
/// 另含评分随机与仓库装备分解退金，供 T2/T3/T4 直接调用，勿重复实现。
/// </summary>
public static class PersistentEquipConverter
{
    const string LogTag = "[PersistentEquipConverter]";

    /// <summary>EquipInstance → 持久记录（全字段，equipId = eq.templateId，job/qualityScore 写入）；eq==null 返回 null。</summary>
    public static EquipmentData ToPersistent(EquipInstance eq, int job, int qualityScore)
    {
        if (eq == null) return null;
        return new EquipmentData
        {
            equipId = eq.templateId,
            rarity = (int)eq.rarity,
            star = eq.star,
            requireLevel = eq.requireLevel,
            attrBonus = eq.attrBonus ?? new List<AttrBonusData>(),
            // tags / isLegacy 在 EquipInstance 无对应字段，留 EquipmentData 默认值（与 ToSnapshot 字段清单一致）
            job = job,
            qualityScore = qualityScore,
            enhanceLevel = eq.enhanceLevel,
            baseAttrCount = eq.baseAttrCount,
            gridWidth = eq.gridWidth,
            gridHeight = eq.gridHeight,
            slotType = (int)eq.slotType,
            weaponType = (int)eq.weaponType,
            weaponHand = (int)eq.weaponHand,
            weaponKindOverride = eq.weaponKindOverride,
            weaponAttackType = (int)eq.weaponAttackType,
            armorPrefix = (int)eq.armorPrefix,
            equipName = eq.equipName ?? "",
            appearanceId = eq.appearanceId ?? "",
            spumNameOverride = eq.spumNameOverride ?? "",
            grantSkillId = eq.grantSkillId ?? "",
            enchants = eq.enchants ?? new List<EnchantData>(),
            skillPassives = eq.skillPassives ?? new List<AttrBonusData>(),
            globalBonus = eq.globalBonus
        };
    }

    /// <summary>持久记录 → 可装备实例（全字段，挂模板并 ResolveIcon）；d 为空/缺模板/异常 LogError 后返回 null，不静默兜底。</summary>
    public static EquipInstance FromPersistent(EquipmentData d)
    {
        if (d == null)
        {
            Debug.LogError(LogTag + " FromPersistent 入参 d 为空");
            return null;
        }

        EquipTemplate t;
        try
        {
            if (ConfigManager.Instance == null)
            {
                Debug.LogError(LogTag + " ConfigManager 未就绪，无法还原装备：" + d.equipId);
                return null;
            }
            t = ConfigManager.Instance.GetEquipTemplate(d.equipId);
            if (t == null)
            {
                Debug.LogError(LogTag + " 装备模板缺失，无法还原：" + d.equipId);
                return null;
            }
            t.ResolveIcon();
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " 挂模板异常：" + d.equipId + " " + e.Message);
            return null;
        }

        return new EquipInstance
        {
            template = t,
            templateId = d.equipId,
            icon = t.icon,
            rarity = (Rarity)d.rarity,
            star = d.star,
            enhanceLevel = d.enhanceLevel,
            baseAttrCount = d.baseAttrCount,
            requireLevel = d.requireLevel,
            gridWidth = d.gridWidth > 0 ? d.gridWidth : t.gridWidth,
            gridHeight = d.gridHeight > 0 ? d.gridHeight : t.gridHeight,
            slotType = (EquipSlotType)d.slotType,
            weaponType = (WeaponType)d.weaponType,
            weaponHand = (WeaponHandSlot)d.weaponHand,
            weaponKindOverride = d.weaponKindOverride,
            weaponAttackType = (WeaponAttackType)d.weaponAttackType,
            armorPrefix = (ArmorPrefix)d.armorPrefix,
            equipName = string.IsNullOrEmpty(d.equipName) ? t.equipName : d.equipName,
            appearanceId = d.appearanceId ?? "",
            spumNameOverride = d.spumNameOverride ?? "",
            grantSkillId = d.grantSkillId ?? "",
            attrBonus = d.attrBonus ?? new List<AttrBonusData>(),
            enchants = d.enchants ?? new List<EnchantData>(),
            skillPassives = d.skillPassives ?? new List<AttrBonusData>(),
            globalBonus = d.globalBonus
        };
    }

    /// <summary>评分：白10-30 / 蓝31-50 / 传奇51-100；绿、紫按相邻区间就近归并。仅存字段+展示，不参与属性计算。</summary>
    /// 【第九节 R3 / 2026-11-19】difficulty(0普通/1困难/2噩梦)只改变区间内取值位置，区间上下限不变：
    /// 普通全区间随机、困难取后半段、噩梦取最后 1/4；难度越高取值越高，不越出原区间（守住区间不重叠验收）。
    public static int RollQualityScore(Rarity rarity, int difficulty = 0)
    {
        // 原评分区间上下限（左闭右闭）；绿/紫就近归并相邻区，沿用原逻辑
        int baseLo, baseHi;
        switch (rarity)
        {
            case Rarity.Common:
            case Rarity.Uncommon:
                baseLo = 10; baseHi = 30;
                break;
            case Rarity.Rare:
            case Rarity.Epic:
                baseLo = 31; baseHi = 50;
                break;
            case Rarity.Legendary:
                baseLo = 51; baseHi = 100;
                break;
            default:
                Debug.LogError(LogTag + " RollQualityScore 未知稀有度：" + (int)rarity);
                baseLo = 10; baseHi = 30;
                break;
        }

        // difficulty clamp 到 0~2：0=全区间，1=后半段，2=最后 1/4
        int d = difficulty < 0 ? 0 : (difficulty > 2 ? 2 : difficulty);
        int span = baseHi - baseLo + 1;              // 区间内取值个数
        int lo = baseLo;
        if (d == 1) lo = baseLo + span / 2;          // 后半段：白20 / 蓝41 / 传奇76
        else if (d == 2) lo = baseLo + span * 3 / 4; // 最后1/4：白25 / 蓝46 / 传奇88
        return UnityEngine.Random.Range(lo, baseHi + 1); // 左闭右开，上界 +1；上限仍是原区间上限
    }

    /// <summary>从 legacyEquipPool 移除 index 处记录，按 EquipScrapGold 退金币（城镇分解同一出口）、落盘；返回退金数，失败 -1。</summary>
    public static int ScrapPersistent(int index)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        var pool = data != null ? data.legacyEquipPool : null;
        if (pool == null || index < 0 || index >= pool.Count)
        {
            Debug.LogError(LogTag + " 分解失败：存档池为空或索引越界（index=" + index + ", count=" + (pool != null ? pool.Count : 0) + "）");
            return -1;
        }

        EquipmentData d = pool[index];
        if (d == null)
        {
            Debug.LogError(LogTag + " 分解失败：index=" + index + " 处记录为空");
            return -1;
        }

        int gold = GameConfig.EquipScrapGold((Rarity)d.rarity, d.star);
        pool.RemoveAt(index);
        // 与城镇主动分解同一出口（BattleBagSystem.DecomposeItem）：save:false 由本方法末尾统一 Save()
        ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: false, notify: true);
        SaveSystem.Instance.Save();
        return gold;
    }
}
