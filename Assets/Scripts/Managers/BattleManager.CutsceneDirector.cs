using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗内演出统一编排器（2026-11-23 解耦达重构 / 施工图 CutsceneDirector）：
/// 单一职责——按 BattleCutsceneDefs 的有序步骤表，用【唯一一条协程】驱动演出，彻底消除旧版
/// 4 协程 + phase 0~4 + 5 个散落 bool 的多点 StartCoroutine 抢时序。
/// 对外【唯一信号】IsOccupyingBattlefield：true 时波次系统不得推进（替代旧 phase 散落判断）。
/// 【对话步骤冻场，战斗步骤照常】（2026-11-23 主人拍板 B，覆盖当日拍板 A）：5 句台词期间
/// UnitsCanAct=false 冻全场（与引导 TalkBlock 同源），气泡走 unscaled 时间逐字出字；对话完 finally 解冻，
/// 玩家看清对白后再开打。放狗/等跑/等清波/归队等战斗步骤不冻场，狗与怪照常真打。
/// 演出期间屏幕 Boss/精英血条由 BattleBossHpBar.SetCutsceneSuppressed 压住，演完自动补显。
/// _preBattleStoryPlaying 留给开场/抽奖/首波硬刷旧用途，与占用标志分层共存、互不干扰。
/// </summary>
public partial class BattleManager
{
    bool _cutsceneActive;                // 本关演出是否在跑（换关复位中止协程）
    bool IsOccupyingBattlefield;         // 唯一对外占用信号；true 时波次系统只读它、不推进
    bool _cutsceneFrozeUnits;            // 本演出当前是否正冻着场（只解自己冻的场，防误解冻别人）
    bool _cutsceneTalkCanActBefore;      // 进入连续对话段前的 UnitsCanAct（整段冻一次、整段末才解，中途不解冻）
    Coroutine _cutsceneCo;               // 唯一协程句柄
    Pet _cutsceneDog;                    // 本场狗战斗单位（真参战，进 allyUnits）
    bool _dogRescueForceHero;            // 【2026-11-24】第2波战斗期间怪强制锁玩家（气泡解冻→首领来袭前）
    /// <summary>狗跑入时长倍率（2026-11-23 23:2x 主人「狗的移动速度也快 减一半」：3.5 → 1.75）。</summary>
    const float DOG_RUN_SPEED_MUL = 1.75f;
    /// <summary>等「狗被打跑」的看门狗（秒）：超时强制撤场，绝不把演出卡死（2026-11-23 分镜）。</summary>
    const float PET_FLED_TIMEOUT = 18f;
    /// <summary>等「精英打到半血」的看门狗（秒）：超时直接让狗归队，不无限等。</summary>
    const float ELITE_REJOIN_TIMEOUT = 10f;
    /// <summary>狗归队的精英血量线：血剩一半或更少（主人「打到一半狗进场助阵」，2026-11-23）。</summary>
    const float ELITE_REJOIN_HP_RATIO = 0.5f;
    /// <summary>【2026-11-24】玩家走到狗多近才触发冻场气泡（横向世界单位）：狗已提前入场开打，
    /// 玩家先走过去看一会，靠近了才出剧情。狗在怪群里会移动，阈值留余量。</summary>
    const float CUTSCENE_TRIGGER_DIST = 5.5f;

    /// <summary><see cref="IsOccupyingBattlefield"/> 只读出口：波次系统（Waves.cs）唯一依赖。</summary>
    internal bool CutsceneOccupyingBattlefield => IsOccupyingBattlefield;

