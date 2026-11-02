using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一件「本局装备」的可序列化快照。
/// 为什么不直接序列化 <see cref="EquipInstance"/>：它带 Sprite icon 与 [NonSerialized] template，
/// JsonUtility 往返会把它们抹成 null，必须按 templateId 重新挂模板。
/// 字段与 <see cref="EquipInstance"/> 一一对应，加字段时两边同步（漏了不报错，只是恢复后少一条属性）。
/// </summary>
[Serializable]
public class EndlessRiftEquipSnapshot
{
    public string templateId;
    public int rarity;
    public int star;
    public int enhanceLevel;
    public int baseAttrCount;
    public int requireLevel;
    public int gridWidth;
    public int gridHeight;
    public int slotType;
    public int weaponType;
    public int weaponHand;
    public int weaponKindOverride;
    public int weaponAttackType;   // WeaponAttackType：武器攻击型（影响武器技能/特效）
    public int armorPrefix;        // ArmorPrefix：护甲前缀（影响属性档）
    public string equipName;
    public string appearanceId;
    public string spumNameOverride;
    public string grantSkillId;
    public List<AttrBonusData> attrBonus = new List<AttrBonusData>();
    public List<EnchantData> enchants = new List<EnchantData>();
    public List<AttrBonusData> skillPassives = new List<AttrBonusData>();
    public AttrBonusData globalBonus;
}

/// <summary>无限裂隙记录点快照的落盘结构。</summary>
[Serializable]
public class EndlessRiftCheckpointData
{
    public int version = 1;
    /// <summary>快照对应的**已通关存档点层**（10/20/30…）。恢复条件：下次起始层 == 本值 + 1。</summary>
    public int stage;
    /// <summary>本局构筑（技能 / 佣兵 / 势能 / 抽数）。局内金币已强制清零，见 <see cref="EndlessRiftCheckpoint.Save"/>。</summary>
    public RunLoadoutData loadout;
    /// <summary>本局已穿戴的装备（撤离不带出，但记录点快照要留）。</summary>
    public List<EndlessRiftEquipSnapshot> equips = new List<EndlessRiftEquipSnapshot>();
    public string savedUtc = "";
}

/// <summary>
/// 无限裂隙「记录点构筑快照」—— 【2026-11-02 主人拍板】解决卡层死锁。
///
/// <para><b>问题</b>：裂隙构筑（RunLoadout）撤离/死亡即作废，下次进本从零开始；
/// 而怪物强度按绝对层数累积（30 层 3.17×、49 层 5.82×）。两者相乘 = 玩家在某一层永久卡死，
/// 攒了二十几抽都打不过的层，重进只给 3 抽，永远过不去。靠"多给几次抽"只是拿点击量换难度，治标。</para>
///
/// <para><b>解法</b>：每 10 层存档点（大 BOSS 层，<see cref="GameConfig.ENDLESS_SAVE_INTERVAL"/>）通关时，
/// 把当前构筑（技能/佣兵/势能 + 已穿戴装备）快照存盘；
/// 下次进本若正好从该记录点出发（起始层 == 快照层 + 1），就把快照顶回 RunLoadout 并恢复装备。
/// 玩家死了只是"回到上一个记录点"，构筑不会归零 —— 死锁消失，难度曲线不用让步。</para>
///
/// <para><b>口径</b>：<br/>
/// · 快照只存**存档点层**（每 10 层，2026-11-02 主人拍板由 5 改 10），不是每层都存 —— 死亡仍然要丢掉当层之后的东西，惩罚还在；<br/>
/// · 局内金币强制清零（<c>runGold = 0</c>），防止"打一层赚一笔 → 死 → 下次带回"刷钱；<br/>
/// · 快照不过期、不按天清 —— 玩家什么时候攒够局外成长回来都能接着打。</para>
/// </summary>
public static class EndlessRiftCheckpoint
{
    const string PrefsKey = "EndlessRiftCheckpoint_v1";

    /// <summary>本次进本是否已经恢复过装备（防止同一次进本重复穿，叠加属性）。</summary>
    static bool _equipsRestoredThisRun;

    /// <summary>
    /// 本次进本的快照是否真的接上了（<see cref="RestoreToPrefs"/> 的匹配结果）。
    /// 装备恢复必须过这道闸 —— 否则「层号不匹配、构筑没接回」的那次进本，
    /// <see cref="RestoreEquipsOnce"/> 仍会照着盘上的旧快照把装备穿上去（构筑与装备脱节）。
    /// </summary>
    static bool _snapshotMatched;

