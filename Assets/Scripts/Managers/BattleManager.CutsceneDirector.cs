using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗内演出统一编排器（2026-11-23 解耦达重构 / 施工图 CutsceneDirector）：
/// 单一职责——按 BattleCutsceneDefs 的有序步骤表，用【唯一一条协程】驱动演出，彻底消除旧版
/// 4 协程 + phase 0~4 + 5 个散落 bool 的多点 StartCoroutine 抢时序。
/// 对外【唯一信号】IsOccupyingBattlefield：true 时波次系统不得推进（替代旧 phase 散落判断）。
/// 【不冻场】：气泡用 unscaled 时间 + LateUpdate 每帧跟随锚点，狗是真战斗单位会随队移动，
/// 玩家走过也看得到（前 8 轮冻场正是"怪被定在屏外/玩家走过静止狗"的根因，已弃用）。
/// _preBattleStoryPlaying 留给开场/抽奖/首波硬刷旧用途，与占用标志分层共存、互不干扰。
/// </summary>
public partial class BattleManager
{
    bool _cutsceneActive;                // 本关演出是否在跑（换关复位中止协程）
    bool IsOccupyingBattlefield;         // 唯一对外占用信号；true 时波次系统只读它、不推进
    Coroutine _cutsceneCo;               // 唯一协程句柄
    Pet _cutsceneDog;                    // 本场狗战斗单位（真参战，进 allyUnits）

    /// <summary><see cref="IsOccupyingBattlefield"/> 只读出口：波次系统（Waves.cs）唯一依赖。</summary>
    internal bool CutsceneOccupyingBattlefield => IsOccupyingBattlefield;

    /// <summary>第二波刷出时由 Update 调用：启动狗营救演出（幂等，重复调用不叠加）。</summary>
    void TryPlayDogRescueCutscene()
    {
        if (_cutsceneActive || _cutsceneCo != null) return;
        if (!_petRescueStageActive) return;
        if (_activeWaveIndex != 1) return;            // 第二波（0 基 1）
        if (CountAliveMonsters() <= 0) return;
        if (!isInBattle || _stageCleared || _portalActive) return;

        _cutsceneActive = true;
        IsOccupyingBattlefield = true;               // 立即占用，拦住波次推进（防 wave1 清完抢跑 Boss）
        _cutsceneCo = StartCoroutine(CoRunDogRescue(BattleCutsceneDefs.DogRescue));
        Debug.Log("[Cutscene] 狗营救演出启动（单协程编排，占用战场）");
    }

    /// <summary>换关复位：停协程 + 清占用标志，绝不把上一关的演出状态带进下一关。</summary>
    void ResetBattleCutscene()
    {
        _cutsceneActive = false;
        IsOccupyingBattlefield = false;
        _cutsceneDog = null;
        if (_cutsceneCo != null)
        {
            StopCoroutine(_cutsceneCo);
            _cutsceneCo = null;
        }
    }

    IEnumerator CoRunDogRescue(BattleCutsceneStep[] steps)
    {
        try
        {
            for (int i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                Debug.Log("[CutsceneDiag] STEP=" + step + " occupied=" + IsOccupyingBattlefield + " alive=" + CountAliveMonsters() + " spawnCo=" + (_spawnWaveCo != null) + " waveIdx=" + _activeWaveIndex);
                if (step == BattleCutsceneStep.Release) break;   // 统一在 finally 释放，绝不中途漏放
                yield return RunDogRescueStep(step);
                if (!_cutsceneActive) yield break;               // 换关中止
            }
        }
        finally
        {
            IsOccupyingBattlefield = false;                      // 唯一出口：放行下一帧首领来袭
            _cutsceneDog = null;
            _cutsceneCo = null;
            Debug.Log("[Cutscene] 狗营救演出结束，释放战场占用");
        }
    }

