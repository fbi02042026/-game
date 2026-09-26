using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 毒 DOT 运行器：游侠武器「中毒」装备词缀的结算点（2026-09-26 主人拍板：毒之前没落地，本次补上）。
/// 结构照抄 MercPassiveRunner 的流血 DOT（ApplyBleed / TickBleeds）。
/// 节奏参数全部走 GameConfig（POISON_TICK_INTERVAL / POISON_DURATION / POISON_DPS_RATIO），
/// 后期调数值只改 GameConfig，不用动这里。
/// </summary>
public class PoisonDotRunner : MonoBehaviour
{
    Hero _hero;
    readonly Dictionary<UnitBase, PoisonState> _poisons = new Dictionary<UnitBase, PoisonState>();

    struct PoisonState
    {
        public int Stacks;     // 层数（照抄流血，上限 2）
        public float Timer;    // 剩余持续时间
        public float Acc;      // 距上次跳伤的累计时间
        public float PerTick;  // 每跳伤害（含层数叠加）
    }

    void Awake()
    {
        _hero = GetComponent<Hero>();
        if (_hero != null)
            _hero.OnAttack += OnHeroBasicAttackHit; // 照抄 Mercenary.WirePassiveOnAttack
    }

    void OnDestroy()
    {
        if (_hero != null)
            _hero.OnAttack -= OnHeroBasicAttackHit;
    }

    /// <summary>普攻命中即给目标挂毒（照抄 MercPassiveRunner.OnBasicAttackHit）。</summary>
    void OnHeroBasicAttackHit(UnitBase target, float damage, bool isCrit)
    {
        if (_hero == null || _hero.attr == null) return;
        // 2026-09-26 主人拍板：毒伤只给游侠，职业白名单走表，不写死。
        // 不在白名单内的职业（equip_attr_ranges.适用职业）即使穿了带毒词缀的装备也不触发毒伤。
        if (!RiftEquipTables.IsAttrAllowedForJob("POISON", PlayerJobDefs.GetSelected())) return;
        float poison = _hero.attr.GetAttr(AttrType.Poison);
        if (poison <= 0f) return;                                   // 没穿中毒词缀不触发
        if (target == null || target.isDead || target.attr == null) return;
        float atk = _hero.attr.GetAttr(AttrType.Attack);
        float perTick = atk * poison * GameConfig.POISON_DPS_RATIO; // 每跳伤害
        ApplyPoison(target, perTick);
    }

    void ApplyPoison(UnitBase target, float perTick)
    {
        if (!_poisons.TryGetValue(target, out var st))
            st = new PoisonState();
        st.Stacks = Mathf.Min(2, st.Stacks + 1);                    // 照抄流血上限 2 层
        st.Timer = GameConfig.POISON_DURATION;
        st.Acc = 0f;
        st.PerTick = perTick * st.Stacks;                           // 多层叠加（与流血一致）
        _poisons[target] = st;
    }

    void Update()
    {
        if (_hero == null) return;
        TickPoisons(Time.deltaTime);
    }

    /// <summary>照抄 MercPassiveRunner.TickBleeds：按间隔跳伤、到期移除。</summary>
    void TickPoisons(float dt)
    {
        if (_poisons.Count == 0) return;
        var keys = new List<UnitBase>(_poisons.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var target = keys[i];
            if (target == null || target.isDead)
            {
                _poisons.Remove(target);
                continue;
            }
            var st = _poisons[target];
            st.Timer -= dt;
            st.Acc += dt;
            while (st.Acc >= GameConfig.POISON_TICK_INTERVAL)
            {
                st.Acc -= GameConfig.POISON_TICK_INTERVAL;
                target.TakeDamage(st.PerTick, false, true, showHitVfx: false, source: _hero);
            }
            if (st.Timer <= 0f) _poisons.Remove(target);
            else _poisons[target] = st;
        }
    }
}
