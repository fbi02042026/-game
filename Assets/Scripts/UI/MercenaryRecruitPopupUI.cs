using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 招募佣兵弹窗：绑定手做 Prefab（Card0/1/2）。
/// 每卡双按钮：ConfirmButton=金币；ConfirmButton1=招募卷（稀有/传奇），无卷时显示金币价。
/// 雇佣写入 hiredMercs（下本结束离队）；图鉴 MarkMercSeen。
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
    public Button infoButton;

    [Header("三选一")]
    public CardView[] cards = new CardView[3];

    [Header("底栏")]
    public Text remainText;
    public Button refreshButton;
    public Text refreshCostText;
    public Text autoRefreshText;

    GameObject _infoRoot;

    [System.Serializable]
    public class CardView
    {
        public GameObject root;
        public Image background;
        public Image portrait;
        public Image roleIcon;
        public Text nicknameText;
        public Text nameText;
        public Text rarityText;
        public Text skill1Name;
        public Text skill1Desc;
        public Text skill2Name;
        public Text skill2Desc;
        public Image skill1Icon;
        public Image skill2Icon;
        public Button goldButton;
        public Button scrollButton;
        public Text goldLabel;
        public Text scrollLabel1;
        public GameObject orLabel;
        public Image scrollButtonImage;
    }

    List<MercenaryData> _offers = new List<MercenaryData>();
    bool _wired;
    string _lastBanterKey;
    float _nextIdleBanterUnscaled;

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
        MercHireSession.EnsureDailyOfferRefresh();
        var data = SaveSystem.Instance?.Data;
        bool dayRefresh = data != null && data.mercOfferDirty;
        if (dayRefresh || _offers == null || _offers.Count == 0)
        {
            RerollOffers();
            if (dayRefresh)
                MercHireSession.MarkRefreshed();
            else if (data != null)
                data.mercOfferDirty = false;
        }
        RefreshAll();
        WireOnce();
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
        MaybeToastAppearBanter(force: false, avoidLast: false);
        ScheduleNextIdleBanter();
    }

    void RerollOffers()
    {
        _offers = MercenaryOfferGenerator.GenerateOffers();
    }

    void RefreshAll()
    {
        int hired = MercHireSession.HiredCount();
        int max = MercenaryManager.Instance != null
            ? Mathf.Max(1, MercenaryManager.Instance.GetMaxMercSlots())
            : 1;
        if (remainText != null)
            remainText.text = $"本局雇佣：{hired}/{max}";

        if (autoRefreshText != null)
            autoRefreshText.text = "每日 0 点自动刷新";

        bool canRefresh = MercHireSession.CanManualRefresh(out int remainSec);
        if (refreshButton != null)
        {
            refreshButton.interactable = canRefresh;
            var img = refreshButton.targetGraphic as Image;
            if (img != null)
                img.color = canRefresh ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
        }
        if (refreshCostText != null)
            refreshCostText.text = canRefresh ? "可刷新" : $"冷却 {remainSec / 60}分{remainSec % 60}秒";

        for (int i = 0; i < cards.Length; i++)
            RefreshCard(i);
    }

    void RefreshCard(int i)
    {
        var c = cards[i];
        if (c == null || c.root == null) return;
        var offer = i < _offers.Count ? _offers[i] : null;
        bool on = offer != null;
        c.root.SetActive(on);
        if (!on)
        {
            ApplyHiredVisual(c, false);
            return;
        }

        var rarity = MercHireSession.OfferRarity(offer);
        string job = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetJobName(offer.mercId)
            : "";

        if (c.nicknameText != null)
            c.nicknameText.text = string.IsNullOrEmpty(offer.nickname) ? "—" : offer.nickname;
        if (c.nameText != null)
            c.nameText.text = string.IsNullOrEmpty(offer.displayName) ? offer.mercId : offer.displayName;
        if (c.rarityText != null)
            c.rarityText.text = RarityLabel(rarity);

        if (c.roleIcon != null)
        {
            var sp = MercHireSession.LoadJobIcon(job);
            c.roleIcon.sprite = sp;
            c.roleIcon.enabled = sp != null;
            c.roleIcon.preserveAspect = true;
            c.roleIcon.color = Color.white;
        }

        if (c.portrait != null)
        {
            string portraitKey = !string.IsNullOrEmpty(offer.hireId) ? offer.hireId : offer.mercId;
            var sp = MercPortraitSprites.GetStand(portraitKey)
                ?? (MercenaryManager.Instance != null ? MercenaryManager.Instance.GetIcon(offer.mercId) : null);
            c.portrait.sprite = sp;
            c.portrait.enabled = true;
            c.portrait.color = sp != null ? Color.white : new Color(0.35f, 0.32f, 0.38f, 1f);
            c.portrait.preserveAspect = true;
            if (sp != null)
                PortraitIdleMotion.EnsureOn(c.portrait.rectTransform, i * 0.31f);
            else
            {
                var idle = c.portrait.GetComponent<PortraitIdleMotion>();
                if (idle != null) idle.enabled = false;
            }
        }

        if (c.background != null)
        {
            var frame = MercHireSession.LoadRarityFrame(rarity);
            if (frame != null)
            {
                c.background.sprite = frame;
                c.background.color = Color.white;
            }
        }

        BindSkillTexts(c, offer);

        bool alreadyHired = MercHireSession.IsAlreadyHired(offer);
        ApplyHiredVisual(c, alreadyHired);

        int gold = MercHireSession.GoldCost(offer);
        bool canHireMore = MercHireSession.CanHireMore();
        bool hasGold = ResourceWallet.Get(SaveSystem.Instance?.Data, ResourceWallet.ResourceType.Gold) >= gold;
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        bool hasScroll = !isCommon && MercHireSession.HasScrollFor(rarity);

        // ConfirmButton：金币
        if (c.goldButton != null)
        {
            c.goldButton.gameObject.SetActive(true);
            bool goldOk = !alreadyHired && canHireMore && hasGold;
            c.goldButton.interactable = goldOk;
            SetButtonGray(c.goldButton, !goldOk);
        }
        if (c.goldLabel != null)
            c.goldLabel.text = gold.ToString();

        // ConfirmButton1：稀有/传奇卷；普通隐藏；无卷时显示金币价
        if (c.scrollButton != null)
        {
            if (isCommon)
            {
                c.scrollButton.gameObject.SetActive(false);
            }
            else
            {
                c.scrollButton.gameObject.SetActive(true);
                if (c.scrollButtonImage != null)
                {
                    var mat = MercHireSession.LoadScrollButtonMaterial(rarity);
                    c.scrollButtonImage.material = mat;
                }

                if (hasScroll)
                {
                    if (c.scrollLabel1 != null)
                        c.scrollLabel1.text = rarity == MercRosterDefs.MercRarity.Legendary ? "传奇卷×1" : "稀有卷×1";
                    bool scrollOk = !alreadyHired && canHireMore;
                    c.scrollButton.interactable = scrollOk;
                    SetButtonGray(c.scrollButton, !scrollOk);
                }
                else
                {
                    // 无卷：按钮位改显示金币价，可点则扣金币
                    if (c.scrollLabel1 != null)
                        c.scrollLabel1.text = gold.ToString();
                    bool goldOk = !alreadyHired && canHireMore && hasGold;
                    c.scrollButton.interactable = goldOk;
                    SetButtonGray(c.scrollButton, !goldOk);
                    if (c.scrollButtonImage != null)
                        c.scrollButtonImage.material = null;
                }
            }
        }

        if (c.orLabel != null)
            c.orLabel.SetActive(!isCommon);
    }

    static void BindSkillTexts(CardView c, MercenaryData offer)
    {
        string active = offer.skillId;
        string passive = offer.passiveSkillId;

        if (c.skill1Name != null)
            c.skill1Name.text = string.IsNullOrEmpty(active)
                ? MercenaryOfferGenerator.SkillDisplayName(passive)
                : MercenaryOfferGenerator.SkillDisplayName(active);
        if (c.skill1Desc != null)
        {
            string id = !string.IsNullOrEmpty(active) ? active : passive;
            c.skill1Desc.text = MercSkillTable.TryGet(id, out var row) ? row.RecruitDesc : "";
        }
        if (c.skill2Name != null)
            c.skill2Name.text = string.IsNullOrEmpty(passive)
                ? "—"
                : MercenaryOfferGenerator.SkillDisplayName(passive);
        if (c.skill2Desc != null)
        {
            c.skill2Desc.text = MercSkillTable.TryGet(passive, out var row) ? row.RecruitDesc : "";
        }
        if (c.skill1Icon != null)
        {
            string id = !string.IsNullOrEmpty(active) ? active : passive;
            var sp = MercSkillTable.LoadIcon(id);
            c.skill1Icon.sprite = sp;
            c.skill1Icon.enabled = sp != null;
            c.skill1Icon.preserveAspect = true;
        }
        if (c.skill2Icon != null)
        {
            var sp = MercSkillTable.LoadIcon(passive);
            c.skill2Icon.sprite = sp;
            c.skill2Icon.enabled = sp != null;
            c.skill2Icon.preserveAspect = true;
        }
    }

    static string RarityLabel(MercRosterDefs.MercRarity r)
    {
        if (r == MercRosterDefs.MercRarity.Legendary) return "传奇";
        if (r == MercRosterDefs.MercRarity.Rare) return "稀有";
        return "普通";
    }

    static void SetButtonGray(Button btn, bool gray)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null)
            img.color = gray ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
    }

    const float AppearBanterChance = 0.6f;
    const float IdleBanterMinSec = 8f;
    const float IdleBanterMaxSec = 14f;

    void Update()
    {
        if (root == null || !root.activeInHierarchy) return;
        if (_infoRoot != null && _infoRoot.activeSelf) return;
        if (Time.unscaledTime < _nextIdleBanterUnscaled) return;
        MaybeToastAppearBanter(force: true, avoidLast: true);
        ScheduleNextIdleBanter();
    }

    void ScheduleNextIdleBanter()
    {
        _nextIdleBanterUnscaled = Time.unscaledTime + UnityEngine.Random.Range(IdleBanterMinSec, IdleBanterMaxSec);
    }

    bool MaybeToastAppearBanter(bool force, bool avoidLast)
    {
        if (_offers == null || _offers.Count == 0) return false;
        if (!force && UnityEngine.Random.value >= AppearBanterChance) return false;

        var pool = new List<MercenaryData>();
        for (int i = 0; i < _offers.Count; i++)
        {
            if (_offers[i] == null) continue;
            if (MercHireSession.IsAlreadyHired(_offers[i])) continue;
            pool.Add(_offers[i]);
        }
        if (pool.Count == 0) return false;

        if (avoidLast && !string.IsNullOrEmpty(_lastBanterKey) && pool.Count > 1)
        {
            var others = new List<MercenaryData>();
            for (int i = 0; i < pool.Count; i++)
            {
                string k = OfferBanterKey(pool[i]);
                if (k != _lastBanterKey) others.Add(pool[i]);
            }
            if (others.Count > 0) pool = others;
        }

        var offer = pool[UnityEngine.Random.Range(0, pool.Count)];
        string key = OfferBanterKey(offer);
        bool last = MercHireSession.WasInLastRun(key)
            || (!string.IsNullOrEmpty(offer.mercId) && MercHireSession.WasInLastRun(offer.mercId));
        string line = MercRosterDefs.PickTavernAppearLine(key, last);
        if (string.IsNullOrEmpty(line)) return false;

        string name = !string.IsNullOrEmpty(offer.nickname) ? offer.nickname
            : (!string.IsNullOrEmpty(offer.displayName) ? offer.displayName : "佣兵");
        UIManager.Instance?.ShowToast($"{name}：「{line}」");
        _lastBanterKey = key;
        return true;
    }

    static string OfferBanterKey(MercenaryData offer)
    {
        if (offer == null) return "";
        return !string.IsNullOrEmpty(offer.hireId) ? offer.hireId : (offer.mercId ?? "");
    }

    static void ApplyHiredVisual(CardView c, bool hired)
    {
        if (c == null || c.root == null) return;
        var cg = c.root.GetComponent<CanvasGroup>();
        if (cg == null) cg = c.root.AddComponent<CanvasGroup>();
        cg.alpha = hired ? 0.45f : 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        var badge = EnsureHiredBadge(c.root.transform);
        if (badge != null)
            badge.gameObject.SetActive(hired);
    }

    static Text EnsureHiredBadge(Transform cardRoot)
    {
        if (cardRoot == null) return null;
        var existing = cardRoot.Find("HiredBadge");
        Text t = existing != null ? existing.GetComponent<Text>() : null;
        if (t != null) return t;

        t = CreateTxt(cardRoot, "HiredBadge", "佣兵已雇佣", 22, TextAnchor.MiddleCenter);
        t.color = new Color(1f, 0.86f, 0.42f, 1f);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(280f, 40f);
        t.gameObject.SetActive(false);
        return t;
    }

    void WireOnce()
    {
        if (_wired) return;
        _wired = true;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (refreshButton != null) refreshButton.onClick.AddListener(OnRefresh);
        if (infoButton != null) infoButton.onClick.AddListener(ShowInfo);

        for (int i = 0; i < cards.Length; i++)
        {
            int idx = i;
            var c = cards[i];
            if (c == null) continue;
            if (c.goldButton != null)
            {
                c.goldButton.onClick.RemoveAllListeners();
                c.goldButton.onClick.AddListener(() => OnHire(idx, preferScroll: false));
            }
            if (c.scrollButton != null)
            {
                c.scrollButton.onClick.RemoveAllListeners();
                c.scrollButton.onClick.AddListener(() => OnHire(idx, preferScroll: true));
            }
        }
    }

    void OnRefresh()
    {
        if (!MercHireSession.CanManualRefresh(out int remain))
        {
            UIManager.Instance?.ShowToast($"刷新冷却中（{remain / 60}分{remain % 60}秒）");
            return;
        }
        RerollOffers();
        MercHireSession.MarkRefreshed();
        RefreshAll();
        if (!MaybeToastAppearBanter(force: false, avoidLast: true))
            UIManager.Instance?.ShowToast("已刷新候选佣兵");
        ScheduleNextIdleBanter();
    }

    void OnHire(int index, bool preferScroll)
    {
        if (index < 0 || index >= _offers.Count || _offers[index] == null)
        {
            UIManager.Instance?.ShowToast("无效的佣兵");
            return;
        }
        var offer = _offers[index];
        if (MercHireSession.IsAlreadyHired(offer))
        {
            UIManager.Instance?.ShowToast("佣兵已雇佣");
            return;
        }
        if (!MercHireSession.CanHireMore())
        {
            UIManager.Instance?.ShowToast("本局雇佣已满，下本结束会离队");
            return;
        }

        var rarity = MercHireSession.OfferRarity(offer);
        int gold = MercHireSession.GoldCost(offer);
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        bool hasScroll = !isCommon && MercHireSession.HasScrollFor(rarity);

        if (preferScroll && hasScroll)
        {
            if (!MercHireSession.TrySpendScroll(rarity))
            {
                UIManager.Instance?.ShowToast("招募卷不足");
                return;
            }
        }
        else
        {
            // 金币：ConfirmButton，或 ConfirmButton1 无卷时
            if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, gold, save: false, notify: true))
            {
                UIManager.Instance?.ShowToast("金币不足");
                return;
            }
        }

        var picked = CloneOffer(offer);
        MercHireSession.AddHired(picked);
        string name = !string.IsNullOrEmpty(picked.displayName) ? picked.displayName
            : (!string.IsNullOrEmpty(picked.nickname) ? picked.nickname : "佣兵");
        UIManager.Instance?.ShowToast($"{name}加入队伍！");
        RefreshAll();
        ScheduleNextIdleBanter();
    }

    static MercenaryData CloneOffer(MercenaryData src)
    {
        return new MercenaryData
        {
            mercId = src.mercId,
            displayName = src.displayName,
            nickname = src.nickname,
            hireId = src.hireId,
            uid = System.Guid.NewGuid().ToString("N"),
            level = src.level,
            star = src.star,
            skillId = src.skillId,
            passiveSkillId = src.passiveSkillId,
            favorLevel = 1
        };
    }

    void ShowInfo()
    {
        if (_infoRoot != null)
        {
            _infoRoot.SetActive(true);
            return;
        }
        EnsureCanvas();
        _infoRoot = new GameObject("RecruitInfo", typeof(RectTransform));
        _infoRoot.transform.SetParent(transform, false);
        var rt = _infoRoot.GetComponent<RectTransform>();
        Stretch(rt);

        var dim = CreateImg(_infoRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => _infoRoot.SetActive(false));

        var panel = CreateImg(_infoRoot.transform, "Panel", new Color(0.12f, 0.1f, 0.14f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(560f, 720f);

        var title = CreateTxt(panel.transform, "Title", "招募说明", 28, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.92f, 0f, 0f, 400f, 40f);
        title.color = new Color(1f, 0.86f, 0.42f);

        string body =
            "· 每次展示 3 名候选佣兵，可分别用金币或招募卷雇佣。\n" +
            "· 普通佣兵仅金币雇佣；稀有可用稀有招募卷，传奇可用传奇招募卷。\n" +
            "· 有招募卷时优先用卷；没有卷时稀有/传奇条可改用金币价雇佣。\n" +
            "· 招募卷可在冒险日志里程商店兑换；关卡掉落后续开放。\n" +
            "· 雇佣的佣兵只跟随本次下本，回城或撤离后离队。\n" +
            "· 曾雇佣过的佣兵会记入冒险日志图鉴。\n" +
            "· 候选列表每日 0 点自动刷新；手动刷新有 30 分钟冷却。\n" +
            "· 出战人数受酒馆槽位限制。";
        var text = CreateTxt(panel.transform, "Body", body, 18, TextAnchor.UpperLeft);
        SetRect(text.rectTransform, 0.5f, 0.48f, 0f, 0f, 500f, 520f);
        text.alignment = TextAnchor.UpperLeft;

        var close = CreateBtn(panel.transform, "CloseInfo", "知道了", new Vector2(0f, -310f), new Vector2(220f, 56f));
        close.onClick.AddListener(() => _infoRoot.SetActive(false));
        GameFonts.ApplyToHierarchy(_infoRoot.transform);
    }

    void Close()
    {
        if (_infoRoot != null) _infoRoot.SetActive(false);
        if (root != null) root.SetActive(false);
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
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
        if (infoButton == null)
        {
            var infoTf = FindDeep(transform, "信息");
            if (infoTf != null) infoButton = infoTf.GetComponent<Button>() ?? infoTf.gameObject.AddComponent<Button>();
        }

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
            if (c.roleIcon == null) c.roleIcon = FindDeep(cardTf, "Role")?.GetComponent<Image>();
            // Name (1)=外号，Name=名字
            if (c.nicknameText == null)
            {
                var nick = FindDeep(cardTf, "Name (1)");
                if (nick != null) c.nicknameText = nick.GetComponent<Text>();
            }
            if (c.nameText == null) c.nameText = FindDeep(cardTf, "Name")?.GetComponent<Text>();
            if (c.rarityText == null) c.rarityText = FindDeep(cardTf, "Rarity")?.GetComponent<Text>();
            if (c.skill1Name == null) c.skill1Name = FindDeep(cardTf, "Skill1_name")?.GetComponent<Text>();
            if (c.skill2Name == null) c.skill2Name = FindDeep(cardTf, "Skill2_name")?.GetComponent<Text>();
            if (c.skill1Desc == null) c.skill1Desc = FindDeep(cardTf, "Skill1_内容")?.GetComponent<Text>();
            if (c.skill2Desc == null) c.skill2Desc = FindDeep(cardTf, "Skill2_内容")?.GetComponent<Text>();
            if (c.skill1Icon == null) c.skill1Icon = FindDeep(cardTf, "Skill1icon")?.GetComponent<Image>();
            if (c.skill2Icon == null) c.skill2Icon = FindDeep(cardTf, "Skill2icon")?.GetComponent<Image>();

            if (c.goldButton == null) c.goldButton = FindDeep(cardTf, "ConfirmButton")?.GetComponent<Button>();
            if (c.scrollButton == null) c.scrollButton = FindDeep(cardTf, "ConfirmButton1")?.GetComponent<Button>();
            if (c.scrollButtonImage == null && c.scrollButton != null)
                c.scrollButtonImage = c.scrollButton.GetComponent<Image>();
            if (c.goldLabel == null && c.goldButton != null)
            {
                var g = FindDeep(c.goldButton.transform, "Label")
                    ?? FindDeep(cardTf, "金币");
                if (g != null) c.goldLabel = g.GetComponent<Text>();
            }
            if (c.scrollLabel1 == null && c.scrollButton != null)
                c.scrollLabel1 = FindDeep(c.scrollButton.transform, "Label1")?.GetComponent<Text>();
            if (c.orLabel == null)
            {
                var orTf = FindDeep(cardTf, "或");
                if (orTf != null) c.orLabel = orTf.gameObject;
            }
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

    public void BuildFallbackHierarchy()
    {
        EnsureCanvas();
        for (int i = transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(transform.GetChild(i).gameObject);

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var dim = CreateImg(root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);

        var panel = CreateImg(root.transform, "Panel", new Color(0.09f, 0.08f, 0.10f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(700f, 1120f);

        titleText = CreateTxt(panel.transform, "Title", "招募佣兵", 36, TextAnchor.MiddleCenter);
        SetRect(titleText.rectTransform, 0.5f, 0.92f, 0f, 0f, 400f, 48f);
        closeButton = CreateBtn(panel.transform, "CloseButton", "X", new Vector2(300f, 500f), new Vector2(52f, 52f));
        infoButton = CreateBtn(panel.transform, "信息", "信息", new Vector2(-280f, 500f), new Vector2(80f, 48f));

        cards = new CardView[3];
        for (int i = 0; i < 3; i++)
        {
            var card = CreateImg(panel.transform, "Card" + i, new Color(0.18f, 0.16f, 0.2f, 1f));
            SetRect(card.rectTransform, 0.5f, 0.55f, (i - 1) * 220f, 0f, 200f, 560f);
            var nick = CreateTxt(card.transform, "Name (1)", "外号", 16, TextAnchor.MiddleCenter);
            SetRect(nick.rectTransform, 0.5f, 0.94f, 0f, 0f, 180f, 28f);
            var name = CreateTxt(card.transform, "Name", "名字", 16, TextAnchor.MiddleCenter);
            SetRect(name.rectTransform, 0.5f, 0.88f, 0f, 0f, 180f, 28f);
            var role = CreateImg(card.transform, "Role", Color.white);
            SetRect(role.rectTransform, 0.15f, 0.91f, 0f, 0f, 36f, 36f);
            var portrait = CreateImg(card.transform, "Portrait", new Color(0.3f, 0.3f, 0.32f));
            SetRect(portrait.rectTransform, 0.5f, 0.62f, 0f, 0f, 160f, 200f);
            var goldBtn = CreateBtn(card.transform, "ConfirmButton", "金币", new Vector2(0f, -200f), new Vector2(160f, 44f));
            var scrollBtn = CreateBtn(card.transform, "ConfirmButton1", "招募卷", new Vector2(0f, -250f), new Vector2(160f, 44f));
            var label1 = CreateTxt(scrollBtn.transform, "Label1", "0", 16, TextAnchor.MiddleCenter);
            Stretch(label1.rectTransform);
            cards[i] = new CardView
            {
                root = card.gameObject,
                background = card,
                portrait = portrait,
                roleIcon = role,
                nicknameText = nick,
                nameText = name,
                goldButton = goldBtn,
                scrollButton = scrollBtn,
                scrollButtonImage = scrollBtn.GetComponent<Image>(),
                scrollLabel1 = label1,
                goldLabel = goldBtn.GetComponentInChildren<Text>()
            };
        }

        refreshButton = CreateBtn(panel.transform, "RefreshButton", "刷新佣兵", new Vector2(0f, -480f), new Vector2(280f, 64f));
        refreshCostText = CreateTxt(refreshButton.transform, "RefreshCost", "", 14, TextAnchor.MiddleCenter);
        SetRect(refreshCostText.rectTransform, 0.5f, 0.2f, 0f, -8f, 200f, 20f);
        autoRefreshText = CreateTxt(panel.transform, "AutoRefresh", "每日 0 点自动刷新", 14, TextAnchor.MiddleCenter);
        SetRect(autoRefreshText.rectTransform, 0.5f, 0.06f, 0f, 0f, 360f, 24f);
        remainText = CreateTxt(panel.transform, "RemainText", "本局雇佣：0/1", 16, TextAnchor.MiddleCenter);
        SetRect(remainText.rectTransform, 0.5f, 0.12f, 0f, 0f, 300f, 28f);

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
        var txt = CreateTxt(img.transform, "Label", label, 20, TextAnchor.MiddleCenter);
        Stretch(txt.rectTransform);
        return btn;
    }
}
