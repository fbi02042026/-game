using UnityEngine;

/// <summary>
/// 传奇佣兵专属 · 全队护盾（SK021）。
/// 2026-09-26 主人决定暂不上线，保留实现待后续启用（不删除 .cs，避免遗留孤儿 .meta）。
///
/// 回退说明（2026-09-26）：
/// - merc_skills.csv 中的 SK021 行已删除；merc_roster.csv 中 H004 维克的主动技已还原为 SK010。
/// - 本文件未接入任何运行时工厂（MercPassiveRunner.CreateModule 无 SK021 case），故仅作保留，
///   不参与战斗、不被创建、不生效。
/// - 若日后要重新启用：在 merc_skills.csv 补回 SK021 行，并在 merc_roster.csv 把目标传奇佣兵
///   的 ActiveSkillId 指回 SK021，无需改本文件。
///
/// 实现说明（按任务 1 模块化规范，单独成文件，不塞回大杂烩）：
/// - 效果复用现有 TeamShield 机制（MercSkillExecutor.Kind.TeamShield + MercSkillExecutor.TeamShieldParams），
///   数值 100% 来自 merc_skills 表（生命上限 × ratio% / 持续时间(s)），代码不写死。
/// - 传奇限定：不在此处硬编码 `if (rarity == Legendary)`；判据走表列「适用佣兵稀有度」
///   （MercSkillTable.MercRarityMin）与佣兵实际稀有度（RarityPalette.ResolveMercRarity）比较，
///   见 MercSkillTable.MercCanUseSkill（配置驱动、集中判据）。
/// - 护盾的 1:1 抵扣 / 蓝盾条（CharacterSlotUI / BattleUI.CharacterBar）全部复用，无需新机制。
/// </summary>
public static class LegendaryTeamShieldSkill
{
    public const string Id = "SK021";

    /// <summary>该技能是否为「仅限传奇佣兵」：读表列「适用佣兵稀有度」，不在此硬编码魔法值。</summary>
    public static bool IsLegendaryOnly()
    {
        return MercSkillTable.RequiredMercRarity(Id) >= MercRosterDefs.MercRarity.Legendary;
    }
}
