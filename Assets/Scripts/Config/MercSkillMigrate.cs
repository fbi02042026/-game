using UnityEngine;

/// <summary>
/// 旧存档 ally_* 技能迁移到 SK 系列；优先按 hireId 查映射表。
/// 2026-09 规范 ID：SK004=治愈之光(主动)，SK011=自愈(被动)，SK012=狂怒(被动)。
/// 旧 ID：SK004=狂怒，SK011=治愈之光，SK012=自愈。
/// </summary>
public static class MercSkillMigrate
{
    /// <summary>存档技能 ID 规范版本（2026-09 三方换号）。</summary>
    public const int CanonVersion = 202609;

    public static void AlignSave(SaveData data)
    {
        if (data == null) return;
        bool threeWay = data.mercSkillCanonVersion < CanonVersion;
        if (data.permanentMercs != null)
        {
            for (int i = 0; i < data.permanentMercs.Count; i++)
                AlignMercenary(data.permanentMercs[i], threeWay);
        }
        if (data.hiredMercs != null)
        {
            for (int i = 0; i < data.hiredMercs.Count; i++)
                AlignMercenary(data.hiredMercs[i], threeWay);
        }
        data.mercSkillCanonVersion = CanonVersion;
    }

    public static void AlignMercenary(MercenaryData m)
    {
        AlignMercenary(m, threeWay: false);
    }

    public static void AlignMercenary(MercenaryData m, bool threeWay)
    {
        if (m == null || string.IsNullOrEmpty(m.mercId)) return;

        MigrateLaoDunAssetId(m);
        if (threeWay)
            RemapThreeWayLegacyIds(m);
        RemapSlotMismatchIds(m);

        string active = null;
        string passive = null;
        if (!string.IsNullOrEmpty(m.hireId) && MercSkillMapping.TryGetByHireId(m.hireId, out var byHire))
        {
            active = byHire.ActiveSkillId;
            passive = byHire.PassiveSkillId;
        }
        else
        {
            MercSkillMapping.GetDefaultSkills(m.mercId, out active, out passive);
            if (string.IsNullOrEmpty(m.hireId))
            {
                string resolved = MercPortraitSprites.ResolveHireId(m.mercId);
                if (!string.IsNullOrEmpty(resolved))
                    m.hireId = resolved;
            }
        }

        // 无 hire 映射时：旧自愈 SK012 仍可能停在被动槽（规范后 SK012=狂怒）
        if (string.IsNullOrEmpty(passive) && m.passiveSkillId == "SK012"
            && MercRosterDefs.TryGetByAssetId(m.mercId, out var roster)
            && roster.PassiveSkillId == "SK011")
            m.passiveSkillId = "SK011";

        ApplySkillFields(m, active, passive);
    }

    /// <summary>
    /// 一次性三方换号（仅 CanonVersion 之前的存档）：
    /// 主动 SK011→SK004，被动 SK004→SK012，自愈 SK012→SK011。
    /// </summary>
    public static void RemapThreeWayLegacyIds(MercenaryData m)
    {
        if (m == null) return;
        string active = m.skillId;
        string passive = m.passiveSkillId;

        if (active == "SK011") m.skillId = "SK004";
        if (passive == "SK004") m.passiveSkillId = "SK012";
        else if (passive == "SK012") m.passiveSkillId = "SK011";
    }

    /// <summary>
    /// 按现行表语义纠正放错槽的旧 ID（可重复执行）：
    /// 主动槽里的 SK011 现为被动自愈 → 治愈之光 SK004；
    /// 被动槽里的 SK004 现为主动治疗 → 狂怒 SK012。
    /// </summary>
    public static void RemapSlotMismatchIds(MercenaryData m)
    {
        if (m == null) return;
        if (m.skillId == "SK011" && MercSkillTable.IsPassive("SK011"))
            m.skillId = "SK004";
        if (m.passiveSkillId == "SK004" && !MercSkillTable.IsPassive("SK004"))
            m.passiveSkillId = "SK012";
        if (m.passiveSkillId == "SK012"
            && !string.IsNullOrEmpty(m.hireId)
            && MercSkillMapping.TryGetByHireId(m.hireId, out var map)
            && map.PassiveSkillId == "SK011")
            m.passiveSkillId = "SK011";
    }

    /// <summary>101/102 对调后：老盾（H001）存档统一为 dunbing101。</summary>
    static void MigrateLaoDunAssetId(MercenaryData m)
    {
        if (m.mercId != "dunbing102") return;
        if (m.hireId == "H001")
        {
            m.mercId = "dunbing101";
            return;
        }
        if (!string.IsNullOrEmpty(m.hireId)) return;
        if (m.nickname == "老盾"
            || (!string.IsNullOrEmpty(m.displayName) && m.displayName.Contains("老盾"))
            || (!string.IsNullOrEmpty(m.uid) && m.uid.StartsWith("tutorial_")))
        {
            m.mercId = "dunbing101";
            m.hireId = "H001";
        }
    }

    static void ApplySkillFields(MercenaryData m, string active, string passive)
    {
        bool legacyActive = IsLegacyAllySkill(m.skillId);
        bool hasSkActive = MercSkillTable.IsMercSkillId(m.skillId) && !MercSkillTable.IsPassive(m.skillId);
        bool hasSkPassive = MercSkillTable.IsPassive(m.passiveSkillId);

        if (legacyActive || string.IsNullOrEmpty(m.skillId) || (!hasSkActive && !string.IsNullOrEmpty(active)))
            m.skillId = active;

        if (string.IsNullOrEmpty(m.passiveSkillId) || IsLegacyAllySkill(m.passiveSkillId) || (!hasSkPassive && !string.IsNullOrEmpty(passive)))
            m.passiveSkillId = passive;

        if (string.IsNullOrEmpty(active) && IsLegacyAllySkill(m.skillId))
            m.skillId = null;
    }

    public static bool IsLegacyAllySkill(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return id.StartsWith("ally_");
    }

    const string MercAutoPrefsKey = "settings.merc_skill_auto";

    /// <summary>佣兵主动技始终自动释放（设置项已移除）。</summary>
    public static bool IsMercSkillAutoCast() => true;

    public static void SetMercSkillAutoCast(bool auto)
    {
        // 保留 API 兼容；强制自动，忽略手动请求
        PlayerPrefs.SetInt(MercAutoPrefsKey, 1);
        PlayerPrefs.Save();
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.mercSkillCastMode != 1)
        {
            data.mercSkillCastMode = 1;
            SaveSystem.Instance?.Save();
        }
    }

    /// <summary>读档后强制自动释放。</summary>
    public static void SyncAutoCastPrefsFromSave()
    {
        PlayerPrefs.SetInt(MercAutoPrefsKey, 1);
        PlayerPrefs.Save();
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        AlignSave(data);
        if (data.mercSkillCastMode != 1)
        {
            data.mercSkillCastMode = 1;
            SaveSystem.Instance?.Save();
        }
    }

    /// <summary>存档同步：强制自动。</summary>
    public static void ApplyAutoCastPrefsToSave()
    {
        SyncAutoCastPrefsFromSave();
    }
}
