using UnityEngine;

/// <summary>
/// PS012 牧师治疗：周期给全队（英雄 + 存活佣兵）回血。私有状态 = _priestTick / _priestInterval / _priestHealPct。
/// 逻辑原样照抄旧 PlayerPassiveCombat 的 TickPriestHeal（共用 HealUnit helper 留在调度器）。
/// </summary>
public class PS012_Priest : PlayerPassiveModule
{
    float _priestTick;
    float _priestInterval = 5f;
    float _priestHealPct = 0.05f;

    public override void Configure(PlayerPassiveTables.Row row)
    {
        if (row == null) return;
        float v = row.ValueNumber;
        if (v > 1f && (row.ValueRaw.Contains("%") || v >= 5f)) v *= 0.01f;
        if (v > 0.01f) _priestHealPct = v;
        if (row.CooldownSeconds > 0.01f) _priestInterval = row.CooldownSeconds;
    }

    public override void OnUpdate()
    {
        _priestTick += Time.deltaTime;
        if (_priestTick < _priestInterval) return;
        _priestTick = 0f;
        var hero = Hero;
        if (hero?.attr == null) return;
        float heal = hero.attr.GetAttr(AttrType.MaxHp) * _priestHealPct;
        var bm = BattleManager.Instance;
        if (bm == null) return;
        PlayerPassiveCombat.HealUnit(hero, heal);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null || m.isDead || m.attr == null) continue;
            PlayerPassiveCombat.HealUnit(m, m.attr.GetAttr(AttrType.MaxHp) * _priestHealPct);
        }
    }
}
