using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色页（MainNavTab.Character）。参考 Art/UI/Character/character_reference.png。
/// - 无左侧装备栏
/// - 右上：天赋 / 技能；左侧独立按钮也可打开技能选择
/// - 背包格子按角色页预制体显示（4×3，共 12 格）
/// - 不自建资源条/底栏，Show 时 RaiseSharedChrome，底部预留下方 150
/// </summary>
public class CharacterUI : MonoBehaviour, ITownPage
{
    public static CharacterUI Instance { get; private set; }
    public MainNavTab Tab => MainNavTab.Character;

    const float TopReserve = 120f;
    const float BottomReserve = 150f;

    [Header("入口")]
    public Button talentButton;
    public Button skillButton;
    public Button leftSkillButton; // 2026-09-19 起按主人要求移除显示：原左下角「装备技能」入口，技能改随机后失效
    public Transform leftButtonRoot; // 主人在预制体里把 LeftSkillButton 改名为 LeftButton；该组现在内含「背包」按钮，禁止整体隐藏
    Button backpackEntryButton;     // 「背包」入口：优先取预制体 Content/Stage/LeftButton/背包，缺失时才运行时生成
    Button boardEntryButton;        // 运行时创建的「看板」入口，挂在天赋按钮同父节点下方（背包之后）

    [Header("展示")]
    public Image portraitImage;
    public Button flipPortraitButton;
    public Text titleText;
    public Text titleSubText;
    public Image headerAvatar;
    public Image carriedSkillIcon;

    [Header("基础属性")]
    public Text attrHpText;
    public Text attrAtkText;
    public Text attrDefText;
    public Text attrSpdText;
    public Text attrCritText;
    public Text attrResistText;

    [Header("背包")]
    public Text bagCapacityText;
    public TownBackpackGrid backpackGrid;
    public Button bagExpandButton;
    /// <summary>
    /// 界面内的背包面板 Content/BackpackPanel（主人在预制体里摆好的，不是弹窗）。
    /// 「背包」按钮切换它的显隐；AutoBind 里赋值。
    /// </summary>
    public Transform bagPanelRoot;
    /// <summary>
    /// 兜底开关：主人在编辑器里展开编辑后忘了把 Content/BackpackPanel、SkillSelectUI 关掉
    /// （预制体里 activeSelf 是 1），初始化时强制收起这两个面板。
    /// 置 false 可一键回退（完全不碰二者）。
    /// </summary>
    public bool collapsePanelsOnStart = true;
    /// <summary>角色页背包网格显示规格覆盖：`每行 8 格 × 4 行，下 2 行上锁`。关掉则回退 4 列默认规格。</summary>
    public bool useWideBackpackGrid = true;
    /// <summary>点击 BackpackPanel 外任意处收起背包面板。</summary>
    public bool tapOutsideToCloseBag = true;
    /// <summary>背包翻页按钮（预制体优先：BackpackPanel/PrevPageButton、NextPageButton）。</summary>
    public Button bagPrevPageButton;
    /// <summary>背包翻页按钮（预制体优先：BackpackPanel/NextPageButton）。</summary>
    public Button bagNextPageButton;

    [Header("弹层")]
    public SkillSelectUI skillSelect;

    bool _built;
    bool _preloaded;
    bool _wired;
    bool _portraitFlipped;
    bool _bagEventsWired;
    int _bagPage;
    GameObject _bagTapLayer;
    /// <summary>背包窗体自适应（瘦屏下只做防溢出 / 防裁切）。关掉则完整还原主人手摆的原始值。</summary>
    public bool enableBackpackFit = true;
    bool _bagFitCaptured;
    BackpackFitBase _bagFitBase;

    /// <summary>背包自适应用的原始值快照（主人手摆的矩形 + 手调的 GridLayoutGroup 参数），还原时原样套回。</summary>
    private struct BackpackFitBase
    {
        public Vector2 panelAnchorMin, panelAnchorMax, panelPivot, panelSizeDelta, panelPos;
        public Vector2 gridAnchorMin, gridAnchorMax, gridPivot, gridSizeDelta, gridPos;
        public Vector2 cellSize, spacing;
        public int padL, padR, padT, padB;
        public bool hasPadding;
    }

