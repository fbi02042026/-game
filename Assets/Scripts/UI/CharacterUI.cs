using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色页（MainNavTab.Character）。参考 Art/UI/Character/character_reference.png。
/// - 无左侧装备栏
/// - 右上：天赋 / 技能；左侧独立按钮也可打开技能选择
/// - 背包格子与战斗一致（TownBackpackGrid / GameConfig 7×4 + 底行锁）
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

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
            // 已从战斗复制格子时不要重建，避免盖掉手做网格
            if (backpackGrid.cells.Count == 0)
            {
                var bag = transform.Find("Content/BackpackPanel")
                          ?? transform.Find("Content/BagPanel");
                if (bag != null) backpackGrid.BuildGrid(bag);
            }
        }
        GameFonts.ApplyToHierarchy(transform);
        WireClicks();
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

        RefreshAll();
    }

    public void HidePage()
    {
        if (skillSelect != null) skillSelect.Hide();
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        SetGuildOverlay(false);
    }

    public void Show() => ShowPage();
    public void Hide() => HidePage();

    public void RefreshAll()
    {
        RefreshAttrs();
        RefreshBag();
    }

    void RefreshAttrs()
    {
        float hp = GameConfig.BASE_HP, atk = GameConfig.BASE_ATTACK, def = GameConfig.BASE_DEFENSE;
        float spd = GameConfig.BASE_ATTACK_SPEED, crit = GameConfig.BASE_CRIT_RATE, resist = 0f;
        try
        {
            var hero = Hero.Instance;
            if (hero != null && hero.attr != null)
            {
                hp = hero.attr.GetAttr(AttrType.MaxHp);
                atk = hero.attr.GetAttr(AttrType.Attack);
                def = hero.attr.GetAttr(AttrType.Defense);
                spd = hero.attr.GetAttr(AttrType.AttackSpeed);
                crit = hero.attr.GetAttr(AttrType.CritRate);
            }
            else
            {
                // 城镇无 Hero 时用天赋汇总估算展示
                int leftN = TalentDefs.LeftUnlockedCount(SaveSystem.Instance?.Data?.talents);
                for (int i = 0; i < leftN; i++)
                {
                    var e = TalentDefs.Left[i].effect;
                    switch (e.kind)
                    {
                        case TalentDefs.AttrKind.Attack: atk += e.value; break;
                        case TalentDefs.AttrKind.Hp: hp += e.value; break;
                        case TalentDefs.AttrKind.Defense: def += e.value; break;
                        case TalentDefs.AttrKind.CritRate: crit += e.value; break;
                        case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
                    }
                }
            }
        }
        catch { }

        float critDisplay = crit;
        if (Hero.Instance != null && Hero.Instance.attr != null && critDisplay <= 1.5f)
            critDisplay *= 100f;

        if (attrHpText != null) attrHpText.text = Mathf.RoundToInt(hp).ToString("N0");
        if (attrAtkText != null) attrAtkText.text = Mathf.RoundToInt(atk).ToString("N0");
        if (attrDefText != null) attrDefText.text = Mathf.RoundToInt(def).ToString("N0");
        if (attrSpdText != null) attrSpdText.text = spd.ToString("0.##");
        if (attrCritText != null) attrCritText.text = critDisplay.ToString("0.#") + "%";
        if (attrResistText != null) attrResistText.text = resist.ToString("0.#") + "%";
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
        if (TalentUI.Instance != null)
        {
            TalentUI.Instance.Show();
            return;
        }
        var prefab = Resources.Load<GameObject>("Prefabs/Talent/TalentUI");
        if (prefab == null)
        {
            UIManager.Instance?.ShowToast("天赋界面未就绪");
            return;
        }
        var go = Instantiate(prefab);
        go.name = "TalentUI";
        go.GetComponent<TalentUI>()?.Show();
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
    }

    /// <summary>立绘用自身像素大小，仅翻转</summary>
    public void SetPortrait(Sprite sprite, bool flip = false)
    {
        if (portraitImage == null) return;
        portraitImage.sprite = sprite;
        portraitImage.preserveAspect = true;
        if (sprite != null)
        {
            portraitImage.enabled = true;
            portraitImage.SetNativeSize();
        }
        _portraitFlipped = flip;
        var s = portraitImage.rectTransform.localScale;
        float ax = Mathf.Abs(s.x) < 0.01f ? 1f : Mathf.Abs(s.x);
        s.x = ax * (flip ? -1f : 1f);
        portraitImage.rectTransform.localScale = s;
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
        var hall = GetComponentInParent<GuildHallUI>();
        bool nested = hall != null && hall.gameObject != gameObject;
        if (nested)
        {
            var ray = GetComponent<GraphicRaycaster>();
            if (ray != null) Destroy(ray);
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null) Destroy(scaler);
            var own = GetComponent<Canvas>();
            if (own != null) Destroy(own);
            _canvasConfigured = true;
            return;
        }
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;
        UICanvasSetup.Apply(canvas, Camera.main);
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        _canvasConfigured = true;
    }

    static void SetGuildOverlay(bool characterOpen)
    {
        TavernUI.SetGuildHallOverlayMode(characterOpen);
    }

    public void AutoBind()
    {
        titleText = FindTxt("Content/Header/TitleText");
        talentButton = transform.Find("Content/Stage/RightButtons/TalentButton")?.GetComponent<Button>();
        skillButton = transform.Find("Content/Stage/RightButtons/SkillButton")?.GetComponent<Button>();
        leftSkillButton = transform.Find("Content/Stage/LeftSkillButton")?.GetComponent<Button>();
        portraitImage = transform.Find("Content/Stage/Portrait")?.GetComponent<Image>();
        flipPortraitButton = transform.Find("Content/Stage/FlipButton")?.GetComponent<Button>();

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
