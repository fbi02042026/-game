using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 战斗UI管理器：管理战斗界面所有UI元素
/// 对应预制体 BattleUI.prefab
/// </summary>

public partial class BattleUI : MonoBehaviour
{
    public static BattleUI Instance;

    /// <summary>本次隐藏是不是被关卡图按下的（只有它按下的才由它恢复）。</summary>
    bool _hiddenByStageMap;

    /// <summary>
    /// 【2026-10-12 主人拍板】关卡图 / 下一关转盘显示期间，隐藏整个战斗 HUD。
    ///
    /// <para>起因：技槽（BackpackPanel/SkillBar）自带嵌套 Canvas 被抬到 921 ——
    /// 这是 2026-10-11 为压过战斗内抽奖面板（BattlePopup 920）加的；而关卡图 BattleStageMap(880)、
    /// 下一关转盘 TownPopup(900) 都比 921 低 → 只有那一排技能槽穿到关卡图/转盘上面
    /// （主人报「技能跑到老虎机界面了」）。HUD 根 Canvas 是 0，本来就被关卡图整片盖住，
    /// 唯独技槽例外；整根 HUD 关掉最干净，也不用去动 UiSort 层值。</para>
    ///
    /// <para><b>唯一出口</b>：只由 <c>BattleStageMapUI</c> 在开图（StartFlow / StartBranchFlow）
    /// 与关图（CoPedestalAccept / Hide）四个时机调用。别处一律不许改本节点的激活态。</para>
    ///
    /// <para>幂等且 fail closed：只恢复「自己按下去的那次」，本来就隐藏的对象绝不被强行点亮。</para>
    /// </summary>
    public void SetHiddenForStageMap(bool hidden)
    {
        if (hidden)
        {
            if (!gameObject.activeSelf) return;
            _hiddenByStageMap = true;
            gameObject.SetActive(false);
        }
        else
        {
            if (!_hiddenByStageMap) return;
            _hiddenByStageMap = false;
            gameObject.SetActive(true);
        }
    }

    [Header("=== 顶部状态栏 ===")]
    public Text stageLabel;         // 关卡标识：只显示地图名（暮影森林…），不再出现「第X章」
    public Text difficultyLabel;    // 难度标识 "普通"
    public Text goldText;           // 金币
    /// <summary>顶栏 DraftDisplay 抽奖币资源条（主人 2026-10-18 摆的节点，子节点 DraftCoinIcon/DraftCoinText，
    /// 原名 GoldIcon/GoldText 与金币区重名已改）：
    /// 数字 = BattleManager.currentGold（抽奖唯一能花的那本账），与抽奖面板旁池子计数同源同刻。</summary>
    public Text draftCoinText;      // 抽奖币（DraftDisplay/DraftCoinText）
    public Text talentStoneText;    // 天赋石
    public Text enchantStoneText;   // 旧布局的第三个资源位；新顶栏用它显示天赋石，附魔石不再单列
    public Text decomposeMatText;   // 材料
    public Button settingsButton;   // 设置按钮（右上角顶栏）

    [Header("=== 进度条 ===")]
    public Transform progressContainer; // 进度条容器
    public List<Image> progressNodes;   // 关卡节点圆点
    public Image playerMarker;          // 玩家位置图标
    public Image endFlag;               // 终点旗帜

    [Header("=== 任务面板 ===")]
    public GameObject questPanel;        // 任务面板
    public Text questTitle;             // "任务"
    public Text questDesc;              // "击败所有敌人"
    public Text questProgress;          // "(0/3)"
    public Image questRewardIcon;       // jianglitubiao
    public Text questRewardAmount;      // jianglishuzi

    [Header("=== 角色栏 ===")]
    public CharacterSlotUI playerSlot;      // 玩家槽位
    public CharacterSlotUI mercSlot1;       // 佣兵槽位1（需酒馆1级解锁）
    public CharacterSlotUI mercSlot2;       // 佣兵槽位2（需酒馆2级解锁）

    [Header("=== 网格背包 ===")]
    public GridLayoutGroup gridLayout;       // 网格布局组
    public List<GridCellUI> gridCells;       // 12 格（4 列 × 3 行，与美术 GridContainer 一致）
    /// <summary>拾取模式（BattleLootMode）的「确定」按钮。整理功能已移除。</summary>
    public Button lootConfirmButton;

