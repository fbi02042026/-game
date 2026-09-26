using UnityEngine;

/// <summary>
/// PS005 狂暴：血量越低攻击越高（每 10% 缺血症 +3%，上限 30%，离开阈值回退）。
/// 私有状态 = _berserkBonusApplied。逻辑原样照抄旧 PlayerPassiveCombat 的 TickBerserk。
/// </summary>
public class PS005_Berserk : PlayerPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultStep = 0.10f;     // 每缺失 10% 血量算一阶
    const float DefaultPerStep = 0.03f;  // 每阶攻击加成
    const float DefaultCap = 0.30f;      // 加成上限
    float _berserkStep = DefaultStep;
    float _berserkPerStep = DefaultPerStep;
    float _berserkCap = DefaultCap;

    float _berserkBonusApplied;

    public override void Configure(PlayerPassiveTables.Row row)
    {
        if (row == null) return;
        // 2026-09-26 主人拍板：数值走表，模块只管模式——阶长/每阶加成/上限走 p1/p2/p3
        if (row.Param1 > 0f) _berserkStep = row.Param1;
        if (row.Param2 > 0f) _berserkPerStep = row.Param2;
        if (row.Param3 > 0f) _berserkCap = row.Param3;
    }

    public override void OnUpdate()
    {
        var hero = Hero;
        if (hero?.attr == null) return;
        float maxHp = Mathf.Max(1f, hero.attr.GetAttr(AttrType.MaxHp));
        float missing = 1f - Mathf.Clamp01(hero.currentHp / maxHp);
        float stacks = Mathf.Floor(missing / _berserkStep);
        float bonusPct = Mathf.Min(_berserkCap, stacks * _berserkPerStep);
        float delta = bonusPct - _berserkBonusApplied;
        if (Mathf.Abs(delta) < 0.0001f) return;
        // 用百分比攻击加成叠在 AttrSystem 上：相对当前攻击调整
        float atk = hero.attr.GetAttr(AttrType.Attack);
        float baseWithout = atk / Mathf.Max(0.01f, 1f + _berserkBonusApplied);
        hero.attr.SetAttr(AttrType.Attack, baseWithout * (1f + bonusPct));
        _berserkBonusApplied = bonusPct;
    }
}
