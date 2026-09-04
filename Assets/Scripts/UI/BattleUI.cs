using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 战斗UI管理器：管理战斗界面所有UI元素
/// 对应预制体 BattleUI.prefab
/// </summary>
public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance;

    [Header("=== 顶部状态栏 ===")]
    public Text stageLabel;         // 关卡标识 "第一章" / "1-1"
    public Text difficultyLabel;    // 难度标识 "普通"
    public Text goldText;           // 金币
    public Text talentStoneText;    // 天赋石
    public Text enchantStoneText;   // 附魔石（可选）
    public Text decomposeMatText;   // 材料
    public Button settingsButton;   // 设置按钮

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
    public List<GridCellUI> gridCells;       // 24个格子
    public Button organizeButton;            // 整理背包

    [Header("=== 技能头像区 ===")]
    public SkillAvatarUI playerSkillAvatar;   // 玩家技能头像（圆形+能量槽+光边）
    public SkillAvatarUI merc1SkillAvatar;    // 佣兵1技能头像
    public SkillAvatarUI merc2SkillAvatar;    // 佣兵2技能头像
    public Button autoButton;                 // 自动战斗按钮

    [Header("=== 底部功能入口 ===")]
    public Button characterButton;   // 角色属性按钮
    public Button pauseButton;       // 暂停按钮
    // settingsButton 已在顶部状态栏定义，底部复用同一个

    [Header("=== 面板 ===")]
    public GameObject characterPanel;    // 角色属性面板
    public GameObject pausePanel;        // 暂停面板
    public GameObject settingsPanel;     // 设置面板

    void Awake()
    {
        Instance = this;

        // 运行时补绑预制体里漏接的引用
        AutoBindMissingRefs();

        // 中文 → fusion-pixel；数字 → PixelFont
        GameFonts.ApplyToHierarchy(transform);

        // 右侧：连杀 + 下一波倒计时
        BattleSideHud.EnsureOn(transform);

        // Boss 屏幕血条（场景节点 BossBar）
        BattleBossHpBar.Ensure(transform);

        // 后备入口：仅 Battle 场景才跑战斗初始化
        if (GameSceneGate.IsBattle)
            AutoGameInitializer.Initialize();

        // 战斗场景：Match Width 铺满竖屏，避免 HUD 被裁切
        if (GameSceneGate.IsBattle)
        {
            BattleViewportFit.Apply(UICanvasSetup.ResolveUiCamera(), GetComponent<Canvas>() ?? GetComponentInParent<Canvas>());
            UiPrefabRectGuard.Attach(transform, "Background");
        }
        else
            UICanvasSetup.ApplyOn(gameObject, UICanvasSetup.ResolveUiCamera());

        if (autoButton != null) autoButton.onClick.AddListener(ToggleAutoBattle);
        if (pauseButton != null) pauseButton.onClick.AddListener(OnPause);
        if (characterButton != null) characterButton.onClick.AddListener(OnOpenCharacter);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnOpenSettings);
        if (organizeButton != null)
        {
            organizeButton.onClick.RemoveListener(OnOrganizeBackpack);
            organizeButton.onClick.AddListener(OnOrganizeBackpack);
        }
        // 头像=技能：在 AutoBind + WireSlotSkillClicks 里统一绑 Player/Merc 槽
    }

    void Start()
    {
        Invoke(nameof(DelayedUpdateSlots), 0.1f);
    }

    void DelayedUpdateSlots()
    {
        if (GridBackpackSystem.Instance != null)
        {
            GridBackpackSystem.Instance.OnBackpackChanged -= UpdateBackpackGrid;
            GridBackpackSystem.Instance.OnBackpackChanged += UpdateBackpackGrid;
        }
        RebindAfterSystemsReady();
        RefreshBattleHud();
        GameFonts.ApplyToHierarchy(transform);
        // Canvas 尺寸这时才是最终值，越界判断必须放在这之后
        ClampCharacterBarInsideParent();
        HeroThunderUltimate.Instance?.EnsureBattleUi();
        Debug.Log($"[BattleUI] HUD已刷新 — playerSlot={playerSlot?.root!=null} merc1={mercSlot1?.root!=null} merc2={mercSlot2?.root!=null} progressNodes={progressNodes?.Count} marker={playerMarker!=null}");
    }

    float _liveBarTimer;
    float _lastPlayerHp = -1f, _lastPlayerMaxHp = -1f, _lastPlayerEnergy = -1f;
    float _lastMerc1Hp = -1f, _lastMerc1Energy = -1f;
    float _lastMerc2Hp = -1f, _lastMerc2Energy = -1f;
    const float LiveBarInterval = 0.1f;

    void Update()
    {
        if (playerSlot != null && playerSlot.glowBorder != null && playerSlot.glowBorder.gameObject.activeSelf)
            playerSlot.TickSkillReadyPulse();
        if (playerSkillAvatar != null && playerSkillAvatar.IsReadyPulse)
            playerSkillAvatar.TickReadyPulse();

        _liveBarTimer += Time.deltaTime;
        if (_liveBarTimer < LiveBarInterval) return;
        _liveBarTimer = 0f;
        RefreshLiveBars();
    }

    void RefreshLiveBars()
    {
        var hero = Hero.Instance;
        if (playerSlot != null && hero != null && !hero.isDead)
        {
            float maxHp = hero.attr.GetAttr(AttrType.MaxHp);
            float energy = BattleManager.Instance != null ? BattleManager.Instance.playerSkillEnergy : 0f;
            if (!Mathf.Approximately(_lastPlayerHp, hero.currentHp)
                || !Mathf.Approximately(_lastPlayerMaxHp, maxHp)
                || !Mathf.Approximately(_lastPlayerEnergy, energy))
            {
                _lastPlayerHp = hero.currentHp;
                _lastPlayerMaxHp = maxHp;
                _lastPlayerEnergy = energy;
                playerSlot.UpdateSlot(PlayerIdentity.DisplayName, hero.level, hero.currentHp, maxHp, showLevel: false);
                playerSlot.SetEnergy(energy);
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
            float energy = BattleManager.Instance != null ? BattleManager.Instance.GetMercSkillEnergy(0) : 0f;
            if (!Mathf.Approximately(_lastMerc1Hp, m.currentHp) || !Mathf.Approximately(_lastMerc1Energy, energy))
            {
                _lastMerc1Hp = m.currentHp;
                _lastMerc1Energy = energy;
                mercSlot1.UpdateSlot(mm.GetJobName(m.mercId), m.mercLevel, m.currentHp, maxHp);
                mercSlot1.SetEnergy(energy);
            }
        }
        if (maxSlots > 1 && mercSlot2 != null && mercs != null && mercs.Count > 1 && mercs[1] != null)
        {
            var m = mercs[1];
            float maxHp = m.attr.GetAttr(AttrType.MaxHp);
            float energy = BattleManager.Instance != null ? BattleManager.Instance.GetMercSkillEnergy(1) : 0f;
            if (!Mathf.Approximately(_lastMerc2Hp, m.currentHp) || !Mathf.Approximately(_lastMerc2Energy, energy))
            {
                _lastMerc2Hp = m.currentHp;
                _lastMerc2Energy = energy;
                mercSlot2.UpdateSlot(mm.GetJobName(m.mercId), m.mercLevel, m.currentHp, maxHp);
                mercSlot2.SetEnergy(energy);
            }
        }
    }

    void OnDestroy()
    {
        if (GridBackpackSystem.Instance != null)
            GridBackpackSystem.Instance.OnBackpackChanged -= UpdateBackpackGrid;
        if (Instance == this)
            Instance = null;
    }

    /// <summary>战斗 HUD 全量刷新入口（章节/难度/资源/头像/背包）</summary>
    public void RefreshBattleHud()
    {
        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        int stageIdx = 0;
        string diff = "普通";
        if (BattleManager.Instance != null && BattleManager.Instance.currentStage != null)
        {
            stageIdx = BattleManager.Instance.currentStage.stageIndex;
            diff = StageTypeToDifficulty(BattleManager.Instance.currentStage.type);
        }
        long gold = BattleManager.Instance != null ? BattleManager.Instance.currentGold : 0;
        var save = SaveSystem.Instance?.Data;
        if (save != null && gold <= 0) gold = save.totalGold;

        UpdateStageInfo(chapter, stageIdx, diff, gold);
        ApplySoloBattleHud();
        UpdateCharacterSlots();
        UpdateSkillAvatars();
        UpdateSkillAvatars();
        UpdateBackpackGrid();
        UpdateStageProgress(stageIdx);

        // QuestText 任务默认文案（波次刷新后由 BattleManager 再改）
        if (questPanel != null && !questPanel.activeSelf)
            questPanel.SetActive(true);
        if (questDesc != null && string.IsNullOrEmpty(questDesc.text))
            UpdateQuest("击败所有敌人", 0, 3);
        RefreshQuestReward();
    }

    static Sprite _questGoldSprite;

    int _questRewardGold = 100;

    /// <summary>任务奖励区：金币图标 + 数量（预制体默认是宝箱图）。</summary>
    public void RefreshQuestReward(int goldAmount = -1)
    {
        if (goldAmount >= 0) _questRewardGold = goldAmount;
        else if (BattleManager.Instance != null && BattleManager.Instance.StageQuestClearGold > 0)
            _questRewardGold = BattleManager.Instance.StageQuestClearGold;

        if (questRewardIcon != null)
        {
            Sprite sp = LoadQuestGoldSprite();
            if (sp != null)
            {
                questRewardIcon.sprite = sp;
                questRewardIcon.preserveAspect = true;
            }
        }
        if (questRewardAmount != null)
            questRewardAmount.text = $"×{_questRewardGold}";
    }

    static Sprite LoadQuestGoldSprite()
    {
        if (_questGoldSprite != null) return _questGoldSprite;
        if (Instance == null) return null;
        Transform goldIcon = FindDeepChildIgnoreCase(Instance.transform, "GoldIcon");
        if (goldIcon != null)
        {
            var img = goldIcon.GetComponent<Image>();
            if (img != null && img.sprite != null)
                _questGoldSprite = img.sprite;
        }
        return _questGoldSprite;
    }

    static string StageTypeToDifficulty(StageType t)
    {
        switch (t)
        {
            case StageType.Elite: return "精英";
            case StageType.Boss: return "Boss";
            default: return "普通";
        }
    }

    /// <summary>按节点名自动补全未拖拽的引用</summary>
    void AutoBindMissingRefs()
    {
        if (stageLabel == null) stageLabel = FindUIText("StageLabel");
        if (difficultyLabel == null) difficultyLabel = FindUIText("DifficultyLabel");
        if (goldText == null) goldText = FindUIText("GoldText");
        if (talentStoneText == null)
            talentStoneText = FindUIText("TalentText") ?? FindUIText("TalentStoneText") ?? FindUIText("DiamondText");
        if (enchantStoneText == null) enchantStoneText = FindUIText("EnchantText");
        if (decomposeMatText == null) decomposeMatText = FindUIText("DecomposeText");

        // 若金币误绑到分解文字，纠正
        if (goldText != null && goldText.gameObject.name.IndexOf("Decompose", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var realGold = FindUIText("GoldText");
            if (realGold != null) goldText = realGold;
        }

        if (settingsButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "SettingsButton");
            if (t != null) settingsButton = t.GetComponent<Button>();
        }
        if (settingsPanel == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "SettingsPanel");
            if (t != null) settingsPanel = t.gameObject;
        }
        if (pausePanel == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "PausePanel");
            if (t != null) pausePanel = t.gameObject;
        }
        if (characterPanel == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "CharacterPanel");
            if (t != null) characterPanel = t.gameObject;
        }
        if (pauseButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "PauseButton");
            if (t != null) pauseButton = t.GetComponent<Button>();
        }
        if (characterButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "CharacterButton");
            if (t != null) characterButton = t.GetComponent<Button>();
        }
        if (autoButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "AutoButton");
            if (t != null) autoButton = t.GetComponent<Button>();
        }
        if (organizeButton == null)
        {
            Transform backpack = FindDeepChildIgnoreCase(transform, "BackpackPanel")
                ?? FindDeepChildIgnoreCase(transform, "Backpack");
            Transform t = null;
            if (backpack != null)
            {
                t = FindDeepChildIgnoreCase(backpack, "整理")
                    ?? FindDeepChildIgnoreCase(backpack, "整理Button")
                    ?? FindDeepChildIgnoreCase(backpack, "OrganizeButton")
                    ?? FindDeepChildIgnoreCase(backpack, "SortButton");
            }
            if (t == null)
            {
                t = FindDeepChildIgnoreCase(transform, "整理")
                    ?? FindDeepChildIgnoreCase(transform, "OrganizeButton")
                    ?? FindDeepChildIgnoreCase(transform, "SortButton");
            }
            if (t != null) organizeButton = t.GetComponent<Button>();
        }

        if (questDesc == null) questDesc = FindUIText("QuestDesc");
        if (questProgress == null) questProgress = FindUIText("QuestProgress");
        if (questTitle == null) questTitle = FindUIText("QuestTitle");
        if (questRewardIcon == null)
        {
            Transform rt = FindDeepChildIgnoreCase(transform, "jianglitubiao");
            if (rt != null) questRewardIcon = rt.GetComponent<Image>();
        }
        if (questRewardAmount == null)
        {
            Transform rt = FindDeepChildIgnoreCase(transform, "jianglishuzi");
            if (rt != null) questRewardAmount = rt.GetComponent<Text>();
        }
        if (questPanel == null)
        {
            Transform qt = FindDeepChildIgnoreCase(transform, "QuestPanel")
                ?? FindDeepChildIgnoreCase(transform, "QuestText")
                ?? FindDeepChildIgnoreCase(transform, "TaskPanel")
                ?? FindDeepChildIgnoreCase(transform, "Task");
            if (qt != null) questPanel = qt.gameObject;
            else if (questDesc != null) questPanel = questDesc.transform.parent?.gameObject;
        }

        BindProgressBar();
        // 引用一律按名字绑齐；未解锁槽只是不改 Fill / 不加 Button
        BindCharacterSlot(ref playerSlot, "PlayerSlot", true);
        BindCharacterSlot(ref mercSlot1, "MercSlot1", false);
        BindCharacterSlot(ref mercSlot2, "MercSlot2", false);

        BindSkillAvatar(ref playerSkillAvatar, "SkillBtn1", "PlayerSkill");
        BindSkillAvatar(ref merc1SkillAvatar, "SkillBtn2", "MercSkill1");
        BindSkillAvatar(ref merc2SkillAvatar, "SkillBtn3", "MercSkill2");

        EnsureGridCellsBound();
        FixCharacterBarLayout();
        // MercenaryManager 可能尚未创建，系统就绪后再 Wire / Configure
        WireSlotSkillClicks();
        ApplySoloBattleHud();
    }

    /// <summary>GameRoot/佣兵系统就绪后重绑：Fill、点击、进度条、槽位刷新</summary>
    public void RebindAfterSystemsReady()
    {
        BattleSideHud.EnsureOn(transform);
        BindProgressBar();
        ApplySoloBattleHud();
        int maxSlots = GameConfig.SOLO_PLAYER_BATTLE ? 0
            : MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;
        if (maxSlots > 0) ApplyFillBars(mercSlot1);
        if (maxSlots > 1) ApplyFillBars(mercSlot2);
        WireSlotSkillClicks();
        UpdateCharacterSlots();
        UpdateSkillAvatars();
        int stageIdx = BattleManager.Instance != null && BattleManager.Instance.currentStage != null
            ? BattleManager.Instance.currentStage.stageIndex : 0;
        UpdateStageProgress(stageIdx);
    }

    void BindProgressBar()
    {
        Transform t = FindDeepChildIgnoreCase(transform, "ProgressBar")
            ?? FindDeepChildIgnoreCase(transform, "Progress")
            ?? FindDeepChildIgnoreCase(transform, "StageProgress");
        if (t != null) progressContainer = t;
        if (progressContainer == null) return;

        Transform markerT = FindDeepChildIgnoreCase(progressContainer, "PlayerMarker");
        if (markerT != null)
        {
            playerMarker = markerT.GetComponent<Image>();
            if (playerMarker == null) playerMarker = markerT.GetComponentInChildren<Image>(true);
        }

        Transform flagT = FindDeepChildIgnoreCase(progressContainer, "EndFlag");
        if (flagT != null)
        {
            endFlag = flagT.GetComponent<Image>();
            if (endFlag == null) endFlag = flagT.GetComponentInChildren<Image>(true);
        }

        // 每次按 Node_0..Node_9 重建，丢掉预制体里多余的空 fileID
        progressNodes = new List<Image>(GameConfig.STAGES_PER_CHAPTER);
        for (int i = 0; i < GameConfig.STAGES_PER_CHAPTER; i++)
        {
            Transform n = FindDeepChildIgnoreCase(progressContainer, $"Node_{i}");
            if (n == null) continue;
            var img = n.GetComponent<Image>();
            if (img == null)
            {
                img = n.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = false;
            }
            progressNodes.Add(img);
        }
    }

    static void ApplyFillBars(CharacterSlotUI slot)
    {
        if (slot == null) return;
        ConfigureFillBar(slot.hpBarFill);
        ConfigureFillBar(slot.lanBarFill);
        ConfigureFillBar(slot.energyRing);
    }

    void BindCharacterSlot(ref CharacterSlotUI slot, string rootName, bool configureFills = true)
    {
        if (slot == null) slot = new CharacterSlotUI();
        if (slot.root == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, rootName);
            if (t != null) slot.root = t.gameObject;
        }
        if (slot.root == null) return;
        Transform root = slot.root.transform;

        if (slot.portrait == null)
        {
            // 用户约定：Portrait 是头像位；PlayerSlot 是头像框背景，不能被当成头像图层
            Transform portraitRoot = FindDeepChildIgnoreCase(root, "Portrait");
            if (portraitRoot != null)
                slot.portrait = portraitRoot.GetComponent<Image>();
            if (slot.portrait == null)
                slot.portrait = FindImageNamedNoFallback(root, "Portrait");
        }
        // 占位图：有真实头像时隐藏
        if (slot.portraitPlaceholder == null)
        {
            Transform ph = FindDeepChildIgnoreCase(root, "PortraitPlaceholder");
            if (ph != null) slot.portraitPlaceholder = ph.gameObject;
        }

        if (slot.levelLabel == null)
            slot.levelLabel = FindTextNamed(root, "LevelLabel", "Level", "Lv");
        if (slot.nameText == null)
            slot.nameText = FindTextNamed(root, "Name", "NameText", "PlayerName");

        // HP：优先 HPBarBg/HPBarFill，避免误绑到底板
        if (slot.hpBarFill == null)
        {
            Transform hpBg = FindDeepChildIgnoreCase(root, "HPBarBg")
                ?? FindDeepChildIgnoreCase(root, "HPBarBG");
            if (hpBg != null)
                slot.hpBarFill = FindImageNamedNoFallback(hpBg, "HPBarFill", "Fill");
            if (slot.hpBarFill == null)
                slot.hpBarFill = FindImageNamedNoFallback(root, "HPBarFill", "HPFill", "HpFill");
        }
        if (slot.hpText == null)
            slot.hpText = FindTextNamed(root, "HPText", "HpText");

        // 蓝条/技能能量：lanBarBg/lanBarFill
        if (slot.lanBarFill == null)
        {
            Transform lanBg = FindDeepChildIgnoreCase(root, "lanBarBg")
                ?? FindDeepChildIgnoreCase(root, "LanBarBg");
            if (lanBg != null)
                slot.lanBarFill = FindImageNamedNoFallback(lanBg, "lanBarFill", "LanBarFill", "Fill");
            if (slot.lanBarFill == null)
                slot.lanBarFill = FindImageNamedNoFallback(root, "lanBarFill", "LanBarFill");
        }
        if (slot.lanText == null)
            slot.lanText = FindTextNamed(root, "lanText", "LanText");

        // 兼容旧能量环命名
        if (slot.energyRing == null)
            slot.energyRing = FindImageNamedNoFallback(root, "Energy", "EnergyRing", "Ring");

        if (slot.glowBorder == null)
        {
            Transform g = FindDeepChildIgnoreCase(root, "Glow")
                ?? FindDeepChildIgnoreCase(root, "GlowBorder")
                ?? FindDeepChildIgnoreCase(root, "SkillGlow");
            if (g != null) slot.glowBorder = g.GetComponent<Image>();
        }
        // 按用户要求：不对头像框做任何运行时改造，不再改尺寸/新增节点

        if (slot.lockedOverlay == null)
        {
            Transform l = FindDeepChildIgnoreCase(root, "LockedOverlay")
                ?? FindDeepChildIgnoreCase(root, "Locked")
                ?? FindDeepChildIgnoreCase(root, "Lock");
            if (l != null) slot.lockedOverlay = l.gameObject;
        }

        // 未解锁槽：不改 Image.Filled，完全保留美术默认
        if (configureFills) ApplyFillBars(slot);
    }

    static void ConfigureFillBar(Image img)
    {
        if (img == null) return;
        if (img.type != Image.Type.Filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    void ApplySoloBattleHud()
    {
        // 单人模式也保留三个头像位：未解锁显示「锁定」，不要藏掉第 3 个
        bool showTutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        SetSlotRootActive(playerSlot, true);
        SetSlotRootActive(mercSlot1, true);
        SetSlotRootActive(mercSlot2, true);

        // 单人/引导：两格伙伴都按未解锁处理，清掉占位血量数字
        bool lockExtraSlots = (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle) && !showTutorialMerc;
        if (lockExtraSlots)
            mercSlot1?.ShowUnavailable("未解锁");
        else
            mercSlot1?.SetLocked(false);

        if (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle)
            mercSlot2?.ShowUnavailable("未解锁");
        else
            mercSlot2?.SetLocked(false);

        SetAvatarRootActive(merc1SkillAvatar, showTutorialMerc || !(GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle));
        SetAvatarRootActive(merc2SkillAvatar, !(GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle));
    }

    public void ApplySoloBattleHudPublic() => ApplySoloBattleHud();

    static void SetSlotRootActive(CharacterSlotUI slot, bool active)
    {
        if (slot?.root != null) slot.root.SetActive(active);
    }

    static void SetAvatarRootActive(SkillAvatarUI avatar, bool active)
    {
        if (avatar?.root != null) avatar.root.SetActive(active);
    }

    void WireSlotSkillClicks()
    {
        WireSlotClick(playerSlot, OnPlayerSkillClick);
        bool tutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        if ((GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle) && !tutorialMerc) return;
        if ((GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle) && tutorialMerc)
        {
            WireSlotClick(mercSlot1, () => OnMercSkillClick(0));
            return;
        }
        int maxSlots = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;
        if (maxSlots > 0)
            WireSlotClick(mercSlot1, () => OnMercSkillClick(0));
        if (maxSlots > 1)
            WireSlotClick(mercSlot2, () => OnMercSkillClick(1));
        // 未解锁槽：不加 Button，保持美术默认
    }

    static void WireSlotClick(CharacterSlotUI slot, UnityEngine.Events.UnityAction action)
    {
        if (slot?.root == null || action == null) return;
        Button btn = slot.root.GetComponent<Button>();
        if (btn == null) btn = slot.root.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
        // 头像也可点
        if (slot.portrait != null)
        {
            var pBtn = slot.portrait.GetComponent<Button>();
            if (pBtn == null) pBtn = slot.portrait.gameObject.AddComponent<Button>();
            pBtn.transition = Selectable.Transition.None;
            pBtn.onClick.RemoveAllListeners();
            pBtn.onClick.AddListener(action);
        }
    }

    void BindSkillAvatar(ref SkillAvatarUI avatar, params string[] names)
    {
        if (avatar == null) avatar = new SkillAvatarUI();
        if (avatar.root != null) return;
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(transform, names[i]);
            if (t == null) continue;
            avatar.root = t.gameObject;
            if (avatar.avatarImage == null)
                avatar.avatarImage = FindImageNamed(t, "Avatar", "Icon", "Portrait", "Mask");
            if (avatar.energyRing == null)
                avatar.energyRing = FindImageNamed(t, "Energy", "EnergyRing", "Ring");
            if (avatar.glowBorder == null)
            {
                Transform g = FindDeepChildIgnoreCase(t, "Glow");
                if (g != null) avatar.glowBorder = g.GetComponent<Image>();
            }
            break;
        }
    }

    void EnsureGridCellsBound()
    {
        Transform grid = FindDeepChildIgnoreCase(transform, "GridContainer");
        if (grid == null) return;
        if (gridLayout == null) gridLayout = grid.GetComponent<GridLayoutGroup>();

        // 用户已在 GridContainer 下放了底行锁图案 LockedOverlay，不要再造黑色遮罩
        if (_backpackRowLock == null)
        {
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform c = grid.GetChild(i);
                if (c.name.IndexOf("Locked", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || c.name.Equals("Lock", System.StringComparison.OrdinalIgnoreCase))
                {
                    _backpackRowLock = c.gameObject;
                    break;
                }
            }
        }

        var list = new List<GridCellUI>();
        for (int i = 0; i < grid.childCount; i++)
        {
            Transform cell = grid.GetChild(i);
            // 跳过整行锁遮罩节点
            if (cell.name.IndexOf("Locked", System.StringComparison.OrdinalIgnoreCase) >= 0
                || cell.name.Equals("Lock", System.StringComparison.OrdinalIgnoreCase))
                continue;

            int gx = i % GameConfig.BACKPACK_WIDTH;
            int gy = i / GameConfig.BACKPACK_WIDTH;
            string n = cell.name;
            if (n.StartsWith("Cell_", System.StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = n.Split('_');
                if (parts.Length >= 3
                    && int.TryParse(parts[1], out int px)
                    && int.TryParse(parts[2], out int py))
                {
                    gx = px;
                    gy = py;
                }
            }

            var ui = new GridCellUI
            {
                root = cell.gameObject,
                cellBg = FindImageNamed(cell, "CellBg", "Bg", "Background")
                    ?? cell.GetComponent<Image>(),
                itemIcon = FindImageNamed(cell, "Icon", "ItemIcon"),
                rarityFrame = FindImageNamed(cell, "Frame", "Rarity", "Border"),
                lockedOverlay = FindDeepChildIgnoreCase(cell, "LockedOverlay")?.gameObject
                    ?? FindDeepChildIgnoreCase(cell, "Locked")?.gameObject,
                gridX = gx,
                gridY = gy
            };
            ui.CaptureDefaultVisual();
            list.Add(ui);
        }
        gridCells = list;
    }

    GameObject _backpackRowLock; // GridContainer 下用户放的底行锁图案

    Text FindUIText(string name)
    {
        Transform t = FindDeepChildIgnoreCase(transform, name);
        return t != null ? t.GetComponent<Text>() : null;
    }

    static Image FindImageNamed(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(root, names[i]);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        // 退而求其次：根上的 Image
        return root.GetComponent<Image>();
    }

    /// <summary>
    /// 严格查找：只按名字找，不回退 root Image（避免把 PlayerSlot 背景误绑成头像/血条）
    /// </summary>
    static Image FindImageNamedNoFallback(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(root, names[i]);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return null;
    }

    static Text FindTextNamed(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(root, names[i]);
            if (t != null)
            {
                var tx = t.GetComponent<Text>();
                if (tx != null) return tx;
            }
        }
        return null;
    }

    static Transform FindDeepChildIgnoreCase(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        if (string.Equals(parent.name, name, System.StringComparison.OrdinalIgnoreCase))
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeepChildIgnoreCase(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>
    /// 切换自动战斗
    /// </summary>
    void ToggleAutoBattle()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.isAutoBattle = !BattleManager.Instance.isAutoBattle;
            if (autoButton != null)
            {
                var txt = autoButton.GetComponentInChildren<Text>();
                if (txt != null) txt.text = BattleManager.Instance.isAutoBattle ? "停止" : "自动";
            }
        }
    }

    /// <summary>
    /// 暂停：打开暂停面板
    /// </summary>
    void OnPause()
    {
        if (pausePanel != null)
        {
            var panel = pausePanel.GetComponent<PausePanel>();
            if (panel != null)
            {
                panel.Show();
            }
            else
            {
                // 兜底：如果没有PausePanel组件，简单切换timeScale
                pausePanel.SetActive(!pausePanel.activeSelf);
                Time.timeScale = pausePanel.activeSelf ? 0f : 1f;
            }
        }
    }

    /// <summary>
    /// 玩家技能释放（点击头像）
    /// </summary>
    void OnPlayerSkillClick()
    {
        if (TutorialDirector.IsTutorialBattle
            && TutorialDirector.Instance != null
            && !TutorialDirector.Instance.AllowBattleSkillClick)
            return;

        if (BattleManager.Instance != null)
        {
            bool success = BattleManager.Instance.TryUsePlayerSkill();
            if (!success)
                UIManager.Instance?.ShowToast("技能能量不足");
        }
    }

    void OnMercSkillClick(int mercIndex)
    {
        if (BattleManager.Instance == null) return;
        bool success = BattleManager.Instance.TryUseMercSkill(mercIndex);
        if (!success)
            UIManager.Instance?.ShowToast("佣兵技能未就绪");
    }

    /// <summary>
    /// 更新关卡信息（章节、难度、金币）并刷新资源条
    /// </summary>
    public void UpdateStageInfo(int chapter, int stage, string difficulty, long gold)
    {
        bool hideStageInfo = TutorialDirector.IsTutorialBattle;
        if (stageLabel != null)
        {
            // 连底框一起藏：文字挂在 StageIcon 上，只藏文字会留一个空壳
            SetLabelWithFrameVisible(stageLabel, !hideStageInfo);
            if (!hideStageInfo)
                stageLabel.text = $"第{chapter}章";
        }
        if (difficultyLabel != null)
        {
            SetLabelWithFrameVisible(difficultyLabel, !hideStageInfo);
            if (!hideStageInfo)
                difficultyLabel.text = string.IsNullOrEmpty(difficulty) ? "普通" : difficulty;
        }
        UpdateGold(gold);
        UpdateTopBarResources();
    }

    /// <summary>
    /// 章节/难度标签连同它的底框一起显示或隐藏。
    /// 标签是底框（StageIcon / DifficultyIcon）的子节点，所以往上找一层带 Image 的父节点整块关掉。
    /// </summary>
    static void SetLabelWithFrameVisible(Text label, bool visible)
    {
        if (label == null) return;
        Transform parent = label.transform.parent;
        // 父节点自己有图（就是底框）时关父节点；否则退化成只关文字
        if (parent != null && parent.GetComponent<Image>() != null
            && parent.childCount <= 3)
        {
            parent.gameObject.SetActive(visible);
            return;
        }
        label.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 更新顶部资源：金币 / 天赋石 / 材料；附魔石有独立文本则另刷
    /// </summary>
    public void UpdateTopBarResources()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        if (talentStoneText != null)
            talentStoneText.text = data.talentPoints.ToString();
        else if (enchantStoneText != null)
            // 旧布局三资源：金 / 天赋石(占附魔位) / 材料
            enchantStoneText.text = data.talentPoints.ToString();

        if (talentStoneText != null && enchantStoneText != null)
            enchantStoneText.text = data.enchantStones.ToString();

        if (decomposeMatText != null)
            decomposeMatText.text = data.decomposeMats.ToString();
    }

    /// <summary>更新技能区圆形头像</summary>
    public void UpdateSkillAvatars()
    {
        var mm = MercenaryManager.Instance;
        if (playerSkillAvatar != null)
            playerSkillAvatar.SetAvatar(mm != null ? mm.GetPlayerIcon() : null);

        bool tutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        if (GameConfig.SOLO_PLAYER_BATTLE && !tutorialMerc) return;

        if (tutorialMerc)
        {
            var mercs = mm != null ? mm.GetActiveMercs() : null;
            Mercenary m0 = (mercs != null && mercs.Count > 0) ? mercs[0] : null;
            Sprite icon = GetMercSkillIcon(m0) ?? (m0 != null ? MercPortraitSprites.GetHead(!string.IsNullOrEmpty(m0.hireId) ? m0.hireId : m0.mercId) : null)
                ?? (m0 != null && mm != null ? mm.GetIcon(m0.mercId) : null);
            merc1SkillAvatar?.SetAvatar(icon);
            if (mercSlot1 != null && m0 != null)
                mercSlot1.SetEnergyEnabled(m0.SkillCaster != null && m0.SkillCaster.HasActiveSkill && !MercSkillMigrate.IsMercSkillAutoCast());
            return;
        }

        var mercIds = mm != null ? mm.GetActiveMercIds() : new List<string>();
        var mercsList = mm != null ? mm.GetActiveMercs() : null;
        if (merc1SkillAvatar != null)
            merc1SkillAvatar.SetAvatar(GetMercSkillIconAt(mercsList, 0) ?? GetMercPortraitAt(mm, mercIds, 0));
        if (merc2SkillAvatar != null)
            merc2SkillAvatar.SetAvatar(GetMercSkillIconAt(mercsList, 1) ?? GetMercPortraitAt(mm, mercIds, 1));
    }

    static Sprite GetMercSkillIcon(Mercenary m)
    {
        if (m?.SkillCaster == null || !m.SkillCaster.HasActiveSkill) return null;
        return MercSkillTable.LoadIcon(m.SkillCaster.ActiveSkillId);
    }

    static Sprite GetMercSkillIconAt(List<Mercenary> mercs, int index)
    {
        if (mercs == null || index < 0 || index >= mercs.Count) return null;
        return GetMercSkillIcon(mercs[index]);
    }

    static Sprite GetMercPortraitAt(MercenaryManager mm, List<string> mercIds, int index)
    {
        if (mm == null || mercIds == null || index < 0 || index >= mercIds.Count) return null;
        var mercs = mm.GetActiveMercs();
        if (mercs != null && index < mercs.Count && mercs[index] != null && !string.IsNullOrEmpty(mercs[index].hireId))
            return MercPortraitSprites.GetHead(mercs[index].hireId) ?? mm.GetIcon(mercs[index].mercId);
        var hireIds = mm.GetActiveMercHireIds();
        if (hireIds != null && index < hireIds.Count && !string.IsNullOrEmpty(hireIds[index]))
            return MercPortraitSprites.GetHead(hireIds[index]) ?? mm.GetIcon(mercIds[index]);
        return mm.GetIcon(mercIds[index]);
    }

    /// <summary>刷新下方网格背包（只锁最底一行 y=3，用你放的锁图案）</summary>
    public void UpdateBackpackGrid()
    {
        // 战斗中捡到装备时可能还没绑过格子，先补绑再判空
        if (gridCells == null || gridCells.Count == 0)
            EnsureGridCellsBound();
        if (gridCells == null || gridCells.Count == 0)
        {
            Debug.LogWarning("[BattleUI] 背包格子未绑定（缺 GridContainer），装备无法显示");
            return;
        }

        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        bool bottomLocked = unlockedRows < GameConfig.BACKPACK_HEIGHT;

        // 整行锁图案（GridContainer/LockedOverlay）
        if (_backpackRowLock != null)
            _backpackRowLock.SetActive(bottomLocked);

        foreach (var cell in gridCells)
        {
            if (cell == null) continue;
            bool rowLocked = cell.gridY >= unlockedRows;
            cell.SetRowLocked(rowLocked);
            if (!rowLocked)
                cell.Clear();
        }

        var bag = GridBackpackSystem.Instance;
        if (bag == null) return;

        var items = bag.GetAllBackpackItems();
        if (items == null) return;

        var placements = new List<BackpackGridVisual.ItemPlacement>();
        Transform grid = FindDeepChildIgnoreCase(transform, "GridContainer");
        var gridRt = grid as RectTransform;
        foreach (var bip in items)
        {
            if (bip == null || bip.equip == null) continue;
            if (bip.y >= unlockedRows) continue;
            placements.Add(new BackpackGridVisual.ItemPlacement
            {
                x = bip.x, y = bip.y, w = bip.width, h = bip.height, equip = bip.equip,
                equipped = bag.IsEquipped(bip.equip)
            });
        }
        // 传入真实格子：没有 GridLayoutGroup（格子是美术手摆的）时也能算对位置
        BackpackGridVisual.ClearAndPlace(gridRt, gridLayout, placements, FindGridCellRect);
        ApplyBackpackCellOccupiedColors(placements);
        Debug.Log($"[BattleUI] 背包刷新 items={placements.Count} cells={gridCells.Count} layout={(gridLayout != null)}");
    }

    void ApplyBackpackCellOccupiedColors(List<BackpackGridVisual.ItemPlacement> placements)
    {
        if (gridCells == null) return;
        foreach (var cell in gridCells)
        {
            if (cell == null) continue;
            cell.SetEmptyVisual();
        }
        if (placements == null) return;
        for (int i = 0; i < placements.Count; i++)
        {
            var p = placements[i];
            for (int dx = 0; dx < p.w; dx++)
            for (int dy = 0; dy < p.h; dy++)
            {
                var cell = FindGridCell(p.x + dx, p.y + dy);
                cell?.SetOccupiedVisual(p.equipped);
            }
        }
    }

    GridCellUI FindGridCell(int gx, int gy)
    {
        if (gridCells == null) return null;
        for (int i = 0; i < gridCells.Count; i++)
        {
            var c = gridCells[i];
            if (c != null && c.gridX == gx && c.gridY == gy)
                return c;
        }
        return null;
    }

    /// <summary>按格子坐标取真实格子的 RectTransform，供多格装备量取实际占位。</summary>
    RectTransform FindGridCellRect(int gx, int gy)
    {
        if (gridCells == null) return null;
        for (int i = 0; i < gridCells.Count; i++)
        {
            var c = gridCells[i];
            if (c != null && c.gridX == gx && c.gridY == gy)
                return c.VisualRect;
        }
        return null;
    }

    /// <summary>
    /// 更新任务信息
    /// </summary>
    public void UpdateQuest(string desc, int current, int total, int rewardGold = -1)
    {
        if (questDesc != null) questDesc.text = desc;
        if (questProgress != null) questProgress.text = $"({current}/{total})";
        RefreshQuestReward(rewardGold);
    }

    /// <summary>
    /// 更新角色栏
    /// </summary>
    public void UpdateCharacterSlots()
    {
        var mm = MercenaryManager.Instance;

        // 玩家槽位
        if (playerSlot != null)
        {
            if (playerSlot.lockedOverlay != null)
                playerSlot.lockedOverlay.SetActive(false);
            var hero = Hero.Instance;
            if (hero != null)
            {
                float maxHp = hero.attr.GetAttr(AttrType.MaxHp);
                playerSlot.UpdateSlot(PlayerIdentity.DisplayName, hero.level, hero.currentHp, maxHp, showLevel: false);
            }
            // 玩家头像对接
            Sprite playerIcon = mm != null ? mm.GetPlayerIcon() : null;
            playerSlot.SetPortrait(playerIcon);
        }

        if (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle)
        {
            ApplySoloBattleHud();
            if (TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud)
                RefreshTutorialMercSlot(mm);
            return;
        }

        // 佣兵槽位（根据酒馆等级解锁 + 存档出战佣兵）
        var mercIds = mm != null ? mm.GetActiveMercIds() : new List<string>();
        var mercHireIds = mm != null ? mm.GetActiveMercHireIds() : new List<string>();
        var activeMercs = mm != null ? mm.GetActiveMercs() : new List<Mercenary>();
        int maxSlots = mm != null ? mm.GetMaxMercSlots() : 0;

        SetupMercSlot(mercSlot1, 0, mercIds, mercHireIds, activeMercs, maxSlots, mm);
        SetupMercSlot(mercSlot2, 1, mercIds, mercHireIds, activeMercs, maxSlots, mm);
    }

    void RefreshTutorialMercSlot(MercenaryManager mm)
    {
        if (mercSlot1 == null || mm == null) return;
        var mercs = mm.GetActiveMercs();
        if (mercs == null || mercs.Count == 0 || mercs[0] == null) return;
        var m = mercs[0];
        mercSlot1.SetLocked(false);
        bool tutHasActive = m.SkillCaster != null && m.SkillCaster.HasActiveSkill;
        mercSlot1.SetEnergyEnabled(tutHasActive && !MercSkillMigrate.IsMercSkillAutoCast());
        Sprite mercIcon = MercPortraitSprites.GetHead(!string.IsNullOrEmpty(m.hireId) ? m.hireId : "H001")
            ?? mm.GetIcon(m.mercId);
        mercSlot1.SetPortrait(mercIcon);
        // 教程老盾不在存档出战列表里，技能圆形头像要单独绑
        merc1SkillAvatar?.SetAvatar(mercIcon);
        // 没配头像时也不要露出「头像」占位白框
        if (mercIcon == null && mercSlot1.portraitPlaceholder != null)
            mercSlot1.portraitPlaceholder.SetActive(false);
        float maxHp = m.attr.GetAttr(AttrType.MaxHp);
        mercSlot1.UpdateSlot("老盾", m.mercLevel, m.currentHp, maxHp);
    }

    void RefreshTutorialMercLiveBar()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || mercSlot1 == null) return;
        var mercs = mm.GetActiveMercs();
        if (mercs == null || mercs.Count == 0 || mercs[0] == null) return;
        var m = mercs[0];
        float maxHp = m.attr.GetAttr(AttrType.MaxHp);
        mercSlot1.UpdateSlot("老盾", m.mercLevel, m.currentHp, maxHp);
        mercSlot1.SetEnergy(BattleManager.Instance != null ? BattleManager.Instance.GetMercSkillEnergy(0) : 0f);
    }

    /// <summary>
    /// 设置单个佣兵槽位显示
    /// </summary>
    void SetupMercSlot(CharacterSlotUI slot, int index,
        List<string> mercIds, List<string> mercHireIds, List<Mercenary> activeMercs, int maxSlots, MercenaryManager mm)
    {
        if (slot == null) return;

        // 酒馆未解锁的槽：显示「未解锁」
        bool unlocked = index < maxSlots;
        if (!unlocked)
        {
            slot.ShowUnavailable("未解锁");
            return;
        }

        if (index < mercIds.Count)
        {
            slot.SetLocked(false);
            bool hasActive = index < activeMercs.Count
                && activeMercs[index] != null
                && activeMercs[index].SkillCaster != null
                && activeMercs[index].SkillCaster.HasActiveSkill;
            slot.SetEnergyEnabled(hasActive && !MercSkillMigrate.IsMercSkillAutoCast());
            string id = mercIds[index];
            string hireId = index < mercHireIds.Count ? mercHireIds[index] : null;
            if (index < activeMercs.Count && activeMercs[index] != null && !string.IsNullOrEmpty(activeMercs[index].hireId))
                hireId = activeMercs[index].hireId;
            Sprite icon = MercPortraitSprites.GetHead(hireId) ?? MercPortraitSprites.GetHead(id) ?? (mm != null ? mm.GetIcon(id) : null);
            string job = mm != null ? mm.GetJobName(id) : id;
            slot.SetPortrait(icon);

            if (index < activeMercs.Count && activeMercs[index] != null)
            {
                var m = activeMercs[index];
                float maxHp = m.attr.GetAttr(AttrType.MaxHp);
                slot.UpdateSlot(job, m.mercLevel, m.currentHp, maxHp);
            }
            else
            {
                // 出战名单有占位但单位未生成：也不涨蓝条
                slot.SetEnergyEnabled(false);
                slot.UpdateSlot(job, 1, 0, 0);
            }
        }
        else
        {
            // 已解锁但无佣兵：空槽占位
            slot.ShowEmpty();
        }
    }

    /// <summary>角色栏：禁止 ForceExpand 把槽拉扁；不强制 childControl（否则无 LayoutElement 会被压成 0）</summary>
    void FixCharacterBarLayout()
    {
        Transform bar = FindDeepChildIgnoreCase(transform, "CharacterBar");
        if (bar == null) return;
        var hlg = bar.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            // 保留美术预制体的 childControl*，不要改成 true
        }

        // 只给已有 LayoutElement 的槽关 flex，未解锁槽不 AddComponent
        SoftFixLayoutElement(playerSlot?.root);
        SoftFixLayoutElement(mercSlot1?.root);
        SoftFixLayoutElement(mercSlot2?.root);
    }

    /// <summary>
    /// 头像栏保留美术摆的位置，但不许超出父容器：
    /// 窄屏/高屏下预制体的固定偏移会把整条栏顶到框外，这里只把越界的部分推回来。
    /// </summary>
    public void ClampCharacterBarInsideParent()
    {
        Transform t = FindDeepChildIgnoreCase(transform, "CharacterBar");
        var bar = t as RectTransform;
        if (bar == null) return;
        var parent = bar.parent as RectTransform;
        if (parent == null) return;

        // 布局这一帧可能还没算完，先强制刷新再量
        LayoutRebuilder.ForceRebuildLayoutImmediate(bar);

        Rect pr = parent.rect;
        // Canvas 还没定尺寸时不要动，否则会把栏推到错的地方
        if (pr.width <= 1f || pr.height <= 1f) return;

        float halfH = bar.rect.height * 0.5f;
        float halfW = bar.rect.width * 0.5f;
        if (halfH <= 0.01f || halfW <= 0.01f) return;

        // bar 在 parent 局部空间里的中心
        Vector3 center = parent.InverseTransformPoint(bar.TransformPoint(bar.rect.center));
        const float margin = 6f;

        float minY = pr.yMin + halfH + margin;
        float maxY = pr.yMax - halfH - margin;
        float minX = pr.xMin + halfW + margin;
        float maxX = pr.xMax - halfW - margin;

        float wantY = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : (pr.yMin + pr.yMax) * 0.5f;
        float wantX = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : (pr.xMin + pr.xMax) * 0.5f;

        float dx = wantX - center.x;
        float dy = wantY - center.y;
        if (Mathf.Abs(dx) < 0.5f && Mathf.Abs(dy) < 0.5f) return;

        bar.anchoredPosition += new Vector2(dx, dy);
        Debug.Log($"[BattleUI] CharacterBar 越界已推回 dx={dx:F1} dy={dy:F1} " +
                  $"size={bar.rect.width:F0}x{bar.rect.height:F0} parent={pr.width:F0}x{pr.height:F0}");
    }

    static void SoftFixLayoutElement(GameObject root)
    {
        if (root == null) return;
        var le = root.GetComponent<UnityEngine.UI.LayoutElement>();
        if (le == null) return; // 不新增组件
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        le.ignoreLayout = false;
    }

    /// <summary>
    /// 更新金币显示
    /// </summary>
    public void UpdateGold(long gold)
    {
        if (goldText != null) goldText.text = gold.ToString();
    }

    /// <summary>
    /// 更新进度条：按关卡索引把 PlayerMarker 挂到对应 Node 下；Boss 通关后挂到 EndFlag
    /// </summary>
    public void UpdateProgress(float progress)
    {
        UpdateStageProgress(-1, progress, false);
    }

    /// <param name="stageIndex">0-based 关卡；&lt;0 时用 progress 0~1</param>
    /// <param name="atEndFlag">打完 Boss 后停在最右旗子</param>
    public void UpdateStageProgress(int stageIndex, float progress = -1f, bool atEndFlag = false)
    {
        if (progressContainer == null) BindProgressBar();
        if (playerMarker == null) return;

        RectTransform markerRT = playerMarker.rectTransform;
        if (atEndFlag && endFlag != null)
        {
            AttachMarkerUnder(endFlag.transform);
            return;
        }

        if (progressNodes != null && progressNodes.Count > 0 && stageIndex >= 0)
        {
            int idx = Mathf.Clamp(stageIndex, 0, progressNodes.Count - 1);
            if (progressNodes[idx] != null)
            {
                AttachMarkerUnder(progressNodes[idx].transform);
                return;
            }
        }

        // 兜底：仍挂在 ProgressBar 下，按 0~1 插值
        if (playerMarker.transform.parent != progressContainer)
            playerMarker.transform.SetParent(progressContainer, false);
        if (progress < 0f) progress = 0f;
        float containerWidth = ((RectTransform)progressContainer).rect.width;
        Vector2 pos = markerRT.anchoredPosition;
        pos.x = Mathf.Lerp(-containerWidth * 0.45f, containerWidth * 0.45f, Mathf.Clamp01(progress));
        markerRT.anchoredPosition = pos;
    }

    void AttachMarkerUnder(Transform parent)
    {
        if (playerMarker == null || parent == null) return;
        if (playerMarker.transform.parent != parent)
            playerMarker.transform.SetParent(parent, false);
        RectTransform markerRT = playerMarker.rectTransform;
        markerRT.anchorMin = new Vector2(0.5f, 0.5f);
        markerRT.anchorMax = new Vector2(0.5f, 0.5f);
        markerRT.pivot = new Vector2(0.5f, 0.5f);
        markerRT.anchoredPosition = Vector2.zero;
        markerRT.localScale = Vector3.one;
        playerMarker.transform.SetAsLastSibling();
    }

    void OnOrganizeBackpack()
    {
        GridBackpackSystem.Instance?.OrganizeBackpack();
    }

    /// <summary>
    /// 更新技能头像能量（玩家=0, 佣兵1=1, 佣兵2=2）
    /// </summary>
    public void UpdateSkillEnergy(int skillIndex, float energyRatio)
    {
        if (skillIndex == 0 && playerSlot != null)
        {
            playerSlot.SetEnergy(energyRatio);
        }
        else if (skillIndex == 1 && mercSlot1 != null)
        {
            if (!mercSlot1.EnergyEnabled) energyRatio = 0f;
            mercSlot1.SetEnergy(energyRatio);
        }
        else if (skillIndex == 2 && mercSlot2 != null)
        {
            if (!mercSlot2.EnergyEnabled) energyRatio = 0f;
            mercSlot2.SetEnergy(energyRatio);
        }
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

    static readonly System.Collections.Generic.Dictionary<Transform, Vector3> _killCamHudBaseScales =
        new System.Collections.Generic.Dictionary<Transform, Vector3>();

    static readonly string[] KillCamHudNodeNames =
    {
        "TopBar", "TopStatus", "ProgressBar",
        "QuestText", "QuestPanel", "BackpackPanel",
        "BottomBar", "SkillBar", "PausePanel", "SettingsPanel",
        "CharacterBar"
    };

    /// <summary>击杀镜头：HUD 反缩放，避免 ortho zoom 把界面一起放大。</summary>
    public static void ApplyKillCamHudCompensation(float zoomMul)
    {
        if (Instance == null || zoomMul <= 0.01f) return;
        float inv = 1f / zoomMul;
        for (int i = 0; i < KillCamHudNodeNames.Length; i++)
        {
            Transform t = FindChildIgnoreCase(Instance.transform, KillCamHudNodeNames[i]);
            if (t == null) continue;
            if (!_killCamHudBaseScales.ContainsKey(t))
                _killCamHudBaseScales[t] = t.localScale;
            Vector3 b = _killCamHudBaseScales[t];
            t.localScale = new Vector3(b.x * inv, b.y * inv, b.z * inv);
        }
    }

    public static void ResetKillCamHudCompensation()
    {
        foreach (var kv in _killCamHudBaseScales)
        {
            if (kv.Key != null)
                kv.Key.localScale = kv.Value;
        }
        _killCamHudBaseScales.Clear();
    }

    static Transform FindChildIgnoreCase(Transform root, string name)
    {
        if (root == null) return null;
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildIgnoreCase(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 关闭所有面板
    /// </summary>
    public void CloseAllPanels()
    {
        if (characterPanel != null) characterPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (SettingsPopupUI.Instance != null && SettingsPopupUI.Instance.IsOpen)
            SettingsPopupUI.Instance.Close();
    }
}

/// <summary>
/// 技能头像UI：圆形头像 + 底部能量槽 + 能量满时金色光边
/// </summary>
[System.Serializable]
public class SkillAvatarUI
{
    public GameObject root;           // 根对象
    public Image avatarImage;         // 圆形头像（Mask裁剪）
    public Image energyRing;          // 能量环（圆形填充）
    public Image glowBorder;          // 光边（能量满时显示）
    public Text cooldownText;         // 冷却倒计时文字

    [System.NonSerialized]
    public System.Action onClick;     // 点击回调

    private bool _isReady = false;

    public bool IsReadyPulse => _isReady;

    /// <summary>
    /// 设置能量比例 0~1
    /// </summary>
    public void SetEnergy(float ratio)
    {
        if (energyRing != null)
        {
            energyRing.fillAmount = Mathf.Clamp01(ratio);
        }

        bool ready = ratio >= 1f;
        if (ready != _isReady)
        {
            _isReady = ready;
            if (glowBorder != null)
            {
                glowBorder.gameObject.SetActive(ready);
                if (ready)
                    glowBorder.color = new Color(1f, 0.85f, 0.15f, 0.85f);
            }
        }
    }

    public void TickReadyPulse()
    {
        if (!_isReady || glowBorder == null || !glowBorder.gameObject.activeSelf) return;
        float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f));
        var c = glowBorder.color;
        c.r = 1f;
        c.g = 0.85f;
        c.b = 0.15f;
        c.a = a;
        glowBorder.color = c;
    }

    /// <summary>
    /// 设置冷却文字
    /// </summary>
    public void SetCooldownText(string text)
    {
        if (cooldownText != null)
        {
            cooldownText.text = text;
            cooldownText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    /// <summary>
    /// 设置头像图片（技能头像区圆形头像）
    /// </summary>
    public void SetAvatar(Sprite icon)
    {
        if (avatarImage != null)
        {
            avatarImage.preserveAspect = true;
            avatarImage.sprite = icon;
            avatarImage.gameObject.SetActive(icon != null);
        }
    }
}

/// <summary>
/// 角色槽位UI（玩家/佣兵头像 + 血条 + 技能能量环）
/// 玩家槽位：点击头像释放技能，能量环显示进度，满时金色描边
/// </summary>
[System.Serializable]
public class CharacterSlotUI
{
    public GameObject root;             // 槽位根对象
    public Image portrait;              // 头像图标（不改头像框）
    public GameObject portraitPlaceholder; // 占位图
    public Image energyRing;            // 圆形能量环（可选）
    public Image glowBorder;            // 金色描边（能量满时显示）
    public Text levelLabel;             // 等级标签 "Lv.4"
    public Text nameText;               // 角色名（签名后延用）
    public Image hpBarFill;             // 血条填充 HPBarFill
    public Text hpText;                 // HP数值 "28/28"
    public Image lanBarFill;            // 蓝条/技能能量 lanBarFill
    /// <summary>未解锁/空槽时为 false，蓝条强制保持 0</summary>
    public bool EnergyEnabled { get; private set; } = true;
    public Text lanText;                // 蓝条文字
    public GameObject lockedOverlay;    // 锁定遮罩

    private float _lastEnergy = 0f;

    /// <summary>
    /// 更新槽位显示
    /// </summary>
    public void UpdateSlot(string name, int level, float currentHp, float maxHp, bool showLevel = true)
    {
        if (root == null) return;
        root.SetActive(true);
        var le = root.GetComponent<UnityEngine.UI.LayoutElement>();
        if (le != null) le.ignoreLayout = false;

        if (nameText != null && !string.IsNullOrEmpty(name))
            nameText.text = name;
        if (levelLabel != null)
        {
            levelLabel.gameObject.SetActive(showLevel);
            if (showLevel)
                levelLabel.text = $"Lv.{level}";
        }
        if (hpText != null)
        {
            hpText.gameObject.SetActive(true);
            hpText.text = $"{Mathf.RoundToInt(currentHp)}";
        }
        if (hpBarFill != null)
        {
            float ratio = maxHp > 0 ? currentHp / maxHp : 0;
            hpBarFill.enabled = true;
            hpBarFill.fillAmount = Mathf.Clamp01(ratio);
        }
        if (lanText != null) lanText.gameObject.SetActive(true);
        if (lanBarFill != null) lanBarFill.enabled = true;
    }

    /// <summary>设置头像图片（只换图标，不改头像框；强制保持比例防拉伸）</summary>
    public void SetPortrait(Sprite icon)
    {
        if (portrait != null)
        {
            portrait.preserveAspect = true;
            portrait.type = Image.Type.Simple;
            portrait.sprite = icon;
            portrait.gameObject.SetActive(icon != null);
            portrait.color = Color.white;
            if (icon != null)
                FitPortraitNoStretch(portrait);
        }
        if (portraitPlaceholder != null && portrait != null && portraitPlaceholder != portrait.gameObject)
            portraitPlaceholder.SetActive(icon == null);
    }

    static void FitPortraitNoStretch(Image img)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        // 用 FitInParent 保证正方形/矩形框里不横向拉伸像素图
        var fitter = img.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = img.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        if (img.sprite != null)
        {
            var r = img.sprite.rect;
            fitter.aspectRatio = Mathf.Max(0.01f, r.width / Mathf.Max(1f, r.height));
        }
        else
            fitter.aspectRatio = 1f;
        rt.localScale = Vector3.one;
    }

    /// <summary>技能能量：底栏 lanBar 显示进度，满时仅显示光边（不改头像框）</summary>
    public void SetEnergy(float energy)
    {
        if (!EnergyEnabled) energy = 0f;
        _lastEnergy = energy;
        float e = Mathf.Clamp01(energy);

        if (lanBarFill != null)
            lanBarFill.fillAmount = e;
        if (lanText != null)
            lanText.text = e <= 0.001f ? "" : $"{Mathf.RoundToInt(e * 100)}%";
        // 不用头像框/头像当进度条
        if (energyRing != null)
            energyRing.fillAmount = 0f;

        bool isReady = e >= 0.99f;
        if (glowBorder != null)
        {
            glowBorder.gameObject.SetActive(isReady);
            if (isReady)
            {
                glowBorder.color = new Color(1f, 0.85f, 0.15f, 0.85f);
            }
        }
    }

    public void TickSkillReadyPulse()
    {
        if (glowBorder == null || !glowBorder.gameObject.activeSelf) return;
        float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f));
        var c = glowBorder.color;
        c.r = 1f;
        c.g = 0.85f;
        c.b = 0.15f;
        c.a = a;
        glowBorder.color = c;
    }

    public void SetEnergyEnabled(bool enabled)
    {
        EnergyEnabled = enabled;
        if (!enabled)
            SetEnergy(0f);
    }

    /// <summary>满能量光边：叠在 Portrait 父节点上，不用头像当进度条</summary>
    public void EnsureSkillGlow()
    {
        if (glowBorder != null) return;
        Transform portraitRoot = portrait != null ? portrait.transform.parent : null;
        if (portraitRoot == null) return;

        Transform existing = portraitRoot.Find("SkillGlow");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("SkillGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(portraitRoot, false);
            go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-8f, -8f);
            rt.offsetMax = new Vector2(8f, 8f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = null;
            img.color = new Color(1f, 0.85f, 0.15f, 0.85f);
            img.preserveAspect = true;
        }
        glowBorder = go.GetComponent<Image>();
        go.SetActive(false);
    }

    /// <summary>
    /// 设置锁定状态。锁定/空闲时隐藏槽位，但不触发兄弟槽拉伸（需 CharacterBar ForceExpand=false）。
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
        if (root != null)
            root.SetActive(true);
        if (locked)
        {
            if (portrait != null) portrait.gameObject.SetActive(false);
            if (portraitPlaceholder != null) portraitPlaceholder.SetActive(true);
            SetEnergyEnabled(false);
        }
        else
        {
            SetEnergyEnabled(true);
        }
    }

    /// <summary>
    /// 清空槽位（无佣兵时隐藏，不占布局拉伸）
    /// </summary>
    public void Clear()
    {
        SetEnergyEnabled(false);
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        if (root != null)
        {
            root.SetActive(false);
            var le = root.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le != null) le.ignoreLayout = true;
        }
    }

    /// <summary>空槽占位显示（已解锁但无佣兵）</summary>
    public void ShowEmpty()
    {
        SetEnergyEnabled(false);
        if (root != null) root.SetActive(true);
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        if (portrait != null) portrait.gameObject.SetActive(false);
        if (portraitPlaceholder != null) portraitPlaceholder.SetActive(true);
        if (levelLabel != null) levelLabel.text = "";
        ClearNumericDisplays();
    }

    /// <summary>
    /// 未解锁槽：保留锁定遮罩与美术布局，但清掉血量/能量数值（预制体占位数字不要露出来）。
    /// </summary>
    public void KeepArtistDefault()
    {
        SetEnergyEnabled(false);
        if (root == null) return;
        root.SetActive(true);
        if (lockedOverlay != null)
            lockedOverlay.SetActive(true);
        ClearNumericDisplays();
    }

    bool _lockedLabelSized;

    /// <summary>未开放槽：保留节点可见，头像关掉，文案显示「未解锁」</summary>
    public void ShowUnavailable(string label = "未解锁")
    {
        SetEnergyEnabled(false);
        if (root == null) return;
        root.SetActive(true);
        if (lockedOverlay != null) lockedOverlay.SetActive(true);
        if (portrait != null) portrait.gameObject.SetActive(false);
        if (portraitPlaceholder != null) portraitPlaceholder.SetActive(true);
        ApplyLockedOverlayText(label ?? "未解锁");
        if (levelLabel != null)
            levelLabel.text = "";
        ClearNumericDisplays();
    }

    void ApplyLockedOverlayText(string text)
    {
        if (lockedOverlay == null) return;
        var texts = lockedOverlay.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null) continue;
            t.text = text;
            if (!_lockedLabelSized)
                t.fontSize += 2;
        }
        _lockedLabelSized = true;
    }

    /// <summary>清掉血条/蓝条上的数值与填充，避免未解锁槽露出占位数字。</summary>
    void ClearNumericDisplays()
    {
        if (hpText != null)
        {
            hpText.text = "";
            hpText.gameObject.SetActive(false);
        }
        if (lanText != null)
        {
            lanText.text = "";
            lanText.gameObject.SetActive(false);
        }
        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = 0f;
            hpBarFill.enabled = false;
        }
        if (lanBarFill != null)
        {
            lanBarFill.fillAmount = 0f;
            lanBarFill.enabled = false;
        }
    }
}

