using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 波次系统
    // === 波次系统（规划/出怪在 WavePlanner；此处只留本局状态） ===
    WavePlanner _wavePlanner;
    internal WavePlanner Planner => _wavePlanner ?? (_wavePlanner = new WavePlanner(this));
    /// <summary>本关抽到的关卡模式名；空 = 没走模式逻辑（仍是旧均分波次）。UI 播报用。</summary>
    public string StageModeName => _wavePlanner != null ? _wavePlanner.StageModeName : "";
    /// <summary>本关模式的播报文案（如「两侧都有敌人」），给玩家 3 秒读条时间。</summary>
    public string StageModeTelegraph => _wavePlanner != null ? _wavePlanner.StageModeTelegraph : "";

    internal List<WaveData> _waves = new List<WaveData>();
    internal int _totalWaves = 0;
    internal bool _allWavesSpawned = false;
    private bool _stageCleared = false;
    internal int _totalMonstersSpawnedThisStage = 0;
    // 2026-09-26 主人拍板：按本关击杀的物理/魔法怪数量推导「主导怪物类型」，用于掉落按类型分池。
    int _physicalKills, _magicKills;
    /// <summary>当前进行中的波次下标；-1=尚未开刷</summary>
    internal int _activeWaveIndex = -1;
    private bool _waveAnnounceRunning;
    Coroutine _waveAnnounceCo;
    // 【2026-10-17 主人报「切屏回来战斗卡住，人物站着不动」】波次闸门看门狗计时（真实时间）
    float _waveGateStuckSec;
    /// <summary>连杀计数</summary>
    private int _killCombo;
    private float _lastKillTime;
    /// <summary>友军连杀加速倍率（1~1.5）。</summary>
    public float KillComboSpeedMul { get; private set; } = 1f;

    void SyncKillComboHaste()
    {
        KillComboSpeedMul = GameConfig.GetKillComboSpeedMul(_killCombo);
    }

    // === 技能能量 ===
    /// <summary>
    /// 玩家技能能量 0~1，<b>每个技能槽各一条</b>（V6）。索引 = RunLoadout 的技能槽序。
    /// 只在盟友受击时按「伤害/最大生命」回充（见 AddCombatSkillEnergy），<b>冷却中的槽不充能</b> —— 这条是天然错峰的关键。
    /// 槽序同时是自动释放优先级：SkillSystem.GetReadyPlayerSkill 取第一个「能量满且不在冷却」的技能。
    /// </summary>
    #endregion
    #region 关卡加载与波次执行

    // —— 本关掉落快照基线（冒险页获得奖励弹窗用，见 AdventureRewardNotice）——
    long _stageGoldEarnedAtStart;
    int _stageTalentAtStart;
    int _stageEnchantAtStart;
    int _stageMatsAtStart;
    int _stageEquipCountAtStart;

    /// <summary>进关时记一次 RunStats 基线；关末差值 = 本关实际掉落（唯一口径，别处不要再算一遍）。</summary>
    void SnapshotStageRewardBaseline()
    {
        _stageGoldEarnedAtStart = RunStats != null ? RunStats.GoldEarned : 0;
        _stageTalentAtStart = RunStats != null ? RunStats.TalentGained : 0;
        _stageEnchantAtStart = RunStats != null ? RunStats.EnchantStoneDelta : 0;
        _stageMatsAtStart = RunStats != null ? RunStats.DecomposeMatDelta : 0;
        _stageEquipCountAtStart = RunStats != null && RunStats.EquipGains != null ? RunStats.EquipGains.Count : 0;
    }

    public void LoadStage(StageData stage)
    {
        MonsterStatsTable.Reload();
        StageDropTable.Reload();
        StageSpawnTable.Reload();
        TutorialBattleTable.Reload();
        MonsterAttackStyleTable.Reload();
        BattleQuestTable.Reload();
        SpritePickWeightTable.Reload();
        WaveSlotTable.Reload();
        ChapterStatScaleTable.Reload();
        ChapterRouteTable.Reload();
        StageEventTable.Reload(); // 事件层 V1.0：每关重载（缺表/关闭时 Draw 返回 null，回退无事件）
        BossStageVariantTable.Reload(); // Boss 关变体 V1.0：每关重载（缺表/关闭时回退「只有 Boss 本体」）

        currentStage = stage;
        // 【2026-10-22 新增】本关掉落快照基线：关末用「现值 − 基线」算本关实际掉落，
        // 供冒险页「获得奖励」弹窗（AdventureRewardNotice，与结算同账不新造数值）。
        SnapshotStageRewardBaseline();
        ClearAllMonsters();
        _waves.Clear();
        _totalWaves = 0;
        _allWavesSpawned = false;
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        ResetPortalGateState();
        _totalMonstersSpawnedThisStage = 0;
        // 【2026-10-05 波次排查】一局内连打多关时，任务金币只发第一关：
        // _stageQuestGoldGranted 原先只在 StartNewRun 复位，LoadStage 没复位，
        // 第 1 关领过之后永久为 true → 第 2 关起 TryGrantStageQuestGold 直接 return。
        _stageQuestGoldGranted = false;
        _physicalKills = 0;
        _magicKills = 0;
        _eliteToastShownThisStage = false;
        AllowMonsterMapEnter = false;
        // 纯冷却制：清能量换成「解除间隔 + 按槽位错峰上初始 CD」
        ResetPlayerSkillGcd();
        PrimePlayerSkillOpeningCooldowns();
        // 上一局/上一关残留的攻击/防御/暴击增益（定时增益层）必须清掉，否则会跨关带着走
        Hero.Instance?.attr?.ClearTimedBuffs();
        ResetMpForNewStage();
        HeroThunderUltimate.Instance?.ResetForBattle();

        float startX = GetStageStartX();
        float z = unitRoot != null ? unitRoot.position.z : 0f;
        // 只摆玩家/佣兵，不要改写用户的 SpawnPoint 坐标
        GameConfig.SetWorldPosition(hero.gameObject, new Vector3(startX, UnitBase.GROUND_Y, z));
        // 【2026-10-20 主人拍板·方案C】进关不再满血：血量跨关延续 + 基础回复 30%（STAGE_ENTRY_HEAL_PCT），
        // 恢复关回血 / 抽奖「回血N」卡 / 老虎机回血才有意义。开局满血由 Hero.InitNewRun 保证，此处对满血无副作用。
        float heroMaxHp = hero.attr.GetAttr(AttrType.MaxHp);
        hero.currentHp = Mathf.Min(heroMaxHp, hero.currentHp + heroMaxHp * GameConfig.STAGE_ENTRY_HEAL_PCT);
        GameConfig.AttachToUnitRoot(hero.transform);
        Vector3 mercBasePos = new Vector3(startX, UnitBase.GROUND_Y, z);
        MercenaryManager.Instance?.ResetMercenaries(mercBasePos);
        // 【2026-10-17】阵亡佣兵下一关复活（兑现 2026-10-06 拍板；只在新一关生效，反复重打同一关不白刷
        // —— fallenStages 由「通关」TickFallenMercsStagePassed 推进）
        ReviveFallenMercsForStage();
        // 镜头先对齐；偏左显示
        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.offset = new Vector2(GameConfig.CAMERA_FOLLOW_OFFSET_X, 0f);
            follow.SetTarget(hero.transform);
        }
        isInBattle = true;
        Time.timeScale = 1f;
        FocusMarkSystem.Ensure()?.ResetForBattle();
        BattleUI.Instance?.EnsureBattleControls();
        // 【2026-10-11 主人拍板】**点装备弹出来的那几层同样要自动关掉** ——
        // 主人原话「点击装备出来的弹窗 开始战斗要自动关闭 跟背包一样」。
        // BackpackItemActionUI = 点装备后浮出的「装备 / 分解 / 丢弃」操作层；
        // EquipStatTipUI = 装备属性浮字。开打了还糊在屏幕上会挡住战斗 UI。
        BackpackItemActionUI.Instance?.Hide();
        EquipStatTipUI.Instance?.Hide();
        BattleUI.Instance?.EnsureRunSkillSlotsRepaired();
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(true);

        // 触发章节背景切换
        SwitchBattleBackground(CurrentChapter);

        // 按关卡类型切 BGM（Loading 中会记 pending，结束后再播）
        GameBgm.PlayForStage(stage.type);

        // 进关即写一次中断存档（后续由 BattleStateSaver 心跳续写退出时刻）
        BattleStateSaver.Instance?.SaveBattleState();

        switch (stage.type)
        {
            case StageType.Normal:
                Planner.SetupNormalWaves(stage.stageIndex);
                break;
            case StageType.Elite:
                Planner.SetupEliteWaves(stage.stageIndex);
                break;
            case StageType.Boss:
                Planner.SetupBossWave(stage.stageIndex);
                break;
            case StageType.Rest:
                isInBattle = false;
                LoadRestStage();
                break;
            default:
                // 商人/诅咒/锻造/附魔等：静默当普通战斗关
                stage.type = StageType.Normal;
                Planner.SetupNormalWaves(stage.stageIndex);
                break;
        }

        ResetWaveProgress();

        BattleUI.Instance?.UpdateCharacterSlots();
        BattleUI.Instance?.UpdateSkillEnergy(0, 0f);
        BattleUI.Instance?.RefreshBattleHud();
        // 进关把进度条归零：【2026-10-21 主人拍板】顶部进度条改按当前关波次推进，进关 = 0 进度
        if (currentStage != null)
            BattleUI.Instance?.UpdateWaveProgress(0, _waves?.Count ?? 0);
        int monsterGoal = GetStageMonsterGoal();
        var stageQuest = BattleQuestConfig.GetStageQuest(CurrentChapter, stage.type, IsGoldDungeon, BattleDifficulty);
        StageQuestClearGold = stageQuest.clearGold;
        // 徽记副本（2026-10-23 主人拍板「徽记副本不给金币」）：通关金清零，产出只有徽记
        // （BattleManager.Settlement → MercGrowInventory.GrantBadgeDungeonDrops）。
        // 不清会走普通 Elite 分支拿几十块通关金，HUD 也会显示 +N 金。
        if (IsBadgeDungeon) StageQuestClearGold = 0;
        // 支线岛（2026-10-23 合并方案）：不发关卡任务金，HUD 不显示 +N 金（岛金=goldReward，走岛结算链）
        if (IsSideIsland) StageQuestClearGold = 0;
        _stageQuestObjective = stageQuest.objective;
        BattleUI.Instance?.UpdateQuest(_stageQuestObjective, 0, monsterGoal, StageQuestClearGold);

        if (isInBattle)
        {
            if (Rules.DirectorOwnsWaves)
                Planner.PrepareTutorialWaves();
            PlacePartyAt(startX, z);
            UnitsCanAct = false;
            _battleIntroFinished = false;
            _battleStartTime = Time.unscaledTime;
            EnsureMonsterPrefabReady();
            Analytics.StageStart(CurrentChapter, stage.stageIndex); // 埋点：关卡开始

            Debug.Log($"[BattleManager] LoadStage 战斗关 type={stage.type} waves={_waves?.Count ?? 0} heroX={UnitBase.GetCombatX(hero):F2}");

            if (Instance != this)
                Debug.LogError("[BattleManager] 当前实例不是单例实例！说明存在重复 BattleManager，Update/协程不会在本实例上运行");
            if (!gameObject.activeInHierarchy)
                Debug.LogError($"[BattleManager] 宿主 {gameObject.name} 未激活，协程与 Update 不会执行");

            StopBattleSpawnCoroutines();
            StartCoroutine("BattleStartSequenceCoroutine");
            MercBattleBanter.EnsureOn(this);
            // 教程关由 TutorialDirector 控刷怪，禁止硬性首波/紧急刷怪抢跑
            if (!Rules.SkipFirstWaveAuto)
                _firstWaveHardFallbackCo = StartCoroutine(CoFirstWaveHardFallback());
        }
        else
        {
            UnitsCanAct = true;
            Debug.LogWarning($"[BattleManager] 非战斗关 type={stage.type}，不刷怪");
        }
    }

    Coroutine _firstWaveHardFallbackCo;
    /// <summary>
    /// 战前剧情（TryPlayPreBattle）或进关抽奖进行中：硬刷兜底须等它结束，防止剧透/选卡时刷怪抢跑。
    /// 2026-09-29 起进关抽奖也复用这个标志，见 <see cref="CoStageEntryDraft"/>。
    /// </summary>
    bool _preBattleStoryPlaying;

    /// <summary>停掉开战/硬刷协程，避免连续 LoadStage 叠多个兜底。</summary>
    public void StopBattleSpawnCoroutines()
    {
        StopCoroutine("BattleStartSequenceCoroutine");
        StopWaveAnnounce();
        // 战前剧情若在播放中被打断，标志位必须复位，否则 CoFirstWaveHardFallback 会永久卡在等待里
        _preBattleStoryPlaying = false;
        if (_firstWaveSpawnCo != null)
        {
            StopCoroutine(_firstWaveSpawnCo);
            _firstWaveSpawnCo = null;
        }
        if (_firstWaveHardFallbackCo != null)
        {
            StopCoroutine(_firstWaveHardFallbackCo);
            _firstWaveHardFallbackCo = null;
        }
    }
    /// <summary>无视走路门槛，立刻刷下一波；失败则紧急造怪（不依赖配置/图集）</summary>
    void ForceSpawnFirstWaveNow() => Planner.ForceSpawnFirstWaveNow();

    void EmergencySpawnVisibleMonsters(int count) => Planner.EmergencySpawnVisibleMonsters(count);


    void ResetWaveProgress()
    {
        _activeWaveIndex = -1;
        _allWavesSpawned = false;
        _killCombo = 0;
        _lastKillTime = 0f;
        SyncKillComboHaste();
        CombatJuice.ResetComboToast();
        _firstWaveSpawned = false;
        _battleIntroFinished = false;
        _battleStartTime = Time.unscaledTime;
        // 【2026-10-19 主人拍板】「击败」累计计数跨关卡不重置（原 ResetCombo 已删），新一局才在 StartNewRun 归零
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
    }
    /// <summary>波次为空时强制补一波（兜底）</summary>
    void EnsureAtLeastOneWave() => Planner.EnsureAtLeastOneWave();


    void ScheduleFirstWaveSpawn()
    {
        if (_firstWaveSpawned) return;
        TrySpawnFirstWaveOnce();
        if (_firstWaveSpawned || _firstWaveSpawnCo != null) return;
        _firstWaveSpawnCo = StartCoroutine(FirstWaveSpawnRetry());
    }

    IEnumerator FirstWaveSpawnRetry()
    {
        yield return new WaitForSecondsRealtime(0.35f);
        _firstWaveSpawnCo = null;
        TrySpawnFirstWaveOnce();
    }

    /// <summary>首波刷怪（含兜底）；成功才标记 _firstWaveSpawned</summary>
    void TrySpawnFirstWaveOnce()
    {
        if (BattleLootMode.Active) return;
        if (!isInBattle || _stageCleared) return;
        if (!Rules.SkipFirstWaveAuto && !_battleIntroFinished) return;
        // 教程关禁止自动首波/紧急刷怪（否则会从玩家身上穿出来）
        if (Rules.SkipFirstWaveAuto) return;
        if (CountAliveMonsters() > 0)
        {
            _firstWaveSpawned = true;
            return;
        }

        ForceSpawnFirstWaveNow();
        if (CountAliveMonsters() > 0)
            _firstWaveSpawned = true;
        else
        {
            // 常规全失败 → 紧急造怪，保证玩家一定能看到怪
            Debug.LogError("[BattleManager] 首波常规刷怪失败 → EmergencySpawnVisibleMonsters");
            EmergencySpawnVisibleMonsters(Mathf.Min(3, GameConfig.WAVE_MONSTER_MAX));
            if (CountAliveMonsters() > 0)
                _firstWaveSpawned = true;
        }
    }

    /// <summary>硬性保险：过场/协程被停也能刷出第一波（独立协程，不被 StopCoroutine(string) 误伤）</summary>
    IEnumerator CoFirstWaveHardFallback()
    {
        while (!_battleIntroFinished || _preBattleStoryPlaying)
            yield return null;
        yield return new WaitForSecondsRealtime(2.2f);
        if (!isInBattle || _stageCleared || _firstWaveSpawned) yield break;
        if (CountAliveMonsters() > 0)
        {
            _firstWaveSpawned = true;
            yield break;
        }

        Debug.LogError("[BattleManager] 硬性保险触发：2.2s 仍无怪 → EmergencySpawnVisibleMonsters");
        TrySpawnFirstWaveOnce();
        if (CountAliveMonsters() == 0)
            EmergencySpawnVisibleMonsters(Mathf.Min(3, GameConfig.WAVE_MONSTER_MAX));
        if (CountAliveMonsters() > 0)
            _firstWaveSpawned = true;
    }

    /// <summary>黑屏章节名 → 揭幕 → 队伍从左侧屏外走入开战位 → 再前进/刷怪</summary>
    IEnumerator BattleStartSequenceCoroutine()
    {
        UnitsCanAct = false;
        _battleIntroFinished = false;
        PartyIntroWalking = false;
        _runClosed = false;   // 新一局开始：清掉上一局的收尾标志（撤离/死亡置位，见字段注释）

        float startX = GetStageStartX();
        float z = hero != null ? hero.transform.position.z
            : (unitRoot != null ? unitRoot.position.z : 0f);
        float enterX = GetPartyEnterX(startX);

        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
            follow.offset = new Vector2(GameConfig.CAMERA_FOLLOW_OFFSET_X, 0f);

        // 黑屏期间：镜头对准开战位，队伍在左屏外待命（揭幕后再走进来）
        PlacePartyAt(enterX, z);
        SnapCameraToBattleView(startX);
        if (follow != null) follow.PauseFollowX();

        ExtendCameraMaxX(startX + 80f);

        var parallax = FindObjectOfType<ParallaxBackground>();
        if (parallax != null) parallax.ResetHeroOrigin();

        // 【2026-10-20 主人拍板】进战斗顺序 = Loading 装载完 → 章节卡演完 → 队伍进场开打。
        // 本协程在场景激活帧就启动（AutoGameInitializer.Awake 同步 StartNewRun），
        // 而章节卡要等 Loading 关闭后才 Show（见 GameSceneManager.LoadBattleAsync）——
        // 必须在这里等它结束，否则走场 / 战前剧情 / 抽奖 / 首波全会在黑卡底下抢跑。
        // 超时兜底（15s）防卡死：章节卡正常 hold+fade ≈ 4s，宽裕够用。
        float splashGuard = 0f;
        while (ChapterSplashOverlay.BattleSplashPending && splashGuard < 15f)
        {
            splashGuard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }
        var battleSplash = ChapterSplashOverlay.Current;
        splashGuard = 0f;
        while (battleSplash != null && !battleSplash.IsFinished && splashGuard < 15f)
        {
            splashGuard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }

        // 章节开场卡已由 GameSceneManager 在 Loading 关闭后演出完毕（2026-10-20 起顺序：Loading → 章节卡 → 开打）。
        yield return CoPartyWalkInFromLeft(startX, z);

        FinishBattleIntro(follow);

        if (Rules.DirectorOwnsWaves)
        {
            // 【2026-10-08 主人拍板】引导局开局不抽奖 —— 先打完第一波，再抽装备，
            // 玩家才看得到「换了家伙之后变强」的对比。第一抽改由 TutorialDirector.BattleRoutine
            // 在第一波清场后弹（CoMidBattleDraft）。
            // 旧口径（2026-09-29「引导关也走金币抽奖，在 Director 接管波次之前抽」）已作废。
            // NotifyBattleSplashFinished 只是启动引导协程，晚一点调用不会打断它。
            if (!IsTutorialRun)
                yield return CoStageEntryDraft();
            TutorialDirector.Instance?.NotifyBattleSplashFinished();
        }
        else
            // 时序：过场卡→队伍进场→战前剧情→抽奖→首波。
            // 协程内部已包含“剧情不可用则直接放行首波”的兜底逻辑。
            yield return CoPreBattleStoryThenFirstWave();

        // 【2026-10-18 主人报「撤离结算下面还能点抽奖」】抽奖等待期间撤离/死亡 → 上游协程当场退场：
        // 不再 StartPartyAdvance（结算界面后面队伍乱跑），也不再走开战日志分支。
        if (_runClosed) yield break;

        // 2026-09-30：抽完奖才出发 —— 抽奖期间队伍在开战位原地静止（见 FinishBattleIntro）
        StartPartyAdvance(follow);

        // 【2026-10-07 主人拍板】去掉「开始游戏」大字横幅。
        // 这里原本会在抽完奖点「继续」后先甩出大字、再正式开打（2026-09-30 加入）。
        // 现在不要了，直接移除调用（不留开关，符合「加保险·不多入口·不兜底」）。
        // StageStartBannerUI 类随之停用。
        // ⚠ 注：此处原注释写着「主人要求」，但主人 2026-10-07 明确说并未要求过加这个字 ——
        //    那是当时的误记。**凡涉及「主人要求 / 主人拍板」的归因，没听清就必须当场问，不许替主人脑补。**

        if (hero != null && monsters.Count > 0)
        {
            var m0 = monsters[0];
            float hx = UnitBase.GetCombatX(hero);
            float mx = UnitBase.GetCombatX(m0);
            Debug.Log($"[BattleManager] 开战完成 monsters={monsters.Count} heroX={hx:F2} mon0={m0?.name} monX={mx:F2} dist={Mathf.Abs(hx - mx):F2} monHp={m0?.currentHp:F0} scale={m0?.transform.localScale}");
        }
        else if (!Rules.DirectorOwnsWaves)
            Debug.LogError($"[BattleManager] 开战完成仍无怪 monsters={monsters.Count} waves={_waves?.Count ?? 0}");
        else
            Debug.Log("[BattleManager] 教程开战完成，引导已在入场结束后启动");
    }

    /// <summary>左屏外走进开战位；镜头固定在开战视角，不跟到屏外。</summary>
    IEnumerator CoPartyWalkInFromLeft(float targetHeroX, float z)
    {
        if (hero == null)
        {
            PlacePartyAt(targetHeroX, z);
            yield break;
        }

        float speed = Mathf.Max(0.5f, hero.attr.GetAttr(AttrType.MoveSpeed));
        float timeout = 12f;
        float elapsed = 0f;

        SnapCameraToBattleView(targetHeroX);
        if (Camera.main != null)
        {
            var follow = Camera.main.GetComponent<CameraFollow>();
            if (follow != null) follow.PauseFollowX();
        }
        PartyIntroWalking = true;
        SetPartyWalkAnim(true);

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            float hx = UnitBase.GetCombatX(hero);
            if (hx >= targetHeroX - 0.06f)
                break;

            float nx = Mathf.Min(targetHeroX, hx + speed * Time.deltaTime);
            PlacePartyAt(nx, z);
            SnapCameraToBattleView(targetHeroX);
            yield return null;
        }

        PlacePartyAt(targetHeroX, z);
        SnapCameraToBattleView(targetHeroX);
    }

    /// <summary>
    /// 走进场结束：队伍**停在开战位静止**，等玩家抽完奖再出发。
    /// 2026-09-30 主人要求：进关先站着不动，抽奖面板走完才前进。
    /// 出发动作见 <see cref="StartPartyAdvance"/>。
    /// </summary>
    void FinishBattleIntro(CameraFollow follow)
    {
        if (hero != null)
        {
            float z = hero.transform.position.z;
            PlacePartyAt(UnitBase.GetCombatX(hero), z);
            if (hero.rb != null) hero.rb.velocity = Vector2.zero;

            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                {
                    var m = mercs[i];
                    if (m == null || m.rb == null) continue;
                    m.rb.velocity = Vector2.zero;
                }
            }
            SetPartyWalkAnim(false);
        }

        // 镜头保持暂停：抽奖期间画面固定在开战视角，不跟队伍跑
        PartyIntroWalking = false;
        _battleIntroFinished = true;
        UnitsCanAct = false;
        MercBattleBanter.EnsureOn(this);
    }

    /// <summary>
    /// 抽奖结束 → 队伍出发：给速度、开战斗 AI、恢复镜头跟随、放出摇杆。
    /// 与 <see cref="FinishBattleIntro"/> 配对使用，中间夹着进关抽奖。
    /// </summary>
    void StartPartyAdvance(CameraFollow follow)
    {
        if (hero != null)
        {
            float spd = Mathf.Max(0.5f, hero.attr.GetAttr(AttrType.MoveSpeed));
            if (hero.rb != null)
                hero.rb.velocity = new Vector2(spd, 0f);

            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                {
                    var m = mercs[i];
                    if (m == null || m.rb == null) continue;
                    m.rb.velocity = new Vector2(Mathf.Max(0.5f, m.attr.GetAttr(AttrType.MoveSpeed)), 0f);
                }
            }
            SetPartyWalkAnim(true);
        }

        if (follow != null && hero != null)
            follow.ResumeFollowX(hero.transform);
        else if (follow != null)
            follow.ResumeFollowX();

        UnitsCanAct = true;
        // 【2026-10-11 排查】主人报「开始战斗后玩家没有往前走 还上下移动了一下」：
        // 出发后连采 6 次队伍状态（x / 速度 / 车道 / 最近怪距离），拿真值定位是「索敌不到」还是「进了射程就站桩」。
        StartCoroutine(CoLogPartyAdvance());
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(true);
    }

    /// <summary>
    /// 【2026-10-11 排查用，定位后即删】出发后每 0.25s 采一次队伍状态。
    /// 主人报「开始战斗后玩家没有往前走 还上下移动了一下」，这里把 x / 速度 / 车道 / 最近怪距离打到日志，
    /// 好判断是「没索到敌」还是「一进射程就站桩」。
    /// </summary>
    IEnumerator CoLogPartyAdvance()
    {
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            var h = hero;
            if (h == null) yield break;
            float hx = UnitBase.GetCombatX(h);
            float best = float.MaxValue;
            string nm = "无";
            for (int k = 0; k < monsters.Count; k++)
            {
                var mo = monsters[k];
                if (mo == null || mo.isDead) continue;
                float d = Mathf.Abs(hx - UnitBase.GetCombatX(mo));
                if (d < best) { best = d; nm = mo.name; }
            }
            Debug.Log($"[BattleManager] 出发采样#{i + 1} heroX={hx:F2} velX={(h.rb != null ? h.rb.velocity.x : 0f):F2} "
                + $"laneY={h.LaneY:F2} 射程={h.GetEffectiveAttackRange():F2} 活怪={CountAliveMonsters()} "
                + $"最近怪={nm} 距离={(best < float.MaxValue ? best.ToString("F2") : "-")}");
        }
    }

    float GetPartyEnterX(float battleStartX)
    {
        Camera cam = Camera.main;
        float halfW = 4f;
        if (cam != null && cam.orthographic)
            halfW = cam.orthographicSize * cam.aspect;
        float camX = battleStartX + GameConfig.CAMERA_FOLLOW_OFFSET_X;
        return camX - halfW - PARTY_ENTER_FROM;
    }

    void SnapCameraToBattleView(float battleStartX)
    {
        if (Camera.main == null) return;
        Vector3 cp = Camera.main.transform.position;
        cp.x = battleStartX + GameConfig.CAMERA_FOLLOW_OFFSET_X;
        Camera.main.transform.position = cp;
    }

    void SetPartyWalkAnim(bool walking)
    {
        SetUnitWalkAnim(hero, walking);
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
            SetUnitWalkAnim(mercs[i], walking);
    }

    static void SetUnitWalkAnim(UnitBase unit, bool walking)
    {
        if (unit == null) return;
        var anim = unit.GetComponent<UnitAnimation>();
        if (anim != null)
            anim.SetMove(walking, 1);
    }

    /// <summary>等切场景 Loading 完全关掉，避免与章节黑屏叠在一起。</summary>
    static IEnumerator CoWaitForLoadingOverlayGone()
    {
        const float maxWait = 15f;
        float guard = 0f;
        while ((SceneLoadingCoordinator.IsActive || BattleLoadingOverlay.IsShowing) && guard < maxWait)
        {
            guard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }
        yield return null;
    }

    /// <summary>按间距摆玩家+佣兵（佣兵在身后，不再挤压到重叠）</summary>
    void PlacePartyAt(float heroX, float z)
    {
        Vector3 heroPos = new Vector3(heroX, UnitBase.GROUND_Y, z);
        if (hero != null)
        {
            if (hero.rb != null) hero.rb.velocity = Vector2.zero;
            GameConfig.AttachToUnitRoot(hero.transform);
            GameConfig.SetWorldPosition(hero.gameObject, heroPos);
            hero.facingDir = 1;
            EnsureSpritesEnabled(hero.transform);
        }

        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null) continue;
            if (m.rb != null) m.rb.velocity = Vector2.zero;
            GameConfig.AttachToUnitRoot(m.transform);
            float mx = hero != null
                ? UnitCrowd.GetMercDesiredCombatX(hero, m, i)
                : heroX + MERC_FRONT_SPACING * (i + 1);
            GameConfig.SetWorldPosition(m.gameObject, new Vector3(mx, UnitBase.GROUND_Y, z));
            m.SetPartyIndex(i);
            // 入场每帧 Face/ApplyFacing 会反复写 Visual scale，加重顿挫；朝向只在未正对时设一次
            if (m.facingDir != 1)
                m.Face(1);
            else
                m.facingDir = 1;
            EnsureSpritesEnabled(m.transform);
        }
    }

    /// <summary>只恢复此前误关的 Sprite</summary>
    static void EnsureSpritesEnabled(Transform root)
    {
        if (root == null) return;
        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] != null && !srs[i].enabled)
                srs[i].enabled = true;
        }
    }

    /// <summary>舞台起始站位 X：优先用场景 SpawnPoint（不要用镜头位置，否则会「跑到场景中间」）</summary>
    internal float GetStageStartX()
    {
        if (spawnPoint != null)
            return spawnPoint.position.x + GameConfig.SPAWN_X_LEFT_BIAS;

        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
            return cam.transform.position.x - 1.2f + GameConfig.SPAWN_X_LEFT_BIAS;
        return -7f + GameConfig.SPAWN_X_LEFT_BIAS;
    }
    /// <summary>
    /// 立即刷下一波未刷出的波次（定时/加速共用）。怪刷在玩家前方并朝玩家移动。
    /// </summary>
    void SpawnNextPendingWave() => Planner.SpawnNextPendingWave();


    /// <summary>清完当前波后播放「下一波来袭」再出兵</summary>
    void BeginNextWaveCountdown()
    {
        if (Rules.SkipWaveAnnounce) return;
        if (_allWavesSpawned || _portalActive || _stageCleared) return;
        if (FindNextUnspawnedWaveIndex() < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            return;
        }
        if (CountAliveMonsters() > 0) return;
        if (_waveAnnounceCo != null) return;

        // 【2026-10-21 主人拍板】恢复关下线后血线口子消失 → 普通战斗波清完时全队回血（WaveClearHealRatio）。
        // 本方法是权威波清点：MonsterDeath.CoCheckWaveClearNextFrame 与 Update 清波检测两路都汇聚到这里，
        // _waveAnnounceCo 闸门保证每波只回一次；末波/Boss 波清完走 StartStageClearRewardSequence 通关链，
        // 不进本方法，不与通关链上的 TickFallenMercsStagePassed 回血叠加。死亡单位不复活、满血无操作。
        // 引导局波次由 TutorialDirector 直刷、不走本方法（按拍板不做特判）。
        // 【2026-10-22 主人拍板 v4.1】波清不回血：治疗出口只剩石像恢复事件（BattleStatueEventUI）、
        // 佣兵复活（护符/石像/下一关 fallen 养血）、通关链原有恢复（进关基础回复 STAGE_ENTRY_HEAL_PCT 等）。
        // HealPartyOnWaveCleared() 调用停用（注释保留）；函数与 WaveClearHealRatio 常量保留不删（下线≠删除）。
        // HealPartyOnWaveCleared();

        // 【2026-10-21 主人拍板】顶部进度条改按当前关波次推进：已清波数 = 下一个未刷波索引
        //（FindNextUnspawnedWaveIndex 返回第一个未刷波的下标 = 已刷完的波数，见 WavePlanner.Advance.cs:216）
        BattleUI.Instance?.UpdateWaveProgress(Mathf.Max(0, FindNextUnspawnedWaveIndex()), _waves != null ? _waves.Count : 0);

        GamePerf.Log("[BattleManager] 下一波来袭 Banner");
        // 【2026-10-21 主人拍板】每清完一波停下弹一次抽奖窗（一抽），点「继续」或倒计时走完
        // 再播「下一波来袭」进下一波。整段（波间抽奖 + 预告 + 出兵）都在 _waveAnnounceCo 里，
        // :671 `if (_waveAnnounceCo != null) return;` 的防重入语义不变。
        _waveAnnounceCo = StartCoroutine(CoWaveIntermissionDraftThenNextWave());
    }

    /// <summary>
    /// 【2026-10-21 主人拍板】波间单拍抽奖窗 → 原预告+出兵链。
    /// 复用 CoMidBattleDraft：自带 UnitsCanAct 冻结/finally 还原、面板 3 秒没弹出的防卡死跳过、
    /// 正式关 30s 倒计时自动继续、一抽即收。其等待循环不查 _runClosed（进关抽奖既有口径，不动），
    /// 下面的 guard 就是波间这条链的防线：撤离/死亡/通关后旧协程当场退场，不串台。
    /// 末波/Boss 波清完走 StartStageClearRewardSequence 通关链，不进本方法 → 末波后无波间抽；
    /// 进关抽奖（CoStageEntryDraft）承担下一关第一抽，正好「每波之间恰好一抽」。
    /// </summary>
    IEnumerator CoWaveIntermissionDraftThenNextWave()
    {
        // 【2026-10-22 主人拍板】抽奖节拍收窄到回合末（只对主线正式关生效）：
        // 【2026-10-22 主人拍板 v4.2】回合末判定作废旧口径「下一未刷波下标 % 3 == 0」（波数按章可变后回合长不再固定 3），
        // 改读铺波时落在 WaveData 上的回合末标记：刚清完的那波（next-1）isRoundEnd == true 才算回合末。
        // Boss 波不算回合末（isRoundEnd 恒 false），末回合清完走通关链不进本方法。
        // CoMidBattleDraft / CoStageEntryDraft 本体不改；进关第一次抽奖不掷石像（CoStageEntryDraft 未动）。
        // 引导局整条早已 SkipWaveAnnounce 跳过，保持；金币本/裂隙保持 2026-10-21 口径：每波照旧弹（行为不变）。
        int next = FindNextUnspawnedWaveIndex();
        bool formalRoundEnd = IsFormalMainRun
            && next > 0
            && _waves != null && next - 1 < _waves.Count
            && _waves[next - 1] != null
            && _waves[next - 1].isRoundEnd;
        if (IsFormalMainRun && !formalRoundEnd)
        {
            // 正式关非回合末：不弹抽奖，直接预告 + 出兵
            yield return CoAnnounceThenSpawnNextWave();
            yield break;
        }

        if (formalRoundEnd)
        {
            // 【2026-10-22 主人拍板】石像事件：回合末抽奖窗弹出前掷 10%（每关最多 1 次，恢复/诅咒互斥）
            BattleStatueEventUI.TryShowOnDraft();
        }

        yield return CoMidBattleDraft();

        // 【2026-10-22 主人拍板】抽奖窗关闭（点继续/超时）未点石像 → 石像一并收起，不结转
        BattleStatueEventUI.ForceCollapse();

        // 防撤离/死亡/通关后旧协程串台：当场退场（协程被外部 StopWaveAnnounce 停时同样安全）
        if (_runClosed || !isInBattle || _stageCleared)
            yield break;

        yield return CoAnnounceThenSpawnNextWave();
    }

    /// <summary>
    /// 【2026-10-21 主人拍板】每个战斗波清完时的全队回血比例（占各自最大生命，可调）。
    /// 恢复关（RestStagePopupUI 流程）下线后补上的血线口子（P1-3 方案②）。
    /// </summary>
    internal const float WaveClearHealRatio = 0.10f;

    /// <summary>
    /// 【2026-10-21 主人拍板】波清回血：主角 + 全体存活佣兵各回 最大生命 × WaveClearHealRatio。
    /// 复用佣兵治疗技同款回血原语 SkillCastService.ApplyHealToUnit（钳上限 + 治疗飘字 + 治疗量统计），
    /// 不新建第二份回血、不额外加 UI。
    /// 【2026-10-22 主人拍板 v4.1】停用：唯一调用点（BeginNextWaveCountdown）已注释；函数与常量保留不删（下线≠删除）。
    /// </summary>
    void HealPartyOnWaveCleared()
    {
        HealUnitOnWaveCleared(hero);
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
            HealUnitOnWaveCleared(mercs[i]);
    }

    void HealUnitOnWaveCleared(UnitBase unit)
    {
        // 死亡单位不复活；满血时 ApplyHealToUnit 内 gained≤0 自然无操作、无飘字
        if (unit == null || unit.isDead || unit.attr == null) return;
        SkillCast.ApplyHealToUnit(unit, unit.attr.GetAttr(AttrType.MaxHp) * WaveClearHealRatio);
    }

    void StopWaveAnnounce()
    {
        if (_waveAnnounceCo != null)
        {
            StopCoroutine(_waveAnnounceCo);
            _waveAnnounceCo = null;
        }
        BattleWaveAnnounceUI.Cancel();
        _waveAnnounceRunning = false;
    }

    internal void StopWaveCountdown()
    {
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
    }

    void TickWaveAnnounceInterrupt()
    {
        if (!_waveAnnounceRunning || !UnitsCanAct || _stageCleared || _portalActive)
            return;

        if (CountAliveMonsters() > 0)
        {
            StopWaveAnnounce();
            StopWaveCountdown();
        }
    }

    IEnumerator CoAnnounceThenSpawnNextWave()
    {
        if (_waveAnnounceRunning || _stageCleared || _portalActive) yield break;
        if (FindNextUnspawnedWaveIndex() < 0) yield break;

        // 【2026-10-17 主人报「切屏回来战斗卡住，人物站着不动」】_waveAnnounceRunning / _waveAnnounceCo
        // 是「有借有还」闸门：预告协程若在播放中被中断（协程被停 / 播放链抛异常 / 切后台恢复首帧异常），
        // 标志没人清 → Update 里 `if (_waveAnnounceCo != null) return` 永远进不去，
        // 下一波永远不刷，英雄干站着没怪可打。与 2026-10-05 中途抽奖 UnitsCanAct 卡死同形态，
        // 同样用 try/finally 兜住归还。
        try
        {
            _waveAnnounceRunning = true;
            int nextIdx = FindNextUnspawnedWaveIndex();
            bool boss = nextIdx >= 0 && _waves != null && nextIdx < _waves.Count
                        && _waves[nextIdx] != null && _waves[nextIdx].isBossWave;
            var kind = boss ? BattleWaveAnnounceUI.Kind.Boss : BattleWaveAnnounceUI.Kind.NextWave;
            // 带上本波原型播报（箭雨 / 夹击 / 围杀…），让玩家知道这波跟上一波不一样。
            // 【2026-10-07 主人拍板】Boss 波已经有「首领来袭」大图，下面不再叠一行字。
            string announce = (boss || Planner == null) ? "" : Planner.WaveAnnounceText(nextIdx);

            yield return BattleWaveAnnounceUI.CoPlay(kind, announce);
        }
        finally
        {
            _waveAnnounceRunning = false;
            _waveAnnounceCo = null;
            StopWaveCountdown();
        }

        if (!_stageCleared && !_portalActive && CountAliveMonsters() <= 0)
            SpawnNextPendingWave();
    }

    IEnumerator CoPlayWaveAnnounceOnly()
    {
        if (_waveAnnounceRunning || _stageCleared || _portalActive) yield break;
        if (FindNextUnspawnedWaveIndex() < 0) yield break;

        _waveAnnounceRunning = true;
        int nextIdx = FindNextUnspawnedWaveIndex();
        bool boss = nextIdx >= 0 && _waves != null && nextIdx < _waves.Count
                    && _waves[nextIdx] != null && _waves[nextIdx].isBossWave;
        var kind = boss ? BattleWaveAnnounceUI.Kind.Boss : BattleWaveAnnounceUI.Kind.NextWave;
        // 带上本波原型播报（箭雨 / 夹击 / 围杀…），让玩家知道这波跟上一波不一样。
        // 【2026-10-07 主人拍板】Boss 波已经有「首领来袭」大图，下面不再叠一行字。
        string announce = (boss || Planner == null) ? "" : Planner.WaveAnnounceText(nextIdx);
        yield return BattleWaveAnnounceUI.CoPlay(kind, announce);
        _waveAnnounceRunning = false;
    }

    [System.Obsolete("Countdown end spawns directly; announce runs at countdown start.")]
    IEnumerator CoAnnounceAndSpawnNextWave()
    {
        yield return CoPlayWaveAnnounceOnly();
        if (!_stageCleared && !_portalActive && CountAliveMonsters() <= 0)
            SpawnNextPendingWave();
    }

    /// <summary>预告播放中点击加速：立刻出兵</summary>
    public bool TrySkipToNextWave()
    {
        if (!isInBattle || _stageCleared || _portalActive) return false;
        if (!_waveAnnounceRunning) return false;
        if (CountAliveMonsters() > 0) return false;
        if (FindNextUnspawnedWaveIndex() < 0) return false;

        UIManager.Instance?.ShowToast("加速出兵");
        GamePerf.Log("[BattleManager] 加速下一波");

        StopWaveAnnounce();
        StopWaveCountdown();
        SpawnNextPendingWave();
        return true;
    }

    /// <summary>兼容旧调用名</summary>
    void TrySpawnWaveByProgress() => SpawnNextPendingWave();
    void TrySpawnWavesApproachingScreen() => SpawnNextPendingWave();
    int FindNextUnspawnedWaveIndex() => Planner.FindNextUnspawnedWaveIndex();


    int _aliveMonsterCache = -1;
    int _aliveMonsterCacheFrame = -1;

    internal int CountAliveMonsters()
    {
        int frame = Time.frameCount;
        if (_aliveMonsterCacheFrame == frame && _aliveMonsterCache >= 0)
            return _aliveMonsterCache;

        int n = 0;
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null && !monsters[i].isDead) n++;
        _aliveMonsterCache = n;
        _aliveMonsterCacheFrame = frame;
        return n;
    }

    /// <summary>
    /// 生成/移除怪物后必须调用：CountAliveMonsters 的缓存只按帧失效，
    /// 同一帧内刷完怪立刻再读会拿到刷怪前的旧值（曾导致引导关误判"没刷出来"而补刷一整波）。
    /// </summary>
    internal void InvalidateAliveMonsterCache()
    {
        _aliveMonsterCacheFrame = -1;
        _aliveMonsterCache = -1;
    }

    int GetStageMonsterGoal()
    {
        int goal = 0;
        if (_waves != null)
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                var w = _waves[i];
                if (w == null) continue;
                if (Rules.QuestCountsOnlySpawnedWaves && !w.spawned) continue;
                goal += w.monsterCount;
            }
        }
        return Mathf.Max(1, goal);
    }

    public void RefreshStageQuestProgress()
    {
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        BattleUI.Instance?.UpdateQuest(_stageQuestObjective, defeated, goal, StageQuestClearGold);
    }

    public static void GetBattleVisibleX(out float minX, out float maxX, float pad = 0.7f)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            minX = -6f;
            maxX = 6f;
            return;
        }
        float halfW = cam.orthographicSize * cam.aspect;
        float cx = cam.transform.position.x;
        minX = cx - halfW + pad;
        maxX = cx + halfW - pad;
        if (maxX - minX < 1.5f)
        {
            minX = cx - 2f;
            maxX = cx + 2f;
        }
    }

    internal void EnsureMonsterPrefabReady()
    {
        // 【2026-10-05 主人拍板「资源层 fail fast」】原先 PoolManager 为空是静默 return，
        // 怪刷不出来后波次层只会无限重播「下一波来袭」。这里必须报出来。
        if (PoolManager.Instance == null)
        {
            Debug.LogError("[BattleManager] PoolManager 未就绪，怪物预制体无从加载");
            return;
        }
        if (PoolManager.Instance._monsterPrefab != null) return;
        var prefab = Resources.Load<GameObject>("Prefabs/Monster/Monstersmoban")
                  ?? Resources.Load<GameObject>("Prefabs/Monster/Monster");
        if (prefab != null)
        {
            PoolManager.Instance.Preload("Monster", prefab, 8);
            PoolManager.Instance._monsterPrefab = prefab;
            Debug.Log("[BattleManager] 补载怪物预制体: " + prefab.name);
        }
        else
            Debug.LogError("[BattleManager] 找不到怪物预制体 Prefabs/Monster/Monstersmoban");
    }

    /// <summary>
    /// 切换战斗背景：根据章节加载对应的视差背景
    /// </summary>
    void SwitchBattleBackground(int chapter)
    {
        ParallaxBackground parallax = FindObjectOfType<ParallaxBackground>();
        if (parallax != null)
        {
            parallax.SwitchBackground(chapter);
            parallax.ResetHeroOrigin();
        }
        else
        {
            Debug.LogWarning("[BattleManager] ParallaxBackground未找到，跳过背景切换");
        }
    }

    // ============================================================
    // Update
    // ============================================================

    void Update()
    {
        // 可行走区可视化标记跟着 walk 走（map 展开会改 map 的位置+缩放，
        // walk 是 map 的子节点所以真值变了；标记挂世界空间不会自己跟）。幂等、默认不可见。
        BattleLaneBounds.Tick();

        if (BattleLootMode.Active) return;
        if (!isInBattle || _stageCleared) return;

        if (UnitsCanAct)
            RunStats.BattleTimeSec += Time.deltaTime;

        PlayerSkillPassive.TickAutoCast();
        // 升级三选一：可行动时消费升级队列
        RunDraftDirector.Instance?.Tick();

        // 首波刷怪：主路径 ScheduleFirstWaveSpawn；兜底仅 CoFirstWaveHardFallback（勿在 Update 叠刷）

        // 清理死怪（不要在刷怪同一帧误清：只移真正死亡的）
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            var m = monsters[i];
            if (m == null)
            {
                monsters.RemoveAt(i);
                continue;
            }
            if (m.isDead)
            {
                m.OnDead -= OnMonsterDead;
                monsters.RemoveAt(i);
            }
        }

        // 清波后的下一波倒计时（可点击加速）
        if (UnitsCanAct)
            TickWaveAnnounceInterrupt();

        UnitCrowd.TickMonsterOverlapStacks();

        // 连杀超时清零（只清加成数值；【2026-10-19】「击败」常驻计数不再跟随隐藏）
        if (_killCombo > 0 && Time.time - _lastKillTime > GameConfig.COMBO_WINDOW)
        {
            _killCombo = 0;
            SyncKillComboHaste();
        }

        // 首波已刷完且场上清空：由「下一波来袭」推进下一波（不在此 Emergency）
        if (!Rules.SkipWaveAnnounce
            && UnitsCanAct && !_stageCleared && !_portalActive
            && _firstWaveSpawned
            && _waveAnnounceCo == null && !_waveAnnounceRunning
            && CountAliveMonsters() == 0 && _waves != null && _waves.Count > 0
            && FindNextUnspawnedWaveIndex() >= 0)
        {
            BeginNextWaveCountdown();
        }

        // 【2026-10-17 主人报「切屏回来战斗卡住，人物站着不动」】波次闸门看门狗：
        // 「可行动 + 没怪 + 还有波没刷 + 预告闸门挂着 + timeScale 正常」连续 5 秒（真实时间）
        // 即判定预告协程卡死（切后台 / 中断后标志没归还）。报错让主人看见，并按玩家
        // 「加速出兵」同一出口强制放行（StopWaveAnnounce + SpawnNextPendingWave），
        // 正常预告播完约 2~3 秒就会离开这个分支重新计时，不会误伤。
        if (UnitsCanAct && !_stageCleared && !_portalActive && Time.timeScale > 0.01f
            && CountAliveMonsters() == 0 && FindNextUnspawnedWaveIndex() >= 0
            && _waveAnnounceRunning)
        {
            _waveGateStuckSec += Time.unscaledDeltaTime;
            if (_waveGateStuckSec >= 5f)
            {
                _waveGateStuckSec = 0f;
                Debug.LogError("[BattleManager] 波次预告闸门卡死超过 5 秒（疑似切后台/协程中断后标志未归还），强制放行下一波");
                StopWaveAnnounce();
                StopWaveCountdown();
                SpawnNextPendingWave();
            }
        }
        else
        {
            _waveGateStuckSec = 0f;
        }

        bool allSpawned = true;
        if (_waves != null)
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                if (_waves[i] == null || !_waves[i].spawned) { allSpawned = false; break; }
            }
        }
        else allSpawned = false;
        _allWavesSpawned = allSpawned && _waves != null && _waves.Count > 0;

        // 所有波次已刷完且场上无活怪 → 宝箱结算（用存活数，勿用 list.Count）
        if (_allWavesSpawned && CountAliveMonsters() == 0 && _totalMonstersSpawnedThisStage > 0
            && !_portalActive && !_stageCleared && !_rewardSequenceStarted
            && !SuppressStageClear)
        {
            StartStageClearRewardSequence();
        }

        // 通关：门开了 → 玩家先走、佣兵隔一个身位跟着进（详见 TickPortalWalkIn）
        if (_portalActive && !_stageCleared && _portalGate != null)
            TickPortalWalkIn();

        // 跑图时镜头随英雄缓慢放宽
        if (hero != null)
            // 【2026-10-11 主人拍板「可见性选 B」】传送门一开，镜头右缘放宽到 hero+9（原 +6）。
            // 门的可视子物体 door01 挂在根节点右侧 2.26（场景里的偏移量），门根在 hero+3.2 →
            // 门实际落在 hero+5.46；+6 那档太贴边，玩家还没看清就踩到触发线了。
            ExtendCameraMaxX(UnitBase.GetCombatX(hero) + (_portalActive ? 9f : 12f));

        // 传送门开启后不再把英雄卡在镜头右缘，允许走向 chuansongmen
        if (!_portalActive)
            ClampHeroInCamera();

        if (hero != null && !hero.isDead)
        {
            if (BattleUI.Instance != null)
                BattleUI.Instance.UpdateSkillEnergy(0, PlayerSkillEnergyPeak);
        }

        // 2026-10-06：佣兵蓝条统一走 TickMercMp（自然回复 + 每关开局灌满 + 刷 UI），
        // 正式关 / 单人局 / 引导局三套分支合并成一个出口，不再有「自动模式就归零」的旧逻辑。
        TickMercMp(Time.deltaTime);
        TickPlayerMp(Time.deltaTime);
    }

    /// <summary>
    /// 每关开局：佣兵与玩家的蓝条清零并重新灌满一次
    /// （佣兵换人不回满 —— 只有新的一关才回满；玩家同理）。
    /// </summary>
    void ResetMpForNewStage()
    {
        for (int i = 0; i < mercMp.Length; i++)
        {
            mercMp[i] = 0f;
            _mercMpFilled[i] = false;
        }
        _playerMp = 0f;
        _playerMpFilled = false;
    }

    /// <summary>
    /// 佣兵蓝条每帧：本关首次见到有效佣兵 → 灌满；之后按 MpProfile 的自然回复回蓝；最后刷 UI。
    /// 抽卡 / 面板冻结时 Time.deltaTime = 0，天然不回蓝。
    /// 槽未解锁 / 无佣兵 / 无主动技 / 该技能无蓝 → 归零（蓝条由 UI 隐藏）。
    /// </summary>
    void TickMercMp(float dt)
    {
        bool solo = GameConfig.SOLO_PLAYER_BATTLE;
        bool tutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        int unlocked = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;

        for (int i = 0; i < mercMp.Length; i++)
        {
            var m = MercAt(i);
            bool live = m != null && !m.isDead;
            bool hasActive = live && m.SkillCaster != null && m.SkillCaster.HasActiveSkill;
            // 单人局只认引导佣兵那一条（槽 0，H003 塔克）；正式关按已解锁槽数
            bool slotUsable = solo ? (tutorialMerc && i == 0) : (i < unlocked);
            float pool = MercMpPool(i);

            if (!slotUsable || !live || !hasActive || pool <= 0f)
            {
                if (mercMp[i] > 0f)
                {
                    mercMp[i] = 0f;
                    BattleUI.Instance?.UpdateSkillEnergy(i + 1, 0f);
                }
                continue;
            }

            if (!_mercMpFilled[i])
            {
                // 每关开局回满。换佣兵（同槽位）时 _mercMpFilled 仍为 true → 沿用当前蓝量，不回满。
                mercMp[i] = pool;
                _mercMpFilled[i] = true;
            }
            else
            {
                mercMp[i] = Mathf.Min(pool, mercMp[i] + MercMpRegen(i) * dt);
            }
            BattleUI.Instance?.UpdateSkillEnergy(i + 1, GetMercMp(i));
        }
    }

    void ClampHeroInCamera()
    {
        if (hero == null) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float halfW = cam.orthographicSize * Mathf.Max(0.2f, cam.aspect);
        float camX = cam.transform.position.x;
        float margin = 0.35f;
        float minHeroX = camX - halfW + margin;
        float maxHeroX = camX + halfW - margin;
        float hx = hero.transform.position.x;
        if (hx < minHeroX || hx > maxHeroX)
        {
            Vector3 p = hero.transform.position;
            p.x = Mathf.Clamp(hx, minHeroX, maxHeroX);
            GameConfig.SetWorldPosition(hero.gameObject, p);
            if (hero.rb != null)
            {
                float vx = hero.rb.velocity.x;
                if (p.x <= minHeroX) vx = Mathf.Max(vx, 0f);
                if (p.x >= maxHeroX) vx = Mathf.Min(vx, 0f);
                hero.rb.velocity = new Vector2(vx, hero.rb.velocity.y);
            }
        }
    }
    /// <summary>引导关刷怪前：从 tutorial_battle 表读取近战/远程精灵编号与 HP 档。</summary>
    public void ApplyTutorialBattleStep(int order) => Planner.ApplyTutorialBattleStep(order);

    // ============================================================
    // 怪物死亡
    // ============================================================

    #endregion
}
