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
        {
            // 【2026-10-19 主人拍板】无限裂隙 10 的倍数层 = 大 BOSS：在小 BOSS 体型上再放大一档
            waveScaleMultiplier = GameConfig.IsEndlessBigBossStage(BattleManager.Instance.EndlessRiftStage)
                ? GameConfig.BOSS_SCALE_MULTIPLIER * GameConfig.ENDLESS_BIGBOSS_SCALE_EXTRA
                : GameConfig.BOSS_SCALE_MULTIPLIER;
        }

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
        // 【2026-11-24】在 StartCoroutine 前同步标记 wave.spawned / _activeWaveIndex：
        // 一旦决定刷这波就立即占住 waveIdx，防止 SpawnNextPendingWave 被并发调时
        // FindNextUnspawnedWaveIndex 还返回同一波 → 重复刷。协程硬失败（ConfigManager 空 / pool 空）
        // 在上方已 return 走 SpawnFallbackWave，不会走到这里标记，保留重试机会。
        wave.spawned = true;
        _activeWaveIndex = waveIndex;
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
        // 【2026-10-20 主人拍板】读入本波随机编成（全近战/混合/全远程），供 PickWaveSpriteIndex 分流；
        // 引导波 compositionStyle 恒 0，走 tutorial_battle 表编成不受影响。
        _activeWaveComposition = wave != null ? wave.compositionStyle : 0;
        // 【2026-11-23 统一编排器】旧「怪围着狗」落位特判已删：狗改走真参战上场，
        // 怪走通用屏外入场、自然走入交战点，不再有狗位真源/落位钳制（消除抢时序根源）。
        float waveSpacing = GetMonsterWaveSpacing();
        // 【2026-10-20 主人拍板】本波的出生点候选池（车道×深度档，已洗牌），逐只随机取用；
        // 随机只落在车道与深度上，进场侧恒为右侧。
        var spawnPool = BuildSpawnPointPool();
        var usedSpritesThisWave = new System.Collections.Generic.HashSet<int>();
        var usedLanes = new System.Collections.Generic.List<float>();
        // —— 【2026-11-05 主人拍板】本波词条怪槽位：从「安全槽区间」随机取 N 个，
        // 避开波尾的精英 / 签名怪 / 波内 Boss / 亲卫窗口；引导波不插（Rules.UseTutorialSprites 排除）。
        var affixSlotsThisWave = new System.Collections.Generic.HashSet<int>();
        var arch0 = GetWaveArchetype(waveIndex);
        int eliteQuota0 = (arch0 != null ? arch0.elite : 0) + (wave != null && wave.eliteBonus > 0 ? wave.eliteBonus : 0);
        // 【2026-11-23 主人拍板】仅第1章第3关：精英出现的那一波不插词条怪，槽位回落普通选怪（总怪数不变）。
        // 判精英波复用下方刷怪同一份 eliteQuota0；其它关/波/副本/裂隙/引导一律不变。
        bool stage3EliteNoAffix = bm != null && bm.IsFormalMainRun && CurrentChapter == 1
            && stageIdx == 2 && eliteQuota0 > 0;
        // 【2026-11-23 主人拍板】第5关/第10关（Boss 关）不插词条怪。Boss 本体波本就无配额，此处挡住小怪波。
        bool bossStageNoAffix = bm != null && bm.IsFormalMainRun && (stageIdx == 4 || stageIdx == 9);
        if (wave != null && wave.affixQuotaInWave > 0 && !Rules.UseTutorialSprites && !stage3EliteNoAffix && !bossStageNoAffix)
        {
            int tailReserved = Mathf.Max(0, wave.bossCountInWave) + eliteQuota0 + (wave.signatureSpriteIndex > 0 ? 1 : 0);
            int safeCount = Mathf.Max(0, wave.monsterCount - tailReserved);
            int want = Mathf.Min(wave.affixQuotaInWave, safeCount);
            var slots = new System.Collections.Generic.List<int>(safeCount);
            for (int s = 0; s < safeCount; s++) slots.Add(s);
            for (int s = slots.Count - 1; s > 0; s--)
            {
                int j = UnityEngine.Random.Range(0, s + 1);
                int t = slots[s]; slots[s] = slots[j]; slots[j] = t;
            }
            for (int s = 0; s < want && s < slots.Count; s++) affixSlotsThisWave.Add(slots[s]);
        }
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
            // ===== T2 敌对乱入：目标波前 intruderSlotCount 个槽位改走机器人（不增波数、不吞怪数）=====
            // 放在词条/模板选择之前：避免 continue 吞掉 affix 预选槽，或遗留 SetPendingBias 未清。
            // 解析/生成失败会 LogError 并回落普通怪，保证本波怪数与非目标波一致。
            if (wave != null && wave.intruderSlotCount > 0 && i < wave.intruderSlotCount
                && TrySpawnIntruderMercenary(wave, i, waveIndex, stageIdx, engageBaseX, waveSpacing, spawnZ))
                continue;

            // Boss 关变体「双护法」：本波最前 N 个名额是精英亲卫（非 Boss 模板），Boss 本体最后出场
            bool guardSlot = wave.isBossWave && wave.bossGuardCount > 0 && i < wave.bossGuardCount;

            // 【2026-11-05 主人拍板】词条怪槽优先：命中预选槽位且抽到表定词条怪则直接用；
            // 抽不到（池空 / 找不到 config）回落普通选怪，不吞怪；槽位未命中不消耗名额。
            // 【修 CS0165】&& 短路下 out 内联声明的变量跨语句不保持「已赋值」关联（CS0165），
            // 预置默认值后任何路径都有值；affixSlotHit=true 时下方 TryPickWaveMonster 不会执行、不覆盖。
            MonsterConfig template = null;
            int spriteIndexOverride = 1;
            bool affixSlotHit = affixSlotsThisWave.Remove(i)
                && TryPickAffixMonster(stageIdx, out template, out spriteIndexOverride);
            if (!affixSlotHit && !TryPickWaveMonster(stageIdx, i, wave.isBossWave, usedSpritesThisWave,
                    out template, out spriteIndexOverride, waveIndex, stageType, guardSlot))
            {
                Debug.LogWarning($"[BattleManager] wave monster pick fail i={i}");
                continue;
            }

            // 【2026-10-26 前期优化】签名怪：本波最后一个名额强制成「迷你精英+词缀」，给「每关一只特殊怪」的变化感
            bool signatureSlot = wave != null && wave.signatureSpriteIndex > 0 && i == wave.monsterCount - 1;

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
            // 【2026-10-26 前期优化】签名怪：迷你精英体型 + 指定精灵（Ch1 简单档不挂，见 WavePlanner 三档曲线）
            if (signatureSlot)
            {
                monsterScale = GameConfig.ELITE_SCALE_MULTIPLIER;
                spriteIndexOverride = wave.signatureSpriteIndex;
            }

            // 原型精英额度：占本波最后一个/几个名额（不是额外追加），与引导关同一套口径
            var arch = GetWaveArchetype(waveIndex);
            int eliteQuota = (arch != null ? arch.elite : 0) + (wave != null ? wave.eliteBonus : 0);
            // 【2026-10-23 主人拍板】活动副本（金币/徽记）高级难度：波内 Boss 占最后 N 个名额，
            // Boss 槽不吃精英额度（精英窗口整体前移）；体型给 Boss 档 → Monster.Init 走 bossUnit
            // 分支（金币/徽记 Boss 专属图 + Boss 数值口径），普通波清关链路不受影响。
            int bossTail = wave != null ? Mathf.Max(0, wave.bossCountInWave) : 0;
            bool bossSlot = bossTail > 0 && i >= wave.monsterCount - bossTail;
            bool archEliteSlot = !bossSlot && eliteQuota > 0 && i >= wave.monsterCount - bossTail - eliteQuota;
            if (archEliteSlot)
                monsterScale = GameConfig.ELITE_SCALE_MULTIPLIER;
            if (bossSlot)
                monsterScale = GameConfig.BOSS_SCALE_MULTIPLIER;
            // 亲卫：强制精英体型（同章普通怪模板 + 精英缩放），Boss 本体仍走 Boss 体型
            if (guardSlot)
                monsterScale = GameConfig.ELITE_SCALE_MULTIPLIER;

            GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
            bool fromLeft = false;
            float preferEngageX;
            // 普通波：出生点随机抽出来的车道 / 深度（池空兜底时才在下面补算）
            float pooledLane = 0f;
            float pooledDepth = 0f;
            if (wave.aroundAnchor)
            {
                float ox = AroundEngageOffsets[i % AroundEngageOffsets.Length] + Random.Range(-0.45f, 0.45f);
                preferEngageX = anchorX + ox;
                // 【2026-10-27 主人拍板】引导精英固定从右侧进场：around 波的精英槽不再随交战点
                // 随机选边（原来落点在英雄左边就从左进）。fromLeft=false → SpawnMonsterOffscreenEnter
                // 从右屏外进场，ResolveEngageXForEnterSide 会把落点钳到英雄右侧，不穿帮。
                fromLeft = tutorialEliteSlot ? false : preferEngageX < heroCombatX;
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
                // X 方向保留原有等距队列 + 抖动（ResolveEngageXForEnterSide 会把交战点钳到进场同侧）
                preferEngageX = engageBaseX + i * waveSpacing + Random.Range(-0.22f, 0.22f);
                SpawnPointSlot sp;
                if (TryTakeSpawnPoint(spawnPool, usedLanes, out sp))
                {
                    pooledLane = sp.laneY;
                    pooledDepth = sp.depth;
                    usedLanes.Add(sp.laneY);
                }
                else
                {
                    // 池空（可行走区读不到 / 候选取尽）→ 退回原行为，不许静默
                    Debug.LogWarning($"[WavePlanner] 出生点池已空，第 {i + 1}/{wave.monsterCount} 只退回等距车道兜底");
                    fromLeft = false;
                    pooledLane = BattleLaneBounds.LaneSlot(i, wave.monsterCount);
                    pooledDepth = i * GameConfig.MONSTER_ENTER_DEPTH_STEP;
                    usedLanes.Add(pooledLane);
                }
            }

            float lane = (wave.aroundAnchor || wave.bilateralEnter)
                ? BattleLaneBounds.PickSpreadLane(usedLanes)
                : pooledLane;
            if (wave.aroundAnchor || wave.bilateralEnter)
                usedLanes.Add(lane);
            if (wave.spawnAnchor != null)
                spawnZ = wave.spawnAnchor.position.z;

            Vector3 engagePos = new Vector3(preferEngageX, UnitBase.GROUND_Y + lane, spawnZ);
            // 词缀偏好：只在本次实例化期间挂上（Monster.Init 内同步 Roll 词缀），出完立刻清，
            // 避免把「分裂」之类的偏好泄漏给下一只普通精英。
            bool eliteUnit = monsterScale >= GameConfig.ELITE_SCALE_MULTIPLIER - 0.05f;
            bool hasBias = false;
            string biasToApply = null;
            if (signatureSlot && wave != null && !string.IsNullOrEmpty(wave.signatureAffixBias))
            {
                hasBias = true;
                biasToApply = wave.signatureAffixBias;
            }
            else if (arch != null && !string.IsNullOrEmpty(arch.affixBias) && eliteUnit)
            {
                hasBias = true;
                biasToApply = arch.affixBias;
            }
            if (hasBias) MonsterAffixDefs.SetPendingBias(biasToApply);
            // 【2026-10-05 主人拍板「多点出生点」】一整波从远近不同的位置一起涌进来，
            // 观感是「成群压过来」，不再需要靠长 stagger 排队。
            // 【2026-10-20 主人拍板】普通波的深度档不再按序号累加，改由出生点池随机抽取
            // （见 GameConfig.MONSTER_SPAWN_DEPTH_TIERS）；围攻/夹击波保持 offset=0 不动。
            float enterDepth = (wave.aroundAnchor || wave.bilateralEnter)
                ? 0f
                : pooledDepth;
            // 【2026-11-23 统一编排器】所有波统一走通用屏外入场（不再有狗剧情落位特判）。
            Monster m = SpawnMonsterOffscreenEnter(
                engagePos, lane, monsterScale, template, stageIdx, spriteIndexOverride,
                wave.forcedTarget, fromLeft, enterDepth);
            if (hasBias) MonsterAffixDefs.ClearPendingBias();
            if (m != null)
            {
                ForceEnableMonsterRenderers(m.transform);
                wave.aliveCount++;
                // 【2026-10-26 词缀怪 / 2026-11-20 改】吐槽台词改为进屏后再说：这里只武装，
                // 真正说话在 Monster.AIUpdate 检测身体进入可视区后触发（贴边即算，说完就能看见怪）。
                m.ArmAffixBanter();
            }
        }

        // 记账：这一波用到过哪些精灵，下一波优先换一批（只影响外观）
        _lastWaveSprites.Clear();
        _lastWaveSprites.UnionWith(usedSpritesThisWave);
        _activeWaveComposition = 0;

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

        bool fallbackLogged = false; // 【2026-10-24 主人拍板】兜底红块怪 = 严重异常，必须 LogError（铁律：出错要让我看见）；一波只报一次防刷屏

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
                if (!fallbackLogged)
                {
                    Debug.LogError($"[WavePlanner] 怪物池与 _monsterPrefab 均不可用，第{waveIndex + 1}波将生成 {wave.monsterCount} 只红色方块占位怪 —— 请检查 PoolManager 初始化与 _monsterPrefab 配置！");
                    fallbackLogged = true;
                }
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

    /// <summary>
    /// T2 敌对乱入：把一个槽位换成敌方佣兵（机器人冒险者）。复用竞技场 SpawnEnemyMercenary 链路，
    /// 只进 bm.monsters、不进 _activeMercs；🔴 四条记账自己补，缺一会导致「怪清不完 / 不掉金币 / 不算击杀任务」。
    /// 返回 false = 数据或生成失败（已 LogError），调用方回落普通怪，不吞怪数。
    /// </summary>
    bool TrySpawnIntruderMercenary(WaveData wave, int slot, int waveIndex, int stageIdx,
        float engageBaseX, float waveSpacing, float spawnZ)
    {
        var ids = wave.intruderAssetIds;
        if (ids == null || slot >= ids.Length || string.IsNullOrEmpty(ids[slot]))
        {
            Debug.LogError($"[Intruder] 波 {waveIndex + 1} 槽位 {slot} 的 intruderAssetIds 缺失/越界（event={wave.intruderEventId}）");
            return false;
        }
        string assetId = ids[slot];
        string hireId = MercRosterDefs.ResolveHireId(assetId);
        if (string.IsNullOrEmpty(hireId))
        {
            Debug.LogError($"[Intruder] assetId={assetId} → hireId 解析失败（MercRosterDefs.ResolveHireId 返回 null）");
            return false;
        }
        var mm = MercenaryManager.Instance;
        if (mm == null)
        {
            Debug.LogError("[Intruder] MercenaryManager.Instance 为空，敌对乱入单位不生成");
            return false;
        }

        float lane = BattleLaneBounds.LaneSlot(slot, wave.monsterCount);
        var pos = new Vector3(engageBaseX + slot * waveSpacing + Random.Range(-0.22f, 0.22f),
            UnitBase.GROUND_Y + lane,
            wave.spawnAnchor != null ? wave.spawnAnchor.position.z : spawnZ);
        // T0 表无乱入单位等级/星级字段：按章节+关序给确定性值（数值口径待主人确认，本批不造配置）。
        int level = Mathf.Clamp(CurrentChapter * 5 + stageIdx, 1, 60);
        int star = Mathf.Clamp(1 + CurrentChapter / 2, 1, 5);

        // 🔴 生成必须传数据 assetId：技能/属性/专属行为全按它查表，不可改成 bandit_*。
        // 旧代码误传 hireId（H022/H013/H009）→ GetPrefabName 原样返回 → Units/H022 三层兜底全失败 → 强盗根本刷不出。
        // 【2026-11-26 主人拍板】强盗用专属战斗模型（bandit_boss/witch/blade），不复刻玩家佣兵的脸；
        // 只覆盖加载名，数据 id 仍是 zhongzhan201/fashi202/kuangzhan201，技能/行为/属性零外溢。
        string banditPrefab = IntruderPicker.BanditPrefabOfAssetId(assetId);
        Mercenary merc = mm.SpawnEnemyMercenary(assetId, pos, level, star, banditPrefab);
        if (merc == null)
        {
            Debug.LogError($"[Intruder] SpawnEnemyMercenary 失败 assetId={assetId} hireId={hireId}（见上方日志）");
            return false;
        }
        merc.isAlly = false;          // 显式再置一次（SpawnEnemyMercenary 内部 L409 已设）
        merc.Face(-1);                // 面向玩家半场（左），照 ArenaEnemySpawner L258
        merc.SetPartyIndex(slot);     // 车道偏移按队内序错开（同 L257）

        // 🔴 花名册默认技能（主动+被动）—— 敌方强盗照常放技能，口径对齐 ArenaEnemySpawner L262-275。
        // 按 hireId 精确取行，不用按 asset 首行：kuangzhan201 被 H009/H016 共用，
        // ResolveHireId 取首条 H009 → 阿刃拿 SK001 疾风斩，不会被 H016 的 SK003 串味。
        if (MercRosterDefs.TryGetByHireId(hireId, out var banditDef)
            && (!string.IsNullOrEmpty(banditDef.ActiveSkillId) || !string.IsNullOrEmpty(banditDef.PassiveSkillId)))
        {
            merc.SetupBattleSkills(banditDef.ActiveSkillId, banditDef.PassiveSkillId);
            Debug.Log($"[Intruder] 强盗技能挂载 assetId={assetId} hireId={hireId} 主动={banditDef.ActiveSkillId} 被动={banditDef.PassiveSkillId}");
        }
        else
        {
            Debug.LogError($"[Intruder] hireId={hireId}（assetId={assetId}）花名册无技能，强盗只会普攻（不静默吞）");
        }

        // 🔴 【2026-11-26 主人拍板】强盗三人专属行为打标（老雷眩晕 / 阿刃脆皮换目标 / 夜薇星落+闪现）。
        // 按 assetId 判（IntruderPicker 真源），None 的强盗组合不会出现；未匹配一律 None → 普通敌方佣兵行为。
        var banditRole = BanditRoleOfAssetId(assetId);
        if (banditRole != Mercenary.BanditRole.None)
            merc.ConfigureBandit(banditRole);

        // 【2026-11-26 主人拍板】强盗按 Boss 出场：第一只播 1.2 秒血条入场，
        // 后续每只刷新绑定 → 三只合成一条求和血条（与 Boss/精英多目标口径同款）。
        if (slot == 0)
            BattleBossHpBar.PlayBossIntro(merc);
        else
            BattleBossHpBar.RefreshFromField();

        // 【2026-11-22 主人拍板】强盗强度按 boss 档，不打死倍率：玩家装备继承会随关卡成长，
        // 固定「血3攻2」到后期会被反超。改吃 boss 同款章内成长曲线（GetStatScale 唯一出口）
        // + BOSS_TTK_HP_MUL 血档，随章节/大关/小关自动爬坡。
        // ⚠ 不直接照搬 boss 绝对值：boss 攻击另吃 ChapterAtkMul(0.35~0.75)×BOSS_ATTACK_MUL(0.6)
        // 两道削减，佣兵体系不吃；照搬会变成「数值像 boss、打人比小弟还轻」。
        // 故只借 boss 的成长曲线与血档倍率，基数仍用佣兵自身（GetAttr 当前值），
        // SetBaseAndCurrent 一起写回，RecalcAllAttr 不回退；夜薇法攻同步放大。只作用于本函数生成的强盗。
        var bMgr = BattleManager.Instance;
        int banditChapter = bMgr != null ? bMgr.CurrentChapter : 1;
        int banditStage = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : 0;
        int banditGrand = bMgr != null && bMgr.IsFormalMainRun && ChapterManager.Instance != null
            ? ChapterManager.Instance.currentGrand : 1;   // 与 Monster.Init.GrandIndexForStatScale 同款
        float banditScale = GameConfig.GetStatScale(banditChapter, banditGrand, banditStage);
        float banditHpMul = 3f * banditScale * GameConfig.BOSS_TTK_HP_MUL;   // 血：boss TTK 血档，随章爬坡
        float banditAtkMul = 2f * banditScale;                               // 攻：只吃章内曲线，不吃 boss 攻击削减
        var a = merc.attr;
        a.SetBaseAndCurrent(AttrType.MaxHp, a.GetAttr(AttrType.MaxHp) * banditHpMul);
        a.SetBaseAndCurrent(AttrType.Attack, a.GetAttr(AttrType.Attack) * banditAtkMul);
        float magicAtk = a.GetAttr(AttrType.MagicAttack);
        if (magicAtk > 0f) a.SetBaseAndCurrent(AttrType.MagicAttack, magicAtk * banditAtkMul);
        merc.currentHp = a.GetAttr(AttrType.MaxHp);   // 当前血量同步到放大后的上限，否则开局残血

        // 🔴 记账四件套（缺一会导致怪清不完 / 不掉金币 / 不算击杀任务）
        merc.OnDead += OnMonsterDead;      // 普通怪 Execute L516
        bm.monsters.Add(merc);             // 普通怪 Execute L517 / 竞技场 L276
        _totalMonstersSpawnedThisStage++;  // 普通怪 Execute L518 / 竞技场 L277
        bm.InvalidateAliveMonsterCache();  // 普通怪 Execute L519 / 竞技场 L278
        wave.aliveCount++;                 // 普通怪 Execute L287
        return true;
    }

    /// <summary>
    /// 【2026-11-26】强盗 assetId → 专属角色真源（与 IntruderPicker.cs 三人 assetId 一字不差）。
    /// 三人之外（含将来加强盗）返回 None = 不启用专属行为，仍照普通敌方佣兵链路，避免误伤。
    /// </summary>
    static Mercenary.BanditRole BanditRoleOfAssetId(string assetId)
    {
        switch (assetId)
        {
            case "zhongzhan201": return Mercenary.BanditRole.OldLei;    // 独眼老雷 · 眩晕
            case "kuangzhan201": return Mercenary.BanditRole.Arens;     // 阿刃 · 脆皮换目标
            case "fashi202": return Mercenary.BanditRole.NightWei;      // 夜薇 · 星落 + 闪现
            default: return Mercenary.BanditRole.None;
        }
    }
}