/// <summary>
/// 网格格子UI
/// </summary>
[System.Serializable]
public class GridCellUI
{
    public GameObject root;             // 格子根对象
    public Image cellBg;                // 格子底色（有/无装备区分）
    public Image itemIcon;              // 装备图标
    public Image rarityFrame;           // 品质边框
    public GameObject lockedOverlay;    // 行锁定遮罩（天赋未解锁）
    public int gridX;                   // 格子X坐标
    public int gridY;                   // 格子Y坐标
    public EquipInstance equippedItem;  // 当前装备的物品

    Color _artistBg;
    Color _artistFrame;
    bool _artistCached;

    public RectTransform VisualRect
    {
        get
        {
            if (root != null) return root.GetComponent<RectTransform>();
            if (cellBg != null) return cellBg.rectTransform;
            return null;
        }
    }

    public void CaptureDefaultVisual()
    {
        if (_artistCached) return;
        if (cellBg != null)
        {
            Color c = cellBg.color;
            // 勿把程序占用色当成美术默认（重绑/二次 Capture 会把空格锁成深色）
            if (ApproxColor(c, OccupiedBg) || ApproxColor(c, EquippedBg))
                c = Color.white;
            _artistBg = c;
        }
        if (rarityFrame != null) _artistFrame = rarityFrame.color;
        _artistCached = true;
    }

