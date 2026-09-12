using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 战斗编排器：本局状态、胜负/结算、能量与开场过场。
/// 波次规划/出怪 → <see cref="WavePlanner"/>；技能施放 → <see cref="SkillCastService"/>。
/// </summary>
public class BattleManager : Singleton<BattleManager>, ICombatBoundSingleton
{
    [Header("场景引用")]
    public Hero hero;
    public Transform spawnPoint;
    public Transform endPoint;
    public Transform[] monsterSpawnPoints;
    /// <summary>用户摆好的 unit 节点：所有人物/怪物挂在其下，位置即草地站立线</summary>
    public Transform unitRoot;

    public List<UnitBase> allyUnits = new List<UnitBase>();
    public List<UnitBase> monsters = new List<UnitBase>();
    public StageData currentStage;
    public long currentGold = 0;
    /// <summary>开战时城镇金币快照；死亡时本局增量清零用</summary>
    long _goldAtRunStart;
    int _enchantAtRunStart;
    int _matsAtRunStart;
    public BattleRunStats RunStats { get; private set; } = new BattleRunStats();
    public bool isInBattle = false;
    /// <summary>自动战斗未开放：UI 已隐藏，战斗逻辑不读取。勿在本阶段接线 AI。</summary>
    public bool isAutoBattle = false;
    public List<AttrBonusData> tempBuffs = new List<AttrBonusData>();
    /// <summary>开战过场结束后才允许单位行动</summary>
    public bool UnitsCanAct { get; set; } = true;
    /// <summary>
    /// 剧情仍冻结战斗（UnitsCanAct=false），但允许怪物继续走进场。
    /// 仅埋伏过场等短窗口使用；对白期间必须关掉。
    /// </summary>
    public bool AllowMonsterMapEnter { get; set; }
    /// <summary>黑幕结束后队伍从左走进场（此期间播走路动画，但不战斗）</summary>
    public bool PartyIntroWalking { get; private set; }
    /// <summary>本局规则包。引导特例走 TutorialRules，勿再往核心循环加 IsTutorialRun。</summary>
    TutorialRules _rules = TutorialRules.Formal;
    public TutorialRules Rules
    {
        get => _rules ?? TutorialRules.Formal;
        private set => _rules = value ?? TutorialRules.Formal;
    }
    public bool IsTutorialRun => Rules.Active;
    /// <summary>引导拿剑后：强伤一刀一个怪的爽点阶段。</summary>
    public bool TutorialPowerFantasy { get; private set; }
    public bool SuppressStageClear { get; set; }
    public bool SkipLegacyOnEvacuate { get; set; }
    /// <summary>本局金币获得倍率（剧情选择等）</summary>
    public float runGoldGainMul = 1f;
    /// <summary>当前关任务清关金币（HUD 预览 + 开箱发放）</summary>
    public int StageQuestClearGold { get; private set; }
    bool _stageQuestGoldGranted;
    string _stageQuestObjective = "击败所有敌人";
    /// <summary>本关已击杀数（进度条口径；勿用 goal-alive，未刷波次会假满）</summary>
    int _defeatedMonsterKills;
    internal int _tutorialSpriteMelee = 1;
    internal int _tutorialSpriteRanged = 2;
    internal int _tutorialEliteCount;
    internal int _tutorialWaveMonsterCount;
    internal float _tutorialHpMin = TutorialBattleTable.DefaultHpMin;
    internal float _tutorialHpMax = TutorialBattleTable.DefaultHpMax;
    internal float _tutorialEliteHpMin = TutorialBattleTable.DefaultEliteHpMin;
    internal float _tutorialEliteHpMax = TutorialBattleTable.DefaultEliteHpMax;
    internal bool _tutorialHpFromTable;
    /// <summary>本局怪物攻速倍率（剧情选择等）</summary>
    public float runMonsterAtkSpeedMul = 1f;
    /// <summary>正在走向 chuansongmen，放宽屏幕钳制</summary>
    public bool PortalWalkMode => _portalActive && !_stageCleared;
    /// <summary>佣兵相对主角前后间距（世界单位）；近战前/远程后由 UnitCrowd 决定符号。</summary>
    public const float MERC_FRONT_SPACING = 0.55f;
    [System.Obsolete("Use MERC_FRONT_SPACING / UnitCrowd.GetMercDesiredCombatX")]
    public const float MERC_BEHIND_SPACING = MERC_FRONT_SPACING;
    /// <summary>开场从站位左侧多远走进来（越小出生越靠镜头/越靠右）</summary>
    const float PARTY_ENTER_FROM = 1.15f;

    float _battleStartTime;
    internal bool _firstWaveSpawned;
    bool _eliteToastShownThisStage;
    bool _battleIntroFinished;
    Coroutine _firstWaveSpawnCo;

    public int CurrentChapter => ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
    public int BattleDifficulty { get; private set; }
    public bool IsGoldDungeon { get; private set; }
    public float DifficultyStatScale => GameConfig.GetDifficultyStatScale(BattleDifficulty);
    public float DifficultyGoldMul => GameConfig.GetDifficultyGoldMul(BattleDifficulty);

    // === 波次系统（规划/出怪在 WavePlanner；此处只留本局状态） ===
    WavePlanner _wavePlanner;
    internal WavePlanner Planner => _wavePlanner ?? (_wavePlanner = new WavePlanner(this));

    internal List<WaveData> _waves = new List<WaveData>();
    internal int _totalWaves = 0;
    internal bool _allWavesSpawned = false;
    private bool _stageCleared = false;
    internal int _totalMonstersSpawnedThisStage = 0;
    /// <summary>当前进行中的波次下标；-1=尚未开刷</summary>
    internal int _activeWaveIndex = -1;
    private bool _waveAnnounceRunning;
    Coroutine _waveAnnounceCo;
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
    /// <summary>玩家技能能量 0~1（杀怪累积+时间累积，满了可以释放）</summary>
    public float playerSkillEnergy = 0f;
    public const float MAX_SKILL_ENERGY = 1f;
    /// <summary>佣兵技能能量（最多2槽）</summary>
    readonly float[] mercSkillEnergy = new float[2];
    internal Coroutine _spawnWaveCo;
    internal int _offscreenEnterSideToggle;
    SkillCastService _skillCast;
    internal SkillCastService SkillCast => _skillCast ?? (_skillCast = new SkillCastService(this));

    public float GetMercSkillEnergy(int index)
    {
        if (index < 0 || index >= mercSkillEnergy.Length) return 0f;
        return mercSkillEnergy[index];
    }

    // === 传送门 ===
    /// <summary>传送门是否已激活（所有怪清完后激活，玩家进入后通关）</summary>
    private bool _portalActive = false;
    private bool _rewardSequenceStarted = false;
    private Transform _chuanSongMen;
    private bool _portalEnterVfxPlayed;

    /// <summary>
    /// 旧 endPoint 传送门路径（已弃用）。正式通关走 chuansongmen：StageClearRewardDirector → NotifyChuanSongMenOpened。
    /// </summary>
    [System.Obsolete("使用 chuansongmen 通关导演，勿再调用 ActivatePortal")]
    void ActivatePortal()
    {
        GamePerf.Log("[BattleManager] ActivatePortal 已弃用，忽略（请走 chuansongmen）");
    }

    IEnumerator CoActivatePortal()
    {
        yield break;
    }