    /// <summary>角色页背包网格的显示列数（只改显示，GameConfig.BACKPACK_WIDTH 仍是 4）。</summary>
    const int WideBackpackColumns = 8;
    /// <summary>角色页背包网格显示的解锁行数：4 显示行里下 2 行上锁。</summary>
    const int WideBackpackUnlockedRows = 2;
    /// <summary>翻页按钮在 BackpackPanel 内的纵坐标（GridContainer 下方那条空档）。</summary>
    const float BagPageButtonY = -184f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_bagEventsWired && GridBackpackSystem.Instance != null)
        {
            GridBackpackSystem.Instance.OnBackpackChanged -= OnBagOrCostumeChanged;
            GridBackpackSystem.Instance.OnCostumeChanged -= OnBagOrCostumeChanged;
            _bagEventsWired = false;
        }
    }

    /// <summary>
    /// 收起主人在编辑器里「展开编辑忘了关」的两个节点（预制体里 activeSelf 目前是 1）：
    /// Content/BackpackPanel 与 SkillSelectUI。只在 PreloadOnce 里跑一次，运行时兜底纠正，不改预制体文件。
    /// 只收起，不动 OpenSkillSelect() 里的 skillSelect.Show()。
    /// </summary>
    void CollapseForgottenPanels()
    {
        if (!collapsePanelsOnStart) return;

        if (bagPanelRoot == null)
            bagPanelRoot = transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel");
        if (bagPanelRoot != null && bagPanelRoot.gameObject.activeSelf)
            bagPanelRoot.gameObject.SetActive(false);

        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect != null)
        {
            if (skillSelect.gameObject.activeSelf)
                skillSelect.Hide();
        }
        else
        {
            var skillRoot = transform.Find("SkillSelectUI") ?? FindDeep(transform, "SkillSelectUI");
            if (skillRoot != null && skillRoot.gameObject.activeSelf)
                skillRoot.gameObject.SetActive(false);
        }
    }

    public void PreloadOnce()
    {
        if (_preloaded) return;

        if (transform.Find("Content") != null)
        {
            AutoBind();
            _built = true;
        }
        else if (!_built)
            BuildHierarchyForPrefab();

        CollapseForgottenPanels();
        StripLocalChrome();
        EnsureVisibleTransform();
        ConfigureHostCanvasOnce();
        if (backpackGrid != null)
        {
            backpackGrid.BindFromHierarchy(transform);
            // 预制体已有格子则只绑不重建，避免盖掉手摆布局
        }
        GameFonts.ApplyToHierarchy(transform);
        WireClicks();
        WireBagEvents();
        FixWrongArtBindings();
        _preloaded = true;
        gameObject.SetActive(false);
    }

    public void ShowPage()
    {
        if (!_preloaded) PreloadOnce();
        EnsureVisibleTransform();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SetGuildOverlay(true);
        TownPageDim.Ensure(transform);

        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);

        // 页可能比背包系统更早预载，Show 时再补订一次
        WireBagEvents();
        RefreshAll();
        EnsureStageShadow();
        // SPUM 仅离屏换装；Portrait 用玩家立绘
        TownHeroCostumePreview.EnsureOn(this)?.EnsureOffscreenCostume();
        RefreshPlayerPortrait();
        TutorialHintUI.Ensure().Hide();
    }

    public void HidePage()
    {
        if (skillSelect != null) skillSelect.Hide();
        TownHeroCostumePreview.EnsureOn(this)?.Hide();
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        SetGuildOverlay(false);
    }

    public void Show() => ShowPage();
    public void Hide() => HidePage();

    public void RefreshAll()
    {
        RefreshIdentity();
        RefreshPlayerPortrait();
        // 携带技能展示按主人 2026-09-19 要求移除（技能改随机，已装备技能入口失效），不再刷新
        RefreshAttrs();
        RefreshBag();
        TownHeroCostumePreview.EnsureOn(this)?.EnsureOffscreenCostume();
    }

    void RefreshPlayerPortrait()
    {
        if (portraitImage == null)
            portraitImage = transform.Find("Content/Stage/Portrait")?.GetComponent<Image>();
        if (portraitImage == null) return;

        // 清掉历史 SpumPreview
        var junk = portraitImage.transform.Find("SpumPreview");
        if (junk != null)
            Destroy(junk.gameObject);

        MercPortraitSprites.ClearCache();
        string boardId = GetCurrentBoardId(); // "player" 或 佣兵 HireId
        // 看板统一显示「佣兵头像」（与佣兵碎片预制体 HeadIcon 同源）：
        // 优先 MercPortraitSprites.GetHead → Resources/Icons/MercHead（编辑器直读 Art/UI/Icons/佣兵头像，玩家为 玩家.png）
        // 头像取不到才退回立绘 GetStand；两者都拿不到就保留预制体里主人摆好的图
        var head = MercPortraitSprites.GetHead(boardId);
        var sp = head ?? MercPortraitSprites.GetStand(boardId);
        if (sp != null)
            SetPortrait(sp, _portraitFlipped);
        else
            portraitImage.enabled = true;
    }

    /// <summary>当前看板 id：默认玩家自己（"player"），否则已选佣兵 HireId。持久化走 PlayerPrefs。</summary>
    public string GetCurrentBoardId()
    {
        return PlayerPrefs.GetString(BoardKey, "player");
    }

    /// <summary>设为看板并立即刷新立绘；id 为空或 "player" 表示恢复默认（玩家自己）。</summary>
    public void ApplyBoardSelection(string id)
    {
        if (string.IsNullOrEmpty(id)) id = "player";
        PlayerPrefs.SetString(BoardKey, id);
        PlayerPrefs.Save();
        RefreshPlayerPortrait();
    }

    static readonly string BoardKey = "CharBoardId";

    // 2026-09-25 主人明确 LeftSkillButton 已改名且暂时不要了，携带技能展示彻底废弃。
    // 保留方法签名以免其它引用处（如技能选择回调）编译报错，但不再动任何节点、不再 SetActive。
    void RefreshCarriedSkill()
    {
        return;
    }

    static Sprite LoadPlayerSkillIcon(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        var sp = Resources.Load<Sprite>("Icons/SkillIcon/" + skillId);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>("Icons/SkillIcon/" + skillId);
        if (all != null && all.Length > 0) return all[0];
#if UNITY_EDITOR
        string artPath = "Assets/Art/UI/Icons/玩家SkillIcon/" + skillId + ".png";
        sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
        if (sp != null) return sp;
#endif
        return null;
    }

    /// <summary>
    /// 技能图画在 LeftSkillButton 自身 Image 上。
    /// 2026-09-25 适配主人新层级：预制体 LeftButton/Skill/Icon 是主人新加的真按钮（挂了 Button），
    /// 因此这里禁止 Find("Icon") 后 Destroy —— 那会把主人的按钮删掉；
    /// 也不再 SetActive 任何子节点 Image（属于代码改美术节点状态，作用于 LeftButton 时更无意义）。
    /// </summary>
    void EnsureCarriedSkillIcon()
    {
        if (leftSkillButton == null) return;

        carriedSkillIcon = leftSkillButton.targetGraphic as Image
                           ?? leftSkillButton.GetComponent<Image>();
    }

    void WireBagEvents()
    {
        if (_bagEventsWired) return;
        if (GridBackpackSystem.Instance == null) return;
        GridBackpackSystem.Instance.OnBackpackChanged += OnBagOrCostumeChanged;
        GridBackpackSystem.Instance.OnCostumeChanged += OnBagOrCostumeChanged;
        _bagEventsWired = true;
    }

    void OnBagOrCostumeChanged()
    {
        if (!gameObject.activeInHierarchy) return;
        RefreshBag();
        RefreshAttrs();
        TownHeroCostumePreview.EnsureOn(this)?.EnsureOffscreenCostume();
    }

    void RefreshIdentity()
    {
        if (titleText != null)
            titleText.text = PlayerIdentity.DisplayName;
        if (titleSubText != null)
            titleSubText.text = PlayerIdentity.Title;
    }

    /// <summary>
    /// 纠正手做预制体里明显错绑的图：名字旁用了「切换」、背包用了战斗大边框、
    /// 右侧天赋半透底用了横板翻转。只换资源/显隐，不改坐标。
    /// </summary>
    void FixWrongArtBindings()
    {
        // Header/icon：切换箭头 → 玩家头像
        if (headerAvatar == null)
            headerAvatar = transform.Find("Content/Header/icon")?.GetComponent<Image>();
        if (headerAvatar != null)
        {
            var avatar = LoadNavArt("角色_0000s_0000_玩家头像");
            if (avatar != null)
            {
                headerAvatar.sprite = avatar;
                headerAvatar.preserveAspect = true;
                headerAvatar.color = Color.white;
            }
        }

        // 背包底框：战斗「大边框」→ 角色页面板
        var bagPanel = (transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel"))
                       ?.GetComponent<Image>();
        if (bagPanel != null)
        {
            var frame = LoadNavArt("角色_0002s_0009_图层-6-拷贝");
            if (frame != null)
            {
                bagPanel.sprite = frame;
                bagPanel.type = Image.Type.Sliced;
                bagPanel.color = Color.white;
            }
        }

        // 右侧天赋底：错用横板+Y翻转，关掉以免脏半透条；按钮本身已有底图
        var rightBg = transform.Find("Content/Stage/RightButtons/bg");
        if (rightBg != null)
            rightBg.gameObject.SetActive(false);
    }

    static Sprite LoadNavArt(string fileNameWithoutExt)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExt)) return null;
        // 统一走 Resources，避免 Art / Resources 双份
        var sp = Resources.Load<Sprite>("UI/NavCharacter/" + fileNameWithoutExt);
        if (sp != null) return sp;
#if UNITY_EDITOR
        string path = "Assets/Art/UI/NavCharacter/" + fileNameWithoutExt + ".png";
        var ed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (ed != null) return ed;
