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

    [Header("=== 顶部状态栏 ===")]
    public Text stageLabel;         // 关卡标识：只显示地图名（暮影森林…），不再出现「第X章」
    public Text difficultyLabel;    // 难度标识 "普通"
    public Text goldText;           // 金币
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

    [Header("=== 技能头像区 ===")]
    public SkillAvatarUI playerSkillAvatar;   // 玩家技能头像（圆形+能量槽+光边）
    public SkillAvatarUI merc1SkillAvatar;    // 佣兵1技能头像
    public SkillAvatarUI merc2SkillAvatar;    // 佣兵2技能头像
    /// <summary>自动战斗未开放。运行时隐藏，勿在预制体里删节点。</summary>
    public Button autoButton;

    [Header("=== 底部临时布局（4技能槽 / 5装备槽）===")]
    public Transform skillSlotRoot;      // BackpackPanel/SkillBar（4 个被动技槽，自动释放）
    public Transform equipSlotRoot;      // BackpackPanel/zhuangbei（5 个装备快捷槽）
    public List<SkillAvatarUI> runSkillSlots = new List<SkillAvatarUI>();
    public List<EquipQuickSlotUI> equipQuickSlots = new List<EquipQuickSlotUI>();
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
        Instance = this;

        // 运行时补绑预制体里漏接的引用
        AutoBindMissingRefs();

        // 中文 → fusion-pixel；数字 → PixelFont
        GameFonts.ApplyToHierarchy(transform);

        // 右侧：连杀 + 下一波倒计时
        BattleSideHud.EnsureOn(transform);

        // Boss 屏幕血条（场景节点 BossBar）
        BattleBossHpBar.Ensure(transform);

        EnsureBattleControls();

        // 开战只走 AutoGameInitializer（场景组件 Awake）。UI 不再后备 Initialize，避免双入口叠开战。

        // 战斗场景：map 自适应铺满；与主相机共用，避免拆分相机黑屏
        if (GameSceneGate.IsBattle)
        {
            BattleViewportFit.Apply(Camera.main, GetComponent<Canvas>() ?? GetComponentInParent<Canvas>());
            UiPrefabRectGuard.Attach(transform, "Background");
        }
        else
            UICanvasSetup.ApplyOn(gameObject, UICanvasSetup.ResolveUiCamera());

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

    void Start()
    {
        if (GameSceneGate.IsBattle && BattleManager.Instance == null)
            Debug.LogError("[BattleUI] BattleManager 未装配：开战只走 AutoGameInitializer，UI 不再后备初始化。");
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
    float _lastPlayerHp = -1f, _lastPlayerMaxHp = -1f, _lastPlayerEnergy = -1f, _lastPlayerExp = -1f;
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
        TickRunSkillSlots();
        RefreshLiveBars();
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
        UpdateBackpackGrid();
        UpdateStageProgress(stageIdx);

        // QuestText 任务默认文案（波次刷新后由 BattleManager 再改）
        if (questPanel != null && !questPanel.activeSelf)
            questPanel.SetActive(true);
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
