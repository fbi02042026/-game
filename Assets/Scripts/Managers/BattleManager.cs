using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 战斗管理器（v2 波次刷怪版）
/// 玩家向右走，每走过一段距离触发一波怪物刷新
/// 所有波次怪物清完后到达终点才算通关
/// </summary>
public class BattleManager : Singleton<BattleManager>
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
    public bool isInBattle = false;
    public bool isAutoBattle = false;
    public List<AttrBonusData> tempBuffs = new List<AttrBonusData>();
    /// <summary>开战过场结束后才允许单位行动</summary>
    public bool UnitsCanAct { get; set; } = true;
    public bool IsTutorialRun { get; private set; }
    public bool SuppressStageClear { get; set; }
    public bool SkipLegacyOnEvacuate { get; set; }
    /// <summary>本局金币获得倍率（剧情选择等）</summary>
    public float runGoldGainMul = 1f;
    /// <summary>本局怪物攻速倍率（剧情选择等）</summary>
    public float runMonsterAtkSpeedMul = 1f;
    /// <summary>正在走向 chuansongmen，放宽屏幕钳制</summary>
    public bool PortalWalkMode => _portalActive && !_stageCleared;
    /// <summary>佣兵相对主角身后间距（世界单位）</summary>
    public const float MERC_BEHIND_SPACING = 0.42f;
    /// <summary>开场从站位左侧多远走进来</summary>
    const float PARTY_ENTER_FROM = 2.5f;

    float _battleStartTime;
    bool _firstWaveSpawned;
    Coroutine _firstWaveSpawnCo;

    public int CurrentChapter => ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
    public int BattleDifficulty { get; private set; }
    public bool IsGoldDungeon { get; private set; }
    public float DifficultyStatScale => GameConfig.GetDifficultyStatScale(BattleDifficulty);
    public float DifficultyGoldMul => GameConfig.GetDifficultyGoldMul(BattleDifficulty);

    // === 波次系统 ===
    [System.Serializable]
    public class WaveData
    {
        public float triggerX;           // 无刷怪点时的兜底触发 X
        public Transform spawnAnchor;    // 场景 MonsterSpawn_*（世界坐标，不跟 Ground 逻辑）
        public int monsterCount;
        public bool isBossWave;
        public bool spawned;
        public int aliveCount;
    }
    private List<WaveData> _waves = new List<WaveData>();
    private int _totalWaves = 0;
    private bool _allWavesSpawned = false;
    private bool _stageCleared = false;
    private int _totalMonstersSpawnedThisStage = 0;
    /// <summary>当前进行中的波次下标；-1=尚未开刷</summary>
    private int _activeWaveIndex = -1;
    /// <summary>清波后的下一波倒计时是否开启</summary>
    private bool _waveCountdownActive;
    private float _nextWaveCountdown;
    /// <summary>连杀计数</summary>
    private int _killCombo;
    private float _lastKillTime;

    // === 技能能量 ===
    /// <summary>玩家技能能量 0~1（杀怪累积+时间累积，满了可以释放）</summary>
    public float playerSkillEnergy = 0f;
    public const float MAX_SKILL_ENERGY = 1f;
    /// <summary>佣兵技能能量（最多2槽）</summary>
    readonly float[] mercSkillEnergy = new float[2];

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
    private bool _didUpdateForceSpawn;

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

    void ExtendCameraMaxX(float worldX)
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
    public const float ENERGY_PER_KILL = 0.2f;     // 每杀一个怪+20%能量
    public const float ENERGY_PER_SECOND = 0.015f;  // 每秒+1.5%能量（约67秒从0到满）

    protected override void Awake()
    {
        base.Awake();
    }

    public void StartNewRun()
    {
        Debug.Log("[BattleManager] ===== StartNewRun 开始 =====");
        StopBattleSpawnCoroutines();
        currentGold = SaveSystem.Instance?.Data?.totalGold ?? 0;
        _goldAtRunStart = currentGold;
        tempBuffs.Clear();
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        _chuanSongMen = null;
        UnitsCanAct = false;
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

        IsTutorialRun = StoryProgress.ShouldStartTutorialBattle();
        if (IsTutorialRun)
            StoryProgress.ConsumeTutorialBattleFlag();
        SuppressStageClear = IsTutorialRun;
        SkipLegacyOnEvacuate = IsTutorialRun;
        runGoldGainMul = 1f;
        runMonsterAtkSpeedMul = 1f;
        ApplyChapter1RunModifiersFromSave();
        if (IsTutorialRun)
            Debug.Log("[BattleManager] 新手引导战斗：短关+强制撤离");

        if (!GameConfig.SOLO_PLAYER_BATTLE && !IsTutorialRun)
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
        if (!IsTutorialRun)
        {
            StartCoroutine(CoPreLevelThenLoad(first));
            return;
        }
        LoadStage(first);
    }

    IEnumerator CoPreLevelThenLoad(StageData first)
    {
        bool done = false;
        if (PreLevelSystem.Instance != null)
        {
            PreLevelSystem.Instance.StartPreLevelSelection();
            PreLevelEquipUI.Show(PreLevelSystem.Instance, () => done = true);
            while (!done)
                yield return null;
        }
        LoadStage(first);
    }

    void EnsureTestMercenaries()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.townLevel == null) data.townLevel = new TownLevel();

        // 仅空档补一只测试弓手；不删玩家已有佣兵、不强改酒馆等级
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
        }

        if (data.permanentMercs.Count == 0)
        {
            data.permanentMercs.Add(new MercenaryData { mercId = "gongshou101", favorLevel = 1, level = 1 });
            Debug.Log("[BattleManager] 空档自动添加测试佣兵: 弓手101");
        }

        if (data.townLevel.tavern < 1)
            data.townLevel.tavern = 1;
    }

    void SpawnMercenaries()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null) return;

        var ids = mm.GetActiveMercIds();
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : hero.transform.position;
        basePos.y = UnitBase.GROUND_Y;
        basePos.z = 0f;

        for (int i = 0; i < ids.Count; i++)
        {
            Vector3 pos = basePos + new Vector3(-0.85f * (i + 1), 0, 0);
            var merc = mm.SpawnMercenary(ids[i], pos, 1);
            if (merc != null)
            {
                allyUnits.Add(merc);
                merc.OnDead += OnMercenaryDead;
            }
        }
        Debug.Log($"[BattleManager] 已生成佣兵 {ids.Count} 个 (tavern槽={mm.GetMaxMercSlots()})");
    }

    public void OnMercenaryDead(UnitBase merc)
    {
        allyUnits.Remove(merc);
    }

    void ApplyChapter1RunModifiersFromSave()
    {
        if (IsTutorialRun) return;
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

    void PrepareTutorialWaves()
    {
        _waves.Clear();
        _waves.Add(new WaveData
        {
            triggerX = GetStageStartX() + GameConfig.MONSTER_ENGAGE_OFFSET,
            spawnAnchor = null,
            monsterCount = 2,
            isBossWave = false,
            spawned = false,
            aliveCount = 0
        });
        _totalWaves = 1;
        _allWavesSpawned = false;
        _activeWaveIndex = -1;
        _firstWaveSpawned = true; // 教程首波由 TutorialDirector 控，关掉 Update 硬刷
        SuppressStageClear = true;
        SkipLegacyOnEvacuate = true;
        Debug.Log("[BattleManager] 引导关：1 波 2 怪");
    }

    public void QueueTutorialWave(int count)
    {
        // 跳过未刷出的旧波次（PrepareTutorialWaves 遗留），避免卡在 index 0
        if (_waves != null)
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                if (_waves[i] != null && !_waves[i].spawned)
                    _waves[i].spawned = true;
            }
        }

        int n = Mathf.Max(1, count);
        var wave = new WaveData
        {
            triggerX = (hero != null ? UnitBase.GetCombatX(hero) : GetStageStartX()) + GameConfig.MONSTER_ENGAGE_OFFSET,
            spawnAnchor = null,
            monsterCount = n,
            isBossWave = false,
            spawned = false,
            aliveCount = 0
        };
        _waves.Add(wave);
        _totalWaves = _waves.Count;
        _allWavesSpawned = false;
        _activeWaveIndex = -1;
        _waveCountdownActive = false;
        _didUpdateForceSpawn = false;
        SuppressStageClear = true;

        int waveIdx = _waves.Count - 1;
        int spawnedBefore = wave.aliveCount;
        int aliveBefore = CountAliveMonsters();
        TrySpawnTutorialWave(wave, waveIdx, spawnedBefore, aliveBefore);

        if (CountAliveMonsters() == 0)
        {
            EmergencySpawnVisibleMonsters(Mathf.Min(n, 6));
            if (CountAliveMonsters() > 0)
            {
                wave.spawned = true;
                _activeWaveIndex = waveIdx;
            }
        }
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
        }
        else
        {
            wave.spawned = false;
            Debug.LogWarning($"[BattleManager] 教程波 {waveIdx + 1} 常规刷怪未出怪，将走 Emergency");
        }
    }

    public Mercenary SpawnTutorialMerc(string mercId, float hpRatio)
    {
        return SpawnTutorialMercAt(mercId, hpRatio, 7.5f, stunned: true);
    }

    /// <summary>
    /// 引导救援：老盾刷在玩家前方固定距离，原地眩晕；周围刷怪围殴他。
    /// </summary>
    public Mercenary SpawnTutorialMercAt(string mercId, float hpRatio, float aheadDist, bool stunned)
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || hero == null) return null;

        float heroX = UnitBase.GetCombatX(hero);
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;
        Vector3 pos = new Vector3(heroX + aheadDist, UnitBase.GROUND_Y, z);
        var merc = mm.SpawnMercenary(string.IsNullOrEmpty(mercId) ? "dunbing102" : mercId, pos, 1);
        if (merc == null) return null;
        if (!allyUnits.Contains(merc))
            allyUnits.Add(merc);
        merc.OnDead += OnMercenaryDead;
        float maxHp = merc.attr != null ? merc.attr.GetAttr(AttrType.MaxHp) : 100f;
        merc.currentHp = Mathf.Max(1f, maxHp * Mathf.Clamp01(hpRatio));
        merc.Face(-1);
        if (stunned)
            merc.SetTutorialStunned(true);
        ExtendCameraMaxX(pos.x + 6f);
        return merc;
    }

    /// <summary>在受害者周围刷怪并强制锁定打他（引导围殴）。</summary>
    public void SpawnTutorialAmbushAround(UnitBase victim, int count)
    {
        if (victim == null) return;
        EnsureMonsterPrefabReady();
        float vx = UnitBase.GetCombatX(victim);
        float z = unitRoot != null ? unitRoot.position.z : victim.transform.position.z;
        int n = Mathf.Max(1, count);

        // 记入一波，便于任务计数 / WaitFieldClear
        var wave = new WaveData
        {
            triggerX = vx,
            spawnAnchor = null,
            monsterCount = n,
            isBossWave = false,
            spawned = true,
            aliveCount = 0
        };
        if (_waves == null) _waves = new List<WaveData>();
        // 跳过未刷旧波
        for (int i = 0; i < _waves.Count; i++)
        {
            if (_waves[i] != null && !_waves[i].spawned)
                _waves[i].spawned = true;
        }
        _waves.Add(wave);
        _totalWaves = _waves.Count;
        _allWavesSpawned = false;
        _activeWaveIndex = _waves.Count - 1;
        SuppressStageClear = true;

        float[] offsets = { -1.1f, 1.15f, 0.35f, -0.55f, 1.7f };
        for (int i = 0; i < n; i++)
        {
            float ox = offsets[i % offsets.Length];
            Vector3 pos = new Vector3(vx + ox, UnitBase.GROUND_Y, z);
            Monster m = SpawnAmbushMonsterAt(pos);
            if (m == null) continue;
            m.SetForcedTarget(victim);
            wave.aliveCount++;
        }

        if (wave.aliveCount == 0)
        {
            EmergencySpawnVisibleMonsters(n);
            RetargetAllMonsters(victim);
        }
    }

    Monster SpawnAmbushMonsterAt(Vector3 pos)
    {
        EnsureMonsterPrefabReady();
        GameObject go = null;
        if (PoolManager.Instance != null)
        {
            go = PoolManager.Instance.Get("Monster", pos, Quaternion.identity);
            if (go == null && PoolManager.Instance._monsterPrefab != null)
            {
                go = Object.Instantiate(PoolManager.Instance._monsterPrefab, pos, Quaternion.identity);
                PoolManager.Instance.RegisterExternal(go, "Monster");
            }
        }
        if (go == null) return null;

        go.SetActive(true);
        if (unitRoot != null) go.transform.SetParent(unitRoot, true);
        GameConfig.SetWorldPosition(go, pos);

        Monster monster = go.GetComponent<Monster>();
        if (monster == null) monster = go.AddComponent<Monster>();

        var cfg = ScriptableObject.CreateInstance<MonsterConfig>();
        cfg.id = "ambush";
        cfg.baseHp = 40f;
        cfg.baseAttack = 4f;
        cfg.attackRange = GameConfig.RANGE_PX_SWORD;
        cfg.baseAttackSpeed = 0.9f;
        cfg.spriteScale = 1f;
        cfg.spriteIndex = 1;
        cfg.baseGoldDrop = 1;
        cfg.expDrop = 1;
        try
        {
            monster.Init(cfg, 0, CurrentChapter, 1f, 1);
        }
        catch
        {
            monster.currentHp = 40f;
            monster.isAlly = false;
        }
        ApplyTutorialMonsterTuning(monster);
        ForceEnableMonsterRenderers(monster.transform);
        monster.OnDead += OnMonsterDead;
        monsters.Add(monster);
        _totalMonstersSpawnedThisStage++;
        return monster;
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

    void ApplyTutorialMonsterTuning(Monster monster)
    {
        if (!IsTutorialRun || monster == null || monster.attr == null) return;
        // 引导怪总血量 8–12，玩家约 2 点伤害，3–4 下击杀
        float hp = Random.Range(8, 13);
        monster.attr.SetAttr(AttrType.MaxHp, hp);
        monster.currentHp = hp;
    }

    // ============================================================
    // 波次生成
    // ============================================================

    public void LoadStage(StageData stage)
    {
        currentStage = stage;
        ClearAllMonsters();
        _waves.Clear();
        _totalWaves = 0;
        _allWavesSpawned = false;
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        _chuanSongMen = null;
        _totalMonstersSpawnedThisStage = 0;
        _didUpdateForceSpawn = false;
        playerSkillEnergy = 0f;
        mercSkillEnergy[0] = 0f;
        mercSkillEnergy[1] = 0f;

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

        // 触发章节背景切换
        SwitchBattleBackground(CurrentChapter);

        // 按关卡类型切 BGM（Loading 中会记 pending，结束后再播）
        GameBgm.PlayForStage(stage.type);

        switch (stage.type)
        {
            case StageType.Normal:
                SetupNormalWaves(stage.stageIndex);
                break;
            case StageType.Elite:
                SetupEliteWaves(stage.stageIndex);
                break;
            case StageType.Boss:
                SetupBossWave(stage.stageIndex);
                break;
            case StageType.Merchant:
            case StageType.Curse:
                // 软著版：商人/诅咒未开放，回退为普通战斗关，避免空壳 UI 假过关
                UIManager.Instance?.ShowToast("本版本未开放该关卡类型，已改为普通战斗");
                stage.type = StageType.Normal;
                SetupNormalWaves(stage.stageIndex);
                break;
            case StageType.Enchant:
                isInBattle = false;
                LoadEnchantStage();
                break;
            case StageType.Rest:
                isInBattle = false;
                LoadRestStage();
                break;
            case StageType.Forge:
                isInBattle = false;
                LoadForgeStage();
                break;
        }

        ResetWaveProgress();

        BattleUI.Instance?.UpdateCharacterSlots();
        BattleUI.Instance?.UpdateSkillEnergy(0, 0f);
        BattleUI.Instance?.RefreshBattleHud();
        // 进关立刻把进度条 Marker 挂到当前 Node
        if (currentStage != null)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex);
        int monsterGoal = 0;
        foreach (var w in _waves) monsterGoal += w.monsterCount;
        if (monsterGoal <= 0) monsterGoal = 3;
        BattleUI.Instance?.UpdateQuest("击败所有敌人", 0, monsterGoal);

        if (isInBattle)
        {
            if (IsTutorialRun)
                PrepareTutorialWaves();
            PlacePartyAt(startX, z);
            UnitsCanAct = true;
            _battleStartTime = Time.unscaledTime;
            EnsureMonsterPrefabReady();

            Debug.Log($"[BattleManager] LoadStage 战斗关 type={stage.type} waves={_waves?.Count ?? 0} heroX={UnitBase.GetCombatX(hero):F2}");

            if (Instance != this)
                Debug.LogError("[BattleManager] 当前实例不是单例实例！说明存在重复 BattleManager，Update/协程不会在本实例上运行");
            if (!gameObject.activeInHierarchy)
                Debug.LogError($"[BattleManager] 宿主 {gameObject.name} 未激活，协程与 Update 不会执行");

            StopBattleSpawnCoroutines();
            StartCoroutine("BattleStartSequenceCoroutine");
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
    void ForceSpawnFirstWaveNow()
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
    void EmergencySpawnVisibleMonsters(int count)
    {
        if (hero == null) return;
        float hx = UnitBase.GetCombatX(hero);
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;

        for (int i = 0; i < count; i++)
        {
            float x = GetMonsterEngageBaseX(hx) + i * 0.55f;
            Vector3 pos = new Vector3(x, UnitBase.GROUND_Y, z);

            GameObject go = null;
            EnsureMonsterPrefabReady();
            if (PoolManager.Instance != null)
            {
                go = PoolManager.Instance.Get("Monster", pos, Quaternion.identity);
                if (go == null && PoolManager.Instance._monsterPrefab != null)
                {
                    go = Object.Instantiate(PoolManager.Instance._monsterPrefab, pos, Quaternion.identity);
                    PoolManager.Instance.RegisterExternal(go, "Monster");
                }
            }
            if (go == null)
            {
                go = new GameObject("EmergencyMonster_" + i);
                var sr = go.AddComponent<SpriteRenderer>();
                var tex = Texture2D.whiteTexture;
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 8f);
                sr.color = Color.red;
                sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                sr.sortingOrder = GameConfig.SORT_UNIT;
                go.transform.localScale = Vector3.one * 2f;
            }

            go.SetActive(true);
            if (unitRoot != null) go.transform.SetParent(unitRoot, true);
            GameConfig.SetWorldPosition(go, pos);

            Monster monster = go.GetComponent<Monster>();
            if (monster == null) monster = go.AddComponent<Monster>();

            var cfg = ScriptableObject.CreateInstance<MonsterConfig>();
            cfg.id = "emergency_" + i;
            cfg.baseHp = 40f;
            cfg.baseAttack = 5f;
            cfg.attackRange = GameConfig.RANGE_PX_SWORD;
            cfg.baseAttackSpeed = 0.8f;
            cfg.spriteScale = 1f;
            cfg.spriteIndex = 1;
            cfg.baseGoldDrop = 1;
            cfg.expDrop = 1;

            try
            {
                monster.Init(cfg, 0, CurrentChapter, 1f, 1);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleManager] Emergency Init 失败，手工赋值HP: {e.Message}");
                monster.currentHp = 40f;
                monster.isAlly = false;
                GameConfig.ApplyUnitSorting(monster.transform);
            }

            if (monster.currentHp <= 0f) monster.currentHp = 40f;
            ApplyTutorialMonsterTuning(monster);
            ForceEnableMonsterRenderers(monster.transform);
            monster.transform.localScale = Vector3.one * Mathf.Max(monster.transform.localScale.x, GameConfig.MONSTER_BASE_SCALE);
            monster.OnDead += OnMonsterDead;
            monsters.Add(monster);
            _totalMonstersSpawnedThisStage++;
            Debug.Log($"[BattleManager] Emergency 怪已生成 name={go.name} pos={go.transform.position} hp={monster.currentHp} scale={go.transform.localScale}");
        }
    }

    void ResetWaveProgress()
    {
        _activeWaveIndex = -1;
        _waveCountdownActive = false;
        _nextWaveCountdown = 0f;
        _allWavesSpawned = false;
        _killCombo = 0;
        _lastKillTime = 0f;
        _firstWaveSpawned = false;
        _didUpdateForceSpawn = false;
        _battleStartTime = Time.unscaledTime;
        BattleSideHud.Instance?.ResetCombo();
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
    }

    /// <summary>波次为空时强制补一波（兜底）</summary>
    void EnsureAtLeastOneWave()
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

    void ScheduleFirstWaveSpawn()
    {
        if (_firstWaveSpawned || _firstWaveSpawnCo != null) return;
        _firstWaveSpawnCo = StartCoroutine(FirstWaveSpawnAfterDelay());
    }

    IEnumerator FirstWaveSpawnAfterDelay()
    {
        yield return new WaitForSecondsRealtime(GameConfig.FIRST_WAVE_SPAWN_DELAY);
        _firstWaveSpawnCo = null;
        TrySpawnFirstWaveOnce();
    }

    /// <summary>首波刷怪（含兜底）；成功才标记 _firstWaveSpawned</summary>
    void TrySpawnFirstWaveOnce()
    {
        if (!isInBattle || _stageCleared) return;
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

    /// <summary>怪物交战 X：在英雄前方，且尽量落在当前镜头内可见</summary>
    float GetMonsterEngageBaseX(float heroCombatX)
    {
        float prefer = heroCombatX + GameConfig.MONSTER_ENGAGE_OFFSET;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return prefer;

        float halfW = cam.orthographicSize * cam.aspect;
        float camRight = cam.transform.position.x + halfW;
        // 贴在镜头右缘内侧，避免刷在屏外像「没怪」
        float visible = camRight - 1.2f;
        return Mathf.Clamp(prefer, heroCombatX + 2.0f, visible);
    }

    /// <summary>硬性保险：过场/协程被停也能刷出第一波（独立协程，不被 StopCoroutine(string) 误伤）</summary>
    IEnumerator CoFirstWaveHardFallback()
    {
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

    /// <summary>黑屏章节名 → 队伍从屏外走进来（无传送特效）</summary>
    IEnumerator BattleStartSequenceCoroutine()
    {
        UnitsCanAct = false;

        float startX = GetStageStartX();
        float z = hero != null ? hero.transform.position.z
            : (unitRoot != null ? unitRoot.position.z : 0f);
        // 直接站到开战位，立刻刷怪到镜头内（不再先躲屏外再等黑屏）
        PlacePartyAt(startX, z);

        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.offset = new Vector2(GameConfig.CAMERA_FOLLOW_OFFSET_X, 0f);
            if (hero != null) follow.SetTarget(hero.transform);
        }
        if (Camera.main != null && hero != null)
        {
            Vector3 cp = Camera.main.transform.position;
            cp.x = hero.transform.position.x + GameConfig.CAMERA_FOLLOW_OFFSET_X;
            Camera.main.transform.position = cp;
        }

        ExtendCameraMaxX(startX + 80f);
        UnitsCanAct = true;
        ScheduleFirstWaveSpawn();

        string title = GameConfig.GetChapterTitleText(CurrentChapter);
        string body = null;
        if (IsTutorialRun)
        {
            title = "森林区域，第一层";
            body = "阳光还能照进来，怪物也不算太强。\n正好适合一个新人进去摸摸路。";
        }
        else if (CurrentChapter <= 1 && StoryProgress.TutorialDone && !StoryProgress.Chapter1ChoiceDone)
        {
            body = "新人任务开始。不要想太多。";
        }
        var splash = ChapterSplashOverlay.Show(title, body);
        float need = ChapterSplashOverlay.HoldSeconds + ChapterSplashOverlay.FadeSeconds + 0.5f;
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

        var parallax = FindObjectOfType<ParallaxBackground>();
        if (parallax != null) parallax.ResetHeroOrigin();

        UnitsCanAct = true;
        ExtendCameraMaxX(GetStageStartX() + 80f);

        if (monsters == null || CountAliveMonsters() == 0)
            TrySpawnFirstWaveOnce();

        if (CountAliveMonsters() == 0)
        {
            Debug.LogError("[BattleManager] 过场后仍无怪 → EmergencySpawnVisibleMonsters");
            EmergencySpawnVisibleMonsters(Mathf.Min(3, GameConfig.WAVE_MONSTER_MAX));
            if (CountAliveMonsters() > 0)
                _firstWaveSpawned = true;
        }

        if (hero != null && monsters.Count > 0)
        {
            var m0 = monsters[0];
            float hx = UnitBase.GetCombatX(hero);
            float mx = UnitBase.GetCombatX(m0);
            Debug.Log($"[BattleManager] 开战完成 monsters={monsters.Count} heroX={hx:F2} mon0={m0?.name} monX={mx:F2} dist={Mathf.Abs(hx - mx):F2} monHp={m0?.currentHp:F0} scale={m0?.transform.localScale}");
        }
        else
            Debug.LogError($"[BattleManager] 开战完成仍无怪 monsters={monsters.Count} waves={_waves?.Count ?? 0}");

        if (IsTutorialRun)
            TutorialDirector.Instance?.NotifyBattleSplashFinished();
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
            float mx = heroX - MERC_BEHIND_SPACING * (i + 1);
            GameConfig.SetWorldPosition(m.gameObject, new Vector3(mx, UnitBase.GROUND_Y, z));
            m.Face(1);
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
    float GetStageStartX()
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
    void SpawnNextPendingWave()
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
        if (!IsTutorialRun && CountAliveMonsters() > 0 && _activeWaveIndex >= 0)
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

    /// <summary>清完当前波后开启下一波倒计时</summary>
    void BeginNextWaveCountdown()
    {
        if (_allWavesSpawned || _portalActive || _stageCleared) return;
        if (FindNextUnspawnedWaveIndex() < 0)
        {
            _allWavesSpawned = true;
            StopWaveCountdown();
            return;
        }
        if (CountAliveMonsters() > 0) return;

        _waveCountdownActive = true;
        _nextWaveCountdown = GameConfig.GetWaveSpawnInterval();
        BattleSideHud.Instance?.SetWaveCountdown(true, _nextWaveCountdown, true);
        GamePerf.Log($"[BattleManager] 下一波倒计时 {_nextWaveCountdown:F1}s（可点击加速）");
    }

    void StopWaveCountdown()
    {
        _waveCountdownActive = false;
        _nextWaveCountdown = 0f;
        BattleSideHud.Instance?.SetWaveCountdown(false, 0f, false);
    }

    void TickWaveCountdown()
    {
        if (!_waveCountdownActive || !UnitsCanAct || _stageCleared || _portalActive)
            return;

        if (CountAliveMonsters() > 0)
        {
            StopWaveCountdown();
            return;
        }

        _nextWaveCountdown -= Time.deltaTime;
        bool canSkip = _nextWaveCountdown > 0.05f;
        BattleSideHud.Instance?.SetWaveCountdown(true, Mathf.Max(0f, _nextWaveCountdown), canSkip);

        if (_nextWaveCountdown <= 0f)
        {
            StopWaveCountdown();
            SpawnNextPendingWave();
        }
    }

    /// <summary>点击倒计时：立刻出兵，剩余时间换金币</summary>
    public bool TrySkipToNextWave()
    {
        if (!isInBattle || _stageCleared || _portalActive) return false;
        if (!_waveCountdownActive || _nextWaveCountdown <= 0.05f) return false;
        if (CountAliveMonsters() > 0) return false;
        if (FindNextUnspawnedWaveIndex() < 0) return false;

        int bonus = Mathf.Max(1, Mathf.CeilToInt(_nextWaveCountdown * GameConfig.WAVE_SKIP_GOLD_PER_SEC));
        currentGold += bonus;
        BattleUI.Instance?.UpdateGold(currentGold);
        UIManager.Instance?.ShowToast($"加速出兵 +{bonus} 金");
        GamePerf.Log($"[BattleManager] 加速下一波 leftover={_nextWaveCountdown:F1}s → +{bonus}金");

        StopWaveCountdown();
        SpawnNextPendingWave();
        return true;
    }

    /// <summary>兼容旧调用名</summary>
    void TrySpawnWaveByProgress() => SpawnNextPendingWave();
    void TrySpawnWavesApproachingScreen() => SpawnNextPendingWave();

    int FindNextUnspawnedWaveIndex()
    {
        if (_waves == null) return -1;
        for (int i = 0; i < _waves.Count; i++)
            if (_waves[i] != null && !_waves[i].spawned)
                return i;
        return -1;
    }

    int _aliveMonsterCache = -1;
    int _aliveMonsterCacheFrame = -1;

    int CountAliveMonsters()
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

    void EnsureMonsterPrefabReady()
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

    /// <summary>
    /// 普通关：公式随机总怪数 → 分波 → 怪朝玩家前进。
    /// </summary>
    void SetupNormalWaves(int stageIdx)
    {
        BuildCombatWaves(stageIdx, elite: false);
    }

    /// <summary>
    /// 精英关：同随机总量；属性倍率在 SpawnWave 里乘 ELITE_SCALE。
    /// </summary>
    void SetupEliteWaves(int stageIdx)
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
        int waveCount = GameConfig.GetSuggestedWaveCount(total, usable.Count);
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
    void SetupBossWave(int stageIdx)
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

    // ============================================================
    // Update
    // ============================================================

    void Update()
    {
        if (!isInBattle || _stageCleared) return;

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
            TickWaveCountdown();

        // 连杀超时清零显示
        if (_killCombo > 0 && Time.time - _lastKillTime > GameConfig.COMBO_WINDOW)
        {
            _killCombo = 0;
            BattleSideHud.Instance?.ResetCombo();
        }

        // 首波已刷完且场上清空：由倒计时推进下一波（不在此 Emergency）
        if (UnitsCanAct && !_stageCleared && !_portalActive
            && _firstWaveSpawned
            && !_waveCountdownActive
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

        // 所有波次已刷完且场上无怪 → 宝箱结算（不再直接开 EndPoint）
        if (_allWavesSpawned && monsters.Count == 0 && _totalMonstersSpawnedThisStage > 0
            && !_portalActive && !_stageCleared && !_rewardSequenceStarted
            && !SuppressStageClear)
        {
            StartStageClearRewardSequence();
        }

        // 通关：chuansongmen 已开且玩家走到传送门
        if (_portalActive && hero != null && !hero.isDead && !_stageCleared && _chuanSongMen != null)
        {
            if (hero.transform.position.x >= _chuanSongMen.position.x - 0.6f)
                FinishStageAfterPortalReached();
        }

        // 跑图时镜头随英雄缓慢放宽
        if (hero != null)
            ExtendCameraMaxX(UnitBase.GetCombatX(hero) + (_portalActive ? 6f : 12f));

        // 传送门开启后不再把英雄卡在镜头右缘，允许走向 chuansongmen
        if (!_portalActive)
            ClampHeroInCamera();

        // 更新技能能量（渐变 + 时间累积）
        if (hero != null && !hero.isDead)
        {
            playerSkillEnergy = Mathf.Min(MAX_SKILL_ENERGY, playerSkillEnergy + ENERGY_PER_SECOND * Time.deltaTime);
            if (BattleUI.Instance != null)
                BattleUI.Instance.UpdateSkillEnergy(0, playerSkillEnergy);
        }

        if (!GameConfig.SOLO_PLAYER_BATTLE)
        {
            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercSkillEnergy.Length; i++)
                {
                    if (i >= mercs.Count || mercs[i] == null || mercs[i].isDead) continue;
                    mercSkillEnergy[i] = Mathf.Min(MAX_SKILL_ENERGY, mercSkillEnergy[i] + ENERGY_PER_SECOND * 0.85f * Time.deltaTime);
                    BattleUI.Instance?.UpdateSkillEnergy(i + 1, mercSkillEnergy[i]);
                }
            }
        }
    }

    void ClampHeroInCamera()
    {
        if (hero == null) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float halfW = cam.orthographicSize * Mathf.Max(0.2f, cam.aspect);
        float maxHeroX = cam.transform.position.x + halfW - 0.35f;
        float hx = hero.transform.position.x;
        if (hx > maxHeroX)
        {
            Vector3 p = hero.transform.position;
            p.x = maxHeroX;
            GameConfig.SetWorldPosition(hero.gameObject, p);
            if (hero.rb != null) hero.rb.velocity = new Vector2(Mathf.Min(hero.rb.velocity.x, 0f), hero.rb.velocity.y);
        }
    }

    // ============================================================
    // 刷怪
    // ============================================================

    /// <summary>单波内交替近战/远程，同波搭配刷出</summary>
    int PickWaveSpriteIndex(System.Collections.Generic.List<int> availableSprites, int stageIdx,
        System.Collections.Generic.List<MonsterConfig> pool, int slotIndex)
    {
        if (availableSprites == null || availableSprites.Count == 0) return 1;

        bool wantRanged = slotIndex % 2 == 1;
        int chapter = CurrentChapter;
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

        if (slotIndex == 0 && filtered.Contains(1))
            return 1;

        return ConfigManager.Instance.PickWeightedSpriteIndex(filtered, stageIdx);
    }

    void SpawnWave(WaveData wave, int waveIndex = -1)
    {
        if (waveIndex < 0 && _waves != null)
            waveIndex = _waves.IndexOf(wave);
        if (waveIndex < 0) waveIndex = 0;

        int stageIdx = currentStage != null ? currentStage.stageIndex : 0;
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

        for (int i = 0; i < wave.monsterCount; i++)
        {
            MonsterConfig template;
            int spriteIndexOverride;

            if (wave.isBossWave)
            {
                template = pool.Find(m => m.isBoss) ?? pool[0];
                spriteIndexOverride = availableSprites.Count > 0 ? availableSprites[0] : GameConfig.BOSS_SPRITE_START;
            }
            else
            {
                spriteIndexOverride = PickWaveSpriteIndex(availableSprites, stageIdx, pool, i);

                template = pool.Find(m => !m.isBoss && m.spriteIndex == spriteIndexOverride);
                if (template == null)
                {
                    template = pool.Where(m => !m.isBoss).OrderBy(m => m.spriteIndex).FirstOrDefault() ?? pool[0];
                    spriteIndexOverride = Mathf.Max(1, template.spriteIndex > 0 ? template.spriteIndex : 1);
                }
            }

            float monsterScale = waveScaleMultiplier;
            if (template.isBoss && !wave.isBossWave)
                monsterScale = GameConfig.BOSS_SCALE_MULTIPLIER;

            float spawnY = UnitBase.GROUND_Y;
            float spawnX = engageBaseX + i * 0.4f;
            if (wave.spawnAnchor != null)
                spawnZ = wave.spawnAnchor.position.z;

            Vector3 engagePos = new Vector3(spawnX, spawnY, spawnZ);
            float enterDist = GameConfig.MONSTER_ENTER_DISTANCE;
            Vector3 enterFrom = new Vector3(spawnX + enterDist, spawnY, spawnZ);

            Monster m = SpawnMonster(template, stageIdx, enterFrom, monsterScale, spriteIndexOverride);
            if (m != null)
            {
                ForceEnableMonsterRenderers(m.transform);
                m.BeginMapEnter(engagePos, GameConfig.MONSTER_ENTER_SPEED);
                if (isElite && !wave.isBossWave)
                {
                    m.currentHp *= 1.5f;
                    m.attr.AddAttr(AttrType.Attack, 0.5f, true);
                }
                wave.aliveCount++;
            }
        }

        GamePerf.Log($"[BattleManager] 波次{waveIndex + 1} 刷新{wave.monsterCount}只 @x={engageBaseX:F1} anchor={(wave.spawnAnchor != null ? wave.spawnAnchor.name : "null")}");
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

        for (int i = 0; i < wave.monsterCount; i++)
        {
            float spawnY = UnitBase.GROUND_Y;
            float spawnZ = unitRoot != null ? unitRoot.position.z : 0f;
            float spawnX = engageBaseX + i * 0.35f;
            Vector3 pos = new Vector3(spawnX + GameConfig.MONSTER_ENTER_DISTANCE, spawnY, spawnZ);
            Vector3 engage = new Vector3(spawnX, spawnY, spawnZ);

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
            fallbackCfg.baseAttackSpeed = 0.8f;
            fallbackCfg.isBoss = wave.isBossWave;
            fallbackCfg.spriteScale = 1f;
            fallbackCfg.spriteIndex = 1;

            int fallbackSpriteOverride = 1;
            if (availableSprites != null && availableSprites.Count > 0)
            {
                if (waveIndex == 0 && i == 0 && availableSprites.Contains(1))
                    fallbackSpriteOverride = 1;
                else if (ConfigManager.Instance != null)
                    fallbackSpriteOverride = ConfigManager.Instance.PickWeightedSpriteIndex(availableSprites, stageIdx);
                else
                    fallbackSpriteOverride = availableSprites[0];
            }

            monster.Init(fallbackCfg, 0, CurrentChapter, fallbackScale, fallbackSpriteOverride);
            ApplyTutorialMonsterTuning(monster);
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
        pos.y = UnitBase.GROUND_Y;
        GameConfig.SetWorldPosition(go, pos);
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

    // ============================================================
    // 怪物死亡
    // ============================================================

    void OnMonsterDead(UnitBase monster)
    {
        Monster m = monster as Monster;
        if (m == null) return;

        currentGold += (long)(m.goldDrop * runGoldGainMul);

        // 连杀
        float now = Time.time;
        if (now - _lastKillTime <= GameConfig.COMBO_WINDOW)
            _killCombo++;
        else
            _killCombo = 1;
        _lastKillTime = now;
        if (_killCombo >= 3)
            currentGold += GameConfig.COMBO_BONUS_GOLD;
        BattleSideHud.Instance?.SetCombo(_killCombo);

        Hero.Instance?.AddExp(m.expDrop);
        BattleUI.Instance?.UpdateGold(currentGold);

        int goal = 0;
        foreach (var w in _waves) goal += w.monsterCount;
        if (goal < 1) goal = 1;
        int alive = 0;
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null && !monsters[i].isDead) alive++;
        // 当前死亡单位仍在列表里时 alive 含自己，defeated 用 clamp
        int defeated = Mathf.Clamp(goal - Mathf.Max(0, alive - 1), 0, goal);
        BattleUI.Instance?.UpdateQuest("击败所有敌人", defeated, goal);

        float killGain = (m.config != null && m.config.isBoss) ? 0.5f : ENERGY_PER_KILL;
        playerSkillEnergy = Mathf.Min(MAX_SKILL_ENERGY, playerSkillEnergy + killGain);
        for (int i = 0; i < mercSkillEnergy.Length; i++)
            mercSkillEnergy[i] = Mathf.Min(MAX_SKILL_ENERGY, mercSkillEnergy[i] + killGain * 0.75f);

        AchievementSystem.Instance?.OnKillMonster(CurrentChapter, m.config != null && m.config.isBoss);

        BattleUI.Instance?.UpdateSkillEnergy(0, playerSkillEnergy);
        BattleUI.Instance?.UpdateSkillEnergy(1, mercSkillEnergy[0]);
        BattleUI.Instance?.UpdateSkillEnergy(2, mercSkillEnergy[1]);

        // 本波清完 → 开下一波倒计时
        if (CountAliveMonsters() <= 1) // 含即将移除的自己，下一帧会清；用 <=1 更稳
        {
            // 延迟到本帧列表清理后判断；用协程下一帧
            if (!_waveCountdownActive && !_allWavesSpawned)
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
            yield break;
        }
        BeginNextWaveCountdown();
    }

    // ============================================================
    // 玩家技能释放（由头像点击触发）
    // ============================================================

    /// <summary>释放玩家技能（需要能量满）— 头像点击</summary>
    public bool TryUsePlayerSkill()
    {
        if (playerSkillEnergy < 0.99f) return false;
        if (hero == null || hero.isDead) return false;

        var skill = ResolvePlayerSkill();
        bool ok = skill.skillType != SkillSystem.SkillType.Buff
            && SkillSystem.Instance != null
            && SkillSystem.Instance.UseSkill(skill, hero);
        if (!ok)
            ExecuteAllySkillFallback(hero, skill);

        SkillRegistry.Instance?.PlaySkillVfx(skill.skillId, hero.GetHitPosition(), true, hero.facingDir, hero.transform);

        playerSkillEnergy = 0f;
        BattleUI.Instance?.UpdateSkillEnergy(0, 0f);
        TutorialDirector.Instance?.NotifyPlayerSkillUsed();
        Debug.Log($"[BattleManager] 玩家技能释放: {skill.skillName} ({skill.skillId})");
        return true;
    }

    /// <summary>释放佣兵技能（槽位 index 0/1）— 玩家/佣兵共用 Ally 技能池</summary>
    public bool TryUseMercSkill(int mercIndex)
    {
        if (mercIndex < 0 || mercIndex >= mercSkillEnergy.Length) return false;
        if (mercSkillEnergy[mercIndex] < 0.99f) return false;

        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null || mercIndex >= mercs.Count) return false;
        Mercenary merc = mercs[mercIndex];
        if (merc == null || merc.isDead) return false;

        var skill = ResolveMercSkill(merc.mercId);
        if (skill.skillType == SkillSystem.SkillType.Buff)
            ExecuteAllySkillFallback(merc, skill);
        else if (!(SkillSystem.Instance != null && SkillSystem.Instance.UseSkill(skill, merc)))
            ExecuteAllySkillFallback(merc, skill);

        SkillRegistry.Instance?.PlaySkillVfx(skill.skillId, merc.GetHitPosition(), true, merc.facingDir, merc.transform);

        mercSkillEnergy[mercIndex] = 0f;
        BattleUI.Instance?.UpdateSkillEnergy(mercIndex + 1, 0f);
        Debug.Log($"[BattleManager] 佣兵技能释放: {merc.mercId} → {skill.skillName} ({skill.skillId})");
        return true;
    }

    SkillSystem.ActiveSkill ResolvePlayerSkill()
    {
        string id = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetPlayerSkillId()
            : SkillRegistry.DefaultPlayerSkillId;
        return ResolveSkill(id);
    }

    SkillSystem.ActiveSkill ResolveMercSkill(string mercId)
    {
        string id = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetMercDefaultSkillId(mercId)
            : SkillRegistry.DefaultMercMeleeSkillId;
        return ResolveSkill(id);
    }

    SkillSystem.ActiveSkill ResolveSkill(string skillId)
    {
        var fromReg = SkillRegistry.Instance?.GetActiveSkill(skillId);
        if (fromReg != null) return fromReg;
        return new SkillSystem.ActiveSkill
        {
            skillId = skillId,
            skillName = skillId,
            damageMultiplier = 2.5f,
            cooldown = 0.1f,
            skillType = SkillSystem.SkillType.AOE,
            aoeRadius = 5f
        };
    }

    void ExecuteAllySkillFallback(UnitBase caster, SkillSystem.ActiveSkill skill)
    {
        var cfg = SkillRegistry.Instance?.Get(skill.skillId);

        if (skill.skillType == SkillSystem.SkillType.Buff)
        {
            float healBase = cfg != null && cfg.healBase > 0 ? cfg.healBase : skill.baseDamage;
            float pct = cfg != null ? cfg.healPercentOfMax : 0f;
            if (pct > 0f || healBase > 0 || skill.skillId == "ally_heal")
            {
                float maxHp = 0f;
                UnitBase target = FindLowestHpAlly();
                if (target == null) target = caster;
                if (target.attr != null)
                    maxHp = target.attr.GetAttr(AttrType.MaxHp);
                float heal = pct > 0f ? maxHp * pct : healBase + caster.attr.GetAttr(AttrType.Attack) * 0.5f;
                if (pct <= 0f && skill.skillId == "ally_heal")
                    heal = maxHp * 0.3f;
                ApplyHealToUnit(target, heal);
                return;
            }

            if (cfg != null && cfg.buffValue > 0)
            {
                ApplyTeamBuff(cfg);
                return;
            }
        }

        float damage = caster.attr.GetAttr(AttrType.Attack) * Mathf.Max(1f, skill.damageMultiplier);
        if (skill.skillType == SkillSystem.SkillType.SingleTarget)
        {
            UnitBase t = caster is Mercenary m ? m.FindNearestEnemy() : FindNearestMonster();
            if (t != null) t.TakeDamage(damage, false);
        }
        else
        {
            for (int i = monsters.Count - 1; i >= 0; i--)
            {
                if (monsters[i] == null || monsters[i].isDead) continue;
                float dist = Mathf.Abs(monsters[i].transform.position.x - caster.transform.position.x);
                if (skill.aoeRadius > 0 && dist > skill.aoeRadius) continue;
                monsters[i].TakeDamage(damage, false);
            }
        }
    }

    void ApplyHealToUnit(UnitBase unit, float heal)
    {
        if (unit == null || unit.isDead || unit.attr == null) return;
        float before = unit.currentHp;
        float maxHp = unit.attr.GetAttr(AttrType.MaxHp);
        unit.currentHp = Mathf.Min(maxHp, unit.currentHp + heal);
        int gained = Mathf.RoundToInt(unit.currentHp - before);
        if (gained > 0)
            DamageTextSystem.Instance?.SpawnHealText(unit.GetHitPosition(), gained);
    }

    void ApplyHealToTeam(float heal)
    {
        ApplyHealToUnit(hero, heal);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null) return;
        foreach (var m in mercs)
            ApplyHealToUnit(m, heal * 0.8f);
    }

    void ApplyTeamBuff(SkillConfig cfg)
    {
        void Apply(UnitBase u)
        {
            if (u == null || u.isDead || u.attr == null) return;
            u.attr.AddAttr(cfg.buffAttr, cfg.buffValue, cfg.buffIsPercent);
        }
        Apply(hero);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
            foreach (var m in mercs) Apply(m);
    }

    UnitBase FindLowestHpAlly()
    {
        UnitBase best = null;
        float worstRatio = 2f;

        void Consider(UnitBase u)
        {
            if (u == null || u.isDead || u.attr == null) return;
            float maxHp = u.attr.GetAttr(AttrType.MaxHp);
            if (maxHp <= 1f) return;
            float ratio = u.currentHp / maxHp;
            if (ratio < worstRatio)
            {
                worstRatio = ratio;
                best = u;
            }
        }

        Consider(hero);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
                Consider(mercs[i]);
        }
        return best;
    }

    UnitBase FindNearestMonster()
    {
        UnitBase nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null || monsters[i].isDead) continue;
            float d = Mathf.Abs(monsters[i].transform.position.x - hero.transform.position.x);
            if (d < best) { best = d; nearest = monsters[i]; }
        }
        return nearest;
    }

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

    /// <summary>清怪后：宝箱 → 掉落 → 三选一 → chuansongmen</summary>
    void StartStageClearRewardSequence()
    {
        if (_rewardSequenceStarted || _stageCleared) return;
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
        if (IsTutorialRun) return false;
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
        int bonusGold = 0;
        int equipCount = GameConfig.EQUIP_CHOOSE_COUNT;
        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;

        if (IsGoldDungeon)
        {
            bonusGold = GameConfig.GetGoldDungeonClearGold(ch, BattleDifficulty);
            equipCount = 0;
        }
        else if (isBoss)
        {
            bonusStar = 2;
            bonusGold = Mathf.RoundToInt(200 * ch * DifficultyGoldMul);
            equipCount += 1;
        }
        else if (currentStage.type == StageType.Elite)
        {
            bonusStar = 1;
            bonusGold = Mathf.RoundToInt(50 * DifficultyGoldMul);
        }

        // 多出的奖励件直接折金，三选一只展示 3 张
        int blacksmithLevel = TownSystem.Instance != null ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith) : 1;
        List<EquipInstance> rewards = (!IsGoldDungeon && ConfigManager.Instance != null)
            ? ConfigManager.Instance.GetRandomEquipInstances(equipCount, blacksmithLevel, bonusStar)
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
    }

    /// <summary>走进 chuansongmen 后：写档并弹选关</summary>
    public void FinishStageAfterPortalReached()
    {
        if (_stageCleared) return;
        _stageCleared = true;
        isInBattle = false;
        UnitsCanAct = false;

        if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;

        PersistBattleGold();
        ChapterManager.Instance?.OnStageComplete();

        if (IsGoldDungeon)
        {
            MercenaryManager.Instance?.ClearAllMercs();
            GameSceneManager.Instance?.ReturnToTown();
            return;
        }

        bool isBoss = currentStage != null && currentStage.type == StageType.Boss;
        if (isBoss)
        {
            UIManager.Instance?.ShowChapterClearChoice(
                onReturnTown: () =>
                {
                    MercenaryManager.Instance?.ClearAllMercs();
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
        }
        else
        {
            // 强制走下一关轮盘：不回章节地图，也没有返回按钮
            UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
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
        // 引导关死亡：不走遗产，按撤离收尾标记教程进度，避免反复卡在引导战
        if (IsTutorialRun || SkipLegacyOnEvacuate)
        {
            FinishTutorialEvacuate();
            return;
        }
        TriggerLegacyFlow(isDeath: true);
    }

    public void TriggerEvacuation()
    {
        isInBattle = false;
        ClearAllMonsters();
        MercenaryManager.Instance?.ClearAllMercs();
        // 撤离回城后打开冒险页（引导局另有收尾，不抢页签）
        if (!IsTutorialRun)
            TownHubController.PendingOpenAdventure = true;
        if (SkipLegacyOnEvacuate || IsTutorialRun)
        {
            FinishTutorialEvacuate();
            return;
        }
        TriggerLegacyFlow(isDeath: false);
    }

    void FinishTutorialEvacuate()
    {
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.WaitingEvacuate = false;
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

        List<EquipInstance> allEquips = GridBackpackSystem.Instance != null
            ? GridBackpackSystem.Instance.GetAllItemsForLegacy()
            : new List<EquipInstance>();

        void Finish(EquipInstance selectedLegacy)
        {
            EquipInstance legacyToTake = selectedLegacy;
            if (legacyToTake == null && allEquips.Count > 0)
                legacyToTake = allEquips[0];

            if (legacyToTake != null && SaveSystem.Instance?.Data != null)
            {
                EquipmentData legacy = new EquipmentData
                {
                    equipId = legacyToTake.templateId,
                    rarity = (int)legacyToTake.rarity,
                    attrBonus = legacyToTake.attrBonus,
                    tags = legacyToTake.template != null ? legacyToTake.template.tags : null,
                    isLegacy = true,
                    star = legacyToTake.star,
                    requireLevel = legacyToTake.requireLevel
                };
                SaveSystem.Instance.Data.legacyEquipPool.Add(legacy);
                if (legacyToTake.rarity == Rarity.Legendary
                    && !SaveSystem.Instance.Data.unlockedLegendaryWeapons.Contains(legacyToTake.templateId))
                    SaveSystem.Instance.Data.unlockedLegendaryWeapons.Add(legacyToTake.templateId);
                UIManager.Instance?.ShowToast($"获得遗产：{legacyToTake.equipName}");
                AchievementSystem.Instance?.OnBringLegacy();
            }

            // 用差额同步城镇金币（死亡时 currentGold 已回退到开局快照）
            PersistBattleGold();
            if (!isDeath)
            {
                int talentGain = (int)(Mathf.Max(0, currentGold - _goldAtRunStart) / GameConfig.GOLD_PER_TALENT_POINT);
                if (talentGain > 0)
                    ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talentGain, save: false, notify: false);
            }
            SaveSystem.Instance?.Save();
            BattleStateSaver.Instance?.ClearBattleState();
            TownHubController.PendingOpenAdventure = true;
            GameSceneManager.Instance?.LoadTownScene();
        }

        if (UIManager.Instance != null)
            UIManager.Instance.ShowLegacyChooseUI(allEquips, Finish);
        else
            Finish(null);
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

    // 商人/诅咒关：软著版已回退为普通战斗，保留方法供后续开放时启用
    [System.Obsolete("软著版未开放商人关")]
    void LoadMerchantStage(StageData stage)
    {
        UIManager.Instance?.ShowToast("本版本未开放商人关");
        ChapterManager.Instance?.OnStageComplete();
        UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
    }

    [System.Obsolete("软著版未开放诅咒关")]
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