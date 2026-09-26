using UnityEngine;

/// <summary>
/// SK017 魔力涌动：魔法类型伤害提升。无私有状态。
/// 逻辑原样照抄旧 MercPassiveRunner 的 OnDealMagicDamage(SK017 分支)。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——魔法伤害加成(p1)从 merc_skills 表读；
/// 表缺失回退默认常量，行为不变。
/// </summary>
public class SK017_MagicSurge : MercPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultBonus = 0.15f; // 魔法伤害加成（damage *= 1 + 此值）

    float _bonus = DefaultBonus;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        if (row.Param1 > 0f) _bonus = row.Param1;
    }

    public override void OnDealMagicDamage(ref float damage)
    {
        damage *= (1f + _bonus);
    }
}
