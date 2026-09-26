using UnityEngine;

/// <summary>
/// PS008 游侠攻速：暴击命中给自己加攻速 buff（默认 3s，+25%）。私有状态 = _rangerAtkSpdUntil / _rangerAtkSpdMul / _rangerBuffDuration。
/// 逻辑原样照抄旧 PlayerPassiveCombat 的 OnHeroCritHit(PS008 分支) / GetAttackSpeedMul / BindForBattle(PS008 配置)。
/// </summary>
public class PS008_Ranger : PlayerPassiveModule
{
    float _rangerAtkSpdUntil;
    float _rangerAtkSpdMul = 1.25f;
    float _rangerBuffDuration = 3f;

    public override void Configure(PlayerPassiveTables.Row row)
    {
        if (row == null) return;
        // 与旧版 BindForBattle 同一套 % 解析
        float v = row.ValueNumber;
        if (v > 1f && (row.ValueRaw.Contains("%") || v >= 5f)) v *= 0.01f;
        if (v > 0.01f) _rangerAtkSpdMul = 1f + v;
        // 冷却列 = buff 持续时间（旧版：if (_passiveId == "PS008" && row.CooldownSeconds > 0.01f) _rangerBuffDuration = row.CooldownSeconds;）
        if (row.CooldownSeconds > 0.01f) _rangerBuffDuration = row.CooldownSeconds;
    }

    public override void OnHeroCritHit(UnitBase target)
    {
        _rangerAtkSpdUntil = Time.time + _rangerBuffDuration;
    }

    public override float GetAttackSpeedMul()
    {
        if (Time.time < _rangerAtkSpdUntil) return _rangerAtkSpdMul;
        return 1f;
    }
}
