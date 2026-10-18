using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次规划 partial —— 2026-10-18 由 WavePlanner.cs 纯位移拆出（无逻辑改动）
// 职责：引导波（PrepareTutorialWaves/QueueTutorialStep 步进刷怪/引导波核心与校验）
public partial class WavePlanner
{

    // ---- extracted from BattleManager L565-L698 ----
    internal void PrepareTutorialWaves()
    {
        _waves.Clear();
        _totalWaves = 0;
        _allWavesSpawned = false;
        _activeWaveIndex = -1;
        _firstWaveSpawned = true; // 关掉 Update/过场后的硬刷；真正刷怪只走 TutorialDirector
        _lastWaveSprites.Clear();
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

        // 引导关人数真源 = tutorial_battle.csv，这里默认不缩放（MonsterCountMul=1）。
        int raw = Mathf.Max(1, count);
        int n = Rules.ScaleMonsterCount(raw, bilateralEnter || aroundAnchor);
        if (n != raw)
            GamePerf.Log($"[BattleManager] 引导波人数缩放 {raw} → {n}（MonsterCountMul={Rules.MonsterCountMul}）");
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

        int waveIdx = _waves.Count - 1;
        int spawnedBefore = wave.aliveCount;
        int aliveBefore = CountAliveMonsters();
        TrySpawnTutorialWave(wave, waveIdx, spawnedBefore, aliveBefore);

        // 必须是「主波一只都没刷出来」才补刷，不能写 == 0：
        // CountAliveMonsters() 有同帧缓存，主波协程还在按 stagger 出怪时读回来仍是刷怪前的旧值，
        // 写 == 0 会误判成"没刷出来"再补一整波，实际怪量直接翻倍（6→8、7→10、8→12）。
        if (CountAliveMonsters() <= aliveBefore)
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
}
