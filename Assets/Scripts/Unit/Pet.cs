using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗宠物单位（2026-11-21）：上阵宠物进正式 battle 的实体。
///
/// 继承 Mercenary 白嫖已验证的「跟队 / 索敌 / 前压 / 攻击 / 受击 / 硬直」全套 AI，
/// 但<b>不复用 Mercenary.Init</b>（那条会去 MercRosterDefs 查佣兵表，宠物 id 不在表里 →
/// 吃到兜底属性，还会 ApplyLegendaryFx / 名字条等一堆佣兵专属逻辑）。这里走独立 InitPet：
/// 属性直接来自 PetDefs.Def，血条走 MonsterHealthBar.Create，缩放按 bounds 反推到「玩家一半高」。
///
/// 死亡口径（规格第五节 / 用户 11-21）：宠物<b>不死、不复活</b>。血空 = 掉头跑出屏幕后销毁，
/// 不进 UnitBase.Die（不播死亡动画、不触发 OnDead 结算），只发一条退场提示。
///
/// 动画：宠物 prefab 自带 Animator + controller（Idle/Walk/Attack/Hit/Dead 五状态、无参数无 Transition），
/// 用 animator.Play("Animal_Attack") 这类状态名直接切，不走 UnitAnimation 的参数驱动。
/// </summary>
public partial class Pet : Mercenary
{
    /// <summary>宠物高度 = 玩家高度的这个比例（用户 11-21「战斗中宠物大概是玩家一半的高度」）。</summary>
    const float PET_HEIGHT_RATIO = 0.5f;

    public string petDefId;          // PetDefs.Def.id（dog / toucan / goat / lizard / wolf + 皮肤 id）
    PetDefs.Def _def;
    bool _fled;                      // 本局已跑掉（防重复触发逃跑）
    MonsterHealthBar _hpBar;

    protected override void Awake()
    {
        // 与 Mercenary 同偏移口径，但宠物体型小，受击/发射点按宠物 bounds 在 InitPet 里重设。
        base.Awake();
        isAlly = true;
    }

    /// <summary>
    /// 宠物专用初始化。调用方（PetSpawner）负责先实例化 UI/Pets 下 prefab 并挂本组件。
    /// </summary>
    public void InitPet(string defId)
    {
        var def = PetDefs.Get(defId);
        if (def == null)
        {
            Debug.LogError("[Pet] 宠物 id 不在 PetDefs 真源: " + defId + "（不静默兜底）");
            return;
        }
        _def = def;
        petDefId = defId;
        gameObject.name = "Pet_" + defId;

        ResetForReuse();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        // 订阅出手事件播攻击动作（OnAttack 是 Action 字段，不是虚方法，只能 +=）
        OnAttack -= OnPetAttack;
        OnAttack += OnPetAttack;

        SetupPetAttributes(def);

        // 半高：读 prefab 自身 bounds 反推 localScale，不写死数字（prefab 根现为 1）
        ApplyHalfHeight();

        // 受击点/发射点按宠物实际包围盒落位（不沿用佣兵写死偏移）
        ConfigureHitFirePoints();

        Face(1);
        _hpBar = MonsterHealthBar.Create(this);
        if (_hpBar == null)
            Debug.LogWarning("[Pet] " + defId + " 头顶血条创建失败（MonsterHealthBar.Create 返回 null）");

        Debug.Log($"[Pet] {defId} 出场 hp={currentHp:0} atk={attr.GetAttr(AttrType.Attack):0} " +
                  $"scale={transform.localScale.x:F2} pos={transform.position}");

        // 专属技能循环（Pet.Skills.cs）：皮肤跟随本体同一套技能
        StartSkillLoop();
    }

