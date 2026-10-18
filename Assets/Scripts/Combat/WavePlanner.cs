using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 波次规划 + 刷怪执行。正式关与引导步进共用同一条 SpawnWave 轨。
/// TutorialDirector 只点 QueueTutorialStep；本类读表铺 WaveData 并出怪。
/// 方法体从 BattleManager 原样迁出，手感（人数/L-R/stagger/HP 档）保持 1:1。
/// </summary>
public sealed partial class WavePlanner
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
    // 【2026-10-05】这两个旗标真源唯一 = 规则包，BattleManager 侧已改只读直通，
    // 这里同步去掉 setter（原先每次教程波都写一遍，是残留值的帮凶）。
    bool SuppressStageClear => bm.SuppressStageClear;
    bool SkipLegacyOnEvacuate => bm.SkipLegacyOnEvacuate;

    List<WaveData> _waves { get => bm._waves; set => bm._waves = value; }
    int _totalWaves { get => bm._totalWaves; set => bm._totalWaves = value; }
    bool _allWavesSpawned { get => bm._allWavesSpawned; set => bm._allWavesSpawned = value; }
    int _activeWaveIndex { get => bm._activeWaveIndex; set => bm._activeWaveIndex = value; }
    bool _firstWaveSpawned { get => bm._firstWaveSpawned; set => bm._firstWaveSpawned = value; }
    int _tutorialSpriteMelee { get => bm._tutorialSpriteMelee; set => bm._tutorialSpriteMelee = value; }
    int _tutorialSpriteRanged { get => bm._tutorialSpriteRanged; set => bm._tutorialSpriteRanged = value; }
    int _tutorialEliteCount { get => bm._tutorialEliteCount; set => bm._tutorialEliteCount = value; }
    int _tutorialWaveMonsterCount { get => bm._tutorialWaveMonsterCount; set => bm._tutorialWaveMonsterCount = value; }
    int _tutorialRangedCount { get => bm._tutorialRangedCount; set => bm._tutorialRangedCount = value; }
    float _tutorialHpMin { get => bm._tutorialHpMin; set => bm._tutorialHpMin = value; }
    float _tutorialHpMax { get => bm._tutorialHpMax; set => bm._tutorialHpMax = value; }
    float _tutorialEliteHpMin { get => bm._tutorialEliteHpMin; set => bm._tutorialEliteHpMin = value; }
    float _tutorialEliteHpMax { get => bm._tutorialEliteHpMax; set => bm._tutorialEliteHpMax = value; }
    float _tutorialRangedHpMin { get => bm._tutorialRangedHpMin; set => bm._tutorialRangedHpMin = value; }
    float _tutorialRangedHpMax { get => bm._tutorialRangedHpMax; set => bm._tutorialRangedHpMax = value; }
    bool _tutorialHpFromTable { get => bm._tutorialHpFromTable; set => bm._tutorialHpFromTable = value; }
    Coroutine _spawnWaveCo { get => bm._spawnWaveCo; set => bm._spawnWaveCo = value; }
    int _totalMonstersSpawnedThisStage { get => bm._totalMonstersSpawnedThisStage; set => bm._totalMonstersSpawnedThisStage = value; }
    int _offscreenEnterSideToggle { get => bm._offscreenEnterSideToggle; set => bm._offscreenEnterSideToggle = value; }

    /// <summary>本关抽到的关卡模式名；空 = 没走模式逻辑。</summary>
    public string StageModeName => _stageModeName;
    /// <summary>本关模式的播报文案（给玩家的读条时间）。</summary>
    public string StageModeTelegraph => _stageModeTelegraph;
    string _stageModeName = "";
    string _stageModeTelegraph = "";

    /// <summary>Boss 关变体「裂隙涌动」的增援协程；换关 / 通关时随事件层一起清掉。</summary>
    Coroutine _bossPressureCo;

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
}
