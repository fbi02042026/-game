using UnityEngine;

/// <summary>
/// SK006 格挡：受击时有概率减少所受伤害。无私有状态（纯概率判定）。
/// 逻辑原样照抄旧 MercPassiveRunner 的 ModifyIncomingDamage 内 SK006 分支。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——触发概率(p1)/减伤比例(p2)从 merc_skills 表读；
/// 表缺失回退默认常量，行为不变。
/// </summary>
public class SK006_Block : MercPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultProb = 0.2f;   // 触发概率
    const float DefaultReduce = 0.3f; // 减伤比例（damage *= 1 - 此值）

    float _prob = DefaultProb;
    float _reduce = DefaultReduce;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        if (row.Param1 > 0f) _prob = row.Param1;
        if (row.Param2 > 0f) _reduce = row.Param2;
    }

    public override void ModifyIncomingDamage(ref float damage)
    {
        if (Random.value < _prob)
            damage *= (1f - _reduce);
    }
}
