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
    /// <summary>跨大波三选一正在等玩家选：挡住 BeginNextWaveCountdown 每帧重入（2026-11-24）。</summary>
    bool _waveDraftPending;
    /// <summary>已为哪个 nextIdx 弹过 crossRound 三选一：防选完卡后 wave 还没刷、Emergency Update 又调 TryShowWaveClearDraft 重复弹（2026-11-24）。
    /// 每波 isRoundEnd 都该弹（路线 B 下每波都是回合末），故按 nextIdx 区分而非本关只弹一次；wave 刷出后 nextIdx 自然变化，无需手动重置。换关在 ResetWaveProgress 清回 -1。</summary>
    int _waveDraftDoneForNextIdx = -1;
    // 【2026-11-14 撤除】_waveGatePlayed「全关只播一次 Gate」的闸门已由「跨大波才播」取代，
    // 字段本身没有读取方了，按不留死字段的口径整段删掉（播报条件见波清链 crossRound）。
    // 【2026-10-17 主人报「切屏回来战斗卡住，人物站着不动」】波次闸门看门狗计时（真实时间）
    float _waveGateStuckSec;
    /// <summary>连杀计数</summary>
    private int _killCombo;
    private float _lastKillTime;
    /// <summary>
    /// 【2026-11-13 主人拍板】波流式衔接协程句柄：上一波出完后隔 WAVE_STREAM_GAP_SEC 秒
    /// 直接刷下一波（不等清场）。收口点：StopBattleSpawnCoroutines / ResetWaveProgress / StopWaveStream。
    /// </summary>
    Coroutine _waveStreamCo;
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
        // 【2026-11-14 主人报「怪物信息也带到了下一关」】进关唯一出口统一清零：
        // 词条怪图标栈 / 本场去重表 / 还开着的怪物信息弹窗（原地换关不切场景，不在这里清就会跟过去）。
        AffixMonsterNoticeUI.ResetForNewStage();
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
        // 🔴【2026-11-12 主人拍板「必须是单关的」】上一关 Boss 三选一的战意在开场撤销：
        // run 三件套回第一章存档基线、tempBuffs 只按引用摘掉本关那一条（石像赐福等整局 buff 不动）。
        // 必须放在下面读取 heroMaxHp 之前，否则进关回血还会按上一关加过属性的上限算。
        RevertBossStageBlessing();
        // 【战斗白夜天气 T5】本关天气 debuff 挂摘（BattleManager.WeatherBuff.cs，独立引用字段）：
        // 订阅 BattleAtmosphere.OnChanged，与下方 T2 的 ApplyForStage 同步；同时装配 T3/T6 表现控制器（懒创建，随场景销毁）。
        BindWeatherBuffToAtmosphere();
        ResetMpForNewStage();
        HeroThunderUltimate.Instance?.ResetForBattle();
        // 2026-11-13 职业自带技能：每关重新送 30% 起步槽，攒槽不跨关继承。
        // 2026-11-13：ApplyForBattle 不是每关都走，这里兜住组件与事件订阅（幂等）。
        PlayerPassiveCombat.EnsureCareerAttached(Hero.Instance);
        PlayerPassiveCombat.Instance?.ResetCareerChargeForStage();

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

        // 【2026-11-19 战斗昼夜天气 T2】按章节解析一次相位/天气，引导局强制白日晴（不受表影响）。
        {
            DayPhase phase = DayPhase.Day;
            WeatherType weather = WeatherType.Clear;
            if (Mode == null || Mode.Id != BattleModeId.Tutorial)
            {
                phase = ChapterThemeMapTable.GetAmbience(CurrentChapter);
                weather = ChapterThemeMapTable.GetWeather(CurrentChapter);
            }
            if (BattleAtmosphere.Instance != null)
                BattleAtmosphere.Instance.ApplyForStage(phase, weather);
        }

        // 按关卡类型切 BGM（Loading 中会记 pending，结束后再播）。
        // 【2026-10-31 主人拍板】竞技场对战与首领/精英同用 bgm_boss（新曲「boss战」）；普通关维持 bgm_battle。
        // 【2026-11-14 主人报「背景音乐还是重叠了」】胜负 jingle 原本只在 sceneLoaded 停，
        // 「点下一关」是原地换关不切场景 → 胜利曲还在响、新关 BGM 又起 = 两首叠在一起。
        // 进关是唯一出口，这里统一停一次（幂等，没在播时什么都不做），再切 BGM。
        GameSting.Stop();
        // 【2026-11-15 主人拍板「只在精英或者 boss 出现的波次里才换 boss 背景音乐」】
        // 进关先把波级选曲状态清零，必须放在下面所有切曲分支之前：
        // 不清会带着上一关的残留，让新关第一波小怪就响 boss 曲。
        GameBgm.SetBattleStrongWave(false);
        // 【2026-11-14 主人拍板「只有 BOSS 响 boss 曲」】裂隙精英层不再共用 bgm_boss：
        // 全局 TrackForStage(Elite) = Boss 的口径给主线/活动本留着，这里只给裂隙单独分流，
        // 于是第 2 层（现为普通层）与第 5/15 层（精英）都播 bgm_battle，只有 10/20/30 层播 boss 曲。
        if (IsEndlessRift)
        {
            // 【保留 11-14「只有 BOSS 层整层响 boss 曲」】裂隙曲目在进关一次性定死，不参与波级切曲；
            // 状态同步置位，保证静音/场景恢复（GuessTrackFromScene → TrackForStage）也按整层曲走，
            // 否则会把整层 boss 曲换成普通曲。裂隙 OffersRevive=false（11-02「死亡只能返回」），
            // 没有复活恢复这条路径，别按复活去理解这里。
            bool bossLayer = stage.type == StageType.Boss;
            GameBgm.SetBattleStrongWave(bossLayer);
            GameBgm.Play(bossLayer ? GameBgm.Track.Boss : GameBgm.Track.Battle);
        }
        else if (IsArena)
            GameBgm.Play(GameBgm.Track.Boss);
        else
            GameBgm.PlayForStage(stage.type);

        // 进关即写一次中断存档（后续由 BattleStateSaver 心跳续写退出时刻）
        BattleStateSaver.Instance?.SaveBattleState();

        if (Mode != null && Mode.Id == BattleModeId.Arena)
        {
            // 竞技场（2026-10-30 主人拍板「竞技场真战斗」）：不规划常规怪波——
            // 敌方 = 机器人佣兵镜像小队，开战过场结束后由 TrySpawnFirstWaveOnce 的 Arena 分支
            // 一次性生成（ArenaEnemySpawner.SpawnSquad），清场判定走 Update 的 Arena 分支。
            ArenaEnemySpawner.ResetForStage();
            Debug.Log($"[BattleManager] 竞技场建关：第{CurrentChapter}章地图（无常规怪波，敌=佣兵镜像小队）");
        }
        else switch (stage.type)
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
        // 【2026-10-31 主人拍板「没有任务通关金这个」】任务不再承诺金币：StageQuestClearGold 恒 0。
        // 连带效果（都是想要的）：TryGrantStageQuestGold 因 grant<=0 自然早退（:85 / 撤离 / 引导三条路都不发）、
        // BeginRewardSequence 的 bonusGold 回落到 GetClearGold 只给无限裂隙宝箱用、HUD 任务奖励区
        // 由 BattleUI.RefreshQuestReward 按 0 隐藏（不再显示「×N」）。
        // 任务文案（stageQuest.objective）照旧，只有奖励金没了。
        StageQuestClearGold = 0;
        // 下面三行（徽记/卷轴/礼物、支线岛、竞技场 清零）在上面恒 0 后已成冗余，
        // 按「下线≠删除」保留，将来要恢复任务通关金时它们仍是各自模式的豁免口径。
        // 徽记副本（2026-10-23 主人拍板「徽记副本不给金币」）：通关金清零，产出只有徽记
        // （BattleManager.Settlement → MercGrowInventory.GrantBadgeDungeonDrops）。
        // 不清会走普通 Elite 分支拿几十块通关金，HUD 也会显示 +N 金。
        // 卷轴副本（2026-10-24）：照徽记口径，通关金清零，产出只有招募卷轴。
        if (IsBadgeDungeon || IsScrollDungeon || IsGiftDungeon) StageQuestClearGold = 0;
        // 支线岛（2026-10-23 合并方案）：不发关卡任务金，HUD 不显示 +N 金（岛金=goldReward，走岛结算链）
        if (IsSideIsland) StageQuestClearGold = 0;
        // 竞技场挑战（2026-10-30 主人拍板「打赢不发金币，只获得积分和更高的排名」）：任务通关金一并清零
        if (IsArena) StageQuestClearGold = 0;
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
            _reviveUsedThisStage = false;   // 【2026-10-24】复活每关一次，关卡开始重置
            _revivedThisStage = false;      // 【2026-11-15】二次失败界面标记，同样随关卡开始重置
            EnsureMonsterPrefabReady();
            Analytics.StageStart(CurrentChapter, stage.stageIndex); // 埋点：关卡开始

            Debug.Log($"[BattleManager] LoadStage 战斗关 type={stage.type} waves={_waves?.Count ?? 0} heroX={UnitBase.GetCombatX(hero):F2}");

            if (Instance != this)
                Debug.LogError("[BattleManager] 当前实例不是单例实例！说明存在重复 BattleManager，Update/协程不会在本实例上运行");
            if (!gameObject.activeInHierarchy)
                Debug.LogError($"[BattleManager] 宿主 {gameObject.name} 未激活，协程与 Update 不会执行");

            StopBattleSpawnCoroutines();
            _battleStartCo = StartCoroutine(BattleStartSequenceCoroutine());
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
    Coroutine _battleStartCo;   // 开战序列协程句柄（原为字符串版 StartCoroutine/StopCoroutine，难维护且走反射找方法）
    /// <summary>
    /// 战前剧情（TryPlayPreBattle）或进关抽奖进行中：硬刷兜底须等它结束，防止剧透/选卡时刷怪抢跑。
    /// 2026-09-29 起进关抽奖也复用这个标志，见 <see cref="CoStageEntryDraft"/>。
    /// </summary>
    bool _preBattleStoryPlaying;

    // 【2026-10-29 实时抽改版】正式关/金币本进关阶段「开始战斗」待办的队伍出发参数：
    // BattleStartSequenceCoroutine 暂存（那时还不能出发，要等玩家点「开始战斗」），
    // OnLiveStartFight 消费；撤离/重载经 StopBattleSpawnCoroutines 清掉，不残留。
    bool _liveStartAdvancePending;
    CameraFollow _liveStartFollow;
    /// <summary>
    /// 【2026-11-14 修「1-5 点开始战斗无反应」】玩家**已经点过**「开始战斗」的持久标记。
    /// <para>根因：Boss 关的 <c>CoBossStageBlessingChoice</c>（三选一）阻塞在「置 _liveStartAdvancePending」
    /// 之前，玩家在那一段里点开战 → <see cref="DoStartFight"/> 当场消费不到 pending → 之后协程把 pending
    /// 置上却没人再点按钮 → 队伍永久停在开战位（Editor.log 实证：怪已刷 monsters=1，全程零「出发采样」）。</para>
    /// <para>本标记只做一件事：协程走到出发点（Waves.cs 置 pending 那处）时补消费一次。
    /// 正常流程（玩家等按钮亮着点）里它同样为 true，但那时 pending 已当场消费、这里无 pending 可消费，
    /// 行为与改动前一致，不会二次出发。</para>
    /// </summary>
    bool _liveStartRequested;

    /// <summary>停掉开战/硬刷协程，避免连续 LoadStage 叠多个兜底。</summary>
    public void StopBattleSpawnCoroutines()
    {
        if (_battleStartCo != null)
        {
            StopCoroutine(_battleStartCo);
            _battleStartCo = null;
        }
        StopWaveAnnounce();
        // 【2026-11-13】波流式衔接协程一并停掉（换关/撤离/重载不残留，防旧计时串台刷怪）
        StopWaveStream();
        // 战前剧情若在播放中被打断，标志位必须复位，否则 CoFirstWaveHardFallback 会永久卡在等待里
        _preBattleStoryPlaying = false;
        // 【2026-10-29 实时抽改版】待出发的「开始战斗」一并作废（撤离/重载不残留）
        _liveStartAdvancePending = false;
        // 【2026-11-14】玩家已点标记同源作废：换关/撤离/重载后不该沿用上一关的点击意图
        _liveStartRequested = false;
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
        // 修点①：首波乱入剧情协程一并停（换关/撤离不残留）；_preBattleStoryPlaying 上面已复位，
        // _intruderStoryShownThisStage 由 ResetWaveProgress 清（StopCoroutine 不跑 finally，此处必须手动置空句柄）
        if (_intruderStoryCo != null)
        {
            StopCoroutine(_intruderStoryCo);
            _intruderStoryCo = null;
        }
        // 批次4（T5/T6 撤场兜底）：救场小队随换关/撤离/收尾撤场——🔴 不带到结算/下一关。
        // 正常路径已在波末收口（TickRescueSquadWaveEnd）撤过，这里幂等兜住中断路径。
        DespawnRescueSquad();
    }
    /// <summary>无视走路门槛，立刻刷下一波；失败则紧急造怪（不依赖配置/图集）</summary>
    void ForceSpawnFirstWaveNow() => Planner.ForceSpawnFirstWaveNow();

    void EmergencySpawnVisibleMonsters(int count) => Planner.EmergencySpawnVisibleMonsters(count);


    void ResetWaveProgress()
    {
        _activeWaveIndex = -1;
        _allWavesSpawned = false;
        _waveDraftPending = false;
        _waveDraftDoneForNextIdx = -1;
        // 【2026-11-13】换关重置波次时把流式衔接计时一并清掉，防上一关的协程串台
        StopWaveStream();
        _killCombo = 0;
        _lastKillTime = 0f;
        SyncKillComboHaste();
        CombatJuice.ResetComboToast();
        _firstWaveSpawned = false;
        _intruderStoryShownThisStage = false;   // 修点①（T8 关卡级口径）：换关允许下一关的首波乱入剧情再播
        _rescueShownThisStage = false;          // 批次4 T8（关卡级）：换关允许下一关再触发友军救场
        _rescueWaveEndHandled = false;          // 批次4 T8（关卡级）：波末成败/撤场闸随换关复位
        _petRescueStageActive = false;          // 【2026-11-23】换关允许关 3 未通关时再演狗剧情
        ResetBattleCutscene();                  // 【2026-11-23 统一编排器】停演出协程 + 清占用标志，防串台
        _battleIntroFinished = false;
        _battleStartTime = Time.unscaledTime;
        _reviveUsedThisStage = false;   // 【2026-10-24】复活每关一次，关卡开始重置
        _revivedThisStage = false;      // 【2026-11-15】二次失败界面标记，同样随关卡开始重置
        // 【2026-10-19 主人拍板】「击败」累计计数跨关卡不重置（原 ResetCombo 已删），新一局才在 StartNewRun 归零
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
    }
    /// <summary>波次为空时强制补一波（兜底）</summary>
    void EnsureAtLeastOneWave() => Planner.EnsureAtLeastOneWave();


    void ScheduleFirstWaveSpawn()
    {
        if (_firstWaveSpawned) return;
        // ===== 修点①（2026-11-20 二次拍板④）首波链剧情钩子 =====
        // 首波（triggerNode=wave1 的 EV03）走本链同步刷出，不经 CoAnnounceThenSpawnNextWave
        //（那只管第 2 波起，:1134）——在此插同类三件套剧情，播完才刷首波。
        // 命中时协程接管本次调度（含失败重试），这里直接让路；_intruderStoryCo 进行中也幂等让路。
        if (_intruderStoryCo != null) return;
        if (TryStartIntruderStoryBeforeFirstWave()) return;
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

    // ===== 修点①（2026-11-20 二次拍板④）：首波敌对乱入剧情（EV03 triggerNode=wave1）=====
    // 首波由 ScheduleFirstWaveSpawn → TrySpawnFirstWaveOnce 同步刷出，不经 CoAnnounceThenSpawnNextWave；
    // 在首波刷怪点前同步阻塞播剧情，三件套（try/catch + StopPlaying + 300s unscaled 看门狗）照
    // CoAnnounceThenSpawnNextWave 的 T3 分支（:1180-1206）同款。
    /// <summary>本关已播过首波乱入剧情（开播即记，含跳过/异常）；换关在 ResetWaveProgress 清（T8 关卡级口径）。</summary>
    bool _intruderStoryShownThisStage;
    /// <summary>首波乱入剧情协程句柄：进行中 ScheduleFirstWaveSpawn 幂等让路；换关/撤离随 StopBattleSpawnCoroutines 停。</summary>
    Coroutine _intruderStoryCo;

    /// <summary>
    /// 【2026-11-26 停用】首波前强盗剧情钩子。
    /// <para>原因：强盗已从「每章固定第6关」改为「每章关3/关7 替换末波」，剧情统一由
    /// <see cref="CoAnnounceThenSpawnNextWave"/> 的末波前分支播（关7播、关3让给狗营救剧情）。
    /// 原实现只在调味关让路 → 第1章等<b>普通关</b>反而会凭空弹「提着大剑的家伙堵路」旁白
    /// （画面里根本没有强盗，主人截图报障「怎么第一关就有了」）。</para>
    /// <para>方法保留便于回退，恒返回 false；恢复时从 git 历史取原实现，别只把 return 删掉。</para>
    /// </summary>
    bool TryStartIntruderStoryBeforeFirstWave()
    {
        return false;
    }

    /// <summary>播完（或跳过/异常/超时）后才刷首波；刷怪失败沿用 FirstWaveSpawnRetry 原节奏，不改时序。</summary>
    IEnumerator CoPlayIntruderStoryBeforeFirstWave(string intruderEventId)
    {
        _intruderStoryShownThisStage = true;   // 开播即记（与战前剧情 MarkPreStorySeen 同口径）：跳过/异常也不重播
        // 压住 CoFirstWaveHardFallback：其闸门在等 _preBattleStoryPlaying（CoFirstWaveHardFallback 开头 while），
        // 不置 true 的话点「开始战斗」后 2.2s 硬刷会在剧情播到一半时抢跑首波。播完按原值归还。
        bool preBattleStoryWasPlaying = _preBattleStoryPlaying;
        _preBattleStoryPlaying = true;
        try
        {
            bool done = false;
            BattleEntryDraftPanel.Hide();   // 幂等：防抽奖按钮盖住对话窗（CoAnnounce 分支同款）
            bool playing = false;
            try
            {
                // storyKey 传节点名（与 Plan 阶段查表节点一致）；本批剧情只有一种，按章节分派台词
                playing = IntruderStoryBeats.TryPlayIntruder(CurrentChapter, "preBattle:" + CurrentChapter, () => done = true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleManager] 首波敌对乱入剧情播放异常，放行首波: " + e.Message);
                // 剧情可能已开播（timeScale 被压 0 / 对话窗停在半句）：强制收掉再放行，绝不卡 timeScale=0
                StoryDirector.Ensure().StopPlaying();
                var dlg = DialogueUI.Instance;
                if (dlg != null) dlg.Hide();
                playing = false;
            }
            if (playing)
            {
                // 批次5 修2：开播即 Mark（防同关重进重播；多周目由 Plan 不打标 + 首波链自查存档兜底）
                SaveSystem.Instance?.Data?.MarkIntruderStorySeen(intruderEventId);
                float watchdog = 0f;
                while (!done && watchdog < 300f)
                {
                    watchdog += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!done) Debug.LogWarning("[BattleManager] 首波敌对乱入剧情 5 分钟未完成，强制放行首波");
            }
        }
        finally
        {
            _preBattleStoryPlaying = preBattleStoryWasPlaying;
            _intruderStoryCo = null;
            TrySpawnFirstWaveOnce();
            if (!_firstWaveSpawned && _firstWaveSpawnCo == null)
                _firstWaveSpawnCo = StartCoroutine(FirstWaveSpawnRetry());
        }
    }

    // ============================================================
    // 批次4（2026-11-20 T4/T5/T6/T9）：友军救场 + 入场救援
    // ============================================================
    // T4 触发链：UnitBase.Damage.cs 实际扣血后 → TryTriggerRescueOnWounded（门禁+Boss波+阈值+一次旗）
    //   → CoRescueArrival：先 SpawnRescueSquad(满血 1f) 落人，再 CoRescueBattleScene 头顶气泡演（2026-11-23 起不再走全屏立绘）。
    // T6 入场救援：BattleStartSequenceCoroutine 的 FinishBattleIntro 之后 → CoEntryRescueBeforeFirstWave
    //   → 先 SpawnRescueSquad(0.4 低血起步) 落人 → CoEntryRescueBattleScene 气泡 + 刀光 → 首波照常刷。
    // 波末：Update 通关检测点 TickRescueSquadWaveEnd —— T5 直接撤场；T6 先谢幕剧情再撤场（不拦结算）。
    // 撤场兜底：StopBattleSpawnCoroutines（换关/撤离/收尾）→ DespawnRescueSquad，不带到结算/下一关。
    // 🔴 不调 UnlockMerc（临时入队不算已拥有，照 RunDraftDirector.cs:583 引导分支口径）。
    // 🔴 友方机器人死亡零奖励口径在 MonsterDeath 已处理，本批不碰。

    /// <summary>T4 濒死阈值（主人 2026-11-18 拍板 40%；实测过频再单独拍板调，不私自改）。</summary>
    internal const float RescueHpRatio = 0.4f;
    /// <summary>固定救场小队（2026-11-20 拍板①；2026-11-23 主人拍板治疗位换牧师）：加百列 H023 剑盾 / 艾拉 H015 游侠 / 莉娜 H017 牧师，squadId="SQ_MAIN"。</summary>
    static readonly string[] RescueSquadHireIds = { "H023", "H015", "H017" };
    /// <summary>
    /// 救场小队生成点的玩家前方基线（2026-11-23 由 2.2 收窄到 1.0）：救场/入场救援都改战场演出，
    /// 三个说话人必须同框。窄屏（720×1280）画面右边界 ≈ 开战位 +3.89，旧基线 2.2 + 1.1×2
    /// 会把第三人推到屏外（气泡被钳在屏幕边缘、脱离本人）→ 现为 1.0 / 2.1 / 3.2。
    /// </summary>
    const float RescueSpawnAheadBase = 1.0f;
    const float RescueSpawnAheadStep = 1.1f;   // 队内成员错位间距

    /// <summary>
    /// 救场小队屏外生成点（2026-11-23 主人「佣兵那些剧情是不是也有这些问题」）：
    /// 画面左缘再往外这么多 —— 生成那一刻玩家看不到 pop 出现，随后由 <see cref="Mercenary.BeginMapEnter"/>
    /// 走到编队位，观感是「援军从左侧走进来」。与狗剧情 <c>DOG_RUN_IN_SPAWN_MARGIN</c> 同口径。
    /// </summary>
    const float RescueWalkInMargin = 0.9f;

    /// <summary>T4 本关已触发旗：命中即置 true 不再查；换关在 ResetWaveProgress 清（T8 关卡级口径）。</summary>
    bool _rescueShownThisStage;
    /// <summary>当前在场的是不是 T6 入场救援（true=波末判成败后撤场；false=T5 救场，波末直接撤场）。</summary>
    bool _rescueEntryMode;
    /// <summary>波末成败/撤场只走一次的闸（Update 通关检测点可能多帧命中）。</summary>
    bool _rescueWaveEndHandled;
    /// <summary>在场救场小队（T5/T6 共用生成撤场链）。</summary>
    readonly List<Mercenary> _rescueSquad = new List<Mercenary>();
    /// <summary>救场/谢幕剧情协程句柄；换关/撤离随 StopBattleSpawnCoroutines 停。</summary>
    Coroutine _rescueStoryCo;
    /// <summary>T5 救场对话是否正冻着场（只解自己冻的场，防误解冻死亡/结算的冻场）。</summary>
    bool _rescueFrozeUnits;

    /// <summary>Boss 波进行中（读现有 WaveData.isBossWave 标记，不新造判据）。</summary>
    bool IsBossWaveActive => _activeWaveIndex >= 0 && _waves != null && _activeWaveIndex < _waves.Count
        && _waves[_activeWaveIndex] != null && _waves[_activeWaveIndex].isBossWave;

    /// <summary>
    /// T4 触发判定（唯一调用点：UnitBase.Damage.cs RecordDamageTaken 之后，玩家/友军每次受伤查一次，不每帧轮询）。
    /// 全部满足才触发：Boss 波进行中 + 该单位血量 ≤40% 且未死 + 本关未触发过 + 门禁全过 + 表里有 Rescue 事件。
    /// </summary>
    public void TryTriggerRescueOnWounded(UnitBase unit)
    {
        if (_rescueShownThisStage) return;              // 本关一次旗：命中过就不再查（最廉价的先查）
        if (!isInBattle || _stageCleared || _runClosed) return;
        // 门禁（复用 Settlement.cs:267-380 现成出口）：主线正式关 / 支线岛才可能触发；
        // 显式排除 引导 / 羁绊 / 竞技场 / 活动副本 / 裂隙。
        if (!(IsFormalMainRun || IsSideIsland)) return;
        if (IsTutorialRun || IsBondDungeon || IsArena || IsActivityDungeon || IsEndlessRift) return;
        if (!NormalOnlyContentAllowed(BattleDifficulty)) return;   // 剧情只普通难度：困难/噩梦不播（与主线剧情统一口径）
        if (!IsBossWaveActive) return;                  // 只在 Boss 波进行中触发
        if (unit == null || unit.attr == null) return;
        if (unit.currentHp <= 0f) return;               // 已到致死一击的不算（死亡走各自流程，不和复活链叠加）
        float maxHp = Mathf.Max(1f, unit.attr.GetAttr(AttrType.MaxHp));
        if (unit.currentHp / maxHp > RescueHpRatio) return;
        // 事件匹配：主线按章（EV01=Rescue/preBattle:1 → chapter=1）；支线岛按岛（EV04=Rescue/island:2 → islandId=2）
        IntruderEventTable.Row row;
        bool has = IsSideIsland
            ? IntruderEventDirector.TryGetRescueForIsland(SideIslandId.ToString(), out row)
            : IntruderEventDirector.TryGetRescueForChapter(CurrentChapter, out row);
        if (!has) return;
        // 批次5 修1：存档级一次性（§一.8「全篇 3-5 次」拍板）——该救场剧情已看过 → 直接放行：
        // 不触发、不置旗、不播。eventId 天然隔离主线 EV01 / 支线岛 EV04；data 为空视为未看（老档兼容）。
        var rescueData = SaveSystem.Instance?.Data;
        if (rescueData != null && rescueData.HasIntruderStorySeen(row.eventId)) return;
        _rescueShownThisStage = true;   // 命中即置"本关已触发"，不再查
        rescueData?.MarkIntruderStorySeen(row.eventId);   // 开播即 Mark（照 Entry :775 口径：跳过也算看过）
        _rescueStoryCo = StartCoroutine(CoRescueArrival());
    }

    /// <summary>
    /// T5 救场（2026-11-23 改战场内演出）：三名满血友军直接入场开打，台词走战斗头顶气泡。
    /// 不再开全屏立绘窗、不再靠 StoryDirector 的 timeScale=0 定格 —— 这段发生在玩家 40% 血、
    /// Boss 波进行中，弹立绘等于「战斗中弹剧情 + 回来又变像素小人」（主人 11-23 拍板）。
    /// 先落人再演也让救场当场生效：莉娜的治疗与加百列的盾立刻接上，比「演完才落地」更救命。
    /// </summary>
    IEnumerator CoRescueArrival()
    {
        SpawnRescueSquad(1f, entryMode: false);   // 救场友军满血入场
        if (_rescueSquad.Count == 0)
        {
            // 一个都没生成：不静默兜底造人，报错后照常打（流程不能卡）
            Debug.LogError("[BattleManager] 救场小队一个都没生成，救场演出跳过（战斗照常）");
            _rescueStoryCo = null;
            yield break;
        }
        yield return CoRescueBattleScene();
        _rescueStoryCo = null;
    }

    /// <summary>
    /// 救场·战场演出（2026-11-23）：三人轮流喊话 + 玩家回一句，气泡贴本人头上（跟着单位走）。
    /// 【2026-11-23 主人「冻场」】对话期间冻住全场（UnitsCanAct=false），与引导 TalkBlock / 狗营救对话同源；
    /// 三人先不冻场地走到编队位（冻着会僵在屏外），全员到位后才冻场开口，说完 finally 解冻继续打。
    /// 台词取 IntruderStoryBeats.RescueBattleLines（支线岛版/揭底版/首遇版），取不到人跳过该句。
    /// </summary>
    IEnumerator CoRescueBattleScene()
    {
        var talk = BattleHeadTalkUI.Ensure();
        if (talk == null)
        {
            Debug.LogError("[BattleManager] BattleHeadTalkUI 取不到，救场演出跳过（友军已入场，战斗照常）");
            yield break;
        }
        var lines = IntruderStoryBeats.RescueBattleLines(
            IsSideIsland ? 0 : CurrentChapter,
            IsSideIsland ? SideIslandId.ToString() : null);

        // 走入期不冻场：冻着走不动会僵在屏外（T5 此刻 UnitsCanAct 为 true，走入照常）
        for (int i = 0; i < lines.Count; i++)
        {
            if (_runClosed || _stageCleared) yield break;
            var walker = ResolveSquadSpeaker(lines[i].Speaker);
            if (walker == null) continue;   // 该位已阵亡/未生成：跳过这句，不兜底换人
            yield return CoWaitRescueSpeakerInPlace(walker);   // 走到位再开口，气泡不落在屏外
        }

        // 全员到位后整段冻场播台词：进来前谁能动才由谁冻，finally 只解自己冻的那次
        try
        {
            if (UnitsCanAct) { UnitsCanAct = false; _rescueFrozeUnits = true; }
            for (int i = 0; i < lines.Count; i++)
            {
                if (_runClosed || _stageCleared) break;   // 玩家死了/已通关：立刻收掉气泡，别压在结算上
                var unit = ResolveSquadSpeaker(lines[i].Speaker);
                if (unit == null) continue;   // 该位已阵亡/未生成：跳过这句，不兜底换人
                yield return talk.CoPlayLine(unit, lines[i].Text, lines[i].Hold);
            }
        }
        finally
        {
            // 死亡冻场/结算已接管时不动 UnitsCanAct，只解自己冻的那次
            if (_rescueFrozeUnits && !_runClosed && !_stageCleared) UnitsCanAct = true;
            _rescueFrozeUnits = false;
        }
    }

    /// <summary>生成救场小队（T5 满血 1f / T6 低血 startHpRatio=0.4）。固定三人同一支队伍，不随机。</summary>
    void SpawnRescueSquad(float hpRatio, bool entryMode)
    {
        var mm = MercenaryManager.Instance;
        if (mm == null) { Debug.LogError("[BattleManager] 救场小队生成失败：MercenaryManager.Instance 为空"); return; }
        if (hero == null) { Debug.LogError("[BattleManager] 救场小队生成失败：hero 为空"); return; }
        _rescueEntryMode = entryMode;
        float heroX = UnitBase.GetCombatX(hero);
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;
        // 【2026-11-23 主人「佣兵那些剧情是不是也有这些问题」】旧写法把三人直接 Instantiate 在队伍前方
        //（heroX+1.0/2.1/3.2）—— 冻场中凭空出现、连走路都没有，和狗剧情「突然出现」是同一个病根。
        // 改走现存唯一屏外走入机制（Mercenary.BeginMapEnter，与抽卡入场同款）：
        // 屏外左缘生成 → 走到编队位 → 到点自动交还普通 AI。朝向由 StepMapEnter 每帧按位移方向翻，不会倒着跑。
        GetBattleVisibleX(out float visMin, out float visMax, 0.35f);
        float enterX0 = visMin - RescueWalkInMargin;
        int spawned = 0;
        for (int i = 0; i < RescueSquadHireIds.Length; i++)
        {
            float targetX = heroX + RescueSpawnAheadBase + i * RescueSpawnAheadStep;
            // 起点与终点同间距错开 → 三人保持队形一起走进来，不会叠在同一处出生
            float enterX = enterX0 - i * RescueSpawnAheadStep;
            var merc = SpawnRescueSquadMember(mm, RescueSquadHireIds[i],
                new Vector3(enterX, UnitBase.GROUND_Y, z), targetX,
                hpRatio);
            if (merc != null)
            {
                // T6 入场救援挂在 FinishBattleIntro 之后（战前 UnitsCanAct=false），不传 walkWhileFrozen
                // 就会被冻在屏外看不见 —— 这正是「突然出现/看不到人」的成因；
                // T5 走入期不冻场（三人走到位后才冻场念台词），同一口径照走。
                merc.BeginMapEnter(targetX, 1, walkWhileFrozen: true);
                _rescueSquad.Add(merc);
                spawned++;
            }
        }
        Debug.Log($"[BattleManager] 救场小队屏外入场 hpRatio={hpRatio:0.##} entryMode={entryMode} spawned={spawned}/3 enterX={enterX0:0.##}");
    }

    /// <summary>
    /// 单个救场友军生成链（照 SpawnTutorialMercAt 的步骤，身份改用花名册真值）：
    /// SpawnMercenary(AssetId) → 挂技能/真名/真 hireId → 进 allyUnits → 挂死亡 → 计贡献 → 按比例起步血量。
    /// 🔴 不调 UnlockMerc —— 临时入队不算已拥有（RunDraftDirector.cs:583 引导分支同口径）。
    /// </summary>
    Mercenary SpawnRescueSquadMember(MercenaryManager mm, string hireId, Vector3 pos, float cameraExtendX, float hpRatio)
    {
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def) || string.IsNullOrEmpty(def.AssetId))
        {
            Debug.LogError($"[BattleManager] 救场友军花名册查不到 hireId={hireId}（fail-closed 不生成，不兜底造人）");
            return null;
        }
        // SpawnMercenary 只认 AssetId（mercId 口径）；hireId 由下方 SetHireId 补挂并按需重建基底（共用 asset 的行，如 H015/H005 共用 gongshou101）
        var merc = mm.SpawnMercenary(def.AssetId, pos, 1);
        if (merc == null)
        {
            Debug.LogError($"[BattleManager] 救场友军生成失败 hireId={hireId} assetId={def.AssetId}");
            return null;
        }
        merc.SetupBattleSkills(def.ActiveSkillId, def.PassiveSkillId);
        merc.SetDisplayName(def.Name);
        merc.SetHireId(hireId);
        if (!allyUnits.Contains(merc))
            allyUnits.Add(merc);
        merc.OnDead += OnMercenaryDead;
        RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
        float maxHp = merc.attr != null ? merc.attr.GetAttr(AttrType.MaxHp) : 100f;
        merc.currentHp = Mathf.Max(1f, maxHp * Mathf.Clamp01(hpRatio));
        // 注意 pos 现在是屏外走入起点，镜头扩展必须按【编队落点】算（cameraExtendX），
        // 按起点算会把 maxX 顶错位置（旧实现按 pos.x+6f，pos 原来就是落点，同口径）。
        ExtendCameraMaxX(cameraExtendX + 6f);
        return merc;
    }

    /// <summary>
    /// 波末收口（T5 战后台词 + 撤场 / T6 成败判定 + 撤场）。唯一调用点：Update 的通关检测块
    /// （所有波刷完 + 场上无活怪），即 Boss 波结束、StartStageClearRewardSequence 之前 ——
    /// 后续剧情播在撤场前，结算不被拦截。
    /// </summary>
    void TickRescueSquadWaveEnd()
    {
        if (_rescueWaveEndHandled || _rescueSquad.Count == 0) return;
        _rescueWaveEndHandled = true;
        if (!_rescueEntryMode)
        {
            _rescueStoryCo = StartCoroutine(CoRescueAfterBattleThenDespawn());   // T5：战后补几句再撤场
            return;
        }
        _rescueStoryCo = StartCoroutine(CoRescueOutcomeThenDespawn());   // T6：先成败剧情再撤场
    }

    /// <summary>
    /// T5 战后台词（2026-11-23 主人「其他的战斗完再继续说几句就行」）：战中只播 2 句最短救场说明，
    /// 「谁托的/为什么来」挪到这里——波末我方已赢、播在撤场前，与 T6 谢幕同一条 PlayRescueBeats 出口。
    /// </summary>
    IEnumerator CoRescueAfterBattleThenDespawn()
    {
        Debug.Log("[BattleManager] 救场波末：我方已赢 → 播救场战后台词");
        bool done = false;
        BattleEntryDraftPanel.Hide();   // 与 T6 同款：先收掉结算抽奖面板，别压住剧情
        bool playing = false;
        try
        {
            playing = IntruderStoryBeats.TryPlayRescueAfterBattle(
                IsSideIsland ? 0 : CurrentChapter,
                IsSideIsland ? SideIslandId.ToString() : null,
                () => done = true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BattleManager] 救场战后台词播放异常，直接撤场放行结算: " + e.Message);
            StoryDirector.Ensure().StopPlaying();
            var dlg = DialogueUI.Instance;
            if (dlg != null) dlg.Hide();
            playing = false;
        }
        if (playing)
        {
            float watchdog = 0f;
            while (!done && watchdog < 300f)
            {
                watchdog += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!done) Debug.LogWarning("[BattleManager] 救场战后台词 5 分钟未完成，强制撤场放行结算");
        }
        DespawnRescueSquad();
        _rescueStoryCo = null;
    }

    /// <summary>T6 谢幕（2026-11-20 三次拍板）：播点在 Update 通关检测点（必然我方已赢）→ 统一「救援成功·受伤版」剧情（小队受伤没死，无死亡暗示）→ 撤场。不拦结算。</summary>
    IEnumerator CoRescueOutcomeThenDespawn()
    {
        Debug.Log("[BattleManager] 入场救援波末：我方已赢 → 播救援成功谢幕（受伤口径）");
        bool done = false;
        BattleEntryDraftPanel.Hide();   // T9 同款
        bool playing = false;
        try
        {
            playing = IntruderStoryBeats.TryPlayRescueOutcome(() => done = true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BattleManager] 入场救援谢幕剧情播放异常，直接撤场放行结算: " + e.Message);
            StoryDirector.Ensure().StopPlaying();
            var dlg = DialogueUI.Instance;
            if (dlg != null) dlg.Hide();
            playing = false;
        }
        if (playing)
        {
            float watchdog = 0f;
            while (!done && watchdog < 300f)
            {
                watchdog += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!done) Debug.LogWarning("[BattleManager] 入场救援谢幕剧情 5 分钟未完成，强制撤场放行结算");
        }
        DespawnRescueSquad();
        _rescueStoryCo = null;
    }

    /// <summary>
    /// 撤场：逐个 DespawnMercenary（MercenaryManager.cs:539，同时清 _activeMercs 与 allyUnits 并销毁实体）。
    /// 阵亡成员的 GameObject 已随死亡动画 Destroy（Mercenary.OnDeathRelease），假 null 直接跳过。
    /// 调用点：波末收口 / StopBattleSpawnCoroutines（换关/撤离/收尾）—— 撤场后场上无残留、不带到结算/下一关。
    /// </summary>
    void DespawnRescueSquad()
    {
        if (_rescueStoryCo != null)
        {
            StopCoroutine(_rescueStoryCo);
            _rescueStoryCo = null;
        }
        // StopCoroutine 不触发协程 finally：若正冻在 T5 对话中撤场，这里补解冻（只解自己冻的场）
        if (_rescueFrozeUnits)
        {
            if (!_runClosed && !_stageCleared) UnitsCanAct = true;
            _rescueFrozeUnits = false;
        }
        if (_rescueSquad.Count == 0) return;
        var mm = MercenaryManager.Instance;
        for (int i = 0; i < _rescueSquad.Count; i++)
        {
            var m = _rescueSquad[i];
            if (m == null) continue;   // 阵亡已销毁（Unity 假 null）
            if (mm != null) mm.DespawnMercenary(m);
            else Destroy(m.gameObject);
        }
        _rescueSquad.Clear();
        Debug.Log("[BattleManager] 救场小队已撤场（无残留）");
    }

    /// <summary>
    /// T6 入场救援（2026-11-23 改战场内演出）：进 Boss 关先把 3 名低血友军（startHpRatio=0.4，表值优先）
    /// 落在队伍前方 → 在场上用战斗小人 + 头顶气泡演「被咬住、搭把手」 → 首波照常刷。
    /// 挂点 = BattleStartSequenceCoroutine 的 FinishBattleIntro 之后（队伍与镜头都已就位）。
    /// 一次性：存档级 intruderStorySeenKeys（key=eventId），多周目不重播（已看则整段不触发，友军也不生成）。
    /// </summary>
    IEnumerator CoEntryRescueBeforeFirstWave()
    {
        // 门禁：主线正式关专属；引导/羁绊/竞技场/活动副本/裂隙零触发；难度局不写存档级标记（NormalOnlyContentAllowed 口径）
        if (!IsFormalMainRun || IsTutorialRun || IsBondDungeon || IsArena || IsActivityDungeon || IsEndlessRift) yield break;
        if (!NormalOnlyContentAllowed(BattleDifficulty)) yield break;
        // 触发关：该章 Boss 关（关 10 / stageIdx0=9 大 Boss，走 ActProgressionDefs 单一出口，不自写判据）
        if (currentStage == null || currentStage.type != StageType.Boss) yield break;
        int stageIdx0 = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : -1;
        if (ActProgressionDefs.BossKindOfStage(stageIdx0) != 2) yield break;
        // 查表：Entry 一律挂 preBattle:章（2026-11-20 拍板⑥）
        IntruderEventTable.Row row;
        bool has = false;
        try { has = IntruderEventDirector.TryGetEntryForNode("preBattle:" + CurrentChapter, out row); }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BattleManager] 入场救援事件查询异常，跳过: " + e.Message);
            yield break;
        }
        if (!has) yield break;
        var data = SaveSystem.Instance?.Data;
        if (data != null && data.HasIntruderStorySeen(row.eventId)) yield break;   // 多周目不重播

        // 开演即记存档（跳过也算看过，防同段重播；与 MarkPreStorySeen 同口径）
        data?.MarkIntruderStorySeen(row.eventId);

        // 先落人再演：旧流程是「全屏窗演完才生成友军」，画面里没有人、回来才是像素小人（割裂根因）。
        // 现在三人直接站在队伍前方（低血起步），气泡就贴在本人头上。
        float hp = row.startHpRatio > 0f ? row.startHpRatio : RescueHpRatio;   // 表值优先，缺省 0.4（拍板③）
        SpawnRescueSquad(hp, entryMode: true);
        if (_rescueSquad.Count == 0)
        {
            // 一个都没生成：不静默兜底造人，报错后照常放行首波（流程不能卡）
            Debug.LogError("[BattleManager] 入场救援三名友军一个都没生成，战场演出跳过（首波照常）");
            yield break;
        }

        // 演出期间压住硬刷兜底（照 PetRescue 同款）；玩家若已点「开始战斗」则演出内部提前收场
        bool prevGate = _preBattleStoryPlaying;
        _preBattleStoryPlaying = true;
        yield return CoEntryRescueBattleScene();
        _preBattleStoryPlaying = prevGate;
    }

    /// <summary>
    /// 【2026-11-26 主人拍板】友军助阵改为 Boss 波刷出前就位（原 T5 等 Boss 波打到 40% 血才中途来，
    /// 主人嫌乱）。挂点 = CoAnnounceThenSpawnNextWave 的 Boss 播报前，先落人再说话，播报后 Boss 立刻刷。
    /// 门禁 / 查表 / 存档一次性全部复用 T5 现成出口；命中即置 _rescueShownThisStage，
    /// 老受伤触发链 TryTriggerRescueOnWounded 首行早退，绝不在同一关再来一次。
    /// 不动表：triggerNode=hp40 列保留不删（口径变更只改时机，不改事件身份）。
    /// </summary>
    IEnumerator CoPreBossRescueArrival()
    {
        if (_rescueShownThisStage) yield break;
        if (!isInBattle || _stageCleared || _runClosed) yield break;
        if (!(IsFormalMainRun || IsSideIsland)) yield break;
        if (IsTutorialRun || IsBondDungeon || IsArena || IsActivityDungeon || IsEndlessRift) yield break;
        if (!NormalOnlyContentAllowed(BattleDifficulty)) yield break;

        IntruderEventTable.Row row;
        bool has = IsSideIsland
            ? IntruderEventDirector.TryGetRescueForIsland(SideIslandId.ToString(), out row)
            : IntruderEventDirector.TryGetRescueForChapter(CurrentChapter, out row);
        if (!has) yield break;
        var data = SaveSystem.Instance?.Data;
        if (data != null && data.HasIntruderStorySeen(row.eventId)) yield break;

        data?.MarkIntruderStorySeen(row.eventId);
        _rescueShownThisStage = true;
        SpawnRescueSquad(1f, entryMode: false);   // 满血入场，与旧救场同口径
        if (_rescueSquad.Count == 0)
        {
            Debug.LogError("[BattleManager] Boss 前助阵三人一个都没生成，跳过演出（Boss 波照常）");
            yield break;
        }
        // 玩家已点「开始战斗」就只落人不演，别挡开打（与 T6 入场救援同口径）
        if (!_liveStartRequested)
            yield return CoRescueBattleScene();
    }

    /// <summary>
    /// 入场救援·战场演出（2026-11-23）：三名低血友军在队伍前方被咬住 → 头顶气泡轮流喊话 → 玩家回一句
    /// → 收尾三处刀光表示冲进围圈。纯视觉/零统计污染：不生成假怪、不结算伤害、不计数、
    /// 不动波数怪数（铁律 40/41）——台上这三人本来就是这一关要参战的真实友军。
    /// 全程 unscaled 时间，与 PetRescue / StoryDirector 同口径。
    /// </summary>
    IEnumerator CoEntryRescueBattleScene()
    {
        var talk = BattleHeadTalkUI.Ensure();
        if (talk == null)
        {
            Debug.LogError("[BattleManager] BattleHeadTalkUI 取不到，入场救援战场演出跳过（友军已入场，首波照常）");
            yield break;
        }
        var lines = IntruderStoryBeats.EntryBattleLines(CurrentChapter);
        for (int i = 0; i < lines.Count; i++)
        {
            // 玩家已点「开始战斗」：立刻收掉演出让路（剧情只是插播，不挡开打）
            if (_liveStartRequested) break;
            var unit = ResolveSquadSpeaker(lines[i].Speaker);
            if (unit == null) continue;   // 该位没生成/已阵亡：跳过这句，不兜底换人
            yield return CoWaitRescueSpeakerInPlace(unit, abortOnLiveStart: true);   // 走到位再开口，气泡不落在屏外
            yield return talk.CoPlayLine(unit, lines[i].Text, lines[i].Hold);
        }

        // 收尾刀光：玩家冲进围圈解围（纯特效，不结算伤害）
        var vfx = BattleVFXSystem.Instance;
        if (vfx != null)
        {
            float z = unitRoot != null ? unitRoot.position.z : 0f;
            var first = _rescueSquad[0];
            float cx = first != null ? UnitBase.GetCombatX(first)
                : (hero != null ? UnitBase.GetCombatX(hero) + 2f : 0f);
            vfx.PlaySlash(new Vector3(cx - 0.5f, UnitBase.GROUND_Y + 0.4f, z), 1, VfxFaction.Ally);
            vfx.PlaySlash(new Vector3(cx + 0.5f, UnitBase.GROUND_Y + 0.4f, z), -1, VfxFaction.Ally);
            yield return new WaitForSecondsRealtime(0.35f);
        }
    }

    /// <summary>
    /// 说话人槽位 → 场上单位（0/1/2 = 救场小队，3 = 玩家）；救场与入场救援两段演出共用。取不到返回 null。
    /// 认人用花名册 hireId（不认 _rescueSquad 下标）：三人里万一某个生成失败，下标会整体前移，
    /// 按顺序取会把加百列的台词安到艾拉头上。
    /// </summary>
    UnitBase ResolveSquadSpeaker(IntruderStoryBeats.EntrySpeaker who)
    {
        if (who == IntruderStoryBeats.EntrySpeaker.Player) return hero;
        int idx = (int)who;
        if (_rescueSquad == null || idx < 0 || idx >= RescueSquadHireIds.Length) return null;
        string hire = RescueSquadHireIds[idx];
        for (int i = 0; i < _rescueSquad.Count; i++)
        {
            var m = _rescueSquad[i];
            if (m != null && m.hireId == hire) return m;
        }
        return null;   // 该位没生成/已阵亡：调用方跳过这句，不兜底换人
    }

    /// <summary>
    /// 等说话人从屏外走到编队位再开口（2026-11-23）：走入途中头顶气泡的锚点还在屏幕外，
    /// 会被钳在画面边缘、脱离本人 —— 与狗剧情「气泡看不到」是同一个病根（锚点跑到屏外）。
    /// 5 秒看门狗：窄屏里队尾第三人从屏外走到编队位约 2.5~3 秒，留足余量；
    /// 真走不进来也照常说，绝不把演出卡死。非佣兵 / 已到位直接放行。
    /// abortOnLiveStart=true（T6 入场救援）：玩家一点「开始战斗」就不再等，立刻收掉演出让路。
    /// </summary>
    IEnumerator CoWaitRescueSpeakerInPlace(UnitBase unit, bool abortOnLiveStart = false)
    {
        var merc = unit as Mercenary;
        if (merc == null) yield break;
        float t = 0f;
        while (t < 5f && merc != null && merc.IsMapEntering && !_runClosed && !_stageCleared)
        {
            if (abortOnLiveStart && _liveStartRequested) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>首波刷怪（含兜底）；成功才标记 _firstWaveSpawned</summary>
    void TrySpawnFirstWaveOnce()
    {
        if (BattleLootMode.Active) return;
        if (!isInBattle || _stageCleared) return;
        if (!Rules.SkipFirstWaveAuto && !_battleIntroFinished) return;
        // 教程关禁止自动首波/紧急刷怪（否则会从玩家身上穿出来）
        if (Rules.SkipFirstWaveAuto) return;

        // 竞技场（2026-10-30「真战斗」）：首波刷怪口改生成敌方佣兵小队（替代常规怪波，一次性幂等）；
        // 失败时返回 false 不置 _firstWaveSpawned，硬刷协程会按原节奏重试
        if (Mode != null && Mode.Id == BattleModeId.Arena)
        {
            if (ArenaEnemySpawner.SpawnSquad(this))
                _firstWaveSpawned = true;
            return;
        }

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
        // 【2026-11-12 主人报「黑幕结束玩家已站在地图上，卡一下才往前走一段」】
        // 走进场免罪窗提前到摆屏外之前：覆盖「摆屏外→走进场→FinishBattleIntro」整段，
        // 防 ClampToScreen 在 UnitsCanAct 短暂为 true 的窗口把队伍钳回屏内。
        PartyIntroWalking = true;
        PlacePartyAt(enterX, z);
        SnapCameraToBattleView(startX);
        if (follow != null) follow.PauseFollowX();

        ExtendCameraMaxX(startX + 80f);

        var parallax = FindObjectOfType<ParallaxBackground>();
        if (parallax != null) parallax.ResetHeroOrigin();

        // 【2026-11-13 修「抽奖面板一上来不是打开状态」】常显实时抽面板必须在等章节卡之前落位。
        // 根因：本块原放在下面「等章节卡演完」的两个 while 之后，而 GameSceneManager 从 10-20 起
        // 把章节卡挪到 Loading 关闭之后才 Show（LoadBattleAsync :247-266），卡片不透明 + Hold 2.5s/Fade 1.5s。
        // 结果 BeginLive 实际发生在揭幕 + 4s 之后：章节卡淡出过程中底下仍是 Build() 拍的收起态
        //（600 / DraftRoot -400），淡完那一帧突然展开 →「一上来没打开 / 按钮窜一下」。
        // 现在提前到本协程开头（场景激活帧，章节卡还没 Show）：面板 600→750 一步落位、DraftPool
        // 首次装载全藏在不透明黑卡底下；卡片淡出时底下已经是展开态，揭幕第一帧按钮就在屏上。
        // 覆盖：主线正式关 / 金币本 / 竞技场 / 支线岛（裂隙与资源副本仍走各自旧面板流程，不动）。
        // 引导局走脚本化硬引导、羁绊本 T8 拍板全程无抽奖，两者排除。
        // 剧情窗(13000) / 半透横幅(928) 的播放顺序与层级一律不动：面板先开出，剧情照播仍盖在其上。
        // ⚠ 必须同时提前压住硬刷：CoFirstWaveHardFallback 的等待闸门是一次性的，
        //   从现在起直到 DoStartFight（点「开始战斗」）才解除，中途不得再有置 false 的窗口。
        // ⚠ 只此一处 BeginLive：CoPreBattleStoryThenFirstWave 的 liveStartEarly 分支只补压制位，
        //   二次 Begin 会重置抽奖 CD / 引导 /「开始战斗」回调（口径不变）。
        bool liveStartEarly = !IsTutorialRun && !IsBondDungeon && !IsEndlessRift && !IsResourceDungeon;
        if (liveStartEarly)
        {
            _preBattleStoryPlaying = true;
            // 【2026-11-14 修「手机端一进战斗自动开战 / 抽奖与开始战斗按钮点不动」】
            // 本协程由 AutoGameInitializer.Awake 同步 StartNewRun 启动，第 0 帧可能抢在 BattleUI.Awake 之前；
            // 真机脚本执行顺序与编辑器不同，此时 EnterLiveStart → Ensure() 拿不到宿主返回 null，
            // 防卡死分支会直接 onFight() 开打（首波自动刷出），且面板组件没建出来 → Build() 没跑，
            // 预制体里的四个抽奖按钮 /「开始战斗」都没有 onClick，看得见却点不动。
            // 等宿主就绪再开面板；章节卡要等 Loading 关闭才 Show，多等一两帧不影响揭幕时序。
            // 超时不吞：打 Error 后仍走原路径，保留既有防卡死口径。
            float uiGuard = 0f;
            while ((BattleUI.Instance == null || !BattleUI.Instance.gameObject.activeInHierarchy)
                   && uiGuard < 3f)
            {
                uiGuard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
                yield return null;
            }
            if (BattleUI.Instance == null)
                Debug.LogError("[BattleManager] 进关面板宿主 3 秒未就绪，抽奖面板可能挂载失败");
            EnterLiveStartPhase();
        }

        // 【2026-11-14 主人「一进战斗就开始」】会播进关剧情的关（章节战前剧情 / Boss 线引子+三选一）：
        // 从 BeginLive 这一帧起先把四个抽奖按钮置灰，剧情在章节卡淡出第一帧就播，播完才解冻。
        // 判据与下方提前播放共用同一个 bool（不多入口）；不播剧情的关一律不冻结、不提前，行为零变化。
        // 置灰走 BattleEntryDraftPanel.SetDrawLocked 唯一出口（_drawLocked → Refresh.cs:73），
        // 不新增遮罩 / 输入锁 / 字段；LockDrawKeepVisible 会强隐「开始战斗」+ 抬层，进关阶段不能用。
        bool entryStoryEarly = ShouldPlayPreBattleStory() || ShouldOfferBossBlessing();
        if (entryStoryEarly)
            BattleEntryDraftPanel.Instance?.SetDrawLocked(true);

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

        // 抽奖面板已在本协程开头开出（11-13），章节卡演完后不再二次 BeginLive。

        // 【2026-11-14 主人「一进战斗就开始」】章节卡淡出那一帧（玩家看到战斗画面的第一帧）就播
        // 进关剧情，不再等队伍走进场 / FinishBattleIntro；抽奖按钮此时仍置灰，玩家抽不了。
        // 剧情窗 StoryDialogue(13000) > 章节卡 FullscreenFx(12000)，但章节卡此时已 Destroy，
        // 两层不会叠；Loading 也已被 GameSceneManager 等关掉，剧情窗不会压在加载遮罩上。
        // 只对 entryStoryEarly 的关提前：其余关 CoPreBattleStoryThenFirstWave 的后半段
        //（抽奖分流 / ScheduleFirstWaveSpawn）仍留在走进场之后，时序一字不动。
        if (entryStoryEarly)
        {
            yield return CoPreBattleStoryThenFirstWave();
            yield return CoBossStageBlessingChoice();
            BattleEntryDraftPanel.Instance?.SetDrawLocked(false);   // 剧情播完才恢复可抽
        }

        // 【2026-11-09】走进场必须等 Loading 遮罩真正收起再播：开场定位与相机跳位仍留在遮罩底下执行，
        // 遮罩收起后画面是「空战斗场景 + 队伍在屏外」，再连续走进来 —— 位置变更全程不可见。
        // 非主线玩法没有章节卡（GameSceneManager 里 splash=null），原先这里没有任何人等 Loading，
        // 走进场在黑罩底下就走完了，揭幕瞬间直接切到已就位的画面，表现为「闪一下 + 位置被重置」。
        yield return CoWaitForLoadingOverlayGone();

        yield return CoPartyWalkInFromLeft(startX, z);

        FinishBattleIntro(follow);

        // 【2026-11-23 主人拍板】入场救援（T6）改「战场内演出」，挂点从提前槽挪到这里：
        // 旧槽位（章节卡淡出那一帧）队伍还在屏外，像素小人说话玩家根本看不见；
        // 现在队伍已在开战位站定、UnitsCanAct=false，三个说话人不会跑，镜头也已就位。
        // 门禁/存档一次性/生成低血友军仍在 CoEntryRescueBeforeFirstWave 内部，这里只管时序。
        yield return CoEntryRescueBeforeFirstWave();

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
        else if (!entryStoryEarly)
            // 时序：过场卡→队伍进场→战前剧情→抽奖→首波。
            // 协程内部已包含“剧情不可用则直接放行首波”的兜底逻辑。
            // 【2026-11-14】会播进关剧情的关已在章节卡淡出后提前播完，这里不再重播。
            yield return CoPreBattleStoryThenFirstWave();

        // 【2026-11-11 主人拍板「三选一就是战前剧情之后出现的」】Boss 关的 boss 线引子 + 三选一
        // 统一挂在章节线战前剧情之后：剧情窗收干净了才弹，不再压在剧情前面。
        // 门槛与防卡死仍收口在 CoBossStageBlessingChoice（非主线正式关/难度局/引导局直接 yield break）；
        // 正式关此时抽奖面板已开出但首波未刷（_preBattleStoryPlaying 压着），选完仍由「开始战斗」放行。
        // 【2026-11-14】entryStoryEarly 的关已在提前块里播过，这里不再重播（提前块内顺序仍是先章节线后 Boss 线）。
        if (!entryStoryEarly)
            yield return CoBossStageBlessingChoice();

        // 【2026-10-18 主人报「撤离结算下面还能点抽奖」】抽奖等待期间撤离/死亡 → 上游协程当场退场：
        // 不再 StartPartyAdvance（结算界面后面队伍乱跑），也不再走开战日志分支。
        if (_runClosed) yield break;

        // 2026-09-30：抽完奖才出发 —— 抽奖期间队伍在开战位原地静止（见 FinishBattleIntro）
        // 🔴【2026-10-29 实时抽改版】正式关/金币本进关阶段非阻塞（CoPreBattleStoryThenFirstWave
        // 在 EnterLiveStartPhase 后即返回，不等玩家点「开始战斗」）→ 队伍出发挪到 OnLiveStartFight，
        // follow 暂存下面两个字段；无限裂隙（旧面板阻塞等玩家）与引导局照旧这里直接出发。
        // 2026-11-09 口径调整：资源副本走旧面板、不进 LiveMode 回调，因此必须在这里直接出发。
        if (!IsEndlessRift && !IsTutorialRun && !IsResourceDungeon)
        {
            _liveStartFollow = follow;
            _liveStartAdvancePending = true;
            // 【2026-11-14 修「1-5 点开始战斗无反应」】玩家若在协程走到这里之前就点过「开始战斗」
            //（Boss 关三选一 / 战前剧情那一段，按钮早已亮出），DoStartFight 当时消费不到 pending，
            // 不补这一步队伍会永久停在开战位。补消费后 pending 立即清零，正常流程零影响。
            // 同时解除 _preBattleStoryPlaying：玩家已明确要开打，剧情只是插播，
            // 不解除的话若他点得更早（首波还没刷出），硬刷兜底会一直等这个位。
            if (_liveStartRequested)
            {
                _liveStartAdvancePending = false;
                _preBattleStoryPlaying = false;
                ScheduleFirstWaveSpawn();   // 幂等：首波已刷过就直接返回
                StartPartyAdvance(_liveStartFollow);
            }
        }
        else
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
        // 【2026-11-07】实时抽改版（2026-10-29）后，正式关/金币本在开战序列结束时首波**故意还没刷**：
        // EnterLiveStartPhase 非阻塞，要等玩家点「开始战斗」(OnLiveStartFight → ScheduleFirstWaveSpawn)。
        // 旧判定把「此时无怪」当故障报 LogError，属误报；这里按阶段分流，不改刷怪逻辑、不新增入口。
        else if (_liveStartAdvancePending)
            Debug.Log("[BattleManager] 开战完成，首波待玩家点「开始战斗」后触发");
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
        SetPartyWalkAnim(true);

        while (elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            float hx = UnitBase.GetCombatX(hero);
            if (hx >= targetHeroX - 0.06f)
                break;

            float nx = Mathf.Min(targetHeroX, hx + speed * Time.unscaledDeltaTime);
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
        // 【2026-11-12 主人报「玩家一开始就在屏幕里，没有从外面走进来的过程」】
        // 摆屏外发生在 LoadStage（Awake 早期），此刻 BattleViewportFit 还没把瘦屏 orthoSize
        // 撑大：实测摆位时 halfW=2.39（5.4×瘦屏比例）、揭幕后变 3.04（锁定值 5.4×720/1280），
        // 差 0.65 → 队伍只摆到屏外 0.5，揭幕第一帧玩家半个身子已在屏内。
        // 战斗横向半宽被适配锁定为「设计比例 × CAMERA_ORTHO_SIZE」，取两者较大值兜住此时序差；
        // 宽屏 halfW 更大仍读相机，行为不变。
        float lockedHalfW = GameConfig.CAMERA_ORTHO_SIZE * GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        if (halfW < lockedHalfW)
            halfW = lockedHalfW;
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
        // 宠物跟队伍一起播进场走/停走（宠物动画走 PlayAnim 状态名，不用 UnitAnimation 参数）
        var pet = GetDeployedPet();
        if (pet != null) pet.SetPartyWalking(walking);
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

        // 宠物跟队伍一起摆位（2026-11-22）：进驻场时同 hero 从左侧一起走入，不再提前站在战场上。
        // 必须与 hero 同批摆，故放在 mercs==null 早退之前；偏移复用 DeployDeployedPet 的 PET_FOLLOW_OFFSET。
        var pet = GetDeployedPet();
        if (pet != null)
        {
            if (pet.rb != null) pet.rb.velocity = Vector2.zero;
            GameConfig.AttachToUnitRoot(pet.transform);
            GameConfig.SetWorldPosition(pet.gameObject, new Vector3(heroX - PET_FOLLOW_OFFSET, UnitBase.GROUND_Y, z));
            if (pet.facingDir != 1) pet.Face(1);
            EnsureSpritesEnabled(pet.transform);
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


    /// <summary>
    /// 【2026-11-23 主人拍板·狗营救分镜】整数制关 3 精英关的精英末波要不要当「首领来袭」播：
    /// 狗被打跑后先弹首领大图、再出精英。只认剧情已激活（_petRescueStageActive）的那一关的末波，
    /// 其它精英关 / 其它波恒 false，别处不用另写判定。
    /// </summary>
    internal bool IsPetRescueEliteWaveAnnounce(int waveIdx)
    {
        if (!_petRescueStageActive) return false;
        if (_waves == null || waveIdx < 0 || waveIdx >= _waves.Count) return false;
        if (waveIdx != _waves.Count - 1) return false;      // 只末波（精英关末波 = 精英波）
        var w = _waves[waveIdx];
        if (w == null || w.isBossWave) return false;
        // 【2026-11-26 二修】强盗波已按 Boss 打标（WavePlanner.Plan 写 isBossWave=true），
        // 上方 `w.isBossWave` 已早退，「首领来袭」由 Boss 主链播；本行退役保留（不删）。
        if (!string.IsNullOrEmpty(w.intruderEventId)) return false;
        return true;
    }

    /// <summary>清完当前波后出下一波：第 1 波清场播全关唯一一次「下一波来袭」Gate，其余普通波清完直接出兵，Boss 波仍走 CoAnnounceThenSpawnNextWave 播「首领来袭」再出兵（2026-11-13 v5 口径）</summary>
    void BeginNextWaveCountdown()
    {
        if (Rules.SkipWaveAnnounce) return;
        // 【2026-11-24 主人拍板】跨大波三选一没选完不许推进。本方法由 Update 每帧调用，
        // 弹层期间 _waveAnnounceCo 还是 null，必须自己挡重入，否则会重复弹。
        if (_waveDraftPending) return;
        // 【2026-11-23 主人「狗剧情在前一波播完、处理完再出首领来袭」】剧情播放期间不推进下一波。
        // 旧版这里没闸：Update 的 Emergency 每帧调本方法，狗插叙把 _preBattleStoryPlaying 置 true
        // 后，下一帧 hook 已 fired 返回 false → 直接排 CoAnnounceThenSpawnNextWave（首领来袭+刷 Boss），
        // 与狗剧情并行。剧情收窗后标志还原，下一帧从这里自然推进，狗已 DeployRescuedPet 归队助战。
        if (_preBattleStoryPlaying) return;
        // 【2026-11-23 统一编排器】演出占用战场期间一律不推进波次（唯一真源 = IsOccupyingBattlefield）。
        // 旧版散落的 phase=1/3 判断已随 4 个并发协程一起删除；演出结束由 finally 释放标志，
        // 下一帧 Update Emergency 自然补上首领来袭，无自锁、无抢跑。
        if (CutsceneOccupyingBattlefield) return;
        if (_allWavesSpawned || _portalActive || _stageCleared) return;
        int nextIdx = FindNextUnspawnedWaveIndex();
        if (nextIdx < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            return;
        }
        if (CountAliveMonsters() > 0) return;
        if (_waveAnnounceCo != null) return;

        // 【2026-11-23 统一编排器】第一波清完只"激活"判定（不插叙、照常刷第二波）；
        // 第二波刷出时由 Update → TryPlayDogRescueCutscene 启动单协程演出，占用标志自动拦本方法。
        TryPetRescueActivate();

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
        BattleUI.Instance?.UpdateWaveProgress(Mathf.Max(0, nextIdx), _waves != null ? _waves.Count : 0);
        // 【2026-11-23 主人拍板】黄昏关（以 Phase==Dusk 判定）清完第 1 波后渐变到夜晚；末波/白天关/夜关/引导局由方法内部判定自然不推进。
        BattleAtmosphere.Instance?.OnWaveCleared(Mathf.Max(0, nextIdx), _waves != null ? _waves.Count : 0);

        // 【2026-11-01 主人拍板】抽奖只在每层开始（CoStageEntryDraft），波清一律不弹抽奖窗：
        // 无限裂隙与正式关/金币本同款 —— 旧口径：5 秒倒计时（不冻结）→「下一波来袭」预告出兵（2026-11-12 起停用）。
        // 旧「裂隙每波清完弹连抽窗」（2026-10-21/26 口径）作废。整段在 _waveAnnounceCo 里，
        // :671 `if (_waveAnnounceCo != null) return;` 的防重入语义不变。
        // 【2026-11-13 v5 覆盖 11-12 口径】普通波仅第 1 波清场播一次「下一波来袭」Gate（复用现成播报链），
        // 其余普通波清完仍直接 SpawnNextPendingWave() 出兵；5 秒倒计时与「下一波来袭」旧链保持停用（只恢复一次 Gate 播报，不恢复倒计时）。
        // 旧 5 秒倒计时 + 下一波来袭预告整段停用，函数保留不删（下线≠删除）；
        // Boss 波仍走 CoAnnounceThenSpawnNextWave 播「首领来袭」，羁绊本 Boss 前剧情保留。
        bool boss = _waves != null && nextIdx < _waves.Count && _waves[nextIdx] != null && _waves[nextIdx].isBossWave;
        // 【2026-11-23 主人拍板·狗营救分镜】关 3 精英末波也走「首领来袭」大图：狗被打跑后先弹大图再出精英。
        if (!boss && IsPetRescueEliteWaveAnnounce(nextIdx))
        {
            _waveAnnounceCo = StartCoroutine(CoAnnounceThenSpawnNextWave());
            return;
        }
        // 【2026-11-14 主人拍板·覆盖 2026-11-13 v5「全关唯一一次 Gate」】
        // 「下一波来袭」只在**跨大波**那一拍播：下一个待刷波是另一个回合（大波）的首波时才播，
        // 同一大波里的小波之间一律直刷（配合 WAVE_STREAM_GAP_SEC=1 秒流式压上）——
        // 就是植物大战僵尸那种「一波跟一波涌上来，只有新的一大波才亮旗」的节奏。
        // 大波边界真源 = 上一波的 WaveData.isRoundEnd（FormalRoundEndFlags 铺的回合末标记）。
        if (!boss)
        {
            bool crossRound = nextIdx > 0
                && _waves != null && nextIdx - 1 < _waves.Count
                && _waves[nextIdx - 1] != null && _waves[nextIdx - 1].isRoundEnd;
            if (crossRound)
            {
                // 【2026-11-24 主人拍板·下线】原「跨大波先冻场三选一」已下线（TryShowWaveClearDraft 恒 false），
                // 跨大波直接播「下一波来袭」+ 出兵；同一大波内的小波仍是流式直刷。
                if (TryShowWaveClearDraft()) return;
                _waveAnnounceCo = StartCoroutine(CoAnnounceThenSpawnNextWave());
                return;
            }
            SpawnNextPendingWave();
            return;
        }
        _waveAnnounceCo = StartCoroutine(CoAnnounceThenSpawnNextWave());
    }

    /// <summary>
    /// 跨大波「三选一」（2026-11-24 主人拍板）：
    /// 冻场 → 按<b>抽奖的概率</b>掷一个类别 → 该类三选一 → 玩家选完（或倒计时随机选）才出下一波。
    ///
    /// <para>为什么用抽奖那套：主人要求「三选一的概率按抽奖的概率来」。<c>AvailableCategories()</c>
    /// 本身就是「有内容才进候选」——<b>佣兵满了 / 技能槽满了会自动从候选里消失</b>，
    /// 所以不用另写「满了怎么办」的分支；权重也只认 <see cref="SlotMachineDefs"/> 那一份。</para>
    ///
    /// <para>抽奖币方向传 <c>allowGold: false</c>：白送的奖励再抽回币没有意义（与老虎机免费三选一同口径）。</para>
    ///
    /// <para>返回 true = 已弹出，调用方不要再推进波次；false = 条件不满足，调用方走原流程。</para>
    /// </summary>
    bool TryShowWaveClearDraft()
    {
        // 【2026-11-24 主人拍板·下线】去掉波次间三选一（弹了但看不见，主人决定不要了）。
        // 跨大波直接走原流程：CoAnnounceThenSpawnNextWave 播「下一波来袭」+ 出兵。
        // 方法体保留不删（下线≠删除），老虎机抽奖 / 通关奖励的三选一仍走 LevelUpDraftUI，不受影响。
        return false;
#pragma warning disable CS0162 //  unreachable code 保留，方便日后恢复
        if (IsTutorialRun) { Debug.Log("[WaveClearDraft] SKIP: IsTutorialRun"); return false; }
        if (_waveDraftPending) { Debug.Log("[WaveClearDraft] SKIP: _waveDraftPending"); return false; }
        int draftNextIdx = FindNextUnspawnedWaveIndex();
        if (_waveDraftDoneForNextIdx == draftNextIdx) { Debug.Log("[WaveClearDraft] SKIP: already drafted for nextIdx=" + draftNextIdx); return false; }
        if (LevelUpDraftUI.IsShowing) { Debug.Log("[WaveClearDraft] SKIP: LevelUpDraftUI.IsShowing"); return false; }

        // 类别候选 & 权重复用抽奖系统（唯一真源，不另写一份概率）
        // 【2026-11-24】满员也给佣兵方向：这是免费奖励，选中后弹「换下谁」，不是花钱买到空包。
        var cats = SlotMachineSystem.AvailableCategories(allowMercWhenFull: true);
        if (cats == null || cats.Count == 0) { Debug.Log("[WaveClearDraft] SKIP: cats empty (skills=" + DraftPool.HasSkillContent() + " merc=" + DraftPool.HasMercContent(true) + " equip=" + DraftPool.HasEquipContent() + ")"); return false; }

        var cat = SlotMachineSystem.RollCategory(cats, allowGold: false);
        var cards = DraftPool.BuildCards(cat, true, allowMercWhenFull: true);
        if (cards == null || cards.Count == 0) { Debug.Log("[WaveClearDraft] SKIP: cards empty (cat=" + cat + ")"); return false; }

        var rd = RunDraftDirector.Instance ?? RunDraftDirector.Ensure(this);
        if (rd == null) { Debug.LogError("[WaveClearDraft] FATAL: RunDraftDirector.Ensure 也返回 null"); return false; }

        _waveDraftPending = true;
        Debug.Log("[WaveClearDraft] OK: cats=" + cats.Count + " cat=" + cat + " cards=" + cards.Count);
        LevelUpDraftUI.Show(cards, "波次奖励 · 选一个", card =>
        {
            _waveDraftPending = false;
            // 记录为哪个 nextIdx 弹过：wave 刷出前 Emergency Update 重复进来时被挡住；wave 刷出后 nextIdx 变化，下一个 crossRound 窗口可再弹。
            _waveDraftDoneForNextIdx = draftNextIdx;
            // 统一出口：生效 + 存档 + 刷 HUD + toast（与抽奖面板同一条链）
            if (card.IsValid) rd.OnCardPicked(card);

            // 【2026-11-24】满员选中佣兵卡会接着弹「换下谁」：等它收场再推进下一波，
            // 否则下一波会在替换弹窗底下刷出来。
            if (MercReplaceConfirmUI.IsOpen)
                StartCoroutine(CoAdvanceWaveAfterMercReplace());
            else
                AdvanceNextWaveAfterDraft();
        }, null, true, GameConfig.WAVE_CLEAR_DRAFT_SEC);
        return true;
#pragma warning restore CS0162
    }

    /// <summary>波间奖励收场后推进下一波（含通关/传送门/已在排队的拦截，口径与原来那段一致）。</summary>
    void AdvanceNextWaveAfterDraft()
    {
        if (_allWavesSpawned || _portalActive || _stageCleared || _waveAnnounceCo != null) return;
        _waveAnnounceCo = StartCoroutine(CoAnnounceThenSpawnNextWave());
    }

    /// <summary>满员替换弹窗收场后才推进下一波。</summary>
    IEnumerator CoAdvanceWaveAfterMercReplace()
    {
        while (MercReplaceConfirmUI.IsOpen) yield return null;
        AdvanceNextWaveAfterDraft();
    }

    // ============================================================
    // 【2026-11-13 主人拍板】波流式衔接：两拨合成一波
    // ============================================================
    // 原节奏 = 清一波 → 才来下一波（普通波 2026-11-12 起已无「下一波来袭」过场，但两拨仍分开）。
    // 现改为：上一波**出完**（出海协程结束）后隔 WAVE_STREAM_GAP_SEC=1 秒，下一波直接压上来，
    // 不等清场、允许场上叠怪 —— 玩家面前始终有足够的怪，升级成长感与紧张感都在。
    // 怪量口径不变（每波仍按既有表摇 10-15 等，只是衔接更紧）；Boss 波不参与流式，
    // 仍等场上清空后走「首领来袭」过场（2026-11-12 拍板保留）。

    /// <summary>上一波出完后调用：排 1 秒后的下一波。重复调用会重排（顺延），不叠协程。</summary>
    internal void ScheduleWaveStreamNext()
    {
        StopWaveStream();
        _waveStreamCo = StartCoroutine(CoWaveStreamNext());
    }

    IEnumerator CoWaveStreamNext()
    {
        // 用缩放时间：抽奖面板 / 剧情冻结时计时跟着停，不会在冻结里偷跑出怪
        yield return new WaitForSeconds(GameConfig.WAVE_STREAM_GAP_SEC);
        _waveStreamCo = null;
        TickWaveStreamSpawn();
    }

    /// <summary>流式衔接到点：能刷就刷（允许叠刷），不能刷就按原因顺延或放弃。</summary>
    void TickWaveStreamSpawn()
    {
        if (!isInBattle || _stageCleared || _runClosed || BattleLootMode.Active) return;
        if (_preBattleStoryPlaying) return;
        // 【2026-11-23 统一编排器】演出占用战场期间不流式刷怪，避免 WaitWaveCleared 永远清不完。
        if (CutsceneOccupyingBattlefield) return;
        // 2026-11-23 主人「下一波和剧情不要同时出现」：任何剧情正在播（timeScale=0 的全屏对白，
        // 或战场内不冻结时间的插叙演出）都顺延流式刷怪，绝不放弃——剧情收窗后下一拍自然补上。
        // 旧版只看 _preBattleStoryPlaying，覆盖不到羁绊本 Boss 前剧情/敌对乱入等未置该位的剧情。
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying)
        {
            ScheduleWaveStreamNext();
            return;
        }
        int nextIdx = FindNextUnspawnedWaveIndex();
        if (nextIdx < 0) return;
        var next = _waves != null && nextIdx < _waves.Count ? _waves[nextIdx] : null;
        if (next == null || next.isBossWave) return;                    // Boss 波仍走清场 + 首领来袭
        if (_waveAnnounceCo != null || _waveAnnounceRunning) return;    // 「首领来袭」播报中不抢跑
        // 【2026-11-14 主人拍板「只有跨回合才播下一波来袭」】上一波是回合末（大波边界）时流式不抢跑：
        // 交给波清链 BeginNextWaveCountdown 的跨回合横幅（CoAnnounceThenSpawnNextWave）出兵。
        // 不加这道闸，1 秒流式会把 ch2 [2,2] / ch3+ [2,2,2] 的跨回合横幅全部吃掉（实测零横幅）。
        if (nextIdx > 0 && _waves != null && nextIdx - 1 < _waves.Count
            && _waves[nextIdx - 1] != null && _waves[nextIdx - 1].isRoundEnd)
            return;
        // 上一波还在出海（SpawnWave 会掐掉上一个出海协程，直接刷会截断上一波）→ 顺延再等 1 秒
        if (_spawnWaveCo != null) { ScheduleWaveStreamNext(); return; }
        Planner.SpawnNextPendingWave(allowStackWhileAlive: true);
    }

    void StopWaveStream()
    {
        if (_waveStreamCo != null)
        {
            StopCoroutine(_waveStreamCo);
            _waveStreamCo = null;
        }
    }

    /// <summary>
    /// 波清 → 倒计时 → 预告+出兵链。
    /// 🔴【2026-11-01 主人拍板】波清**不弹抽奖窗**，无限裂隙与正式关/金币本统一口径：
    /// 5 秒倒计时（不冻结，玩家可趁机实时抽——裂隙实时抽仍被 TryLiveDraw 守卫拦着，
    /// 裂隙的抽奖只发生在每层开始的 CoStageEntryDraft）→「下一波来袭」预告出兵。
    /// 旧「裂隙每波清完弹连抽窗」（2026-10-21/26 口径）随本拍板整体作废。
    /// 末波/Boss 波清完走 StartStageClearRewardSequence 通关链，不进本方法 → 末波后无波间流程。
    /// </summary>
    IEnumerator CoWaveIntermissionDraftThenNextWave()
    {
        // 【2026-10-22 主人拍板】石像未点 → 一并收起，不结转（原裂隙波清口径保留；
        // 正式关石像挂在旧波间抽奖窗上，实时抽改版后本就不弹，此处对裂隙兜一把）
        if (IsEndlessRift)
            BattleStatueEventUI.ForceCollapse();

        yield return CoWaveCountdown(5f);
        // 防撤离/死亡/通关后旧协程串台：当场退场（协程被外部 StopWaveAnnounce 停时同样安全）
        if (_runClosed || !isInBattle || _stageCleared) yield break;
        yield return CoAnnounceThenSpawnNextWave();
    }

    /// <summary>
    /// 【2026-10-29 主人拍板·实时抽改版】波清后的 5 秒倒计时：
    /// 复用侧栏「下一波 N」倒计时 UI（<c>BattleSideHud.SetWaveCountdown</c>，原定时波专用、
    /// 当前闲置，canSkip=false 不可点加速，显示「下一波 N / 即将出兵…」）。
    /// 期间<b>不冻结</b>战斗 —— 玩家可趁机点常显按钮实时抽。走完由调用方播预告出兵。
    /// 撤离/死亡/通关随时收显示退场。
    /// </summary>
    IEnumerator CoWaveCountdown(float sec)
    {
        float left = sec;
        while (left > 0f)
        {
            if (_runClosed || !isInBattle || _stageCleared)
            {
                BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
                yield break;
            }
            BattleSideHud.Instance?.SetWaveCountdown(true, left, false);
            left -= Time.unscaledDeltaTime;
            yield return null;
        }
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
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
            // 【2026-11-23 主人拍板·狗营救分镜】关 3 精英末波按 Boss 播报（「首领来袭」大图 + 不叠下面那行字）
            bool bossLook = boss || IsPetRescueEliteWaveAnnounce(nextIdx);
            var kind = bossLook ? BattleWaveAnnounceUI.Kind.Boss : BattleWaveAnnounceUI.Kind.NextWave;
            // 带上本波原型播报（箭雨 / 夹击 / 围杀…），让玩家知道这波跟上一波不一样。
            // 【2026-10-07 主人拍板】Boss 波已经有「首领来袭」大图，下面不再叠一行字。
            string announce = (bossLook || Planner == null) ? "" : Planner.WaveAnnounceText(nextIdx);

            // 【T10】羁绊副本 Boss 前剧情：Boss 波刷出前播，播完再走「首领来袭」大图 + 刷怪。
            // 本协程期间 _waveAnnounceRunning=true，SpawnNextPendingWave 不会被提前触发（沿用既有闸门）。
            if (boss && IsBondDungeon)
            {
                bool bondDone = false;
                BondDungeonStoryBeats.PlayBeforeBoss(() => bondDone = true);
                float watchdog = 0f;
                while (!bondDone && watchdog < 300f)
                {
                    watchdog += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!bondDone) Debug.LogWarning("[BattleManager] 羁绊副本 Boss 前剧情 5 分钟未完成，强制放行 Boss 波");
            }

            // 【2026-11-26 主人拍板】友军助阵改到 Boss 战斗前就位，不再等 Boss 波打到 40% 血中途乱入。
            // 放在「首领来袭」播报前：三人先走进场说完话，播报后 Boss 立刻刷出，时序干净。
            if (boss)
                yield return CoPreBossRescueArrival();

            // ===== T3 敌对乱入：目标波开打前播剧情，播完才刷怪 =====
            // 门禁复用现成出口：只在线性正式主线放行；引导 / 羁绊 / 竞技场 / 活动副本 / 裂隙 零触发。
            bool intruderWave = nextIdx >= 0 && _waves != null && nextIdx < _waves.Count
                                && _waves[nextIdx] != null
                                && !string.IsNullOrEmpty(_waves[nextIdx].intruderEventId)
                                && _waves[nextIdx].intruderSlotCount > 0;
            bool intruderGate = IsFormalMainRun && !IsTutorialRun && !IsBondDungeon
                                && !IsArena && !IsActivityDungeon && !IsEndlessRift
                                && NormalOnlyContentAllowed(BattleDifficulty);   // 剧情只普通难度：困难/噩梦不播（与主线剧情统一口径）
            // 【2026-11-26 主人拍板】强盗原挪到关3/关7、替换末波；同日改回：关3 恢复本章怪物末波，只留关7。
            // 门禁仍读多关位真源（Plan 打标同款双保险）；关3 狗营救剧情照旧，不再有强盗抢时序。
            int curIntruderStageIdx = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : -1;
            // 【2026-11-26】剧情一次性下沉到这里：强盗怪每关必刷（Plan 不再用旗挡打标），
            // 但对话仍按 eventId 全篇只播一次；老档在旧第6关播过 → 关7 照样刷强盗、只跳过台词。
            var intruderSave = SaveSystem.Instance?.Data;
            bool intruderStorySeen = false;
            if (intruderWave && intruderSave != null)
                intruderStorySeen = intruderSave.HasIntruderStorySeen(_waves[nextIdx].intruderEventId);
            if (intruderWave && intruderGate
                && IntruderEventDirector.IsBanditFlavorStage(curIntruderStageIdx)
                && !IntruderEventDirector.IsDogRescueBanditStage(curIntruderStageIdx)
                && !intruderStorySeen)
            {
                bool done = false;
                BattleEntryDraftPanel.Hide();   // 幂等：防抽奖按钮盖住对话窗（Settlement.cs:387 同款）
                bool playing = false;
                try
                {
                    // eventId 仅作 storyKey 传入（本批剧情只有一种，按章节分派台词）
                    playing = IntruderStoryBeats.TryPlayIntruder(CurrentChapter, _waves[nextIdx].intruderEventId, () => done = true);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[BattleManager] 敌对乱入剧情播放异常，放行本波: " + e.Message);
                    // 剧情可能已开播（timeScale 被压 0 / 对话窗停在半句）：强制收掉再放行，绝不卡 timeScale=0
                    StoryDirector.Ensure().StopPlaying();
                    var dlg = DialogueUI.Instance;
                    if (dlg != null) dlg.Hide();
                    playing = false;
                }
                if (playing)
                {
                    // 批次5 修2：开播即 Mark（防同关重进重播；多周目由 Plan 不打标兜底）
                    SaveSystem.Instance?.Data?.MarkIntruderStorySeen(_waves[nextIdx].intruderEventId);
                    float intruderWatchdog = 0f;
                    while (!done && intruderWatchdog < 300f)
                    {
                        intruderWatchdog += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (!done) Debug.LogWarning("[BattleManager] 敌对乱入剧情 5 分钟未完成，强制放行本波");
                }
            }

            yield return BattleWaveAnnounceUI.CoPlay(kind, announce);
        }
        finally
        {
            _waveAnnounceRunning = false;
            _waveAnnounceCo = null;
            StopWaveCountdown();
        }

        // 【2026-11-23 统一编排器】演出占用战场期间不补刷下一波（本条出口是唯一没走
        // BeginNextWaveCountdown 闸的路径）：直接跳过，下一帧 Update Emergency 在演出释放后自然补上。
        if (!_stageCleared && !_portalActive && CountAliveMonsters() <= 0 && !CutsceneOccupyingBattlefield)
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
        var kind = (boss || IsPetRescueEliteWaveAnnounce(nextIdx)) ? BattleWaveAnnounceUI.Kind.Boss : BattleWaveAnnounceUI.Kind.NextWave;
        // 带上本波原型播报（箭雨 / 夹击 / 围杀…），让玩家知道这波跟上一波不一样。
        // 【2026-10-07 主人拍板】Boss 波已经有「首领来袭」大图，下面不再叠一行字。
        string announce = (kind == BattleWaveAnnounceUI.Kind.Boss || Planner == null) ? "" : Planner.WaveAnnounceText(nextIdx);
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
        // 【2026-11-24】判定锁现状：读不随动态视野缩放变化的基准，点击落点 / 刷怪点不漂移。
        float halfW = GameConfig.CombatJudgeOrthoSize() * cam.aspect;
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
        // 【2026-11-09】点击地点 → 我方全体集结（吃点击 + 推进 2 秒驻留）
        BattleRally.Tick();
        // 升级三选一：可行动时消费升级队列
        RunDraftDirector.Instance?.Tick();

        // 首波刷怪：主路径 ScheduleFirstWaveSpawn；兜底仅 CoFirstWaveHardFallback（勿在 Update 叠刷）

        // 【2026-11-23 统一编排器】第二波刷出时启动狗营救单协程演出（狗归队改由演出步骤驱动）
        TryPlayDogRescueCutscene();
        // 【2026-11-24 主人「让怪都来打玩家」】演出激活期间每帧把怪强制锁玩家（气泡解冻→首领来袭前）
        TickDogRescueForceTarget();

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

        // 竞技场（2026-10-30「真战斗」）：不规划常规怪波，敌方 = 开战时一次性生成的佣兵小队——
        // 直接视为「波次全部刷完」，清场判定走下面的存活数（_totalMonstersSpawnedThisStage 已由
        // ArenaEnemySpawner 抬起，>0 条件自然满足）
        bool arenaSquadStage = Mode != null && Mode.Id == BattleModeId.Arena;
        bool allSpawned = arenaSquadStage;
        if (!arenaSquadStage)
        {
            if (_waves != null)
            {
                for (int i = 0; i < _waves.Count; i++)
                {
                    if (_waves[i] == null || !_waves[i].spawned) { allSpawned = false; break; }
                }
            }
            else allSpawned = false;
        }
        _allWavesSpawned = allSpawned && (arenaSquadStage || (_waves != null && _waves.Count > 0));

        // 所有波次已刷完且场上无活怪 → 宝箱结算（用存活数，勿用 list.Count）
        if (_allWavesSpawned && CountAliveMonsters() == 0 && _totalMonstersSpawnedThisStage > 0
            && !_portalActive && !_stageCleared && !_rewardSequenceStarted
            && !SuppressStageClear)
        {
            // 批次4 波末收口：Boss 波结束 → T5 撤场 / T6 谢幕剧情+撤场（播在结算前，只播剧情不拦结算）
            TickRescueSquadWaveEnd();
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
                // 【2026-11-11 根因修复】槽位暂时失效（阵亡 / 取不到人 / 技能未就绪）时把
                // 「本关已灌满」标志一并复位，单位回来后按每关开局口径重新灌满。
                // 否则按 MpProfile 的 1.2~1.3/秒 从 0 爬，池 150 要近两分钟才回得来 —— 主人报的
                // 「蓝条一下变成 0% 且一直不回」就是这里。换佣兵（同槽、名单不变）不进这分支，
                // 「换人不回满防刷蓝」的口径不受影响（只有 ResetMpForNewStage 才该整关重置）。
                _mercMpFilled[i] = false;
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
        // 【2026-11-12 主人报「一进来就在屏幕边走」实锤根因】走进场期间队伍是**故意**摆在左屏外的，
        // 这里每帧把英雄钳回屏内，会把 -6.03 拖到左屏缘 -5.38（= camX - halfW + margin），
        // 揭幕第一帧就贴在屏边、只能看见最后 2.7 单位走位。免罪窗与 ClampToScreen 同口径：
        // 整段入场（摆屏外 → 走到开战位）不钳。
        if (PartyIntroWalking) return;
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
