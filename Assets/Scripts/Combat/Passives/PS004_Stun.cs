using UnityEngine;

/// <summary>
/// PS004 重击眩晕：暴击命中给目标上眩晕。私有状态 = _heavyStunSec。
/// 逻辑原样照抄旧 PlayerPassiveCombat 的 OnHeroCritHit(PS004 分支)。
/// </summary>
public class PS004_Stun : PlayerPassiveModule
{
    float _heavyStunSec = 1f;

    public override void Configure(PlayerPassiveTables.Row row)
    {
        if (row == null) return;
        // 与旧版 BindForBattle 一致：优先冷却列，否则用 ValueNumber（<10 视为秒数）
        if (row.CooldownSeconds > 0.01f) _heavyStunSec = row.CooldownSeconds;
        else if (row.ValueNumber > 0.01f && row.ValueNumber < 10f) _heavyStunSec = row.ValueNumber;
    }

    public override void OnHeroCritHit(UnitBase target)
    {
        target.ApplyStun(_heavyStunSec);
    }
}
