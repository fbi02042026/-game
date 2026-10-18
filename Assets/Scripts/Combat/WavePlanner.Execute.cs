using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次规划 partial —— 2026-10-18 由 WavePlanner.cs 纯位移拆出（无逻辑改动）
// 职责：刷怪执行（SpawnWave 协程、Boss 裂隙涌怪、兜底波、生成单只与渲染器强制开启）
public partial class WavePlanner
{

    float GetMonsterWaveSpacing()
    {
        return GameConfig.MONSTER_WAVE_SPACING * Rules.WaveSpacingMul;
    }

    void SpawnWave(WaveData wave, int waveIndex = -1)
    {
        if (BattleLootMode.Active) return;
        if (waveIndex < 0 && _waves != null)
            waveIndex = _waves.IndexOf(wave);
        if (waveIndex < 0) waveIndex = 0;

        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        StageType stageType = currentStage != null ? currentStage.type : StageType.Normal;
        int chapter = CurrentChapter;
        if (ConfigManager.Instance == null)
        {
            Debug.LogError("[BattleManager] ConfigManager 为空，走兜底刷怪");
            SpawnFallbackWave(wave, waveIndex);
            return;
        }
        var pool = ConfigManager.Instance.GetWaveMonsterPool(chapter, stageIdx);

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[BattleManager] 怪物池为空 stageIdx={stageIdx}，使用兜底怪物");
            SpawnFallbackWave(wave, waveIndex);
            return;
        }

        // 【2026-10-14 主人拍板】精英关不再整波乘 ELITE_SCALE（那会让全波都变精英）：
        // 只有每波末尾 2~4 个精英槽位在 CoSpawnWaveMonsters 里单独乘，其余保持普通怪体型/血量。
        float waveScaleMultiplier = 1f;
        if (wave.isBossWave)
            waveScaleMultiplier = GameConfig.BOSS_SCALE_MULTIPLIER;

        var availableSprites = ConfigManager.Instance.GetAvailableSpriteIndices(chapter, stageIdx, wave.isBossWave);
        if (!wave.isBossWave)
        {
            var nonBossSpriteIndices = pool.Where(m => !m.isBoss && m.spriteIndex > 0)
                .Select(m => m.spriteIndex).Distinct().OrderBy(s => s).ToList();
            if (nonBossSpriteIndices.Count > 0)
            {
                availableSprites = availableSprites.Where(idx => nonBossSpriteIndices.Contains(idx)).OrderBy(s => s).ToList();
                if (availableSprites.Count == 0)
                    availableSprites = nonBossSpriteIndices;
            }
        }
        if (!wave.isBossWave && availableSprites.Count == 0)
            availableSprites.Add(1);

        float spawnZ = unitRoot != null ? unitRoot.position.z : 0f;
        // 交战点必须在英雄前方可见距离内：远锚点只作参考，避免怪刷在屏外像「没刷」
        float heroCombatX = hero != null ? UnitBase.GetCombatX(hero) : GetStageStartX();
        float preferX = GetMonsterEngageBaseX(heroCombatX);
        float engageBaseX = preferX;
        if (wave.spawnAnchor != null)
        {
            float ax = wave.spawnAnchor.position.x;
            if (ax > heroCombatX + 2f && ax < heroCombatX + 10f)
                engageBaseX = ax;
            else
                engageBaseX = Mathf.Clamp(ax, preferX, GetMonsterEngageBaseX(heroCombatX) + 1.5f);
        }
        else if (wave.triggerX > -900f)
        {
            float tx = wave.triggerX;
            engageBaseX = (tx > heroCombatX + 2f && tx < heroCombatX + 10f)
                ? tx
                : preferX;
        }
        ExtendCameraMaxX(engageBaseX + 6f);