    [Header("=== 技能头像区（旧布局遗留，当前预制体已无对应节点）===")]
    /// <summary>
    /// 已废弃：绑的是 SkillBtn1 / PlayerSkill，而 BattleUI.prefab 里这些节点已被美术改版删除，
    /// BindSkillAvatar 找不到节点 → root 恒为 null，所有调用都是空转。
    /// 保留字段只为不破坏预制体已序列化的数据；玩家技能实际走下面的 runSkillSlots。
    /// </summary>
    public SkillAvatarUI playerSkillAvatar;   // 玩家技能头像（圆形+能量槽+光边）
    /// <summary>已废弃：原绑 SkillBtn2 / MercSkill1，节点已不存在。佣兵技能实际走 mercSkillSlots。</summary>
    public SkillAvatarUI merc1SkillAvatar;    // 佣兵1技能头像
    /// <summary>已废弃：原绑 SkillBtn3 / MercSkill2，节点已不存在。佣兵技能实际走 mercSkillSlots。</summary>
    public SkillAvatarUI merc2SkillAvatar;    // 佣兵2技能头像
    /// <summary>自动战斗未开放。运行时隐藏，勿在预制体里删节点。</summary>
    public Button autoButton;

    [Header("=== 底部临时布局（4技能槽 / 5装备槽）===")]
    public Transform skillSlotRoot;      // BackpackPanel/SkillBar（4 个玩家被动技槽）
    public Transform equipSlotRoot;      // BackpackPanel/zhuangbei（5 个装备快捷槽）
    /// <summary>
    /// 玩家被动技能槽（4 个）：技能由 PlayerSkillPassive 充能满后自动释放，放完进冷却。
    /// 玩家不手动点，界面只表现「图标 + 名称 + 星级 + 充能条 + 冷却」。
    /// </summary>
    public List<SkillAvatarUI> runSkillSlots = new List<SkillAvatarUI>();
    public List<EquipQuickSlotUI> equipQuickSlots = new List<EquipQuickSlotUI>();
    /// <summary>
    /// 佣兵自带技能槽（2 个）：节点在各自角色卡里 MercSlot1/skill、MercSlot2/skill，
    /// 与上面的玩家被动技槽是两套。佣兵技能同样由 MercSkillCaster 自动释放。
    /// </summary>
    public List<SkillAvatarUI> mercSkillSlots = new List<SkillAvatarUI>();
    /// <summary>
    /// 装备快捷槽顺序兜底：先按节点名认部位（见 EquipSlotTypeOf），认不出来才按下标取这里。
    /// 按需求暂时不做「手」和「披风」两槽。
    /// </summary>
    static readonly EquipSlotType[] QuickSlotOrder =
    {
        EquipSlotType.Head,     // 头
        EquipSlotType.Chest,    // 胸甲
        EquipSlotType.Feet,     // 脚
        EquipSlotType.OffHand,  // 副手
        EquipSlotType.MainHand  // 主手
    };

    [Header("=== 底部功能入口 ===")]
    public Button characterButton;   // 角色属性按钮（新预制体暂无此节点）
    // 暂停已合并进右上角设置的弹窗里，不再单独放按钮

