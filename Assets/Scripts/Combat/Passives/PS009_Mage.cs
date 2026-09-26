using UnityEngine;

/// <summary>
/// PS009 法师站桩：站立不动叠攻击加成，移动则回退。私有状态 = _mageAtkBonusPct / _mageStillThreshold / _mageStandingBonus / _lastPos / _stillTimer。
/// 逻辑原样照抄旧 PlayerPassiveCombat 的 TickMageStand。
/// </summary>
public class PS009_Mage : PlayerPassiveModule
{
    float _mageAtkBonusPct = 0.20f;
    float _mageStillThreshold = 0.05f;
    // —— 默认硬编码值（兜底）——
    const float DefaultStandDelay = 0.15f; // 静止多久后才叠加成(秒)
    float _mageStandDelay = DefaultStandDelay;
    bool _mageStandingBonus;
    Vector3 _lastPos;
    float _stillTimer;

    public override void Configure(PlayerPassiveTables.Row row)
    {
        if (row == null) return;
        // 与旧版 BindForBattle 同一套 % 解析
        float v = row.ValueNumber;
        if (v > 1f && (row.ValueRaw.Contains("%") || v >= 5f)) v *= 0.01f;
        if (v > 0.01f) _mageAtkBonusPct = v;
        // 2026-09-26 主人拍板：数值走表，模块只管模式——静止阈值走 p1、站桩延迟走 p2
        if (row.Param1 > 0f) _mageStillThreshold = row.Param1;
        if (row.Param2 > 0f) _mageStandDelay = row.Param2;
        // 旧版在 BindForBattle 里 _lastPos = transform.position；这里在战斗开始时对齐，保持首帧 moved≈0 行为一致
        _lastPos = Hero != null ? Hero.transform.position : Vector3.zero;
    }

    public override void OnUpdate()
    {
        var hero = Hero;
        if (hero?.attr == null) return;
        Vector3 p = hero.transform.position;
        float moved = Vector3.Distance(p, _lastPos);
        _lastPos = p;
        if (moved > _mageStillThreshold)
        {
            _stillTimer = 0f;
            if (_mageStandingBonus)
            {
                float atk = hero.attr.GetAttr(AttrType.Attack);
                hero.attr.SetAttr(AttrType.Attack, atk / (1f + _mageAtkBonusPct));
                _mageStandingBonus = false;
            }
            return;
        }
        _stillTimer += Time.deltaTime;
        if (!_mageStandingBonus && _stillTimer >= _mageStandDelay)
        {
            float atk = hero.attr.GetAttr(AttrType.Attack);
            hero.attr.SetAttr(AttrType.Attack, atk * (1f + _mageAtkBonusPct));
            _mageStandingBonus = true;
        }
    }
}
