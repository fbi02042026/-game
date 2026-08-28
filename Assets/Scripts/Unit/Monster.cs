using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 怪物类：使用用户制作的 Monstersmoban 预制体
/// 预制体结构：
///   Monstersmoban (RectTransform + Animator)
///   ├── Monsters (SpriteRenderer, scale=100) — 怪物图片
///   ├── HPBar (scale=15, y=-2.2) — 血条根
///   │   ├── HPBarBG (SpriteRenderer) — 血条背景
///   │   └── HPBarFill (SpriteRenderer) — 血条填充
///   ├── beattack (空节点, y=6.6) — 受击点
///   └── fire (空节点, x=-6.6, y=6.6) — 发射点
///
/// 缩放：根节点scale控制整体大小
///   普通 = MONSTER_BASE_SCALE × spriteScale × 1.0
///   精英 = MONSTER_BASE_SCALE × spriteScale × 1.5
///   Boss  = MONSTER_BASE_SCALE × spriteScale × 2.0
/// </summary>
public class Monster : UnitBase
{
    public float goldDrop;
    public int expDrop;
    public MonsterConfig config;
    private int _chapter;

    // 用户预制体中的 SpriteRenderer 血条
    private SpriteRenderer _hpBarFill;
    private SpriteRenderer _hpBarBg;
    private Transform _hpBarRoot;
    static int s_hpBarFrontBoost;

    protected override void Awake()
    {
        // 偏移仅作兜底；怪物根节点常 ×4+，本地 0.5 会变成世界两米多，按世界高度反推本地
        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        hitPointOffset = new Vector3(0f, 0.55f / rootAbs, 0f);
        firePointOffset = new Vector3(-0.08f / rootAbs, 0.55f / rootAbs, 0f);

        // 怪物使用程序化动画，根节点上的 Animator 没有所需参数会报错，直接移除
        Animator animator = GetComponent<Animator>();
        if (animator != null) Destroy(animator);

        base.Awake();
        if (unitAnim != null)
            unitAnim.ForceProceduralMode(sr);
        isAlly = false;
        // 2D Pixel RPG Monster Pack 精灵默认朝左
        spriteDefaultFacesRight = false;

        // 用户预制体没有 Rigidbody2D，需要添加
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }

        // SpriteRenderer 在 "Monsters" 子节点上
        if (sr == null)
        {
            Transform monstersChild = transform.Find("Monsters");
            if (monstersChild != null)
                sr = monstersChild.GetComponent<SpriteRenderer>();
        }

