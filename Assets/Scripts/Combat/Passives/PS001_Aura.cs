using UnityEngine;

/// <summary>
/// PS001 减伤光环：友军（isAlly）在光环半径内受击减伤 (1 - pct)。私有状态 = _shieldAuraPct。
/// 逻辑原样照抄旧 PlayerPassiveCombat 的 GetAllyIncomingDamageMul(PS001 分支)。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——光环半径走 player_passives 表 p1，减伤比例走 数值列；表缺失回退默认常量。
/// </summary>
public class PS001_Aura : PlayerPassiveModule
{
    // —— 默认硬编码值（兜底）——
    const float DefaultShieldAuraPx = 120f; // 光环半径(px)，2026-09-26 起从表 p1 读
    float _shieldAuraPx = DefaultShieldAuraPx;
    float _shieldAuraPct = 0.15f;

    public override void Configure(PlayerPassiveTables.Row row)
    {
        if (row == null) return;
        // 2026-09-26 主人拍板：数值走表，模块只管模式——光环半径走 p1
        if (row.Param1 > 0f) _shieldAuraPx = row.Param1;
        // 与旧版 BindForBattle 同一套 % 解析：>1 且带 % 或 ≥5 视为百分比
        float v = row.ValueNumber;
        if (v > 1f && (row.ValueRaw.Contains("%") || v >= 5f)) v *= 0.01f;
        if (v > 0.01f) _shieldAuraPct = v;
    }

    public override float GetAllyIncomingDamageMul(UnitBase victim)
    {
        if (victim == null || !victim.isAlly) return 1f;
        var hero = Hero;
        if (hero == null || hero.isDead) return 1f;
        float maxDist = _shieldAuraPx / GameConfig.PIXEL_PER_UNIT;
        if (Vector3.Distance(hero.transform.position, victim.transform.position) > maxDist)
            return 1f;
        return Mathf.Clamp01(1f - _shieldAuraPct);
    }
}
