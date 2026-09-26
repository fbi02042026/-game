using UnityEngine;

/// <summary>
/// SK009 钢铁意志：生命低于阈值时触发一次，之后一段时间内受击减伤。
/// 私有状态 = _ironWillUsed / _ironWillTimer。逻辑原样照抄旧 MercPassiveRunner 的
/// OnHpChanged(SK009 分支) / ModifyIncomingDamage(SK009 分支) / TickIronWill。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——触发血量阈值(p1)/减伤比例(p2)/持续时间(Duration列)
/// 从 merc_skills 表读；表缺失回退默认常量，行为不变。
/// </summary>
public class SK009_IronWill : MercPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultHpThreshold = 0.3f; // 触发血量阈值
    const float DefaultReduce = 0.4f;      // 减伤比例（damage *= 1 - 此值）
    const float DefaultDuration = 5f;      // 减伤持续(秒)，复用表 Duration 列

    float _hpThreshold = DefaultHpThreshold;
    float _reduce = DefaultReduce;
    float _duration = DefaultDuration;

    bool _ironWillUsed;
    float _ironWillTimer;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        if (row.Param1 > 0f) _hpThreshold = row.Param1;
        if (row.Param2 > 0f) _reduce = row.Param2;
        if (row.Duration > 0f) _duration = row.Duration;
    }

    public override void ModifyIncomingDamage(ref float damage)
    {
        if (_ironWillTimer > 0f)
            damage *= (1f - _reduce);
    }

    public override void OnHpChanged()
    {
        // 2026-09-26 主人拍板：保留「每场最多触发 1 次」语义（_ironWillUsed 不重置）
        if (_ironWillUsed || Merc == null || Merc.attr == null) return;
        float ratio = Merc.currentHp / Mathf.Max(1f, Merc.attr.GetAttr(AttrType.MaxHp));
        if (ratio < _hpThreshold)
        {
            _ironWillUsed = true;
            _ironWillTimer = _duration;
        }
    }

    public override void OnUpdate(float dt)
    {
        if (_ironWillTimer <= 0f) return;
        _ironWillTimer -= dt;
    }
}
