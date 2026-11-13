using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次规划 partial —— 2026-10-18 由 WavePlanner.cs 纯位移拆出（无逻辑改动）
// 职责：波次推进与兜底（ForceSpawn 首波、Emergency 可见刷怪、补波、下一未刷波）
public partial class WavePlanner
{

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
        GamePerf.Log($"[BattleManager] ForceSpawn 第1轮 beforeAlive={before} afterAlive={after} totalList={monsters.Count} waves={_waves.Count} prefab={(PoolManager.Instance != null && PoolManager.Instance._monsterPrefab != null)}");

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
    /// <param name="allowStackWhileAlive">
    /// 【2026-11-13 主人拍板】波流式衔接用：true = 场上还有上一波活怪也照刷（把两拨压成一波连续怪潮）。
    /// 只由 BattleManager.TickWaveStreamSpawn 传 true；清场路径（波清→下一波）保持原闸门不动。
    /// </param>
    internal void SpawnNextPendingWave(bool allowStackWhileAlive = false)
    {
        if (_waves == null || _waves.Count == 0 || hero == null) return;
        EnsureMonsterPrefabReady();
        RestoreHazardPenalty(); // 清掉上一波灾害残留，避免跨波泄漏

        int waveIdx = FindNextUnspawnedWaveIndex();
        if (waveIdx < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            return;
        }

        // 场上还有上一波活怪时，不叠刷（教程关由 QueueTutorialWave 重置 _activeWaveIndex；
        // 波流式衔接 allowStackWhileAlive=true 例外，2026-11-13 主人拍板）
        if (!Rules.AllowStackSpawnWhileAlive && !allowStackWhileAlive
            && CountAliveMonsters() > 0 && _activeWaveIndex >= 0)
            return;

        var wave = _waves[waveIdx];
        float heroX = UnitBase.GetCombatX(hero);
        try
        {
            int aliveBefore = CountAliveMonsters();
            // 事件层：本波若为补给/灾害，出兵前先结算预效果（仅一次）
            if (wave != null && !wave.stageEventApplied)
            {
                if (wave.stageEventHealPct > 0f) HealTeamOnEvent(wave);
                else if (wave.stageEventMovePenalty > 0f) ApplyHazardPenalty(wave);
                wave.stageEventApplied = true;
            }
            SpawnWave(wave, waveIdx);
            int aliveAfter = CountAliveMonsters();
            if (aliveAfter > aliveBefore)
            {
                wave.spawned = true;
                _activeWaveIndex = waveIdx;
                StopWaveCountdown();
                GamePerf.Log($"[BattleManager] 刷第{waveIdx + 1}/{_waves.Count}波 OK alive={aliveAfter} heroX={heroX:F2}");
                // 【2026-11-13 主人拍板】顶部进度条随出波推进（流式衔接后波与波之间不再清场，
                // 原「波清才推进」的点在普通波上摸不到了）：已刷波数 = waveIdx + 1
                BattleUI.Instance?.UpdateWaveProgress(waveIdx + 1, _waves != null ? _waves.Count : 0);
                // 【2026-11-13 v5 两段式】第 1 波（开场波 waveIdx==0）不排流式：单独刷、能清场，让全关唯一一次 Gate 播得出来。
                // 自第 2 波出完起（waveIdx>=1）才排 1s 波流式衔接（多波连续怪潮）；引导局不走本轨（SkipWaveAnnounce）。
                // Boss 波仍走「首领来袭」：不在协程里排，等场上清空后由 Update → BeginNextWaveCountdown 处理。
                if (waveIdx >= 1 && !wave.isBossWave && !Rules.SkipWaveAnnounce)
                    bm.ScheduleWaveStreamNext();
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
}
