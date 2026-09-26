using UnityEngine;

/// <summary>
/// 佣兵被动模块基类。每个 SK0xx 被动一个独立子类（见同目录 SK002_Bleed 等），
/// 只持有自己私有的状态字段；逻辑由 MercPassiveRunner（调度器）按当前 passiveId 转发。
/// 数值 / 时序 / 判定逻辑与旧 MercPassiveRunner 大杂烩完全一致，仅做结构拆分（2026-09-26）。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——具体数值由 Configure(MercSkillTable.Row) 从 merc_skills 表读入，
/// 表缺失时回退各模块内默认硬编码常量，行为逻辑不变。
/// </summary>
public abstract class MercPassiveModule
{
    /// <summary>所属调度器（持有 _merc 等共享上下文）。</summary>
    public MercPassiveRunner Runner { get; set; }

    /// <summary>从 merc_skills 表行初始化本模块数值（只消费属于本被动的列）。默认空实现，未识别/主动技行无需配置。</summary>
    public virtual void Configure(MercSkillTable.Row row) { }

    /// <summary>便捷访问：本佣兵单元。</summary>
    protected Mercenary Merc => Runner != null ? Runner.Merc : null;

    /// <summary>每帧 tick（dt 秒）。子类按需重写；只处理「属于自己的」被动逻辑。</summary>
    public virtual void OnUpdate(float dt) { }

    /// <summary>普攻命中目标时。</summary>
    public virtual void OnBasicAttackHit(UnitBase target, float damage) { }

    /// <summary>造成魔法伤害时（ref 可改伤害）。</summary>
    public virtual void OnDealMagicDamage(ref float damage) { }

    /// <summary>本佣兵承受伤害时（见调度器 ModifyIncomingDamage，在护盾抵扣之前）。</summary>
    public virtual void ModifyIncomingDamage(ref float damage) { }

    /// <summary>本佣兵血量变化时。</summary>
    public virtual void OnHpChanged() { }

    /// <summary>本佣兵治疗他人后。</summary>
    public virtual void OnOwnerHealed(float amount) { }
}
