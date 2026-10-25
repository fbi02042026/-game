using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 怪物单位 partial —— 2026-10-17 由 Monster.cs 纯位移拆出（无逻辑改动）
// 职责：出生初始化（Init 数值/风格/索敌定稿）、贴图加载、锚点规范化、精英光环
public partial class Monster
{
    private int _spriteIndex;
    // 【2026-10-16 主人摆的 JY 节点】精英/Boss 脚下光环标识（Monstersmoban 预制体直接子节点）。
    // 缓存一次即可：Visual 进对象池时节点跟着走，不会销毁。
    Transform _eliteRing;

    void ApplyMonsterVisualScaleRules()
    {
        Transform visual = _visualRoot != null ? _visualRoot : transform;
        visual.localScale = Vector3.one;

        Transform monstersChild = transform.Find("Monsters");
        if (monstersChild != null)
        {
            var mp = monstersChild.localPosition;
            mp.z = 0f;
            monstersChild.localPosition = mp;
            // Monsters ?? scale ? ani ????? 1
            monstersChild.localScale = Vector3.one * GameConfig.MONSTER_CHILD_REF_SCALE;
        }
    }

    /// <summary>??/??????????????????LoadSprite ??????/summary>
    void NormalizeMonsterAnchorNodes()
    {
        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(MoveRoot.lossyScale.y));
        float localY = 0.55f / rootAbs;
        float localFireX = -0.12f / rootAbs;
        ApplyAnchorPosition(transform.Find("beattack"), 0f, localY);
        ApplyAnchorPosition(transform.Find("fire"), localFireX, localY);
        Transform be = transform.Find("beattack");
        if (be != null)
        {
            hitPoint = be;
            // ???????????????????????????
            TryPlaceHitPointByOpaqueSprite(be);
            StartCoroutine(CalcHitPointCenter(be));
        }
        Transform fire = transform.Find("fire");
        if (fire != null)
        {
            firePoint = fire;
            StartCoroutine(CalcFirePointCenter(fire));
        }

