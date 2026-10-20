using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 招募佣兵弹窗：绑定手做 Prefab（Card0/1/2）。
/// 每卡货币钮：ConfirmButton/ConfirmButton1 二选一显示（2026-10-19 主人拍板：普通=金币、
/// 稀有=普通卷、传奇=稀有卷；Card0 没摆卷钮节点时由金币钮节点顶位显示卷轴）。
/// 已拥有的候选不显示货币钮、灰显带「已拥有」标。
/// 雇佣 = 永久解锁进已拥有（2026-10-19 主人拍板）；图鉴 MarkMercSeen。
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

    // 左下角两种卷轴数量 Label（2026-10-18 主人要求绑数据）：招募卷/1=普通卷轴，招募卷/2=高级招募卷轴
    Text _scrollCommonCountText;
    Text _scrollLegendaryCountText;

    // 卡上卷钮的卷轴图快照（2026-10-19 修「普通卷图标用错」）：招募卷/1/icon=普通卷、招募卷/2/icon=高级卷
    Sprite _commonScrollIcon;
    Sprite _legendaryScrollIcon;

    // —— 货币钮外观快照（2026-10-19 主人报「按钮还不一样」）——
    // 金币钮(ConfirmButton)与卷钮(ConfirmButton1)是两套预制体节点：底图/颜色/Label颜色/尺寸都不同。
    // 绑定时各拍一份快照，填充时按货币套用对应外观 —— 三张卡的货币钮看起来完全一致。
    Sprite _goldBtnSprite; Color _goldBtnColor; Color _goldLabelColor; Vector2 _goldBtnSize;
    Sprite _scrollBtnSprite; Color _scrollBtnColor; Color _scrollLabelColor; Vector2 _scrollBtnSize;
    bool _btnLooksCaptured;

    // —— 佣兵台词气泡（2026-10-17 主人拍板：三选一里佣兵说话用气泡框，不再走顶部 Toast）——
    // 样式对齐看板娘 SpeechBubbleTalker：speech_bubble 底图 + 打字机 + SpeechBubbleFit 文字自适应。
    GameObject _mercBubbleRoot;
    Text _mercBubbleText;
    Coroutine _mercBubbleCo;
    Coroutine _refreshCountdownCo;

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
        public Image scrollButtonImage;
        public Image goldIcon;            // ConfirmButton/icon（主人在预制体里已摆好）
        public Image scrollIcon;          // ConfirmButton1/icon（主人在预制体里已摆好）
        [System.NonSerialized] public Sprite scrollIconDefault; // 主人摆的卷轴图快照：切图后要还原
        [System.NonSerialized] public Sprite goldIconDefault;   // 金币图快照：金币钮顶卷钮位切卷图后要还原
    }

    List<MercenaryData> _offers = new List<MercenaryData>();
    /// <summary>
    /// 稀有度 → 卡牌底图（【2026-10-19 主人拍板】预制体 Card0/1/2 是普通/稀有/传奇三种稀有度卡牌，
    /// 三套美术就用预制体自己的三张底图：首刷前按底图名归档缓存，RefreshCard 按佣兵稀有度取用）。
    /// </summary>
    readonly Dictionary<MercRosterDefs.MercRarity, Sprite> _cardFrameByRarity =
        new Dictionary<MercRosterDefs.MercRarity, Sprite>();
    bool _cardFramesMapped;
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
        // 主人预制体里的「对话框」默认带着占位字激活 → 打开先绑好并收起，等台词来了再亮
        EnsureMercBubble();
        HideMercBubble();
        FitPanelScale();
        TownSaveAlign.AlignAll();
        MercHireSession.EnsureDailyOfferRefresh();
        var data = SaveSystem.Instance?.Data;
        bool dayRefresh = data != null && data.mercOfferDirty;
        if (dayRefresh || _offers == null || _offers.Count == 0)
        {
            // 【2026-10-19 主人拍板】招募候选只认 0 点和手动刷新，App 重启也不许换批：
            // 非日切、且存档里有当前批次 → 原样恢复（不算刷新）；否则重掷并写回存档。
            if (!dayRefresh && data != null && TryLoadOffersFromSave(data))
            {
                data.mercOfferDirty = false;
            }
            else
            {
                RerollOffers();
                if (dayRefresh)
                    MercHireSession.MarkRefreshed();
                else if (data != null)
                {
                    data.mercOfferDirty = false;
                    SaveSystem.Instance.Save();   // 首掷批次立刻落盘，杀进程也不丢
                }
            }
        }
        RefreshAll();
        WireOnce();
        PlaceRefreshCostAboveButton();
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
        MaybeToastAppearBanter(force: false, avoidLast: false);
        ScheduleNextIdleBanter();
        StartRefreshCountdown();
    }

    void RerollOffers()
    {
        // 【2026-10-19 主人拍板】三名候选全部随机；卡面（普通/稀有/传奇三套卡牌美术）
        // 由 RefreshCard 按佣兵稀有度实时切换（见 _cardFrameByRarity）。
        _offers = MercenaryOfferGenerator.GenerateOffers();
        PersistOffers(SaveSystem.Instance?.Data);
    }

    /// <summary>
    /// 候选批次写回存档（2026-10-19 主人拍板：只认 0 点和手动刷新，重启不许换批）。
    /// 只写内存字段；落盘由调用方的 Save()/MarkRefreshed 负责。
    /// </summary>
    void PersistOffers(SaveData data)
    {
        if (data == null || _offers == null) return;
        data.mercOffers ??= new List<MercenaryData>();
        data.mercOffers.Clear();
        for (int i = 0; i < _offers.Count; i++)
            data.mercOffers.Add(_offers[i]);
    }

    /// <summary>
    /// 从存档恢复当前批次（重启后弹窗首开用）。批次缺卡或佣兵数据无效视为存档损坏，
    /// 返回 false 走重掷，绝不开天窗。
    /// </summary>
    bool TryLoadOffersFromSave(SaveData data)
    {
        var saved = data?.mercOffers;
        if (saved == null || saved.Count != MercenaryOfferGenerator.OfferCount) return false;
        for (int i = 0; i < saved.Count; i++)
        {
            if (saved[i] == null || string.IsNullOrEmpty(saved[i].mercId)) return false;
        }
        _offers = new List<MercenaryData>(saved);
        return true;
    }

    /// <summary>
    /// 首刷前把三张卡的预制体底图按稀有度归档（Card0/1/2 是三种稀有度卡牌，底图名带
    /// frame_common/rare/legendary 后缀）。必须在任何换框发生前调用，晚了底图已被覆盖。
    /// 识别不出不打扰：留空走 LoadRarityFrame 兜底。
    /// </summary>
    void MapCardFrames()
    {
        if (_cardFramesMapped) return;
        _cardFramesMapped = true;
        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            var img = c.root.GetComponent<Image>();
            var sp = img != null ? img.sprite : null;
            if (sp == null) continue;
            MercRosterDefs.MercRarity? r = null;
            if (sp.name.Contains("legendary")) r = MercRosterDefs.MercRarity.Legendary;
            else if (sp.name.Contains("rare")) r = MercRosterDefs.MercRarity.Rare;
            else if (sp.name.Contains("common")) r = MercRosterDefs.MercRarity.Common;
            if (r.HasValue && !_cardFrameByRarity.ContainsKey(r.Value))
                _cardFrameByRarity[r.Value] = sp;
        }
    }

    void RefreshAll()
    {
        MapCardFrames();   // 必须先归档三套卡面，再做任何换框
        int hired = MercHireSession.HiredCount();
        int max = MercenaryManager.Instance != null
            ? Mathf.Max(1, MercenaryManager.Instance.GetMaxMercSlots())
            : 1;
        if (remainText != null)
            remainText.text = $"本局雇佣：{hired}/{max}";

        if (autoRefreshText != null)
            autoRefreshText.text = "每日 0 点自动刷新";

        // 左下角两种卷轴数量绑存档（2026-10-18 主人拍板）：沿用静态文案的「X{数量}」格式
        var data = SaveSystem.Instance?.Data;
        if (_scrollCommonCountText != null)
            _scrollCommonCountText.text = "X" + (data != null ? data.mercScrollCommon : 0);
        if (_scrollLegendaryCountText != null)
            _scrollLegendaryCountText.text = "X" + (data != null ? data.mercScrollLegendary : 0);

        UpdateRefreshCostText();   // 含按钮灰显/点亮与顶上倒计时文字

        for (int i = 0; i < cards.Length; i++)
            RefreshCard(i);
    }

    /// <summary>
    /// 刷新按钮顶上的冷却提示（2026-10-17 主人拍板）：可刷新时不显示文字；
    /// 冷却中按钮灰显、按钮顶上显示倒计时（2026-10-18 主人要求改 HH:MM:SS 时分秒格式），
    /// 由 CoRefreshCountdown 每秒驱动，冷却走完这一秒顺带把按钮点亮（不必等下次 RefreshAll）。
    /// </summary>
    void UpdateRefreshCostText()
    {
        bool canRefresh = MercHireSession.CanManualRefresh(out int remainSec);
        if (refreshCostText != null)
            refreshCostText.text = canRefresh ? "" : $"{remainSec / 3600:D2}:{remainSec / 60 % 60:D2}:{remainSec % 60:D2}";
        if (refreshButton != null)
        {
            refreshButton.interactable = canRefresh;
            var img = refreshButton.targetGraphic as Image;
            if (img != null)
                img.color = canRefresh ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
        }
    }

    /// <summary>把 RefreshCost 文字挪到刷新按钮顶边上方（按钮内原位会跟按钮文字打架）。幂等。</summary>
    void PlaceRefreshCostAboveButton()
    {
        if (refreshCostText == null || refreshButton == null) return;
        var rt = refreshCostText.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 4f);
        rt.sizeDelta = new Vector2(200f, 30f);
    }

    void StartRefreshCountdown()
    {
        if (_refreshCountdownCo != null) StopCoroutine(_refreshCountdownCo);
        _refreshCountdownCo = StartCoroutine(CoRefreshCountdown());
    }

    void StopRefreshCountdown()
    {
        if (_refreshCountdownCo != null)
        {
            StopCoroutine(_refreshCountdownCo);
            _refreshCountdownCo = null;
        }
    }

    IEnumerator CoRefreshCountdown()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            UpdateRefreshCostText();
            yield return wait;
        }
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
            // 招募卡的 Role 角标 = 佣兵职业四分类徽标（Icons/职业icon），唯一入口 JobIconResolver.CombatBadge
            var sp = JobIconResolver.CombatBadge(job);
            c.roleIcon.sprite = sp;
            c.roleIcon.enabled = sp != null;
            c.roleIcon.color = Color.white;
            // 【2026-10-19 主人拍板】职业徽章素材原生 42×50，显示不许改变图片大小：
            // 节点尺寸跟随精灵原生宽高，且不开 preserveAspect（那会按节点框改显示尺寸）
            if (sp != null)
            {
                c.roleIcon.rectTransform.sizeDelta = new Vector2(sp.rect.width, sp.rect.height);
                c.roleIcon.preserveAspect = false;
            }
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
            // 【2026-10-19 主人拍板】Card0/1/2 是三种稀有度卡牌：卡面随佣兵稀有度切到对应档的
            // 卡牌美术 —— 优先用预制体自己的三张底图（MapCardFrames 归档），取不到再退 LoadRarityFrame
            Sprite frame = null;
            if (_cardFrameByRarity.TryGetValue(rarity, out var own) && own != null)
                frame = own;
            else
                frame = MercHireSession.LoadRarityFrame(rarity);
            if (frame != null)
            {
                c.background.sprite = frame;
                c.background.color = Color.white;
            }
        }

        BindSkillTexts(c, offer);

        // 【2026-10-19 主人拍板】已拥有的候选灰显 + 黄字「已拥有」标（不可重复购买）
        bool owned = MercHireSession.IsMercOwned(offer);
        ApplyHiredVisual(c, owned);

        RefreshGrowBadge(c, offer, rarity);

        // 【2026-10-19 主人拍板】已拥有的候选：灰显 + 「已拥有」标，货币钮整个不显示
        // （图标/价格数字都不显示），只保留灰卡本体。
        if (MercHireSession.IsMercOwned(offer))
        {
            if (c.goldButton != null) c.goldButton.gameObject.SetActive(false);
            if (c.scrollButton != null) c.scrollButton.gameObject.SetActive(false);
            return;
        }

        int gold = MercHireSession.GoldCost(offer);
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        bool isLegendary = rarity == MercRosterDefs.MercRarity.Legendary;

        // 【2026-10-19 主人拍板】一卡一钮，货币跟稀有度走：普通=金币；稀有=普通卷；传奇=稀有卷（高级卷）。
        // 两钮显隐每次显式设置（旧版漏关卷钮 → 同一张卡同时出现金币+卷轴两种购买方式，主人报过）。
        // 卡上没摆卷钮节点的（Card0）由金币钮节点顶位显示卷轴。
        bool useScrollNode = !isCommon && c.scrollButton != null;
        if (c.goldButton != null)
            c.goldButton.gameObject.SetActive(!useScrollNode);
        if (c.scrollButton != null)
            c.scrollButton.gameObject.SetActive(useScrollNode);
        if (useScrollNode)
        {
            FillCurrencyButton(c.scrollButtonImage, c.scrollIcon, c.scrollLabel1,
                c.scrollIconDefault, isCommon, isLegendary, gold);
        }
        else if (c.goldButton != null)
        {
            FillCurrencyButton(c.goldButton.GetComponent<Image>(), c.goldIcon, c.goldLabel,
                c.goldIconDefault, isCommon, isLegendary, gold);
        }
    }

    /// <summary>
    /// 货币钮统一填充：普通=金币钮「600金币」（金币图）；稀有=「普通卷×1」；传奇=「稀有卷×1」（卷图）。
    /// 外观按快照统一（金币一种样子、卷轴一种样子）：底图/颜色/尺寸/Label颜色都从快照套用，
    /// 字号统一 13（预制体金币钮 Label 16 / 卷钮 Label 11 不一致，主人报「按钮的字大小不一致」）。
    /// </summary>
    void FillCurrencyButton(Image btnImg, Image icon, Text label, Sprite iconDefault,
        bool isCommon, bool isLegendary, int gold)
    {
        if (btnImg != null)
        {
            var matRarity = isLegendary ? MercRosterDefs.MercRarity.Legendary : MercRosterDefs.MercRarity.Rare;
            btnImg.material = !isCommon ? MercHireSession.LoadScrollButtonMaterial(matRarity) : null;

            // 外观快照套用： whichever 节点顶位，都长成对应货币的标准样子
            var lookSp = isCommon ? _goldBtnSprite : _scrollBtnSprite;
            if (lookSp != null) btnImg.sprite = lookSp;
            var lookSize = isCommon ? _goldBtnSize : _scrollBtnSize;
            var rt = btnImg.transform as RectTransform;
            if (rt != null && lookSize.x > 0f) rt.sizeDelta = lookSize;
            if (isCommon)
            {
                if (_goldBtnSprite != null) btnImg.color = _goldBtnColor;
            }
            else if (_scrollBtnSprite != null) btnImg.color = _scrollBtnColor;
        }

        if (icon != null)
        {
            Sprite sp;
            if (isCommon)
                sp = iconDefault;   // 金币图（预制体摆的）
            else
            {
                var scrollSp = isLegendary ? _legendaryScrollIcon : _commonScrollIcon;
                sp = scrollSp != null ? scrollSp : iconDefault;
            }
            icon.sprite = sp;
            icon.enabled = sp != null;
        }
        if (label != null)
        {
            label.fontSize = 13;
            if (isCommon)
            {
                if (_goldBtnSprite != null) label.color = _goldLabelColor;
                label.text = gold + "金币";
            }
            else
            {
                if (_scrollBtnSprite != null) label.color = _scrollLabelColor;
                label.text = isLegendary ? "稀有卷×1" : "普通卷×1";
            }
        }
    }

    static void BindSkillTexts(CardView c, MercenaryData offer)
    {
        // 【2026-10-19 主人拍板】卡上按实有技能数展示：有两个显示两个、只有一个显示一个、
        // 全没有显示「无技能」。取代 2026-10-18「主动槽只显示主动」的固定槽位口径（B9 修的
        // 「两槽显示同一技能」由槽2留白天然规避，不会复发）。
        string active = offer.skillId;
        string passive = offer.passiveSkillId;
        bool hasActive = !string.IsNullOrEmpty(active);
        bool hasPassive = !string.IsNullOrEmpty(passive);

        // 槽1 = 第一个实有技能（主动优先）；一个都没有 → 「无技能」
        FillSkillSlot(c.skill1Name, c.skill1Desc, c.skill1Icon,
            hasActive ? active : hasPassive ? passive : null, "无技能");
        // 槽2 = 第二个实有技能；只有一个技能时槽2整槽留白
        FillSkillSlot(c.skill2Name, c.skill2Desc, c.skill2Icon,
            hasActive && hasPassive ? passive : null, "");
    }

    /// <summary>单个技能槽填充：skillId 为空显示 emptyLabel、描述与图标清空（留白，不占位报错）。</summary>
    static void FillSkillSlot(Text name, Text desc, Image icon, string skillId, string emptyLabel)
    {
        bool has = !string.IsNullOrEmpty(skillId);
        if (name != null)
            name.text = has ? MercenaryOfferGenerator.SkillDisplayName(skillId) : emptyLabel;
        if (desc != null)
            desc.text = has && MercSkillTable.TryGet(skillId, out var row) ? row.RecruitDesc : "";
        if (icon != null)
        {
            var sp = has ? MercSkillTable.LoadIcon(skillId) : null;
            icon.sprite = sp;
            icon.enabled = sp != null;
            icon.preserveAspect = true;
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

        var pool = new List<int>();
        for (int i = 0; i < _offers.Count; i++)
        {
            if (_offers[i] == null) continue;
            if (MercHireSession.IsAlreadyHired(_offers[i])) continue;
            pool.Add(i);
        }
        if (pool.Count == 0) return false;

        if (avoidLast && !string.IsNullOrEmpty(_lastBanterKey) && pool.Count > 1)
        {
            var others = new List<int>();
            for (int i = 0; i < pool.Count; i++)
            {
                string k = OfferBanterKey(_offers[pool[i]]);
                if (k != _lastBanterKey) others.Add(pool[i]);
            }
            if (others.Count > 0) pool = others;
        }

        int pick = pool[UnityEngine.Random.Range(0, pool.Count)];
        var offer = _offers[pick];
        string key = OfferBanterKey(offer);
        string line = MercLineTable.Pick(key, MercLineTable.Scene.TavernRefresh);
        if (string.IsNullOrEmpty(line)) return false;

        // 【2026-10-17 主人拍板】佣兵说话改走该佣兵卡上方的气泡框，不再占顶部 Toast。
        ShowMercBubble(pick, line);
        _lastBanterKey = key;
        return true;
    }

    static string OfferBanterKey(MercenaryData offer)
    {
        if (offer == null) return "";
        return !string.IsNullOrEmpty(offer.hireId) ? offer.hireId : (offer.mercId ?? "");
    }

    // ==========================================================================
    // 佣兵台词气泡：【2026-10-18 主人拍板「对话框按我预制体的位置走」】台词填进主人在
    // 招募预制体里摆好的「对话框」节点（Root/Frame 下），位置 / 底图 / 字号全以预制体为准，
    // 代码只填字 + 打字机；超长句仍走 SpeechBubbleFit 等比放大（每次先复位基准，不累积）。
    // 旧的「运行时在佣兵卡内自建 MercBubble、写死卡内坐标」整段废弃删除。
    // ==========================================================================

    /// <summary>在弹窗的「对话框」里让佣兵说 line；上一条气泡（若有）立即打断。
    /// cardIndex 仅为兼容旧调用保留，位置不再跟说话佣兵的卡走。</summary>
    void ShowMercBubble(int cardIndex, string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        EnsureMercBubble();
        if (_mercBubbleRoot == null || _mercBubbleText == null) return;
        if (_mercBubbleCo != null) StopCoroutine(_mercBubbleCo);
        _mercBubbleCo = StartCoroutine(CoMercBubble(cardIndex, line));
    }

    bool _mercBubbleBindTried;
    Vector2 _mercBubbleBaseSize;
    // 【2026-10-18 主人口径】对话框 x 跟说话佣兵的卡走：卡0=-100 / 卡1=100 / 卡2=250（Frame 本地坐标）；
    // 纵向保持预制体的 y 不动。长句放大后按 Frame 边界钳制（ApplyBubbleX），绝不出屏外。
    static readonly float[] MercBubbleXByCard = { -100f, 100f, 250f };

    /// <summary>绑主人预制体里的「对话框」（框图 + 子 Text）；找不到报错，不运行时造节点。只绑一次。</summary>
    void EnsureMercBubble()
    {
        if (_mercBubbleRoot != null || _mercBubbleBindTried) return;
        _mercBubbleBindTried = true;
        var node = FindDeep(transform, "对话框");
        if (node == null)
        {
            Debug.LogError("[MercenaryRecruit] 预制体里找不到「对话框」节点，佣兵台词不显示");
            return;
        }
        var rt = node as RectTransform;
        if (rt == null || rt.sizeDelta.x < 8f || rt.sizeDelta.y < 8f)
        {
            Debug.LogError("[MercenaryRecruit] 「对话框」不是合法 RectTransform（或尺寸异常），佣兵台词不显示");
            return;
        }
        _mercBubbleRoot = node.gameObject;
        _mercBubbleBaseSize = rt.sizeDelta;   // SpeechBubbleFit 每次先复位到此基准，长句放大不累积
        _mercBubbleText = node.Find("Text")?.GetComponent<Text>();
        if (_mercBubbleText == null)
            Debug.LogError("[MercenaryRecruit] 「对话框」下没有 Text 子节点，台词没地方填");
        _mercBubbleRoot.SetActive(false);
    }

    IEnumerator CoMercBubble(int cardIndex, string line)
    {
        _mercBubbleRoot.SetActive(true);
        var rt = _mercBubbleRoot.transform as RectTransform;
        string display = SpeechBubbleFit.Apply(rt, _mercBubbleText, line, _mercBubbleBaseSize);
        // SpeechBubbleFit 结尾会把根图强制成 Simple；主人的对话框图是 Sliced，恢复回去防九宫格拉扁
        var rootImg = _mercBubbleRoot.GetComponent<Image>();
        if (rootImg != null) { rootImg.type = Image.Type.Sliced; rootImg.preserveAspect = false; }
        ApplyBubbleX(rt, cardIndex);
        _mercBubbleText.text = "";
        float delay = 1f / Mathf.Max(1f, 18f);
        for (int i = 1; i <= display.Length; i++)
        {
            _mercBubbleText.text = display.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }
        _mercBubbleText.text = display;
        yield return new WaitForSeconds(2.8f);
        HideMercBubble();
        _mercBubbleCo = null;
    }

    /// <summary>
    /// 对话框横向定位：x 取 <see cref="MercBubbleXByCard"/>（说话佣兵所在卡），纵向保持预制体 y 不动。
    /// 长句被 SpeechBubbleFit 放大后，按父级 Frame 宽度左右钳制（各留 6px 余量），保证不出屏外。
    /// </summary>
    void ApplyBubbleX(RectTransform rt, int cardIndex)
    {
        if (rt == null) return;
        int i = Mathf.Clamp(cardIndex, 0, MercBubbleXByCard.Length - 1);
        float frameHalf = 350f;   // 主人 Frame 700 宽 → 本地坐标 ±350；读不到布局时兜底此值
        if (rt.parent is RectTransform p && p.rect.width > 100f)
            frameHalf = p.rect.width * 0.5f;
        float halfW = rt.sizeDelta.x * 0.5f;
        float minX = -frameHalf + halfW + 6f;
        float maxX = frameHalf - halfW - 6f;
        float x = Mathf.Clamp(MercBubbleXByCard[i], minX, maxX);
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }

    void HideMercBubble()
    {
        if (_mercBubbleCo != null) { StopCoroutine(_mercBubbleCo); _mercBubbleCo = null; }
        if (_mercBubbleRoot != null) _mercBubbleRoot.SetActive(false);
        if (_mercBubbleText != null) _mercBubbleText.text = "";
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
        {
            // 【2026-10-19 主人拍板】显示时压到卡内最上层，防被后创建的构件（MG_Badge 等）盖住
            if (hired) badge.transform.SetAsLastSibling();
            badge.gameObject.SetActive(hired);
        }
    }

    static Text EnsureHiredBadge(Transform cardRoot)
    {
        if (cardRoot == null) return null;
        var existing = cardRoot.Find("HiredBadge");
        Text t = existing != null ? existing.GetComponent<Text>() : null;
        if (t != null) return t;

        // 【2026-10-19 主人拍板】「已拥有」加大加粗、摆在卡面正中（原来是 22 号细字缩在中心偏下，
        // 被下方技能区/按钮区压住看不清）
        t = CreateTxt(cardRoot, "HiredBadge", "已拥有", 34, TextAnchor.MiddleCenter);
        t.color = new Color(1f, 0.86f, 0.42f, 1f);
        t.fontStyle = FontStyle.Bold;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(320f, 60f);
        t.gameObject.SetActive(false);
        return t;
    }

    /// <summary>补挂「佣兵养成构件」：职业徽记角标 + 本命碎片数量（懒创建，reroll 时只刷新不重建）。</summary>
    static void RefreshGrowBadge(CardView c, MercenaryData offer, MercRosterDefs.MercRarity rarity)
    {
        if (c == null || c.root == null || offer == null) return;
        var cardRoot = c.root.transform;

        // —— 职业徽记角标（右上角）——
        Image badge = cardRoot.Find("MG_Badge")?.GetComponent<Image>();
        string key = !string.IsNullOrEmpty(offer.hireId) ? offer.hireId : offer.mercId;
        if (badge == null)
        {
            // 2026-09-27：末位那个 40f 只是新建节点的初始占位，真实尺寸由
            // CreateBadgeForMerc 按**图片自身尺寸**（职业图 44×50）还原，代码不再压成正方形。
            badge = MercGrowUI.CreateBadgeForMerc(cardRoot, "MG_Badge", key, rarity, 40f);
            if (badge != null)   // 兜空：构件返回 null 或素材缺失都不加空节点、不改排版
            {
                var rt = badge.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-6f, -6f);
            }
        }
        else
        {
            MercGrowUI.RefreshBadge(badge, MercRosterDefs.GetJobName(key), rarity);
        }

        // —— 本命碎片数量（角标下方，0 时不显示）——
        Text frag = cardRoot.Find("MG_Fragment")?.GetComponent<Text>();
        if (frag == null)
        {
            frag = CreateTxt(cardRoot, "MG_Fragment", "", 16, TextAnchor.MiddleRight);
            var rt = frag.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-6f, -50f);
            rt.sizeDelta = new Vector2(160f, 24f);
            frag.color = new Color(1f, 0.86f, 0.42f, 1f);
        }
        if (frag != null)
        {
            int fragCount = MercGrowInventory.FragmentCount(offer.hireId);
            if (fragCount <= 0)
                frag.gameObject.SetActive(false);
            else
            {
                frag.gameObject.SetActive(true);
                frag.text = "碎片 x" + fragCount;
            }
        }
    }

    void WireOnce()
    {
        if (_wired) return;
        _wired = true;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (refreshButton != null) refreshButton.onClick.AddListener(OnRefresh);
        if (infoButton != null) infoButton.onClick.AddListener(ShowRecruitOdds);   // 2026-10-17：info 改弹概率公示（口径与实际随机一致）；旧规则面板 ShowInfo 保留未删

        for (int i = 0; i < cards.Length; i++)
        {
            int idx = i;
            var c = cards[i];
            if (c == null || c.root == null) continue;
            // 【2026-10-19 主人拍板】卡上按钮不隐藏，点击必给反馈：
            // 金币钮/卷钮 → 买得起弹「佣兵信息」确认弹窗（购买/取消），不够弹字提示；
            // 点卡其余区域 → 直接看「佣兵信息」弹窗（购买在弹窗里走同一确认口径）。
            if (c.goldButton != null)
            {
                c.goldButton.onClick.RemoveAllListeners();
                c.goldButton.onClick.AddListener(() => OnBuyButtonClicked(idx));
            }
            if (c.scrollButton != null)
            {
                c.scrollButton.onClick.RemoveAllListeners();
                c.scrollButton.onClick.AddListener(() => OnBuyButtonClicked(idx));
            }
            var rootBtn = c.root.GetComponent<Button>();
            if (rootBtn == null) rootBtn = c.root.AddComponent<Button>();
            rootBtn.onClick.RemoveAllListeners();
            rootBtn.onClick.AddListener(() => OnCardClicked(idx));
        }
    }

    /// <summary>
    /// 卡上购买钮点击（2026-10-19 主人拍板）：先按货币口径查够不够 ——
    /// 普通=金币（不够弹「金币不足」）；稀有=普通卷、传奇=稀有卷（不够弹「道具不足」）；
    /// 够才弹「佣兵信息」确认弹窗（是否购买）。购买 = 永久解锁进「已拥有」；
    /// 已拥有的候选不可重复购买（点击只提示）。
    /// </summary>
    void OnBuyButtonClicked(int index)
    {
        if (index < 0 || index >= _offers.Count || _offers[index] == null)
        {
            UIManager.Instance?.ShowToast("无效的佣兵");
            return;
        }
        var offer = _offers[index];
        if (MercHireSession.IsMercOwned(offer))
        {
            UIManager.Instance?.ShowToast("该佣兵已拥有");
            return;
        }
        var rarity = MercHireSession.OfferRarity(offer);
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        if (isCommon)
        {
            if (!HasEnoughGold(offer))
            {
                UIManager.Instance?.ShowToast("金币不足");
                return;
            }
        }
        else if (!MercHireSession.HasScrollFor(rarity))
        {
            UIManager.Instance?.ShowToast("道具不足");
            return;
        }

        // 买得起 → 弹确认（佣兵信息 + 购买/取消，购买钮带货币价格）
        MercObtainPopupUI.ShowForRecruit(offer, () => TryHireOffer(offer, index));
    }

    static bool HasEnoughGold(MercenaryData offer)
    {
        return ResourceWallet.Get(SaveSystem.Instance?.Data, ResourceWallet.ResourceType.Gold)
            >= MercHireSession.GoldCost(offer);
    }

    /// <summary>点招募卡：弹佣兵信息弹窗（MercObtainPopupUI），弹窗内「购买」回调 TryHireOffer。</summary>
    void OnCardClicked(int index)
    {
        if (index < 0 || index >= _offers.Count || _offers[index] == null) return;
        var offer = _offers[index];
        MercObtainPopupUI.ShowForRecruit(offer, () => TryHireOffer(offer, index));
    }

    void OnRefresh()
    {
        if (!MercHireSession.CanManualRefresh(out int remain))
        {
            // 冷却提示与按钮顶上倒计时统一时分秒口径（2026-10-18 主人拍板）
            UIManager.Instance?.ShowToast($"刷新冷却中（{remain / 3600}:{remain / 60 % 60:D2}:{remain % 60:D2}）", force: true);
            return;
        }
        RerollOffers();
        MercHireSession.MarkRefreshed();
        RefreshAll();
        if (!MaybeToastAppearBanter(force: false, avoidLast: true))
            UIManager.Instance?.ShowToast("已刷新候选佣兵");
        ScheduleNextIdleBanter();
    }

    /// <summary>
    /// 购买一名招募候选（2026-10-19 起由「佣兵信息」弹窗的购买钮触发，返回是否成交）。
    /// 【2026-10-19 主人拍板】购买 = 永久解锁进「已拥有」——与雇佣队列、战斗内抽奖队列、
    /// 临时出战名单都无关，不再走 MercHireSession.AddHired。
    /// 支付口径：普通=金币；稀有=普通卷；传奇=稀有卷（高级卷）。
    /// 失败时 Toast 提示并返回 false（弹窗不关，玩家可改点取消）。
    /// </summary>
    bool TryHireOffer(MercenaryData offer, int index)
    {
        if (offer == null)
        {
            UIManager.Instance?.ShowToast("无效的佣兵");
            return false;
        }
        if (MercHireSession.IsMercOwned(offer))
        {
            UIManager.Instance?.ShowToast("该佣兵已拥有");
            return false;
        }

        var rarity = MercHireSession.OfferRarity(offer);
        bool isCommon = rarity == MercRosterDefs.MercRarity.Common;
        if (isCommon)
        {
            int gold = MercHireSession.GoldCost(offer);
            if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, gold, save: false, notify: true))
            {
                UIManager.Instance?.ShowToast("金币不足");
                return false;
            }
        }
        else if (!MercHireSession.TrySpendScroll(rarity))
        {
            UIManager.Instance?.ShowToast("道具不足");
            return false;
        }

        // 永久解锁（进「已拥有」，下次开酒馆自动按既有口径补进三选一池资格）
        var data = SaveSystem.Instance?.Data;
        string hid = offer.hireId;
        if (string.IsNullOrEmpty(hid) && MercRosterDefs.TryGetByAssetId(offer.mercId, out var def0))
            hid = def0.HireId;
        if (data == null || string.IsNullOrEmpty(hid) || !data.UnlockMerc(hid))
        {
            Debug.LogError("[MercRecruit] 解锁佣兵失败：hireId=" + hid
                + "（data=" + (data != null) + "，钱已扣，需主人确认是否补发）");
            UIManager.Instance?.ShowToast("购买失败");
            return false;
        }
        AdventureCodex.MarkMercSeen(hid);
        SaveSystem.Instance.Save();

        // 【2026-10-20 主人拍板】酒馆三选一购买完成 = 已拥有 → 弹「获得佣兵」全屏演出（同战斗三选一口径）。
        // 酒馆能买成功的必是首次获得（已拥有灰显不可买）；演出层 11500 盖过 Toast，点任意处关闭回到招募界面。
        MercGainCinematicUI.Show(hid, null);

        string name = !string.IsNullOrEmpty(offer.displayName) ? offer.displayName
            : (!string.IsNullOrEmpty(offer.nickname) ? offer.nickname
            : (MercRosterDefs.TryGetByHireId(hid, out var def1) ? def1.Name : "佣兵"));
        // 【2026-10-19 主人拍板】购买报喜文案 = 恭喜获得XXX实力大增；佣兵台词仍走该卡上方气泡框。
        // force:true —— 购买结果不允许被闲聊气泡压制（主人报「购买后没有提示获得信息」的根因）
        UIManager.Instance?.ShowToast($"恭喜获得{name}实力大增", force: true);
        ShowMercBubble(index, MercLineTable.Pick(OfferBanterKey(offer), MercLineTable.Scene.HireSuccess));
        RefreshAll();
        ScheduleNextIdleBanter();
        return true;
    }

    /// <summary>
    /// info 按钮弹出招募概率公示（2026-10-17 主人拍板：info 节点 → 概率弹窗）。
    /// 口径与 MercenaryOfferGenerator 实际掷点完全一致（常量直引，不会公示假数）：
    /// 佣兵 100%；品质 普通 133/200 = 66.5% / 稀有 66/200 = 33% / 传奇 1/200 = 0.5%（2026-10-19 主人拍板）。
    /// 卡面美术随佣兵稀有度实时切换（Card0/1/2 三套稀有度卡牌）。
    /// </summary>
    void ShowRecruitOdds()
    {
        var rows = new List<DraftPool.OddsRow>
        {
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_TYPE,
                Label = "佣兵",
                Percent = 100f,
                Color = RarityPalette.Common,
            },
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_QUALITY,
                Label = "传奇",
                Percent = MercenaryOfferGenerator.LegendaryWeight * 100f / MercenaryOfferGenerator.RollTotal,
                Color = RarityPalette.Legendary,
            },
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_QUALITY,
                Label = "稀有",
                Percent = MercenaryOfferGenerator.RareWeight * 100f / MercenaryOfferGenerator.RollTotal,
                Color = RarityPalette.Rare,
            },
            new DraftPool.OddsRow
            {
                Section = DraftPool.SEC_QUALITY,
                Label = "普通",
                Percent = MercenaryOfferGenerator.CommonWeight * 100f / MercenaryOfferGenerator.RollTotal,
                Color = RarityPalette.Common,
            },
        };
        DraftOddsPopupUI.ToggleWith(rows);
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
            "· 每次展示 3 名候选佣兵，货币跟稀有度走。\n" +
            "· 普通佣兵用金币雇佣；稀有佣兵用普通卷轴×1；传奇佣兵用稀有卷轴×1。\n" +
            "· 金币/卷轴不足时点购买会提示。\n" +
            "· 招募卷可在冒险日志里程商店兑换；关卡掉落后续开放。\n" +
            "· 购买 = 永久解锁该佣兵进「已拥有」。\n" +
            "· 候选列表每日 0 点自动刷新；手动刷新有 3 小时冷却。\n";
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
        HideMercBubble();
        StopRefreshCountdown();
        if (root != null) root.SetActive(false);
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
    }

    /// <summary>
    /// 【2026-10-17 主人拍板】按预制体布局整体缩放：不逐节点改坐标，把 Panel 连同全部子节点
    /// 等比缩到当前画布的可用区内（顶部资源条 + 底部导航各留出空间）。
    /// 预制体 Panel 714×1133 在瘦屏（match=0 时画布高可达 1364+）几乎占满全屏并压住顶栏/底栏，
    /// 只缩 Panel 的 localScale，预制体内部布局一个像素不动。
    /// </summary>
    void FitPanelScale()
    {
        var canvasRt = transform as RectTransform;
        if (canvasRt == null || canvasRt.rect.width <= 0f || canvasRt.rect.height <= 0f) return;
        var panelTf = root != null ? root.transform.Find("Panel") : null;
        if (panelTf == null) panelTf = transform.Find("Panel");
        var prt = panelTf as RectTransform;
        if (prt == null) return;

        // 预留：顶部资源条 ~120 + 底部五入口 ~150 + 余量 30 = 300；左右各留 20。
        float availW = canvasRt.rect.width - 40f;
        float availH = canvasRt.rect.height - 300f;
        float w = prt.sizeDelta.x;
        float h = prt.sizeDelta.y;
        if (w <= 0f || h <= 0f) return;
        float s = Mathf.Min(1f, Mathf.Min(availW / w, availH / h));
        prt.localScale = new Vector3(s, s, 1f);
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
        if (_scrollCommonCountText == null || _scrollLegendaryCountText == null)
        {
            // 左下角卷轴数量：预制体「招募卷」下「1」=普通卷、「2」=高级卷，各自子 Label 静态文案「X10000」
            var scrollRow = FindDeep(transform, "招募卷");
            if (scrollRow != null)
            {
                if (_scrollCommonCountText == null)
                    _scrollCommonCountText = scrollRow.Find("1/Label")?.GetComponent<Text>();
                if (_scrollLegendaryCountText == null)
                    _scrollLegendaryCountText = scrollRow.Find("2/Label")?.GetComponent<Text>();
            }
        }
        // 卡上卷钮图标源：普通卷/高级卷各取主人摆的展示位（2026-10-19 修「普通卷图标用错」）
        if (_commonScrollIcon == null)
            _commonScrollIcon = FindDeep(transform, "招募卷")?.Find("1/icon")?.GetComponent<Image>()?.sprite;
        if (_legendaryScrollIcon == null)
            _legendaryScrollIcon = FindDeep(transform, "招募卷")?.Find("2/icon")?.GetComponent<Image>()?.sprite;
        if (infoButton == null)
        {
            // 【2026-10-18 修·主人报「info 按钮不弹概率弹窗」】预制体节点名是「info」不是「信息」，
            // 以前只按「信息」找永远绑不上。两个名字都兼容（代码兜底壳建的节点叫「信息」）。
            var infoTf = FindDeep(transform, "info") ?? FindDeep(transform, "信息");
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
                var g = FindDeep(c.goldButton.transform, "Label");
                if (g != null) c.goldLabel = g.GetComponent<Text>();
            }
            // 卷轴按钮文字：节点名就是 Label（与金币按钮同名，各自按钮下查找）
            if (c.scrollLabel1 == null && c.scrollButton != null)
                c.scrollLabel1 = FindDeep(c.scrollButton.transform, "Label")?.GetComponent<Text>();
            // 按钮内 icon 槽（主人在预制体里已摆好图，代码只按状态切换、不重设默认图）
            if (c.goldIcon == null && c.goldButton != null)
                c.goldIcon = FindDeep(c.goldButton.transform, "icon")?.GetComponent<Image>();
            if (c.scrollIcon == null && c.scrollButton != null)
                c.scrollIcon = FindDeep(c.scrollButton.transform, "icon")?.GetComponent<Image>();
            if (c.scrollIcon != null && c.scrollIconDefault == null)
                c.scrollIconDefault = c.scrollIcon.sprite;
            // 金币钮图标快照（金币钮顶卷钮位切卷图后要还原金币图，2026-10-19）
            if (c.goldIcon != null && c.goldIconDefault == null)
                c.goldIconDefault = c.goldIcon.sprite;
        }
        CaptureButtonLooks();
    }

    /// <summary>
    /// 货币钮外观快照（绑定后、任何填充前调用）：金币钮(ConfirmButton)与卷钮(ConfirmButton1)
    /// 是两套预制体节点，底图/颜色/Label颜色/尺寸都不同 —— 各拍一份，填充时按货币套用，
    /// 保证「金币一种样子、卷轴一种样子」，三张卡看起来完全一致。
    /// </summary>
    void CaptureButtonLooks()
    {
        if (_btnLooksCaptured) return;
        _btnLooksCaptured = true;
        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (c.goldButton != null && _goldBtnSprite == null)
            {
                var img = c.goldButton.GetComponent<Image>();
                if (img != null)
                {
                    _goldBtnSprite = img.sprite;
                    _goldBtnColor = img.color;
                    var rt = c.goldButton.transform as RectTransform;
                    if (rt != null) _goldBtnSize = rt.sizeDelta;
                }
                if (c.goldLabel != null) _goldLabelColor = c.goldLabel.color;
            }
            if (c.scrollButton != null && _scrollBtnSprite == null)
            {
                var img = c.scrollButtonImage != null ? c.scrollButtonImage : c.scrollButton.GetComponent<Image>();
                if (img != null)
                {
                    _scrollBtnSprite = img.sprite;
                    _scrollBtnColor = img.color;
                    var rt = c.scrollButton.transform as RectTransform;
                    if (rt != null) _scrollBtnSize = rt.sizeDelta;
                }
                if (c.scrollLabel1 != null) _scrollLabelColor = c.scrollLabel1.color;
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