#endif
        return null;
    }

    void RefreshAttrs()
    {
        float hp = GameConfig.BASE_HP, atk = GameConfig.BASE_ATTACK, def = GameConfig.BASE_DEFENSE;
        float spd = GameConfig.BASE_ATTACK_SPEED, crit = GameConfig.BASE_CRIT_RATE, resist = 0f;
        float talAtk = 0f, talHp = 0f, talDef = 0f, talCrit = 0f, talSpd = 0f;
        SumTalentAttrBonuses(ref talAtk, ref talHp, ref talDef, ref talCrit, ref talSpd);
        try
        {
            AttrSystem src = null;
            var hero = Hero.Instance;
            if (hero != null && hero.attr != null)
            {
                src = hero.attr;
            }
            else
            {
                src = new AttrSystem(AttrOwnerKind.Player);
                var bonuses = EquipStatRollup.BuildBonusList(GridBackpackSystem.Instance);
                src.RecalcAllAttr(bonuses);
            }

            if (src != null)
            {
                hp = src.GetAttr(AttrType.MaxHp);
                atk = src.GetAttr(AttrType.Attack);
                def = src.GetAttr(AttrType.Defense);
                spd = src.GetAttr(AttrType.AttackSpeed);
                crit = src.GetAttr(AttrType.CritRate);
                resist = 0f;
            }
        }
        catch { }

        float critDisplay = crit;
        if (critDisplay <= 1.5f)
            critDisplay *= 100f;

        // 天赋加成在后面用 +N 标出（与天赋页汇总一致）
        if (attrHpText != null) attrHpText.text = FormatAttrWithTalent(hp, talHp, roundInt: true);
        if (attrAtkText != null) attrAtkText.text = FormatAttrWithTalent(atk, talAtk, roundInt: true);
        if (attrDefText != null) attrDefText.text = FormatAttrWithTalent(def, talDef, roundInt: true);
        if (attrSpdText != null) attrSpdText.text = FormatAttrWithTalent(spd, talSpd, roundInt: false, decimals: "0.##");
        if (attrCritText != null)
        {
            string core = FormatAttrWithTalent(critDisplay, talCrit, roundInt: false, decimals: "0.#");
            attrCritText.text = core.EndsWith("%") ? core : core + "%";
        }
        if (attrResistText != null) attrResistText.text = resist.ToString("0.#") + "%";
    }

    static void SumTalentAttrBonuses(ref float atk, ref float hp, ref float def, ref float crit, ref float spd)
    {
        var talents = SaveSystem.Instance?.Data?.talents;
        if (talents == null) return;
        int leftN = TalentDefs.LeftUnlockedCount(talents);
        for (int i = 0; i < leftN; i++)
        {
            var e = TalentDefs.Left[i].effect;
            if (e == null) continue;
            switch (e.kind)
            {
                case TalentDefs.AttrKind.Attack: atk += e.value; break;
                case TalentDefs.AttrKind.Hp: hp += e.value; break;
                case TalentDefs.AttrKind.Defense: def += e.value; break;
                case TalentDefs.AttrKind.CritRate: crit += e.value; break;
                case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
            }
        }
        // 右列重制节点：累加可计入面板的属性（攻击/生命/防御/暴击/攻速）
        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var node = TalentDefs.RightNodes[i];
            int lv = TalentDefs.GetRightNodeLevel(talents, node.id);
            if (lv <= 0) continue;
            int job = TalentDefs.GetRightNodeChosenJob(talents, node.id);
            var opt = node.IsJobChoice ? node.ChosenOption(job)
                                       : (node.options != null && node.options.Length > 0 ? node.options[0] : null);
            if (opt == null) continue;
            AccumulateRightTalent(opt.kind, node.EffectValue(lv, job),
                ref atk, ref hp, ref def, ref crit, ref spd);
        }
    }

    static void AccumulateRightTalent(TalentDefs.AttrKind kind, float value,
        ref float atk, ref float hp, ref float def, ref float crit, ref float spd)
    {
        switch (kind)
        {
            case TalentDefs.AttrKind.Attack: atk += value; break;
            case TalentDefs.AttrKind.Hp: hp += value; break;
            case TalentDefs.AttrKind.Defense: def += value; break;
            case TalentDefs.AttrKind.CritRate: crit += value; break;
            case TalentDefs.AttrKind.AtkSpeed: spd += value; break;
        }
    }

    /// <summary>总数已含天赋时：显示 base+bonus（如 120+3）；无加成只显示总数。</summary>
    static string FormatAttrWithTalent(float total, float talentBonus, bool roundInt, string decimals = "0")
    {
        if (Mathf.Abs(talentBonus) < 0.0001f)
        {
            if (roundInt) return Mathf.RoundToInt(total).ToString("N0");
            return total.ToString(decimals);
        }
        float baseVal = total - talentBonus;
        if (roundInt)
            return Mathf.RoundToInt(baseVal).ToString("N0") + "+" + Mathf.RoundToInt(talentBonus).ToString();
        return baseVal.ToString(decimals) + "+" + talentBonus.ToString(decimals);
    }

    void RefreshBag()
    {
        backpackGrid?.Refresh();
        int unlocked = backpackGrid != null ? backpackGrid.UnlockedSlotCount() : GameConfig.BACKPACK_DEFAULT_ROWS * GameConfig.BACKPACK_WIDTH;
        int used = 0;
        if (backpackGrid != null)
        {
            for (int i = 0; i < backpackGrid.cells.Count; i++)
                if (backpackGrid.cells[i]?.equippedItem != null) used++;
        }
        if (bagCapacityText != null)
            bagCapacityText.text = used + "/" + unlocked;
    }

    void WireClicks()
    {
        if (_wired) return;
        _wired = true;
        if (talentButton != null)
        {
            talentButton.onClick.RemoveAllListeners();
            talentButton.onClick.AddListener(OpenTalent);
        }
        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(OpenSkillSelect);
        }
        // 2026-09-25 主人明确：LeftSkillButton 已改名且暂时不要了，原入口废弃。
        // 不再隐藏/停用任何节点（leftSkillButton、carriedSkillIcon 保持 null，不动）。

        EnsureBackpackEntry();
        EnsureBoardEntry();
        EnsureBagPageButtons();
        if (flipPortraitButton != null)
        {
            flipPortraitButton.onClick.RemoveAllListeners();
            flipPortraitButton.onClick.AddListener(FlipPortrait);
        }
        if (bagExpandButton != null)
        {
            bagExpandButton.onClick.RemoveAllListeners();
            bagExpandButton.onClick.AddListener(() =>
                UIManager.Instance?.ShowToast("扩容请在天赋中解锁背包行"));
        }
    }

    public void OpenTalent()
    {
        TalentUI ui = TalentUI.Instance;
        if (ui == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Talent/TalentUI");
            if (prefab == null)
            {
                UIManager.Instance?.ShowToast("天赋界面未就绪");
                return;
            }
            var go = Instantiate(prefab);
            go.name = "TalentUI";
            ui = go.GetComponent<TalentUI>();
        }
        if (ui == null) return;
        if (ui.onClosed == null) ui.onClosed = new UnityEngine.Events.UnityEvent();
        ui.onClosed.RemoveAllListeners();
        ui.onClosed.AddListener(() =>
        {
            if (gameObject.activeInHierarchy)
                RefreshAttrs();
        });
        ui.Show();
    }

    public void OpenSkillSelect()
    {
        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect == null)
        {
            UIManager.Instance?.ShowToast("技能界面未就绪");
            return;
        }
        skillSelect.Show();
    }

    /// <summary>
    /// 「背包」入口：预制体优先。
    /// 2026-09-25 起主人在 Content/Stage/LeftButton/背包 里摆好了按钮（AutoBind 里绑到 backpackEntryButton），
    /// 此时直接复用，不再运行时生成，避免出现第二个背包按钮；
    /// 只有预制体里真的没有时才在天赋按钮同父节点（右侧按钮列）下用 BuildSideBtn 兜底。
    /// 点击统一在这里绑一次（RemoveAllListeners 保证不会重复监听），打开独立背包弹窗。
    /// </summary>
    void EnsureBackpackEntry()
    {
        if (backpackEntryButton == null && talentButton != null)
        {
            Transform parent = talentButton.transform.parent;
            if (parent != null)
            {
                // 技能按钮（SkillButton）在 BuildSideBtn 约定里 y = -90，背包入口排在其下方 y = -180
                BuildSideBtn(parent, "BackpackButton", "背包", -180f, new Color(0.45f, 0.4f, 0.25f, 1f));
                backpackEntryButton = parent.Find("BackpackButton")?.GetComponent<Button>();
            }
        }
        if (backpackEntryButton != null)
        {
            backpackEntryButton.onClick.RemoveAllListeners();
            backpackEntryButton.onClick.AddListener(OpenBackpack);
        }
    }

    /// <summary>
    /// 「背包」按钮：切换界面内的 Content/BackpackPanel（展开/收起），不再弹独立弹窗。
    /// 展开时把 SkillSelectUI 收掉，两个面板互斥，避免叠在一起。
    /// 只有预制体里真的没有 BackpackPanel 时才回退到独立弹窗 BackpackPopupUI。
    /// </summary>
    public void OpenBackpack()
    {
        if (bagPanelRoot == null)
            bagPanelRoot = transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel");

        if (bagPanelRoot != null)
        {
            bool open = !bagPanelRoot.gameObject.activeSelf;
            if (!open)
            {
                CloseBackpackPanel();
                return;
            }

            bagPanelRoot.gameObject.SetActive(true);
            EnsureBagTapLayer();          // 面板下面垫一层全屏透明点击层
            SetBagTapLayerActive(true);
            // 互斥：展开背包时收起技能面板
            HideSkillSelect();
            RefreshBag();
            ApplyBackpackPanelFit();     // 面板打开后做一次瘦屏防溢出 / 防裁切
            return;
        }

        // 兜底：没有界面内面板时才走原来的独立背包弹窗（复用 TownBackpackGrid / BackpackItemActionUI）
        var popup = BackpackPopupUI.Ensure(transform);
        if (popup == null)
        {
            UIManager.Instance?.ShowToast("背包未就绪");
            return;
        }
        popup.Show();
    }

    /// <summary>
    /// 收起背包面板的唯一入口：`OpenBackpack()` 的收起分支与「点击面板外」层都调它，
    /// 避免两处各写一份导致逻辑不一致。同时收起 SkillSelectUI（与展开时的互斥逻辑对称）。
    /// </summary>
    void CloseBackpackPanel()
    {
        if (bagPanelRoot != null && bagPanelRoot.gameObject.activeSelf)
            bagPanelRoot.gameObject.SetActive(false);
        SetBagTapLayerActive(false);
        HideSkillSelect();
    }

    /// <summary>收起技能选择面板（与展开背包时的互斥处理完全一致）。</summary>
    void HideSkillSelect()
    {
        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect != null)
            skillSelect.Hide();
        else
        {
            var skillRoot = transform.Find("SkillSelectUI") ?? FindDeep(transform, "SkillSelectUI");
            if (skillRoot != null) skillRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// D：点面板外任意处收起。运行时建一层全屏透明点击层（Image alpha 0、raycastTarget=1），
    /// 插在 BackpackPanel <b>之前</b>（即之下），所以绝不会挡住面板自己的按钮与格子。
    /// 写法参考 RewardPopupUI.SetupTapAnywhereToClose。
    /// </summary>
    void EnsureBagTapLayer()
    {
        if (!tapOutsideToCloseBag || bagPanelRoot == null) return;
        if (_bagTapLayer != null)
        {
            KeepTapLayerBehindPanel();
            return;
        }

        var parent = bagPanelRoot.parent;
        if (parent == null) return;

        var go = new GameObject("BagTapAnywhereLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        btn.interactable = true;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(CloseBackpackPanel);

        _bagTapLayer = go;
        go.SetActive(false);   // 新建对象默认 active，先关掉，等 OpenBackpack 打开面板时才亮
        KeepTapLayerBehindPanel();
    }

    /// <summary>保证点击层排在 BackpackPanel 之前（之下），不挡面板本身。</summary>
    void KeepTapLayerBehindPanel()
    {
        if (_bagTapLayer == null || bagPanelRoot == null) return;
        int panelIdx = bagPanelRoot.GetSiblingIndex();
        if (_bagTapLayer.transform.parent != bagPanelRoot.parent) return;
        if (_bagTapLayer.transform.GetSiblingIndex() > panelIdx)
            _bagTapLayer.transform.SetSiblingIndex(panelIdx);
    }

    void SetBagTapLayerActive(bool active)
    {
        if (_bagTapLayer == null) return;
        if (_bagTapLayer.activeSelf != active)
            _bagTapLayer.SetActive(active);
        if (active) KeepTapLayerBehindPanel();
    }

    /// <summary>
    /// B：角色页背包网格的显示规格覆盖 —— 每行 8 格、共 4 行、下 2 行上锁。
    /// 只写本组件上的角色页专属网格（CharacterBagGrid）的字段，
    /// 不碰 GameConfig、物品放置逻辑与存档格式，也不碰共用组件 TownBackpackGrid。
    /// 2026-09-25：主人已经在预制体 Content/BackpackPanel/GridContainer 里手摆好 32 格
    /// （Cell_{显示行}_{显示列}，行 0..3、列 0..7）并调好了 GridLayoutGroup（82×82 / 8 列），
    /// 所以这里**不再重建** —— 只绑定；只有预制体里确实没有 GridContainer 时才兜底重建。
    /// </summary>
    void ApplyWideBackpackGrid()
    {
        if (backpackGrid == null) return;

        var bag = backpackGrid as CharacterBagGrid;
        if (bag != null)
        {
            if (useWideBackpackGrid)
            {
                bag.columns = WideBackpackColumns;
                bag.unlockedDisplayRows = WideBackpackUnlockedRows;
            }
            else
            {
                // 关掉宽规格：两个值置 0，子类会回退基类默认（4 列 / 存档真值）
                bag.columns = 0;
                bag.unlockedDisplayRows = 0;
            }
            bag.page = 0;
        }
        _bagPage = 0;

        // 主人的 32 格是手摆的：先绑定，绝不销毁重建（重建会把手摆格子和手调参数全清掉）
        backpackGrid.BindFromHierarchy(transform);
        // 只有预制体里真的没有 GridContainer（绑定没拿到）时才兜底重建
        if (backpackGrid.gridContainer == null)
            backpackGrid.BuildGrid(bagPanelRoot != null ? bagPanelRoot : backpackGrid.transform);
        backpackGrid.Refresh();
    }

    /// <summary>
    /// 背包窗体自适应：只在瘦屏下做「防溢出 / 防裁切」，绝不主动做位移。
    /// a) 格子自适应：算出来的格子宽度比主人调的 82 小才缩小，**只缩小不放大**；
    /// b) 面板完整可见：只有顶边超出父级顶边、或底边超出父级底边时才把 anchoredPosition.y 钳回来，
    ///    不超出就一动不动。
    /// 开关关掉或非瘦屏 → 把主人手摆的原始值完整还原后 return。
    /// 绝不改 BackpackPanel 的 anchor / pivot / sizeDelta（都是主人手摆的）。
    /// </summary>
    void ApplyBackpackPanelFit()
    {
        if (backpackGrid == null || bagPanelRoot == null) return;
        var panelRt = bagPanelRoot as RectTransform;
        var gridRt = backpackGrid.gridContainer;
        if (panelRt == null || gridRt == null) return;

        var gl = gridRt.GetComponent<GridLayoutGroup>();

        // 首次进来先把主人手摆的原始值记下来，后面还原用
        if (!_bagFitCaptured)
        {
            _bagFitBase.panelAnchorMin = panelRt.anchorMin;
            _bagFitBase.panelAnchorMax = panelRt.anchorMax;
            _bagFitBase.panelPivot = panelRt.pivot;
            _bagFitBase.panelSizeDelta = panelRt.sizeDelta;
            _bagFitBase.panelPos = panelRt.anchoredPosition;
            _bagFitBase.gridAnchorMin = gridRt.anchorMin;
            _bagFitBase.gridAnchorMax = gridRt.anchorMax;
            _bagFitBase.gridPivot = gridRt.pivot;
            _bagFitBase.gridSizeDelta = gridRt.sizeDelta;
            _bagFitBase.gridPos = gridRt.anchoredPosition;
            if (gl != null)
            {
                _bagFitBase.cellSize = gl.cellSize;
                _bagFitBase.spacing = gl.spacing;
                if (gl.padding != null)
                {
                    _bagFitBase.hasPadding = true;
                    _bagFitBase.padL = gl.padding.left;
                    _bagFitBase.padR = gl.padding.right;
                    _bagFitBase.padT = gl.padding.top;
                    _bagFitBase.padB = gl.padding.bottom;
                }
            }
            _bagFitCaptured = true;
        }

        if (!enableBackpackFit || !UiLayoutStretch.IsThinnerScreen())
        {
            RestoreBackpackFit(panelRt, gridRt, gl);
            return;
        }

        // a) 格子自适应：只缩小不放大，绝不盖掉主人调好的 82
        if (gl != null)
        {
            int cols = BagDisplayColumns();
            if (cols > 0)
            {
                float padLR = gl.padding != null ? gl.padding.left + gl.padding.right : 0f;
                float w = (gridRt.rect.width - padLR - (cols - 1) * gl.spacing.x) / cols;
                if (w > 0f && w < gl.cellSize.x)
                    gl.cellSize = new Vector2(w, w);
            }
        }

        // b) 面板完整可见：超出父级才钳 y，不超出就一动不动（父级矩形用父级 rect 算）
        var parentRt = panelRt.parent as RectTransform;
        if (parentRt != null)
        {
            var corners = new Vector3[4];
            panelRt.GetWorldCorners(corners);
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float y = parentRt.InverseTransformPoint(corners[i]).y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            float dy = 0f;
            if (maxY > parentRt.rect.yMax) dy = parentRt.rect.yMax - maxY;
            else if (minY < parentRt.rect.yMin) dy = parentRt.rect.yMin - minY;
            if (dy > 0.5f || dy < -0.5f)
                panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, panelRt.anchoredPosition.y + dy);
        }
    }

    /// <summary>把背包面板 / 网格还原成主人手摆的原始值（非瘦屏或开关关闭时走这里）。</summary>
    void RestoreBackpackFit(RectTransform panelRt, RectTransform gridRt, GridLayoutGroup gl)
    {
        if (!_bagFitCaptured) return;
        panelRt.anchorMin = _bagFitBase.panelAnchorMin;
        panelRt.anchorMax = _bagFitBase.panelAnchorMax;
        panelRt.pivot = _bagFitBase.panelPivot;
        panelRt.sizeDelta = _bagFitBase.panelSizeDelta;
        panelRt.anchoredPosition = _bagFitBase.panelPos;
        gridRt.anchorMin = _bagFitBase.gridAnchorMin;
        gridRt.anchorMax = _bagFitBase.gridAnchorMax;
        gridRt.pivot = _bagFitBase.gridPivot;
        gridRt.sizeDelta = _bagFitBase.gridSizeDelta;
        gridRt.anchoredPosition = _bagFitBase.gridPos;
        if (gl != null)
        {
            gl.cellSize = _bagFitBase.cellSize;
            gl.spacing = _bagFitBase.spacing;
            if (_bagFitBase.hasPadding)
                gl.padding = new RectOffset(_bagFitBase.padL, _bagFitBase.padR, _bagFitBase.padT, _bagFitBase.padB);
        }
    }

    /// <summary>
    /// 拿角色页专属网格实例。预制体上可能同时挂着通用 TownBackpackGrid 与 CharacterBagGrid，
    /// GetComponent&lt;TownBackpackGrid&gt;() 的返回顺序不确定，所以遍历全部：
    /// 只认 CharacterBagGrid，其余通用组件运行时停用（enabled = false，不写盘、不改预制体），
    /// 避免两个组件抢同一个 GridContainer。
    /// </summary>
    CharacterBagGrid ResolveBagGrid()
    {
        CharacterBagGrid bag = null;
        var all = GetComponents<TownBackpackGrid>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is CharacterBagGrid b) bag = b;
            else all[i].enabled = false;        // 停用预制体上自带的通用组件
        }
        if (bag == null) bag = gameObject.AddComponent<CharacterBagGrid>();
        backpackGrid = bag;                     // 字段类型保持 TownBackpackGrid 不变
        return bag;
    }

    /// <summary>
    /// C：格子下面的左右翻页按钮。预制体优先（BackpackPanel/PrevPageButton、NextPageButton，
    /// 再找中文「上一页」/「下一页」），都找不到才在 GridContainer 同级、它之后运行时创建。
    /// </summary>
    void EnsureBagPageButtons()
    {
        if (bagPanelRoot == null) return;

        // 角色页 32 格（8 列 × 4 显示行）一页就把 16 个逻辑格全显示完了，第 2 页没有新内容 →
        // 翻页没有意义：只有 BagPageCount() > 1 时才创建 / 显示按钮，否则找到也隐藏，绝不新建无用按钮。
        if (BagPageCount() <= 1)
        {
            if (bagPrevPageButton == null) bagPrevPageButton = FindBagPageBtn("PrevPageButton", "上一页");
            if (bagNextPageButton == null) bagNextPageButton = FindBagPageBtn("NextPageButton", "下一页");
            if (bagPrevPageButton != null && bagPrevPageButton.gameObject.activeSelf)
                bagPrevPageButton.gameObject.SetActive(false);
            if (bagNextPageButton != null && bagNextPageButton.gameObject.activeSelf)
                bagNextPageButton.gameObject.SetActive(false);
            return;
        }

        if (bagPrevPageButton == null) bagPrevPageButton = FindBagPageBtn("PrevPageButton", "上一页");
        if (bagNextPageButton == null) bagNextPageButton = FindBagPageBtn("NextPageButton", "下一页");

        if (bagPrevPageButton != null && !bagPrevPageButton.gameObject.activeSelf)
            bagPrevPageButton.gameObject.SetActive(true);
        if (bagNextPageButton != null && !bagNextPageButton.gameObject.activeSelf)
            bagNextPageButton.gameObject.SetActive(true);

        if (bagPrevPageButton == null) bagPrevPageButton = BuildBagPageBtn("PrevPageButton", "◀", -100f);
        if (bagNextPageButton == null) bagNextPageButton = BuildBagPageBtn("NextPageButton", "▶", 100f);

        if (bagPrevPageButton != null)
        {
            bagPrevPageButton.onClick.RemoveAllListeners();
            bagPrevPageButton.onClick.AddListener(PrevBagPage);
        }
        if (bagNextPageButton != null)
        {
            bagNextPageButton.onClick.RemoveAllListeners();
            bagNextPageButton.onClick.AddListener(NextBagPage);
        }
    }

    Button FindBagPageBtn(string enName, string cnName)
    {
        if (bagPanelRoot == null) return null;
        return bagPanelRoot.Find(enName)?.GetComponent<Button>()
               ?? bagPanelRoot.Find(cnName)?.GetComponent<Button>();
    }

    Button BuildBagPageBtn(string name, string label, float x)
    {
        if (bagPanelRoot == null) return null;
        var img = CreateImg(bagPanelRoot, name, new Color(0.45f, 0.36f, 0.2f, 0.95f));
        Set(img.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, x, BagPageButtonY, 120f, 44f);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var tx = CreateTxt(img.transform, "Label", label, 26, new Color(1f, 0.95f, 0.8f));
        Stretch(tx.rectTransform);
        return btn;
    }

    /// <summary>
    /// 背包有效页数：只数「至少有一格未锁」的页。
    /// 角色页 32 格（8 列 × 4 显示行，一个显示行折 2 个逻辑行）一页就铺到 8 个逻辑行，
    /// 已经 ≥ BACKPACK_HEIGHT_MAX(4) → 16 个逻辑格一页全显示完，第 2 页起全是越界锁格 → 只有 1 页。
    /// BackpackPopupUI 走基类（4 列、page 恒为 0）时返回值仍 >= 1，**绝不返回 0**。
    /// </summary>
    int BagPageCount()
    {
        if (backpackGrid == null) return 1;
        int offsetRows = backpackGrid.LogicalRowsPerPage();    // 页间逻辑行偏移
        if (offsetRows <= 0) return 1;

        // 一页实际铺到的逻辑行数 = 显示行数 × 一个显示行折进去的逻辑行数
        int perRowLogical = GameConfig.BACKPACK_HEIGHT_MAX / offsetRows;
        if (perRowLogical <= 0) perRowLogical = 1;
        int rowsPerPage = BagDisplayRowCount() * perRowLogical;
        if (rowsPerPage <= 0) return 1;

        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)GameConfig.BACKPACK_HEIGHT_MAX / rowsPerPage));
        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        int count = 0;
        for (int p = 0; p < totalPages; p++)
        {
            int rowStart = p * rowsPerPage;
            if (rowStart >= GameConfig.BACKPACK_HEIGHT_MAX) break;   // 整页越界 → 全是锁格
            if (rowStart >= unlockedRows) break;                     // 这页起已没有解锁行 → 全是锁格
            count++;
        }
        return count > 0 ? count : 1;
    }

    /// <summary>背包网格的显示行数（总格数 ÷ 显示列数）。</summary>
    int BagDisplayRowCount()
    {
        int cols = BagDisplayColumns();
        if (backpackGrid == null || cols <= 0) return 0;
        return Mathf.CeilToInt((float)backpackGrid.cells.Count / cols);
    }

    /// <summary>背包网格的显示列数（角色页 8 列；基类 / 弹窗为 GameConfig.BACKPACK_WIDTH）。</summary>
    int BagDisplayColumns()
    {
        if (backpackGrid is CharacterBagGrid bag && bag.columns > 0) return bag.columns;
        return GameConfig.BACKPACK_WIDTH;
    }

    public void PrevBagPage() => StepBagPage(-1);
    public void NextBagPage() => StepBagPage(1);

    /// <summary>翻页：只改显示用的页索引并刷新，绝不移动/删除任何存档道具。</summary>
    void StepBagPage(int delta)
    {
        int pages = BagPageCount();
        if (pages <= 1)
        {
            UIManager.Instance?.ShowToast("没有更多页");
            return;
        }

        int next = Mathf.Clamp(_bagPage + delta, 0, pages - 1);
        if (next == _bagPage)
        {
            UIManager.Instance?.ShowToast("没有更多页");
            return;
        }

        _bagPage = next;
        if (backpackGrid is CharacterBagGrid bag) bag.page = next;
        RefreshBag();
    }

    /// <summary>
    /// 「看板」入口：预制体优先。
    /// 2026-09-25 起主人在 Content/Stage/RightButtons/立绘 里摆好了按钮（AutoBind 已绑到 boardEntryButton），
    /// 此时直接复用，不再运行时生成 BoardButton，避免出现第二个看板按钮；
    /// 只有预制体里真的没有时才在天赋按钮同父节点（右侧按钮列）下用 BuildSideBtn 兜底。
    /// 点击统一在这里绑一次（RemoveAllListeners 保证不会重复监听），打开看板选择弹窗。
    /// </summary>
    void EnsureBoardEntry()
    {
        if (boardEntryButton == null && talentButton != null)
        {
            Transform parent = talentButton.transform.parent;
            if (parent != null)
            {
                // 背包入口 y = -180，看板入口排在其下方 y = -270（与列内 90 间距一致）
                BuildSideBtn(parent, "BoardButton", "看板", -270f, new Color(0.4f, 0.3f, 0.55f, 1f));
                boardEntryButton = parent.Find("BoardButton")?.GetComponent<Button>();
            }
        }
        if (boardEntryButton != null)
        {
            boardEntryButton.onClick.RemoveAllListeners();
            boardEntryButton.onClick.AddListener(OpenBoard);
        }
    }

    /// <summary>打开看板选择弹窗（列出已解锁佣兵，可设为看板或恢复默认）。</summary>
    public void OpenBoard()
    {
        var popup = BoardSelectPopupUI.Ensure(transform);
        if (popup == null)
        {
            UIManager.Instance?.ShowToast("看板未就绪");
            return;
        }
        popup.Show();
    }

    void FlipPortrait()
    {
        if (portraitImage == null) return;
        _portraitFlipped = !_portraitFlipped;
        var s = portraitImage.rectTransform.localScale;
        float ax = Mathf.Abs(s.x) < 0.01f ? 1f : Mathf.Abs(s.x);
        s.x = ax * (_portraitFlipped ? -1f : 1f);
        portraitImage.rectTransform.localScale = s;
        portraitImage.GetComponent<PortraitIdleMotion>()?.RefreshBase();
    }

    /// <summary>立绘保持预制体框尺寸，只换 Sprite，禁止 SetNativeSize 撑破布局。</summary>
    public void SetPortrait(Sprite sprite, bool flip = false)
    {
        if (portraitImage == null) return;
        portraitImage.sprite = sprite;
        // 立绘按原比例 fit 入框（preserveAspect），避免更瘦屏/异比例立绘被拉伸变形。
        portraitImage.preserveAspect = true;
        if (sprite != null)
            portraitImage.enabled = true;
        _portraitFlipped = flip;
        var s = portraitImage.rectTransform.localScale;
        float ax = Mathf.Abs(s.x) < 0.01f ? 1f : Mathf.Abs(s.x);
        s.x = ax * (flip ? -1f : 1f);
        portraitImage.rectTransform.localScale = s;
        EnsureStageShadow();
        if (sprite != null)
            PortraitIdleMotion.EnsureOn(portraitImage.rectTransform, 0.42f);
        else
        {
            var idle = portraitImage.GetComponent<PortraitIdleMotion>();
            if (idle != null) idle.enabled = false;
        }
    }

    void StripLocalChrome()
    {
        DestroyChildNamed(transform, "TopBar");
        DestroyChildNamed(transform, "SharedResourceBar");
        DestroyChildNamed(transform, "BottomNav");
        DestroyChildNamed(transform, "MainBottomNav");
    }

    static void DestroyChildNamed(Transform root, string name)
    {
        var t = root.Find(name);
        if (t != null) Destroy(t.gameObject);
    }

    void EnsureVisibleTransform()
    {
        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 立绘与影子等比对齐兜底（不改预制体文件）：
    /// 1) 预制体保存时把 shadow 的 localScale 存成了 0，影子整个不可见；
    /// 2) 立绘开了 preserveAspect 后实际显示宽随图片比例变化，影子宽度要跟着等比联动。
    /// 影子图是 SPUM 的白椭圆（1024×1024，椭圆约占七成宽），中心贴在人物脚底。
    /// </summary>
    void EnsureStageShadow()
    {
        var stage = transform.Find("Content/Stage");
        var shadowRt = stage != null ? stage.Find("shadow") as RectTransform : null;
        if (shadowRt == null) return;
        if (shadowRt.localScale.sqrMagnitude < 0.0001f)
            shadowRt.localScale = Vector3.one;
        if (portraitImage == null || portraitImage.sprite == null) return;

        var box = portraitImage.rectTransform;
        float boxW = box.rect.width, boxH = box.rect.height;
        if (boxW < 1f || boxH < 1f) return;

        var sp = portraitImage.sprite;
        float ar = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        float boxAr = boxW / boxH;
        float drawW = boxW, drawH = boxH;
        if (ar > boxAr) drawH = drawW / ar;
        else drawW = drawH * ar;

        // 椭圆可见宽 ≈ 节点宽 × 0.7，让影子略宽于人物肩宽
        float shadowW = drawW * 1.3f;
        shadowRt.sizeDelta = new Vector2(shadowW, shadowW);

        // 脚底 y：必须计入 Portrait 的 pivot（预制体里是 (0.5, 0)）与 localScale（0.5），
        // 否则算出来的点不在 Anchor 同一个坐标系里、会掉到画面外导致「阴影没了」。
        //   1) preserveAspect 把图画在 rect 内居中 ⇒ 局部底边 y = rect.yMin + (boxH - drawH) * 0.5
        //   2) 用 Transform 换算到 Stage 局部空间 ⇒ 自动带上 localScale / 翻转 / 旋转
        //   3) 影子与立绘同父但锚点可能不同 ⇒ anchoredPosition = 父空间点 - 影子锚点在父空间的 y
        var parentRt = shadowRt.parent as RectTransform;
        if (parentRt == null) return;
        float localFootY = box.rect.yMin + (boxH - drawH) * 0.5f;
        Vector3 worldFoot = box.TransformPoint(new Vector3(box.rect.center.x, localFootY, 0f));
        Vector2 footInParent = parentRt.InverseTransformPoint(worldFoot);
        float anchorY = parentRt.rect.yMin + shadowRt.anchorMin.y * parentRt.rect.height;
        shadowRt.anchoredPosition = new Vector2(footInParent.x, footInParent.y - anchorY);
    }

    bool _canvasConfigured;
    void ConfigureHostCanvasOnce()
    {        if (_canvasConfigured) return;
        EnsureVisibleTransform();
        TownPageCanvas.Configure(gameObject, 20, stripCanvasWhenNested: true);
        _canvasConfigured = true;
    }

    static void SetGuildOverlay(bool characterOpen)
    {
        TavernUI.SetGuildHallOverlayMode(characterOpen);
    }

    public void AutoBind()
    {
        titleText = FindTxt("Content/Header/TitleText");
        // 主人会把中文节点名改成英文：英文名优先、中文名回退，两种都能命中
        titleSubText = FindTxt("Content/Header/SubTitle")
                       ?? FindTxt("Content/Header/称号")
                       ?? FindTxt("Content/Header/TitleSub");
        headerAvatar = transform.Find("Content/Header/icon")?.GetComponent<Image>();
        talentButton = transform.Find("Content/Stage/RightButtons/TalentButton")?.GetComponent<Button>();
        skillButton = transform.Find("Content/Stage/RightButtons/SkillButton")?.GetComponent<Button>();
        // 2026-09-25 适配主人新层级：LeftSkillButton 已改名且暂时不要了，原节点在预制体里不复存在。
        // leftButtonRoot 只用于定位/参考（指向 LeftButton 组），代码不会调整它的显隐；
        // leftSkillButton 保持 null（不再查找、不再赋值），保留公开字段以免其它文件引用报错。
        leftButtonRoot = transform.Find("Content/Stage/LeftButton");
        portraitImage = transform.Find("Content/Stage/Portrait")?.GetComponent<Image>();
        // 2026-09-25：flipPortraitButton 只绑旧名 Stage/FlipButton（现已不存在 → 为 null）；
        // 主人把「立绘」按钮改成了看板入口，已绑给 boardEntryButton，不可再绑给翻转。
        flipPortraitButton = transform.Find("Content/Stage/FlipButton")?.GetComponent<Button>();
        // 看板入口：主人改英文后优先 BoardButton，中文旧名「立绘」回退，再退回旧运行时生成的 BoardButton
        boardEntryButton = transform.Find("Content/Stage/RightButtons/BoardButton")?.GetComponent<Button>()
                           ?? transform.Find("Content/Stage/RightButtons/立绘")?.GetComponent<Button>();
        // 背包入口：2026-09-25 主人把按钮移到右列了 → 优先 Content/Stage/RightButtons，
        // 再回退左列（LeftButton），最后才是旧路径 Content/Stage/LeftButton。
        backpackEntryButton = transform.Find("Content/Stage/RightButtons/BackpackButton")?.GetComponent<Button>()
                              ?? transform.Find("Content/Stage/RightButtons/背包")?.GetComponent<Button>();
        if (backpackEntryButton == null && leftButtonRoot != null)
            backpackEntryButton = leftButtonRoot.Find("BackpackButton")?.GetComponent<Button>()
                                  ?? leftButtonRoot.Find("背包")?.GetComponent<Button>();
        if (backpackEntryButton == null)
            backpackEntryButton = transform.Find("Content/Stage/LeftButton/BackpackButton")?.GetComponent<Button>();
        EnsureCarriedSkillIcon();

        attrHpText = FindTxt("Content/AttrPanel/AttrHpRoot/AttrHp") ?? FindTxt("Content/AttrPanel/AttrHp");
        attrAtkText = FindTxt("Content/AttrPanel/AttrAtkRoot/AttrAtk") ?? FindTxt("Content/AttrPanel/AttrAtk");
        attrDefText = FindTxt("Content/AttrPanel/AttrDefRoot/AttrDef") ?? FindTxt("Content/AttrPanel/AttrDef");
        attrSpdText = FindTxt("Content/AttrPanel/AttrSpdRoot/AttrSpd") ?? FindTxt("Content/AttrPanel/AttrSpd");
        attrCritText = FindTxt("Content/AttrPanel/AttrCritRoot/AttrCrit") ?? FindTxt("Content/AttrPanel/AttrCrit");
        attrResistText = FindTxt("Content/AttrPanel/AttrResistRoot/AttrResist") ?? FindTxt("Content/AttrPanel/AttrResist");

        // 界面内背包面板（「背包」按钮切换用；可能在预制体里被展开着，初始化时会收起）
        bagPanelRoot = transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel");
        bagCapacityText = FindTxt("Content/BackpackPanel/CapacityText")
                          ?? FindTxt("Content/BagPanel/CapacityText");
        bagExpandButton = transform.Find("Content/BackpackPanel/CapacityPlus")?.GetComponent<Button>()
                          ?? transform.Find("Content/BagPanel/CapacityPlus")?.GetComponent<Button>();
        // 角色页只用自己的专属网格；预制体上自带的通用 TownBackpackGrid 会被运行时停用
        var bag = ResolveBagGrid();
        if (useWideBackpackGrid)
        {
            bag.columns = WideBackpackColumns;
            bag.unlockedDisplayRows = WideBackpackUnlockedRows;
        }
        backpackGrid.BindFromHierarchy(transform);
        // 拿到网格后立刻套「8 列 / 下 2 行锁」的显示规格（只作用于本组件实例）
        ApplyWideBackpackGrid();

        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect != null)
        {
            skillSelect.onSkillSelected -= OnSkillSelectedFromPanel;
            skillSelect.onSkillSelected += OnSkillSelectedFromPanel;
        }
    }

    void OnSkillSelectedFromPanel(int _)
    {
        RefreshCarriedSkill();
    }

    Text FindTxt(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    /// <summary>
    /// 深度查找：在 root 的所有后代里（不含 root 自身）找第一个同名节点，找不到返回 null。
    /// 只给 transform.Find("硬编码路径") 当兜底 —— 以后主人再挪层级（例如把 SkillSelectUI
    /// 挪进 Popups）也不会断。先按精确匹配。
    /// </summary>
    static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>编辑器首次建树；已换美术勿覆盖</summary>
    public void BuildHierarchyForPrefab()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var content = CreateRect(transform, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(0f, BottomReserve);
        crt.offsetMax = new Vector2(0f, -TopReserve);

        // Header
        var header = CreateImg(content.transform, "Header", new Color(0.28f, 0.18f, 0.1f, 0.9f));
        Set(header.rectTransform, 0f, 1f, 1f, 1f, 0.5f, 1f, 0f, 0f, 0f, 56f);
        var title = CreateTxt(header.transform, "TitleText", "角色", 32, new Color(1f, 0.9f, 0.55f));
        Set(title.rectTransform, 0f, 0f, 0.4f, 1f, 0f, 0.5f, 24f, 0f, 0f, 0f);
        title.alignment = TextAnchor.MiddleLeft;

        // Stage：立绘 + 左技能钮 + 右天赋/技能（无装备栏）
        var stage = CreateImg(content.transform, "Stage", new Color(0.35f, 0.28f, 0.22f, 1f));
        var srt = stage.rectTransform;
        srt.anchorMin = new Vector2(0f, 0.42f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(12f, 0f);
        srt.offsetMax = new Vector2(-12f, -64f);

        var portrait = CreateImg(stage.transform, "Portrait", new Color(0.45f, 0.5f, 0.55f, 0.85f));
        portrait.preserveAspect = true;
        Set(portrait.rectTransform, 0.5f, 0.15f, 0.5f, 0.15f, 0.5f, 0f, 0f, 0f, 160f, 220f);

        var leftBtn = CreateImg(stage.transform, "LeftSkillButton", new Color(0.4f, 0.32f, 0.55f, 1f));
        Set(leftBtn.rectTransform, 0f, 0.55f, 0f, 0.55f, 0f, 0.5f, 16f, 0f, 100f, 72f);
        leftBtn.gameObject.AddComponent<Button>().targetGraphic = leftBtn;
        var leftLab = CreateTxt(leftBtn.transform, "Label", "技能", 24, Color.white);
        Stretch(leftLab.rectTransform);

        var right = CreateRect(stage.transform, "RightButtons");
        Set(right.GetComponent<RectTransform>(), 1f, 0.2f, 1f, 0.95f, 1f, 1f, -12f, 0f, 120f, 0f);
        BuildSideBtn(right.transform, "TalentButton", "天赋", 0f, new Color(0.35f, 0.55f, 0.35f, 1f));
        BuildSideBtn(right.transform, "SkillButton", "技能", -90f, new Color(0.4f, 0.35f, 0.65f, 1f));

        // Attr panel — 宽度随界面
        var attrs = CreateImg(content.transform, "AttrPanel", new Color(0.22f, 0.16f, 0.12f, 0.95f));
        var art = attrs.rectTransform;
        art.anchorMin = new Vector2(0f, 0.30f);
        art.anchorMax = new Vector2(1f, 0.42f);
        art.offsetMin = new Vector2(12f, 4f);
        art.offsetMax = new Vector2(-12f, -4f);
        var attrTitle = CreateTxt(attrs.transform, "AttrTitle", "基础属性", 22, new Color(1f, 0.92f, 0.75f));
        Set(attrTitle.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -4f, 200f, 28f);
        CreateAttr(attrs.transform, "AttrHp", "生命", "5240", 0f);
        CreateAttr(attrs.transform, "AttrAtk", "攻击", "1280", 1f);
        CreateAttr(attrs.transform, "AttrDef", "防御", "860", 2f);
        CreateAttr(attrs.transform, "AttrSpd", "速度", "105", 3f);
        CreateAttr(attrs.transform, "AttrCrit", "暴击", "18%", 4f);
        CreateAttr(attrs.transform, "AttrResist", "抗性", "15%", 5f);

        // Bag — 宽度自适应，格子与战斗一致
        var bag = CreateImg(content.transform, "BagPanel", new Color(0.4f, 0.28f, 0.16f, 1f));
        var brt = bag.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0.30f);
        brt.offsetMin = new Vector2(12f, 8f);
        brt.offsetMax = new Vector2(-12f, -4f);
        var bagTitle = CreateTxt(bag.transform, "BagTitle", "背包", 24, new Color(1f, 0.92f, 0.75f));
        Set(bagTitle.rectTransform, 0f, 1f, 0f, 1f, 0f, 1f, 16f, -8f, 100f, 32f);
        bagTitle.alignment = TextAnchor.MiddleLeft;
        var cap = CreateTxt(bag.transform, "CapacityText", "0/21", 22, new Color(1f, 0.95f, 0.8f));
        Set(cap.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, -56f, -8f, 100f, 32f);
        cap.alignment = TextAnchor.MiddleRight;
        var plus = CreateImg(bag.transform, "CapacityPlus", new Color(0.55f, 0.35f, 0.2f, 1f));
        Set(plus.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, -12f, -8f, 32f, 32f);
        plus.gameObject.AddComponent<Button>().targetGraphic = plus;
        var plusL = CreateTxt(plus.transform, "Label", "+", 22, Color.white);
        Stretch(plusL.rectTransform);

        ResolveBagGrid();
        backpackGrid.BuildGrid(bag.transform);

        // 底部五入口占位（不自建按钮，仅留空给 SharedChrome）
        var reserve = CreateRect(transform, "BottomNavReserve");
        var rrt = reserve.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0f, 0f);
        rrt.anchorMax = new Vector2(1f, 0f);
        rrt.pivot = new Vector2(0.5f, 0f);
        rrt.sizeDelta = new Vector2(0f, BottomReserve);
        rrt.anchoredPosition = Vector2.zero;

        // Skill select popup
        var skillGo = new GameObject("SkillSelectUI", typeof(RectTransform));
        skillGo.transform.SetParent(transform, false);
        Stretch(skillGo.GetComponent<RectTransform>());
        skillSelect = skillGo.AddComponent<SkillSelectUI>();
        skillSelect.BuildHierarchyForPrefab();

        AutoBind();
        GameFonts.ApplyToHierarchy(transform);
        _built = true;
    }

    static void BuildSideBtn(Transform parent, string name, string label, float y, Color col)
    {
        var img = CreateImg(parent, name, col);
        Set(img.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, 0f, y, 110f, 72f);
        img.gameObject.AddComponent<Button>().targetGraphic = img;
        var t = CreateTxt(img.transform, "Label", label, 24, Color.white);
        Stretch(t.rectTransform);
    }

    static void CreateAttr(Transform parent, string name, string label, string value, float index)
    {
        float x0 = index / 6f;
        float x1 = (index + 1f) / 6f;
        var root = CreateRect(parent, name + "Root");
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, 0f);
        rt.anchorMax = new Vector2(x1, 0.7f);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = new Vector2(-4f, -4f);
        var lab = CreateTxt(root.transform, "Label", label, 16, new Color(0.85f, 0.8f, 0.7f));
        Set(lab.rectTransform, 0f, 0.55f, 1f, 1f, 0.5f, 0.5f, 0f, 0f, 0f, 0f);
        // 数值节点名与 AutoBind 一致
        var val = CreateTxt(root.transform, name, value, 20, new Color(0.7f, 0.95f, 0.7f));
        Set(val.rectTransform, 0f, 0f, 1f, 0.55f, 0.5f, 0.5f, 0f, 0f, 0f, 0f);
    }

    static GameObject CreateRect(Transform p, string n)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        return go;
    }

    static Image CreateImg(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(p, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text CreateTxt(Transform p, string n, string t, int size, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(p, false);
        var tx = go.GetComponent<Text>();
        tx.text = t;
        tx.fontSize = size;
        tx.color = c;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.raycastTarget = false;
        tx.font = GameFonts.GetChinese();
        return tx;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Set(RectTransform rt, float aminX, float aminY, float amaxX, float amaxY,
        float px, float py, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(aminX, aminY);
        rt.anchorMax = new Vector2(amaxX, amaxY);
        rt.pivot = new Vector2(px, py);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
