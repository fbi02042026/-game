using System;
using UnityEngine;

/// <summary>
/// 持久背包（角色页「随身仓库」）发放入口 —— 唯一、正确的发放点。
/// <para>⚠ 铁律：所有「给玩家发道具 / 装备」都必须走这里，绝不能直接 new 一个 List 塞进 SaveData：
/// - 道具 → SaveData.townBagItems（运行时字典），存盘时由 SaveData 自动 sync 回 townBagItemEntries 落盘；
/// - 装备 → SaveData.legacyEquipPool（直接序列化字段）。
/// 两者都会经 SaveSystem.Save() 落盘，重载后 CharacterUI 的随身仓库能正确显示。</para>
/// <para>战斗局内背包（BattleBagSystem）是另一套、出局清零，与此处<b>完全无关</b>。</para>
/// </summary>
public static class ItemGrantSystem
{
    /// <summary>
    /// 给玩家发道具（入随身仓库 townBagItems）。返回是否成功。
    /// defId 必须是 item_defs.csv 里存在的道具 id；amount &lt;= 0 直接忽略。
    /// </summary>
    public static bool GrantItem(string defId, int amount)
    {
        if (string.IsNullOrEmpty(defId) || amount <= 0) return false;
        var def = ItemDefs.Get(defId);
        if (def == null)
        {
            Debug.LogError("[ItemGrant] 道具不存在，无法发放：" + defId);
            return false;
        }
        try
        {
            var data = SaveSystem.Instance?.Data;
            if (data == null) return false;
            data.TownBagAdd(defId, amount);
            SaveSystem.Instance.Save();
            Debug.Log($"[ItemGrant] 发放道具 {defId} ×{amount} → 随身仓库");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[ItemGrant] 发放道具失败：" + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 给玩家发装备（入仓库 legacyEquipPool）。返回是否成功。
    /// equipId 必须能在 EquipTemplate 表里查到；rarity 0~4，star 默认 0。
    /// </summary>
    public static bool GrantEquip(string equipId, int rarity, int star = 0)
    {
        if (string.IsNullOrEmpty(equipId)) return false;
        // 运行时模板（竞技场冠军限定武器等）在此保证已注册 —— 邮件领取若查不到模板，
        // 附件会被标已领导致武器永久丢失，所以在唯一发放入口先补注册（幂等）。
        ArenaSeasonWeapons.EnsureRegistered();
        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(equipId) : null;
        if (tpl == null)
        {
            Debug.LogError("[ItemGrant] 装备模板不存在，无法发放：" + equipId);
            return false;
        }
        try
        {
            var data = SaveSystem.Instance?.Data;
            if (data == null) return false;
            data.legacyEquipPool ??= new System.Collections.Generic.List<EquipmentData>();
            data.legacyEquipPool.Add(new EquipmentData
            {
                equipId = equipId,
                rarity = Mathf.Clamp(rarity, 0, 4),
                star = star,
                isLegacy = true,
            });
            SaveSystem.Instance.Save();
            Debug.Log($"[ItemGrant] 发放装备 {equipId}（稀有度{rarity}） → 随身仓库");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[ItemGrant] 发放装备失败：" + e.Message);
            return false;
        }
    }
}
