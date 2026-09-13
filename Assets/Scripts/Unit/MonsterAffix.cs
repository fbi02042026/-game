using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 挂在精英 / Boss 身上的词缀组件（V6）。
/// 全部是<b>纯数值乘子 + 一个死亡事件</b>：不引入新 AI、不需要新美术。
/// 由 <see cref="RollAndAttach"/> 在 Monster 属性初始化末尾生成。
/// </summary>
public class MonsterAffix : MonoBehaviour
{
    readonly List<MonsterAffixId> _ids = new List<MonsterAffixId>();
    Monster _owner;
    bool _splitDone;

    /// <summary>受到伤害的乘子（&lt;1 = 减伤）。Monster.TakeDamage 会读它。</summary>
    public float DamageTakenMul { get; private set; } = 1f;
    /// <summary>反弹给近战攻击者的比例。</summary>
    public float ThornsRatio { get; private set; }
    /// <summary>造成伤害时回血的比例。</summary>
    public float LeechRatio { get; private set; }

    public static MonsterAffix Get(Monster m)
    {
        return m != null ? m.GetComponent<MonsterAffix>() : null;
    }

    /// <summary>
    /// 给精英/Boss 掷词缀并挂上。普通小怪传 eliteOrBoss=false 直接跳过（避免视觉噪音）。
    /// 幂等：已经挂过就直接返回，不重复叠加。
    /// </summary>
    public static MonsterAffix RollAndAttach(Monster m, int chapter, bool eliteOrBoss)
    {
        if (m == null || !eliteOrBoss) return null;
        var existing = m.GetComponent<MonsterAffix>();
        if (existing != null) return existing;

        var comp = m.gameObject.AddComponent<MonsterAffix>();
        comp._owner = m;
        comp.Apply(MonsterAffixDefs.Roll(chapter), m);
        return comp;
    }

    void Apply(List<MonsterAffixId> ids, Monster m)
    {
        _ids.Clear();
        if (ids == null || ids.Count == 0) return;
        if (m == null || m.attr == null) return;

        float atkSpeed = 1f, moveSpeed = 1f, taken = 1f;
        for (int i = 0; i < ids.Count; i++)
        {
            var d = MonsterAffixDefs.Get(ids[i]);
            _ids.Add(ids[i]);
            if (d.AtkSpeedMul > 0f) atkSpeed *= d.AtkSpeedMul;
            if (d.MoveSpeedMul > 0f) moveSpeed *= d.MoveSpeedMul;
            if (d.DamageTakenMul > 0f) taken *= d.DamageTakenMul;
            if (d.ThornsRatio > ThornsRatio) ThornsRatio = d.ThornsRatio;
            if (d.LeechRatio > LeechRatio) LeechRatio = d.LeechRatio;
        }
        DamageTakenMul = taken;

        // 属性已在 Monster 里按表算完，这里只做乘子叠加
        if (!Mathf.Approximately(atkSpeed, 1f))
            m.attr.SetAttr(AttrType.AttackSpeed, m.attr.GetAttr(AttrType.AttackSpeed) * atkSpeed);
        if (!Mathf.Approximately(moveSpeed, 1f))
            m.attr.SetAttr(AttrType.MoveSpeed, m.attr.GetAttr(AttrType.MoveSpeed) * moveSpeed);
    }

    /// <summary>血条后缀，如「·狂暴·铁壁」；无词缀返回空串。</summary>
    public string Suffix
    {
        get
        {
            if (_ids.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < _ids.Count; i++)
                sb.Append('·').Append(MonsterAffixDefs.Name(_ids[i]));
            return sb.ToString();
        }
    }

    public int Count => _ids.Count;

    /// <summary>词缀「吸血」：造成伤害后按比例回血（UnitBase.TakeDamage 里回调）。</summary>
    public void OnDealtDamage(float finalDamage)
    {
        if (LeechRatio <= 0f || finalDamage <= 0f) return;
        if (_owner == null || _owner.isDead || _owner.attr == null) return;

        float maxHp = Mathf.Max(1f, _owner.attr.GetAttr(AttrType.MaxHp));
        float heal = finalDamage * LeechRatio;
        float before = _owner.currentHp;
        _owner.currentHp = Mathf.Min(maxHp, before + heal);
        float healed = _owner.currentHp - before;
        if (healed >= 1f)
            DamageTextSystem.Instance?.SpawnHealText(_owner.GetHitPosition(), Mathf.RoundToInt(healed));
    }

    /// <summary>词缀「分裂」：死亡时分裂出残血小怪（Monster.TakeDamage 里回调）。</summary>
    public void NotifyDead()
    {
        if (_splitDone) return;
        _splitDone = true;
        if (_owner == null) return;
        if (!Has(MonsterAffixId.Split)) return;

        int n = MonsterAffixDefs.Get(MonsterAffixId.Split).SplitCount;
        if (n <= 0) return;

        var bm = BattleManager.Instance;
        var planner = bm != null ? bm.Planner : null;
        if (planner == null) return;
        if (_owner.config == null) return;

        Vector3 pos = _owner.transform.position;
        for (int i = 0; i < n; i++)
        {
            // 左右各偏一点，避免完全重叠
            var p = pos + new Vector3((i % 2 == 0 ? -1f : 1f) * 0.6f * (1 + i / 2), 0f, 0f);
            planner.SpawnMonsterAt(_owner.config, bm.currentStage != null ? bm.currentStage.stageIndex : 0, p, 0.3f);
        }
    }

    public bool Has(MonsterAffixId id)
    {
        for (int i = 0; i < _ids.Count; i++)
            if (_ids[i] == id) return true;
        return false;
    }
}