        // 查找用户预制体中的 SpriteRenderer 血条
        FindHPBar();
    }

    /// <summary>查找预制体中的 HPBar 节点（Init 时也会再查一次）</summary>
    void FindHPBar()
    {
        _hpBarRoot = transform.Find("HPBar");
        if (_hpBarRoot == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "HPBar") { _hpBarRoot = child; break; }
            }
        }
        if (_hpBarRoot == null) return;

        Transform fill = _hpBarRoot.Find("HPBarFill");
        if (fill != null)
            _hpBarFill = fill.GetComponent<SpriteRenderer>();

        Transform bg = _hpBarRoot.Find("HPBarBG") ?? _hpBarRoot.Find("HPBarBg");
        if (bg != null)
        {
            var bgSr = bg.GetComponent<SpriteRenderer>();
            if (bgSr != null)
            {
                bgSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                bgSr.sortingOrder = GameConfig.SORT_UNIT;
            }
        }
    }

    void NormalizeHPBarLayout(float monsterRootScale)
    {
        if (_hpBarRoot == null) return;

        float spriteWidth = 0.32f;
        if (sr != null && sr.sprite != null)
            spriteWidth = sr.sprite.bounds.size.x;

        // 血条世界宽度 ≈ 怪物体宽 85%，再换算到根节点本地 scale
        float monsterWorldWidth = spriteWidth * monsterRootScale;
        float barSpriteWidth = 1.01f;
        if (_hpBarFill != null && _hpBarFill.sprite != null)
            barSpriteWidth = Mathf.Max(0.01f, _hpBarFill.sprite.bounds.size.x);
        float barLocalScale = (monsterWorldWidth * 0.85f) / (barSpriteWidth * Mathf.Max(0.5f, monsterRootScale));
        barLocalScale = Mathf.Clamp(barLocalScale, 0.08f, 2f);
        _hpBarRoot.localScale = Vector3.one * barLocalScale;

        ApplyAnchorPosition(_hpBarRoot, 0f, GameConfig.MONSTER_HP_BAR_FOOT_LOCAL_Y);

        if (_hpBarFill != null)
        {
            _hpBarFillBaseWidth = _hpBarFill.sprite != null
                ? _hpBarFill.sprite.bounds.size.x
                : 1f;
            var fillT = _hpBarFill.transform;
            fillT.localScale = Vector3.one;
            fillT.localPosition = Vector3.zero;
        }
    }

    /// <summary>受击/发射点：按精灵不透明像素中心。须在 LoadSprite 之后调用。</summary>
    void NormalizeMonsterAnchorNodes()
    {
        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float localY = 0.55f / rootAbs;
        float localFireX = -0.12f / rootAbs;
        ApplyAnchorPosition(transform.Find("beattack"), 0f, localY);
        ApplyAnchorPosition(transform.Find("fire"), localFireX, localY);
        Transform be = transform.Find("beattack");
        if (be != null)
        {
            hitPoint = be;
            // 精灵已加载：先同步摆一次不透明中心，再协程两帧后确认
            TryPlaceHitPointByOpaqueSprite(be);
            StartCoroutine(CalcHitPointCenter(be));
        }
        Transform fire = transform.Find("fire");
        if (fire != null)
        {
            firePoint = fire;
            StartCoroutine(CalcFirePointCenter(fire));
        }
    }

    System.Collections.IEnumerator CalcFirePointCenter(Transform fireTransform)
    {
        yield return null;
        yield return null;
        if (fireTransform == null) yield break;

        // 与受击点同高、水平挪到躯干边缘。缩放过的预制体不能在局部空间算。
        if (!TryGetBodyBounds(out Bounds body))
            yield break;

        Vector3 center = hitPoint != null ? hitPoint.position : GetBodyCenterWorld(body);
        float side = Mathf.Max(0.12f, body.size.x * 0.35f);
        bool toLeft = fireTransform.localPosition.x < 0f;
        fireTransform.position = new Vector3(center.x + (toLeft ? -side : side), center.y, transform.position.z);
        Vector3 lp = fireTransform.localPosition;
        fireTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    static void ApplyAnchorPosition(Transform t, float x, float y)
    {
        if (t == null) return;
        if (t is RectTransform rt)
            rt.anchoredPosition3D = new Vector3(x, y, 0f);
        else
            t.localPosition = new Vector3(x, y, 0f);
    }

    private float _hpBarFillBaseWidth = 1f;
    private string _skillId;
    private bool _canUseActiveSkill;
    private float _skillEnergy = 0f;
    private float _skillCooldown = 0f;
    private MonsterAttackStyle _attackStyle = MonsterAttackStyle.Melee;
    private MonsterAttackStyle _swingStyle = MonsterAttackStyle.Melee;
    private int _spriteIndex;
    private bool _isBossUnit;
    private bool _eliteWave;
    private int _bossSwingIndex;
    private bool _isEnteringMap;
    private Vector3 _enterTargetPos;
    private float _enterSpeed = 1.6f;

    /// <summary>从屏外缓入到交战点，进场期间不攻击</summary>
    public void BeginMapEnter(Vector3 engagePos, float speed)
    {
        _enterTargetPos = engagePos;
        _enterSpeed = Mathf.Max(0.4f, speed);
        _isEnteringMap = true;
        facingDir = -1;
        ApplyFacing(-1);
        if (rb != null) rb.velocity = Vector2.zero;
    }

    UnitBase _forcedTarget;

    /// <summary>引导等：强制锁定某个目标（可为空清除）。</summary>
    public void SetForcedTarget(UnitBase t) => _forcedTarget = t;

    protected override void AIUpdate()
    {
        if (_forcedTarget != null)
        {
            if (_forcedTarget.isDead)
                _forcedTarget = null;
            else
            {
                if (_isEnteringMap) _isEnteringMap = false;
                target = _forcedTarget;
                // 直接走基类战斗逻辑（追击/攻击）
                RunForcedCombat();
                return;
            }
        }

        if (_isEnteringMap)
        {
            if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                return;
            }

            UnitBase foe = FindNearestEnemyOnField();
            if (foe != null)
            {
                _isEnteringMap = false;
                target = foe.isAlly == isAlly ? null : foe;
                if (target == null)
                {
                    AdvanceTowardEnemies();
                    return;
                }
                RunForcedCombat();
                return;
            }

            float dx = _enterTargetPos.x - transform.position.x;
            if (Mathf.Abs(dx) <= 0.08f)
            {
                GameConfig.SetWorldPosition(transform, new Vector3(_enterTargetPos.x, UnitBase.GROUND_Y, transform.position.z));
                _isEnteringMap = false;
                AdvanceTowardEnemies();
                return;
            }

            facingDir = -1;
            ApplyFacing(facingDir);
            float moveDir = Mathf.Sign(_enterTargetPos.x - transform.position.x);
            if (Mathf.Abs(moveDir) < 0.01f) moveDir = -1f;
            if (UnitCrowd.IsBlockedByFrontAlly(this, moveDir))
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                // 被挡住时不要每帧挤位，否则后面的怪会来回哆嗦
                return;
            }
            float step = _enterSpeed * Time.deltaTime;
            float nx = Mathf.MoveTowards(transform.position.x, _enterTargetPos.x, step);
            GameConfig.SetWorldPosition(transform, new Vector3(nx, UnitBase.GROUND_Y, transform.position.z));
            if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            return;
        }

        // 全图索敌；无目标时仍朝玩家方向推进（不原地待机）
        target = FindNearestEnemyOnField();
        if (target != null && target.isAlly == isAlly)
            target = null;
        if (target == null)
        {
            AdvanceTowardEnemies();
            return;
        }

        RunForcedCombat();
    }

    /// <summary>怪物全图找最近己方目标（不限索敌圈，避免远程半路停住）。</summary>
    UnitBase FindNearestEnemyOnField()
    {
        if (BattleManager.Instance == null) return null;
        UnitBase nearest = null;
        float minDist = float.MaxValue;
        float myX = GetCombatX(this);
        var allies = BattleManager.Instance.allyUnits;
        if (allies == null) return null;
        for (int i = 0; i < allies.Count; i++)
        {
            var enemy = allies[i];
            if (enemy == null || enemy.isDead) continue;
            float dist = Mathf.Abs(myX - GetCombatX(enemy));
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    void RunForcedCombat()
    {
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }
        if (target == null || target.isDead) return;

        float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
        float attackRange = attr.GetAttr(AttrType.AttackRange);
        float dir = GetCombatX(target) > GetCombatX(this) ? 1 : -1;
        facingDir = (int)dir;
        ApplyFacing(facingDir);

        bool isMoving = false;
        bool inRange = distance <= attackRange;
        if (inRange && attackCd <= 0)
        {
            Attack(target);
            attackCd = GetAttackCooldown();
        }

        // 攻击时不原地站定：持续朝目标推进，进射程后边走边打
        if (UnitCrowd.IsBlockedByFrontAlly(this, dir))
        {
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
        }
        else
        {
            float spd = attr.GetAttr(AttrType.MoveSpeed);
            if (rb != null) rb.velocity = new Vector2(dir * spd, rb.velocity.y);
            isMoving = true;
            UnitCrowd.ResolveOverlap(this);
        }

        if (unitAnim != null) unitAnim.SetMove(isMoving, facingDir);
    }

    void AdvanceTowardEnemies()
    {
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }

        UnitBase foe = FindNearestEnemyOnField();
        float dir;
        if (foe != null)
        {
            target = foe;
            dir = GetCombatX(foe) > GetCombatX(this) ? 1f : -1f;
        }
        else
        {
            dir = -1f;
        }

        facingDir = (int)dir;
        ApplyFacing(facingDir);

        if (UnitCrowd.IsBlockedByFrontAlly(this, dir))
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }

        float spd = attr.GetAttr(AttrType.MoveSpeed);
        if (rb != null) rb.velocity = new Vector2(dir * spd, rb.velocity.y);
        if (unitAnim != null) unitAnim.SetMove(true, facingDir);
        UnitCrowd.ResolveOverlap(this);
    }

    /// <summary>
    /// 初始化怪物
    /// </summary>
    /// <param name="scaleMultiplier">缩放倍率：普通1.0 / 精英1.5 / Boss2.0</param>
    /// <param name="spriteIndexOverride">动态精灵编号（1-12），用于渐进式解锁系统覆盖配置中的spriteIndex</param>
    public void Init(MonsterConfig template, int waveNum, int chapter = 1, float scaleMultiplier = 1f, int spriteIndexOverride = 0)
    {
        _chapter = chapter;
        config = template;
        gameObject.name = template.id;

        ResetForReuse();

        // 处理 RectTransform 根节点（用户预制体根是 RectTransform）
        // 关键：修改 anchor/pivot 前保存世界位置，修改后恢复，避免被重置到 (0,0)
        RectTransform rootRT = GetComponent<RectTransform>();
        if (rootRT != null)
        {
            Vector3 worldPos = transform.position;
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            GameConfig.SetWorldPosition(gameObject, worldPos);
        }

        // 根缩放：用怪物专用尺度（约 3.75~4.5），勿用 UNIT_SCALE=1 —— 像素怪会小到「像没刷」
        bool eliteWave = scaleMultiplier >= GameConfig.ELITE_SCALE_MULTIPLIER - 0.05f
                         && scaleMultiplier < GameConfig.BOSS_SCALE_MULTIPLIER - 0.05f;
        bool bossUnit = (template != null && template.isBoss) || scaleMultiplier >= GameConfig.BOSS_SCALE_MULTIPLIER - 0.05f;
        float rootScale = GameConfig.MONSTER_BASE_SCALE;
        if (bossUnit) rootScale = GameConfig.MONSTER_BASE_SCALE * GameConfig.BOSS_SCALE_MULTIPLIER;
        else if (eliteWave) rootScale = GameConfig.MONSTER_BASE_SCALE * GameConfig.ELITE_SCALE_MULTIPLIER;
        else if (scaleMultiplier > 1.01f)
            rootScale = GameConfig.MONSTER_BASE_SCALE * scaleMultiplier;
        GameConfig.AttachToUnitRoot(transform);
        transform.localScale = Vector3.one * rootScale;

        // 预制体 Monsters 子节点常为 100；归一为 1，并把锚点从 Canvas 坐标换算到世界 unit
        Transform monstersChild = transform.Find("Monsters");
        if (monstersChild != null)
        {
            Vector3 monstersPos = monstersChild.localPosition;
            monstersPos.z = 0;
            monstersChild.localPosition = monstersPos;
            monstersChild.localScale = Vector3.one * GameConfig.MONSTER_CHILD_REF_SCALE;
        }

        // 先换精灵，再算受击/发射点（否则两帧内还是空图，会落到被放大的本地 offset）
        int effectiveSpriteIndex = spriteIndexOverride > 0 ? spriteIndexOverride : template.spriteIndex;
        _spriteIndex = effectiveSpriteIndex;
        LoadSprite(template, chapter, effectiveSpriteIndex);
        NormalizeMonsterAnchorNodes();

        // 冒险日志图鉴：首次出场即记遭遇（黑影→亮图）
        if (template != null)
        {
            if (!string.IsNullOrEmpty(template.id))
                AdventureCodex.MarkMonsterSeen(template.id);
            // 渐进精灵编号 → forest_4xx 形式
            int mc = GameConfig.GetMonsterChapter(chapter);
            string guess = AdventureCodex.GuessAssetIdFromSprite(mc, effectiveSpriteIndex);
            if (!string.IsNullOrEmpty(guess))
                AdventureCodex.MarkMonsterSeen(guess);
        }

        // 精灵加载后强制程序化动画 + 重缓存缩放（绑定 Monsters 本体，勿绑血条）
        if (unitAnim != null)
        {
            unitAnim.ForceProceduralMode(sr);
            unitAnim.RecacheBaseScale();
        }

        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        _attackStyle = MonsterAttackStyleTable.Get(monsterChapter, Mathf.Max(1, effectiveSpriteIndex));
        _isBossUnit = bossUnit || effectiveSpriteIndex >= GameConfig.BOSS_SPRITE_START;
        _swingStyle = _attackStyle;
        _bossSwingIndex = 0;

        // 属性：优先数值表基准，再乘章节/公会/难度系数
        attr.ResetToBase();
        int guildLv = SaveSystem.Instance?.Data?.guildLevel ?? 0;
        float chapterScale = 1f + GameConfig.CHAPTER_SCALE_PER * Mathf.Max(0, chapter - 1);
        float guildScale = 1f + GameConfig.GUILD_SCALE_PER * guildLv;
        float diffScale = BattleManager.Instance != null ? BattleManager.Instance.DifficultyStatScale : 1f;
        float scale = chapterScale * guildScale * diffScale;

        float baseHp = template != null && template.baseHp > 0 ? template.baseHp : GameConfig.MONSTER_NORMAL_HP;
        float baseAtk = template != null && template.baseAttack > 0 ? template.baseAttack : GameConfig.MONSTER_NORMAL_ATK;
        float baseDef = GameConfig.MONSTER_NORMAL_DEF;
        float atkInterval = GameConfig.MONSTER_NORMAL_ATK_INTERVAL;

        if (bossUnit)
        {
            if (template == null || template.baseHp < GameConfig.MONSTER_BOSS_HP * 0.5f)
                baseHp = GameConfig.MONSTER_BOSS_HP;
            if (template == null || template.baseAttack < GameConfig.MONSTER_BOSS_ATK * 0.5f)
                baseAtk = GameConfig.MONSTER_BOSS_ATK;
            baseDef = GameConfig.MONSTER_BOSS_DEF;
            atkInterval = GameConfig.MONSTER_BOSS_ATK_INTERVAL;
        }
        else if (eliteWave)
        {
            baseHp = GameConfig.MONSTER_ELITE_HP;
            baseAtk = GameConfig.MONSTER_ELITE_ATK;
            baseDef = GameConfig.MONSTER_ELITE_DEF;
            atkInterval = GameConfig.MONSTER_ELITE_ATK_INTERVAL;
        }

        float waveMul = 1f + waveNum * 0.05f;
        float ttkMul = (bossUnit || eliteWave)
            ? WeaponCombatTable.EliteBossHpMul(monsterChapter, bossUnit)
            : (1f + GameConfig.CHAPTER_SCALE_PER * Mathf.Max(0, chapter - 1));
        // 非精英仍用原 chapterScale；精英/Boss 用 TTK 表（已含章节）
        float hpScale = (bossUnit || eliteWave) ? (guildScale * diffScale * ttkMul) : (scale);
        attr.SetAttr(AttrType.MaxHp, baseHp * hpScale * waveMul);
        attr.SetAttr(AttrType.Attack, baseAtk * scale * waveMul * GameConfig.MONSTER_DAMAGE_MULTIPLIER);
        attr.SetAttr(AttrType.Defense, baseDef * scale);
        float atkSpeedMul = GameConfig.MONSTER_ATK_SPEED_MUL;
        if (BattleManager.Instance != null)
            atkSpeedMul *= BattleManager.Instance.runMonsterAtkSpeedMul;
        attr.SetAttr(AttrType.AttackSpeed,
            (1f / Mathf.Max(0.2f, atkInterval)) * atkSpeedMul);
        // 远程怪攻击间隔再缩 40%（攻速 ÷0.6）
        if (!_isBossUnit && MonsterAttackStyleTable.IsRanged(_attackStyle))
        {
            attr.SetAttr(AttrType.AttackSpeed,
                attr.GetAttr(AttrType.AttackSpeed) / 0.6f);
        }
        float moveSpd = template != null && template.baseMoveSpeed > 0.01f
            ? Mathf.Min(template.baseMoveSpeed, GameConfig.MONSTER_DEFAULT_MOVE_SPEED * 1.5f)
            : GameConfig.MONSTER_DEFAULT_MOVE_SPEED;
        // Boss：近远都能打，用远程射程贴近；小怪严格按表
        float atkRange;
        if (_isBossUnit)
        {
            atkRange = MonsterAttackStyleTable.GetAttackRange(MonsterAttackStyle.Ranged);
        }
        else
        {
            atkRange = MonsterAttackStyleTable.GetAttackRange(_attackStyle);
            if (template != null && template.attackRange > 0.01f)
            {
                float tpl = GameConfig.NormalizeAttackRange(template.attackRange);
                if (MonsterAttackStyleTable.IsRanged(_attackStyle))
                    atkRange = Mathf.Max(tpl, atkRange);
                else
                    atkRange = tpl;
            }
        }
        attr.SetAttr(AttrType.MoveSpeed, moveSpd);
        attr.SetAttr(AttrType.AttackRange, atkRange);
        attr.SetAttr(AttrType.CritRate, 0.05f);

        if (GameConfig.IsOpeningStage() && !bossUnit)
        {
            attr.SetAttr(AttrType.MaxHp, attr.GetAttr(AttrType.MaxHp) * 1.25f);
            attr.SetAttr(AttrType.Attack, attr.GetAttr(AttrType.Attack) * 0.7f);
            attr.SetAttr(AttrType.AttackSpeed, attr.GetAttr(AttrType.AttackSpeed) * 0.7f);
            attr.SetAttr(AttrType.MoveSpeed, attr.GetAttr(AttrType.MoveSpeed) * 0.75f);
        }

        currentHp = attr.GetAttr(AttrType.MaxHp);
        if (currentHp <= 0f)
            currentHp = Mathf.Max(1f, GameConfig.MONSTER_NORMAL_HP);
        isAlly = false; // 池复用时防止脏状态
        float goldMul = BattleManager.Instance != null ? BattleManager.Instance.DifficultyGoldMul : 1f;
        if (BattleManager.Instance != null && BattleManager.Instance.IsGoldDungeon)
            goldMul *= 2f;
        goldDrop = Mathf.FloorToInt((template != null ? template.baseGoldDrop : 5) * (1 + waveNum * 0.1f) * scale * goldMul);
        expDrop = Mathf.FloorToInt((template != null ? template.expDrop : 3) * (1 + waveNum * 0.1f) * scale);

        // 怪物初始面向左（朝玩家方向）
        facingDir = -1;
        ApplyFacing(-1);

        // 重置血条
        FindHPBar();
        NormalizeHPBarLayout(rootScale);
        if (_hpBarRoot != null)
        {
            _hpBarRoot.gameObject.SetActive(true);
        }

        // 设置排序层
        ApplySortingLayer();
        UnitCrowd.EnsureTriggerCollider(this);

        // 设置 HPBar 排序层（在怪物之上）
        SetupHPBarSorting();

        _eliteWave = eliteWave;
        _skillId = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetMonsterSkillId(template, eliteWave, _isBossUnit, _attackStyle)
            : null;
        // 远程小怪也拿到了技能 id，就让它能放：远程怪必须看得到技能子弹
        _canUseActiveSkill = !string.IsNullOrEmpty(_skillId);
        bool strong = eliteWave || _isBossUnit;
        _skillEnergy = _canUseActiveSkill ? (strong ? 0.5f : 0f) : 0f;
        _skillCooldown = _canUseActiveSkill && !strong ? 3f : 0f;

        Debug.Log($"[Monster:{template.id}] Init | sprite={_spriteIndex} style={_attackStyle} boss={_isBossUnit} range={atkRange:F1} skill={_skillId}");
    }

    /// <summary>
    /// 设置 HPBar 各 SpriteRenderer 的排序层
    /// 尊重预制体原有层级，只有未设置（Default）时才使用 Effects 默认值
    /// </summary>
    void SetupHPBarSorting()
    {
        if (_hpBarRoot == null) return;

        Transform bg = _hpBarRoot.Find("HPBarBG") ?? _hpBarRoot.Find("HPBarBg");
        if (bg != null)
        {
            _hpBarBg = bg.GetComponent<SpriteRenderer>();
            if (_hpBarBg != null)
            {
                _hpBarBg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                _hpBarBg.sortingOrder = GameConfig.SORT_VFX - 2;
            }
        }

        if (_hpBarFill != null)
        {
            _hpBarFill.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarFill.sortingOrder = GameConfig.SORT_VFX - 1;
        }

        // 怪物本体压在血条下方
        if (sr != null)
        {
            sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            sr.sortingOrder = GameConfig.SORT_UNIT;
        }
    }

    public bool IsBossUnit => _isBossUnit;
    public bool IsEliteWave => _eliteWave;

    public override void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false)
    {
        base.TakeDamage(damage, isCrit, ignoreDefense);
        if (!isDead)
            BringHpBarFront();
    }

    /// <summary>受击时把血条抬到同层最前，重叠时能看清掉血单位。</summary>
    void BringHpBarFront()
    {
        s_hpBarFrontBoost = (s_hpBarFrontBoost + 2) % 40;
        int bgOrder = GameConfig.SORT_VFX - 2 + s_hpBarFrontBoost;
        int fillOrder = bgOrder + 1;
        if (_hpBarBg != null)
        {
            _hpBarBg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarBg.sortingOrder = bgOrder;
        }
        if (_hpBarFill != null)
        {
            _hpBarFill.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarFill.sortingOrder = fillOrder;
        }
    }

    /// <summary>
    /// 加载怪物精灵（替换 Monsters 子节点上的 sprite，不改变子节点 scale）
    /// </summary>
    /// <param name="effectiveSpriteIndex">有效精灵编号（1-12），0表示随机</param>
    private void LoadSprite(MonsterConfig template, int chapter, int effectiveSpriteIndex = 0)
    {
        if (sr == null)
        {
            Transform monstersChild = transform.Find("Monsters");
            if (monstersChild != null)
                sr = monstersChild.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = GetComponentInChildren<SpriteRenderer>(true);
        }
        if (sr == null)
        {
            Debug.LogError($"[Monster] LoadSprite 失败：无 SpriteRenderer id={template?.id}");
            return;
        }

        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        Sprite monsterSprite = null;

        var loader = MonsterSpriteLoader.Instance;
        if (loader != null)
        {
            if (effectiveSpriteIndex > 0)
                monsterSprite = loader.LoadMonsterSprite(monsterChapter, effectiveSpriteIndex - 1);
            else
                monsterSprite = loader.GetRandomMonsterSprite(monsterChapter);
        }

        // 注册表没赋值时，直接从 Resources 路径加载 PNG
        if (monsterSprite == null)
            monsterSprite = LoadSpriteFromResources(monsterChapter, effectiveSpriteIndex);

        if (monsterSprite != null)
            sr.sprite = monsterSprite;
        else
            Debug.LogWarning($"[Monster] 未找到怪物精灵: 章节{monsterChapter}, 索引{effectiveSpriteIndex}，使用预制体默认精灵");
    }

    /// <summary>
    /// 直接从 Resources 路径加载怪物精灵 PNG（注册表未赋值时的兜底）
    /// </summary>
    Sprite LoadSpriteFromResources(int monsterChapter, int spriteIndex)
    {
        string folderName = null;
        string prefix = null;

        switch (monsterChapter)
        {
            case 1: folderName = "1 Undead"; prefix = "undead_1"; break;
            case 2: folderName = "2 Jungle"; prefix = "jungle_2"; break;
            case 3: folderName = "3 Sea"; prefix = "sea_3"; break;
            case 4: folderName = "4 Forest"; prefix = "forest_4"; break;
            case 5: folderName = "5 Field"; prefix = "field_5"; break;
            case 6: folderName = "6 Cave"; prefix = "cave_6"; break;
            case 7: folderName = "7 Devil"; prefix = "devil_7"; break;
            case 8: folderName = "8 Ice"; prefix = "ice_8"; break;
        }

        if (folderName == null) return null;

        int index = spriteIndex > 0 ? spriteIndex : Random.Range(1, 13);
        string spriteName = $"{prefix}{index:D2}";
        string path = $"Config/MonsterSpriteRegistry/{folderName}/{spriteName}";

        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0f), 100f);
            }
        }

        if (sprite != null)
            Debug.Log($"[Monster] 从Resources加载精灵: {path}");
        return sprite;
    }

    protected override void Update()
    {
        if (!isDead && _canUseActiveSkill)
        {
            _skillCooldown -= Time.deltaTime;
            if (_skillCooldown <= 0f)
                _skillEnergy = Mathf.Min(1f, _skillEnergy + Time.deltaTime * 0.28f);
        }
        base.Update();
    }

    protected override void Attack(UnitBase target)
    {
        _swingStyle = ResolveSwingStyle(target);

        if (_canUseActiveSkill && _skillEnergy >= 0.99f && !string.IsNullOrEmpty(_skillId))
        {
            UseActiveSkill(target);
            attackCd = GetAttackCooldown();
            return;
        }

        base.Attack(target);
        if (_canUseActiveSkill)
            _skillEnergy = Mathf.Min(1f, _skillEnergy + 0.15f);
    }

    MonsterAttackStyle ResolveSwingStyle(UnitBase target)
    {
        if (!_isBossUnit)
            return _attackStyle;

        // Boss：近身刀光，远距法球；并轮换保证两种都会用到
        float dist = target != null
            ? Mathf.Abs(transform.position.x - target.transform.position.x)
            : 99f;
        const float meleeReach = 1.8f;
        MonsterAttackStyle byDist = dist <= meleeReach ? MonsterAttackStyle.Melee : MonsterAttackStyle.Ranged;
        _bossSwingIndex++;
        if (_bossSwingIndex % 3 == 0)
            return byDist == MonsterAttackStyle.Melee ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
        return byDist;
    }

    void UseActiveSkill(UnitBase primaryTarget)
    {
        _skillEnergy = 0f;
        _skillCooldown = (_isBossUnit || _eliteWave) ? 2.5f : 5f;
        _swingStyle = ResolveSwingStyle(primaryTarget);

        var skill = SkillRegistry.Instance?.GetActiveSkill(_skillId);
        float mult = skill != null ? skill.damageMultiplier : 2.2f;
        float extra = skill != null ? skill.baseDamage : 0f;
        // 普通远程小怪也能放技能，但伤害要打折，不然开局会被点爆
        float tier = (_isBossUnit || _eliteWave) ? 1f : GameConfig.MONSTER_NORMAL_SKILL_DAMAGE_MUL;
        float damage = (attr.GetAttr(AttrType.Attack) * mult + extra) * tier;
        float radius = skill != null && skill.aoeRadius > 0 ? skill.aoeRadius : 5f;

        // 弹道按攻击方式表分：Bow 走 Enemy/Bow/vfx_enemy_bow_*，Ranged 走 Enemy/Orb/vfx_orb_*。
        // 小怪一律以表为准，禁止被技能表的 attackKit 盖掉；Boss 才允许技能自带 kit。
        AttackVfxKit kit = MonsterAttackStyleTable.GetVfxKit(_swingStyle);
        bool useTableKit = !_isBossUnit;
        if (_isBossUnit)
        {
            var skillCfg = SkillRegistry.Instance?.Get(_skillId);
            if (skillCfg != null && skillCfg.attackKit != AttackVfxKit.None)
                kit = skillCfg.attackKit;
        }

        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = primaryTarget != null ? primaryTarget.GetHitPosition() : firePos;

        // 技能动作：比普攻夸张一截
        if (unitAnim != null)
            unitAnim.PlaySkillCast(kit, (_isBossUnit || _eliteWave) ? 1.15f : 1f);

        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
        {
            // 远程技：子弹飞到再结算。小怪不传技能专属 impact，命中走敌方套自己的 hit
            GameObject impact = useTableKit
                ? null
                : SkillRegistry.Instance?.GetSkillVfxPrefab(_skillId);
            Transform targetTf = primaryTarget != null ? primaryTarget.transform : null;
            BattleVFXSystem.Instance?.PlaySkillProjectile(
                VfxFaction.Enemy, firePos, hitPos, facingDir, targetTf, kit,
                impact, SkillProjectileScale, SkillProjectileSpeedMul,
                () => ApplySkillDamage(damage, radius, primaryTarget));

            if (BattleVFXSystem.Instance == null)
                ApplySkillDamage(damage, radius, primaryTarget);
            return;
        }

        // 近战技：原地砸，伤害瞬发
        SkillRegistry.Instance?.PlaySkillVfx(_skillId, hitPos, false, facingDir, transform);
        BattleVFXSystem.Instance?.PlayAttackKit(kit, VfxFaction.Enemy, firePos, hitPos, facingDir,
            primaryTarget != null ? primaryTarget.transform : null);
        ApplySkillDamage(damage, radius, primaryTarget);
    }

    /// <summary>技能子弹放大一点，看得出是「技能」不是普攻</summary>
    const float SkillProjectileScale = 1.6f;
    /// <summary>技能子弹比普攻箭慢一点，飞行过程看得清</summary>
    const float SkillProjectileSpeedMul = 0.7f;

    /// <summary>
    /// 技能结算：范围内的我方单位一起吃伤害。
    /// 远程技延迟到子弹命中才调用，这期间自己可能已经死了或者被对象池回收再拿去当别的怪，
    /// 所以必须重新确认还活着、还在场上，否则会出现「死怪继续打人」。
    /// </summary>
    void ApplySkillDamage(float damage, float radius, UnitBase primaryTarget)
    {
        if (this == null || isDead || !gameObject.activeInHierarchy) return;

        var allies = BattleManager.Instance?.allyUnits;
        if (allies != null && allies.Count > 0)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                var u = allies[i];
                if (u == null || u.isDead) continue;
                float dist = Mathf.Abs(u.transform.position.x - transform.position.x);
                if (dist > radius) continue;
                bool crit = Random.value < attr.GetAttr(AttrType.CritRate);
                u.TakeDamage(crit ? damage * 1.5f : damage, crit);
            }
            return;
        }
        if (primaryTarget != null && !primaryTarget.isDead)
            primaryTarget.TakeDamage(damage, false);
    }

    /// <summary>
    /// 重写朝向翻转：怪物只翻转SpriteRenderer.flipX，不翻转整个transform
    /// 因为 Monstersmoban 预制体根节点下有 HPBar/beattack/fire 等子节点，
    /// 翻转整个transform会导致血条和受击点位置错乱
    /// 
    /// 精灵默认朝左（spriteDefaultFacesRight=false）：
    ///   面朝左(dir=-1) → 不翻转(flipX=false) → 显示原始左朝向 ✓
    ///   面朝右(dir=+1) → 翻转(flipX=true)   → 显示右朝向 ✓
    /// </summary>
    private int _lastAppliedDir = 0; // 记录上次朝向，避免每帧重复日志

    protected override void ApplyFacing(int dir)
    {
        int visualDir = spriteDefaultFacesRight ? dir : -dir;
        bool shouldFlip = visualDir < 0;
        if (sr != null)
        {
            sr.flipX = shouldFlip;
            if (dir != _lastAppliedDir)
            {
                _lastAppliedDir = dir;
                // 朝向日志关闭：高频会卡
            }
        }
    }

    /// <summary>按对照表；Boss 本挥击可能近战或远程</summary>
    /// <summary>普攻弹道以攻击方式表为准：Bow=箭矢，Ranged=法球。</summary>
    protected override AttackVfxKit GetAttackVfxKit()
    {
        return MonsterAttackStyleTable.GetVfxKit(_swingStyle);
    }

    /// <summary>远程怪多留一点索敌距离，避免贴脸才开火</summary>
    public override float GetDetectRange()
    {
        float baseRange = base.GetDetectRange();
        if (MonsterAttackStyleTable.IsRanged(_swingStyle) || MonsterAttackStyleTable.IsRanged(_attackStyle))
            return baseRange + GameConfig.MONSTER_RANGED_DETECT_BONUS;
        return baseRange;
    }

    /// <summary>发射点随朝向镜像；高度跟躯干受击点，不再往脚下压。</summary>
    public override Vector3 GetFirePosition()
    {
        Transform fire = firePoint != null ? firePoint : transform.Find("fire");
        if (fire != null)
        {
            // 全程世界坐标：预制体根缩放下，局部偏移会把发射点甩到脚底或体外
            Vector3 fw = fire.position;
            float centerX = transform.position.x;
            float absX = Mathf.Abs(fw.x - centerX);
            if (absX < 0.05f) absX = 0.35f;
            float y = hitPoint != null ? hitPoint.position.y : fw.y;
            return new Vector3(centerX + (facingDir < 0 ? -absX : absX), y, transform.position.z);
        }
        Vector3 off = firePointOffset;
        float fy = hitPoint != null ? hitPoint.position.y : transform.position.y + off.y;
        return new Vector3(transform.position.x + off.x * facingDir, fy, transform.position.z);
    }

    /// <summary>更新血条填充比例</summary>
    protected void LateUpdate()
    {
        if (_hpBarFill != null && _hpBarRoot != null && _hpBarRoot.gameObject.activeSelf)
        {
            float maxHp = attr.GetAttr(AttrType.MaxHp);
            float ratio = maxHp > 0 ? currentHp / maxHp : 0;
            float clamped = Mathf.Clamp01(ratio);
            var fillT = _hpBarFill.transform;
            float w = Mathf.Max(0.01f, _hpBarFillBaseWidth);
            fillT.localScale = new Vector3(clamped, 1f, 1f);
            // 左对齐缩放，避免从中心缩短
            fillT.localPosition = new Vector3(-w * 0.5f * (1f - clamped), 0f, 0f);
        }
    }

    protected override void Die()
    {
        if (_hpBarRoot != null)
            _hpBarRoot.gameObject.SetActive(false);

        base.Die();
    }

    public override void ResetForReuse()
    {
        base.ResetForReuse();
        _lastAppliedDir = 0;
        _isEnteringMap = false;
        _bossSwingIndex = 0;
        _forcedTarget = null;
    }
}
