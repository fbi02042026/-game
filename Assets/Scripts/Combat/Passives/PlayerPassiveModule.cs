using UnityEngine;

/// <summary>
/// 玩家职业被动模块基类。每个 PS0xx 一个独立子类（见同目录 PS001_Aura 等），
/// 只持有自己私有的状态字段；由 PlayerPassiveCombat（调度器）按当前职业被动 id 转发。
/// 数值 / 时序 / 判定逻辑与旧 PlayerPassiveCombat 大杂烩完全一致，仅做结构拆分（2026-09-26）。
/// </summary>
public abstract class PlayerPassiveModule
{
    /// <summary>所属调度器。</summary>
    public PlayerPassiveCombat Runner { get; set; }

    /// <summary>便捷访问当前英雄。</summary>
    protected Hero Hero => Hero.Instance;

    /// <summary>从 player_passives 表行初始化本模块数值（只消费属于本被动的列）。</summary>
    public virtual void Configure(PlayerPassiveTables.Row row) { }

    public virtual void OnUpdate() { }

    public virtual void OnHeroCritHit(UnitBase target) { }

    public virtual float GetAllyIncomingDamageMul(UnitBase victim) => 1f;

    public virtual float GetAttackSpeedMul() => 1f;
}