    [Header("=== 面板 ===")]
    public GameObject characterPanel;    // 角色属性面板

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 刘海避让最先挂（2026-10-26 主人「战斗中顶部没有适配，要和主界面适配一样避免刘海」）：
        // 主界面（TownSharedChrome）与战斗用同一套 EnsureTopOffset，真机主界面正常、战斗贴刘海，
        // 差别只在时序 —— 下面 AutoBindMissingRefs / BattleSideHud / BattleViewportFit 等任何一步
        // 在真机抛异常，原 167 行的挂载就整段静默跳过。这里提前挂顶栏三件套（幂等，重复调用不叠加），
        // 167 行的调用保留，负责晚一步才建出来的 BossBar。
        //
        // 【2026-10-28 修正旧注释】原文写「此刻画布尚未布局、父级 rect=0，Shift 误跑路径天然不触发」——
        // 这个假设是错的：画布根 rect 不是 0，而是**尚未缩放的屏幕像素尺寸**（实测 1240×2770）。
        // 正因为它非 0，SafeAreaFitter 才会以默认 Shift 抢跑（AddComponent 同步触发 Awake→Apply），
        // 把顶条锚点改成全拉伸并按像素尺寸捕获 base → CanvasScaler 生效后顶条高度变负 → 整条不可见。
        // 现在顶/底栏统一走 SafeAreaFitter.EnsureConfigured（AddComponent 前先登记配置，
        // Awake 首次 Apply 就是 Offset，绝不改锚点），这里提前挂零风险。
        int topFitCount = SafeAreaFitter.ApplyTopOffset(transform, "TopBar", "TopStatus", "ProgressBar");
        Debug.Log($"[BattleUI] 顶栏刘海避让已挂载 {topFitCount} 个，safeArea={Screen.safeArea} " +
                  $"screen={Screen.width}x{Screen.height}（safeArea 顶部若为 0 = 机型未上报刘海，另查）");
        // 【2026-10-28 战斗顶条消失修复】把顶条锚点/尺寸打出来，便于一眼判断好坏。
        // 正常值：anchor=(0.5,1)~(0.5,1)、size=720x120。
        // 一旦变成 (0,0)~(1,1) 且高度为负（曾实测 200x-1042），就是锚点又被谁改了 → 顶条不可见。
        var topBarRt = transform.Find("TopBar") as RectTransform;
        if (topBarRt != null)
            Debug.Log($"[BattleUI] TopBar 锚点={topBarRt.anchorMin}~{topBarRt.anchorMax} " +
                      $"尺寸={topBarRt.rect.size} 位置={topBarRt.anchoredPosition}");

        // 【2026-10-27 修「血条一进就有、一会又消失」】顶部进度条已按 2026-10-26 拍板整体去掉，
        // 但 BattleUI.prefab 里 ProgressBar 根节点仍 active=1，开局到第一波怪出场之间无人藏它，
        // 会裸显一条白长条（其 ProgressLine 贴图悬空显示白块，看着像血条）；第一波怪出场时
        // BattleBossHpBar.ApplyVisibility 才顺手把它藏掉 —— 这就是「先有后无」的全过程。
        // 唯一出口：Awake 即藏。显隐联动链（BattleBossHpBar.ApplyVisibility）只藏不放，口径不变。
        var removedProgressBar = FindChildIgnoreCase(transform, "ProgressBar");
        if (removedProgressBar != null && removedProgressBar.gameObject.activeSelf)
            removedProgressBar.gameObject.SetActive(false);

        // 运行时补绑预制体里漏接的引用
        AutoBindMissingRefs();

        // 中文 → fusion-pixel；数字 → PixelFont
        GameFonts.ApplyToHierarchy(transform);

        // 右侧：连杀 + 下一波倒计时
        BattleSideHud.EnsureOn(transform);

        // Boss 屏幕血条（场景节点 BossBar）
        BattleBossHpBar.Ensure(transform);

        EnsureBattleControls();
        // 【2026-10-30 竞技场开场动画】「对战」节点在预制体里默认是亮的 → 进战斗先整根藏掉；
        // 只在竞技场战斗、玩家点「开始战斗/继续」后播一次（BattleManager.OnLiveStartFight → BattleVsIntro.TryPlayForArena）。
        BattleVsIntro.HideForBattle();

        // 开战只走 AutoGameInitializer（场景组件 Awake）。UI 不再后备 Initialize，避免双入口叠开战。

        // 战斗场景：map 自适应铺满；与主相机共用，避免拆分相机黑屏
        if (GameSceneGate.IsBattle)
        {
            BattleViewportFit.Apply(Camera.main, GetComponent<Canvas>() ?? GetComponentInParent<Canvas>());
            UiPrefabRectGuard.Attach(transform, "Background");
        }
        else
            UICanvasSetup.ApplyOn(gameObject, UICanvasSetup.ResolveUiCamera());

        // 刘海 / 挖孔：战斗顶栏（TopBar / TopStatus / ProgressBar 那条）+ 屏幕血条 BossBar
        // 整体下移 safeArea 顶部高度。只挪 anchoredPosition、不动锚点与尺寸；
        // 只会挑最外层那个节点，父子不会双倍下推。
        // 【2026-10-22 血条偏下修复】BossBar 预制体已改贴顶锚（y=-163），在此加入名单后
        // 与顶栏同步避让：瘦屏不再随画布中心下漂，挖孔屏也不再压住状态行（设计间距 38 保持）。
        SafeAreaFitter.ApplyTopOffset(transform, "TopBar", "TopStatus", "ProgressBar", "BossBar");

