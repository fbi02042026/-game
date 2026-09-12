using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 波次规划 + 刷怪执行。正式关与引导步进共用同一条 SpawnWave 轨。
/// TutorialDirector 只点 QueueTutorialStep；本类读表铺 WaveData 并出怪。
/// 方法体从 BattleManager 原样迁出，手感（人数/L-R/stagger/HP 档）保持 1:1。
/// </summary>
public sealed class WavePlanner
{
    readonly BattleManager bm;

    public WavePlanner(BattleManager host)
    {
        bm = host;
    }

    Hero hero => bm.hero;
    Transform unitRoot => bm.unitRoot;
    Transform endPoint => bm.endPoint;
    Transform[] monsterSpawnPoints => bm.monsterSpawnPoints;
    List<UnitBase> monsters => bm.monsters;
    StageData currentStage => bm.currentStage;
    TutorialRules Rules => bm.Rules;
    int CurrentChapter => bm.CurrentChapter;
    bool isInBattle => bm.isInBattle;
    bool SuppressStageClear { get => bm.SuppressStageClear; set => bm.SuppressStageClear = value; }
    bool SkipLegacyOnEvacuate { get => bm.SkipLegacyOnEvacuate; set => bm.SkipLegacyOnEvacuate = value; }

    List<WaveData> _waves { get => bm._waves; set => bm._waves = value; }
    int _totalWaves { get => bm._totalWaves; set => bm._totalWaves = value; }
    bool _allWavesSpawned { get => bm._allWavesSpawned; set => bm._allWavesSpawned = value; }
    int _activeWaveIndex { get => bm._activeWaveIndex; set => bm._activeWaveIndex = value; }
    bool _firstWaveSpawned { get => bm._firstWaveSpawned; set => bm._firstWaveSpawned = value; }
    int _tutorialSpriteMelee { get => bm._tutorialSpriteMelee; set => bm._tutorialSpriteMelee = value; }
    int _tutorialSpriteRanged { get => bm._tutorialSpriteRanged; set => bm._tutorialSpriteRanged = value; }
    int _tutorialEliteCount { get => bm._tutorialEliteCount; set => bm._tutorialEliteCount = value; }
    int _tutorialWaveMonsterCount { get => bm._tutorialWaveMonsterCount; set => bm._tutorialWaveMonsterCount = value; }
    float _tutorialHpMin { get => bm._tutorialHpMin; set => bm._tutorialHpMin = value; }
    float _tutorialHpMax { get => bm._tutorialHpMax; set => bm._tutorialHpMax = value; }
    float _tutorialEliteHpMin { get => bm._tutorialEliteHpMin; set => bm._tutorialEliteHpMin = value; }
    float _tutorialEliteHpMax { get => bm._tutorialEliteHpMax; set => bm._tutorialEliteHpMax = value; }
    bool _tutorialHpFromTable { get => bm._tutorialHpFromTable; set => bm._tutorialHpFromTable = value; }
    Coroutine _spawnWaveCo { get => bm._spawnWaveCo; set => bm._spawnWaveCo = value; }
    int _totalMonstersSpawnedThisStage { get => bm._totalMonstersSpawnedThisStage; set => bm._totalMonstersSpawnedThisStage = value; }
    int _offscreenEnterSideToggle { get => bm._offscreenEnterSideToggle; set => bm._offscreenEnterSideToggle = value; }

    float GetStageStartX() => bm.GetStageStartX();
    int CountAliveMonsters() => bm.CountAliveMonsters();
    int GetAliveMonsterCount() => bm.GetAliveMonsterCount();
    void RefreshStageQuestProgress() => bm.RefreshStageQuestProgress();
    void StopWaveCountdown() => bm.StopWaveCountdown();
    void ExtendCameraMaxX(float x) => bm.ExtendCameraMaxX(x);
    void EnsureMonsterPrefabReady() => bm.EnsureMonsterPrefabReady();
    void RetargetAllMonsters(UnitBase t) => bm.RetargetAllMonsters(t);
    void OnMonsterDead(UnitBase unit) => bm.OnMonsterDead(unit);
    static void GetBattleVisibleX(out float minX, out float maxX, float pad = 0.7f) =>
        BattleManager.GetBattleVisibleX(out minX, out maxX, pad);
    Coroutine StartCoroutine(IEnumerator routine) => bm.StartCoroutine(routine);
    void StopCoroutine(Coroutine routine)
    {
        if (routine != null) bm.StopCoroutine(routine);
    }

    // ---- extracted from BattleManager L565-L698 ----
    internal void PrepareTutorialWaves()
    {
        _waves.Clear();
        _totalWaves = 0;
        _allWavesSpawned = false;
        _activeWaveIndex = -1;
        _firstWaveSpawned = true; // 关掉 Update/过场后的硬刷；真正刷怪只走 TutorialDirector
        SuppressStageClear = Rules.SuppressStageClear;
        SkipLegacyOnEvacuate = Rules.SkipLegacyOnEvacuate;
        Debug.Log("[BattleManager] 引导关：波次由 TutorialDirector 分步刷，入口仍是 SpawnWave");
    }

