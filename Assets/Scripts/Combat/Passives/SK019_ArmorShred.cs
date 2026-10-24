using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SK019 破甲侵蚀：攻击命中降低目标防御，固定每层 -X%，持续 N 秒，叠加上限 M 层。
/// 私有状态 = _shreds（只属于本模块）。
/// ⚠️ 2026-10-24 主人拍板（覆盖 2026-09-26「AppliedAmount 对称还原原样保留」口径——
/// 该实现在乘法语义下并不对称，实测每轮净 -5%~-83% 物防，属 P0 bug）：
/// ① 层数上限真正限制施加次数：仅「层数真增加」时 AddAttr 一次，满层只刷计时；
/// ② 还原按层数求逆：施加 N 层 = ×(1-perLayer)^N，到期还原 = ×(1-perLayer)^(-N)，精确回 1；
/// ③ 按目标 IsMagicDamageDealer 分流减 Defense / MagicDefense（魔法型精英/Boss 物防=0，
///    旧码破甲对它们完全无效）；施加与还原锁定同一条（State.DefType）。
/// 2026-09-26 主人拍板：数值走表，模块只管模式——每层减防比例(p1)/最大层数(p2)/持续时间(Duration列)
/// 从 merc_skills 表读；表缺失回退默认常量。此口径不变。
/// </summary>
public class SK019_ArmorShred : MercPassiveModule
{
    struct ArmorShredState
    {
        public int Stacks;
        public float Timer;
        // 【2026-10-24】本次破甲减的是哪条防御（按目标物/魔类型分流，首次命中锁定），
        // 到期按同一条还原，保证施加/还原同一属性键
        public AttrType DefType;
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
        // 【2026-10-24 主人拍板】见类头注释 ①②③：层数真增加才施加、还原按层数求逆、
        // 按目标类型分流 Defense / MagicDefense。
        if (target.attr == null) return;
        var defType = target.IsMagicDamageDealer() ? AttrType.MagicDefense : AttrType.Defense;
        if (!_shreds.TryGetValue(target, out var st))
            st = new ArmorShredState { DefType = defType };
        if (st.Stacks < _maxStacks)
        {
            st.Stacks++;
            target.attr.AddAttr(st.DefType, -_perLayer, true);
        }
        st.Timer = _duration;
        _shreds[target] = st;
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
                // 【2026-10-24 主人拍板】还原按层数求逆，与施加严格对称：
                // 施加了 N 次 ×(1-perLayer)，这里一次性 ×(1-perLayer)^(-N)，净效果精确回 1。
                // 旧码还原 -(AppliedAmount) 是加法口径，乘法下 3 层后 0.729×1.3=0.948D（永久 -5%）。
                if (target.attr != null && st.Stacks > 0)
                {
                    float inverse = Mathf.Pow(1f - _perLayer, -st.Stacks) - 1f;
                    target.attr.AddAttr(st.DefType, inverse, true);
                }
                _shreds.Remove(target);
            }
            else _shreds[target] = st;
        }
    }
}
