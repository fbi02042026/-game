using UnityEngine;

/// <summary>
/// SK011 自愈：战斗中按间隔恢复自身最大生命的比例。私有状态 = _regenTimer。
/// 逻辑原样照抄旧 MercPassiveRunner 的 TickAlways(SK011 分支)。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——回血比例(p1)/结算间隔(p2)从 merc_skills 表读；
/// 表缺失回退默认常量，行为不变。
/// </summary>
public class SK011_Regen : MercPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultHealPct = 0.01f; // 每次恢复 = 最大生命 × 此比例
    const float DefaultInterval = 1f;   // 结算间隔(秒)

    float _healPct = DefaultHealPct;
    float _interval = DefaultInterval;

    float _regenTimer;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        if (row.Param1 > 0f) _healPct = row.Param1;
        if (row.Param2 > 0f) _interval = row.Param2;
    }

    public override void OnUpdate(float dt)
    {
        _regenTimer -= dt;
        if (_regenTimer > 0f) return;
        _regenTimer = _interval;
        if (Merc == null || Merc.attr == null) return;
        float maxHp = Merc.attr.GetAttr(AttrType.MaxHp);
        float heal = maxHp * _healPct;
        Merc.currentHp = Mathf.Min(maxHp, Merc.currentHp + heal);
    }
}