    /// <summary>导演节拍：按 tutorial_battle 步进刷一波。count/进场方向/HP 档来自表，不由导演发明。</summary>
    public void QueueTutorialStep(int order, float? anchorX = null, UnitBase forcedTarget = null)
    {
        ApplyTutorialBattleStep(order);
        var step = TutorialBattleTable.GetStepOrDefault(order);
        bool flank = step.IsFlank;
        bool around = step.IsAround;
        int n = flank ? Mathf.Max(2, step.count) : Mathf.Max(1, step.count);
        float? ax = anchorX;
        if (!ax.HasValue && around && forcedTarget != null)
            ax = UnitBase.GetCombatX(forcedTarget);
        UnitBase target = forcedTarget;
        if (target == null && flank && hero != null)
            target = hero;
        if (around && target == null)
        {
            Debug.LogWarning("[BattleManager] around 步进缺 forcedTarget，跳过以免围空点");
            return;
        }
        QueueTutorialWaveCore(n, ax, target, flank || around, around, (flank || around) ? 0f : -1f);
    }

    public void QueueTutorialWave(int count)
    {
        QueueTutorialWaveCore(Mathf.Max(1, count), null, null, false, false, -1f);
    }

    void MarkUnspawnedWavesConsumed()
    {
        if (_waves == null) return;
        for (int i = 0; i < _waves.Count; i++)
        {
            if (_waves[i] != null && !_waves[i].spawned)
                _waves[i].spawned = true;
        }
    }

    void QueueTutorialWaveCore(
        int count, float? anchorX, UnitBase forcedTarget,
        bool bilateralEnter, bool aroundAnchor, float staggerOverride)
    {
        if (BattleLootMode.Active) return;
        MarkUnspawnedWavesConsumed();

        int n = Mathf.Max(1, count);
        float hx = hero != null ? UnitBase.GetCombatX(hero) : GetStageStartX();
        float trigger = (anchorX ?? hx) + (bilateralEnter || aroundAnchor ? 0f : GameConfig.MONSTER_ENGAGE_OFFSET);
        var wave = new WaveData
        {
            triggerX = trigger,
            spawnAnchor = null,
            monsterCount = n,
            isBossWave = false,
            spawned = false,
            aliveCount = 0,
            engageAnchorX = anchorX ?? -999f,
            bilateralEnter = bilateralEnter,
            aroundAnchor = aroundAnchor,
            forcedTarget = forcedTarget,
            staggerOverride = staggerOverride
        };
        if (_waves == null) _waves = new List<WaveData>();
        _waves.Add(wave);
        _totalWaves = _waves.Count;
        _allWavesSpawned = false;
        _activeWaveIndex = -1;
        SuppressStageClear = Rules.SuppressStageClear;

        int waveIdx = _waves.Count - 1;
        int spawnedBefore = wave.aliveCount;
        int aliveBefore = CountAliveMonsters();
        TrySpawnTutorialWave(wave, waveIdx, spawnedBefore, aliveBefore);

        if (CountAliveMonsters() == 0)
        {
            if (bilateralEnter && !aroundAnchor)
            {
                EmergencySpawnFlankAmbush(n, anchorX ?? hx);
                if (forcedTarget != null) RetargetAllMonsters(forcedTarget);
            }
            else
            {
                EmergencySpawnVisibleMonsters(aroundAnchor ? n : Mathf.Min(n, 6));
                if (forcedTarget != null) RetargetAllMonsters(forcedTarget);
            }
            if (CountAliveMonsters() > 0)
            {
                wave.spawned = true;
                _activeWaveIndex = waveIdx;
            }
            wave.aliveCount = GetAliveMonsterCount();
        }
        RefreshStageQuestProgress();
    }

    void TrySpawnTutorialWave(WaveData wave, int waveIdx, int spawnedBefore, int aliveBefore)
    {
        if (wave == null) return;
        EnsureMonsterPrefabReady();
        try
        {
            SpawnWave(wave, waveIdx);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] 教程波刷怪异常: {e.Message}");
        }

