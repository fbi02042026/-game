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
            remainText.text = $"今日剩余招募次数：{Mathf.Max(0, max - used)}/{max}";

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
                {
                    string sn = MercenaryOfferGenerator.SkillDisplayName(offer.skillId);
                    c.skill1Text.text = sn + "\n单体攻击或支援技能";
                }
                if (c.skill2Text != null)
                    c.skill2Text.text = $"被动成长\nLv{Mathf.Max(1, offer.level)}  ★{Mathf.Clamp(offer.star, 1, 5)}";
                if (c.portrait != null)
                {
                    var sp = MercenaryManager.Instance != null
                        ? MercenaryManager.Instance.GetIcon(offer.mercId)
                        : null;
                    c.portrait.sprite = sp;
                    c.portrait.enabled = true;
                    c.portrait.color = sp != null ? Color.white : new Color(0.35f, 0.32f, 0.38f, 1f);
                    c.portrait.preserveAspect = true;
                }
                // 未选中：稀有度底色；选中：加亮描边感
                if (c.background != null)
                {
                    Color baseCol = RarityColor(offer.star);
                    c.background.color = i == _selected
                        ? Color.Lerp(baseCol, Color.white, 0.18f)
                        : baseCol;
                }
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
        if (star >= 5) return new Color(0.42f, 0.26f, 0.10f, 1f); // 传奇金褐
        if (star >= 3) return new Color(0.14f, 0.24f, 0.42f, 1f); // 稀有蓝
        return new Color(0.14f, 0.32f, 0.20f, 1f);                 // 普通绿
    }

    static Color RarityBannerColor(int star)
    {
        if (star >= 5) return new Color(0.85f, 0.62f, 0.18f, 1f);
        if (star >= 3) return new Color(0.28f, 0.48f, 0.85f, 1f);
        return new Color(0.28f, 0.68f, 0.38f, 1f);
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
        AdventureCodex.MarkMercSeen(picked.mercId);
        AdventureLogAchievements.OnMercRecruited();
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

    /// <summary>无预制体时的代码壳（对齐设计图：标题 + 三卡 + 刷新/次数/跳过动画）。</summary>
    public void BuildFallbackHierarchy()
    {
        EnsureCanvas();
        // 清掉旧壳，避免重复生成
        for (int i = transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(transform.GetChild(i).gameObject);

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var dim = CreateImg(root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        // 外框（石质感深色）
        var frame = CreateImg(root.transform, "Frame", new Color(0.22f, 0.18f, 0.14f, 1f));
        var frt = frame.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(700f, 1120f);

        var panel = CreateImg(frame.transform, "Panel", new Color(0.09f, 0.08f, 0.10f, 0.98f));
        var prt = panel.rectTransform;
        Stretch(prt);
        prt.offsetMin = new Vector2(10f, 10f);
        prt.offsetMax = new Vector2(-10f, -10f);

        // 顶饰占位
        var crest = CreateImg(panel.transform, "Crest", new Color(0.72f, 0.58f, 0.22f, 0.9f));
        SetRect(crest.rectTransform, 0.5f, 0.97f, 0f, 0f, 56f, 56f);

        titleText = CreateTxt(panel.transform, "Title", "招募佣兵", 36, TextAnchor.MiddleCenter);
        SetRect(titleText.rectTransform, 0.5f, 0.915f, 0f, 0f, 400f, 48f);
        titleText.color = new Color(1f, 0.86f, 0.42f, 1f);
        titleText.fontStyle = FontStyle.Bold;

        subtitleText = CreateTxt(panel.transform, "Subtitle", "选择一名伙伴加入你的队伍", 17, TextAnchor.MiddleCenter);
        SetRect(subtitleText.rectTransform, 0.5f, 0.875f, 0f, 0f, 520f, 30f);
        subtitleText.color = new Color(0.88f, 0.88f, 0.90f, 1f);

        closeButton = CreateBtn(panel.transform, "CloseButton", "X", new Vector2(300f, 500f), new Vector2(52f, 52f));
        closeButton.GetComponent<Image>().color = new Color(0.35f, 0.26f, 0.16f, 1f);

        cards = new CardView[3];
        float cardW = 200f;
        float gap = 14f;
        float startX = -(cardW + gap);
        Color[] previewTint =
        {
            new Color(0.14f, 0.32f, 0.20f, 1f),
            new Color(0.14f, 0.24f, 0.42f, 1f),
            new Color(0.42f, 0.26f, 0.10f, 1f)
        };
        string[] previewRarity = { "普通", "稀有", "传奇" };
        for (int i = 0; i < 3; i++)
        {
            var card = CreateImg(panel.transform, "Card" + i, previewTint[i]);
            var crt = card.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cardW, 560f);
            crt.anchoredPosition = new Vector2(startX + i * (cardW + gap), 40f);
            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            var name = CreateTxt(card.transform, "Name", "佣兵名", 17, TextAnchor.MiddleCenter);
            SetRect(name.rectTransform, 0.5f, 0.945f, 0f, 0f, 186f, 34f);
            name.color = new Color(1f, 0.92f, 0.7f, 1f);

            var portrait = CreateImg(card.transform, "Portrait", new Color(0.28f, 0.26f, 0.30f, 1f));
            SetRect(portrait.rectTransform, 0.5f, 0.70f, 0f, 0f, 168f, 210f);
            portrait.preserveAspect = true;

            var rarityBanner = CreateImg(card.transform, "RarityBanner", RarityBannerColor(i == 2 ? 5 : (i == 1 ? 3 : 1)));
            SetRect(rarityBanner.rectTransform, 0.5f, 0.48f, 0f, 0f, 150f, 30f);
            var rarity = CreateTxt(rarityBanner.transform, "Rarity", previewRarity[i], 16, TextAnchor.MiddleCenter);
            Stretch(rarity.rectTransform);
            rarity.fontStyle = FontStyle.Bold;

            var role = CreateTxt(card.transform, "Role", "定位：输出型", 15, TextAnchor.MiddleCenter);
            SetRect(role.rectTransform, 0.5f, 0.415f, 0f, 0f, 180f, 26f);

            var s1 = CreateTxt(card.transform, "Skill1", "技能一\n描述", 13, TextAnchor.UpperLeft);
            SetRect(s1.rectTransform, 0.5f, 0.30f, 0f, 0f, 178f, 70f);
            s1.color = new Color(0.9f, 0.9f, 0.92f, 1f);

            var s2 = CreateTxt(card.transform, "Skill2", "技能二\n描述", 13, TextAnchor.UpperLeft);
            SetRect(s2.rectTransform, 0.5f, 0.15f, 0f, 0f, 178f, 70f);
            s2.color = new Color(0.9f, 0.9f, 0.92f, 1f);

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

        remainText = CreateTxt(panel.transform, "RemainText", "今日剩余招募次数：1/1", 18, TextAnchor.MiddleCenter);
        SetRect(remainText.rectTransform, 0.5f, 0.155f, 0f, 0f, 420f, 28f);

        refreshButton = CreateBtn(panel.transform, "RefreshButton", "刷新佣兵", new Vector2(0f, -420f), new Vector2(300f, 70f));
        refreshButton.GetComponent<Image>().color = new Color(0.20f, 0.18f, 0.22f, 1f);
        refreshCostText = CreateTxt(refreshButton.transform, "RefreshCost", "50", 16, TextAnchor.MiddleCenter);
        SetRect(refreshCostText.rectTransform, 0.5f, 0.18f, 0f, -6f, 100f, 22f);
        refreshCostText.color = new Color(0.45f, 0.75f, 1f, 1f);

        autoRefreshText = CreateTxt(panel.transform, "AutoRefresh", "自动刷新：次日 0 点", 14, TextAnchor.MiddleCenter);
        SetRect(autoRefreshText.rectTransform, 0.5f, 0.075f, 0f, 0f, 360f, 24f);
        autoRefreshText.color = new Color(0.72f, 0.78f, 0.45f, 1f);

        confirmButton = CreateBtn(panel.transform, "ConfirmButton", "招募选中", new Vector2(0f, -500f), new Vector2(300f, 64f));
        confirmButton.GetComponent<Image>().color = new Color(0.42f, 0.28f, 0.12f, 1f);
        confirmLabel = confirmButton.GetComponentInChildren<Text>();
        if (confirmLabel != null) confirmLabel.color = new Color(1f, 0.9f, 0.55f, 1f);

        var skipGo = new GameObject("SkipAnim", typeof(RectTransform), typeof(Toggle), typeof(Image));
        skipGo.transform.SetParent(panel.transform, false);
        SetRect(skipGo.GetComponent<RectTransform>(), 0.82f, 0.045f, 0f, 0f, 150f, 30f);
        skipGo.GetComponent<Image>().color = new Color(0.15f, 0.14f, 0.16f, 0.5f);
        skipAnimToggle = skipGo.GetComponent<Toggle>();
        var skipLabel = CreateTxt(skipGo.transform, "Label", "跳过动画", 14, TextAnchor.MiddleLeft);
        SetRect(skipLabel.rectTransform, 0.58f, 0.5f, 8f, 0f, 120f, 28f);

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