    /// <summary>
    /// 玩家走到狗附近时由 Update 调用：启动狗营救演出（幂等，重复调用不叠加）。
    /// 【2026-11-24 主人拍板】狗已在第一波清完时提前入场开打（TryPetRescueActivate 生成），
    /// 这里等玩家走到狗附近（CUTSCENE_TRIGGER_DIST）才触发冻场气泡——玩家先走过去看狗打一会，
    /// 靠近了才出剧情，不是第二波一刷出来就打断。解锁存档与生成已前移，本方法只负责启动协程。
    /// </summary>
    void TryPlayDogRescueCutscene()
    {
        if (_cutsceneActive || _cutsceneCo != null) return;
        if (!_petRescueStageActive) return;
        if (_activeWaveIndex != 1) return;            // 第二波（0 基 1）
        if (CountAliveMonsters() <= 0) return;
        if (!isInBattle || _stageCleared || _portalActive) return;

        // 狗已提前生成；找不到说明已被怪打跑（销毁）——不拦演出：气泡退到玩家锚点，
        // 狗归队步骤（DogRejoinElite）会重新生成。活着时才判「玩家走近」距离。
        _cutsceneDog = FindRescueDog();
        if (_cutsceneDog != null && hero != null)
        {
            float dx = Mathf.Abs(hero.transform.position.x - _cutsceneDog.transform.position.x);
            if (dx > CUTSCENE_TRIGGER_DIST) return;   // 玩家还没走到狗附近
        }

        _cutsceneActive = true;
        IsOccupyingBattlefield = true;               // 立即占用，拦住波次推进（防 wave2 清完抢跑首领）
        BattleBossHpBar.SetCutsceneSuppressed(true);  // 演出期屏幕精英血条不弹

        _cutsceneCo = StartCoroutine(CoRunDogRescue(BattleCutsceneDefs.DogRescue));
        Debug.Log("[Cutscene] 狗营救演出启动（玩家走近触发，狗已在打，占用战场）");
    }

    /// <summary>换关复位：停协程 + 清占用标志 + 解除血条压制，绝不把上一关的演出状态带进下一关。</summary>
    void ResetBattleCutscene()
    {
        _cutsceneActive = false;
        IsOccupyingBattlefield = false;
        BattleBossHpBar.SetCutsceneSuppressed(false);
        // StopCoroutine 不触发协程里的 finally：若正冻在台词中换关，这里补解冻，
        // 只解自己冻的场（引导「冻了没恢复→精英残血站桩」教训）。
        if (_cutsceneFrozeUnits) { UnitsCanAct = true; _cutsceneFrozeUnits = false; }
        _cutsceneTalkCanActBefore = true;
        _cutsceneDog = null;
        // 【2026-11-24】换关清掉怪锁玩家标志 + 强制目标，绝不让下一关的怪还锁着玩家
        _dogRescueForceHero = false;
        ClearMonsterForcedTargets();
        if (_cutsceneCo != null)
        {
            StopCoroutine(_cutsceneCo);
            _cutsceneCo = null;
        }
    }

    /// <summary>
    /// 【2026-11-24 主人拍板「让怪都来打玩家」】第2波战斗期间每帧由 Update 调用：
    /// 演出激活且未到首领来袭前，把场上所有怪（含本波内新刷的）强制目标锁成玩家。
    /// 精英波（首领来袭后）不锁，怪正常选最近目标。
    /// </summary>
    internal void TickDogRescueForceTarget()
    {
        if (!_dogRescueForceHero || hero == null) return;
        RetargetAllMonsters(hero);
    }

    IEnumerator CoRunDogRescue(BattleCutsceneStep[] steps)
    {
        try
        {
            for (int i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                Debug.Log("[CutsceneDiag] STEP=" + step + " occupied=" + IsOccupyingBattlefield + " alive=" + CountAliveMonsters() + " spawnCo=" + (_spawnWaveCo != null) + " waveIdx=" + _activeWaveIndex + " canAct=" + UnitsCanAct);
                if (step == BattleCutsceneStep.Release) break;   // 统一在 finally 释放，绝不中途漏放

                // 【对话步骤整段冻场】（2026-11-23 主人「玩家还没说完冲我来就开始战斗」）：
                // 与引导 TalkBlock 同源，但按「连续对话段」冻一次——段首（BarkAtDog）冻，
                // 段尾（ShoutAtHero 后面不是对话步骤时才解冻），中途两句之间绝不解冻，
                // 否则汪汪说完解冻一帧、怪立刻开打，玩家喊话就淹没在混战里。
                // 只解自己冻的场（_cutsceneTalkCanActBefore 双保险）。
                bool isTalk = IsDogRescueTalkStep(step);
                bool nextIsTalk = i + 1 < steps.Length && IsDogRescueTalkStep(steps[i + 1]);
                if (isTalk && !_cutsceneFrozeUnits)
                {
                    _cutsceneTalkCanActBefore = UnitsCanAct;
                    if (_cutsceneTalkCanActBefore) { UnitsCanAct = false; _cutsceneFrozeUnits = true; }
                }
                try
                {
                    yield return RunDogRescueStep(step);
                }
                finally
                {
                    if (isTalk && !nextIsTalk && _cutsceneFrozeUnits)
                    {
                        if (_cutsceneTalkCanActBefore) UnitsCanAct = true;
                        _cutsceneFrozeUnits = false;
                        _cutsceneTalkCanActBefore = true;
                    }
                }
                if (!_cutsceneActive) yield break;               // 换关中止
            }
        }
        finally
        {
            IsOccupyingBattlefield = false;                      // 唯一出口：放行下一帧首领来袭
            BattleBossHpBar.SetCutsceneSuppressed(false);         // 演出结束：屏幕精英血条自动补显
            _cutsceneDog = null;
            _cutsceneCo = null;
            Debug.Log("[Cutscene] 狗营救演出结束，释放战场占用");
            // 末波清空若发生在演出期间，CoCheckWaveClearNextFrame 被占用闸挡掉了，这里补一次通关判定，
            // 否则「清场链」永远没人再触发（剧情播完卡在战场）。
            TryStartStageClearAfterCutscene();
        }
    }