    void SetupPetAttributes(PetDefs.Def def)
    {
        attr.ResetToBase();
        // 宠物数值真源 = PetDefs（策划草稿，基准≈玩家 1/10；不进佣兵表、不吃局外加成）
        attr.SetBaseAndCurrent(AttrType.MaxHp, Mathf.Max(1f, def.hp));
        attr.SetBaseAndCurrent(AttrType.Attack, Mathf.Max(1f, def.atk));
        attr.SetBaseAndCurrent(AttrType.Defense, Mathf.Max(0f, def.def));
        attr.SetBaseAndCurrent(AttrType.MagicDefense, Mathf.Max(0f, def.def));
        bool magic = IsMagicPet(def);
        attr.SetBaseAndCurrent(AttrType.MagicAttack, magic ? Mathf.Max(1f, def.atk) : 0f);
        attr.SetBaseAndCurrent(AttrType.AttackSpeed, GameConfig.BASE_ATTACK_SPEED);
        attr.SetBaseAndCurrent(AttrType.MoveSpeed, GameConfig.BASE_MOVE_SPEED);
        attr.SetBaseAndCurrent(AttrType.AttackRange,
            IsRangedPet(def) ? GameConfig.RangeBow * 0.75f : GameConfig.RangeSword);
        attr.SetBaseAndCurrent(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);
        currentHp = attr.GetAttr(AttrType.MaxHp);
    }

    /// <summary>角蜥/火蜥蜴走法球（火球术），其余走物理。</summary>
    static bool IsMagicPet(PetDefs.Def def)
        => def != null && def.id.StartsWith("lizard");

    /// <summary>巨嘴鸟/鹦鹉、恶魔狼（换皮后远程）走弓；其余近战。</summary>
    static bool IsRangedPet(PetDefs.Def def)
        => def != null && (def.id.StartsWith("toucan") || def.id == "wolf_demon");

    /// <summary>技能套：法术宠→法球，远程宠→弓，其余→近战刀光（与 IsMagicPet/IsRangedPet 同源）。</summary>
    protected override AttackVfxKit GetAttackVfxKit()
    {
        if (_def == null) return AttackVfxKit.MeleeSlash;
        if (IsMagicPet(_def)) return AttackVfxKit.Orb;
        if (IsRangedPet(_def)) return AttackVfxKit.Bow;
        return AttackVfxKit.MeleeSlash;
    }

    /// <summary>按玩家实际高度折半反推缩放。玩家 bounds 取不到时退回 0.5 倍 scale（不静默）。</summary>
    void ApplyHalfHeight()
    {
        float targetH = 0f;
        var hero = Hero.Instance;
        if (hero != null && MeasureBodyHeight(hero, out float heroH) && heroH > 0.01f)
            targetH = heroH * PET_HEIGHT_RATIO;

        if (TryGetBodyHeight(out float myH) && myH > 0.01f && targetH > 0.01f)
        {
            float s = Mathf.Clamp(targetH / myH, 0.05f, 3f);
            transform.localScale = new Vector3(s, s, s);
        }
        else
        {
            // 取不到 bounds（prefab 刚实例化一帧内 SpriteRenderer 未就绪）：退回玩家 scale 的一半
            float hs = hero != null ? Mathf.Abs(hero.transform.localScale.x) : 1f;
            if (hs < 0.0001f) hs = 1f;
            float s = hs * PET_HEIGHT_RATIO;
            transform.localScale = new Vector3(s, s, s);
            Debug.LogWarning("[Pet] bounds 取不到，半高退回 scale=" + s.ToString("F2"));
        }
    }

    /// <summary>合并所有躯干 SpriteRenderer（排除影子）的世界高度。</summary>
    bool TryGetBodyHeight(out float height) => MeasureBodyHeight(this, out height);

