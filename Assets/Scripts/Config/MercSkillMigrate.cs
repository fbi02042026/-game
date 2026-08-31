using UnityEngine;

/// <summary>
/// 旧存档 ally_* 技能迁移到 SK 系列；优先按 hireId 查映射表。
/// </summary>
public static class MercSkillMigrate
{
    public static void AlignMercenary(MercenaryData m)
    {
        if (m == null || string.IsNullOrEmpty(m.mercId)) return;

        MigrateLaoDunAssetId(m);

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

        ApplySkillFields(m, active, passive);
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
        bool hasSkActive = MercSkillTable.IsMercSkillId(m.skillId);
        bool hasSkPassive = MercSkillTable.IsMercSkillId(m.passiveSkillId);

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

    public static bool IsMercSkillAutoCast()
    {
        var data = SaveSystem.Instance?.Data;
        return data != null && data.mercSkillCastMode == 1;
    }

    public static void SetMercSkillAutoCast(bool auto)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.mercSkillCastMode = auto ? 1 : 0;
        SaveSystem.Instance?.Save();
    }
}
