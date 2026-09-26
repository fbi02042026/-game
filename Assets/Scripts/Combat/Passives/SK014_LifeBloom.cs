using UnityEngine;

/// <summary>
/// SK014 生命绽放：施放治疗时，施法者（本佣兵）额外获得一次 HoT 爆发。无长期私有状态（瞬时结算）。
/// 逻辑原样照抄旧 MercPassiveRunner 的 OnOwnerHealed(SK014 分支)。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——每秒系数(p1)/结算系数(p3)/持续秒数(Duration列)从 merc_skills 表读；
/// 表缺失回退默认常量。公式保持原「攻击 × 每秒系数 × 持续秒数 × 结算系数」结构，行为不变。
/// </summary>
public class SK014_LifeBloom : MercPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultPerSecCoef = 0.3f; // 每秒攻击系数
    const float DefaultFinalMul = 0.33f;  // 最终结算系数
    const float DefaultDuration = 3f;     // 持续秒数，复用表 Duration 列

    float _perSecCoef = DefaultPerSecCoef;
    float _finalMul = DefaultFinalMul;
    float _duration = DefaultDuration;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        if (row.Param1 > 0f) _perSecCoef = row.Param1;
        if (row.Param3 > 0f) _finalMul = row.Param3;
        if (row.Duration > 0f) _duration = row.Duration;
    }

    public override void OnOwnerHealed(float amount)
    {
        // 2026-09-26 主人拍板：保留原始 HoT 公式，未改数值（仅把常数改为读表字段）
        if (amount <= 0f) return;
        if (Merc != null && Merc.attr != null)
        {
            float hot = Merc.attr.GetAttr(AttrType.Attack) * _perSecCoef * _duration;
            Merc.currentHp = Mathf.Min(Merc.attr.GetAttr(AttrType.MaxHp), Merc.currentHp + hot * _finalMul);
        }
    }
}