    static bool ApproxColor(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.04f
            && Mathf.Abs(a.g - b.g) < 0.04f
            && Mathf.Abs(a.b - b.b) < 0.04f;
    }

    // 空格不染色，保留预制体图片本色；仅有装备时才换色
    static readonly Color OccupiedBg = new Color(0.24f, 0.30f, 0.38f, 0.95f);
    static readonly Color EquippedBg = new Color(0.30f, 0.26f, 0.16f, 0.95f);

    /// <summary>底行等：天赋未解锁时显示锁定遮罩，格子本身保持显示（不关节点，避免 GridLayout 重排）。</summary>
    public void SetRowLocked(bool locked)
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);
        if (lockedOverlay != null)
            lockedOverlay.SetActive(locked);
        if (locked)
        {
            equippedItem = null;
            if (itemIcon != null)
            {
                itemIcon.sprite = null;
                itemIcon.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 设置装备（单格）
    /// </summary>
    public void SetItem(EquipInstance item)
    {
        SetItemSpan(item, 1, 1, TownBackpackGrid.CellSize, TownBackpackGrid.CellSpacing);
    }

    /// <summary>
    /// 多格装备：从本格左上角向右下 spanning。
    /// </summary>
    public void SetItemSpan(EquipInstance item, int spanW, int spanH, float cellSize, float spacing)
    {
        equippedItem = item;
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        if (item == null)
        {
            Clear();
            return;
        }

        if (itemIcon != null)
        {
            itemIcon.sprite = item.icon;
            itemIcon.preserveAspect = true;
            itemIcon.gameObject.SetActive(item.icon != null);
            var rt = itemIcon.rectTransform;
            const float pad = 4f;
            if (spanW <= 1 && spanH <= 1)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(pad, pad);
                rt.offsetMax = new Vector2(-pad, -pad);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
            }
            else
            {
                float totalW = spanW * cellSize + (spanW - 1) * spacing;
                float totalH = spanH * cellSize + (spanH - 1) * spacing;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(pad, -pad);
                rt.sizeDelta = new Vector2(totalW - pad * 2f, totalH - pad * 2f);
            }
        }
        if (rarityFrame != null)
        {
            Color rarityColor = GetRarityColor(item.rarity);
            rarityFrame.color = rarityColor;
        }
    }

    /// <summary>被相邻多格装备占用的格：不重复画图标。</summary>
    public void SetOccupiedNeighbor()
    {
        equippedItem = null;
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.gameObject.SetActive(false);
        }
    }

    public void SetEmptyVisual()
    {
        CaptureDefaultVisual();
        // 空格：还原美术默认色，不刷深色底
        if (cellBg != null) cellBg.color = _artistBg;
        if (rarityFrame != null) rarityFrame.color = _artistFrame;
    }

    public void SetOccupiedVisual(bool equipped)
    {
        CaptureDefaultVisual();
        if (cellBg != null)
            cellBg.color = equipped ? EquippedBg : OccupiedBg;
    }

    /// <summary>
    /// 清空格子
    /// </summary>
    public void Clear()
    {
        equippedItem = null;
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.gameObject.SetActive(false);
        }
        SetEmptyVisual();
    }

    Color GetRarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Common: return new Color(0.7f, 0.7f, 0.7f);
            case Rarity.Uncommon: return Color.green;
            case Rarity.Rare: return Color.blue;
            case Rarity.Epic: return new Color(0.6f, 0.2f, 0.8f);
            case Rarity.Legendary: return new Color(1f, 0.6f, 0f);
            default: return Color.white;
        }
    }
}