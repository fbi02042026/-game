using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 招募佣兵弹窗：优先加载 Resources/Prefabs/Town/MercenaryRecruitPopup。
/// 结构对齐设计图：标题 + 三选一卡 + 刷新/剩余次数。
/// </summary>
public class MercenaryRecruitPopupUI : MonoBehaviour
{
    public const string ResourcePath = "Prefabs/Town/MercenaryRecruitPopup";

    public static MercenaryRecruitPopupUI Instance { get; private set; }

    [Header("壳")]
    public GameObject root;
    public Button closeButton;
    public Text titleText;
    public Text subtitleText;

    [Header("三选一")]
    public CardView[] cards = new CardView[3];

    [Header("底栏")]
    public Text remainText;
    public Button refreshButton;
    public Text refreshCostText;
    public Text autoRefreshText;
    public Toggle skipAnimToggle;
    public Button confirmButton;
    public Text confirmLabel;

    [System.Serializable]
    public class CardView
    {
        public GameObject root;
        public Image background;
        public Image portrait;
        public Text nameText;
        public Text rarityText;
        public Text roleText;
        public Text skill1Text;
        public Text skill2Text;
        public Button button;
    }

    List<MercenaryData> _offers = new List<MercenaryData>();
    int _selected;
    bool _wired;

    public static void Show()
    {
        Ensure().Open();
    }