    /// <summary>开战时预挂传送门动画，避免清场瞬间 AddComponent</summary>
    public static void EnsurePortalAnimatorReady(Transform end)
    {
        if (end == null) return;
        var fx = end.GetComponent<PortalAnimator>();
        if (fx == null) fx = end.GetComponentInChildren<PortalAnimator>(true);
        if (fx == null)
            fx = end.gameObject.AddComponent<PortalAnimator>();
        fx.Warm();
        // 保持未激活，等真正通关再开
        if (fx.gameObject != end.gameObject)
            fx.gameObject.SetActive(false);
        else
            fx.enabled = false;
    }

    internal void ExtendCameraMaxX(float worldX)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) return;
        if (worldX > follow.maxX)
            follow.maxX = worldX;
    }

    /// <summary>
    /// 隐藏传送门（新一局开始时，打完怪才出现）
    /// </summary>
    void HidePortal()
    {
        if (endPoint != null)
        {
            // 隐藏EndPoint视觉（传送门），但保留碰撞/逻辑
            SpriteRenderer portalSr = endPoint.GetComponent<SpriteRenderer>();
            if (portalSr != null)
            {
                portalSr.enabled = false;
            }
            var portalFx = endPoint.GetComponentInChildren<PortalAnimator>(true);
            if (portalFx != null)
            {
                portalFx.enabled = false;
                if (portalFx.gameObject != endPoint.gameObject)
                    portalFx.gameObject.SetActive(false);
            }
        }
    }
    protected override void Awake()
    {
        base.Awake();
    }

    public void StartNewRun()
    {
        StartNewRunInternal();
    }

    void StartNewRunInternal()
    {
        Debug.Log("[BattleManager] ===== StartNewRun 开始 =====");
        StopBattleSpawnCoroutines();
        currentGold = SaveSystem.Instance?.Data?.totalGold ?? 0;
        _goldAtRunStart = currentGold;
        var data = SaveSystem.Instance?.Data;
        _enchantAtRunStart = data != null ? data.enchantStones : 0;
        _matsAtRunStart = data != null ? data.decomposeMats : 0;
        _stageQuestGoldGranted = false;
        _defeatedMonsterKills = 0;
        RunStats.Reset();
        RunStats.Chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        {
            var save = SaveSystem.Instance?.Data;
            string pn = save != null && !string.IsNullOrEmpty(save.playerDisplayName)
                ? save.playerDisplayName : "冒险者";
            RunStats.EnsureAlly(BattleRunStats.PlayerMvpKey, pn);
        }
        tempBuffs.Clear();
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        _portalEnterVfxPlayed = false;
        _chuanSongMen = null;
        UnitsCanAct = false;
        AllowMonsterMapEnter = false;
        isAutoBattle = false;
        MonsterAttackStyleTable.Reload();
        playerSkillEnergy = 0f;
        mercSkillEnergy[0] = 0f;
        mercSkillEnergy[1] = 0f;
        MercenaryManager.Instance?.ClearAllMercs();
        allyUnits.RemoveAll(u => u == null || u is Mercenary);

        HidePortal();
        EnsureRewardDirector();
        StageClearRewardDirector.Instance?.CacheSceneRefs();
        StageClearRewardDirector.Instance?.HideClearProps();
        EnsurePortalAnimatorReady(endPoint);

        if (hero == null)
            hero = Hero.Instance != null ? Hero.Instance : FindObjectOfType<Hero>();
        if (hero == null)
        {
            Debug.LogError("[BattleManager] StartNewRun 失败：找不到 Hero，无法开战/刷怪");
            return;
        }

        if (!allyUnits.Contains(hero))
            allyUnits.Add(hero);

        try
        {
            hero.InitNewRun();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] hero.InitNewRun 异常（继续开战）: {e}");
        }

        Rules = StoryProgress.ShouldStartTutorialBattle() ? TutorialRules.Tutorial : TutorialRules.Formal;
        TutorialPowerFantasy = false;
        _tutorialHpFromTable = false;
        if (Rules.Active)
        {
            StoryProgress.ResetTutorialRunInventoryIfNeeded();
            TutorialDirector.Instance?.ResetBattleTutorialRuntime();
            StoryProgress.ConsumeTutorialBattleFlag();
        }
        // 选职后一律发职业武器（含教程）；木剑兜底仅非教程
        if (GridBackpackSystem.Instance != null)
        {
            PlayerJobDefs.ApplyForBattle();
            if (!Rules.SkipStarterWeaponFallback
                && GridBackpackSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.MainHand) == null)
            {
                if (GridBackpackSystem.Instance.EnsureStarterWeapon())
                    hero.RecalcAttr();
            }
        }
        SuppressStageClear = Rules.SuppressStageClear;
        SkipLegacyOnEvacuate = Rules.SkipLegacyOnEvacuate;
        runGoldGainMul = 1f;
        runMonsterAtkSpeedMul = 1f;
        ApplyChapter1RunModifiersFromSave();
        if (Rules.Active)
            Debug.Log("[BattleManager] 新手引导战斗：短关+强制撤离（规则包 TutorialRules）");

        if (!GameConfig.SOLO_PLAYER_BATTLE && !Rules.SkipMercPrecall)
        {
            EnsureTestMercenaries();
            SpawnMercenaries();
        }

        int targetChapter = AdventureUI.PendingBattleChapter;
        if (targetChapter < 1)
            targetChapter = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        if (targetChapter < 1) targetChapter = 1;
        BattleDifficulty = Mathf.Clamp(AdventureUI.PendingBattleDifficulty, 0, 2);
        IsGoldDungeon = AdventureUI.PendingGoldDungeon;
        AdventureUI.PendingBattleChapter = 0;
        AdventureUI.PendingBattleDifficulty = 0;
        AdventureUI.PendingGoldDungeon = false;

        if (ChapterManager.Instance == null)
        {
            Debug.LogError("[BattleManager] ChapterManager 为空，用临时 Normal 关强行 LoadStage");
            LoadStage(new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() });
            return;
        }

        if (IsGoldDungeon)
            ChapterManager.Instance.StartGoldDungeon(targetChapter);
        else
            ChapterManager.Instance.StartChapter(targetChapter);

        StageData first = null;
        if (ChapterManager.Instance.availableNextStages != null && ChapterManager.Instance.availableNextStages.Count > 0)
            first = ChapterManager.Instance.availableNextStages[0];
        else if (ChapterManager.Instance.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
            first = ChapterManager.Instance.stageMap[0];

        if (first == null)
        {
            Debug.LogError("[BattleManager] 关卡图为空，创建临时 Normal 关");
            first = new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() };
        }

        Debug.Log($"[BattleManager] StartNewRun → LoadStage type={first.type} idx={first.stageIndex}");
        if (!Rules.SkipRiftEnteredAchievement)
            AdventureLogAchievements.OnRiftEntered();
        LoadStage(first);
    }

    IEnumerator CoPreLevelThenLoad(StageData first)
    {
        // 遗产战前三选一已取消；保留方法避免外部误调编译失败
        LoadStage(first);
        yield break;
    }

    void EnsureTestMercenaries()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.townLevel == null) data.townLevel = new TownLevel();

        if (data.permanentMercs == null)
            data.permanentMercs = new List<MercenaryData>();

        for (int i = data.permanentMercs.Count - 1; i >= 0; i--)
        {
            var m = data.permanentMercs[i];
            if (m == null || string.IsNullOrEmpty(m.mercId))
            {
                data.permanentMercs.RemoveAt(i);
                continue;
            }
            // 无效弓手预制体 id 纠正
            if (m.mercId.StartsWith("gongshou") && Resources.Load<GameObject>("Units/" + m.mercId) == null)
                m.mercId = "gongshou101";
            if (m.level < 1) m.level = 1;
            if (m.star < 1) m.star = 1;
            if (string.IsNullOrEmpty(m.displayName)) m.displayName = m.mercId;
            if (string.IsNullOrEmpty(m.uid)) m.uid = System.Guid.NewGuid().ToString("N");
            MercSkillMigrate.AlignMercenary(m);
        }

        // 软著版：佣兵由酒馆三选一反复招募，不再空档自动塞弓手。
        // 旧逻辑保留注释，便于日后调试：
        // if (data.permanentMercs.Count == 0)
        // {
        //     data.permanentMercs.Add(new MercenaryData {
        //         mercId = "gongshou101", displayName = "测试弓手",
        //         favorLevel = 1, level = 1, star = 1,
        //         skillId = SkillRegistry.DefaultMercRangedSkillId,
        //         uid = System.Guid.NewGuid().ToString("N")
        //     });
        // }

        if (data.townLevel.tavern < 1 && data.permanentMercs.Count > 0)
            data.townLevel.tavern = 1;
    }

    void SpawnMercenaries()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null) return;

        var data = SaveSystem.Instance?.Data;
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : hero.transform.position;
        basePos.y = UnitBase.GROUND_Y;
        basePos.z = 0f;

        int max = mm.GetMaxMercSlots();
        int spawned = 0;
        bool countedLaodun = false;
        var party = mm.GetActiveMercData();
        for (int i = 0; i < party.Count && spawned < max; i++)
        {
            var md = party[i];
            if (md == null || string.IsNullOrEmpty(md.mercId)) continue;
            if (!GameConfig.IsMercAvailable(md.mercId, data)) continue;

            Vector3 pos = basePos;
            if (hero != null)
                pos.x = UnitCrowd.GetMercDesiredCombatX(hero, null, spawned);
            else
                pos.x = basePos.x + MERC_FRONT_SPACING * (spawned + 1);

            var merc = mm.SpawnMercenary(md.mercId, pos, Mathf.Max(1, md.level));
            if (merc != null)
            {
                MercSkillMigrate.AlignMercenary(md);
                string active = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercSkillId(md)
                    : md.skillId;
                string passive = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercPassiveSkillId(md)
                    : md.passiveSkillId;
                merc.SetupBattleSkills(active, passive);
                merc.SetPartyIndex(spawned);
                merc.SetDisplayName(md.displayName, md.nickname);
                if (!string.IsNullOrEmpty(md.hireId))
                    merc.SetHireId(md.hireId);
                else
                {
                    string resolved = MercPortraitSprites.ResolveHireId(md.mercId);
                    if (!string.IsNullOrEmpty(resolved))
                        merc.SetHireId(resolved);
                }
                if (!string.IsNullOrEmpty(md.displayName))
                    merc.gameObject.name = "Merc_" + md.displayName;
                allyUnits.Add(merc);
                merc.OnDead += OnMercenaryDead;
                RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
                spawned++;
                if (!Rules.SkipLaodunAchievement && !countedLaodun && md.mercId.StartsWith("dunbing"))
                {
                    AdventureLogAchievements.OnLaodunBattled();
                    countedLaodun = true;
                }
            }
        }
        Debug.Log($"[BattleManager] 已生成佣兵 {spawned} 个 (tavern槽={max})");
    }

    public void OnMercenaryDead(UnitBase merc)
    {
        allyUnits.Remove(merc);
        TryPlayMercDeathLine(merc);
    }

    void TryPlayMercDeathLine(UnitBase unit)
    {
        if (Rules.SkipMercDeathBanter) return;
        if (!(unit is Mercenary m) || m == null) return;
        string key = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
        string line = MercLineTable.Pick(key, MercLineTable.Scene.Death);
        if (string.IsNullOrEmpty(line)) return;

        // 死亡动画期间单位仍在：优先头顶气泡；否则 Toast
        if (unit != null && unit.gameObject != null && unit.gameObject.activeInHierarchy
            && (BattleHeadTalkUI.Instance == null || !BattleHeadTalkUI.Instance.IsShowing))
        {
            BattleHeadTalkUI.Ensure().PlayLine(unit, line, 1.25f);
            return;
        }
        if (UIManager.Instance != null)
            UIManager.Instance.ShowToast(line);
        else
            GlobalToastUI.Show(line);
    }

    void ApplyChapter1RunModifiersFromSave()
    {
        if (Rules.SkipChapter1RunModifiers) return;
        string c = StoryProgress.GetChoice(1);
        if (c == "A") runMonsterAtkSpeedMul = 1.05f;
        else if (c == "B") runGoldGainMul = 1.1f;
    }

    public void ApplyChapter1ChoiceModifiers(string choiceId)
    {
        if (choiceId == "A") runMonsterAtkSpeedMul = 1.05f;
        else if (choiceId == "B") runGoldGainMul = 1.1f;
    }

    public int GetAliveMonsterCount() => CountAliveMonsters();

    public void FillPlayerSkillEnergy()
    {
        playerSkillEnergy = MAX_SKILL_ENERGY;
        BattleUI.Instance?.UpdateSkillEnergy(0, playerSkillEnergy);
    }

    /// <summary>友方攻击造成伤害 / 友方受击时涨技能能量（玩家与对应佣兵槽）。</summary>
    /// <summary>盟友受击回能：传入 finalDamage/MaxHp（打满血约攒满一槽）。</summary>
    public void AddCombatSkillEnergy(UnitBase unit, float amount)
    {
        if (unit == null || !unit.isAlly || amount <= 0f || !isInBattle) return;
        if (_stageCleared || _portalActive) return;

        if (unit is Hero)
        {
            playerSkillEnergy = Mathf.Min(MAX_SKILL_ENERGY, playerSkillEnergy + amount);
            BattleUI.Instance?.UpdateSkillEnergy(0, playerSkillEnergy);
            return;
        }

        if (!(unit is Mercenary)) return;
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return;
        int unlocked = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;
        bool solo = GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle;
        bool tutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        for (int i = 0; i < mercSkillEnergy.Length && i < mercs.Count; i++)
        {
            if (mercs[i] != unit) continue;
            bool slotUnlocked = solo ? (tutorialMerc && i == 0) : (i < unlocked);
            if (!slotUnlocked) return;
            mercSkillEnergy[i] = Mathf.Min(MAX_SKILL_ENERGY, mercSkillEnergy[i] + amount);
            BattleUI.Instance?.UpdateSkillEnergy(i + 1, mercSkillEnergy[i]);
            return;
        }
    }

    /// <summary>引导开箱拿剑后进入强伤+多怪爽点。</summary>
    public void BeginTutorialPowerFantasy()
    {
        if (!IsTutorialRun) return;
        TutorialPowerFantasy = true;
        Debug.Log("[BattleManager] 引导拿剑爽点：一刀一个 + 后续加怪");
    }
    /// <summary>导演节拍：按 tutorial_battle 步进刷一波。count/进场方向/HP 档来自表，不由导演发明。</summary>
    public void QueueTutorialStep(int order, float? anchorX = null, UnitBase forcedTarget = null)
    {
        Planner.QueueTutorialStep(order, anchorX, forcedTarget);
    }

    public void QueueTutorialWave(int count)
    {
        Planner.QueueTutorialWave(count);
    }

    public Mercenary SpawnTutorialMerc(string mercId, float hpRatio)
    {
        return SpawnTutorialMercAt(mercId, hpRatio, 7.5f, stunned: true);
    }

    /// <summary>
    /// 引导救援：牧师刷在玩家前方固定距离，原地眩晕；周围刷怪围殴她。
    /// </summary>
    public Mercenary SpawnTutorialMercAt(string mercId, float hpRatio, float aheadDist, bool stunned)
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || hero == null) return null;

        float heroX = UnitBase.GetCombatX(hero);
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;
        Vector3 pos = new Vector3(heroX + aheadDist, UnitBase.GROUND_Y, z);
        string useId = string.IsNullOrEmpty(mercId) ? StoryProgress.TutorialMercId : mercId;
        var merc = mm.SpawnMercenary(useId, pos, 1);
        if (merc == null) return null;
        MercRosterDefs.GetSkillIds(useId, out string active, out string passive);
        merc.SetupBattleSkills(active, passive);
        merc.SetDisplayName(StoryProgress.TutorialMercDisplayName, StoryProgress.TutorialMercNickname);
        merc.SetHireId(StoryProgress.TutorialMercHireId);
        if (!allyUnits.Contains(merc))
            allyUnits.Add(merc);
        merc.OnDead += OnMercenaryDead;
        RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
        float maxHp = merc.attr != null ? merc.attr.GetAttr(AttrType.MaxHp) : 100f;
        merc.currentHp = Mathf.Max(1f, maxHp * Mathf.Clamp01(hpRatio));
        merc.Face(-1);
        if (stunned)
            merc.SetTutorialStunned(true);
        ExtendCameraMaxX(pos.x + 6f);
        return merc;
    }
    /// <summary>在受害者周围刷怪并强制锁定打他（引导围殴）。走 SpawnWave 参数，不再另开刷怪轨。</summary>
    public void SpawnTutorialAmbushAround(UnitBase victim, int count)
    {
        Planner.SpawnTutorialAmbushAround(victim, count);
    }

    /// <summary>诱饵埋伏：从锚点左右两侧刷怪。走 SpawnWave 参数（bilateralEnter）。</summary>
    public void SpawnTutorialFlankAmbush(int count, float? anchorX = null)
    {
        Planner.SpawnTutorialFlankAmbush(count, anchorX);
    }

    public void RetargetAllMonsters(UnitBase target)
    {
        if (monsters == null) return;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i] as Monster;
            if (m == null || m.isDead) continue;
            m.SetForcedTarget(target);
        }
    }

    public void ClearMonsterForcedTargets()
    {
        if (monsters == null) return;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i] as Monster;
            if (m == null) continue;
            m.SetForcedTarget(null);
        }
    }

    // ============================================================
    // 波次生成
    // ============================================================

    public void LoadStage(StageData stage)
    {
        MonsterStatsTable.Reload();
        StageSpawnTable.Reload();
        TutorialBattleTable.Reload();
        MonsterAttackStyleTable.Reload();
        BattleQuestTable.Reload();
        SpritePickWeightTable.Reload();
        WaveSlotTable.Reload();
        ChapterStatScaleTable.Reload();

        currentStage = stage;
        ClearAllMonsters();
        _waves.Clear();
        _totalWaves = 0;
        _allWavesSpawned = false;
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        _portalEnterVfxPlayed = false;
        _chuanSongMen = null;
        _totalMonstersSpawnedThisStage = 0;
        _eliteToastShownThisStage = false;
        AllowMonsterMapEnter = false;
        playerSkillEnergy = 0f;
        mercSkillEnergy[0] = 0f;
        mercSkillEnergy[1] = 0f;
        HeroThunderUltimate.Instance?.ResetForBattle();

        float startX = GetStageStartX();
        float z = unitRoot != null ? unitRoot.position.z : 0f;
        // 只摆玩家/佣兵，不要改写用户的 SpawnPoint 坐标
        GameConfig.SetWorldPosition(hero.gameObject, new Vector3(startX, UnitBase.GROUND_Y, z));
        hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
        GameConfig.AttachToUnitRoot(hero.transform);
        Vector3 mercBasePos = new Vector3(startX, UnitBase.GROUND_Y, z);
        MercenaryManager.Instance?.ResetMercenaries(mercBasePos);
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
        BattleJoystick.Instance?.SetVisible(true);

        // 触发章节背景切换
        SwitchBattleBackground(CurrentChapter);

        // 按关卡类型切 BGM（Loading 中会记 pending，结束后再播）
        GameBgm.PlayForStage(stage.type);

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
        // 进关立刻把进度条 Marker 挂到当前 Node
        if (currentStage != null)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex);
        int monsterGoal = GetStageMonsterGoal();
        var stageQuest = BattleQuestConfig.GetStageQuest(CurrentChapter, stage.type, IsGoldDungeon, BattleDifficulty);
        StageQuestClearGold = stageQuest.clearGold;
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

    /// <summary>停掉开战/硬刷协程，避免连续 LoadStage 叠多个兜底。</summary>
    public void StopBattleSpawnCoroutines()
    {
        StopCoroutine("BattleStartSequenceCoroutine");
        StopWaveAnnounce();
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
        BattleSideHud.Instance?.ResetCombo();
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
        while (!_battleIntroFinished)
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

        string title = GameConfig.GetChapterTitleText(CurrentChapter);
        string body = null;
        if (Rules.UseTutorialSplash)
        {
            title = "森林区域，第一层";
            body = "阳光还能照进来，怪物也不算太强。\n正好适合一个新人进去摸摸路。";
        }
        else if (CurrentChapter <= 1 && StoryProgress.TutorialDone && !StoryProgress.Chapter1ChoiceDone)
        {
            body = "新人任务开始。不要想太多。";
        }
        // 先黑屏（盖住 Loading 底下的战斗场景），Loading 关掉后再开始计时
        var splash = ChapterSplashOverlay.Show(title, body, Rules.UseTutorialSplash, waitLoadingBeforeHold: true);
        float need = (Rules.UseTutorialSplash
            ? ChapterSplashOverlay.TutorialHoldSeconds + ChapterSplashOverlay.TutorialFadeSeconds
            : ChapterSplashOverlay.HoldSeconds + ChapterSplashOverlay.FadeSeconds) + 0.5f;
        float guard = 0f;
        while (splash != null && !splash.IsFinished && guard < need)
        {
            guard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
            yield return null;
        }
        if (splash != null && !splash.IsFinished)
        {
            Debug.LogWarning("[BattleManager] 章节过场超时，强制关闭");
            Object.Destroy(splash.gameObject);
        }

        yield return CoPartyWalkInFromLeft(startX, z);

        FinishBattleIntro(follow);

        if (Rules.DirectorOwnsWaves)
            TutorialDirector.Instance?.NotifyBattleSplashFinished();
        else
            ScheduleFirstWaveSpawn();

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

    /// <summary>走进场结束：恢复镜头跟随 + 立即接战斗 AI（不停步、不切 idle）</summary>
    void FinishBattleIntro(CameraFollow follow)
    {
        if (hero != null)
        {
            float z = hero.transform.position.z;
            PlacePartyAt(UnitBase.GetCombatX(hero), z);
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

        PartyIntroWalking = false;
        _battleIntroFinished = true;
        UnitsCanAct = true;
        MercBattleBanter.EnsureOn(this);
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

        GamePerf.Log("[BattleManager] 下一波来袭 Banner");
        _waveAnnounceCo = StartCoroutine(CoAnnounceThenSpawnNextWave());
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

        _waveAnnounceRunning = true;
        int nextIdx = FindNextUnspawnedWaveIndex();
        bool boss = nextIdx >= 0 && _waves != null && nextIdx < _waves.Count
                    && _waves[nextIdx] != null && _waves[nextIdx].isBossWave;
        var kind = boss ? BattleWaveAnnounceUI.Kind.Boss : BattleWaveAnnounceUI.Kind.NextWave;

        yield return BattleWaveAnnounceUI.CoPlay(kind);

        _waveAnnounceRunning = false;
        _waveAnnounceCo = null;
        StopWaveCountdown();

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
        yield return BattleWaveAnnounceUI.CoPlay(kind);
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
        if (PoolManager.Instance == null) return;
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
        if (BattleLootMode.Active) return;
        if (!isInBattle || _stageCleared) return;

        if (UnitsCanAct)
            RunStats.BattleTimeSec += Time.deltaTime;

        PlayerSkillPassive.TickAutoCast();

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

        // 连杀超时清零显示
        if (_killCombo > 0 && Time.time - _lastKillTime > GameConfig.COMBO_WINDOW)
        {
            _killCombo = 0;
            SyncKillComboHaste();
            BattleSideHud.Instance?.ResetCombo();
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

        // 通关：chuansongmen 已开且玩家走到传送门
        if (_portalActive && hero != null && !hero.isDead && !_stageCleared && _chuanSongMen != null)
        {
            if (hero.transform.position.x >= _chuanSongMen.position.x - 0.6f)
            {
                PlayPortalEnterVfxOnce(_chuanSongMen.position);
                FinishStageAfterPortalReached();
            }
        }

        // 跑图时镜头随英雄缓慢放宽
        if (hero != null)
            ExtendCameraMaxX(UnitBase.GetCombatX(hero) + (_portalActive ? 6f : 12f));

        // 传送门开启后不再把英雄卡在镜头右缘，允许走向 chuansongmen
        if (!_portalActive)
            ClampHeroInCamera();

        // 技能能量：仅盟友受击按伤害/MaxHp 涨（AddCombatSkillEnergy），此处只刷新 UI / 锁槽清零
        if (hero != null && !hero.isDead)
        {
            if (BattleUI.Instance != null)
                BattleUI.Instance.UpdateSkillEnergy(0, playerSkillEnergy);
        }

        if (!GameConfig.SOLO_PLAYER_BATTLE)
        {
            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            int unlocked = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;
            for (int i = 0; i < mercSkillEnergy.Length; i++)
            {
                bool live = mercs != null && i < mercs.Count && mercs[i] != null && !mercs[i].isDead;
                bool hasActive = live && mercs[i].SkillCaster != null && mercs[i].SkillCaster.HasActiveSkill;
                bool manual = !MercSkillMigrate.IsMercSkillAutoCast();
                if (i >= unlocked || !live || !hasActive || !manual)
                {
                    if (mercSkillEnergy[i] > 0f)
                    {
                        mercSkillEnergy[i] = 0f;
                        BattleUI.Instance?.UpdateSkillEnergy(i + 1, 0f);
                    }
                    continue;
                }
                BattleUI.Instance?.UpdateSkillEnergy(i + 1, mercSkillEnergy[i]);
            }
        }
        else if (TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud)
        {
            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null && mercs.Count > 0 && mercs[0] != null && !mercs[0].isDead)
                BattleUI.Instance?.UpdateSkillEnergy(1, mercSkillEnergy[0]);
            else if (mercSkillEnergy[0] > 0f)
            {
                mercSkillEnergy[0] = 0f;
                BattleUI.Instance?.UpdateSkillEnergy(1, 0f);
            }
            if (mercSkillEnergy[1] > 0f)
            {
                mercSkillEnergy[1] = 0f;
                BattleUI.Instance?.UpdateSkillEnergy(2, 0f);
            }
        }
        else
        {
            // 单人/教学锁槽：蓝条强制归零，避免杀怪残留能量把锁头像底下蓝条刷满
            for (int i = 0; i < mercSkillEnergy.Length; i++)
            {
                if (mercSkillEnergy[i] <= 0f) continue;
                mercSkillEnergy[i] = 0f;
                BattleUI.Instance?.UpdateSkillEnergy(i + 1, 0f);
            }
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

    internal void OnMonsterDead(UnitBase monster)
    {
        _aliveMonsterCacheFrame = -1;
        Monster m = monster as Monster;
        if (m == null) return;

        RunStats.KillCount++;
        if (m.IsEliteWave)
        {
            RunStats.EliteKillCount++;
            if (!_eliteToastShownThisStage)
            {
                _eliteToastShownThisStage = true;
                GlobalToastUI.ShowFlythrough("精英击破！");
            }
        }
        if (m.IsBossUnit) RunStats.BossKillCount++;
        if (m.LastDamageSource != null && m.LastDamageSource.isAlly)
            RecordAllyKill(m.LastDamageSource);
        AdventureLogAchievements.OnMonsterKilled(m, CurrentChapter);
        HiddenLevelSystem.AddKillExp(m);

        // 连杀（仅展示，击杀不掉金币）
        float now = Time.time;
        if (now - _lastKillTime <= GameConfig.COMBO_WINDOW)
            _killCombo++;
        else
            _killCombo = 1;
        _lastKillTime = now;
        SyncKillComboHaste();
        if (_killCombo > RunStats.MaxKillCombo)
            RunStats.MaxKillCombo = _killCombo;
        BattleSideHud.Instance?.SetCombo(_killCombo);
        CombatJuice.Instance?.OnKillCombo(_killCombo);
        CombatJuice.Instance?.OnKillFinisher(m);
        HeroThunderUltimate.Instance?.OnMonsterKilled(m);


        _defeatedMonsterKills++;
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        BattleUI.Instance?.UpdateQuest(_stageQuestObjective, defeated, goal, StageQuestClearGold);

        // 技能蓝条改由受击/攻击涨，击杀不再加能量
        AchievementSystem.Instance?.OnKillMonster(CurrentChapter, m.config != null && m.config.isBoss);

        // 图鉴：击败解锁完整描述；未见过则顺带记遭遇并发里程
        if (m.config != null)
            AdventureCodex.MarkMonsterDefeated(m.config.id);

        SpecialWeapons.TryFlavorToastOnKill();

        BattleUI.Instance?.UpdateSkillEnergy(0, playerSkillEnergy);
        BattleUI.Instance?.UpdateSkillEnergy(1, mercSkillEnergy[0]);
        BattleUI.Instance?.UpdateSkillEnergy(2, mercSkillEnergy[1]);

        // 本波清完 → 播「下一波来袭」
        if (CountAliveMonsters() <= 1) // 含即将移除的自己，下一帧会清；用 <=1 更稳
        {
            // 延迟到本帧列表清理后判断；用协程下一帧
            if (!Rules.SkipWaveAnnounce && _waveAnnounceCo == null && !_allWavesSpawned)
                StartCoroutine(CoCheckWaveClearNextFrame());
        }
    }

    IEnumerator CoCheckWaveClearNextFrame()
    {
        yield return null;
        if (!isInBattle || _stageCleared || _portalActive) yield break;
        if (CountAliveMonsters() > 0) yield break;
        if (FindNextUnspawnedWaveIndex() < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            // 正式关：末波清完立刻进宝箱，不空等下一帧 Update
            if (!SuppressStageClear && !_rewardSequenceStarted && !_stageCleared && !_portalActive
                && _totalMonstersSpawnedThisStage > 0 && CountAliveMonsters() == 0)
                StartStageClearRewardSequence();
            yield break;
        }
        BeginNextWaveCountdown();
    }

    // ============================================================
    // 玩家技能释放（由头像点击触发）
    // ============================================================

    /// <summary>释放玩家技能（需要能量满）— 头像点击。执行在 SkillCastService。</summary>
    public bool TryUsePlayerSkill() => SkillCast.TryUsePlayerSkill();

    /// <summary>释放佣兵技能（槽位 index 0/1）— 手动模式</summary>
    public bool TryUseMercSkill(int mercIndex)
    {
        if (MercSkillMigrate.IsMercSkillAutoCast()) return false;
        if (mercIndex < 0 || mercIndex >= mercSkillEnergy.Length) return false;
        if (mercSkillEnergy[mercIndex] < 0.99f) return false;

        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null || mercIndex >= mercs.Count) return false;
        Mercenary merc = mercs[mercIndex];
        if (merc == null || merc.isDead) return false;
        if (merc.SkillCaster == null || !merc.SkillCaster.HasActiveSkill) return false;

        bool ok = merc.SkillCaster.TryCast(manual: true);
        if (!ok) return false;

        mercSkillEnergy[mercIndex] = 0f;
        BattleUI.Instance?.UpdateSkillEnergy(mercIndex + 1, 0f);
        return true;
    }

    /// <summary>佣兵主动技施放入口（自动/手动共用）。执行在 SkillCastService。</summary>
    public bool TryCastMercActiveSkill(Mercenary merc, string skillId, bool manual)
        => SkillCast.TryCastMercActiveSkill(merc, skillId, manual);

    SkillSystem.ActiveSkill ResolveSkill(string skillId) => SkillCast.ResolveSkill(skillId);

    // ============================================================
    // 关卡通关
    // ============================================================

    public void OnStageClear()
    {
        // 兼容旧调用：直接走完整结算选关（无宝箱时）
        FinishStageAfterPortalReached();
    }

    void EnsureRewardDirector()
    {
        if (StageClearRewardDirector.Instance != null) return;
        var go = new GameObject("StageClearRewardDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<StageClearRewardDirector>();
    }

    void TryGrantStageQuestGoldIfQuestComplete()
    {
        if (_stageQuestGoldGranted || StageQuestClearGold <= 0) return;
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        if (defeated >= goal)
            TryGrantStageQuestGold();
    }

    void TryGrantStageQuestGold()
    {
        if (_stageQuestGoldGranted || StageQuestClearGold <= 0) return;
        int grant = Mathf.RoundToInt(StageQuestClearGold * runGoldGainMul);
        if (grant <= 0) return;
        _stageQuestGoldGranted = true;
        currentGold += grant;
        BattleUI.Instance?.UpdateGold(currentGold);
        GlobalToastUI.Show($"获得任务金币 +{grant}");
        // #region agent log
        DebugAgentLog.Log("H9", "BattleManager.TryGrantStageQuestGold", "quest_gold_granted",
            $"{{\"grant\":{grant},\"currentGold\":{currentGold}}}");
        // #endregion
    }

    /// <summary>清怪后：宝箱 → 掉落 → 三选一 → chuansongmen</summary>
    void StartStageClearRewardSequence()
    {
        if (_rewardSequenceStarted || _stageCleared) return;
        if (CountAliveMonsters() > 0) return;
        TryGrantStageQuestGold();
        if (ShouldPlayChapter1Ending())
        {
            _rewardSequenceStarted = true;
            StartCoroutine(CoChapter1EndingThenRewards());
            return;
        }
        BeginRewardSequence();
    }

    bool ShouldPlayChapter1Ending()
    {
        if (Rules.SkipChapter1Ending) return false;
        if (!StoryProgress.TutorialDone || StoryProgress.Chapter1ChoiceDone) return false;
        if (CurrentChapter > 1) return false;
        return currentStage != null && currentStage.type == StageType.Boss;
    }

    IEnumerator CoChapter1EndingThenRewards()
    {
        bool done = false;
        Chapter1Story.PlayEnding(() => done = true);
        while (!done) yield return null;
        _rewardSequenceStarted = false;
        BeginRewardSequence();
    }

    void BeginRewardSequence()
    {
        if (_rewardSequenceStarted || _stageCleared) return;
        _rewardSequenceStarted = true;
        isInBattle = false;
        UnitsCanAct = false;

        FocusMarkSystem.Ensure()?.ResetForBattle();
        BattleUI.Instance?.EnsureBattleControls();
        BattleJoystick.Instance?.SetVisible(false);

        if (currentStage == null)
        {
            Debug.LogError("[BattleManager] 结算时 currentStage 为空");
            FinishStageAfterPortalReached();
            return;
        }

        bool isBoss = currentStage.type == StageType.Boss;
        if (isBoss)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex, atEndFlag: true);
        else
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex);

        int bonusStar = 0;
        int equipCount = GameConfig.EQUIP_CHOOSE_COUNT;
        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        HiddenLevelSystem.AddStageClearExp(ch);
        int bonusGold = 0;
        if (!_stageQuestGoldGranted)
        {
            bonusGold = StageQuestClearGold > 0
                ? StageQuestClearGold
                : BattleQuestConfig.GetClearGold(ch, currentStage.type, BattleDifficulty);
            bonusGold = Mathf.RoundToInt(bonusGold * runGoldGainMul);
        }

        if (IsGoldDungeon)
            equipCount = 0;
        else if (isBoss)
        {
            bonusStar = 2;
            equipCount += 1;
        }
        else if (currentStage.type == StageType.Elite)
            bonusStar = 1;

        // 多出的奖励件直接折金，三选一只展示 3 张
        int blacksmithLevel = TownSystem.Instance != null ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith) : 1;
        List<EquipInstance> rewards = (!IsGoldDungeon && ConfigManager.Instance != null)
            ? ConfigManager.Instance.GetRandomEquipInstances(equipCount, blacksmithLevel, bonusStar, currentStage.type)
            : new List<EquipInstance>();

        if (rewards != null && rewards.Count > 3)
        {
            for (int i = 3; i < rewards.Count; i++)
                bonusGold += (int)rewards[i].rarity * 5 * (1 + rewards[i].star);
            rewards.RemoveRange(3, rewards.Count - 3);
        }

        EnsureRewardDirector();
        StageClearRewardDirector.Instance.Begin(rewards, bonusGold, currentStage.type);
        Debug.Log($"[BattleManager] 开始宝箱结算 type={currentStage.type} bonusGold={bonusGold} equips={rewards?.Count ?? 0}");
    }

    public void NotifyChuanSongMenOpened(Transform portal)
    {
        _chuanSongMen = portal;
        _portalActive = true;
        if (portal != null)
            ExtendCameraMaxX(portal.position.x + 3f);
        // 让英雄/佣兵继续向右走向传送门
        UnitsCanAct = true;
        isInBattle = true; // Update 里检测走近传送门需要跑
        BattleJoystick.Instance?.SetVisible(true);
        if (hero != null)
        {
            hero.Face(1);
            if (hero.rb != null)
                hero.rb.velocity = new Vector2(Mathf.Max(0.4f, hero.attr != null ? hero.attr.GetAttr(AttrType.MoveSpeed) : 1.2f), 0f);
        }
    }

    void PlayPortalEnterVfxOnce(Vector3 worldPos)
    {
        if (_portalEnterVfxPlayed) return;
        _portalEnterVfxPlayed = true;
        var prefab = Resources.Load<GameObject>("VFX/other/world/传送");
        if (prefab == null) return;
        var go = Instantiate(prefab, worldPos + new Vector3(0f, 0.6f, 0f), Quaternion.identity);
        go.name = "PortalEnterVfx";
        Destroy(go, 3.5f);
    }

    /// <summary>走进 chuansongmen 后：写档 → 结算 → 选关/回城</summary>
    public void FinishStageAfterPortalReached()
    {
        if (_stageCleared) return;
        _stageCleared = true;
        isInBattle = false;
        UnitsCanAct = false;

        if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;

        PersistBattleGold();
        ChapterManager.Instance?.OnStageComplete();

        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : CurrentChapter;
        bool bossStage = currentStage != null && currentStage.type == StageType.Boss;
        AdventureLogFragments.TryDropOnStageClear(ch, bossStage);

        if (IsGoldDungeon)
        {
            ShowVictorySettlementThen(() =>
            {
                GridBackpackSystem.Instance?.ClearRunEquipment();
                MercenaryManager.Instance?.ClearAllMercs();
                MercHireSession.ClearHired();
                GameSceneManager.Instance?.ReturnToTown();
            });
            return;
        }

        bool isBoss = currentStage != null && currentStage.type == StageType.Boss;
        if (isBoss)
        {
            ShowVictorySettlementThen(() =>
            {
                UIManager.Instance?.ShowChapterClearChoice(
                    onReturnTown: () =>
                    {
                        GridBackpackSystem.Instance?.ClearRunEquipment();
                        MercenaryManager.Instance?.ClearAllMercs();
                        MercHireSession.ClearHired();
                        GameSceneManager.Instance?.ReturnToTown();
                    },
                    onNextChapter: () =>
                    {
                        int next = (ChapterManager.Instance?.currentChapter ?? 1) + 1;
                        if (next > 8) next = 8;
                        ChapterManager.Instance?.StartChapter(next);
                        if (ChapterManager.Instance?.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
                            ChapterManager.Instance.SelectStage(ChapterManager.Instance.stageMap[0]);
                    });
            });
        }
        else
        {
            ShowVictorySettlementThen(() =>
            {
                UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
            });
        }
    }

    void PersistBattleGold()
    {
        var save = SaveSystem.Instance?.Data;
        if (save == null) return;
        // 战斗内金币写回城镇：差额走 ResourceWallet，避免突破上限
        long delta = currentGold - save.totalGold;
        if (delta > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, delta, save: true, notify: false);
        else if (delta < 0)
            ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, -delta, save: true, notify: false);
        else
            SaveSystem.Instance.Save();
        currentGold = save.totalGold;
    }

    public void OnHeroDead()
    {
        isInBattle = false;
        MercenaryManager.Instance?.ClearAllMercs();
        AdventureLogAchievements.OnDied();
        // 引导关死亡：不走遗产，按撤离收尾标记教程进度，避免反复卡在引导战
        if (Rules.Active || SkipLegacyOnEvacuate)
        {
            FinishTutorialEvacuate();
            return;
        }
        TriggerLegacyFlow(isDeath: true);
    }

    public void TriggerEvacuation()
    {
        TryGrantStageQuestGoldIfQuestComplete();
        isInBattle = false;
        ClearAllMonsters();
        MercenaryManager.Instance?.ClearAllMercs();
        if (!Rules.SkipMercHireClearOnEvacuate)
            MercHireSession.ClearHired();
        AdventureLogAchievements.OnEvacuated(Rules.Active);
        // 撤离回城后打开冒险页（引导局另有收尾，不抢页签）
        if (!Rules.SkipPendingAdventureOnEvacuate)
            TownHubController.PendingOpenAdventure = true;
        // 教程也走结算界面，关闭后再回城接剧情
        if (SkipLegacyOnEvacuate || Rules.Active)
        {
            TriggerTutorialEvacuateWithSettlement();
            return;
        }
        TriggerLegacyFlow(isDeath: false);
    }

    /// <summary>引导撤离：弹出结算 → 再回城（TownAfterBattle 剧情）。</summary>
    void TriggerTutorialEvacuateWithSettlement()
    {
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.WaitingEvacuate = false;
        if (!_stageQuestGoldGranted && StageQuestClearGold > 0)
            TryGrantStageQuestGold();

        // 结算前先快照；局内装备仍清（裂缝口径）
        GridBackpackSystem.Instance?.ClearRunEquipment();
        PersistBattleGold();
        int talentGain = (int)(Mathf.Max(0, currentGold - _goldAtRunStart) / GameConfig.GOLD_PER_TALENT_POINT);
        if (talentGain > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talentGain, save: false, notify: false);
        StoryProgress.MarkTutorialBattleCleared();
        BattleStateSaver.Instance?.ClearBattleState();
        SaveSystem.Instance?.Save();

        FillSettlementSnapshot(isDeath: false, talentGain);
        AdventureLogAchievements.OnRunGoldPeak(currentGold - _goldAtRunStart);
        BattleSettlementUI.Show(RunStats, () =>
        {
            GameSceneManager.Instance?.LoadTownScene();
        });
    }

    void FinishTutorialEvacuate()
    {
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.WaitingEvacuate = false;
        if (!_stageQuestGoldGranted && StageQuestClearGold > 0)
            TryGrantStageQuestGold();
        GridBackpackSystem.Instance?.ClearRunEquipment();
        // currentGold 含城镇底金，必须走差额写回，禁止整额 Add
        PersistBattleGold();
        StoryProgress.MarkTutorialBattleCleared();
        BattleStateSaver.Instance?.ClearBattleState();
        SaveSystem.Instance?.Save();
        GameSceneManager.Instance.LoadTownScene();
    }

    /// <param name="isDeath">死亡：本局金币清零（保留进局快照）；撤离：保留本局金币。材料/附魔石均已在钱包中保留。</param>
    void TriggerLegacyFlow(bool isDeath)
    {
        if (isDeath)
            currentGold = _goldAtRunStart;

        // 裂缝口径：局内装备/武器退出清空，不带出城镇
        GridBackpackSystem.Instance?.ClearRunEquipment();

        PersistBattleGold();
        int talentGain = 0;
        if (!isDeath)
        {
            talentGain = (int)(Mathf.Max(0, currentGold - _goldAtRunStart) / GameConfig.GOLD_PER_TALENT_POINT);
            if (talentGain > 0)
                ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talentGain, save: false, notify: false);
        }
        SaveSystem.Instance?.Save();
        BattleStateSaver.Instance?.ClearBattleState();

        FillSettlementSnapshot(isDeath, talentGain);
        if (!isDeath)
            AdventureLogAchievements.OnRunGoldPeak(currentGold - _goldAtRunStart);
        TownHubController.PendingOpenAdventure = true;
        BattleSettlementUI.Show(RunStats, () =>
        {
            MercHireSession.ClearHired();
            GameSceneManager.Instance?.LoadTownScene();
        });
    }

    void FillSettlementSnapshot(bool isDeath, int talentGain, bool isVictory = false)
    {
        RunStats.IsDeath = isDeath;
        RunStats.IsVictory = isVictory && !isDeath;
        RunStats.GoldGained = isDeath ? 0 : Mathf.Max(0, (int)(currentGold - _goldAtRunStart));
        RunStats.TalentGained = talentGain;
        RunStats.EquipCount = GridBackpackSystem.Instance != null
            ? GridBackpackSystem.Instance.GetAllItemsForLegacy().Count
            : 0;
        var data = SaveSystem.Instance?.Data;
        if (data != null)
        {
            RunStats.EnchantStoneDelta = Mathf.Max(0, data.enchantStones - _enchantAtRunStart);
            RunStats.DecomposeMatDelta = Mathf.Max(0, data.decomposeMats - _matsAtRunStart);
            RunStats.Chapter = ChapterManager.Instance != null
                ? ChapterManager.Instance.currentChapter
                : CurrentChapter;
        }
        if (currentStage != null)
            RunStats.StageTitle = ChapterManager.Instance != null
                ? ChapterManager.Instance.GetCurrentStageDisplayName()
                : $"第{RunStats.Chapter}章";
        else
            RunStats.StageTitle = $"第{RunStats.Chapter}章";
        RunStats.ResolveMvp();
    }

    /// <summary>通关后弹结算，再执行后续选关/回城。</summary>
    public void ShowVictorySettlementThen(System.Action afterConfirm)
    {
        int talentGain = (int)(Mathf.Max(0, currentGold - _goldAtRunStart) / GameConfig.GOLD_PER_TALENT_POINT);
        FillSettlementSnapshot(isDeath: false, talentGain, isVictory: true);
        AdventureLogAchievements.OnRunGoldPeak(currentGold - _goldAtRunStart);
        BattleSettlementUI.Show(RunStats, afterConfirm);
    }

    public void RecordDamageDealt(float amount, bool toBoss)
    {
        if (amount <= 0f) return;
        RunStats.DamageDealt += amount;
        if (toBoss) RunStats.BossDamageDealt += amount;
    }

    public void RecordDamageTaken(float amount)
    {
        if (amount <= 0f) return;
        RunStats.DamageTaken += amount;
    }

    public void RecordCrit()
    {
        RunStats.CritCount++;
    }

    public static string GetAllyMvpKey(UnitBase u)
    {
        if (u == null) return BattleRunStats.PlayerMvpKey;
        if (u is Mercenary merc)
        {
            if (!string.IsNullOrEmpty(merc.hireId)) return merc.hireId;
            if (!string.IsNullOrEmpty(merc.mercId)) return merc.mercId;
            return "merc";
        }
        return BattleRunStats.PlayerMvpKey;
    }

    public static string GetAllyMvpDisplayName(UnitBase u)
    {
        if (u is Mercenary merc)
        {
            if (!string.IsNullOrEmpty(merc.DisplayName)) return merc.DisplayName;
            if (!string.IsNullOrEmpty(merc.hireId) && MercRosterDefs.TryGetByHireId(merc.hireId, out var def)
                && !string.IsNullOrEmpty(def.Nickname))
                return def.Nickname;
            return !string.IsNullOrEmpty(merc.mercId) ? merc.mercId : "佣兵";
        }
        var save = SaveSystem.Instance?.Data;
        return save != null && !string.IsNullOrEmpty(save.playerDisplayName)
            ? save.playerDisplayName : "冒险者";
    }

    public void RecordAllyDamage(UnitBase source, float amount)
    {
        if (source == null || !source.isAlly || amount <= 0f) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(source), GetAllyMvpDisplayName(source));
        c.damage += amount;
    }

    public void RecordAllyKill(UnitBase source)
    {
        if (source == null || !source.isAlly) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(source), GetAllyMvpDisplayName(source));
        c.kills++;
    }

    public void RecordAllyHeal(UnitBase healer, float amount)
    {
        if (healer == null || !healer.isAlly || amount <= 0f) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(healer), GetAllyMvpDisplayName(healer));
        c.healingDone += amount;
    }

    public void ClearAllMonsters()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null) continue;
            m.OnDead -= OnMonsterDead;
            if (PoolManager.Instance != null)
                PoolManager.Instance.Release(m.gameObject);
            else
                Destroy(m.gameObject);
        }
        monsters.Clear();
        _aliveMonsterCache = -1;
    }

    // ============================================================
    // 特殊关卡
    // ============================================================

    // 商人/诅咒/锻造/附魔：本版不进池，保留方法供后续开放
    [System.Obsolete("本版未开放商人关")]
    void LoadMerchantStage(StageData stage)
    {
        UIManager.Instance?.ShowToast("本版本未开放商人关");
        ChapterManager.Instance?.OnStageComplete();
        UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
    }

    [System.Obsolete("本版未开放诅咒关")]
    void LoadCurseStage(StageData stage)
    {
        UIManager.Instance?.ShowToast("本版本未开放诅咒关");
        ChapterManager.Instance?.OnStageComplete();
        UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
    }

    /// <summary>
    /// 锻造关：弹锻造窗（发强化材料），关掉后继续推关。
    /// </summary>
    void LoadForgeStage()
    {
        CraftStagePopupUI.ShowForge(() =>
        {
            ChapterManager.Instance?.OnStageComplete();
            UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
        });
    }

    void LoadEnchantStage()
    {
        CraftStagePopupUI.ShowEnchant(() =>
        {
            ChapterManager.Instance?.OnStageComplete();
            UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
        });
    }

    void LoadRestStage()
    {
        // 弹窗里已经回过 50% 血；关掉后继续推关
        UIManager.Instance.ShowRestUI(() =>
        {
            ChapterManager.Instance.OnStageComplete();
            UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
        }, null);
    }

    List<CurseBuff> GenerateCurseOptions()
    {
        return new List<CurseBuff>
        {
            new CurseBuff { buffName = "嗜血：攻击+30%，生命-15%", buff = new AttrBonusData { attrType = AttrType.Attack, value = 0.3f, isPercent = true }, debuff = new AttrBonusData { attrType = AttrType.MaxHp, value = -0.15f, isPercent = true } },
            new CurseBuff { buffName = "疾风：攻速+40%，攻击-20%", buff = new AttrBonusData { attrType = AttrType.AttackSpeed, value = 0.4f, isPercent = true }, debuff = new AttrBonusData { attrType = AttrType.Attack, value = -0.2f, isPercent = true } },
            new CurseBuff { buffName = "坚壁：生命+50%，移速-30%", buff = new AttrBonusData { attrType = AttrType.MaxHp, value = 0.5f, isPercent = true }, debuff = new AttrBonusData { attrType = AttrType.MoveSpeed, value = -0.3f, isPercent = true } }
        };
    }
}