    IEnumerator RunDogRescueStep(BattleCutsceneStep step)
    {
        var talk = BattleHeadTalkUI.Ensure();
        switch (step)
        {
            case BattleCutsceneStep.SpawnPetFight:
            {
                // 【2026-11-23 主人「玩家打完一波往前走时，狗已经和怪在战斗」】
                // 先等第二波怪真正走进战斗镜头再放狗（最多 6s）：玩家往前走那一眼看到的才是
                // 「狗已经在跟怪打」，而不是狗先单独杵在屏边、气泡对着空气播。
                float waitInView = 0f;
                while (_cutsceneActive && !AnyMonsterInCombatView() && waitInView < 6f)
                {
                    waitInView += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log("[CutsceneDiag] 等怪进画面 wait=" + waitInView.ToString("F2") + " inView=" + AnyMonsterInCombatView());

                // 营救成功即解锁狗 + 标记已看（本关失败也不回收，规格第一节），走唯一出口真参战
                var data = SaveSystem.Instance?.Data;
                if (data != null)
                {
                    data.UnlockPet(PetRescueStoryBeats.DogId);
                    data.MarkIntruderStorySeen(PetRescueStoryBeats.SeenKey);
                    SaveSystem.Instance.Save();
                }
                DeployRescuedPet();
                _petRescuePendingDeploy = true;                  // Boss 半血归队（幂等，狗已在场则早退）
                _cutsceneDog = FindRescueDog();
                Debug.Log("[CutsceneDiag] dogFound=" + (_cutsceneDog != null) + " heroX=" + (hero != null ? hero.transform.position.x.ToString("F2") : "null") + " dogX=" + (_cutsceneDog != null ? _cutsceneDog.transform.position.x.ToString("F2") : "null")); 
                if (_cutsceneDog == null)
                    Debug.LogError("[Cutscene] 狗战斗单位生成后取不到（不兜底），气泡退到玩家锚点继续演");
                yield return null;                               // 让狗落位一帧，怪同步涌入
                break;
            }
            case BattleCutsceneStep.BarkAtDog:
            {
                Debug.Log("[CutsceneDiag] BarkAtDog ENTER talkNull=" + (talk == null) + " dogNull=" + (_cutsceneDog == null));
                if (talk == null) { Debug.LogError("[Cutscene] 气泡 UI 取不到，BarkAtDog 跳过"); break; }
                if (_cutsceneDog != null)
                {
                    yield return talk.CoPlayLineAt(_cutsceneDog.transform, PetRescueStoryBeats.DogBark, PetRescueStoryBeats.WhimperHold);
                    Debug.Log("[CutsceneDiag] BarkAtDog DONE show=" + talk.IsShowing + " line=" + talk.CurrentLine);
                }
                else if (hero != null)
                {
                    yield return talk.CoPlayLine(hero, PetRescueStoryBeats.DogBark, PetRescueStoryBeats.WhimperHold);
                    Debug.Log("[CutsceneDiag] BarkAtDog DONE(hero锚点) show=" + talk.IsShowing + " line=" + talk.CurrentLine);
                }
                break;
            }
            case BattleCutsceneStep.ShoutAtHero:
            {
                Debug.Log("[CutsceneDiag] ShoutAtHero ENTER heroNull=" + (hero == null));
                if (talk == null || hero == null) { Debug.LogError("[Cutscene] 气泡 UI/玩家取不到，ShoutAtHero 跳过"); break; }
                yield return talk.CoPlayLine(hero, PetRescueStoryBeats.PlayerShout, PetRescueStoryBeats.ShoutHold);
                Debug.Log("[CutsceneDiag] ShoutAtHero DONE show=" + talk.IsShowing + " line=" + talk.CurrentLine);
                break;
            }
            case BattleCutsceneStep.WaitWaveCleared:
            {
                // 等本波出海结束 + 场上无怪；看门狗 300s 防卡死，绝不无限等
                float watchdog = 0f;
                Debug.Log("[CutsceneDiag] WaitWaveCleared ENTER alive=" + CountAliveMonsters() + " spawnCo=" + (_spawnWaveCo != null));
                while (_cutsceneActive && (CountAliveMonsters() > 0 || _spawnWaveCo != null) && watchdog < 300f)
                {
                    watchdog += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log("[CutsceneDiag] WaitWaveCleared EXIT alive=" + CountAliveMonsters() + " watchdog=" + watchdog.ToString("F1"));
                if (watchdog >= 300f) Debug.LogWarning("[Cutscene] 等本波清空超时 300s，强制进入战后点评");
                break;
            }
            case BattleCutsceneStep.BarkThanksAtDog:
            {
                if (talk == null) { Debug.LogError("[Cutscene] 气泡 UI 取不到，BarkThanksAtDog 跳过"); break; }
                if (_cutsceneDog != null)
                    yield return talk.CoPlayLineAt(_cutsceneDog.transform, PetRescueStoryBeats.DogThanks, PetRescueStoryBeats.ThanksHold);
                else if (hero != null)
                    yield return talk.CoPlayLine(hero, PetRescueStoryBeats.DogThanks, PetRescueStoryBeats.ThanksHold);
                break;
            }
            case BattleCutsceneStep.RemarkAtHero:
            {
                if (talk == null || hero == null) { Debug.LogError("[Cutscene] 气泡 UI/玩家取不到，RemarkAtHero 跳过"); break; }
                yield return talk.CoPlayLine(hero, PetRescueStoryBeats.PlayerRemark, PetRescueStoryBeats.RemarkHold);
                break;
            }
        }
    }

    /// <summary>场上是否有活怪已经进入战斗镜头（狗登场时机用：怪走进画面才算"在打"）。</summary>
    bool AnyMonsterInCombatView()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null || m.isDead) continue;
            if (GameConfig.IsInCombatViewport(m)) return true;
        }
        return false;
    }

    /// <summary>找本场真参战的狗（进 allyUnits 的 Pet），气泡锚点用它的 transform。</summary>
    Pet FindRescueDog()
    {
        for (int i = 0; i < allyUnits.Count; i++)
            if (allyUnits[i] is Pet p && p != null && !p.isDead && p.petDefId == PetRescueStoryBeats.DogId)
                return p;
        return null;
    }
}