    public static MercenaryRecruitPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(ResourcePath);
        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
            go.name = "MercenaryRecruitPopup";
        }
        else
        {
            Debug.LogWarning("[MercenaryRecruit] 未找到预制体 " + ResourcePath + "，使用代码兜底壳");
            go = new GameObject("MercenaryRecruitPopup", typeof(RectTransform));
            var ui = go.AddComponent<MercenaryRecruitPopupUI>();
            ui.BuildFallbackHierarchy();
            Object.DontDestroyOnLoad(go);
            return ui;
        }

        Object.DontDestroyOnLoad(go);
        var host = go.GetComponent<MercenaryRecruitPopupUI>();
        if (host == null) host = go.AddComponent<MercenaryRecruitPopupUI>();
        return host;
    }

    void Awake()
    {
        Instance = this;
        AutoBind();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open()
    {
        AutoBind();
        EnsureCanvas();
        TownSaveAlign.AlignAll();
        EnsureDailyRecruitReset();
        RerollOffers();
        RefreshAll();
        WireOnce();
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    static void EnsureDailyRecruitReset()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        string today = System.DateTime.UtcNow.ToString("yyyyMMdd");
        if (data.dailyMercRecruitDayKey == today) return;
        data.dailyMercRecruitDayKey = today;
        data.dailyMercRecruitUsed = 0;
    }

    void RerollOffers()
    {
        _offers = MercenaryOfferGenerator.GenerateOffers();
        _selected = 0;
    }

    void RefreshAll()
    {
        var data = SaveSystem.Instance?.Data;
        int used = data != null ? data.dailyMercRecruitUsed : 0;
        int max = Mathf.Max(1, GameConfig.DAILY_MERC_RECRUIT_MAX);
        if (remainText != null)
            remainText.text = $"今日剩余招募次数: {Mathf.Max(0, max - used)}/{max}";

        if (refreshCostText != null)
            refreshCostText.text = GameConfig.MERC_REROLL_GEM_COST.ToString();

        if (autoRefreshText != null)
            autoRefreshText.text = "自动刷新：次日 0 点";

        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            var offer = i < _offers.Count ? _offers[i] : null;
            bool on = offer != null;
            c.root.SetActive(on);
            if (!on) continue;

            if (c.nameText != null)
                c.nameText.text = string.IsNullOrEmpty(offer.displayName) ? offer.mercId : offer.displayName;
            if (c.rarityText != null)
                c.rarityText.text = StarToRarity(offer.star);
            if (c.roleText != null)
                c.roleText.text = "定位：" + GuessRole(offer.mercId);
            if (c.skill1Text != null)
                c.skill1Text.text = MercenaryOfferGenerator.SkillDisplayName(offer.skillId);
            if (c.skill2Text != null)
                c.skill2Text.text = $"Lv{Mathf.Max(1, offer.level)}  ★{Mathf.Clamp(offer.star, 1, 5)}";
            if (c.portrait != null)
            {
                var sp = MercenaryManager.Instance != null
                    ? MercenaryManager.Instance.GetIcon(offer.mercId)
                    : null;
                c.portrait.sprite = sp;
                c.portrait.enabled = sp != null;
                c.portrait.preserveAspect = true;
            }
            if (c.background != null)
                c.background.color = i == _selected
                    ? RarityColor(offer.star)
                    : new Color(0.16f, 0.14f, 0.18f, 0.95f);
        }

        if (confirmLabel != null)
            confirmLabel.text = "招募选中";
    }

    static string StarToRarity(int star)
    {
        if (star >= 5) return "传奇";
        if (star >= 3) return "稀有";
        return "普通";
    }

    static string GuessRole(string mercId)
    {
        if (string.IsNullOrEmpty(mercId)) return "输出型";
        string id = mercId.ToLowerInvariant();
        if (id.Contains("dun") || id.Contains("shield")) return "坦克型";
        if (id.Contains("nai") || id.Contains("heal")) return "辅助型";
        return "输出型";
    }

    static Color RarityColor(int star)
    {
        if (star >= 5) return new Color(0.45f, 0.28f, 0.12f, 1f);
        if (star >= 3) return new Color(0.18f, 0.28f, 0.45f, 1f);
        return new Color(0.18f, 0.36f, 0.22f, 1f);
    }

    void WireOnce()
    {
        if (_wired) return;
        _wired = true;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (refreshButton != null) refreshButton.onClick.AddListener(OnRefresh);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        for (int i = 0; i < cards.Length; i++)
        {
            int idx = i;
            var c = cards[i];
            if (c == null) continue;
            if (c.button == null && c.root != null)
                c.button = c.root.GetComponent<Button>() ?? c.root.AddComponent<Button>();
            if (c.button != null)
                c.button.onClick.AddListener(() => Select(idx));
        }
    }

    void Select(int idx)
    {
        _selected = idx;
        RefreshAll();
    }

    void OnRefresh()
    {
        int cost = GameConfig.MERC_REROLL_GEM_COST;
        if (cost > 0 && !ResourceWallet.TrySpend(ResourceWallet.ResourceType.Diamond, cost, save: true, notify: true))
        {
            UIManager.Instance?.ShowToast("宝石不足");
            return;
        }
        RerollOffers();
        RefreshAll();
    }

    void OnConfirm()
    {
        if (_selected < 0 || _selected >= _offers.Count || _offers[_selected] == null)
        {
            UIManager.Instance?.ShowToast("请先选择佣兵");
            return;
        }

        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        int max = Mathf.Max(1, GameConfig.DAILY_MERC_RECRUIT_MAX);
        if (data.dailyMercRecruitUsed >= max)
        {
            UIManager.Instance?.ShowToast("今日招募次数已用完");
            return;
        }

        var picked = CloneOffer(_offers[_selected]);
        if (data.permanentMercs == null)
            data.permanentMercs = new List<MercenaryData>();
        data.permanentMercs.Add(picked);
        data.dailyMercRecruitUsed++;
        if (data.townLevel == null) data.townLevel = new TownLevel();
        if (data.townLevel.tavern < 1) data.townLevel.tavern = 1;
        SaveSystem.Instance.Save();
        UIManager.Instance?.ShowToast($"已招募：{picked.displayName}");
        Close();
    }

    static MercenaryData CloneOffer(MercenaryData src)
    {
        return new MercenaryData
        {
            mercId = src.mercId,
            displayName = src.displayName,
            uid = System.Guid.NewGuid().ToString("N"),
            level = src.level,
            star = src.star,
            skillId = src.skillId
        };
    }

    void Close()
    {
        if (root != null) root.SetActive(false);
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 920;
        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 1f;
        }
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void AutoBind()
    {
        if (root == null)
        {
            var t = transform.Find("Root");
            root = t != null ? t.gameObject : gameObject;
        }
        if (closeButton == null) closeButton = FindBtn("CloseButton");
        if (titleText == null) titleText = FindTxt("Title");
        if (subtitleText == null) subtitleText = FindTxt("Subtitle");
        if (remainText == null) remainText = FindTxt("RemainText");
        if (refreshButton == null) refreshButton = FindBtn("RefreshButton");
        if (refreshCostText == null) refreshCostText = FindTxt("RefreshCost");
        if (autoRefreshText == null) autoRefreshText = FindTxt("AutoRefresh");
        if (skipAnimToggle == null)
        {
            var t = FindDeep(transform, "SkipAnim");
            if (t != null) skipAnimToggle = t.GetComponent<Toggle>();
        }
        if (confirmButton == null) confirmButton = FindBtn("ConfirmButton");
        if (confirmLabel == null && confirmButton != null)
            confirmLabel = confirmButton.GetComponentInChildren<Text>(true);

        if (cards == null || cards.Length < 3) cards = new CardView[3];
        for (int i = 0; i < 3; i++)
        {
            if (cards[i] == null) cards[i] = new CardView();
            var cardTf = FindDeep(transform, "Card" + i);
            if (cardTf == null) continue;
            var c = cards[i];
            c.root = cardTf.gameObject;
            if (c.background == null) c.background = cardTf.GetComponent<Image>();
            if (c.portrait == null) c.portrait = FindDeep(cardTf, "Portrait")?.GetComponent<Image>();
            if (c.nameText == null) c.nameText = FindDeep(cardTf, "Name")?.GetComponent<Text>();
            if (c.rarityText == null) c.rarityText = FindDeep(cardTf, "Rarity")?.GetComponent<Text>();
            if (c.roleText == null) c.roleText = FindDeep(cardTf, "Role")?.GetComponent<Text>();
            if (c.skill1Text == null) c.skill1Text = FindDeep(cardTf, "Skill1")?.GetComponent<Text>();
            if (c.skill2Text == null) c.skill2Text = FindDeep(cardTf, "Skill2")?.GetComponent<Text>();
            if (c.button == null) c.button = cardTf.GetComponent<Button>();
        }
    }

    Button FindBtn(string n) => FindDeep(transform, n)?.GetComponent<Button>();
    Text FindTxt(string n) => FindDeep(transform, n)?.GetComponent<Text>();

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>无预制体时的代码壳（布局对齐设计图，美术可再替换）。</summary>
    public void BuildFallbackHierarchy()
    {
        EnsureCanvas();
        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var dim = CreateImg(root.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.onClick.AddListener(Close);

        var panel = CreateImg(root.transform, "Panel", new Color(0.10f, 0.09f, 0.12f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(680f, 980f);

        titleText = CreateTxt(panel.transform, "Title", "招募佣兵", 34, TextAnchor.MiddleCenter);
        SetRect(titleText.rectTransform, 0.5f, 0.93f, 0f, 0f, 360f, 48f);

        subtitleText = CreateTxt(panel.transform, "Subtitle", "选择一名伙伴加入你的队伍", 18, TextAnchor.MiddleCenter);
        SetRect(subtitleText.rectTransform, 0.5f, 0.88f, 0f, 0f, 520f, 32f);
        subtitleText.color = new Color(0.85f, 0.85f, 0.88f, 1f);

        closeButton = CreateBtn(panel.transform, "CloseButton", "X", new Vector2(300f, 440f), new Vector2(56f, 56f));

        cards = new CardView[3];
        float cardW = 190f;
        float gap = 16f;
        float startX = -((cardW + gap));
        for (int i = 0; i < 3; i++)
        {
            var card = CreateImg(panel.transform, "Card" + i, new Color(0.16f, 0.14f, 0.18f, 0.95f));
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cardW, 520f);
            crt.anchoredPosition = new Vector2(startX + i * (cardW + gap), 20f);
            var btn = card.gameObject.AddComponent<Button>();

            var name = CreateTxt(card.transform, "Name", "佣兵", 18, TextAnchor.MiddleCenter);
            SetRect(name.rectTransform, 0.5f, 0.94f, 0f, 0f, 170f, 36f);

            var portrait = CreateImg(card.transform, "Portrait", new Color(0.3f, 0.3f, 0.35f, 1f));
            SetRect(portrait.rectTransform, 0.5f, 0.72f, 0f, 0f, 140f, 180f);
            portrait.preserveAspect = true;

            var rarity = CreateTxt(card.transform, "Rarity", "普通", 16, TextAnchor.MiddleCenter);
            SetRect(rarity.rectTransform, 0.5f, 0.48f, 0f, 0f, 120f, 28f);

            var role = CreateTxt(card.transform, "Role", "定位：输出型", 15, TextAnchor.MiddleCenter);
            SetRect(role.rectTransform, 0.5f, 0.42f, 0f, 0f, 170f, 28f);

            var s1 = CreateTxt(card.transform, "Skill1", "技能一", 14, TextAnchor.UpperCenter);
            SetRect(s1.rectTransform, 0.5f, 0.30f, 0f, 0f, 170f, 60f);

            var s2 = CreateTxt(card.transform, "Skill2", "技能二", 14, TextAnchor.UpperCenter);
            SetRect(s2.rectTransform, 0.5f, 0.16f, 0f, 0f, 170f, 60f);

            cards[i] = new CardView
            {
                root = card.gameObject,
                background = card,
                portrait = portrait,
                nameText = name,
                rarityText = rarity,
                roleText = role,
                skill1Text = s1,
                skill2Text = s2,
                button = btn
            };
        }

        remainText = CreateTxt(panel.transform, "RemainText", "今日剩余招募次数: 1/1", 18, TextAnchor.MiddleCenter);
        SetRect(remainText.rectTransform, 0.5f, 0.14f, 0f, 0f, 420f, 30f);

        refreshButton = CreateBtn(panel.transform, "RefreshButton", "刷新佣兵", new Vector2(0f, -380f), new Vector2(280f, 64f));
        refreshCostText = CreateTxt(refreshButton.transform, "RefreshCost", "50", 18, TextAnchor.MiddleCenter);
        SetRect(refreshCostText.rectTransform, 0.5f, 0.2f, 0f, -8f, 80f, 24f);

        autoRefreshText = CreateTxt(panel.transform, "AutoRefresh", "自动刷新：次日 0 点", 14, TextAnchor.MiddleCenter);
        SetRect(autoRefreshText.rectTransform, 0.5f, 0.06f, 0f, 0f, 360f, 24f);
        autoRefreshText.color = new Color(0.7f, 0.7f, 0.75f, 1f);

        confirmButton = CreateBtn(panel.transform, "ConfirmButton", "招募选中", new Vector2(0f, -450f), new Vector2(280f, 64f));
        confirmLabel = confirmButton.GetComponentInChildren<Text>();

        var skipGo = new GameObject("SkipAnim", typeof(RectTransform), typeof(Toggle));
        skipGo.transform.SetParent(panel.transform, false);
        SetRect(skipGo.GetComponent<RectTransform>(), 0.82f, 0.04f, 0f, 0f, 140f, 28f);
        skipAnimToggle = skipGo.GetComponent<Toggle>();
        var skipLabel = CreateTxt(skipGo.transform, "Label", "跳过动画", 14, TextAnchor.MiddleLeft);
        SetRect(skipLabel.rectTransform, 0.55f, 0.5f, 10f, 0f, 110f, 28f);

        AutoBind();
        GameFonts.ApplyToHierarchy(transform);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Image CreateImg(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text CreateTxt(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }

    static Button CreateBtn(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var img = CreateImg(parent, name, new Color(0.25f, 0.22f, 0.28f, 1f));
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        var txt = CreateTxt(img.transform, "Label", label, 22, TextAnchor.MiddleCenter);
        Stretch(txt.rectTransform);
        return btn;
    }
}
