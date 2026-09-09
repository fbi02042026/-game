using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色页（MainNavTab.Character）。参考 Art/UI/Character/character_reference.png。
/// - 无左侧装备栏
/// - 右上：天赋 / 技能；左侧独立按钮也可打开技能选择
/// - 背包格子按角色页预制体显示（8×5）；最下方两行天赋解锁
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
    public Button leftSkillButton; // 左侧独立按钮，同样打开技能界面

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

    [Header("弹层")]
    public SkillSelectUI skillSelect;

    bool _built;
    bool _preloaded;
    bool _wired;
    bool _portraitFlipped;
    bool _bagEventsWired;

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

        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);

        // 页可能比背包系统更早预载，Show 时再补订一次
        WireBagEvents();
        RefreshAll();
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
        RefreshCarriedSkill();
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
        var sp = MercPortraitSprites.GetStand("player");
        if (sp != null)
            SetPortrait(sp, _portraitFlipped);
        else
            portraitImage.enabled = true;
    }

    void RefreshCarriedSkill()
    {
        EnsureCarriedSkillIcon();
        if (carriedSkillIcon == null) return;
        string id = SaveSystem.Instance?.Data?.selectedPlayerSkillId;
        if (string.IsNullOrEmpty(id))
            id = PlayerSkillDefs.All != null && PlayerSkillDefs.All.Length > 0 ? PlayerSkillDefs.All[0].id : null;
        Sprite sp = LoadPlayerSkillIcon(id);
        if (sp != null)
        {
            carriedSkillIcon.sprite = sp;
            carriedSkillIcon.enabled = true;
            carriedSkillIcon.preserveAspect = true;
            carriedSkillIcon.color = Color.white;
        }
        carriedSkillIcon.gameObject.SetActive(true);
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
    /// 技能图画在 LeftSkillButton 自身 Image 上；子节点 Image 是装饰，禁止隐藏/禁止新建 Icon。
    /// </summary>
    void EnsureCarriedSkillIcon()
    {
        if (leftSkillButton == null) return;

        // 清掉误生成的 Icon
        var badIcon = leftSkillButton.transform.Find("Icon");
        if (badIcon != null)
            Destroy(badIcon.gameObject);

        // 装饰图保持显示
        var deco = leftSkillButton.transform.Find("Image");
        if (deco != null)
            deco.gameObject.SetActive(true);

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
        var bagPanel = transform.Find("Content/BackpackPanel")?.GetComponent<Image>();
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
                src = new AttrSystem();
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
        if (TalentDefs.RightExtra != null &&
            talents.TryGetValue(TalentDefs.RightExtra.id, out int c1) && c1 > 0)
            AccumulateChoiceTalent(TalentDefs.RightExtra.options, c1, ref atk, ref hp, ref def, ref crit, ref spd);
        for (int i = 0; i < TalentDefs.Right.Length; i++)
        {
            if (!talents.TryGetValue(TalentDefs.Right[i].id, out int opt) || opt <= 0) continue;
            AccumulateChoiceTalent(TalentDefs.Right[i].options, opt, ref atk, ref hp, ref def, ref crit, ref spd);
        }
    }

    static void AccumulateChoiceTalent(TalentDefs.ChoiceOption[] options, int opt,
        ref float atk, ref float hp, ref float def, ref float crit, ref float spd)
    {
        if (options == null || opt <= 0 || opt > options.Length) return;
        var e = options[opt - 1].effect;
        if (e == null) return;
        switch (e.kind)
        {
            case TalentDefs.AttrKind.Attack: atk += e.value; break;
            case TalentDefs.AttrKind.Hp: hp += e.value; break;
            case TalentDefs.AttrKind.Defense: def += e.value; break;
            case TalentDefs.AttrKind.CritRate: crit += e.value; break;
            case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
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
        if (leftSkillButton != null)
        {
            leftSkillButton.onClick.RemoveAllListeners();
            leftSkillButton.onClick.AddListener(OpenSkillSelect);
        }
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
        portraitImage.preserveAspect = true;
        if (sprite != null)
            portraitImage.enabled = true;
        _portraitFlipped = flip;
        var s = portraitImage.rectTransform.localScale;
        float ax = Mathf.Abs(s.x) < 0.01f ? 1f : Mathf.Abs(s.x);
        s.x = ax * (flip ? -1f : 1f);
        portraitImage.rectTransform.localScale = s;
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

    bool _canvasConfigured;
    void ConfigureHostCanvasOnce()
    {
        if (_canvasConfigured) return;
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
        titleSubText = FindTxt("Content/Header/称号")
                       ?? FindTxt("Content/Header/TitleSub")
                       ?? FindTxt("Content/Header/SubTitle");
        headerAvatar = transform.Find("Content/Header/icon")?.GetComponent<Image>();
        talentButton = transform.Find("Content/Stage/RightButtons/TalentButton")?.GetComponent<Button>();
        skillButton = transform.Find("Content/Stage/RightButtons/SkillButton")?.GetComponent<Button>();
        leftSkillButton = transform.Find("Content/Stage/LeftSkillButton")?.GetComponent<Button>();
        portraitImage = transform.Find("Content/Stage/Portrait")?.GetComponent<Image>();
        flipPortraitButton = transform.Find("Content/Stage/FlipButton")?.GetComponent<Button>();
        EnsureCarriedSkillIcon();

        attrHpText = FindTxt("Content/AttrPanel/AttrHpRoot/AttrHp") ?? FindTxt("Content/AttrPanel/AttrHp");
        attrAtkText = FindTxt("Content/AttrPanel/AttrAtkRoot/AttrAtk") ?? FindTxt("Content/AttrPanel/AttrAtk");
        attrDefText = FindTxt("Content/AttrPanel/AttrDefRoot/AttrDef") ?? FindTxt("Content/AttrPanel/AttrDef");
        attrSpdText = FindTxt("Content/AttrPanel/AttrSpdRoot/AttrSpd") ?? FindTxt("Content/AttrPanel/AttrSpd");
        attrCritText = FindTxt("Content/AttrPanel/AttrCritRoot/AttrCrit") ?? FindTxt("Content/AttrPanel/AttrCrit");
        attrResistText = FindTxt("Content/AttrPanel/AttrResistRoot/AttrResist") ?? FindTxt("Content/AttrPanel/AttrResist");

        bagCapacityText = FindTxt("Content/BackpackPanel/CapacityText")
                          ?? FindTxt("Content/BagPanel/CapacityText");
        bagExpandButton = transform.Find("Content/BackpackPanel/CapacityPlus")?.GetComponent<Button>()
                          ?? transform.Find("Content/BagPanel/CapacityPlus")?.GetComponent<Button>();
        backpackGrid = GetComponent<TownBackpackGrid>();
        if (backpackGrid == null) backpackGrid = gameObject.AddComponent<TownBackpackGrid>();
        backpackGrid.BindFromHierarchy(transform);

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

        backpackGrid = gameObject.GetComponent<TownBackpackGrid>();
        if (backpackGrid == null) backpackGrid = gameObject.AddComponent<TownBackpackGrid>();
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