        if (_spawnWaveCo != null)
        {
            StopCoroutine(_spawnWaveCo);
            _spawnWaveCo = null;
        }
        _spawnWaveCo = StartCoroutine(CoSpawnWaveMonsters(
            wave, waveIndex, stageIdx, stageType, waveScaleMultiplier,
            engageBaseX, heroCombatX, spawnZ));
    }

    static readonly float[] AroundEngageOffsets = { -2.2f, 2.15f, -0.85f, 0.95f, -3.1f, 3.0f };

    System.Collections.IEnumerator CoSpawnWaveMonsters(
        WaveData wave, int waveIndex, int stageIdx, StageType stageType, float waveScaleMultiplier,
        float engageBaseX, float heroCombatX, float spawnZ)
    {
        if (Rules.UseTutorialSprites)
            _tutorialWaveMonsterCount = wave != null ? wave.monsterCount : 0;
        float waveSpacing = GetMonsterWaveSpacing();
        var usedSpritesThisWave = new System.Collections.Generic.HashSet<int>();
        var usedLanes = new System.Collections.Generic.List<float>();
        float stagger = wave != null && wave.staggerOverride >= 0f
            ? wave.staggerOverride
            : Rules.SpawnStagger;
        float anchorX = wave != null && wave.HasEngageAnchor ? wave.engageAnchorX : engageBaseX;

        for (int i = 0; i < wave.monsterCount; i++)
        {
            if (i > 0 && stagger > 0.0001f)
                yield return new WaitForSeconds(stagger);

            if (wave == null || BattleLootMode.Active)
                yield break;

            // Boss 关变体「双护法」：本波最前 N 个名额是精英亲卫（非 Boss 模板），Boss 本体最后出场
            bool guardSlot = wave.isBossWave && wave.bossGuardCount > 0 && i < wave.bossGuardCount;

            if (!TryPickWaveMonster(stageIdx, i, wave.isBossWave, usedSpritesThisWave,
                    out MonsterConfig template, out int spriteIndexOverride, waveIndex, stageType, guardSlot))
            {
                Debug.LogWarning($"[BattleManager] wave monster pick fail i={i}");
                continue;
            }

            float monsterScale = waveScaleMultiplier;
            if (template.isBoss && !wave.isBossWave)
                monsterScale = GameConfig.BOSS_SCALE_MULTIPLIER;
            bool tutorialEliteSlot = Rules.UseTutorialSprites && _tutorialEliteCount > 0
                && i >= wave.monsterCount - _tutorialEliteCount;
            if (tutorialEliteSlot)
            {
                monsterScale = GameConfig.ELITE_SCALE_MULTIPLIER;
                // 【2026-10-10 主人拍板】引导精英不许是蘑菇：强制用本章精英专属精灵（Forest-07 树精）。
                spriteIndexOverride = GameConfig.TUTORIAL_ELITE_SPRITE_ID;
            }

            // 原型精英额度：占本波最后一个/几个名额（不是额外追加），与引导关同一套口径
            var arch = GetWaveArchetype(waveIndex);
            int eliteQuota = (arch != null ? arch.elite : 0) + (wave != null ? wave.eliteBonus : 0);
            bool archEliteSlot = eliteQuota > 0 && i >= wave.monsterCount - eliteQuota;
            if (archEliteSlot)
                monsterScale = GameConfig.ELITE_SCALE_MULTIPLIER;
            // 亲卫：强制精英体型（同章普通怪模板 + 精英缩放），Boss 本体仍走 Boss 体型
            if (guardSlot)
                monsterScale = GameConfig.ELITE_SCALE_MULTIPLIER;

            GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
            bool fromLeft = false;
            float preferEngageX;
            if (wave.aroundAnchor)
            {
                float ox = AroundEngageOffsets[i % AroundEngageOffsets.Length] + Random.Range(-0.45f, 0.45f);
                preferEngageX = anchorX + ox;
                fromLeft = preferEngageX < heroCombatX;
            }
            else if (wave.bilateralEnter)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float engageCenter = Mathf.Clamp(anchorX, visMin + 1f, visMax - 1f);
                preferEngageX = engageCenter + side * Random.Range(1.2f, 3.2f) + Random.Range(-0.5f, 0.5f);
                fromLeft = preferEngageX < heroCombatX;
            }
            else
            {
                fromLeft = false;
                preferEngageX = engageBaseX + i * waveSpacing + Random.Range(-0.22f, 0.22f);
            }

            float lane = (wave.aroundAnchor || wave.bilateralEnter)
                ? BattleLaneBounds.PickSpreadLane(usedLanes)
                : BattleLaneBounds.LaneSlot(i, wave.monsterCount);
            if (wave.aroundAnchor || wave.bilateralEnter)
                usedLanes.Add(lane);
            if (wave.spawnAnchor != null)
                spawnZ = wave.spawnAnchor.position.z;

            Vector3 engagePos = new Vector3(preferEngageX, UnitBase.GROUND_Y + lane, spawnZ);
            // 词缀偏好：只在本次实例化期间挂上（Monster.Init 内同步 Roll 词缀），出完立刻清，
            // 避免把「分裂」之类的偏好泄漏给下一只普通精英。
            bool eliteUnit = monsterScale >= GameConfig.ELITE_SCALE_MULTIPLIER - 0.05f;
            bool hasBias = arch != null && !string.IsNullOrEmpty(arch.affixBias) && eliteUnit;
            if (hasBias) MonsterAffixDefs.SetPendingBias(arch.affixBias);
            // 【2026-10-05 主人拍板「多点出生点」】普通波按序号把入场起点往外错开，
            // 一整波从远近不同位置一起涌进来（观感「成群压过来」，也不再需要靠长 stagger 排队）。
            // 围攻/夹击波本来就是同帧同时到场的设计，保持 offset=0 不动。
            float enterDepth = (wave.aroundAnchor || wave.bilateralEnter)
                ? 0f
                : i * GameConfig.MONSTER_ENTER_DEPTH_STEP;
            Monster m = SpawnMonsterOffscreenEnter(
                engagePos, lane, monsterScale, template, stageIdx, spriteIndexOverride,
                wave.forcedTarget, fromLeft, enterDepth);
            if (hasBias) MonsterAffixDefs.ClearPendingBias();
            if (m != null)
            {
                ForceEnableMonsterRenderers(m.transform);
                wave.aliveCount++;
            }
        }

        // 记账：这一波用到过哪些精灵，下一波优先换一批（只影响外观）
        _lastWaveSprites.Clear();
        _lastWaveSprites.UnionWith(usedSpritesThisWave);

        GamePerf.Log($"[BattleManager] wave {waveIndex + 1} spawned {wave.monsterCount} @x={engageBaseX:F1}");

        // Boss 波：变体「裂隙涌动」的软性限时压力（开打 delay 秒后周期性增援，最多 cap 只）
        if (wave != null && wave.isBossWave && wave.bossPressureDelay > 0f && wave.bossPressureCap > 0)
        {
            StopCoroutine(_bossPressureCo);
            _bossPressureCo = StartCoroutine(CoBossPressureReinforce(wave, stageIdx));
        }

        _spawnWaveCo = null;
    }

    /// <summary>
    /// Boss 关变体「裂隙涌动」：开打 delay 秒后，每 interval 秒从右侧增援 1 只小怪，最多 cap 只。
    /// 软性限时 —— 不设失败条件，只加压；Boss 波清空 / 战斗结束 / 撤离时立刻停。
    /// </summary>
    System.Collections.IEnumerator CoBossPressureReinforce(WaveData wave, int stageIdx)
    {
        float delay = wave.bossPressureDelay;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        int spawned = 0;
        while (wave != null && spawned < wave.bossPressureCap)
        {
            if (!isInBattle || BattleLootMode.Active) break;
            if (CountAliveMonsters() <= 0) break;   // Boss 波已清空，不再加压

            SpawnBossPressureMonster(wave, stageIdx);
            spawned++;
            if (!string.IsNullOrEmpty(wave.bossVariantTelegraph))
                GlobalToastUI.Show(wave.bossVariantTelegraph);

            yield return new WaitForSeconds(Mathf.Max(1f, wave.bossPressureInterval));
        }
        _bossPressureCo = null;
    }

    /// <summary>涌怪：同章普通怪模板、默认体型、右侧进场，位置取英雄前方可见处。</summary>
    void SpawnBossPressureMonster(WaveData wave, int stageIdx)
    {
        var used = new System.Collections.Generic.HashSet<int>();
        if (!TryPickWaveMonster(stageIdx, 0, false, used,
                out MonsterConfig template, out int spriteIndexOverride))
            return;

        float heroCombatX = hero != null ? UnitBase.GetCombatX(hero) : GetStageStartX();
        float engageBaseX = GetMonsterEngageBaseX(heroCombatX);
        GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
        float lane = BattleLaneBounds.LaneSlot(0, 3);
        float spawnZ = unitRoot != null ? unitRoot.position.z : 0f;
        var engagePos = new Vector3(
            Mathf.Clamp(engageBaseX, visMin + 0.5f, visMax - 0.5f),
            UnitBase.GROUND_Y + lane,
            spawnZ);

        Monster m = SpawnMonsterOffscreenEnter(engagePos, lane, 1f, template, stageIdx, spriteIndexOverride, null, false);
        if (m != null)
        {
            ForceEnableMonsterRenderers(m.transform);
            wave.aliveCount++;
            GamePerf.Log($"[BossVariant] 裂隙涌动：增援 1 只（已 {wave.aliveCount}）");
        }
    }

    /// <summary>兜底怪物：刷在英雄前方可见处</summary>
    void SpawnFallbackWave(WaveData wave, int waveIndex = 0)
    {
        float fallbackScale = 1f;
        if (wave.isBossWave)
            fallbackScale = GameConfig.BOSS_SCALE_MULTIPLIER;
        else if (currentStage != null && currentStage.type == StageType.Elite)
            fallbackScale = GameConfig.ELITE_SCALE_MULTIPLIER;

        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        var availableSprites = ConfigManager.Instance != null
            ? ConfigManager.Instance.GetAvailableSpriteIndices(CurrentChapter, stageIdx, wave.isBossWave)
            : new System.Collections.Generic.List<int> { 1 };

        float heroCombatX = hero != null ? UnitBase.GetCombatX(hero) : GetStageStartX();
        float engageBaseX = GetMonsterEngageBaseX(heroCombatX);
        ExtendCameraMaxX(engageBaseX + 6f);

        float waveSpacing = GetMonsterWaveSpacing();

        for (int i = 0; i < wave.monsterCount; i++)
        {
            float lane = BattleLaneBounds.LaneSlot(i, wave.monsterCount);
            float spawnY = UnitBase.GROUND_Y + lane;
            float spawnZ = unitRoot != null ? unitRoot.position.z : 0f;
            GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
            bool fromLeft = false; // 默认从右边进场
            float preferEngageX = engageBaseX + i * waveSpacing + Random.Range(-0.22f, 0.22f);
            float engageX = ResolveEngageXForEnterSide(heroCombatX, fromLeft, preferEngageX, visMin, visMax);
            const float offMargin = 1.35f;
            float enterX = fromLeft ? visMin - offMargin : visMax + offMargin;
            Vector3 pos = new Vector3(enterX, spawnY, spawnZ);
            Vector3 engage = new Vector3(engageX, spawnY, spawnZ);

            GameObject go = null;
            if (PoolManager.Instance != null)
                go = PoolManager.Instance.Get("Monster", pos, Quaternion.identity);
            if (go == null && PoolManager.Instance != null && PoolManager.Instance._monsterPrefab != null)
            {
                go = Object.Instantiate(PoolManager.Instance._monsterPrefab, pos, Quaternion.identity);
                PoolManager.Instance.RegisterExternal(go, "Monster");
            }

            if (go == null)
            {
                go = new GameObject("FallbackMonster");
                go.transform.position = pos;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                Texture2D tex = new Texture2D(32, 32);
                Color[] pixels = new Color[32 * 32];
                for (int p = 0; p < pixels.Length; p++) pixels[p] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
                sr.color = new Color(0.9f, 0.3f, 0.2f, 1f);
                sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                sr.sortingOrder = GameConfig.SORT_UNIT;
                go.transform.localScale = Vector3.one * 3f;
            }

            if (unitRoot != null)
                go.transform.SetParent(unitRoot, true);

            Monster monster = go.GetComponent<Monster>();
            if (monster == null) monster = go.AddComponent<Monster>();

            MonsterConfig fallbackCfg = ScriptableObject.CreateInstance<MonsterConfig>();
            fallbackCfg.id = "fallback_slime";
            fallbackCfg.baseHp = 30f;
            fallbackCfg.baseAttack = 5f;
            fallbackCfg.attackRange = GameConfig.RANGE_PX_SWORD; // 像素，Init 里 Normalize
            fallbackCfg.baseAttackSpeed = 1.2f;
            fallbackCfg.isBoss = wave.isBossWave;
            fallbackCfg.spriteScale = 1f;
            fallbackCfg.spriteIndex = 1;

            int fallbackSpriteOverride = 1;
            if (availableSprites != null && availableSprites.Count > 0)
            {
                if (ConfigManager.Instance != null)
                    fallbackSpriteOverride = ConfigManager.Instance.PickWeightedSpriteIndex(availableSprites, stageIdx);
                else
                    fallbackSpriteOverride = availableSprites[i % availableSprites.Count];
            }

            monster.Init(fallbackCfg, 0, CurrentChapter, fallbackScale, fallbackSpriteOverride);
            ApplyTutorialMonsterTuning(monster);
            monster.SetLaneY(lane);
            monster.BeginMapEnter(engage);
            monster.OnDead += OnMonsterDead;
            monsters.Add(monster);
            _totalMonstersSpawnedThisStage++;
            wave.aliveCount++;
        }
        // 【2026-10-05 波次排查】兜底波是同步刷的，和正常路径 SpawnMonster 一样必须失效存活缓存：
        // CountAliveMonsters 按帧缓存，不失效的话 SpawnNextPendingWave 的 aliveAfter 会读回刷怪前的旧值
        // → 误判「无新增活怪」→ spawned=false → 反复重刷兜底波，关卡永远结算不了。
        bm.InvalidateAliveMonsterCache();
        GamePerf.Log($"[BattleManager] 兜底波次{waveIndex + 1}: {wave.monsterCount}只 @刷怪点 spawnedTotal={_totalMonstersSpawnedThisStage}");
    }

    /// <summary>
    /// 对外刷怪入口（V6 词缀「分裂」用）：在指定位置生成一只怪。
    /// 本体 SpawnMonster 是私有的，这里只做一层 public 包装，不改动既有刷怪逻辑。
    /// </summary>
    public Monster SpawnMonsterAt(MonsterConfig template, int stageIdx, Vector3 pos, float scaleMultiplier = 1f)
    {
        return SpawnMonster(template, stageIdx, pos, scaleMultiplier, 0);
    }

    Monster SpawnMonster(MonsterConfig template, int stageIdx, Vector3 pos, float scaleMultiplier = 1f, int spriteIndexOverride = 0)
    {
        if (PoolManager.Instance == null)
        {
            Debug.LogError("[BattleManager] PoolManager.Instance 为空");
            return null;
        }

        GameObject go = PoolManager.Instance.Get("Monster", pos, Quaternion.identity);
        if (go == null && PoolManager.Instance._monsterPrefab != null)
        {
            go = Object.Instantiate(PoolManager.Instance._monsterPrefab, pos, Quaternion.identity);
            PoolManager.Instance.RegisterExternal(go, "Monster");
        }

        if (go == null)
        {
            Debug.LogError("[BattleManager] 无法实例化怪物（池与预制体皆空）");
            return null;
        }

        go.SetActive(true);
        if (unitRoot != null)
            go.transform.SetParent(unitRoot, true);
        else
            GameConfig.AttachToUnitRoot(go.transform);

        Monster monster = go.GetComponent<Monster>();
        if (monster == null)
            monster = go.AddComponent<Monster>();

        monster.Init(template, stageIdx, CurrentChapter, scaleMultiplier, spriteIndexOverride);
        ApplyTutorialMonsterTuning(monster);
        // 保留调用方传入的车道 Y（GROUND_Y + LaneY），禁止钉死站立线导致 Lane 与脚底脱节
        GameConfig.SetWorldPosition(monster.GetBodyTransform(), pos);
        monster.SyncLaneYFromWorld();
        monster.OnDead += OnMonsterDead;
        monsters.Add(monster);
        _totalMonstersSpawnedThisStage++;
        bm.InvalidateAliveMonsterCache();

        GamePerf.Log($"[BattleManager] 生成怪物: {monster.name} hp={monster.currentHp:F0} 存活={CountAliveMonsters()}");
        return monster;
    }

    static void ForceEnableMonsterRenderers(Transform root)
    {
        if (root == null) return;
        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            if (!srs[i].enabled) srs[i].enabled = true;
            var c = srs[i].color;
            if (c.a < 0.05f)
            {
                c.a = 1f;
                srs[i].color = c;
            }
        }
        root.gameObject.SetActive(true);
    }
}
