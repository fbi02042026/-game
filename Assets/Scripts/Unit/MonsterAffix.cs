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
    // 【2026-11-12 主人拍板：迅捷表定词缀怪打一下拉开距离再回来打；只表定怪生效，精英/Boss 掷到 Swift 不触发】
    float _kiteUntil;
    const float KITE_SECONDS = 0.6f;

    /// <summary>受到伤害的乘子（&lt;1 = 减伤）。Monster.TakeDamage 会读它。</summary>
    public float DamageTakenMul { get; private set; } = 1f;
    /// <summary>反弹给近战攻击者的比例。</summary>
    public float ThornsRatio { get; private set; }
    /// <summary>造成伤害时回血的比例。</summary>
    public float LeechRatio { get; private set; }
    /// <summary>攻击暴击概率（0 = 无暴击词缀，2026-10-26 词缀怪）。</summary>
    public float CritChance { get; private set; }
    /// <summary>暴击伤害倍率。</summary>
    public float CritDamageMul { get; private set; } = 1f;
    /// <summary>是否按词缀色着色（表定词缀怪「看着加点不同」，2026-10-26）。</summary>
    public bool VisualTinted { get; private set; }

    public static MonsterAffix Get(Monster m)
    {
        return m != null ? m.GetComponent<MonsterAffix>() : null;
    }

    /// <summary>
    /// 给精英/Boss 掷词缀并挂上。普通小怪传 eliteOrBoss=false 直接跳过（避免视觉噪音）。
    /// 复用语义：池化怪 Init 开头必先 ResetForReuse → ResetState，组件状态保证是干净的，
    /// 这里直接重置后重挂（旧版「已挂过直接返回」会把上一只怪的词缀带进这一只，2026-10-26 改掉）。
    /// </summary>
    public static MonsterAffix RollAndAttach(Monster m, int chapter, bool eliteOrBoss)
    {
        if (m == null || !eliteOrBoss) return null;
        var comp = m.GetComponent<MonsterAffix>();
        if (comp == null)
        {
            comp = m.gameObject.AddComponent<MonsterAffix>();
            comp._owner = m;
        }
        comp.ResetState();
        comp.Apply(MonsterAffixDefs.Roll(chapter), m);
        return comp;
    }

    /// <summary>
    /// 【2026-10-26 词缀怪】按 monster_stats.affix 列给普通小怪挂<b>固定</b>词缀（不掷骰）。
    /// 与 <see cref="RollAndAttach"/> 互斥：精英/Boss/签名怪仍走掷骰（调用方在 Monster.Init 里分流）。
    /// 复用语义同 RollAndAttach：Init 前置 ResetState，这里重置后重挂。ids 为空返回 null（不挂组件）。
    /// </summary>
    public static MonsterAffix AttachFixed(Monster m, List<MonsterAffixId> ids)
    {
        if (m == null || ids == null || ids.Count == 0) return null;
        var comp = m.GetComponent<MonsterAffix>();
        if (comp == null)
        {
            comp = m.gameObject.AddComponent<MonsterAffix>();
            comp._owner = m;
        }
        comp.ResetState();
        comp.VisualTinted = true; // 表定词缀怪上色，让玩家一眼看出「这只不一样」
        comp.Apply(ids, m);
        comp.ApplyTint();
        return comp;
    }

    /// <summary>
    /// 【2026-10-26 词缀怪】回池/复用前清空全部词缀状态（池化怪复用，旧词缀与染色不能带到下一只）。
    /// 不 Destroy 组件：Destroy 延迟到帧末，同帧 Init 的挂词缀路径会拿到「将死组件」误判已挂。
    /// </summary>
    public void ResetState()
    {
        bool hadTint = VisualTinted;
        _ids.Clear();
        DamageTakenMul = 1f;
        ThornsRatio = 0f;
        LeechRatio = 0f;
        CritChance = 0f;
        CritDamageMul = 1f;
        _splitDone = false;
        _kiteUntil = 0f;   // 池化复用：清掉上一只的打带跑状态
        VisualTinted = false;
        if (hadTint && _owner != null)
            _owner.ClearAffixTint();
    }    void Apply(List<MonsterAffixId> ids, Monster m)
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
            if (d.CritChance > CritChance) CritChance = d.CritChance;
            CritDamageMul = Mathf.Max(CritDamageMul, d.CritDamageMul);
        }
        DamageTakenMul = taken;

        // 属性已在 Monster 里按表算完，这里只做乘子叠加
        if (!Mathf.Approximately(atkSpeed, 1f))
            m.attr.SetAttr(AttrType.AttackSpeed, m.attr.GetAttr(AttrType.AttackSpeed) * atkSpeed);
        if (!Mathf.Approximately(moveSpeed, 1f))
            m.attr.SetAttr(AttrType.MoveSpeed, m.attr.GetAttr(AttrType.MoveSpeed) * moveSpeed);

        // 【2026-11-12 主人拍板】只表定词缀怪（VisualTinted）按词条加基础属性：
        // 每个词条只加自己那一项，多个词条累乘；精英/Boss 掷骰词缀属性一律不动。
        if (VisualTinted)
        {
            float hpMul = 1f, atkMul = 1f, defMul = 1f;
            for (int i = 0; i < ids.Count; i++)
            {
                var d = MonsterAffixDefs.Get(ids[i]);
                if (d.HpMul > 0f) hpMul *= d.HpMul;
                if (d.AtkMul > 0f) atkMul *= d.AtkMul;
                if (d.DefMul > 0f) defMul *= d.DefMul;
            }
            if (!Mathf.Approximately(hpMul, 1f))
            {
                m.attr.SetAttr(AttrType.MaxHp, m.attr.GetAttr(AttrType.MaxHp) * hpMul);
                // Monster.Init.cs:441 在挂词缀（:481）之前就把 currentHp 锁死了，不同步会出现血条满但实际血没涨。
                m.currentHp = m.attr.GetAttr(AttrType.MaxHp);
            }
            if (!Mathf.Approximately(atkMul, 1f))
            {
                m.attr.SetAttr(AttrType.Attack, m.attr.GetAttr(AttrType.Attack) * atkMul);
                m.attr.SetAttr(AttrType.MagicAttack, m.attr.GetAttr(AttrType.MagicAttack) * atkMul);
            }
            if (!Mathf.Approximately(defMul, 1f))
            {
                m.attr.SetAttr(AttrType.Defense, m.attr.GetAttr(AttrType.Defense) * defMul);
                m.attr.SetAttr(AttrType.MagicDefense, m.attr.GetAttr(AttrType.MagicDefense) * defMul);
            }
        }
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

    /// <summary>第一个词缀的展示色（无词缀返回白色）。【2026-11-05】头顶词条标签用它，与染色/血条后缀同源。</summary>
    public Color FirstTint
    {
        get
        {
            if (_ids.Count == 0) return Color.white;
            return MonsterAffixDefs.Get(_ids[0]).Tint;
        }
    }

    public int Count => _ids.Count;

    /// <summary>词缀「吸血」：造成伤害后按比例回血（UnitBase.TakeDamage 里回调）。</summary>
    public void OnDealtDamage(float finalDamage)
    {
        if (LeechRatio <= 0f || finalDamage <= 0f) return;
        if (_owner == null || _owner.isDead || _owner.attr == null) return;

        float maxHp = Mathf.Max(1f, _owner.attr.GetAttr(AttrType.MaxHp));
        // 【2026-11-05 主人拍板「boss吸血可以高点 和小怪分开」】Boss 走独立比例 60%，
        // 小怪/精英维持 Def.LeechRatio=30%；Boss 无吸血词缀时本方法不会进来（LeechRatio 早退）。
        float ratio = _owner.IsBossUnit ? MonsterAffixDefs.BossLeechRatio : LeechRatio;
        float heal = finalDamage * ratio;
        float before = _owner.currentHp;
        _owner.currentHp = Mathf.Min(maxHp, before + heal);
        // 【2026-11-05 主人拍板「吸血打一下要看到绿色 +XX」】飘字按理论回血量显示：
        // 旧口径只在实际回血 ≥1 时飘 —— 满血怪回血为 0，而吸血怪多为远程站后排打不到，
        // 全程满血 → 主人实测「从来没见过吸血回血」。改为理论值四舍五入 ≥1 即飘；血仍只加到上限。
        int show = Mathf.RoundToInt(heal);
        if (show >= 1)
            DamageTextSystem.Instance?.SpawnHealText(_owner.GetHitPosition(), show);
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
            var child = planner.SpawnMonsterAt(_owner.config, bm.currentStage != null ? bm.currentStage.stageIndex : 0, p, 0.3f);
            // 【2026-11-05 主人拍板「分裂后的小怪不带词条」】词条只有当前怪有：
            // Boss/精英掷到 Split 时分裂体在 Init 里会重新掷词缀（Boss 体走掷骰路径），
            // 这里统一清空（含染色复位）并隐藏头顶词条，保证分裂体完全无词缀、链式分裂彻底断绝。
            if (child != null)
            {
                var childAffix = child.GetComponent<MonsterAffix>();
                if (childAffix != null) childAffix.ResetState();
                child.HideAffixHeadLabel();
            }
        }
    }

    public bool Has(MonsterAffixId id)
    {
        for (int i = 0; i < _ids.Count; i++)
            if (_ids[i] == id) return true;
        return false;
    }

    /// <summary>
    /// 【2026-10-26 词缀怪】按词缀色给本体精灵上色（白 → 词缀色 40% 混合，浅色不糊美术原画）。
    /// 走 Monster.ApplyAffixTint：同步程序化闪白基准，防止受击闪白把染色洗掉。
    /// 只动 SpriteRenderer.color，不碰贴图/scale/位置（铁律 2）。
    /// </summary>
    public void ApplyTint()
    {
        if (!VisualTinted || _owner == null || _ids.Count == 0) return;
        _owner.ApplyAffixTint(MonsterAffixDefs.Get(_ids[0]).Tint);
    }

    /// <summary>打带跑状态：仅迅捷表定词缀怪攻击后 KITE_SECONDS 秒内为 true（子模型 3 消费）。</summary>
    public bool IsKiting => VisualTinted && Has(MonsterAffixId.Swift) && Time.time < _kiteUntil;

    public void NotifyAttackForKite()
    {
        if (VisualTinted && Has(MonsterAffixId.Swift)) _kiteUntil = Time.time + KITE_SECONDS;
    }

    /// <summary>头顶词条标签用：还有没看过的词条种类就显示；SaveData 为 null 按未看过处理，空 ids 返回 false。</summary>
    public bool HasUnseenAffixKind()
    {
        if (_ids.Count == 0) return false;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        for (int i = 0; i < _ids.Count; i++)
        {
            if (data == null || !data.HasSeenAffixKind(_ids[i])) return true;
        }
        return false;
    }
}
