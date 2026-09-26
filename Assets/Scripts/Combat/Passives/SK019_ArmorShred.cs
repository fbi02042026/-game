using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SK019 破甲侵蚀：攻击命中降低目标防御，固定每层 -X%，持续 N 秒，叠加上限 M 层。
/// 私有状态 = _shreds（只属于本模块）。
/// ⚠️ 2026-09-26 主人拍板：AppliedAmount 累计真实施加量，到期对称还原（修「到期不还原」/「永久 -10%」bug）。
/// 该对称还原逻辑原样保留，不得改动。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——每层减防比例(p1)/最大层数(p2)/持续时间(Duration列)
/// 从 merc_skills 表读；表缺失回退默认常量。add 仍 = -每层减防比例，保证对称还原。
/// </summary>
public class SK019_ArmorShred : MercPassiveModule
{
    struct ArmorShredState
    {
        public int Stacks;
        public float Timer;
        // 累计本次实际施加的减防总量，到期对称还原
        public float AppliedAmount;
    }

    // —— 默认硬编码值（兜底）——
    const float DefaultPerLayer = 0.1f; // 每层减防比例
    const int DefaultMaxStacks = 3;     // 最大叠层
    const float DefaultDuration = 5f;    // 持续时间(秒)，复用表 Duration 列

    float _perLayer = DefaultPerLayer;
    int _maxStacks = DefaultMaxStacks;
    float _duration = DefaultDuration;

    public override void Configure(MercSkillTable.Row row)
    {
        // 2026-09-26 修编译错误：MercSkillTable.Row 是 struct，不能与 null 比较 → 改判 Id
        if (string.IsNullOrEmpty(row.Id)) return;
        if (row.Param1 > 0f) _perLayer = row.Param1;
        if (row.Param2 > 0f) _maxStacks = Mathf.RoundToInt(row.Param2);
        if (row.Duration > 0f) _duration = row.Duration;
    }

    readonly Dictionary<UnitBase, ArmorShredState> _shreds = new Dictionary<UnitBase, ArmorShredState>();

    public override void OnBasicAttackHit(UnitBase target, float damage)
    {
        if (target == null || Merc == null) return;
        ApplyArmorShred(target);
    }

    void ApplyArmorShred(UnitBase target)
    {
        if (!_shreds.TryGetValue(target, out var st))
            st = new ArmorShredState();
        // 2026-09-26 主人拍板：每次破甲固定 -每层比例（维持 Mathf.Min(最大层, Stacks+1) 语义），并累计到 AppliedAmount
        float add = -_perLayer;
        st.Stacks = Mathf.Min(_maxStacks, st.Stacks + 1);
        st.Timer = _duration;
        st.AppliedAmount += add; // 累计真实施加量，保证「加多少还原多少」（含超过上限的溢出）
        _shreds[target] = st;
        if (target.attr != null)
            target.attr.AddAttr(AttrType.Defense, add, true);
    }

    public override void OnUpdate(float dt)
    {
        if (_shreds.Count == 0) return;
        var keys = new List<UnitBase>(_shreds.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var target = keys[i];
            if (target == null || target.isDead)
            {
                _shreds.Remove(target);
                continue;
            }
            var st = _shreds[target];
            st.Timer -= dt;
            if (st.Timer <= 0f)
            {
                // 2026-09-26 主人拍板：到期对称还原累计减防，再移除（修「永久 -10%」bug）
                if (target.attr != null && st.AppliedAmount != 0f)
                    target.attr.AddAttr(AttrType.Defense, -st.AppliedAmount, true);
                _shreds.Remove(target);
            }
            else _shreds[target] = st;
        }
    }
}
