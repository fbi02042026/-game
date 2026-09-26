using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SK002 割裂：普攻触发概率给目标挂流血，持续时间内持续掉血，可叠层。
/// 私有状态 = _bleeds（只属于本模块）。逻辑原样照抄旧 MercPassiveRunner 的 ApplyBleed / TickBleeds。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——触发概率(p1)/最大层数(p2)/每层DPS系数(p3)/持续时间(Duration列)
/// 都从 merc_skills 表读；表缺失回退默认常量，行为不变。
/// </summary>
public class SK002_Bleed : MercPassiveModule
{
    struct BleedState
    {
        public int Stacks;
        public float Timer;
        public float Dps;
    }

    // —— 默认硬编码值（兜底：表缺失时回退，保持原行为）——
    const float DefaultTriggerProb = 0.3f;   // 普攻触发概率
    const int DefaultMaxStacks = 2;          // 最大叠层
    const float DefaultDpsCoef = 0.25f;      // 每层 DPS = 攻击 × 此系数
    const float DefaultDuration = 3f;        // 持续时间(秒)，复用表 Duration 列

    float _triggerProb = DefaultTriggerProb;
    int _maxStacks = DefaultMaxStacks;
    float _dpsCoef = DefaultDpsCoef;
    float _duration = DefaultDuration;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        // 表值 > 0 才覆盖（留空回退默认，避免误填 0 让技能失效）
        if (row.Param1 > 0f) _triggerProb = row.Param1;
        if (row.Param2 > 0f) _maxStacks = Mathf.RoundToInt(row.Param2);
        if (row.Param3 > 0f) _dpsCoef = row.Param3;
        if (row.Duration > 0f) _duration = row.Duration;
    }

    readonly Dictionary<UnitBase, BleedState> _bleeds = new Dictionary<UnitBase, BleedState>();

    public override void OnBasicAttackHit(UnitBase target, float damage)
    {
        if (target == null || Merc == null) return;
        if (Random.value < _triggerProb)
            ApplyBleed(target);
    }

    void ApplyBleed(UnitBase target)
    {
        if (!_bleeds.TryGetValue(target, out var st))
            st = new BleedState();
        st.Stacks = Mathf.Min(_maxStacks, st.Stacks + 1);
        st.Timer = _duration;
        st.Dps = (Merc.attr != null ? Merc.attr.GetAttr(AttrType.Attack) : 10f) * _dpsCoef * st.Stacks;
        _bleeds[target] = st;
    }

    public override void OnUpdate(float dt)
    {
        if (_bleeds.Count == 0) return;
        var keys = new List<UnitBase>(_bleeds.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var target = keys[i];
            if (target == null || target.isDead)
            {
                _bleeds.Remove(target);
                continue;
            }
            var st = _bleeds[target];
            st.Timer -= dt;
            target.TakeDamage(st.Dps * dt, false, true, showHitVfx: false, source: Merc);
            if (st.Timer <= 0f) _bleeds.Remove(target);
            else _bleeds[target] = st;
        }
    }
}