    /// <summary>需要冻场的对话步骤（气泡台词）。放狗/等跑/等清波/归队等战斗步骤不冻场。</summary>
    static bool IsDogRescueTalkStep(BattleCutsceneStep step)
        => step == BattleCutsceneStep.BarkAtDog
        || step == BattleCutsceneStep.ShoutAtHero
        || step == BattleCutsceneStep.DogFleeLine
        || step == BattleCutsceneStep.BarkThanksAtDog
        || step == BattleCutsceneStep.RemarkAtHero;

    /// <summary>
    /// 狗从画面左侧跑进战场（2026-11-23 22:4x 主人「狗没有从左边跑进来，还是突然出现的」）：
    /// 生成点已在画面左缘外（<see cref="ResolveRescueDogLeftSpawnX"/>），这里逐帧给水平速度跑到
    /// toX（怪群/精英），跑动期 SetCutsceneControlled(true) 锁住继承 AI、朝向朝右；
    /// 到位或时长兜底后交还 AI 正常参战。绝不瞬移、绝不卡演出。
    /// </summary>
    IEnumerator CoRunDogIntoScene(Pet dog, float toX)
    {
        if (dog == null) yield break;
        float fromX = dog.transform.position.x;
        int dir = toX >= fromX ? 1 : -1;
        dog.Face(dir);                       // 往画面里跑（宠物原图朝左，ApplyFacing 负责镜像）
        dog.SetCutsceneControlled(true);

        // 速度按距离自适应：生成点在屏外，横穿半个屏（约 12 单位）若用固定速度要 4s+，
        // 观感拖沓还会撞看门狗。这里定成「1.2~3.2s 跑完全程」，跑动意图不变、时长稳定可预期。
        // 【2026-11-23 23:2x 主人「狗的移动速度也快 减一半」】倍率 3.5 → 1.75（时长同倍拉长），
        // 真源只这一处常量 DOG_RUN_SPEED_MUL，首次登场与精英归队共用。
        float dist = Mathf.Abs(toX - fromX);
        float dur = Mathf.Clamp(dist / (GameConfig.BASE_MOVE_SPEED * DOG_RUN_SPEED_MUL), 1.2f, 3.2f);
        float speed = dist > 0.01f ? dist / dur : 0f;

        float t = 0f;
        while (dog != null && t < dur + 0.3f && (toX - dog.transform.position.x) * dir > 0.15f)
        {
            dog.SetCutsceneVelocityX(speed * dir);
            t += Time.deltaTime;
            yield return null;
        }

        if (dog != null)
        {
            dog.SetCutsceneVelocityX(0f);
            dog.SetCutsceneControlled(false);   // 交还 AI：已在怪群里，自然开打
        }
        Debug.Log("[CutsceneDiag] 狗跑入 from=" + fromX.ToString("F2") + " to=" + toX.ToString("F2")
            + " dur=" + dur.ToString("F2") + " t=" + t.ToString("F2"));
    }