    /// <summary>量任一战斗单位的世界高度（排除影子/血条节点），静态供玩家与宠物同源比较。</summary>
    static bool MeasureBodyHeight(UnitBase unit, out float height)
    {
        height = 0f;
        if (unit == null) return false;
        var srs = unit.GetComponentsInChildren<SpriteRenderer>(true);
        Bounds b = new Bounds();
        bool has = false;
        for (int i = 0; i < srs.Length; i++)
        {
            var r = srs[i];
            if (r == null || r.sprite == null || !r.enabled) continue;
            if (r.name == "Shadow" || r.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has) return false;
        height = b.size.y;
        return height > 0.0001f;
    }

    void ConfigureHitFirePoints()
    {
        if (TryGetBodyHeight(out float h))
        {
            hitPointOffset = new Vector3(0f, h * 0.5f, 0f);
            firePointOffset = new Vector3(0.2f, h * 0.5f, 0f);
            // 重建挂点：清空旧的再让基类创建（EnsureFirePoint/HitPoint 只在 null 时建）
            RebuildPoint(ref hitPoint, "HitPoint", hitPointOffset);
            RebuildPoint(ref firePoint, "FirePoint", firePointOffset);
        }
    }

    void RebuildPoint(ref Transform point, string name, Vector3 offset)
    {
        if (point != null) Destroy(point.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        point = go.transform;
    }

    /// <summary>血条宽度随宠物体型收窄（基类默认 0.55 对半高宠物偏宽）。</summary>
    public override float GetHpBarWorldWidth()
    {
        float h = 0f;
        if (TryGetBodyHeight(out h)) return Mathf.Max(0.2f, h * 1.1f);
        return 0.3f;
    }

    /// <summary>
    /// 血空不走死亡：掉头跑出屏幕 → 销毁。不播死亡动画、不触发 OnDead（不结算、不给奖励）。
    /// </summary>
    protected override void Die(bool isCritKill = false)
    {
        if (_fled || _isDying) return;
        _fled = true;
        if (rb != null) rb.velocity = Vector2.zero;
        if (_hpBar != null) _hpBar.gameObject.SetActive(false);

        var bm = BattleManager.Instance;
        if (bm != null) bm.allyUnits.Remove(this);

        PlayAnim("Walk");
        if (unitAnim != null) unitAnim.enabled = false;   // 防止基类动画覆盖行走态
        StartCoroutine(CoFleeAway());
    }

    IEnumerator CoFleeAway()
    {
        // 掉头（朝屏幕左侧跑出），与怪的退场方向一致
        facingDir = -1;
        ApplyFacing(facingDir);

        float speed = GameConfig.BASE_MOVE_SPEED * 2.2f;
        float elapsed = 0f;
        while (elapsed < 1.6f)
        {
            if (rb != null)
                rb.velocity = new Vector2(-speed, rb.velocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Debug.Log("[Pet] " + petDefId + " 血空跑出屏幕，退场（不死不结算）");
        Destroy(gameObject);
    }

    /// <summary>按状态名直接播宠物 prefab 自带动画（controller 无参数，不能 SetTrigger）。</summary>
    void PlayAnim(string state)
    {
        if (anim == null) return;
        string species = SpeciesPrefix();
        if (string.IsNullOrEmpty(species)) return;
        string clip = species + "_" + state;
        if (anim.HasState(0, Animator.StringToHash(clip)))
            anim.Play(clip, 0, 0f);
    }

    string SpeciesPrefix()
    {
        if (_def == null || string.IsNullOrEmpty(_def.prefabPath)) return null;
        // prefabPath = "Animal/Animal_Dog" → 取目录段 "Animal"
        int slash = _def.prefabPath.IndexOf('/');
        return slash > 0 ? _def.prefabPath.Substring(0, slash) : null;
    }

    /// <summary>出手播攻击动作（OnAttack 事件订阅回调）。</summary>
    void OnPetAttack(UnitBase target, float damage, bool isCrit)
    {
        PlayAnim("Attack");
    }

    protected override void PlayHitReaction()
    {
        PlayAnim("Hit");
    }

    void OnDestroy()
    {
        if (_hpBar != null) Destroy(_hpBar.gameObject);
    }
}