    /// <summary>
    /// 存档点通关时存快照。<paramref name="stage"/> = 刚通关的存档点层（10 的倍数）。
    /// 非存档点层不要调 —— 每层都存等于"死亡不惩罚"。
    /// </summary>
    public static void Save(int stage)
    {
        int s = Mathf.Max(1, stage);
        var live = RunLoadout.Data;
        if (live == null)
        {
            Debug.LogWarning($"[EndlessRiftCheckpoint] 第 {s} 层存快照失败：RunLoadout 为空");
            return;
        }

        try
        {
            // 深拷贝：JsonUtility 往返一次，避免快照跟着后续抽卡被一起改
            var copy = JsonUtility.FromJson<RunLoadoutData>(JsonUtility.ToJson(live));
            if (copy == null) return;
            copy.active = true;
            copy.midBattle = false;
            copy.runGold = 0;   // 局内金币不跨局（防刷钱），进本按正常规则发启动金

            var data = new EndlessRiftCheckpointData
            {
                stage = s,
                loadout = copy,
                equips = CaptureEquips(),
                savedUtc = DateTimeOffset.UtcNow.ToString("o")
            };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Debug.Log($"[EndlessRiftCheckpoint] 第 {s} 层记录点快照已存：skills={copy.skills?.Count ?? 0} " +
                      $"mercs={copy.mercs?.Count ?? 0} drawCount={copy.drawCount} equips={data.equips.Count}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[EndlessRiftCheckpoint] 存快照失败: " + e.Message);
        }
    }

    /// <summary>读盘上的快照（不动任何运行时状态）。没有则返回 null。</summary>
    public static EndlessRiftCheckpointData Peek()
    {
        try
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return null;
            string json = PlayerPrefs.GetString(PrefsKey);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<EndlessRiftCheckpointData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[EndlessRiftCheckpoint] 读快照失败，忽略: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// 进本时把快照顶回 RunLoadout 的可续局档。
    /// <para>必须在 <c>BattleManager</c> 把裂隙 parked 局作废（RunLoadout.Clear）之后、
    /// <c>RunDraftDirector.OnRunStart</c> 之前调用 —— 这样 ResumeOrBegin 会把它当"续局"读回来，
    /// 技能/佣兵/势能自动生效，且不会重发启动金。</para>
    /// </summary>
    /// <returns>true = 本次进本确实接上了记录点构筑。</returns>
    public static bool RestoreToPrefs(int startStage)
    {
        _equipsRestoredThisRun = false;   // 每次进本重置装备恢复闸门
        _snapshotMatched = false;
        var d = Peek();
        if (d == null || d.loadout == null) return false;

        // 只恢复"正好从这个记录点出发"的那一次：起始层 = 记录点 + 1。
        // 玩家若已推进到更深的记录点，快照会被新记录点覆盖；不匹配就当作没有快照。
        int expect = Mathf.Max(1, startStage) - 1;
        if (d.stage != expect)
        {
            Debug.Log($"[EndlessRiftCheckpoint] 快照层 {d.stage} 与本次起始层 {startStage} 不匹配，不恢复");
            return false;
        }

        d.loadout.runGold = 0;
        RunLoadout.WriteSnapshotToPrefs(d.loadout);
        _snapshotMatched = true;   // 只有真的接上了才允许后面恢复装备
        Debug.Log($"[EndlessRiftCheckpoint] 第 {d.stage} 层记录点构筑已接回（skills={d.loadout.skills?.Count ?? 0} " +
                  $"mercs={d.loadout.mercs?.Count ?? 0} equips={d.equips?.Count ?? 0}）");
        return true;
    }

    /// <summary>
    /// 恢复记录点装备（每层进本只做一次）。必须在战斗场景/背包系统就绪之后调。
    /// 快照里没有装备或模板已删时静默跳过单件，不阻断进本。
    /// </summary>
    public static void RestoreEquipsOnce()
    {
        if (_equipsRestoredThisRun) return;
        _equipsRestoredThisRun = true;
        if (!_snapshotMatched) return;   // 本次进本没接上快照（层号不匹配/无快照）→ 不穿旧装备

        var d = Peek();
        if (d == null || d.equips == null || d.equips.Count == 0) return;
        var bag = BattleBagSystem.Instance;
        var cfg = ConfigManager.Instance;
        if (bag == null || cfg == null)
        {
            Debug.LogWarning("[EndlessRiftCheckpoint] 装备恢复跳过：BattleBagSystem/ConfigManager 未就绪");
            return;
        }

        int ok = 0;
        for (int i = 0; i < d.equips.Count; i++)
        {
            var s = d.equips[i];
            if (s == null || string.IsNullOrEmpty(s.templateId)) continue;
            var t = cfg.GetEquipTemplate(s.templateId);
            if (t == null)
            {
                Debug.LogWarning($"[EndlessRiftCheckpoint] 装备模板缺失，跳过：{s.templateId}");
                continue;
            }
            t.ResolveIcon();
            var eq = FromSnapshot(s, t);
            if (bag.TryEquipDirect(eq)) ok++;
            else Debug.LogWarning($"[EndlessRiftCheckpoint] 装备穿不上（槽位冲突？）：{s.templateId}");
        }
        Debug.Log($"[EndlessRiftCheckpoint] 记录点装备已恢复 {ok}/{d.equips.Count} 件");
    }

    /// <summary>
    /// 清掉记录点快照。调用点：裂隙阵亡双选选「从头开始」（BattleManager.ChooseEndlessRestartFromFirst）
    /// —— 玩家要重头打，旧构筑不该跟着；另可作调试/重置用。
    /// </summary>
    public static void Clear()
    {
        try
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            _equipsRestoredThisRun = false;
            _snapshotMatched = false;
            Debug.Log("[EndlessRiftCheckpoint] 记录点快照已清");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[EndlessRiftCheckpoint] 清快照失败: " + e.Message);
        }
    }

