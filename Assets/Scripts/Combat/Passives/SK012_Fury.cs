using UnityEngine;

/// <summary>
/// SK012 狂怒：生命低于阈值时攻击力提升，恢复到阈值以上则回退。私有状态 = _lowHpAtkOn。
/// 逻辑原样照抄旧 MercPassiveRunner 的 TickLowHpAtk。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——触发血量阈值(p1)/攻击加成(p2)从 merc_skills 表读；
/// 表缺失回退默认常量，行为不变。
/// </summary>
public class SK012_Fury : MercPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultHpThreshold = 0.5f; // 触发血量阈值
    const float DefaultAtkBonus = 0.25f;    // 攻击力变动比例（±）

    float _hpThreshold = DefaultHpThreshold;
    float _atkBonus = DefaultAtkBonus;

    bool _lowHpAtkOn;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        if (row.Param1 > 0f) _hpThreshold = row.Param1;
        if (row.Param2 > 0f) _atkBonus = row.Param2;
    }

    public override void OnUpdate(float dt)
    {
        if (Merc == null || Merc.attr == null) return;
        float ratio = Merc.currentHp / Mathf.Max(1f, Merc.attr.GetAttr(AttrType.MaxHp));
        bool should = ratio < _hpThreshold;
        if (should == _lowHpAtkOn) return;
        _lowHpAtkOn = should;
        Merc.attr.AddAttr(AttrType.Attack, should ? _atkBonus : -_atkBonus, true);
    }
}