        if (wave.aliveCount > spawnedBefore || CountAliveMonsters() > aliveBefore)
        {
            wave.spawned = true;
            _activeWaveIndex = waveIdx;
            StopWaveCountdown();
            GamePerf.Log($"[BattleManager] 教程波 {waveIdx + 1} OK alive={CountAliveMonsters()} spawn={wave.aliveCount}");
            RefreshStageQuestProgress();
        }
        else
        {
            wave.spawned = false;
            Debug.LogWarning($"[BattleManager] 教程波 {waveIdx + 1} 常规刷怪未出怪，将走 Emergency");
        }
    }

    // ---- extracted from BattleManager L736-L852 ----
    /// <summary>在受害者周围刷怪并强制锁定打他（引导围殴）。走 SpawnWave 参数，不再另开刷怪轨。</summary>
    public void SpawnTutorialAmbushAround(UnitBase victim, int count)
    {
        if (victim == null) return;
        float vx = UnitBase.GetCombatX(victim);
        QueueTutorialWaveCore(Mathf.Max(1, count), vx, victim, true, true, 0f);
    }

    /// <summary>诱饵埋伏：从锚点左右两侧刷怪。走 SpawnWave 参数（bilateralEnter）。</summary>
    public void SpawnTutorialFlankAmbush(int count, float? anchorX = null)
    {
        if (hero == null) return;
        QueueTutorialWaveCore(Mathf.Max(2, count), anchorX ?? UnitBase.GetCombatX(hero), hero, true, false, 0f);
    }

    /// <summary>兜底：在锚点左右对称刷怪（避免只刷右侧）。</summary>
    void EmergencySpawnFlankAmbush(int count, float anchorX)
    {
        if (hero == null) return;
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;
        GetBattleVisibleX(out float visMin, out float visMax);
        float engageCenter = Mathf.Clamp(anchorX, visMin + 1f, visMax - 1f);
        int n = Mathf.Max(2, count);
        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        var usedSprites = new System.Collections.Generic.HashSet<int>();
        var usedLanes = new System.Collections.Generic.List<float>();
        for (int i = 0; i < n; i++)
        {
            float side = (i % 2 == 0) ? -1f : 1f;
            float ox = engageCenter + side * Random.Range(1.0f, 2.8f) + Random.Range(-0.4f, 0.4f);
            float lane = BattleLaneBounds.PickSpreadLane(usedLanes);
            usedLanes.Add(lane);
            Vector3 engagePos = new Vector3(ox, UnitBase.GROUND_Y + lane, z);
            if (!TryPickWaveMonster(stageIdx, monsters.Count + i, false, usedSprites, out MonsterConfig cfg, out int spriteIdx))
                continue;
            bool fromLeft = engagePos.x < engageCenter;
            Monster monster = SpawnMonsterOffscreenEnter(engagePos, lane, 1f, cfg, stageIdx, spriteIdx, null, fromLeft);
            if (monster == null) continue;
            monster.SetForcedTarget(hero);
            ApplyTutorialMonsterTuning(monster);
            ForceEnableMonsterRenderers(monster.transform);
        }
    }

    /// <summary>埋伏/教程：在交战点刷怪，从屏外走入。</summary>
    Monster SpawnAmbushMonsterAt(
        Vector3 engagePos, float laneY, float scaleMultiplier = 1f,
        System.Collections.Generic.HashSet<int> usedSprites = null)
    {
        EnsureMonsterPrefabReady();
        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        if (!TryPickWaveMonster(stageIdx, usedSprites != null ? usedSprites.Count : monsters.Count, false, usedSprites,
                out MonsterConfig cfg, out int spriteIdx))
            return null;

        // 宝箱/佣兵伏击：落点在左则从左进场，在右则从右进场（普通波仍默认右侧）
        float heroX = hero != null ? UnitBase.GetCombatX(hero) : engagePos.x;
        bool fromLeft = engagePos.x < heroX;
        Monster monster = SpawnMonsterOffscreenEnter(
            engagePos, laneY, scaleMultiplier, cfg, stageIdx, spriteIdx, null, fromLeft);
        if (monster == null) return null;
        ApplyTutorialMonsterTuning(monster);
        ForceEnableMonsterRenderers(monster.transform);
        return monster;
    }

    Monster SpawnMonsterOffscreenEnter(
        Vector3 engagePos, float laneY, float scaleMultiplier,
        MonsterConfig cfg, int stageIdx, int spriteIdx, UnitBase forcedTarget = null,
        bool? fromLeftOverride = null)
    {
        if (cfg == null) return null;
        float z = unitRoot != null ? unitRoot.position.z : engagePos.z;
        GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
        // 相对镜头左右交替进场（可覆盖）
        bool fromLeft = fromLeftOverride ?? false; // 默认右侧进场；仅显式 override 才从左
        const float offscreenMargin = 1.35f;
        float enterX = fromLeft ? visMin - offscreenMargin : visMax + offscreenMargin;
        // 交战点落在进场同侧，避免左侧怪跑到玩家右边再回头
        float heroX = hero != null ? UnitBase.GetCombatX(hero) : engagePos.x;
        engagePos.x = ResolveEngageXForEnterSide(heroX, fromLeft, engagePos.x, visMin, visMax);
        engagePos.y = UnitBase.GROUND_Y + laneY;
        Vector3 enterFrom = new Vector3(enterX, engagePos.y, z);

        Monster monster = SpawnMonster(cfg, stageIdx, enterFrom, scaleMultiplier, spriteIdx);
        if (monster == null) return null;
        monster.SetLaneY(laneY);
        if (forcedTarget != null)
            monster.SetForcedTarget(forcedTarget);
        monster.BeginMapEnter(engagePos, GameConfig.MONSTER_ENTER_SPEED, fromLeft ? 1 : -1);
        return monster;
    }

    /// <summary>
    /// 左进场停在英雄左侧，右进场停在英雄右侧；preferredX 仅作同侧微调参考。
    /// </summary>
    float ResolveEngageXForEnterSide(float heroX, bool fromLeft, float preferredX, float visMin, float visMax)
    {
        float ahead = Rules.ResolveEngageAhead(GameConfig.MONSTER_ENGAGE_OFFSET);
        float sideX = fromLeft ? heroX - ahead : heroX + ahead;
        // 同侧优先用 preferred（波次间距），但不得跨过英雄
        if (fromLeft)
        {
            if (preferredX < heroX - 0.6f)
                sideX = preferredX;
            sideX = Mathf.Min(sideX, heroX - 0.85f);
        }
        else
        {
            if (preferredX > heroX + 0.6f)
                sideX = preferredX;
            sideX = Mathf.Max(sideX, heroX + 0.85f);
        }
        return Mathf.Clamp(sideX, visMin + 0.45f, visMax - 0.45f);
    }

    // ---- extracted from BattleManager L876-L910 ----
    void ApplyTutorialMonsterTuning(Monster monster)
    {
        if (!Rules.ApplyTableMonsterHp || !_tutorialHpFromTable || monster == null || monster.attr == null)
            return;

        float min;
        float max;
        if (monster.IsEliteWave)
        {
            min = _tutorialEliteHpMin;
            max = _tutorialEliteHpMax;
        }
        else
        {
            min = _tutorialHpMin;
            max = _tutorialHpMax;
        }
        if (min <= 0f && max <= 0f) return;
        if (max <= 0f) max = min;
        if (min <= 0f) min = max;

        // 整型档沿用旧 Random.Range(minInclusive, maxExclusive)；否则闭区间 float。
        float rolled;
        bool intRange = Mathf.Approximately(min, Mathf.Round(min)) && Mathf.Approximately(max, Mathf.Round(max));
        if (intRange && max > min)
            rolled = Random.Range((int)min, (int)max);
        else if (Mathf.Approximately(min, max))
            rolled = min;
        else
            rolled = Random.Range(min, max);

        float hp = rolled * GameConfig.MONSTER_HP_GLOBAL_MUL;
        monster.attr.SetAttr(AttrType.MaxHp, hp);
        monster.currentHp = hp;
    }

    // ---- extracted from BattleManager L1059-L1166 ----
    internal void ForceSpawnFirstWaveNow()
    {
        if (hero == null)
        {
            Debug.LogError("[BattleManager] ForceSpawn：hero 为空");
            return;
        }
        if (_waves == null)
            _waves = new List<WaveData>();
        if (_waves.Count == 0)
        {
            Debug.LogError("[BattleManager] ForceSpawn：波次列表为空，补 1 波");
            _waves.Add(new WaveData
            {
                triggerX = UnitBase.GetCombatX(hero) + GameConfig.MONSTER_ENGAGE_OFFSET,
                spawnAnchor = null,
                monsterCount = 2,
                isBossWave = false,
                spawned = false,
                aliveCount = 0
            });
            _totalWaves = _waves.Count;
        }

        EnsureMonsterPrefabReady();
        _activeWaveIndex = -1;

        int before = CountAliveMonsters();
        try
        {
            SpawnNextPendingWave();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] ForceSpawn TrySpawn 异常: {e}");
        }

        int after = CountAliveMonsters();
        Debug.Log($"[BattleManager] ForceSpawn 第1轮 beforeAlive={before} afterAlive={after} totalList={monsters.Count} waves={_waves.Count} prefab={(PoolManager.Instance != null && PoolManager.Instance._monsterPrefab != null)}");

        if (after <= 0)
        {
            var w = _waves[0];
            w.spawned = false;
            try
            {
                SpawnFallbackWave(w, 0);
                w.spawned = CountAliveMonsters() > 0;
                _activeWaveIndex = 0;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleManager] ForceSpawn 兜底异常: {e}");
            }
        }

        after = CountAliveMonsters();
        if (after <= 0)
        {
            Debug.LogError("[BattleManager] 常规刷怪全失败 → EmergencySpawnVisibleMonsters");
            EmergencySpawnVisibleMonsters(2);
            after = CountAliveMonsters();
        }

        if (_waves.Count > 0 && _waves[0] != null && after > 0)
        {
            _waves[0].spawned = true;
            _activeWaveIndex = 0;
        }

        StopWaveCountdown();
        Debug.Log($"[BattleManager] ForceSpawn 最终 alive={after} list={monsters.Count}");
    }

    /// <summary>
    /// 最后手段：不走对象池/配置表，当场造 2 只可见怪物。
    /// 用于证明「刷怪入口已执行」；若连这个都没有，说明根本没进 ForceSpawn。
    /// </summary>
    internal void EmergencySpawnVisibleMonsters(int count)
    {
        if (hero == null) return;
        float hx = UnitBase.GetCombatX(hero);
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;

        // 至少刷在玩家身前一段距离，避免叠在身上穿模
        float baseX = Mathf.Max(GetMonsterEngageBaseX(hx), hx + 4.5f);
        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
        var usedSprites = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            float x = baseX + i * GetMonsterWaveSpacing() + Random.Range(-0.22f, 0.22f);
            float lane = BattleLaneBounds.LaneSlot(i, count);
            Vector3 engagePos = new Vector3(x, UnitBase.GROUND_Y + lane, z);

            if (!TryPickWaveMonster(stageIdx, i, false, usedSprites, out MonsterConfig cfg, out int spriteIdx))
            {
                Debug.LogWarning("[BattleManager] Emergency 无法从配置表取怪，跳过");
                continue;
            }

            Monster monster = SpawnMonsterOffscreenEnter(engagePos, lane, 1f, cfg, stageIdx, spriteIdx);
            if (monster == null) continue;

            ApplyTutorialMonsterTuning(monster);
            ForceEnableMonsterRenderers(monster.transform);
            Debug.Log($"[BattleManager] Emergency 怪 cfg={cfg.id} sprite={spriteIdx} pos={monster.transform.position}");
        }
    }

    // ---- extracted from BattleManager L1183-L1201 ----
    /// <summary>波次为空时强制补一波（兜底）</summary>
    internal void EnsureAtLeastOneWave()
    {
        if (_waves == null) _waves = new List<WaveData>();
        if (_waves.Count > 0 && FindNextUnspawnedWaveIndex() >= 0) return;

        float heroX = hero != null ? UnitBase.GetCombatX(hero) : GetStageStartX();
        _waves.Add(new WaveData
        {
            triggerX = heroX + GameConfig.MONSTER_ENGAGE_OFFSET,
            spawnAnchor = null,
            monsterCount = 3,
            isBossWave = false,
            spawned = false,
            aliveCount = 0
        });
        _totalWaves = _waves.Count;
        Debug.LogWarning("[BattleManager] EnsureAtLeastOneWave 补 1 波 3 只");
    }

    // ---- extracted from BattleManager L1246-L1260 ----
    float GetMonsterEngageBaseX(float heroCombatX)
    {
        // 引导关拉远一点，避免一刷就叠在玩家身上穿模
        float minAhead = Rules.EngageMinAhead;
        float prefer = heroCombatX + Rules.ResolveEngageAhead(GameConfig.MONSTER_ENGAGE_OFFSET);
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return prefer;

        float halfW = cam.orthographicSize * cam.aspect;
        float camRight = cam.transform.position.x + halfW;
        // 贴在镜头右缘内侧，避免刷在屏外像「没怪」
        float visible = camRight - 1.2f;
        float clamped = Mathf.Clamp(prefer, heroCombatX + minAhead, Mathf.Max(visible, heroCombatX + minAhead));
        return clamped;
    }

    // ---- extracted from BattleManager L1543-L1588 ----
    internal void SpawnNextPendingWave()
    {
        if (_waves == null || _waves.Count == 0 || hero == null) return;
        EnsureMonsterPrefabReady();

        int waveIdx = FindNextUnspawnedWaveIndex();
        if (waveIdx < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            return;
        }

        // 场上还有上一波活怪时，不叠刷（教程关由 QueueTutorialWave 重置 _activeWaveIndex）
        if (!Rules.AllowStackSpawnWhileAlive && CountAliveMonsters() > 0 && _activeWaveIndex >= 0)
            return;

        var wave = _waves[waveIdx];
        float heroX = UnitBase.GetCombatX(hero);
        try
        {
            int aliveBefore = CountAliveMonsters();
            SpawnWave(wave, waveIdx);
            int aliveAfter = CountAliveMonsters();
            if (aliveAfter > aliveBefore)
            {
                wave.spawned = true;
                _activeWaveIndex = waveIdx;
                StopWaveCountdown();
                GamePerf.Log($"[BattleManager] 刷第{waveIdx + 1}/{_waves.Count}波 OK alive={aliveAfter} heroX={heroX:F2}");
            }
            else
            {
                wave.spawned = false;
                Debug.LogError($"[BattleManager] 刷第{waveIdx + 1}波 SpawnWave 返回但场上无新增活怪");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] 第{waveIdx + 1}波刷怪异常: {e}");
            wave.spawned = false;
        }

        if (FindNextUnspawnedWaveIndex() < 0)
            _allWavesSpawned = true;
    }

    // ---- extracted from BattleManager L1700-L1707 ----
    internal int FindNextUnspawnedWaveIndex()
    {
        if (_waves == null) return -1;
        for (int i = 0; i < _waves.Count; i++)
            if (_waves[i] != null && !_waves[i].spawned)
                return i;
        return -1;
    }

    // ---- extracted from BattleManager L1805-L1975 ----
    internal void SetupNormalWaves(int stageIdx)
    {
        BuildCombatWaves(stageIdx, elite: false);
    }

    /// <summary>
    /// 精英关：同随机总量；属性倍率在 SpawnWave 里乘 ELITE_SCALE。
    /// </summary>
    internal void SetupEliteWaves(int stageIdx)
    {
        BuildCombatWaves(stageIdx, elite: true);
    }

    void BuildCombatWaves(int stageIdx, bool elite)
    {
        float startX = GetStageStartX();
        var points = GetSpawnPointsSortedByX();
        var usable = new List<Transform>();
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null && points[i].position.x >= startX - 2f)
                usable.Add(points[i]);
        }

        int total = elite
            ? GameConfig.GetEliteStageMonsterTotal(stageIdx)
            : GameConfig.GetNormalStageMonsterTotal(stageIdx);
        int waveCountMin = GameConfig.STAGE_WAVE_MIN;
        int waveCountMax = GameConfig.STAGE_WAVE_MAX;
        StageType spawnType = elite ? StageType.Elite : StageType.Normal;
        if (StageSpawnTable.TryResolve(CurrentChapter, stageIdx, spawnType, out var spawnRule))
        {
            if (!spawnRule.useFormulaForTotal && spawnRule.monsterTotal > 0)
                total = spawnRule.monsterTotal;
            waveCountMin = Mathf.Max(1, spawnRule.waveCountMin);
            waveCountMax = Mathf.Max(waveCountMin, spawnRule.waveCountMax);
        }

        int waveCount = GameConfig.GetSuggestedWaveCount(total, usable.Count);
        waveCount = Mathf.Clamp(waveCount, waveCountMin, waveCountMax);
        int[] perWave = GameConfig.DistributeMonstersToWaves(total, waveCount);

        for (int i = 0; i < waveCount; i++)
        {
            Transform anchor = (usable.Count > 0) ? usable[Mathf.Min(i, usable.Count - 1)] : null;
            float triggerX;
            if (anchor != null)
                triggerX = anchor.position.x;
            else
                triggerX = startX + 3.5f + i * GameConfig.VIRTUAL_WAVE_SPACING;

            _waves.Add(new WaveData
            {
                triggerX = triggerX,
                spawnAnchor = anchor,
                monsterCount = perWave[i],
                isBossWave = false,
                spawned = false,
                aliveCount = 0
            });
        }

        _totalWaves = _waves.Count;
        string tag = elite ? "精英关" : "普通关";
        GamePerf.Log($"[BattleManager] {tag} stage={stageIdx + 1} 总怪={total} → {_totalWaves}波 [{string.Join(",", perWave)}] 刷怪点={usable.Count} 起点={startX:F1}");
        for (int i = 0; i < _waves.Count; i++)
        {
            var w = _waves[i];
            string an = w.spawnAnchor != null ? w.spawnAnchor.name : "virtual";
            float ax = w.spawnAnchor != null ? w.spawnAnchor.position.x : w.triggerX;
            GamePerf.Log($"[BattleManager]   第{i + 1}波 anchor={an} worldX={ax:F2} count={w.monsterCount}");
        }
    }

    List<Transform> GetSpawnPointsSortedByX()
    {
        var list = new List<Transform>();
        if (monsterSpawnPoints == null) return list;
        for (int i = 0; i < monsterSpawnPoints.Length; i++)
        {
            if (monsterSpawnPoints[i] != null)
                list.Add(monsterSpawnPoints[i]);
        }
        // 按 X 排序；同 X 时按编号数字排（避免 MonsterSpawn_15 排在 _2 前面）
        list.Sort((a, b) =>
        {
            int cx = a.position.x.CompareTo(b.position.x);
            if (cx != 0) return cx;
            return ParseSpawnIndex(a.name).CompareTo(ParseSpawnIndex(b.name));
        });
        return list;
    }

    /// <summary>只取起点前方的刷怪点，避免刷在身后导致「看不见怪」</summary>
    List<Transform> GetSpawnPointsAheadOf(float startX)
    {
        var all = GetSpawnPointsSortedByX();
        var ahead = new List<Transform>();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].position.x >= startX - 0.5f)
                ahead.Add(all[i]);
        }
        return ahead;
    }

    static int ParseSpawnIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        int us = name.LastIndexOf('_');
        if (us >= 0 && us + 1 < name.Length && int.TryParse(name.Substring(us + 1), out int n))
            return n;
        return 0;
    }

    /// <summary>每波触发线：主角前方；有刷怪点则用点左侧，否则按间距铺开</summary>
    static float ResolveWaveTriggerX(int waveIndex, int waveCount, List<Transform> points, float startX, float endX)
    {
        if (points != null && points.Count > 0)
        {
            if (waveIndex < points.Count)
                return Mathf.Max(startX - 0.5f, points[waveIndex].position.x - 2.5f);

            float lastX = points[points.Count - 1].position.x;
            int extra = waveIndex - points.Count + 1;
            int extraTotal = Mathf.Max(1, waveCount - points.Count);
            float t = extra / (float)(extraTotal + 1);
            return Mathf.Lerp(lastX, endX, t) - 1f;
        }

        // 无可用刷怪点：开局即可触发第一波，怪刷在前方
        return startX - 0.5f + waveIndex * 5f;
    }

    /// <summary>Boss关：若干波小怪 + 最后 1 波 Boss</summary>
    internal void SetupBossWave(int stageIdx)
    {
        float startX = GetStageStartX();
        int minions = GameConfig.GetBossStageMinionTotal(stageIdx);
        int bossCount = GameConfig.GetBossStageMonsterTotal();

        // Boss 关小怪波：按总数抬高波次上限，最多 7 波小怪 + 1 Boss
        int minionWaves = GameConfig.GetSuggestedWaveCount(minions, 0);
        minionWaves = Mathf.Clamp(minionWaves, 3, 7);
        int[] perWave = GameConfig.DistributeMonstersToWaves(minions, minionWaves);

        for (int i = 0; i < minionWaves; i++)
        {
            _waves.Add(new WaveData
            {
                triggerX = startX + 3.5f + i * GameConfig.VIRTUAL_WAVE_SPACING,
                spawnAnchor = null,
                monsterCount = perWave[i],
                isBossWave = false,
                spawned = false,
                aliveCount = 0
            });
        }

        float bossX = endPoint != null ? endPoint.position.x - 2f : startX + 3.5f + minionWaves * GameConfig.VIRTUAL_WAVE_SPACING + 2f;
        _waves.Add(new WaveData
        {
            triggerX = bossX - 1f,
            monsterCount = bossCount,
            isBossWave = true,
            spawned = false,
            aliveCount = 0
        });
        _totalWaves = _waves.Count;
        GamePerf.Log($"[BattleManager] Boss关 stage={stageIdx + 1} 小怪={minions}×{minionWaves}波 + Boss={bossCount} X={bossX:F1}");
    }

    // ---- extracted from BattleManager L2157-L2682 ----
    /// <summary>引导关刷怪前：从 tutorial_battle 表读取近战/远程精灵编号与 HP 档。</summary>
    public void ApplyTutorialBattleStep(int order)
    {
        var step = TutorialBattleTable.GetStepOrDefault(order);
        _tutorialSpriteMelee = step.spriteMelee > 0 ? step.spriteMelee : 1;
        _tutorialSpriteRanged = step.spriteRanged > 0 ? step.spriteRanged : 2;
        _tutorialEliteCount = step.eliteCount > 0 ? step.eliteCount : 0;
        _tutorialHpFromTable = step.HasHp;
        _tutorialHpMin = step.hpMin;
        _tutorialHpMax = step.hpMax;
        _tutorialEliteHpMin = step.eliteHpMin;
        _tutorialEliteHpMax = step.eliteHpMax;
    }

    /// <summary>单波内交替近战/远程；引导关强制混刷弓/法球与近战。</summary>
    int PickTutorialSpriteIndex(System.Collections.Generic.List<int> availableSprites, int slotIndex)
    {
        if (availableSprites == null || availableSprites.Count == 0) return 1;

        // 引导固定近战:远程 ≈ 4:2（按本波人数折算，前段近战、后段远程）
        int waveN = Mathf.Max(1, _tutorialWaveMonsterCount);
        int rangedSlots = Mathf.Max(1, Mathf.RoundToInt(waveN * 2f / 6f));
        int meleeSlots = Mathf.Max(0, waveN - rangedSlots);
        bool wantRanged = slotIndex >= meleeSlots;
        int monsterChapter = GameConfig.GetMonsterChapter(CurrentChapter);
        int fallbackMelee = _tutorialSpriteMelee > 0 ? _tutorialSpriteMelee : 1;
        int fallbackRanged = _tutorialSpriteRanged > 0 ? _tutorialSpriteRanged : 2;

        for (int i = 0; i < availableSprites.Count; i++)
        {
            int idx = availableSprites[i];
            var style = MonsterAttackStyleTable.Get(monsterChapter, idx);
            if (MonsterAttackStyleTable.IsRanged(style))
                fallbackRanged = idx;
            else
                fallbackMelee = idx;
        }

        var matched = new System.Collections.Generic.List<int>();
        for (int i = 0; i < availableSprites.Count; i++)
        {
            int idx = availableSprites[i];
            var style = MonsterAttackStyleTable.Get(monsterChapter, idx);
            bool isRanged = MonsterAttackStyleTable.IsRanged(style);
            if (wantRanged == isRanged)
                matched.Add(idx);
        }
        if (matched.Count == 0)
            return wantRanged ? fallbackRanged : fallbackMelee;

        if (wantRanged)
        {
            for (int i = 0; i < matched.Count; i++)
            {
                var s = MonsterAttackStyleTable.Get(monsterChapter, matched[i]);
                if (s == MonsterAttackStyle.Bow) return matched[i];
            }
        }
        return matched[slotIndex % matched.Count];
    }

    /// <summary>单波内交替近战/远程；同波尽量不重复精灵（走配置表，不用预制体占位图）</summary>
    int PickWaveSpriteIndex(System.Collections.Generic.List<int> availableSprites, int stageIdx,
        System.Collections.Generic.List<MonsterConfig> pool, int slotIndex,
        System.Collections.Generic.HashSet<int> usedThisWave = null,
        int waveIndex = 0, StageType stageType = StageType.Normal)
    {
        if (availableSprites == null || availableSprites.Count == 0) return 1;

        if (Rules.UseTutorialSprites)
            return PickTutorialSpriteIndex(availableSprites, slotIndex);

        int chapter = CurrentChapter;
        WaveSlotTable.EnsureLoaded();
        if (WaveSlotTable.TryGetSlot(chapter, stageIdx, stageType, waveIndex, slotIndex, out var slotRule))
        {
            if (slotRule.spriteIndex > 0 && availableSprites.Contains(slotRule.spriteIndex))
                return slotRule.spriteIndex;

            var styleFiltered = FilterSpritesByStyle(availableSprites, chapter, slotRule.styleFilter);
            if (styleFiltered.Count > 0)
            {
                if (!slotRule.allowDuplicate && usedThisWave != null && usedThisWave.Count > 0)
                {
                    var unused = styleFiltered.Where(idx => !usedThisWave.Contains(idx)).ToList();
                    if (unused.Count > 0) styleFiltered = unused;
                }
                if (ConfigManager.Instance != null)
                    return ConfigManager.Instance.PickWeightedSpriteIndex(styleFiltered, stageIdx);
                return styleFiltered[slotIndex % styleFiltered.Count];
            }
        }

        bool wantRanged = slotIndex % 2 == 1;
        var filtered = new System.Collections.Generic.List<int>();
        for (int k = 0; k < availableSprites.Count; k++)
        {
            int idx = availableSprites[k];
            var style = MonsterAttackStyleTable.Get(GameConfig.GetMonsterChapter(chapter), idx);
            bool isRanged = MonsterAttackStyleTable.IsRanged(style);
            if (wantRanged == isRanged)
                filtered.Add(idx);
        }
        if (filtered.Count == 0)
            filtered.AddRange(availableSprites);

        if (usedThisWave != null && usedThisWave.Count > 0)
        {
            var unused = filtered.Where(idx => !usedThisWave.Contains(idx)).ToList();
            if (unused.Count > 0)
                filtered = unused;
        }

        if (ConfigManager.Instance == null)
            return filtered[slotIndex % filtered.Count];

        return ConfigManager.Instance.PickWeightedSpriteIndex(filtered, stageIdx);
    }

    static System.Collections.Generic.List<int> FilterSpritesByStyle(
        System.Collections.Generic.List<int> availableSprites, int chapter, string styleFilter)
    {
        var result = new System.Collections.Generic.List<int>();
        if (availableSprites == null || availableSprites.Count == 0) return result;
        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        string filter = string.IsNullOrEmpty(styleFilter) ? "Any" : styleFilter.Trim();

        for (int i = 0; i < availableSprites.Count; i++)
        {
            int idx = availableSprites[i];
            var style = MonsterAttackStyleTable.Get(monsterChapter, idx);
            if (filter.Equals("Any", System.StringComparison.OrdinalIgnoreCase))
                result.Add(idx);
            else if (filter.Equals("Bow", System.StringComparison.OrdinalIgnoreCase) && style == MonsterAttackStyle.Bow)
                result.Add(idx);
            else if (filter.Equals("Ranged", System.StringComparison.OrdinalIgnoreCase) && MonsterAttackStyleTable.IsRanged(style))
                result.Add(idx);
            else if (filter.Equals("Melee", System.StringComparison.OrdinalIgnoreCase) && !MonsterAttackStyleTable.IsRanged(style))
                result.Add(idx);
        }
        return result;
    }

    bool TryPickWaveMonster(int stageIdx, int slotIndex, bool isBossWave,
        System.Collections.Generic.HashSet<int> usedSprites,
        out MonsterConfig template, out int spriteIndexOverride,
        int waveIndex = 0, StageType stageType = StageType.Normal)
    {
        template = null;
        spriteIndexOverride = 1;
        if (ConfigManager.Instance == null) return false;

        var pool = ConfigManager.Instance.GetWaveMonsterPool(CurrentChapter, stageIdx);
        if (pool == null || pool.Count == 0) return false;

        var availableSprites = ConfigManager.Instance.GetAvailableSpriteIndices(CurrentChapter, stageIdx, isBossWave);
        if (!isBossWave)
        {
            var nonBossSprites = pool.Where(m => !m.isBoss && m.spriteIndex > 0)
                .Select(m => m.spriteIndex).Distinct().OrderBy(s => s).ToList();
            if (nonBossSprites.Count > 0)
            {
                availableSprites = availableSprites.Where(nonBossSprites.Contains).OrderBy(s => s).ToList();
                if (availableSprites.Count == 0)
                    availableSprites = nonBossSprites;
            }
        }
        if (!isBossWave && availableSprites.Count == 0)
            availableSprites.Add(1);

        if (isBossWave)
        {
            // 优先真 Boss 行（sprite 11/12）；避免高阶小怪 isBoss 误抢模板
            var trueBosses = pool.Where(m => m.isBoss && m.spriteIndex >= GameConfig.BOSS_SPRITE_START).ToList();
            if (trueBosses.Count > 0)
                template = trueBosses[Random.Range(0, trueBosses.Count)];
            else
                template = pool.Find(m => m.isBoss) ?? pool[0];
            if (template != null && template.spriteIndex >= GameConfig.BOSS_SPRITE_START)
                spriteIndexOverride = template.spriteIndex;
            else
                spriteIndexOverride = availableSprites.Count > 0 ? availableSprites[0] : GameConfig.BOSS_SPRITE_START;
        }
        else
        {
            spriteIndexOverride = PickWaveSpriteIndex(availableSprites, stageIdx, pool, slotIndex, usedSprites,
                waveIndex, stageType);
            int pickedSprite = spriteIndexOverride;
            template = pool.Find(m => !m.isBoss && m.spriteIndex == pickedSprite);
            if (template == null)
            {
                template = pool.Where(m => !m.isBoss).OrderBy(m => m.spriteIndex).FirstOrDefault() ?? pool[0];
                spriteIndexOverride = Mathf.Max(1, template.spriteIndex > 0 ? template.spriteIndex : 1);
            }
        }

        usedSprites?.Add(spriteIndexOverride);
        return template != null;
    }

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

        bool isElite = currentStage != null && currentStage.type == StageType.Elite;
        float waveScaleMultiplier = 1f;
        if (wave.isBossWave)
            waveScaleMultiplier = GameConfig.BOSS_SCALE_MULTIPLIER;
        else if (isElite)
            waveScaleMultiplier = GameConfig.ELITE_SCALE_MULTIPLIER;

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

            if (!TryPickWaveMonster(stageIdx, i, wave.isBossWave, usedSpritesThisWave,
                    out MonsterConfig template, out int spriteIndexOverride, waveIndex, stageType))
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
            Monster m = SpawnMonsterOffscreenEnter(
                engagePos, lane, monsterScale, template, stageIdx, spriteIndexOverride,
                wave.forcedTarget, fromLeft);
            if (m != null)
            {
                ForceEnableMonsterRenderers(m.transform);
                wave.aliveCount++;
            }
        }

        GamePerf.Log($"[BattleManager] wave {waveIndex + 1} spawned {wave.monsterCount} @x={engageBaseX:F1}");
        _spawnWaveCo = null;
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
            monster.BeginMapEnter(engage, GameConfig.MONSTER_ENTER_SPEED);
            monster.OnDead += OnMonsterDead;
            monsters.Add(monster);
            _totalMonstersSpawnedThisStage++;
            wave.aliveCount++;
        }
        GamePerf.Log($"[BattleManager] 兜底波次{waveIndex + 1}: {wave.monsterCount}只 @刷怪点 spawnedTotal={_totalMonstersSpawnedThisStage}");
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

        Debug.Log($"[BattleManager] 生成怪物: {go.name} parent={go.transform.parent?.name} pos={go.transform.position} scale={go.transform.localScale} lossy={go.transform.lossyScale} hp={monster.currentHp:F0}");
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