    // ============================================================
    // 装备 ↔ 快照
    // ============================================================

    static List<EndlessRiftEquipSnapshot> CaptureEquips()
    {
        var list = new List<EndlessRiftEquipSnapshot>();
        var bag = BattleBagSystem.Instance;
        if (bag == null) return list;
        var equipped = bag.GetEquippedItems();
        if (equipped == null) return list;
        for (int i = 0; i < equipped.Count; i++)
        {
            var eq = equipped[i];
            if (eq == null || string.IsNullOrEmpty(eq.templateId)) continue;
            list.Add(ToSnapshot(eq));
        }
        return list;
    }

    static EndlessRiftEquipSnapshot ToSnapshot(EquipInstance eq)
    {
        return new EndlessRiftEquipSnapshot
        {
            templateId = eq.templateId,
            rarity = (int)eq.rarity,
            star = eq.star,
            enhanceLevel = eq.enhanceLevel,
            baseAttrCount = eq.baseAttrCount,
            requireLevel = eq.requireLevel,
            gridWidth = eq.gridWidth,
            gridHeight = eq.gridHeight,
            slotType = (int)eq.slotType,
            weaponType = (int)eq.weaponType,
            weaponHand = (int)eq.weaponHand,
            weaponKindOverride = eq.weaponKindOverride,
            weaponAttackType = (int)eq.weaponAttackType,
            armorPrefix = (int)eq.armorPrefix,
            equipName = eq.equipName,
            appearanceId = eq.appearanceId,
            spumNameOverride = eq.spumNameOverride,
            grantSkillId = eq.grantSkillId,
            attrBonus = eq.attrBonus ?? new List<AttrBonusData>(),
            enchants = eq.enchants ?? new List<EnchantData>(),
            skillPassives = eq.skillPassives ?? new List<AttrBonusData>(),
            globalBonus = eq.globalBonus
        };
    }

    static EquipInstance FromSnapshot(EndlessRiftEquipSnapshot s, EquipTemplate t)
    {
        return new EquipInstance
        {
            template = t,
            templateId = s.templateId,
            icon = t.icon,
            rarity = (Rarity)s.rarity,
            star = s.star,
            enhanceLevel = s.enhanceLevel,
            baseAttrCount = s.baseAttrCount,
            requireLevel = s.requireLevel,
            gridWidth = s.gridWidth > 0 ? s.gridWidth : t.gridWidth,
            gridHeight = s.gridHeight > 0 ? s.gridHeight : t.gridHeight,
            slotType = (EquipSlotType)s.slotType,
            weaponType = (WeaponType)s.weaponType,
            weaponHand = (WeaponHandSlot)s.weaponHand,
            weaponKindOverride = s.weaponKindOverride,
            weaponAttackType = (WeaponAttackType)s.weaponAttackType,
            armorPrefix = (ArmorPrefix)s.armorPrefix,
            equipName = string.IsNullOrEmpty(s.equipName) ? t.equipName : s.equipName,
            appearanceId = s.appearanceId,
            spumNameOverride = s.spumNameOverride,
            grantSkillId = s.grantSkillId,
            attrBonus = s.attrBonus ?? new List<AttrBonusData>(),
            enchants = s.enchants ?? new List<EnchantData>(),
            skillPassives = s.skillPassives ?? new List<AttrBonusData>(),
            globalBonus = s.globalBonus
        };
    }
}
