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
    private Transform _hpBarRoot;

    protected override void Awake()
    {
        // 偏移仅作为兜底（预制体有 beattack/fire 节点时不会用到）
        hitPointOffset = new Vector3(0f, 0.5f, 0f);
        firePointOffset = new Vector3(-0.3f, 0.28f, 0f);

        // 怪物使用程序化动画，根节点上的 Animator 没有所需参数会报错，直接移除
        Animator animator = GetComponent<Animator>();
        if (animator != null) DestroyImmediate(animator);

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

    /// <summary>预制体锚点按 Canvas 坐标设计，迁到世界 unit 后需同比例缩小</summary>
    void NormalizeMonsterAnchorNodes()
    {
        float factor = GameConfig.MONSTER_ANCHOR_SCALE_FACTOR;
        ApplyAnchorPosition(transform.Find("beattack"), 0f, 6.6f * factor);
        ApplyAnchorPosition(transform.Find("fire"), -6.6f * factor, 6.6f * factor);
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

            UnitBase foe = FindNearestEnemyInDetectRange();
            if (foe != null)
            {
                _isEnteringMap = false;
                base.AIUpdate();
                return;
            }

            float dx = _enterTargetPos.x - transform.position.x;
            if (Mathf.Abs(dx) <= 0.08f)
            {
                GameConfig.SetWorldPosition(transform, new Vector3(_enterTargetPos.x, UnitBase.GROUND_Y, transform.position.z));
                _isEnteringMap = false;
                IdleWaitForPlayer();
                return;
            }

            facingDir = -1;
            ApplyFacing(facingDir);
            float step = _enterSpeed * Time.deltaTime;
            float nx = Mathf.MoveTowards(transform.position.x, _enterTargetPos.x, step);
            GameConfig.SetWorldPosition(transform, new Vector3(nx, UnitBase.GROUND_Y, transform.position.z));
            if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            return;
        }

        // 无目标：原地朝左待机，等玩家走进索敌范围（禁止向左冲导致擦肩而过）
        if (FindNearestEnemyInDetectRange() == null)
        {
            IdleWaitForPlayer();
            return;
        }

        base.AIUpdate();
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
        if (distance <= attackRange)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (attackCd <= 0)
            {
                Attack(target);
                attackCd = GetAttackCooldown();
            }
        }
        else
        {
            float spd = attr.GetAttr(AttrType.MoveSpeed);
            if (rb != null) rb.velocity = new Vector2(dir * spd, rb.velocity.y);
            isMoving = true;
        }
        if (unitAnim != null) unitAnim.SetMove(isMoving, facingDir);
    }

    void IdleWaitForPlayer()
    {
        if (rb != null) rb.velocity = Vector2.zero;
        facingDir = -1;
        ApplyFacing(facingDir);
        if (unitAnim != null) unitAnim.SetMove(false, facingDir);
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

        NormalizeMonsterAnchorNodes();

        // 加载怪物精灵（使用override或配置中的spriteIndex）
        int effectiveSpriteIndex = spriteIndexOverride > 0 ? spriteIndexOverride : template.spriteIndex;
        _spriteIndex = effectiveSpriteIndex;
        LoadSprite(template, chapter, effectiveSpriteIndex);

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
        attr.SetAttr(AttrType.MaxHp, baseHp * scale * waveMul);
        attr.SetAttr(AttrType.Attack, baseAtk * scale * waveMul * GameConfig.MONSTER_DAMAGE_MULTIPLIER);
        attr.SetAttr(AttrType.Defense, baseDef * scale);
        float atkSpeedMul = GameConfig.MONSTER_ATK_SPEED_MUL;
        if (BattleManager.Instance != null)
            atkSpeedMul *= BattleManager.Instance.runMonsterAtkSpeedMul;
        attr.SetAttr(AttrType.AttackSpeed,
            (1f / Mathf.Max(0.2f, atkInterval)) * atkSpeedMul);
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
            SpriteRenderer bgSr = bg.GetComponent<SpriteRenderer>();
            if (bgSr != null)
            {
                bgSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                bgSr.sortingOrder = GameConfig.SORT_VFX - 2;
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

        AttackVfxKit kit = MonsterAttackStyleTable.GetVfxKit(_swingStyle);
        // 远程小怪技能统一弓箭飞矢 vfx_bow_fly
        if (MonsterAttackStyleTable.IsRanged(_swingStyle) || MonsterAttackStyleTable.IsRanged(_attackStyle))
            kit = AttackVfxKit.Bow;
        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = primaryTarget != null ? primaryTarget.GetHitPosition() : firePos;

        // 技能动作：比普攻夸张一截
        if (unitAnim != null)
            unitAnim.PlaySkillCast(kit, (_isBossUnit || _eliteWave) ? 1.15f : 1f);

        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
        {
            // 远程技：子弹从发射点飞到目标，飞到了才结算 + 才炸技能特效
            GameObject impact = SkillRegistry.Instance?.GetSkillVfxPrefab(_skillId);
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

    /// <summary>发射点随朝向镜像（预制体 fire 默认在左侧）；弓箭再略压低</summary>
    public override Vector3 GetFirePosition()
    {
        Transform fire = firePoint != null ? firePoint : transform.Find("fire");
        float yNudge = MonsterAttackStyleTable.IsRanged(_swingStyle) || MonsterAttackStyleTable.IsRanged(_attackStyle)
            ? -0.12f : 0f;
        if (fire != null)
        {
            Vector3 local = fire.localPosition;
            float absX = Mathf.Abs(local.x);
            float x = facingDir < 0 ? -absX : absX;
            return transform.TransformPoint(new Vector3(x, local.y + yNudge, local.z));
        }
        return transform.position + new Vector3(firePointOffset.x * facingDir, firePointOffset.y + yNudge, 0f);
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
    }
}
