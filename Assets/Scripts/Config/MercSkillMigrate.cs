using UnityEngine;

/// <summary>
/// 旧存档 ally_* 技能迁移到 SK 系列；按 mercId 查映射表。
/// </summary>
public static class MercSkillMigrate
{
    public static void AlignMercenary(MercenaryData m)
    {
        if (m == null || string.IsNullOrEmpty(m.mercId)) return;

        MercSkillMapping.GetDefaultSkills(m.mercId, out string active, out string passive);

        bool legacyActive = IsLegacyAllySkill(m.skillId);
        bool hasSkActive = MercSkillTable.IsMercSkillId(m.skillId);
        bool hasSkPassive = MercSkillTable.IsMercSkillId(m.passiveSkillId);

        if (legacyActive || string.IsNullOrEmpty(m.skillId) || (!hasSkActive && !string.IsNullOrEmpty(active)))
            m.skillId = active;

        if (string.IsNullOrEmpty(m.passiveSkillId) || IsLegacyAllySkill(m.passiveSkillId) || (!hasSkPassive && !string.IsNullOrEmpty(passive)))
            m.passiveSkillId = passive;

        // 普通佣兵无主动：清空误存的 ally_*
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