    IEnumerator RunDogRescueStep(BattleCutsceneStep step)
    {
        var talk = BattleHeadTalkUI.Ensure();
        switch (step)
        {
            case BattleCutsceneStep.SpawnPetFight:
            {
                // 【2026-11-23 22:4x 主人「狗没有从左边跑进来，还是突然出现的」】第一拍：
                // 狗生成在画面左缘外，这里逐帧给水平速度跑到怪群——玩家看到的是"从屏幕左侧跑进来"，
                // 不是原地 pop。跑动期锁继承 AI、朝向朝右（往画面里跑）。
                // 【2026-11-23 23:2x 主人「狗进来后没有汪汪、玩家也没说话，狗开始战斗了才开始说话，节奏不对」】
                // 跑完立刻进下一拍（BarkAtDog 狗叫 + ShoutAtHero 玩家喊话，整段冻场）——不再留 1.8s 观察窗：
                // 那段窗口里狗已交还 AI 开打，气泡被拖到开打之后。「怪围着狗」由落点保证（狗就是跑进怪群）。
                yield return CoRunDogIntoScene(_cutsceneDog, ResolveRescueDogSpawnX());
                break;
            }
            case BattleCutsceneStep.BarkAtDog:
            {
                Debug.Log("[CutsceneDiag] BarkAtDog ENTER talkNull=" + (talk == null) + " dogNull=" + (_cutsceneDog == null));
                if (talk == null) { Debug.LogError("[Cutscene] 气泡 UI 取不到，BarkAtDog 跳过"); }
                else if (_cutsceneDog != null)
                {
                    yield return talk.CoPlayLineAt(_cutsceneDog.transform, PetRescueStoryBeats.DogBark, PetRescueStoryBeats.WhimperHold);
                    Debug.Log("[CutsceneDiag] BarkAtDog DONE show=" + talk.IsShowing + " line=" + talk.CurrentLine);
                }
                else if (hero != null)
                {
                    yield return talk.CoPlayLine(hero, PetRescueStoryBeats.DogBark, PetRescueStoryBeats.WhimperHold);
                    Debug.Log("[CutsceneDiag] BarkAtDog DONE(hero锚点) show=" + talk.IsShowing + " line=" + talk.CurrentLine);
                }
                // 【2026-11-24】气泡段尾：本步是冻场段最后一句，协程 finally 解冻后置标志，
                // 让 Update 每帧把场上怪（含新刷的）强制锁玩家开打（主人「让怪都来打玩家」）。
                _dogRescueForceHero = true;
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
            case BattleCutsceneStep.WaitDogFled:
            {
                // 【2026-11-23 主人拍板·狗营救分镜】等狗被怪打空血跑掉（Pet.Die 的逃跑出口）。
                // 狗血≈玩家 1/10 又站在怪群前方，正常几秒就空；看门狗 18s 兜底强制撤场，绝不把演出卡死。
                float t = 0f;
                while (_cutsceneActive && !IsRescueDogFled() && t < PET_FLED_TIMEOUT)
                {
                    // 第 2 波已被清光但狗还没被打空：没怪可打，直接让它撤，别让玩家干等看门狗
                    if (CountAliveMonsters() <= 0)
                    {
                        Debug.Log("[CutsceneDiag] 第 2 波清空、狗未受伤 → 直接撤场");
                        if (_cutsceneDog != null) _cutsceneDog.FleeNow();
                        break;
                    }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log("[CutsceneDiag] 等狗被打跑 t=" + t.ToString("F2") + " fled=" + IsRescueDogFled());
                if (!IsRescueDogFled() && _cutsceneDog != null)
                {
                    Debug.LogWarning("[Cutscene] 狗 " + PET_FLED_TIMEOUT + "s 未被怪打跑，演出强制撤场（不卡剧情）");
                    _cutsceneDog.FleeNow();
                }
                yield return null;
                break;
            }
            case BattleCutsceneStep.DogFleeLine:
            {
                // 【2026-11-24 主人拍板】第 2 波清完后狗主动跑掉（不再等被怪打空血），
                // 先让狗起跑（CoFleeAway 往屏左跑），再冻场播逃跑旁白——观感是狗边跑边出字。
                if (_cutsceneDog != null && !_cutsceneDog.IsFled) _cutsceneDog.FleeNow();
                if (talk == null) { Debug.LogError("[Cutscene] 气泡 UI 取不到，DogFleeLine 跳过"); break; }
                // 逃跑旁白贴玩家（狗正往外跑，气泡不跟着跑，避免字飞出屏幕）
                if (hero != null)
                    yield return talk.CoPlayLine(hero, PetRescueStoryBeats.DogFlee, PetRescueStoryBeats.FleeHold);
                break;
            }
            case BattleCutsceneStep.ReleaseForElite:
            {
                // 【2026-11-24】首领来袭前清掉怪锁玩家：精英波怪正常选最近目标，不再强锁玩家
                _dogRescueForceHero = false;
                ClearMonsterForcedTargets();
                // 【2026-11-23 主人拍板】狗跑掉 + 第 2 波清完后才放行第 3 波：BeginNextWaveCountdown 下一帧自然推进 →
                // 关 3 精英末波走「首领来袭」大图（IsPetRescueEliteWaveAnnounce）→ 精英刷出。演出本身继续。
                IsOccupyingBattlefield = false;
                // 精英战是真打，屏幕精英血条从这里恢复显示（第 2 波是狗陪打的普通波，全程没有精英血条）
                BattleBossHpBar.SetCutsceneSuppressed(false);
                Debug.Log("[CutsceneDiag] 释放战场占用，等「首领来袭」+ 精英出场");
                yield return null;
                break;
            }
            case BattleCutsceneStep.WaitEliteInView:
            {
                // 【2026-11-23 主人「打到一半的时候狗从左侧进场助阵」】等精英血量掉到 ELITE_REJOIN_HP_RATIO
                // 以下才归队；10s 看门狗兜底（精英没被打/玩家清得快也不卡剧情）。步骤名沿用旧名，不改步骤表。
                float t = 0f;
                while (_cutsceneActive && !AnyEliteBelowHalfHp() && t < ELITE_REJOIN_TIMEOUT)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log("[CutsceneDiag] 等精英打到半血 t=" + t.ToString("F2") + " halfHp=" + AnyEliteBelowHalfHp());
                break;
            }
            case BattleCutsceneStep.DogRejoinElite:
            {
                // 狗归队帮打精英：走开战上阵同一 SpawnBattlePet 出口（旧狗已退场、不在 allyUnits → 重新生成）
                // 【2026-11-23 主人「狗从左侧进场助阵」】归队落点传画面左缘外（与首次登场同一生成点）。
                DeployRescuedPet(ResolveRescueDogLeftSpawnX());
                _cutsceneDog = FindRescueDog();
                Debug.Log("[CutsceneDiag] 狗归队帮忙 dogFound=" + (_cutsceneDog != null));
                if (_cutsceneDog == null)
                    Debug.LogError("[Cutscene] 狗归队生成失败（不兜底），后面台词退到玩家锚点");
                // 【2026-11-23 22:4x 同款】从画面左侧跑进精英战团，不是原地 pop 在左缘
                yield return CoRunDogIntoScene(_cutsceneDog, ResolveRescueDogSpawnX());
                break;
            }
            case BattleCutsceneStep.WaitSecondWaveCleared:   // 第 2 波（狗陪打）清完才放首领来袭
            case BattleCutsceneStep.WaitWaveCleared:         // 第 3 波（精英）清完才播战后台词
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

    /// <summary>狗是否已被打跑：引用已销毁（跑出屏幕后 Destroy）或已置逃跑标志都算。</summary>
    bool IsRescueDogFled() => _cutsceneDog == null || _cutsceneDog.IsFled;

    /// <summary>是否有活着精英血量已掉到一半以下（狗归队时机用）。</summary>
    bool AnyEliteBelowHalfHp()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i] as Monster;
            if (m == null || m.isDead || !m.IsEliteWave) continue;
            float maxHp = m.attr.GetAttr(AttrType.MaxHp);
            if (maxHp > 0f && m.currentHp <= maxHp * ELITE_REJOIN_HP_RATIO) return true;
        }
        return false;
    }

    /// <summary>
    /// 演出结束后补一次通关判定（2026-11-23）：末波清空若发生在演出期间，
    /// MonsterDeath 的 CoCheckWaveClearNextFrame 会被 CutsceneOccupyingBattlefield 闸掉，
    /// 演出释放后没人再调它 → 这里按同一口径补一次（只补触发，不另写结算逻辑）。
    /// </summary>
    void TryStartStageClearAfterCutscene()
    {
        if (!isInBattle || _stageCleared || _portalActive || _rewardSequenceStarted) return;
        if (SuppressStageClear) return;
        if (CutsceneOccupyingBattlefield) return;
        if (CountAliveMonsters() > 0) return;
        if (FindNextUnspawnedWaveIndex() >= 0) return;
        if (_totalMonstersSpawnedThisStage <= 0) return;
        _allWavesSpawned = true;
        StopWaveCountdown();
        Debug.Log("[Cutscene] 演出结束后补判定通关：场清波尽 → 进清场奖励链");
        StartStageClearRewardSequence();
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