        BindAutoBattleUnavailable();
        if (characterButton != null) characterButton.onClick.AddListener(OnOpenCharacter);
        // 设置入口固定在右上角；暂停与撤离都在这个弹窗里
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnOpenSettings);
            settingsButton.onClick.AddListener(OnOpenSettings);
        }
        // 拾取模式的「确定」：平时隐藏，不需要整理按钮
        if (lootConfirmButton != null)
        {
            lootConfirmButton.onClick.RemoveListener(OnLootConfirm);
            lootConfirmButton.onClick.AddListener(OnLootConfirm);
        }
    }

    // ==================== 临时打点拆除记录 2026-10-27 ====================
    // 「顶条抓现行」打点（ArmTopBarWatch / WatchTopBarNode / TopBarWatchMark / RtSnap / _topWatch）
    // 与「B10 战斗右上黑板」打点（CoDumpUiRectsForB10 / DumpUiRectsForB10 / DumpRectForB10）
    // 整段删除。根因（Editor.log:8384 实锤）：顶条打点在 Awake 首行 ArmTopBarWatch 里抛
    // InvalidOperationException「Collection was modified」→ Awake 从第一行中断，
    // AutoBindMissingRefs / 血条 BindUi / ApplyTopBarResourceVisibility（藏 99999 假条）/
    // 佣兵槽绑定与头像点击全部没跑 —— 顶条假数据、佣兵头像不显示、引导点头像卡住、
    // 血条白条四个症状同根因。「顶条消失」「B10 黑板」问题均已拍板关闭，打点使命完成。

    void Start()
    {
        if (GameSceneGate.IsBattle && BattleManager.Instance == null)
            Debug.LogError("[BattleUI] BattleManager 未装配：开战只走 AutoGameInitializer，UI 不再后备初始化。");
        Invoke(nameof(DelayedUpdateSlots), 0.1f);
    }

    void DelayedUpdateSlots()
    {
        if (BattleBagSystem.Instance != null)
        {
            BattleBagSystem.Instance.OnBackpackChanged -= UpdateBackpackGrid;
            BattleBagSystem.Instance.OnBackpackChanged += UpdateBackpackGrid;
        }
        RebindAfterSystemsReady();
        RefreshBattleHud();
        // 「击败」计数开场同步：断点续战恢复的累计击杀数直接亮出来
        BattleSideHud.Instance?.SyncKillTotalFromRunStats();
        // 【2026-10-11 主人拍板】buff 图标条挂到玩家头像「战斗力」右侧（数据出口 AttrSystem.CollectActiveTimedBuffs）
        BuffHudUI.Ensure(playerSlot);
        GameFonts.ApplyToHierarchy(transform);
        // Canvas 尺寸这时才是最终值，越界判断必须放在这之后
        ClampCharacterBarInsideParent();
        HeroThunderUltimate.Instance?.EnsureBattleUi();
        Debug.Log($"[BattleUI] HUD已刷新 — playerSlot={playerSlot?.root!=null} merc1={mercSlot1?.root!=null} merc2={mercSlot2?.root!=null} progressNodes={progressNodes?.Count} marker={playerMarker!=null}");
    }

    float _liveBarTimer;

    float _lastPlayerHp = -1f, _lastPlayerMaxHp = -1f, _lastPlayerEnergy = -1f, _lastPlayerExp = -1f;
    float _lastMerc1Hp = -1f, _lastMerc1Energy = -1f;
    float _lastMerc2Hp = -1f, _lastMerc2Energy = -1f;
    const float LiveBarInterval = 0.1f;

    void Update()
    {

        // 每帧都要跟：开战/撤离/进入整理阶段都要立刻切换遮罩，不能等 0.1s 节流
        TickBattleMask();

        // 【2026-10-07 主人拍板】角色卡头像的「技能就绪」金色光环已整条删除（glowBorder / TickSkillReadyPulse）。
        if (playerSkillAvatar != null && playerSkillAvatar.IsReadyPulse)
            playerSkillAvatar.TickReadyPulse();

        _liveBarTimer += Time.deltaTime;
        if (_liveBarTimer < LiveBarInterval) return;
        _liveBarTimer = 0f;
        TickRunSkillSlots();      // 玩家 4 个被动技槽
        TickMercSkillSlots();     // 佣兵自带技能槽（角色卡内 skill 节点）
        BuffHudUI.Tick();         // 【2026-10-11 主人拍板】战斗力右侧 buff 条（倒计时 / 快到期闪烁）
        RefreshLiveBars();
        RefreshShieldBars();      // 蓝色盾条（有护盾时才显形）
    }

    /// <summary>
    /// 【2026-10-16 主人报「受击掉血要过一会才显示」】受击/治疗当场把血条刷出来，不等 0.1s 节流。
    /// RefreshLiveBars 自带变化检测，没变化时是空转，推着刷不会重复排版。
    /// 唯一调用点 = UnitBase.TakeDamage 我方受击分支；治疗类要跟进时也走这里，不要另写第二份。
    /// </summary>
    public void ForceRefreshLiveBars()
    {
        RefreshLiveBars();
        RefreshShieldBars();
    }

    void RefreshLiveBars()
    {
        var hero = Hero.Instance;
        if (playerSlot != null && hero != null && !hero.isDead)
        {
            float maxHp = hero.attr.GetAttr(AttrType.MaxHp);
            float energy = BattleManager.Instance != null ? BattleManager.Instance.PlayerSkillEnergyPeak : 0f;
            // 等级/经验已移除，不再参与刷新判定
            if (!Mathf.Approximately(_lastPlayerHp, hero.currentHp)
                || !Mathf.Approximately(_lastPlayerMaxHp, maxHp)
                || !Mathf.Approximately(_lastPlayerEnergy, energy))
            {
                _lastPlayerHp = hero.currentHp;
                _lastPlayerMaxHp = maxHp;
                _lastPlayerEnergy = energy;
                playerSlot.UpdateSlot(PlayerIdentity.DisplayName, hero.level, hero.currentHp, maxHp, showLevel: false);
                playerSlot.SetEnergy(energy);   // 蓝条留给技能能量
            }
        }

        if (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle)
        {
            if (TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud)
                RefreshTutorialMercLiveBar();
            return;
        }

        var mm = MercenaryManager.Instance;
        if (mm == null) return;
        int maxSlots = mm.GetMaxMercSlots();
        var mercs = mm.GetActiveMercs();

        // 未解锁槽不刷数值，保持美术默认
        if (maxSlots > 0 && mercSlot1 != null && mercs != null && mercs.Count > 0 && mercs[0] != null)
        {
            var m = mercs[0];
            float maxHp = m.attr.GetAttr(AttrType.MaxHp);
            float energy = BattleManager.Instance != null ? BattleManager.Instance.GetMercMp(0) : 0f;
            if (!Mathf.Approximately(_lastMerc1Hp, m.currentHp) || !Mathf.Approximately(_lastMerc1Energy, energy))
            {
                _lastMerc1Hp = m.currentHp;
                _lastMerc1Energy = energy;
                // 2026-10-11 拍板：头像栏名字一律真名（与 SetupMercSlot / 场景头顶名同源 DisplayName），
                // 旧写法 GetJobName 每帧把名字覆盖回职业名（如「水系法师」），已纠正。
                mercSlot1.UpdateSlot(m.DisplayName, m.mercLevel, m.currentHp, maxHp);
                mercSlot1.SetEnergy(energy);
            }
        }
        if (maxSlots > 1 && mercSlot2 != null && mercs != null && mercs.Count > 1 && mercs[1] != null)
        {
            var m = mercs[1];
            float maxHp = m.attr.GetAttr(AttrType.MaxHp);
            float energy = BattleManager.Instance != null ? BattleManager.Instance.GetMercMp(1) : 0f;
            if (!Mathf.Approximately(_lastMerc2Hp, m.currentHp) || !Mathf.Approximately(_lastMerc2Energy, energy))
            {
                _lastMerc2Hp = m.currentHp;
                _lastMerc2Energy = energy;
                // 同上：真名，不再用职业名覆盖
                mercSlot2.UpdateSlot(m.DisplayName, m.mercLevel, m.currentHp, maxHp);
                mercSlot2.SetEnergy(energy);
            }
        }
    }

    void OnDestroy()
    {
        if (BattleBagSystem.Instance != null)
            BattleBagSystem.Instance.OnBackpackChanged -= UpdateBackpackGrid;
        ArenaBattleSkip.OnBattleUiDestroyed();   // 竞技场跳过加速兜底恢复（切场景/异常必经）
        if (Instance == this)
            Instance = null;
    }

    /// <summary>战斗 HUD 全量刷新入口（章节/难度/资源/头像/背包）</summary>
    public void RefreshBattleHud()
    {
        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        int stageIdx = 0;
        // 【2026-10-06 主人拍板】左上角那格显示的是「玩家选的难度」（真源 BattleManager.BattleDifficulty，
        // 引导局开局写 0 = 普通），**不再**按关卡类型显示「精英 / Boss」——
        // 「精英」是关卡类型、「困难」是难度，两套词混在同一格里会读错。
        // 难度名的唯一出处是 GameConfig.GetDifficultyName（原 StageTypeToDifficulty 已删）。
        string diff = GameConfig.GetDifficultyName(
            BattleManager.Instance != null ? BattleManager.Instance.BattleDifficulty : 0);
        if (BattleManager.Instance != null && BattleManager.Instance.currentStage != null)
            stageIdx = BattleManager.Instance.currentStage.stageIndex;
        // 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】顶栏 = 本局获得金币（RunEarnedGold，
        // 真源 BattleRunStats.GoldEarned + 待发按关结算金），死亡/撤离写回城镇的就是这个数；
        // 抽奖币池（currentGold，启动金/补贴/抽中金，带不出本局）另走顶栏 DraftDisplay 条 +
        // 抽奖面板旁的池子计数，不上「本局获得金币」那一格 —— 绝不能拿 save.totalGold 或 currentGold 顶上。
        long gold = BattleManager.Instance != null ? BattleManager.Instance.RunEarnedGold : 0;

        UpdateStageInfo(chapter, stageIdx, diff, gold);
        // 【2026-10-18 主人摆的 DraftDisplay 抽奖币条】全量刷新时同步初值（进战斗 = 启动金）
        UpdateDraftCoin(BattleManager.Instance != null ? BattleManager.Instance.currentGold : 0);
        ApplySoloBattleHud();
        UpdateCharacterSlots();
        UpdateSkillAvatars();
        UpdateMercSkillSlots();
        UpdateBackpackGrid();
        // 【2026-10-21 主人拍板】顶部进度条改按当前关波次推进：实时读 BattleManager 的波次状态
        //（同 Assembly，_waves/_activeWaveIndex 均 internal 可读）。已清波数 = 当前进行中波下标
        //（第 i 波进行中 → 已清 i 波；-1 尚未开刷 → 0），总波数 = _waves.Count。
        var bmProgress = BattleManager.Instance;
        if (bmProgress != null)
        {
            int totalW = bmProgress._waves != null ? bmProgress._waves.Count : 0;
            int clearedW = Mathf.Clamp(bmProgress._activeWaveIndex, 0, totalW);
            UpdateWaveProgress(clearedW, totalW);
        }
        // 底部 4 技槽 / 5 装备槽以前只在 Start 后 0.1s 刷一次，
        // 战斗中换装备、波次刷新都不会更新 —— 这里补上，保证每次全量刷新都同步。
        UpdateRunSkillSlots();
        UpdateEquipQuickSlots();

        // 【2026-10-18 主人拍板】任务面板击杀计数与结算「击杀敌人」口径不一致，先整体隐藏；
        // 只藏 UI，UpdateQuest / 奖励逻辑不动（BossHpBar 的联动同步改掉，否则会被重新点亮）。
        if (questPanel != null && questPanel.activeSelf)
            questPanel.SetActive(false);
        if (questDesc != null && string.IsNullOrEmpty(questDesc.text))
            UpdateQuest("击败所有敌人", 0, 3);
        RefreshQuestReward();
    }

    // ===== 面板控制 =====

    void OnOpenCharacter()
    {
        if (characterPanel != null) characterPanel.SetActive(true);
    }

    public void OnOpenSettings()
    {
        TutorialHintUI.Instance?.SetHardBlocking(false);
        var panel = SettingsPopupUI.Ensure();
        panel.Open(SettingsHost.Battle);

        if (TutorialDirector.Instance != null && TutorialDirector.Instance.WaitingEvacuate
            && panel.EvacuateButton != null)
        {
            TutorialHintUI.Ensure().ShowHard("选择「撤离」，回城结算。",
                panel.EvacuateButton.GetComponent<RectTransform>());
        }
    }

    /// <summary>
    /// 关闭所有面板
    /// </summary>
    public void CloseAllPanels()
    {
        if (characterPanel != null) characterPanel.SetActive(false);
        if (SettingsPopupUI.Instance != null && SettingsPopupUI.Instance.IsOpen)
            SettingsPopupUI.Instance.Close();
    }
}
