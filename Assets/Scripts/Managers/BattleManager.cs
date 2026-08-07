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
    public bool isInBattle = false;
    public bool isAutoBattle = false;
    public List<AttrBonusData> tempBuffs = new List<AttrBonusData>();
    /// <summary>开战过场结束后才允许单位行动</summary>
    public bool UnitsCanAct { get; private set; } = true;
    /// <summary>佣兵相对主角身后间距（世界单位）</summary>
    const float MERC_BEHIND_SPACING = 0.85f;
    /// <summary>开场从站位左侧多远走进来</summary>
    const float PARTY_ENTER_FROM = 2.5f;
    const float MONSTER_ENTER_DIST = 3.5f;
    const float MONSTER_ENTER_SPEED = 2.2f;
    /// <summary>刷怪点距镜头右缘多远时触发（快进屏幕）</summary>
    const float SPAWN_SCREEN_LEAD = 1.2f;

    public int CurrentChapter => ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;

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

    /// <summary>
    /// 激活传送门：清完所有怪物后调用，EndPoint出现传送门特效
    /// </summary>
    void ActivatePortal()
    {
        if (_portalActive || _stageCleared) return;
        _portalActive = true;

        // 激活EndPoint（传送门）视觉：如果有SpriteRenderer则显示
        if (endPoint != null)
        {
            // 让传送门可见/发光
            SpriteRenderer portalSr = endPoint.GetComponent<SpriteRenderer>();
            if (portalSr != null)
            {
                portalSr.enabled = true;
                portalSr.color = new Color(0.6f, 0.9f, 1f, 1f); // 亮蓝色传送门
            }
            // 若EndPoint下挂PortalAnimator则激活，否则自动添加（保证传送门有动画）
            var portalFx = endPoint.GetComponentInChildren<PortalAnimator>(true);
            if (portalFx != null)
            {
                portalFx.gameObject.SetActive(true);
            }
            else if (endPoint.GetComponent<PortalAnimator>() == null)
            {
                endPoint.gameObject.AddComponent<PortalAnimator>();
                Debug.Log("[BattleManager] 传送门动画组件已自动添加");
            }
        }
        Debug.Log("[BattleManager] 传送门已激活，进入传送门通关");
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
            if (portalFx != null) portalFx.gameObject.SetActive(false);
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
        currentGold = 0;
        tempBuffs.Clear();
        _stageCleared = false;
        _portalActive = false;
        UnitsCanAct = false;
        MonsterAttackStyleTable.Reload();
        playerSkillEnergy = 0f;
        mercSkillEnergy[0] = 0f;
        mercSkillEnergy[1] = 0f;
        MercenaryManager.Instance?.ClearAllMercs();
        allyUnits.RemoveAll(u => u == null || u is Mercenary);

        // 隐藏传送门（打完怪才出现）
        HidePortal();

        if (hero != null && !allyUnits.Contains(hero))
            allyUnits.Add(hero);

        hero.InitNewRun();

        EnsureTestMercenaries();
        SpawnMercenaries();

        // 使用 SaveData 中的 maxUnlockedChapter，而非硬编码第1章
        int targetChapter = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        if (targetChapter < 1) targetChapter = 1;
        ChapterManager.Instance.StartChapter(targetChapter);

        if (ChapterManager.Instance.availableNextStages.Count > 0)
        {
            LoadStage(ChapterManager.Instance.availableNextStages[0]);
        }
    }

    void EnsureTestMercenaries()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.townLevel == null) data.townLevel = new TownLevel();

        if (data.permanentMercs.Count == 0)
        {
            data.permanentMercs.Add(new MercenaryData { mercId = "dunbing101", favorLevel = 1, level = 1 });
            data.permanentMercs.Add(new MercenaryData { mercId = "gongshou101", favorLevel = 1, level = 1 });
            Debug.Log("[BattleManager] 新存档自动添加测试佣兵(初级): 盾兵101 + 弓手101");
        }
        else
        {
            // 纠正存档里不存在的预制体 ID（如 gongshou102）
            for (int i = 0; i < data.permanentMercs.Count; i++)
            {
                string id = data.permanentMercs[i].mercId;
                if (string.IsNullOrEmpty(id)) continue;
                if (Resources.Load<GameObject>("Units/" + id) != null) continue;
                if (id.EndsWith("102")) data.permanentMercs[i].mercId = id.Substring(0, id.Length - 3) + "101";
                else if (id.EndsWith("2") && id.Length > 1)
                    data.permanentMercs[i].mercId = id.Substring(0, id.Length - 1) + "1";
            }
        }

        // tavern=0 会导致 GetMaxMercSlots=0，佣兵完全不刷；有出战名单时至少开对应槽
        int needSlots = Mathf.Clamp(data.permanentMercs.Count, 0, 2);
        if (needSlots > 0 && data.townLevel.tavern < needSlots)
        {
            data.townLevel.tavern = needSlots;
            Debug.Log($"[BattleManager] 酒馆等级纠正为 {needSlots}（保证佣兵槽可出战）");
        }
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
        _totalMonstersSpawnedThisStage = 0;
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

        // 触发章节背景切换
        SwitchBattleBackground(CurrentChapter);

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
                isInBattle = false;
                LoadMerchantStage(stage);
                break;
            case StageType.Enchant:
                isInBattle = false;
                LoadEnchantStage();
                break;
            case StageType.Curse:
                isInBattle = false;
                LoadCurseStage(stage);
                break;
            case StageType.Rest:
                isInBattle = false;
                LoadRestStage();
                break;
        }

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
            // 走近刷怪，不要一次刷完全部波次
            StopCoroutine("BattleStartSequenceCoroutine");
            StartCoroutine("BattleStartSequenceCoroutine");
        }
        else
        {
            UnitsCanAct = true;
        }
    }

    /// <summary>黑屏章节名 → 队伍从屏外走进来（无传送特效）</summary>
    IEnumerator BattleStartSequenceCoroutine()
    {
        UnitsCanAct = false;

        float startX = GetStageStartX();
        float z = hero != null ? hero.transform.position.z
            : (unitRoot != null ? unitRoot.position.z : 0f);
        // 站在镜头左侧外，过场后靠走路进场
        float enterX = startX - PARTY_ENTER_FROM;
        PlacePartyAt(enterX, z);

        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.offset = new Vector2(GameConfig.CAMERA_FOLLOW_OFFSET_X, 0f);
            if (hero != null) follow.SetTarget(hero.transform);
        }
        // 镜头立刻跟到出生侧，避免还停在场景中间
        if (Camera.main != null && hero != null)
        {
            Vector3 cp = Camera.main.transform.position;
            cp.x = hero.transform.position.x + GameConfig.CAMERA_FOLLOW_OFFSET_X;
            Camera.main.transform.position = cp;
        }

        string title = GameConfig.GetChapterTitleText(CurrentChapter);
        var splash = ChapterSplashOverlay.Show(title);
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
        // 若已越过触发线，立刻刷第一波（走近再刷，不会一次全刷）
        TrySpawnWavesApproachingScreen();

        if (hero != null && monsters.Count > 0)
        {
            var m0 = monsters[0];
            float hx = UnitBase.GetCombatX(hero);
            float mx = UnitBase.GetCombatX(m0);
            Debug.Log($"[BattleManager] 开战完成 monsters={monsters.Count} heroX={hx:F2} mon0={m0?.name} monX={mx:F2} dist={Mathf.Abs(hx - mx):F2} monHp={m0?.currentHp:F0} monAlly={m0?.isAlly} atkRange={hero.attr.GetAttr(AttrType.AttackRange):F2}");
        }
        else
            Debug.Log($"[BattleManager] 开战完成 monsters={monsters.Count}（等刷怪点靠近屏幕）");
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
    /// 刷怪：不跟 Ground。刷怪点是世界坐标；镜头跟随玩家右移时，
    /// 当 MonsterSpawn 快进入屏幕右缘时刷怪，怪从点右侧走进来交战。
    /// </summary>
    void TrySpawnWavesApproachingScreen()
    {
        if (_waves == null || hero == null) return;

        if (PoolManager.Instance != null && PoolManager.Instance._monsterPrefab == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Monster/Monstersmoban")
                      ?? Resources.Load<GameObject>("Prefabs/Monster/Monster");
            if (prefab != null)
            {
                PoolManager.Instance.Preload("Monster", prefab, 8);
                PoolManager.Instance._monsterPrefab = prefab;
                Debug.Log("[BattleManager] 补载怪物预制体: " + prefab.name);
            }
            else
            {
                Debug.LogError("[BattleManager] 找不到怪物预制体 Prefabs/Monster/Monstersmoban");
                return;
            }
        }

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float halfW = cam.orthographicSize * cam.aspect;
        float camRight = cam.transform.position.x + halfW;

        for (int i = 0; i < _waves.Count; i++)
        {
            var wave = _waves[i];
            if (wave == null || wave.spawned) continue;

            float pointX = wave.spawnAnchor != null
                ? wave.spawnAnchor.position.x
                : wave.triggerX;

            // 刷怪点已接近/进入屏幕右缘 → 开刷
            if (pointX > camRight + SPAWN_SCREEN_LEAD) continue;

            try
            {
                SpawnWave(wave, i);
                wave.spawned = true;
                Debug.Log($"[BattleManager] 刷怪点入画触发 第{i + 1}波 pointX={pointX:F2} camRight={camRight:F2} monsters={monsters.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BattleManager] 第{i + 1}波刷怪异常: {e}");
                wave.spawned = false;
            }
        }
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

    /// <summary>普通关：每个前方刷怪点一波；点快进屏幕时刷</summary>
    void SetupNormalWaves(int stageIdx)
    {
        float startX = GetStageStartX();
        var points = GetSpawnPointsSortedByX();
        // 只要还在起点右侧（或略左）的点都保留，靠镜头触发，不提前过滤光
        var usable = new List<Transform>();
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null && points[i].position.x >= startX - 2f)
                usable.Add(points[i]);
        }

        if (usable.Count == 0)
        {
            // 无点：在起点前方铺 3 个虚拟触发位
            for (int i = 0; i < 3; i++)
            {
                float x = startX + 5f + i * 6f;
                _waves.Add(new WaveData
                {
                    triggerX = x,
                    spawnAnchor = null,
                    monsterCount = 2,
                    isBossWave = false,
                    spawned = false,
                    aliveCount = 0
                });
            }
        }
        else
        {
            int n = Mathf.Min(usable.Count, 6);
            for (int i = 0; i < n; i++)
            {
                _waves.Add(new WaveData
                {
                    triggerX = usable[i].position.x,
                    spawnAnchor = usable[i],
                    monsterCount = 2,
                    isBossWave = false,
                    spawned = false,
                    aliveCount = 0
                });
            }
        }

        _totalWaves = _waves.Count;
        Debug.Log($"[BattleManager] 普通关 {_totalWaves}波, 刷怪点={usable.Count}, 起点={startX:F1}");
        for (int i = 0; i < _waves.Count; i++)
        {
            var w = _waves[i];
            string an = w.spawnAnchor != null ? w.spawnAnchor.name : "virtual";
            Debug.Log($"[BattleManager]   第{i + 1}波 anchor={an} x={w.triggerX:F1} count={w.monsterCount}");
        }
    }

    /// <summary>精英关：同样按刷怪点入画触发</summary>
    void SetupEliteWaves(int stageIdx)
    {
        SetupNormalWaves(stageIdx);
        for (int i = 0; i < _waves.Count; i++)
        {
            if (_waves[i] != null)
                _waves[i].monsterCount = 2;
        }
        Debug.Log($"[BattleManager] 精英关波次配置: {_totalWaves}波");
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

    /// <summary>Boss关：1波Boss</summary>
    void SetupBossWave(int stageIdx)
    {
        float bossX = endPoint != null ? endPoint.position.x - 2f : GetStageStartX() + 20f;
        _waves.Add(new WaveData
        {
            triggerX = bossX - 2f,
            monsterCount = 1,
            isBossWave = true,
            spawned = false,
            aliveCount = 0
        });
        _totalWaves = 1;
        Debug.Log($"[BattleManager] Boss关，位置X={bossX}");
    }

    // ============================================================
    // Update
    // ============================================================

    void Update()
    {
        if (!isInBattle || _stageCleared) return;

        // 清理死怪
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            if (monsters[i] == null || monsters[i].isDead)
            {
                if (monsters[i] != null)
                    monsters[i].OnDead -= OnMonsterDead;
                monsters.RemoveAt(i);
            }
        }

        // 刷怪点快进入屏幕右缘时刷怪（不跟 Ground）
        if (UnitsCanAct && !_allWavesSpawned && hero != null)
            TrySpawnWavesApproachingScreen();

        if (UnitsCanAct && !_allWavesSpawned)
        {
            _allWavesSpawned = true;
            foreach (var w in _waves)
                if (!w.spawned) { _allWavesSpawned = false; break; }
        }

        // 检查清怪条件：所有波次已触发 且 所有怪物已清除 → 激活传送门
        // 必须曾刷出过怪才开门，避免刷怪失败立刻通关
        if (_allWavesSpawned && monsters.Count == 0 && _totalMonstersSpawnedThisStage > 0
            && !_portalActive && !_stageCleared)
        {
            ActivatePortal();
        }

        // 通关条件：传送门已激活 且 玩家走到传送门位置（EndPoint）
        if (_portalActive && hero != null && !hero.isDead && !_stageCleared)
        {
            if (hero.transform.position.x >= endPoint.position.x - 0.5f)
            {
                _stageCleared = true;
                OnStageClear();
            }
        }

        // 更新技能能量（渐变 + 时间累积）
        if (hero != null && !hero.isDead)
        {
            playerSkillEnergy = Mathf.Min(MAX_SKILL_ENERGY, playerSkillEnergy + ENERGY_PER_SECOND * Time.deltaTime);
            if (BattleUI.Instance != null)
                BattleUI.Instance.UpdateSkillEnergy(0, playerSkillEnergy);
        }

        // 佣兵各自攒能量（稍慢于玩家）
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

    // ============================================================
    // 刷怪
    // ============================================================

    void SpawnWave(WaveData wave, int waveIndex = -1)
    {
        if (waveIndex < 0 && _waves != null)
            waveIndex = _waves.IndexOf(wave);
        if (waveIndex < 0) waveIndex = 0;

        int stageIdx = currentStage.stageIndex;
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

        bool isElite = currentStage.type == StageType.Elite;
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
        // 交战点：优先本波绑定的 MonsterSpawn；否则用 triggerX / 镜头右缘外
        float engageBaseX;
        if (wave.spawnAnchor != null)
            engageBaseX = wave.spawnAnchor.position.x;
        else if (wave.triggerX > -900f)
            engageBaseX = wave.triggerX;
        else
        {
            Camera cam = Camera.main;
            float halfW = cam != null && cam.orthographic ? cam.orthographicSize * cam.aspect : 3f;
            float camX = cam != null ? cam.transform.position.x : GetStageStartX();
            engageBaseX = camX + halfW + 1.5f;
        }

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
                // 从精灵1开始：首只必出1，其余加权仍偏向低编号
                if ((waveIndex == 0 && i == 0) || availableSprites.Count == 1)
                    spriteIndexOverride = availableSprites.Contains(1) ? 1 : availableSprites[0];
                else
                    spriteIndexOverride = ConfigManager.Instance.PickWeightedSpriteIndex(availableSprites, stageIdx);

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
            Vector3 enterFrom = new Vector3(spawnX + MONSTER_ENTER_DIST, spawnY, spawnZ);

            Monster m = SpawnMonster(template, stageIdx, enterFrom, monsterScale, spriteIndexOverride);
            if (m != null)
                m.BeginMapEnter(engagePos, MONSTER_ENTER_SPEED);

            if (isElite && !wave.isBossWave && m != null)
            {
                m.currentHp *= 1.5f;
                m.attr.AddAttr(AttrType.Attack, 0.5f, true);
            }

            wave.aliveCount++;
        }

        Debug.Log($"[BattleManager] 波次{waveIndex + 1} 刷新{wave.monsterCount}只 @x={engageBaseX:F1} anchor={(wave.spawnAnchor != null ? wave.spawnAnchor.name : "null")}");
    }

    /// <summary>兜底怪物：仍走用户刷怪点；精灵从1起</summary>
    void SpawnFallbackWave(WaveData wave, int waveIndex = 0)
    {
        float fallbackScale = 1f;
        if (wave.isBossWave)
            fallbackScale = GameConfig.BOSS_SCALE_MULTIPLIER;
        else if (currentStage.type == StageType.Elite)
            fallbackScale = GameConfig.ELITE_SCALE_MULTIPLIER;

        int stageIdx = currentStage.stageIndex;
        var availableSprites = ConfigManager.Instance.GetAvailableSpriteIndices(CurrentChapter, stageIdx, wave.isBossWave);
        var points = GetSpawnPointsSortedByX();

        for (int i = 0; i < wave.monsterCount; i++)
        {
            float spawnY = UnitBase.GROUND_Y;
            float spawnZ = unitRoot != null ? unitRoot.position.z : 0f;
            float spawnX;
            if (points.Count > 0)
            {
                int pi = Mathf.Min(waveIndex, points.Count - 1);
                Transform sp = points[pi];
                float extra = Mathf.Max(0, waveIndex - (points.Count - 1)) * 1.2f;
                spawnX = sp.position.x + extra + i * 0.35f;
                spawnZ = sp.position.z;
            }
            else
            {
                spawnX = GetStageStartX() + 4f + waveIndex * 2.5f + i * 0.35f;
            }
            Vector3 pos = new Vector3(spawnX + MONSTER_ENTER_DIST, spawnY, spawnZ);
            Vector3 engage = new Vector3(spawnX, spawnY, spawnZ);

            GameObject go = null;
            if (PoolManager.Instance != null)
                go = PoolManager.Instance.Get("Monster", pos, Quaternion.identity);
            if (go == null && PoolManager.Instance != null && PoolManager.Instance._monsterPrefab != null)
                go = Object.Instantiate(PoolManager.Instance._monsterPrefab, pos, Quaternion.identity);

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
                fallbackSpriteOverride = (waveIndex == 0 && i == 0 && availableSprites.Contains(1))
                    ? 1
                    : ConfigManager.Instance.PickWeightedSpriteIndex(availableSprites, stageIdx);
            }

            monster.Init(fallbackCfg, 0, CurrentChapter, fallbackScale, fallbackSpriteOverride);
            monster.BeginMapEnter(engage, MONSTER_ENTER_SPEED);
            monster.OnDead += OnMonsterDead;
            monsters.Add(monster);
            _totalMonstersSpawnedThisStage++;
            wave.aliveCount++;
        }
        Debug.Log($"[BattleManager] 兜底波次{waveIndex + 1}: {wave.monsterCount}只 @刷怪点 spawnedTotal={_totalMonstersSpawnedThisStage}");
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
            go = Object.Instantiate(PoolManager.Instance._monsterPrefab, pos, Quaternion.identity);

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
        pos.y = UnitBase.GROUND_Y;
        GameConfig.SetWorldPosition(go, pos);
        monster.OnDead += OnMonsterDead;
        monsters.Add(monster);
        _totalMonstersSpawnedThisStage++;

        Debug.Log($"[BattleManager] 生成怪物: {go.name} parent={go.transform.parent?.name} pos={go.transform.position} hp={monster.currentHp:F0} ally={monster.isAlly}");
        return monster;
    }

    // ============================================================
    // 怪物死亡
    // ============================================================

    void OnMonsterDead(UnitBase monster)
    {
        Monster m = monster as Monster;
        if (m == null) return;

        currentGold += (long)m.goldDrop;
        Hero.Instance.AddExp(m.expDrop);
        BattleUI.Instance?.UpdateGold(currentGold);

        int goal = 0;
        foreach (var w in _waves) goal += w.monsterCount;
        if (goal < 1) goal = 1;
        int alive = 0;
        for (int i = 0; i < monsters.Count; i++)
            if (monsters[i] != null && !monsters[i].isDead) alive++;
        // 当前死亡单位仍在列表里，defeated = goal - 仍存活的（含即将死的则再 +0）
        int defeated = Mathf.Clamp(goal - alive, 0, goal);
        BattleUI.Instance?.UpdateQuest("击败所有敌人", defeated, goal);

        // 技能能量累积（玩家 + 在场佣兵均分击杀奖励）
        float killGain = (m.config != null && m.config.isBoss) ? 0.5f : ENERGY_PER_KILL;
        playerSkillEnergy = Mathf.Min(MAX_SKILL_ENERGY, playerSkillEnergy + killGain);
        for (int i = 0; i < mercSkillEnergy.Length; i++)
            mercSkillEnergy[i] = Mathf.Min(MAX_SKILL_ENERGY, mercSkillEnergy[i] + killGain * 0.75f);

        AchievementSystem.Instance?.OnKillMonster(CurrentChapter, m.config != null && m.config.isBoss);

        BattleUI.Instance?.UpdateSkillEnergy(0, playerSkillEnergy);
        BattleUI.Instance?.UpdateSkillEnergy(1, mercSkillEnergy[0]);
        BattleUI.Instance?.UpdateSkillEnergy(2, mercSkillEnergy[1]);
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
            if (healBase > 0 || skill.skillId == "ally_heal")
            {
                float heal = healBase + caster.attr.GetAttr(AttrType.Attack) * 0.5f;
                ApplyHealToTeam(heal);
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

    void ApplyHealToTeam(float heal)
    {
        if (hero != null && !hero.isDead)
        {
            float before = hero.currentHp;
            hero.currentHp = Mathf.Min(hero.attr.GetAttr(AttrType.MaxHp), hero.currentHp + heal);
            int gained = Mathf.RoundToInt(hero.currentHp - before);
            if (gained > 0)
                DamageTextSystem.Instance?.SpawnHealText(hero.GetHitPosition(), gained);
        }
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
        {
            foreach (var m in mercs)
            {
                if (m == null || m.isDead) continue;
                float before = m.currentHp;
                m.currentHp = Mathf.Min(m.attr.GetAttr(AttrType.MaxHp), m.currentHp + heal * 0.8f);
                int gained = Mathf.RoundToInt(m.currentHp - before);
                if (gained > 0)
                    DamageTextSystem.Instance?.SpawnHealText(m.GetHitPosition(), gained);
            }
        }
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
        isInBattle = false;
        bool isBoss = currentStage != null && currentStage.type == StageType.Boss;

        if (isBoss)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex, atEndFlag: true);
        else if (currentStage != null)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex);

        int bonusStar = 0;
        int bonusGold = 0;
        int equipCount = GameConfig.EQUIP_CHOOSE_COUNT;

        if (isBoss)
        {
            bonusStar = 2;
            bonusGold = 200 * ChapterManager.Instance.currentChapter;
            currentGold += bonusGold;
            equipCount += 1;
        }
        else if (currentStage.type == StageType.Elite)
        {
            bonusStar = 1;
            bonusGold = 50;
            currentGold += bonusGold;
        }

        int blacksmithLevel = TownSystem.Instance != null ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith) : 1;
        List<EquipInstance> rewards = ConfigManager.Instance.GetRandomEquipInstances(equipCount, blacksmithLevel, bonusStar);

        UIManager.Instance.ShowStageClearUI(rewards, bonusGold, (selectedEquip) =>
        {
            if (selectedEquip != null)
            {
                GridBackpackSystem.Instance.TryAddItem(selectedEquip, out _);
                AchievementSystem.Instance?.OnObtainEquip(selectedEquip.rarity);
                foreach (var equip in rewards)
                {
                    if (equip != selectedEquip)
                        currentGold += (int)equip.rarity * 5 * (1 + equip.star);
                }
            }
            else
            {
                foreach (var equip in rewards)
                    currentGold += (int)equip.rarity * 5 * (1 + equip.star);
            }

            PersistBattleGold();
            ChapterManager.Instance.OnStageComplete();

            if (isBoss)
            {
                UIManager.Instance.ShowChapterClearChoice(
                    onReturnTown: () =>
                    {
                        MercenaryManager.Instance?.ClearAllMercs();
                        GameSceneManager.Instance?.ReturnToTown();
                    },
                    onNextChapter: () =>
                    {
                        int next = ChapterManager.Instance.currentChapter + 1;
                        if (next > 8) next = 8;
                        ChapterManager.Instance.StartChapter(next);
                        if (ChapterManager.Instance.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
                            ChapterManager.Instance.SelectStage(ChapterManager.Instance.stageMap[0]);
                    });
            }
            else
            {
                UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
            }
        });
    }

    void PersistBattleGold()
    {
        var save = SaveSystem.Instance?.Data;
        if (save == null) return;
        save.totalGold = currentGold;
        SaveSystem.Instance.Save();
    }

    public void OnHeroDead()
    {
        isInBattle = false;
        MercenaryManager.Instance?.ClearAllMercs();
        TriggerLegacyFlow();
    }

    public void TriggerEvacuation()
    {
        isInBattle = false;
        ClearAllMonsters();
        MercenaryManager.Instance?.ClearAllMercs();
        TriggerLegacyFlow();
    }

    void TriggerLegacyFlow()
    {
        List<EquipInstance> allEquips = GridBackpackSystem.Instance.GetAllItemsForLegacy();
        UIManager.Instance.ShowLegacyChooseUI(allEquips, (selectedLegacy) =>
        {
            EquipInstance legacyToTake = selectedLegacy;
            if (legacyToTake == null && allEquips.Count > 0)
                legacyToTake = allEquips[0];

            if (legacyToTake != null)
            {
                EquipmentData legacy = new EquipmentData
                {
                    equipId = legacyToTake.templateId,
                    rarity = (int)legacyToTake.rarity,
                    attrBonus = legacyToTake.attrBonus,
                    tags = legacyToTake.template.tags,
                    isLegacy = true,
                    star = legacyToTake.star,
                    requireLevel = legacyToTake.requireLevel
                };
                SaveSystem.Instance.Data.legacyEquipPool.Add(legacy);
                if (legacyToTake.rarity == Rarity.Legendary && !SaveSystem.Instance.Data.unlockedLegendaryWeapons.Contains(legacyToTake.templateId))
                    SaveSystem.Instance.Data.unlockedLegendaryWeapons.Add(legacyToTake.templateId);
                UIManager.Instance.ShowToast($"获得遗产：{legacyToTake.equipName}");
                AchievementSystem.Instance?.OnBringLegacy();
            }
            SaveSystem.Instance.Data.totalGold += currentGold;
            int talentGain = (int)(currentGold / GameConfig.GOLD_PER_TALENT_POINT);
            SaveSystem.Instance.Data.talentPoints += talentGain;
            SaveSystem.Instance.Save();
            GameSceneManager.Instance.LoadTownScene();
        });
    }

    public void ClearAllMonsters()
    {
        foreach (var m in monsters)
        {
            if (m != null)
            {
                m.OnDead -= OnMonsterDead;
                PoolManager.Instance.Release(m.gameObject);
            }
        }
        monsters.Clear();
    }

    // ============================================================
    // 特殊关卡
    // ============================================================

    void LoadMerchantStage(StageData stage)
    {
        stage.merchantGoodsInst = ConfigManager.Instance.GetRandomEquipInstances(Random.Range(3, 6), SaveSystem.Instance.Data.townLevel.blacksmith + 1);
        UIManager.Instance.ShowMerchantUI(stage.merchantGoodsInst, () =>
        {
            ChapterManager.Instance.OnStageComplete();
            UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
        });
    }

    void LoadEnchantStage()
    {
        UIManager.Instance.ShowEnchantUI((selectedItem, enchant) =>
        {
            if (selectedItem != null && enchant != null)
            {
                selectedItem.equip.enchants.Add(enchant);
                Hero.Instance.RecalcAttr();
            }
            ChapterManager.Instance.OnStageComplete();
            UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
        });
    }

    void LoadCurseStage(StageData stage)
    {
        stage.curseOptions = GenerateCurseOptions();
        UIManager.Instance.ShowCurseUI(stage.curseOptions, (selected) =>
        {
            tempBuffs.Add(selected.buff);
            tempBuffs.Add(selected.debuff);
            Hero.Instance.RecalcAttr();
            ChapterManager.Instance.OnStageComplete();
            UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
        });
    }

    void LoadRestStage()
    {
        UIManager.Instance.ShowRestUI(() =>
        {
            hero.currentHp = Mathf.Min(hero.currentHp + hero.attr.GetAttr(AttrType.MaxHp) * 0.3f, hero.attr.GetAttr(AttrType.MaxHp));
            ChapterManager.Instance.OnStageComplete();
            UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
        }, () =>
        {
            UIManager.Instance.ShowDecomposeUI((item) =>
            {
                GridBackpackSystem.Instance.DecomposeItem(item);
                ChapterManager.Instance.OnStageComplete();
                UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
            });
        });
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