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
    /// <summary>宠物高度 = 玩家高度的这个比例（11-23 主人「差不多玩家一半大左右，最大不超过玩家」→ 0.45；演进：1.0 → 0.5 → 0.3 → 0.36 → 0.45 → 0.54 → 0.486 → 0.5103）。所有宠物（狗/狼/羊/鸟/蜥蜴及皮肤）共用同一真源，internal 供战场演出狗复用。11-23 22:39「放大20%」：0.45×1.2=0.54；11-23 22:4x「狗小10%」：0.54×0.9=0.486；11-23 23:1x「放大5%」：0.486×1.05=0.5103。</summary>
    internal const float PET_HEIGHT_RATIO = 0.5103f;
    /// <summary>头顶血条离模型顶部的间隙 = 宠物自身世界高度 × 此比例（11-22 按体型调节，不写死世界单位）。</summary>
    const float HP_BAR_HEAD_GAP_RATIO = 0.12f;
    /// <summary>移动动画倍率上下限（11-22：步频跟位移，别再出现「动画慢移动快」滑步）。</summary>
    const float MOVE_ANIM_SPEED_MIN = 0.6f;
    const float MOVE_ANIM_SPEED_MAX = 3f;
    /// <summary>11-22 主人「移动动画放慢一倍」：在按位移算出的步频倍率上再乘此系数，只影响宠物腿部动画频率，不改实际位移。</summary>
    const float PET_MOVE_ANIM_RATE = 0.5f;
    /// <summary>
    /// 宠物移速（11-22）：整体在玩家基准上慢 10%，主人再要求「移动再减 30%」→ 基准 0.9×0.7=0.63；
    /// 再按外貌微调——狗/狼灵活稍快，羊迟缓稍慢。
    /// 皮肤 id 带本体前缀，走同一系数（dog_fox/wolf_demon/goat_mushroom 跟随本体）。
    /// </summary>
    const float PET_MOVE_BASE_RATIO = 0.63f;  // 慢 10% 后再减 30%（0.9 × 0.7）
    const float MOVE_RATE_DOG = 1.05f;        // 狗：灵活，比其它宠物快但仍比原速慢
    const float MOVE_RATE_WOLF = 1.05f;       // 狼：灵活，同上
    const float MOVE_RATE_GOAT = 0.85f;       // 羊：迟缓
    const float MOVE_RATE_DEFAULT = 1f;       // 鸟/蜥蜴等：保持基准

    // 11-22 主人拍板「宠物血条应该是绿的跟玩家一样」：真源 = 玩家血条图
    // Assets/Resources/UI/Art/battle/血条.png 主色 RGB(78,234,57)（敌人血条.png 是红 234,57,78）。
    // 只改本宠物实例的 fillColor，共用 MonsterHealthBar / 怪物血条零影响。
    static readonly Color PET_HP_FILL_COLOR = new Color(78f / 255f, 234f / 255f, 57f / 255f, 1f);

    public string petDefId;          // PetDefs.Def.id（dog / toucan / goat / lizard / wolf + 皮肤 id）
    PetDefs.Def _def;
    bool _fled;                      // 本局已跑掉（防重复触发逃跑）
    bool _cutsceneControlled;        // 演出接管位移（跑入镜头）：继承 AI 早退，速度由演出协程独占

    /// <summary>
    /// 演出接管位移（2026-11-23 主人「狗没有从左边跑进来，还是突然出现的」）：
    /// 生成在画面左缘外 → 演出协程逐帧写水平速度跑进怪群，期间继承 AI 早退（否则 AI 按目标
    /// 改朝向/改速度，跑入会变成原地抽搐）；到位后 SetCutsceneControlled(false) 交还 AI 正常参战。
    /// 跑动期 Moving 动画仍由 CoPetMoveAnimSpeed 按 rb.velocity 自动切 Walk，不另设动画出口。
    /// </summary>
    public void SetCutsceneControlled(bool on)
    {
        _cutsceneControlled = on;
        if (rb != null) rb.velocity = Vector2.zero;
    }

    /// <summary>演出跑入期间写水平速度（交还 AI 前唯一速度出口）。</summary>
    public void SetCutsceneVelocityX(float vx)
    {
        if (rb != null) rb.velocity = new Vector2(vx, rb.velocity.y);
    }

    /// <summary>本局是否已血空跑掉（演出等「狗被打跑」的只读出口，2026-11-23 狗营救分镜）。</summary>
    public bool IsFled => _fled;

    /// <summary>剧情驱动：立刻走「血空退场」同一出口（血空自然触发之外的唯一补口，共用 CoFleeAway）。</summary>
    public void FleeNow()
    {
        if (_fled || isDead) return;
        Die();
    }
    MonsterHealthBar _hpBar;
    // 移动动画状态：null=待重切（攻击/受击打断后），true=Walk，false=Idle。
    bool? _petWalkState;
    float _actionAnimHoldUntil;      // 攻击/受击 clip 播完前不切回 Walk/Idle，防动作被盖
    int _hitFlashGen;                // 受击闪白代次：连续受击时旧的还原协程让位给新的，防提前还原

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
        else
        {
            _hpBar.fillColor = PET_HP_FILL_COLOR;   // 玩家同款绿（LateUpdate/受伤闪烁都从该字段回写）
            _hpBar.ResetBar();                      // 立刻上色，避免首帧还是怪物红
            PlaceHpBarAboveHead();                  // 2026-11-22：默认脚底锚（footDrop=-0.05）改到头顶，只动本宠物血条
        }

        // 11-22：脚下阴影改成怪物同款黑半透（宠物 prefab 自带白椭圆影子，浅色地图看不见）
        EnsureBattleShadow();

        Debug.Log($"[Pet] {defId} 出场 hp={currentHp:0} atk={attr.GetAttr(AttrType.Attack):0} " +
                  $"scale={transform.localScale.x:F2} pos={transform.position}");

        // 专属技能循环（Pet.Skills.cs）：皮肤跟随本体同一套技能
        StartSkillLoop();

        // 11-22：移动动画倍率跟实际位移走，消除「动画慢移动快」滑步。
        // 只写 unitAnim 的 public 字段，借 UnitAnimation 每帧现成通道生效，不新增第二套 anim.speed 写入。
        StartCoroutine(CoPetMoveAnimSpeed());
    }

    /// <summary>
    /// 每帧按 |水平速度|/移速基准 调 moveAnimSpeedScale；无位移时回 1（UnitAnimation 站立自动回原速）。
    /// 受击/攻击锁期间不覆盖（那两段由 UnitAnimation 用专用倍率接管）。
    /// 11-22 补：同时按状态名直切 Walk/Idle —— 宠物 controller 无参数，unitAnim.SetMove 里的
    /// SetBool 是空操作，移动动画状态从没切过，身体在动、动画站着 = 主人报的「滑步」。
    /// </summary>
    IEnumerator CoPetMoveAnimSpeed()
    {
        while (!_fled && !isDead && unitAnim != null)
        {
            yield return null;
            if (_fled || isDead || unitAnim == null) yield break;
            if (unitAnim.InDamagedRecovery() || unitAnim.InAttackLock) continue;

            float vx = rb != null ? Mathf.Abs(rb.velocity.x) : 0f;
            float refSpeed = Mathf.Max(0.01f, attr.GetAttr(AttrType.MoveSpeed));
            bool moving = vx >= refSpeed * 0.05f;
            // 接近静止时回 1（原速），只有真的在移动才按比例调速——避免站立态被压到下限倍率。
            float baseRate = Mathf.Clamp(vx / refSpeed, MOVE_ANIM_SPEED_MIN, MOVE_ANIM_SPEED_MAX);
            unitAnim.moveAnimSpeedScale = moving ? baseRate * PET_MOVE_ANIM_RATE : 1f;

            // 状态切换（只在变化那一帧切，不每帧重播）：攻击/受击动画保持期内跳过，别把动作盖成走路。
            if (_petWalkState != moving && Time.time >= _actionAnimHoldUntil)
            {
                _petWalkState = moving;
                PlayAnim(moving ? "Walk" : "Idle");
            }
        }
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
        attr.SetBaseAndCurrent(AttrType.MoveSpeed, GameConfig.BASE_MOVE_SPEED * PET_MOVE_BASE_RATIO * GetMoveRateBySpecies(def));
        // 2026-11-23 主人「宠物攻击距离还是远，要和近战一样」：射程全统一近战剑距(0.96)，远程宠不再放风筝。
        // 11-23 二次修：远程/法术宠走 Orb 弹丸(UsesMeleeBasicAttack=false)，吃不到 GetEffectiveAttackRange 里我方近战的
        // ×0.9 贴身系数，实际 0.96+0.12 Slack 比真近战远一截。这里给远程/法术宠配置射程预乘 0.9，
        // 使全部宠物有效射程统一到 0.864(=0.96×0.9)，与玩家/近战佣兵一致，弹丸外观(子弹样)保留不变。
        float petRange = GameConfig.RangeSword * (IsMagicPet(def) || IsRangedPet(def) ? 0.9f : 1f);
        attr.SetBaseAndCurrent(AttrType.AttackRange, petRange);
        attr.SetBaseAndCurrent(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);
        currentHp = attr.GetAttr(AttrType.MaxHp);
    }

    /// <summary>角蜥/火蜥蜴走法球（火球术），其余走物理。</summary>
    static bool IsMagicPet(PetDefs.Def def)
        => def != null && def.id.StartsWith("lizard");

    /// <summary>巨嘴鸟/鹦鹉、恶魔狼（换皮后远程）——射程已统一近战，此处仅标识弹丸外观；其余近战刀光。</summary>
    static bool IsRangedPet(PetDefs.Def def)
        => def != null && (def.id.StartsWith("toucan") || def.id == "wolf_demon");

    /// <summary>
    /// 11-22 主人「宠物总是一边走一边攻击，没有设定攻击范围吗」：
    /// 宠物继承佣兵的前压停步线（射程×0.55），进了射程还往前走到 55% 才站桩，看着就是边走边打。
    /// 宠物 override 成 1f —— 一进有效射程即站桩出手，不再前压（射程/伤害/CD/索敌口径全不动）。
    /// 只影响宠物，佣兵仍走基类默认 0.55。
    /// </summary>
    protected override float EngageDistRatio => 1f;

    /// <summary>
    /// 11-22 主人反复「移动还是快」的根因修复：宠物虽继承 Mercenary，但不是佣兵表的单位，
    /// 基类 GetCombatMoveSpeed 里 isAlly && this is Mercenary 会把移速强换成玩家 Hero 的移速
    /// （佣兵为「跟得上玩家」设计的口径），再叠掉队/冲刺/连杀倍率 —— 导致 SetupPetAttributes 里
    /// 反复下调的 attr.MoveSpeed 根本没生效。宠物回归自身 attr.MoveSpeed 单一真源，只影响宠物，
    /// 佣兵/玩家/怪物取速逻辑零改动。
    /// </summary>
    protected override float GetCombatMoveSpeed()
        => attr != null ? attr.GetAttr(AttrType.MoveSpeed) : base.GetCombatMoveSpeed();

    /// <summary>按外貌给移速系数：狗/狼灵活稍快、羊迟缓稍慢，其余保持基准。皮肤前缀跟随本体。</summary>
    static float GetMoveRateBySpecies(PetDefs.Def def)
    {
        if (def == null) return MOVE_RATE_DEFAULT;
        string id = def.id;
        if (id.StartsWith("dog")) return MOVE_RATE_DOG;
        if (id.StartsWith("wolf")) return MOVE_RATE_WOLF;
        if (id.StartsWith("goat")) return MOVE_RATE_GOAT;
        return MOVE_RATE_DEFAULT;
    }

    /// <summary>攻击套：法术宠→法球，远程宠→法球弹丸(子弹样)，其余→近战刀光（2026-11-23 远程弓箭换弹丸）。</summary>
    protected override AttackVfxKit GetAttackVfxKit()
    {
        if (_def == null) return AttackVfxKit.MeleeSlash;
        if (IsMagicPet(_def)) return AttackVfxKit.Orb;
        // 2026-11-23 主人「远程找个子弹样子的」：Orb=圆球弹丸(VFX/Shared/Ally/Orb)，Bow=宽扁箭矢。
        if (IsRangedPet(_def)) return AttackVfxKit.Orb;
        return AttackVfxKit.MeleeSlash;
    }

    /// <summary>按玩家实际高度折半反推缩放。玩家 bounds 取不到时退回 0.5 倍 scale（不静默）。</summary>
    void ApplyHalfHeight()
    {
        float targetH = 0f;
        var hero = Hero.Instance;
        if (hero != null && MeasureBodyHeight(hero, out float heroH) && heroH > 0.01f)
            targetH = heroH * Mathf.Min(PET_HEIGHT_RATIO, 1f);   // 上限=玩家身高（11-23 主人：最大不要超过玩家）

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
            float s = hs * Mathf.Min(PET_HEIGHT_RATIO, 1f);   // 同上限：不超玩家
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
            // 2026-11-23 主人「战斗宠物别缩太小、还是玩家一半大」：prefab 的 Attack/Hit/Hit_2/Dead
            // 动画帧节点 m_IsActive=0 但 SpriteRenderer.enabled=1 且尺寸远大于躯干(Hit 高 2.01>
            // Body 1.27)，旧版把它们算进 bounds → myH 虚高 → 反推 s 偏小。只给宠物排除 inactive 帧；
            // 玩家(Hero)量法一字不改，targetH 口径不变。
            if (unit is Pet && !r.gameObject.activeInHierarchy) continue;
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

    /// <summary>
    /// 脚下阴影改成怪物同款黑半透（2026-11-22）。
    /// 真根因：宠物 prefab 自带的 Shadow.png 全物种 max alpha 只有 26/255（≈10% 淡灰影），
    /// 旧版再乘 color.a=0.35 ⇒ 实际只有 3.5%，肉眼等于没有。怪物用的是满 alpha 白圆 × 黑 0.35。
    /// 修法：只把运行时实例的 sprite 换成满 alpha 实心软边圆（与 Monster.MakeCircleSprite 同源算法），
    /// 按原影子世界尺寸还原椭圆，再染黑 0.35；不动 prefab / meta / 节点层级 / 排序。
    /// 缺 Shadow 节点只告警不新建（项目红线：不静默造资源）。
    /// </summary>
    /// <summary>阴影可见尺寸放大倍数（随 PET_HEIGHT_RATIO 同比：6→3.6→4.32→5.4，11-23 主人「宠物放大到玩家一半」阴影同比）：实心圆软边让可见核心比标称小。</summary>
    const float BATTLE_SHADOW_SIZE_SCALE = 5.4f;  // 11-23 主人「宠物放大到玩家一半，阴影同比」：4.32→5.4
    /// <summary>阴影纵向压扁系数（11-22 主人「压扁一些」）：横向放大同时收高度，更像贴地椭圆。</summary>
    const float BATTLE_SHADOW_FLATTEN = 0.6f;

    void EnsureBattleShadow()
    {
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        bool found = false;
        for (int i = 0; i < srs.Length; i++)
        {
            var r = srs[i];
            if (r == null) continue;
            if (r.name != "Shadow" && r.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            found = true;

            // 换图前先量原影子的世界尺寸（从 prefab 现读、不写死），换满 alpha 圆后按它放大还原。
            Vector2 origSize = (r.sprite != null ? (Vector2)r.bounds.size : new Vector2(0.9f, 0.3f)) * BATTLE_SHADOW_SIZE_SCALE;
            var solid = GetSolidShadowSprite();
            r.sprite = solid;
            float native = solid.bounds.size.x > 0.001f ? solid.bounds.size.x : 1f;
            float parentX = Mathf.Abs(r.transform.lossyScale.x); if (parentX < 0.001f) parentX = 1f;
            float parentY = Mathf.Abs(r.transform.lossyScale.y); if (parentY < 0.001f) parentY = 1f;
            r.transform.localScale = new Vector3(origSize.x / (native * parentX), origSize.y * BATTLE_SHADOW_FLATTEN / (native * parentY), 1f);

            r.color = new Color(0f, 0f, 0f, 0.35f);   // 与 Monster.Visual.EnsureFootShadow 同口径
            r.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            r.sortingOrder = -20;                      // 身体部件 order 更高，影子压在最底
        }
        if (!found)
            Debug.LogWarning("[Pet] " + petDefId + " prefab 无 Shadow 节点，脚下阴影未生效（不改 prefab、不新建）");
    }

    static Sprite _solidShadowSprite;

    /// <summary>程序化实心软边圆阴影：与 Monster.Skills.MakeCircleSprite 同源，满 alpha 白圆，染黑后清晰可见。</summary>
    static Sprite GetSolidShadowSprite()
    {
        if (_solidShadowSprite != null) return _solidShadowSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = (x - c) / c, dy = (y - c) / c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = d <= 1f ? Mathf.Clamp01(1.15f - d) : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _solidShadowSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return _solidShadowSprite;
    }

    /// <summary>
    /// 血条改挂头顶（2026-11-22）：MonsterHealthBar 默认脚底锚（footDropWorld=-0.05），
    /// 这里按宠物自身 SpriteRenderer 世界 bounds 顶部重定位；只动本宠物血条，不改共用组件。
    /// </summary>
    void PlaceHpBarAboveHead()
    {
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        Bounds b = new Bounds();
        bool has = false;
        for (int i = 0; i < srs.Length; i++)
        {
            var r = srs[i];
            if (r == null || r.sprite == null || !r.enabled) continue;
            // 2026-11-23：与 MeasureBodyHeight 同口径，宠物排除 inactive 动画帧，血条贴可见躯干顶部
            if (!r.gameObject.activeInHierarchy) continue;
            if (r.name == "Shadow" || r.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has)
        {
            Debug.LogWarning("[Pet] " + petDefId + " 头顶血条定位取不到 SpriteRenderer bounds，沿用默认脚底位");
            return;
        }

        // 血条挂在 unit.transform 下，Create 内 ApplyCompensatedScale 已把 localScale 抵消父级 lossyScale；
        // 故 localPosition.y 要除以父级 lossyScale.y，才落到「模型顶部 + 间隙」的世界高度。
        float parentY = Mathf.Abs(transform.lossyScale.y);
        if (parentY < 0.01f) parentY = 1f;
        float topWorld = b.max.y - transform.position.y;
        // 11-22：间隙不再写死世界单位，按宠物自身世界高度取比例——高体型离头顶远、矮体型贴近。
        float bodyH = Mathf.Max(0.01f, b.size.y);
        float headGap = bodyH * HP_BAR_HEAD_GAP_RATIO;
        _hpBar.transform.localPosition = new Vector3(0f, (topWorld + headGap) / parentY, 0f);
    }

    /// <summary>血条宽度随宠物体型收窄（基类默认 0.55 对半高宠物偏宽）。</summary>
    public override float GetHpBarWorldWidth()
    {
        float h = 0f;
        if (TryGetBodyHeight(out h)) return Mathf.Max(0.2f, h * 1.1f);
        return 0.3f;
    }

    /// <summary>
    /// 【2026-11-23 主人「狗脸冲右往左跑出去」】逃跑期间停掉继承来的佣兵 AI：
    /// 宠物血空只置 _fled（非 isDead），基类 Update 的 isDead 闸不生效，Mercenary.AIUpdate 每帧
    /// 按目标把脸转回右、把速度交还 AI → 与 CoFleeAway 的「朝左+左速度」打架，看起来是倒着跑。
    /// 这里 fled 后直接早退，让逃跑朝向/位移只由 CoFleeAway 独占；正常战斗仍走 base。
    /// 【2026-11-23 演出跑入】同理：_cutsceneControlled 期间也早退，位移/朝向由演出协程独占。
    /// </summary>
    protected override void AIUpdate()
    {
        if (_fled || _cutsceneControlled) return;
        base.AIUpdate();
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
        // 【2026-11-23 主人「狗往左跑到一半就消失，没有跑出屏」】原来固定跑 1.6s 就 Destroy：
        // 狗是在怪群中间（画面中部）血空的，1.6s 只够跑几单位，停在屏幕内就没了，看着像半路蒸发。
        // 改成按画面左缘【外】判定——真跑出屏幕才退场；移速不变，6s 上限只兜「相机跟着走永远跑不出去」。
        while (elapsed < 6f)
        {
            if (rb != null)
                rb.velocity = new Vector2(-speed, rb.velocity.y);
            elapsed += Time.deltaTime;
            BattleManager.GetBattleVisibleX(out float visMin, out _);
            // GetBattleVisibleX 的 minX 已含 0.7 pad → 再减 1.5 才是「整只狗都出了视口左缘」。
            if (transform.position.x <= visMin - 1.5f) break;
            yield return null;
        }
        Debug.Log("[Pet] " + petDefId + " 血空跑出屏幕，退场（不死不结算）");
        Destroy(gameObject);
    }

    /// <summary>队伍进场/停走同步：宠物 controller 无参数，按状态名切 Walk/Idle。</summary>
    public void SetPartyWalking(bool walking)
    {
        if (_fled) return;   // 逃跑退场期间不覆盖 Walk 动画
        PlayAnim(walking ? "Walk" : "Idle");
    }

    /// <summary>按状态名直接播宠物 prefab 自带动画（controller 无参数，不能 SetTrigger）。</summary>
    void PlayAnim(string state)
    {
        if (anim == null) return;
        string species = SpeciesPrefix();
        if (string.IsNullOrEmpty(species)) return;
        string clip = species + "_" + state;
        if (anim.HasState(0, Animator.StringToHash(clip)))
        {
            anim.Play(clip, 0, 0f);
            // 攻击/受击：记下 clip 时长，这段时间内移动协程不许切回 Walk/Idle（否则动作被盖）。
            // 时长从 Animator 现读，不写死；_petWalkState=null 让动作播完后强制重切一次。
            if (state == "Attack" || state == "Hit")
            {
                _petWalkState = null;
                float len = FindClipLength(clip);
                _actionAnimHoldUntil = Time.time + len;
            }
        }
    }

    /// <summary>从 controller 的 clip 里按名字现读时长（Play 同帧 stateInfo 还是旧状态，不能用它）。</summary>
    float FindClipLength(string clipName)
    {
        var ctrl = anim != null ? anim.runtimeAnimatorController : null;
        if (ctrl != null)
        {
            var clips = ctrl.animationClips;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && clips[i].name == clipName && clips[i].length > 0f)
                    return clips[i].length;
        }
        return 0.3f;   // 读不到时的保守值，只影响动作后多久恢复走路
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

    /// <summary>
    /// 11-22 主人「不要受击动画，只用闪白」：宠物 prefab 自带 Animator（非程序化模式、无 SPUM），
    /// unitAnim.PlayDamaged(false) 的两条闪白分支都不成立 → 改在这里直接闪白自身身体 SpriteRenderer，
    /// 不播 Hit clip。只染身体（排除 Shadow），现读基准色、按原 alpha 白闪后还原，不动共用 UnitAnimation。
    /// </summary>
    protected override void PlayHitReaction()
    {
        StartCoroutine(CoPetHitFlash());
    }

    IEnumerator CoPetHitFlash()
    {
        int gen = ++_hitFlashGen;
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        var baseColors = new Color[srs.Length];
        bool any = false;
        for (int i = 0; i < srs.Length; i++)
        {
            var r = srs[i];
            baseColors[i] = r != null ? r.color : Color.white;
            if (r == null || !r.enabled || r.sprite == null) continue;
            if (r.name == "Shadow" || r.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            r.color = new Color(1f, 1f, 1f, baseColors[i].a);  // 白闪，保留原 alpha
            any = true;
        }
        if (!any) yield break;

        yield return new WaitForSeconds(0.1f);
        if (gen != _hitFlashGen) yield break;   // 被后续受击接管，颜色交给新协程还原

        for (int i = 0; i < srs.Length; i++)
        {
            var r = srs[i];
            if (r == null) continue;
            if (r.name == "Shadow" || r.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            r.color = baseColors[i];
        }
    }

    void OnDestroy()
    {
        if (_hpBar != null) Destroy(_hpBar.gameObject);
    }
}