        // 【2026-10-06 修 · 主人反馈「敌人的影子还是没有」】
        // 根因：预制体 Monstersmoban 里 shadow 节点的 m_LocalPosition.z = -16000，
        // 而战斗相机是正交、far clip plane = 1000 → 影子整个落在视锥外，**压根不渲染**。
        // 同一个预制体里 Monsters / fire / beattack 的 z 都已被本类归零（所以怪能看见），
        // 唯独 shadow 漏了。这里按同一套「归一化 z」口径把它拉回 0。
        // ⚠ 只改深度，主人摆的 x / y / 缩放 / 贴图 / 透明度一个都不动（铁律 2）。
        Transform shadow = FindFootShadowNode(transform);
        if (shadow != null)
        {
            Vector3 lp = shadow.localPosition;
            if (Mathf.Abs(lp.z) > 0.0001f)
            {
                shadow.localPosition = new Vector3(lp.x, lp.y, 0f);
                Debug.Log($"[Monster] 脚底阴影 z 归一化：{shadow.name} z {lp.z} → 0" +
                          $"（原值在正交相机远裁面 1000 之外，影子不会渲染）");
            }
        }
    }

    System.Collections.IEnumerator CalcFirePointCenter(Transform fireTransform)
    {
        yield return null;
        yield return null;
        if (fireTransform == null) yield break;

        // ?????????????????????????????????
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

    /// <summary>
    /// ?????
    /// </summary>
    /// <param name="scaleMultiplier">????????.0 / ??1.5 / Boss2.0</param>
    /// <param name="spriteIndexOverride">???????1-12?????????????????spriteIndex</param>
    public void Init(MonsterConfig template, int waveNum, int chapter = 1, float scaleMultiplier = 1f, int spriteIndexOverride = 0)
    {
        _chapter = chapter;
        config = template;
        gameObject.name = "Visual";

        ResetForReuse();
        EnsureBodyRoot();
        MoveRoot.name = template.id;

        // ?? RectTransform?Visual ??????
        RectTransform rootRT = GetComponent<RectTransform>();
        if (rootRT != null)
        {
            Vector3 worldPos = MoveRoot.position;
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            GameConfig.SetWorldPosition(MoveRoot, worldPos);
            transform.localPosition = Vector3.zero;
        }

        // ?????? Body?Visual ?? 1??????????????
        bool eliteWave = scaleMultiplier >= GameConfig.ELITE_SCALE_MULTIPLIER - 0.05f
                         && scaleMultiplier < GameConfig.BOSS_SCALE_MULTIPLIER - 0.05f;
        bool bossUnit = (template != null && template.isBoss) || scaleMultiplier >= GameConfig.BOSS_SCALE_MULTIPLIER - 0.05f;
        float rootScale = GameConfig.RollMonsterRootScale(eliteWave, bossUnit);
        // Boss 不参与本次缩小（主人 2026-09-26 拍板）：MONSTER_SMALL_SHRINK 已乘进 MONSTER_SCALE_MIN/MAX，
        // 这里对 Boss 除回去以恢复原尺寸；普通怪/精英仍保持 ×0.7（Normal : Elite = 1 : 1.3 不变）。
        if (bossUnit && !GameConfig.MONSTER_BOSS_APPLY_SMALL_SHRINK && GameConfig.MONSTER_SMALL_SHRINK > 0.0001f)
            rootScale /= GameConfig.MONSTER_SMALL_SHRINK;
        GameConfig.AttachToUnitRoot(MoveRoot);
        MoveRoot.localScale = Vector3.one * rootScale;
        ApplyMonsterVisualScaleRules();
        // ??????????????????????????????????offset??
        int effectiveSpriteIndex = spriteIndexOverride > 0 ? spriteIndexOverride : template.spriteIndex;
        _spriteIndex = effectiveSpriteIndex;
        LoadSprite(template, chapter, effectiveSpriteIndex, eliteWave, bossUnit);
        NormalizeMonsterAnchorNodes();

        // ??????????????????????
        if (template != null)
        {
            if (!string.IsNullOrEmpty(template.id))
                AdventureCodex.MarkMonsterSeen(template.id);
            // ?????? ??forest_4xx ??
            int mc = GameConfig.GetMonsterChapter(chapter);
            string guess = AdventureCodex.GuessAssetIdFromSprite(mc, effectiveSpriteIndex);
            if (!string.IsNullOrEmpty(guess))
                AdventureCodex.MarkMonsterSeen(guess);
        }

        // ????????Monsters/ani ?????attack/run/idle/dead??
        if (unitAnim != null)
        {
            unitAnim.EnableMonsterClipAnimator(sr);
            unitAnim.SetFlipXFacing(false);
            unitAnim.RecacheBaseScale();
            ApplyMonsterVisualScaleRules();
            unitAnim.StabilizeMonsterBodyTransform();
        }

        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        _attackStyle = MonsterAttackStyleTable.Get(monsterChapter, Mathf.Max(1, effectiveSpriteIndex));
        _isBossUnit = bossUnit || effectiveSpriteIndex >= GameConfig.BOSS_SPRITE_START;
        _swingStyle = _attackStyle;

        // ??????????????????/????
        attr.ResetToBase();
        int guildLv = SaveSystem.Instance?.Data?.guildLevel ?? 0;
        // 2026-10-05 主人拍板：难度曲线「每章都上升、章内关卡也要上升」。
        // → 总倍率 = 章倍率 × 章内倍率，**唯一出口是 GameConfig.GetStatScale(章, 章内第几关)**，
        //   绝不在这里自己把两个数乘一遍（那是第二处口径，改表时会漏）。
        int stageIdx0 = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : 0;
        // 无限裂隙（2026-10-17）：强度只随绝对层数走（+7%/层，缓冲关 9 折），
        // 不用循环映射章的曲线 —— 映射章会 1→8 循环，直接套会出现「第 40 层比第 36 层弱」的跳水。
        bool endlessRift = BattleManager.Instance != null && BattleManager.Instance.IsEndlessRift;
        float statScale = endlessRift
            ? GameConfig.EndlessDifficultyMul(BattleManager.Instance.EndlessRiftStage)
            : GameConfig.GetStatScale(chapter, stageIdx0);
        float guildScale = 1f + GameConfig.GUILD_SCALE_PER * guildLv;
        float diffScale = BattleManager.Instance != null ? BattleManager.Instance.DifficultyStatScale : 1f;
        float scale = statScale * guildScale * diffScale;

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
            // 精英不再写死常量：按本章普通怪平均基准推导，避免「精英比本章杂兵还脆」。
            // 表缺该章数据时回退旧常量（MONSTER_ELITE_HP / ATK）。
            float avgHp, avgAtk;
            if (MonsterStatsTable.TryGetChapterAverage(monsterChapter, out avgHp, out avgAtk) && avgHp > 0f)
            {
                baseHp = avgHp * GameConfig.ELITE_HP_FROM_CHAPTER_AVG;
                baseAtk = avgAtk * GameConfig.ELITE_ATK_FROM_CHAPTER_AVG;
            }
            else
            {
                baseHp = GameConfig.MONSTER_ELITE_HP;
                baseAtk = GameConfig.MONSTER_ELITE_ATK;
            }
            baseDef = GameConfig.MONSTER_ELITE_DEF;
            atkInterval = GameConfig.MONSTER_ELITE_ATK_INTERVAL;
            // 分档：关卡波次奇偶 → 血厚 / 血薄
            _eliteGlass = ((waveNum + monsterChapter) & 1) == 1;
            if (_eliteGlass)
            {
                baseHp *= GameConfig.ELITE_GLASS_HP_MUL;
                baseAtk *= GameConfig.ELITE_GLASS_ATK_MUL;
            }
            else
            {
                baseHp *= GameConfig.ELITE_TANK_HP_MUL;
                baseAtk *= GameConfig.ELITE_TANK_ATK_MUL;
            }
        }

        // 【2026-10-24 主人拍板「章末大Boss 独立档」】B1 后一章 = 一座大关 = 5 小关，
        // stageIdx0==4 就是章末大 Boss —— v4.1 的「第 5 关小 Boss ×0.6、第 10 关大 Boss（==9）不乘」
        // 里「不乘」分支已死（域里没有 ==9），大 Boss 一直被误吃 ×0.6。现改：正式关章末 Boss 走
        // 独立档 GameConfig.BOSS_FINAL_STAT_MUL（默认 1.0 = 恢复满配）；无限裂隙不吃（自己两条线），
        // 金币本/徽记/支线岛/终章 stageIdx0 恒 0 不命中，行为不变。旧 MINI_BOSS_STAT_MUL 已随小 Boss 位退役删除。
        bool finalBossStage = bossUnit && !endlessRift
            && BattleManager.Instance != null && BattleManager.Instance.IsFormalMainRun
            && stageIdx0 == GameConfig.STAGES_PER_GRAND - 1;
        float bossStatMul = finalBossStage ? GameConfig.BOSS_FINAL_STAT_MUL : 1f;
        baseHp *= bossStatMul;
        baseAtk *= bossStatMul;

        float waveMul = 1f + waveNum * 0.05f;
        float ttkMul;
        if (endlessRift)
        {
            ttkMul = GameConfig.EndlessDifficultyMul(BattleManager.Instance.EndlessRiftStage);
            // 【2026-10-19 主人拍板】大 BOSS（10 的倍数层）在小 BOSS 基础上再叠一档血量系数
            if (bossUnit && GameConfig.IsEndlessBigBossStage(BattleManager.Instance.EndlessRiftStage))
                ttkMul *= GameConfig.ENDLESS_BIGBOSS_HP_MUL;
        }
        else if (bossUnit || eliteWave)
            ttkMul = WeaponCombatTable.EliteBossHpMul(monsterChapter, bossUnit, stageIdx0);
        else
            ttkMul = GameConfig.GetStatScale(chapter, stageIdx0);
        // ?????? chapterScale????Boss ??TTK ????????
        float hpScale = (bossUnit || eliteWave) ? (guildScale * diffScale * ttkMul) : (scale);
        // 【2026-10-22 主人拍板「无限裂隙先排除了 分成两条线」】血量分两条线：
        // 主线吃全局砍半（MONSTER_HP_GLOBAL_MUL=0.5）；无尽裂隙不吃全局砍半，
        // 强度只随层数走（EndlessDifficultyMul）。攻击侧同样裂隙不参与（ChapterAtkMul）。
        // 【2026-10-23 主人拍板「引导的不要动」】新手引导战两条线都不吃——
        // 引导局怪物（血/攻）保持 10-22 削弱批次之前的原始数值。
        bool isTutorialRun = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        float hpGlobalMul = endlessRift || isTutorialRun ? 1f : GameConfig.MONSTER_HP_GLOBAL_MUL;
        attr.SetAttr(AttrType.MaxHp, baseHp * hpScale * waveMul * hpGlobalMul);
        // ⚠【2026-10-12 整段删除·主人「把关卡的怪物数量回复正常」】
        // 这里原本是流程测试开关 `GameConfig.TEST_BOSS_HP`（Boss 压成 1 滴血，一击通关好验证流程）。
        // 它和 stage_spawn.csv 的「TEST 每关 1 只怪 1 波」是同一批 2026-09-23 测试数据，
        // 主人拍板还原正式数值 → 常量连同这一处唯一调用点一起删掉，不留开关。
        // 之后 Boss 血量照常走上面的 `baseHp × hpScale × waveMul × MONSTER_HP_GLOBAL_MUL`。
        // Boss 原先只有血量 TTK 加成，攻击没有 —— 补上，否则后期 Boss 打人比自家远程杂兵还轻
        float atkTtkMul = bossUnit ? GameConfig.BOSS_TTK_ATK_MUL : 1f;
        // 2026-09-26 主人拍板：怪物只分物理 / 魔法，攻击与防御二选一、不混搭。
        // 魔法型（掷骰命中 magicChance）→ 物攻/物防置 0，攻击与防御全部走 MagicAttack / MagicDefense；
        // 物理型 → 魔攻/魔防置 0，只走 Attack / Defense。
        // 判定走 MonsterAttackTypeResolver。
        // 2026-09-28 主人纠正：只有 **精英 / Boss** 才掷骰换攻击方式（stage_spawn.csv 关卡级
        //   magicChance 优先，含 Boss 0.5；未配才回退单怪级）。普通小怪**不掷骰**，表里写什么
        //   就是什么：近战就是近战、弓就是弓（丢物理子弹，黄色小球那套），法师（Ranged）才丢魔法球。
        //   —— 之前所有怪都掷骰，美术上明明是近战的小蘑菇被判成魔法型后跑去丢法球。
        int gChapter = BattleManager.Instance != null ? BattleManager.Instance.CurrentChapter : monsterChapter;
        StageType gStageType = BattleManager.Instance != null && BattleManager.Instance.currentStage != null
            ? BattleManager.Instance.currentStage.type : StageType.Normal;
        bool isMagicType = (eliteWave || _isBossUnit)
            ? MonsterAttackTypeResolver.IsMagicMonster(
                monsterChapter, Mathf.Max(1, effectiveSpriteIndex), gChapter, gStageType)
            : MonsterAttackTypeResolver.IsMagicMonster(monsterChapter, Mathf.Max(1, effectiveSpriteIndex));
        _isMagicType = isMagicType;
        // 【2026-10-25 主人拍板「断崖调缓 不要让玩家察觉」】按章攻击减免改 8 章缓坡表
        //（0.35/0.36/0.40/0.42/0.45/0.52/0.62/0.75，旧三档阶梯 0.35/0.6/0.8 废除——
        // 原第2→第5章入口跳档是全曲线唯一断崖）。真源 = GameConfig.ChapterAtkMul(chapter)；
        // 只压攻击/魔攻，血防不动；无尽裂隙不参与（强度只随层数走）。
        // 【2026-10-23 主人拍板「引导的不要动」】新手引导战不吃章级减免——引导局怪物保持原始攻击
        //（isTutorialRun 在上面血量段已取）。
        float earlyAtkMul = !endlessRift && !isTutorialRun ? GameConfig.ChapterAtkMul(gChapter) : 1f;
        // 魔法型强制改法球弹道(Ranged)→视觉与伤害类型(魔法)一致；物理型保持表内 style。
        // 普通小怪的魔法型本就等于「表里是法师」，这里赋值与表内原值一致；
        // 只有精英/Boss 掷骰命中魔法时，这里才会真的把 Melee/Bow 改成法球。
        if (isMagicType)
            _attackStyle = MonsterAttackStyle.Ranged;
        // 远程（弓/法球）攻击力倍率（真源 = GameConfig.MONSTER_RANGED_ATK_MUL，唯一出口）。
        // 【2026-10-22 主人拍板「攻击再减50%」后该值 0.675：远程不再上浮、比近战低一截。Boss 不吃。】
        // 放在 style 定稿之后、写 attr 之前，魔法型（已强制 Ranged）也一起吃到。
        // 【2026-10-23 主人拍板「引导的不要动」】引导局不吃远程削减（10-22 批次的一部分）。
        if (!_isBossUnit && !isTutorialRun && MonsterAttackStyleTable.IsRanged(_attackStyle))
            baseAtk *= GameConfig.MONSTER_RANGED_ATK_MUL;
        // 【2026-10-14】style 已定稿（含魔法型强制 Ranged），此处抽索敌策略：近战按距离、
        // 远程先打脆皮职业、按 MONSTER_LOW_HP_TARGET_RATIO 比例抽出「挑血少的打」。
        _targetingPolicy = MonsterTargeting.RollPolicy(MonsterAttackStyleTable.IsRanged(_attackStyle));
        if (_isBossUnit)
        {
            _bossTargetShiftCd = GameConfig.BOSS_TARGET_SHIFT_INTERVAL;
            _lastBossMarkTarget = null;   // 怪体复用/重开时清掉上一局的标记目标
        }
        if (isMagicType)
        {
            attr.SetAttr(AttrType.Attack, 0f);
            attr.SetAttr(AttrType.Defense, 0f);
            // 魔法侧数值：配表(MonsterConfig.baseMagicAttack/Defense)给了就用，没给则等比沿用本单位的 baseAttack / baseDef。
            float baseMagAtk = (template != null && template.baseMagicAttack > 0f)
                ? template.baseMagicAttack
                : baseAtk * GameConfig.MONSTER_MAGIC_ATK_RATIO;
            float baseMagDef = (template != null && template.baseMagicDefense > 0f)
                ? template.baseMagicDefense
                : baseDef * GameConfig.MONSTER_MAGIC_DEF_RATIO;
            attr.SetAttr(AttrType.MagicAttack, baseMagAtk * scale * waveMul * GameConfig.MONSTER_DAMAGE_MULTIPLIER * atkTtkMul * earlyAtkMul);
            attr.SetAttr(AttrType.MagicDefense, baseMagDef * scale);
        }
        else
        {
            attr.SetAttr(AttrType.Attack, baseAtk * scale * waveMul * GameConfig.MONSTER_DAMAGE_MULTIPLIER * atkTtkMul * earlyAtkMul);
            attr.SetAttr(AttrType.Defense, baseDef * scale);
            attr.SetAttr(AttrType.MagicAttack, 0f);
            attr.SetAttr(AttrType.MagicDefense, 0f);
        }
        float atkSpeedMul = GameConfig.MONSTER_ATK_SPEED_MUL;
        if (BattleManager.Instance != null)
            atkSpeedMul *= BattleManager.Instance.runMonsterAtkSpeedMul;
        if (template != null && template.baseAttackSpeed > 0.01f)
            atkSpeedMul *= template.baseAttackSpeed;
        attr.SetAttr(AttrType.AttackSpeed,
            (1f / Mathf.Max(0.2f, atkInterval)) * atkSpeedMul);
        // ??????????40%?????0.6??
        if (!_isBossUnit && MonsterAttackStyleTable.IsRanged(_attackStyle))
        {
            attr.SetAttr(AttrType.AttackSpeed,
                attr.GetAttr(AttrType.AttackSpeed) / 0.6f);
        }
        // 怪物移速统一由表驱动（非 Boss 与 Boss 同一逻辑）。
        // 表 moveSpeed 为「世界前」相对值，需乘 MONSTER_MOVE_SPEED_TO_WORLD 转世界单位；
        // monster_stats 已全体减半(2.2→1.1)，1.1 × 0.3142 = 0.3456 世界单位。
        // 注：原 Boss 走 Mathf.Min(表值, DEFAULT×1.5) 会被钳回 1.0368；减半后表值远低于该上限，
        // 若保留上限会把减半后的速度又钳高，故这里不再设上限。
        float moveSpd = (template != null && template.baseMoveSpeed > 0.01f)
            ? template.baseMoveSpeed * GameConfig.MONSTER_MOVE_SPEED_TO_WORLD
            : GameConfig.MONSTER_DEFAULT_MOVE_SPEED;
        float atkRange;
        if (_isBossUnit)
        {
            atkRange = AttackRangeTable.GetMonsterWorld(MonsterAttackStyle.Ranged);
        }
        else
        {
            atkRange = AttackRangeTable.GetMonsterWorld(_attackStyle);
        }
        attr.SetAttr(AttrType.MoveSpeed, moveSpd);
        // 非 Boss：移速小幅岔开，减轻同相位叠走
        if (!_isBossUnit)
        {
            float jitter = Random.Range(0.88f, 1.12f);
            attr.SetAttr(AttrType.MoveSpeed, moveSpd * jitter);
        }
        attr.SetAttr(AttrType.AttackRange, atkRange);
        attr.SetAttr(AttrType.CritRate, 0.05f);

        // 引导关怪物也走正式数值，不再开局 +25% 血 / 减攻速。

        currentHp = attr.GetAttr(AttrType.MaxHp);
        if (currentHp <= 0f)
            currentHp = Mathf.Max(1f, GameConfig.MONSTER_NORMAL_HP);
        isAlly = false; // ??????????
        float goldMul = BattleManager.Instance != null ? BattleManager.Instance.DifficultyGoldMul : 1f;
        // 2026-09-29：正式关的金币改成「每关固定额度」（见 StageGoldDefs），击杀不再掉金，
        // 所以这里乘一个全局倍率（默认 0）。**金币副本是专门刷金的模式，不受影响**，仍按击杀掉金 ×2。
        float killGoldMul = StageGoldDefs.MONSTER_KILL_GOLD_MUL;
        bool goldDungeon = BattleManager.Instance != null && BattleManager.Instance.IsGoldDungeon;
        if (goldDungeon)
        {
            goldMul *= 2f;
            killGoldMul = 1f;
        }
        goldDrop = Mathf.FloorToInt((template != null ? template.baseGoldDrop : 5) * (1 + waveNum * 0.1f) * scale * goldMul * killGoldMul);
        expDrop = Mathf.FloorToInt((template != null ? template.expDrop : 3) * (1 + waveNum * 0.1f) * scale);

        // ??????????????
        facingDir = -1;
        ApplyFacing(-1);

        // 血条挂在 MonsterBody（与 Visual 平级），运行时生成，避免镜像翻转
        EnsureWorldHealthBar();
        StartCoroutine(RefreshHpBarLayoutAfterSpriteReady(rootScale));

        // ??????
        ApplySortingLayer();
        RemovePhysicsCollider();

        // ?? HPBar ???????????
        SetupHPBarSorting();

        _eliteWave = eliteWave;
        if (!eliteWave) _eliteGlass = false;
        _bossPhase2Started = false;

        // V6 词缀：属性全部按表算完之后再叠乘子，避免被后续赋值覆盖。
        // 只给精英/Boss；普通小怪不 roll，否则满屏后缀变成视觉噪音。
        MonsterAffix.RollAndAttach(this, _chapter, _isBossUnit || _eliteWave);
        _bossPhaseShiftBusy = false;
        _skillId = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetMonsterSkillId(template, eliteWave, _isBossUnit, _attackStyle)
            : null;
        // ???????????id????????????????????
        _canUseActiveSkill = !string.IsNullOrEmpty(_skillId);
        bool strong = eliteWave || _isBossUnit;
        bool rangedSkill = _canUseActiveSkill && MonsterAttackStyleTable.IsRanged(_attackStyle);
        _skillEnergy = _canUseActiveSkill
            ? (strong ? 0.5f : (rangedSkill ? 0.88f : 0f))
            : 0f;
        _skillCooldown = _canUseActiveSkill && !strong ? (rangedSkill ? 0f : 3f) : 0f;

        // 【2026-10-20 主人拍板】远程精英/Boss 弹幕技：首发错开 2.5s / 5s，避免开打瞬间齐射。
        // 携带规则：远程 Boss 两种全带（但追踪弹幕要血量降到二阶段线 70% 才解锁，判定在触发把关里）；
        // 远程精英只随机带一种（扇形/追踪五五开，不受阶段限制）。近战怪与远程小怪不带。
        bool volleyOwner = strong && MonsterAttackStyleTable.IsRanged(_attackStyle);
        _fanVolleyOwned = volleyOwner && (!_eliteWave || Random.value < 0.5f);
        _homingBarrageOwned = volleyOwner && (!_eliteWave || !_fanVolleyOwned);
        _fanVolleyCd = _fanVolleyOwned ? 2.5f : 0f;
        _homingBarrageCd = _homingBarrageOwned ? 5f : 0f;

        // 【2026-10-16 主人摆的 JY 脚下光环】精英/Boss 出场显示，普通怪隐藏。
        // 光环是 Visual 子节点，精英/Boss 的 rootScale 放大时**自动跟着变大**，代码不碰它的 scale/贴图/位置（铁律 2）。
        ApplyEliteRingVisible(_isBossUnit || _eliteWave);

        BattleBossHpBar.RefreshFromField();
    }

    /// <summary>
    /// JY 节点显隐唯一出口（主人 2026-10-16 在 Monstersmoban 摆的精英/Boss 脚下光环标识）：
    /// 精英/Boss 出场显示、普通怪隐藏、回收时关闭。
    /// 只切 active，不动主人在预制体里调好的贴图/缩放/位置/透明度（铁律 2）；
    /// 预制体里没摆 JY 就静默跳过（不兜底造）。
    /// </summary>
    void ApplyEliteRingVisible(bool on)
    {
        if (_eliteRing == null) _eliteRing = transform.Find("JY");
        if (_eliteRing == null) return;
        if (_eliteRing.gameObject.activeSelf != on)
            _eliteRing.gameObject.SetActive(on);
    }

    void RemovePhysicsCollider()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box != null) Destroy(box);
    }

    /// <summary>
    /// ??????????Monsters ??????sprite????????scale??
    /// </summary>
    /// <param name="effectiveSpriteIndex">????????-12??0????</param>
    /// <param name="eliteWave">本怪是否精英槽位（金币副本专用图分流用）</param>
    /// <param name="bossUnit">本怪是否 Boss（金币副本专用图分流用）</param>
    private void LoadSprite(MonsterConfig template, int chapter, int effectiveSpriteIndex = 0,
        bool eliteWave = false, bool bossUnit = false)
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
            Debug.LogError($"[Monster] LoadSprite ???? SpriteRenderer id={template?.id}");
            return;
        }

        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        Sprite monsterSprite = null;

        // 【2026-10-23 主人拍板】金币副本三种怪统一用「金币」文件夹专属图：小怪/精英/Boss。
        // 按路径 Resources.Load（不走 guid 引用，meta 的 56 字符标识不影响加载）；
        // 加载失败打 Error 回落章节图 —— 不静默吞，主人能在 Console 看见。
        if (BattleManager.Instance != null && BattleManager.Instance.IsGoldDungeon)
        {
            string goldName = bossUnit ? "金币boss" : eliteWave ? "金币精英" : "金币小怪";
            monsterSprite = Resources.Load<Sprite>($"Config/MonsterSpriteRegistry/金币/{goldName}");
            if (monsterSprite == null)
                Debug.LogError($"[Monster] 金币副本精灵加载失败：{goldName}（已回落章节图，请检查 Resources/Config/MonsterSpriteRegistry/金币/）");
        }
        // 【2026-10-23 主人拍板】徽记副本同款专属图（徽记小怪/徽记精英/徽记boss），波次布局与金币副本一致
        else if (BattleManager.Instance != null && BattleManager.Instance.IsBadgeDungeon)
        {
            string badgeName = bossUnit ? "徽记boss" : eliteWave ? "徽记精英" : "徽记小怪";
            monsterSprite = Resources.Load<Sprite>($"Config/MonsterSpriteRegistry/徽记/{badgeName}");
            if (monsterSprite == null)
                Debug.LogError($"[Monster] 徽记副本精灵加载失败：{badgeName}（已回落章节图，请检查 Resources/Config/MonsterSpriteRegistry/徽记/）");
        }

        // 金币图已拿到就不再走章节图加载链（防止覆盖）
        if (monsterSprite == null)
        {
            var loader = MonsterSpriteLoader.Instance;
            if (loader != null)
            {
                if (effectiveSpriteIndex > 0)
                    monsterSprite = loader.LoadMonsterSprite(monsterChapter, effectiveSpriteIndex - 1);
                else
                    monsterSprite = loader.GetRandomMonsterSprite(monsterChapter);
            }

            // ??????????? Resources ???? PNG
            if (monsterSprite == null)
                monsterSprite = LoadSpriteFromResources(monsterChapter, effectiveSpriteIndex);
        }

        if (monsterSprite != null)
        {
            sr.sprite = monsterSprite;
            sr.enabled = true;
            var c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
        else
            Debug.LogWarning($"[Monster] ???????: ??{monsterChapter}, ??{effectiveSpriteIndex}??????????");

        EnsureFootShadow();
        // 2026-10-06 主人要求：把「运行中的真实尺寸」抄一份给他对照调预制体（只读，每局只做一次）
        // 2026-10-17 上线稳定优先：一次性探针已完成使命，真机不再执行，仅编辑器保留
#if UNITY_EDITOR
        MonsterTemplateProbe.DumpOnce(transform);
#endif
    }

    /// <summary>
    /// ????Resources ???????? PNG????????????
    /// </summary>
    Sprite LoadSpriteFromResources(int monsterChapter, int spriteIndex)
    {
        string folderName = null;
        string prefix = null;

        switch (monsterChapter)
        {
            case 1: folderName = "1 Forest"; prefix = "forest_1"; break;
            case 2: folderName = "2 Undead"; prefix = "undead_2"; break;
            case 3: folderName = "3 Jungle"; prefix = "jungle_3"; break;
            case 4: folderName = "4 Field"; prefix = "field_4"; break;
            case 5: folderName = "5 Sea"; prefix = "sea_5"; break;
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
            Debug.Log($"[Monster] ?Resources????: {path}");
        return sprite;
    }
}
