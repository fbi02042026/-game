using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险者公会大厅（主界面内容）：看板娘眨眼、金币/体力、场景热点。
/// 底部 5 入口由 <see cref="MainBottomNav"/> 统一处理，可供其他界面复用。
/// 流程：Boot → Town(本界面) → 点「冒险」进 Battle。
/// </summary>
public class GuildHallUI : MonoBehaviour
{
    public static GuildHallUI Instance { get; private set; }

    [Header("顶部")]
    public Text goldText;
    public Button goldPlusButton;
    public Text staminaText;
    public Button staminaPlusButton;
    [Tooltip("体力未满时显示回满倒计时，可空（自动在体力Panel下创建）")]
    public Text staminaRegenText;

    [Header("左侧")]
    public Button mailButton;
    public Button noticeButton;
    public Button activityButton;

    [Header("右侧")]
    public Button rankButton;
    public Button shopButton;
    public Button settingsButton;

    [Header("场景热点")]
    public Button noticeBoardButton;
    public Button licenseHallButton;
    public Button armoryButton;
    public Button receptionistButton;

    [Header("底部导航（可选，优先用 MainBottomNav 组件）")]
    public MainBottomNav bottomNav;
    public Button navGuildButton;
    public Button navCharacterButton;
    public Button navAdventureButton;
    public Button navTavernButton;
    public Button navLogButton;

    [Header("看板娘")]
    public RectTransform mascotEyes;

    [Header("眨眼")]
    public float blinkMinInterval = 2.2f;
    public float blinkMaxInterval = 5.5f;
    public float blinkCloseDuration = 0.05f;
    public float blinkOpenDuration = 0.08f;
    [Range(0.01f, 0.3f)] public float blinkClosedScaleY = 0.08f;

    Coroutine _blinkCo;
    Vector3 _eyesBaseScale = Vector3.one;
    CanvasGroup _introCover;

    /// <summary>首次引导未完成前隐藏大厅，避免片头/剧情前闪一下主界面。</summary>
    public static bool ShouldHideTownForIntro => !StoryProgress.TutorialIntroDone;

    /// <summary>
    /// 热点（咨询台 / 对话气泡）跟随背景底图等比放大的总开关。
    /// 主人反馈「主界面背景上的咨询台底框不对、跟美术对不上」：Background 被 Envelope 整体等比放大后，
    /// 预制体里「锚点居中 + 固定绝对偏移」的热点不会跟着放大，于是热区框还留在原地、美术却往外移了。
    /// 置 false 即完整还原到改动前的行为（标准屏与瘦屏都不做任何缩放）。
    /// </summary>
    public static bool EnableHotspotFollowBg = true;

    void Awake()
    {
        Instance = this;
        UICanvasSetup.ApplyOn(gameObject, UICanvasSetup.ResolveUiCamera());
        // 城镇大厅底图（Background）运行时 envelope 铺满：覆盖更瘦屏（match width）
        // canvas 逻辑高变高后的上下空区。原 UiPrefabRectGuard 会把底图还原为 prefab
        // 固定尺寸导致长屏露空，故移除守卫、改为 envelope 拉伸。
        ApplyBackgroundEnvelope();
        GameFonts.ApplyToHierarchy(transform);
        AutoBindMissingRefs();
        ApplyGuildNameTexts();
        EnsureBottomNav();
        TownHubController.EnsureOn(gameObject);
        WireHallClicks();
        EnsureSpeechBubbleTalker();
        BindRedDots();
        RedDot.RefreshCommon();
        RefreshAllHud();
        EnsureIntroCover();
        if (ShouldHideTownForIntro)
        {
            TownIntroVeil.EnsureShown();
            SetTownChromeVisible(false);
        }
        StartBlink();
        StartCoroutine(StaminaHudLoop());
    }

    void Start()
    {
        // 首次进城镇要接片头：不要在 Loading 尚未关掉时就把黑幕拆掉、把大厅亮出来。
        if (ShouldHideTownForIntro) return;
        TutorialDirector.ClearTownBlockers();
    }

    void EnsureIntroCover()
    {
        if (_introCover != null) return;
        _introCover = GetComponent<CanvasGroup>();
        if (_introCover == null)
            _introCover = gameObject.AddComponent<CanvasGroup>();
    }

    public static bool IsChromeVisible =>
        Instance != null
        && (Instance._introCover == null || Instance._introCover.alpha > 0.5f);

    /// <summary>片头与开场剧情期间隐藏/恢复公会大厅 UI。</summary>
    public static void SetTownChromeVisible(bool visible)
    {
        if (Instance == null) return;
        Instance.EnsureIntroCover();
        Instance._introCover.alpha = visible ? 1f : 0f;
        Instance._introCover.interactable = visible;
        Instance._introCover.blocksRaycasts = visible;
    }

    /// <summary>看板娘 SpeechBubble：打字机 + 多台词 + 闲时隐藏</summary>
    void EnsureSpeechBubbleTalker()
    {
        if (GetComponent<SpeechBubbleTalker>() == null)
            gameObject.AddComponent<SpeechBubbleTalker>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_blinkCo != null) StopCoroutine(_blinkCo);
    }

    IEnumerator StaminaHudLoop()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        float sinceSave = 0f;
        while (true)
        {
            StaminaSystem.Tick(save: false);
            RefreshStamina();
            sinceSave += 0.5f;
            // 约每 30 秒落盘一次体力回复，避免杀进程丢进度
            if (sinceSave >= 30f)
            {
                sinceSave = 0f;
                SaveSystem.Instance?.Save();
            }
            yield return wait;
        }
    }

    public static void RefreshAllHudStatic()
    {
        if (Instance != null) Instance.RefreshAllHud();
    }

    public void RefreshAllHud()
    {
        RefreshGold();
        RefreshStamina();
    }

    /// <summary>确保底部导航组件存在并初始化为公会选中。</summary>
    void EnsureBottomNav()
    {
        var all = GetComponentsInChildren<MainBottomNav>(true);
        if (all != null && all.Length > 1)
        {
            // 只留一个，避免重复监听导致越点越卡
            for (int i = 1; i < all.Length; i++)
            {
                if (all[i] != null) Destroy(all[i]);
            }
        }

        if (bottomNav == null)
            bottomNav = GetComponentInChildren<MainBottomNav>(true);

        if (bottomNav == null)
        {
            Transform host = null;
            Transform bg = FindDeepChild(transform, "BottomNavBG");
            if (bg != null) host = bg.parent;
            if (host == null) host = FindDeepChild(transform, "BottomNav");
            if (host != null)
                bottomNav = host.gameObject.AddComponent<MainBottomNav>();
        }

        if (bottomNav != null)
            bottomNav.Initialize(MainNavTab.Guild);
    }

    /// <summary>
    /// 红点：不手摆每个图标图，运行时 Bind 到右上角。
    /// 有新消息时 RedDot.Set(key, true) 即可。
    /// </summary>
    void BindRedDots()
    {
        if (mailButton != null) RedDot.Bind(mailButton.transform, RedDot.Mail);
        if (noticeButton != null) RedDot.Bind(noticeButton.transform, RedDot.Notice);
        if (activityButton != null) RedDot.Bind(activityButton.transform, RedDot.Activity);
        if (shopButton != null) RedDot.Bind(shopButton.transform, RedDot.Shop);
        if (rankButton != null) RedDot.Bind(rankButton.transform, RedDot.Rank);

        if (bottomNav != null)
        {
            if (bottomNav.characterButton != null)
                RedDot.Bind(bottomNav.characterButton.transform, RedDot.Character);
            if (bottomNav.tavernButton != null)
                RedDot.Bind(bottomNav.tavernButton.transform, RedDot.Tavern);
            if (bottomNav.logButton != null)
                RedDot.Bind(bottomNav.logButton.transform, RedDot.Log);
        }
    }

    void AutoBindMissingRefs()
    {
        if (mascotEyes == null)
        {
            Transform eyes = FindDeepChild(transform, "eyes");
            if (eyes != null) mascotEyes = eyes as RectTransform;
        }
        if (mascotEyes != null)
            _eyesBaseScale = mascotEyes.localScale;

        if (staminaText == null)
        {
            Transform staminaPanel = FindDeepChild(transform, "体力Panel");
            if (staminaPanel != null)
            {
                Transform t = FindDeepChild(staminaPanel, "GoldText");
                if (t != null) staminaText = t.GetComponent<Text>();
                Transform plus = FindDeepChild(staminaPanel, "PlusButton");
                if (plus != null) staminaPlusButton = plus.GetComponent<Button>();
                EnsureStaminaRegenLabel(staminaPanel);
            }
        }
        else if (staminaRegenText == null)
        {
            Transform staminaPanel = FindDeepChild(transform, "体力Panel");
            if (staminaPanel != null) EnsureStaminaRegenLabel(staminaPanel);
        }

        if (goldText == null)
        {
            Transform goldPanel = FindDeepChild(transform, "GoldPanel");
            if (goldPanel != null)
            {
                Transform t = FindDeepChild(goldPanel, "GoldText");
                if (t != null) goldText = t.GetComponent<Text>();
                Transform plus = FindDeepChild(goldPanel, "PlusButton");
                if (plus != null) goldPlusButton = plus.GetComponent<Button>();
            }
        }
    }

    void EnsureStaminaRegenLabel(Transform staminaPanel)
    {
        if (staminaPanel == null) return;
        Transform exist = FindDeepChild(staminaPanel, "RegenTimer");
        if (exist != null)
        {
            staminaRegenText = exist.GetComponent<Text>();
            return;
        }

        var go = new GameObject("RegenTimer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(staminaPanel, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -2f);
        rt.sizeDelta = new Vector2(0f, 28f);
        var text = go.GetComponent<Text>();
        text.font = GameFonts.GetChinese();
        text.fontSize = 20;
        text.alignment = TextAnchor.UpperCenter;
        text.color = new Color(0.95f, 0.9f, 0.55f, 1f);
        text.raycastTarget = false;
        text.text = "";
        staminaRegenText = text;
    }

    /// <summary>预制体上的「冒险者公会」统一改成皇家冒险者公会，不写回 prefab。</summary>
    void ApplyGuildNameTexts()
    {
        var texts = GetComponentsInChildren<Text>(true);
        if (texts == null) return;
        string full = GameConfig.GUILD_NAME;
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null || string.IsNullOrEmpty(t.text)) continue;
            if (t.text.Contains(full)) continue;
            if (t.text.Contains("冒险者公会"))
                t.text = t.text.Replace("冒险者公会", full);
        }
    }

    /// <summary>未接通入口运行时隐藏（可重复调用；切回公会页时会再刷一遍）。</summary>
    public void HideUnfinishedHallButtons()
    {
        ResolveHallButtonRefs();
        // 未接通或半成品：运行时隐藏，不改预制体；清单见 Docs/软著后开发备忘.md「主界面暂隐入口」
        SetBtnHidden(noticeButton);
        SetBtnHidden(rankButton);
        SetBtnHidden(mailButton);        // 无收件箱 UI，仅一键领完
        SetBtnHidden(activityButton);    // 名义活动，实际开里程页，易误导
        SetBtnHidden(noticeBoardButton); // 公告栏
        SetBtnHidden(armoryButton);      // 武器库（遗产浏览）
        SetBtnHidden(licenseHallButton); // 执照厅
        // 商店 2026-09-15 已接通（ShopUI），保留显示
        // 按节点名再兜底（引用丢失时仍能藏）
        HideNamed("NoticeButton", "RankButton", "MailButton", "ActivityButton",
            "NoticeBoard", "Armory", "LicenseHall");
        // 保留：底栏五入口、设置、咨询台、金币/体力加号
    }

    void ResolveHallButtonRefs()
    {
        if (mailButton == null) mailButton = FindBtn("MailButton");
        if (noticeButton == null) noticeButton = FindBtn("NoticeButton");
        if (activityButton == null) activityButton = FindBtn("ActivityButton");
        if (rankButton == null) rankButton = FindBtn("RankButton");
        if (shopButton == null) shopButton = FindBtn("ShopButton");
        if (settingsButton == null) settingsButton = FindBtn("SettingsButton");
        if (noticeBoardButton == null) noticeBoardButton = FindBtn("NoticeBoard");
        if (licenseHallButton == null) licenseHallButton = FindBtn("LicenseHall");
        if (armoryButton == null) armoryButton = FindBtn("Armory");
        if (receptionistButton == null) receptionistButton = FindBtn("Receptionist");
    }

    Button FindBtn(string name)
    {
        var t = FindDeepChild(transform, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    void HideNamed(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var t = FindDeepChild(transform, names[i]);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    void WireHallClicks()
    {
        // 导航改由 MainBottomNav 负责，这里只接大厅自身控件
        HideUnfinishedHallButtons();

        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => BattleSettingsPanel.Ensure().Open(SettingsHost.Town));
        // 商店：2026-09-15 接通，技能解锁 / 抽卡券 / 资源补给 / 材料 四个货架
        if (shopButton != null)
        {
            shopButton.onClick.RemoveAllListeners();
            shopButton.onClick.AddListener(() => ShopUI.Show());
        }
        else
        {
            // 预制体引用丢失时按节点名兜底，避免入口点不动
            var shopNode = FindDeepChild(transform, "ShopButton");
            if (shopNode != null)
            {
                shopButton = shopNode.GetComponent<Button>();
                if (shopButton != null)
                {
                    shopButton.onClick.RemoveAllListeners();
                    shopButton.onClick.AddListener(() => ShopUI.Show());
                }
            }
        }
        // 2026-09-19：这两个加号原是「看广告」入口，现已改成钻石开启，不属于广告内容，
        // 所以聚光灯版本也要显示（原先 SpotlightBuild.Enabled 时会整个隐藏）。
        if (goldPlusButton != null)
        {
            goldPlusButton.gameObject.SetActive(true);
            goldPlusButton.onClick.RemoveAllListeners();
            goldPlusButton.onClick.AddListener(ResourceAdRewards.TryClaimGold);
        }
        if (staminaPlusButton != null)
        {
            staminaPlusButton.gameObject.SetActive(true);
            staminaPlusButton.onClick.RemoveAllListeners();
            staminaPlusButton.onClick.AddListener(ResourceAdRewards.TryClaimStamina);
        }
    }

    // 2026-09-20 主人要求：离线金币（含城镇主动入口 + 钻石翻倍）已暂停，相关代码已移除，待重设计后重新实现。

    static void SetBtnHidden(Button btn)
    {
        if (btn != null) btn.gameObject.SetActive(false);
    }

    void OnLicenseHall()
    {
        TownSaveAlign.AlignAll();
        var data = SaveSystem.Instance?.Data;
        int guild = data != null ? data.guildLevel : 1;
        int mercs = TownSaveAlign.DeployMercCount();
        int legacy = TownSaveAlign.LegacyPoolCount();
        UIManager.Instance?.ShowToast($"公会 Lv{guild} · 出战佣兵 {mercs} · 遗产 {legacy}");
        AchievementMilestoneUI.Show();
    }

    void OnMailClicked()
    {
        int n = MailSystem.UnclaimedCount();
        if (n <= 0)
        {
            UIManager.Instance?.ShowToast("暂无未读邮件");
            Debug.Log("[GuildHall] 邮件箱空");
            return;
        }

        var inbox = MailSystem.GetInbox();
        int claimed = 0;
        for (int i = 0; i < inbox.Count; i++)
        {
            var m = inbox[i];
            if (m == null || m.claimed) continue;
            if (MailSystem.TryClaim(m.id, notify: false))
                claimed++;
        }
        RefreshAllHud();
        RedDot.RefreshCommon();
        if (claimed > 0)
            UIManager.Instance?.ShowToast($"已领取 {claimed} 封邮件");
        else
            UIManager.Instance?.ShowToast($"有 {n} 封邮件，资源仍达上限无法领取");
        Debug.Log($"[GuildHall] 邮件领取 claimed={claimed} remain={MailSystem.UnclaimedCount()}");
    }

    public void RefreshGold()
    {
        if (goldText == null) return;
        long gold = SaveSystem.Instance?.Data?.totalGold ?? 0;
        goldText.text = FormatResource(gold);
    }

    public void RefreshStamina()
    {
        StaminaSystem.Tick(save: false);
        int cur = StaminaSystem.Current;
        if (staminaText != null)
            staminaText.text = cur.ToString(); // 只显示当前值，不要 100/100

        if (staminaRegenText != null)
        {
            if (StaminaSystem.IsFull)
            {
                if (staminaRegenText.gameObject.activeSelf)
                    staminaRegenText.gameObject.SetActive(false);
            }
            else
            {
                if (!staminaRegenText.gameObject.activeSelf)
                    staminaRegenText.gameObject.SetActive(true);
                staminaRegenText.text = "回满 " + StaminaSystem.FormatCountdown(StaminaSystem.SecondsToFull);
            }
        }
    }

    static string FormatResource(long v)
    {
        if (v >= ResourceWallet.DEFAULT_MAX)
            return ResourceWallet.DEFAULT_MAX.ToString();
        return v.ToString("N0");
    }

    void StartBlink()
    {
        if (mascotEyes == null) return;
        if (_blinkCo != null) StopCoroutine(_blinkCo);
        _blinkCo = StartCoroutine(BlinkLoop());
    }

    IEnumerator BlinkLoop()
    {
        while (mascotEyes != null)
        {
            float wait = Random.Range(blinkMinInterval, blinkMaxInterval);
            yield return new WaitForSeconds(wait);
            yield return BlinkOnce();
            if (Random.value < 0.22f)
            {
                yield return new WaitForSeconds(0.12f);
                yield return BlinkOnce();
            }
        }
    }

    IEnumerator BlinkOnce()
    {
        if (mascotEyes == null) yield break;
        Vector3 open = _eyesBaseScale;
        Vector3 closed = new Vector3(open.x, open.y * blinkClosedScaleY, open.z);

        float t = 0f;
        while (t < blinkCloseDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, blinkCloseDuration));
            mascotEyes.localScale = Vector3.Lerp(open, closed, u);
            yield return null;
        }
        mascotEyes.localScale = closed;

        t = 0f;
        while (t < blinkOpenDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, blinkOpenDuration));
            mascotEyes.localScale = Vector3.Lerp(closed, open, u);
            yield return null;
        }
        mascotEyes.localScale = open;
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeepChild(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 瘦屏：热点跟随背景底图等比放大（本界面专用，不进 UiLayoutStretch 通用规则）
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 需要跟随背景底图一起等比放大的热点节点名；以后加热点直接往这个数组里加即可。
    /// 这些节点在预制体里是「锚点居中 + 固定绝对偏移」，底图被 EnvelopeParent 放大后它们不会跟着走，
    /// 于是美术里的咨询台 / 气泡和热区框就错开了。
    /// </summary>
    static readonly string[] HotspotFollowNames = { "Receptionist", "SpeechBubble" };

    /// <summary>热点基准：首次执行时捕获，之后每次都「从 base 重算」，绝不基于当前值累加。</summary>
    struct HotspotFollowBase
    {
        public RectTransform rectTransform;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector3 localScale;
        public bool hasBase;
    }

    readonly HotspotFollowBase[] _hotspotFollows = new HotspotFollowBase[HotspotFollowNames.Length];

    RectTransform _bgRect;
    float _bgBaseW;
    float _bgBaseH;
    bool _bgBaseCaptured;
    bool _hotspotFollowApplied;
    float _hotspotFollowK = 1f;
    bool _hotspotFollowPending;
    bool _applyingBgEnvelope;

    /// <summary>城镇大厅底图（Background）运行时 envelope 铺满，覆盖更瘦屏上下空区。</summary>
    void ApplyBackgroundEnvelope()
    {
        // 防重入：改 rect 可能触发自身的尺寸变化回调（见 OnRectTransformDimensionsChange）。
        if (_applyingBgEnvelope) return;
        _applyingBgEnvelope = true;
        try
        {
            Transform bg = FindDeepChild(transform, "Background");
            if (bg == null) return;
            var rt = bg as RectTransform;
            if (rt == null) return;
            _bgRect = rt;
            UiLayoutStretch.ApplyEnvelopeImage(rt);
            CaptureBgBaseRect(rt);
            // 此刻 rect 还没被 AspectRatioFitter 放大，这里多半算出 k≈1（等于把热点还原到 base）。
            ApplyHotspotFollowBg();
            // 等 Canvas 布局重建、AspectRatioFitter 把 rect 写回之后再补算一次（一次性，不是每帧轮询）。
            EnsureHotspotFollowDeferred();
        }
        finally
        {
            _applyingBgEnvelope = false;
        }
    }

    /// <summary>
    /// 捕获 Background 的基准 rect（算放大倍数 k 的分母）。
    /// 为什么选「ApplyEnvelopeImage 之后、AspectRatioFitter 生效之前」这一瞬间，而不是更早或更晚：
    ///   - 更早（调用 ApplyEnvelopeImage 之前）拿到的是预制体的出血尺寸 799×1420，它比 720×1280 设计框
    ///     还大 1.11 倍，拿它当分母会让 k 在标准 9:16 屏上变成 0.90，标准屏也会被缩放，
    ///     违反「标准屏一个像素都不动」的硬要求，所以不能更早；
    ///   - ApplyEnvelopeCenter 会先把 anchor/pivot 居中、把 sizeDelta 设成「父级 rect」，
    ///     而 AspectRatioFitter 要到下一次 Canvas 布局重建（本帧稍后）才会把 rect 调成最终放大值，
    ///     所以紧跟在 ApplyEnvelopeImage 之后同步读到的正是「父级设计框」720×H，这才是设计基准的正确含义；
    ///   - 更晚（等 rect 稳定后再捕）读到的是「当前屏幕的放大结果」，屏幕一变基准就跟着变，k 会恒等于 1。
    /// 另外这里每次 ApplyBackgroundEnvelope 都重捕一次、而不是只捕一次：旋转 / 分辨率变化后父级高度会变，
    /// 基准必须同步更新，否则「从瘦屏切到更瘦屏」时 k 会算错。重捕不产生累积：k 永远由 base 重算。
    /// </summary>
    void CaptureBgBaseRect(RectTransform bgRt)
    {
        if (bgRt == null) return;
        // 没有 Image 时 ApplyEnvelopeImage 会直接 return，rect 仍是预制体出血尺寸，不能当基准。
        if (bgRt.GetComponent<Image>() == null) return;
        var r = bgRt.rect;
        // 父级还没量好尺寸（w/h 为 0）：保留已有基准，等下一次调用再捕。
        if (r.width < 1f || r.height < 1f) return;
        _bgBaseW = r.width;
        _bgBaseH = r.height;
        _bgBaseCaptured = true;
    }

    /// <summary>
    /// 让热点按背景底图的放大倍数一起等比放大：锚点 / 轴心保持 base 不变，
    /// 「离屏中心的距离」（anchoredPosition）与「自身大小」（localScale）同乘 k，
    /// 等价于把这些热点塞进 Background 里跟底图一起缩放，美术与热区框就重新对齐。
    /// </summary>
    void ApplyHotspotFollowBg()
    {
        if (!_bgBaseCaptured || _bgRect == null) return;
        var r = _bgRect.rect;
        if (r.width < 1f || r.height < 1f) return;

        // 整体等比 = 取较大者，与 AspectRatioFitter.EnvelopeParent「铺满父级」的语义一致。
        float k = Mathf.Max(r.width / _bgBaseW, r.height / _bgBaseH);
        bool apply = EnableHotspotFollowBg
                     && (UiLayoutStretch.IsThinnerScreen() || Mathf.Abs(k - 1f) > 0.001f);

        // 幂等守卫：状态没变就不重复写，既防重复叠加，也避免和其它布局回调互相打架。
        if (apply && _hotspotFollowApplied && Mathf.Abs(k - _hotspotFollowK) < 0.0005f) return;
        if (!apply && !_hotspotFollowApplied) return;
        _hotspotFollowApplied = apply;
        _hotspotFollowK = apply ? k : 1f;

        ResolveHotspots();
        CaptureHotspotBases();
        // apply=false（标准屏 k≈1 或开关关掉）时传 1f，等于把热点完整还原到 base。
        WriteHotspots(apply ? k : 1f);
    }

    /// <summary>按名字找热点；找不到就留空，之后一直跳过，绝不新建节点。</summary>
    void ResolveHotspots()
    {
        for (int i = 0; i < HotspotFollowNames.Length; i++)
        {
            if (_hotspotFollows[i].rectTransform != null) continue;
            var t = FindDeepChild(transform, HotspotFollowNames[i]);
            var rt = t as RectTransform;
            if (rt == null) continue;
            _hotspotFollows[i].rectTransform = rt;
        }
    }

    /// <summary>首次遇到时捕获热点基准（anchorMin/anchorMax/pivot/anchoredPosition/localScale）。</summary>
    void CaptureHotspotBases()
    {
        for (int i = 0; i < _hotspotFollows.Length; i++)
        {
            var item = _hotspotFollows[i];
            if (item.rectTransform == null || item.hasBase) continue;
            var rt = item.rectTransform;
            item.anchorMin = rt.anchorMin;
            item.anchorMax = rt.anchorMax;
            item.pivot = rt.pivot;
            item.anchoredPosition = rt.anchoredPosition;
            item.localScale = rt.localScale;
            item.hasBase = true;
            _hotspotFollows[i] = item;
        }
    }

    /// <summary>从 base 重算写入（k=1 即还原）。只写偏移与缩放，不动子节点结构与 sizeDelta。</summary>
    void WriteHotspots(float k)
    {
        for (int i = 0; i < _hotspotFollows.Length; i++)
        {
            var item = _hotspotFollows[i];
            var rt = item.rectTransform;
            if (rt == null || !item.hasBase) continue;
            rt.anchorMin = item.anchorMin;
            rt.anchorMax = item.anchorMax;
            rt.pivot = item.pivot;
            rt.anchoredPosition = item.anchoredPosition * k;
            rt.localScale = new Vector3(item.localScale.x * k, item.localScale.y * k, item.localScale.z * k);
        }
    }

    /// <summary>布局稳定后补算一次；已有待执行的就不再排队。</summary>
    void EnsureHotspotFollowDeferred()
    {
        if (_hotspotFollowPending) return;
        if (!gameObject.activeInHierarchy) return;
        _hotspotFollowPending = true;
        StartCoroutine(HotspotFollowDeferred());
    }

    IEnumerator HotspotFollowDeferred()
    {
        yield return new WaitForEndOfFrame();   // 本帧渲染前 Canvas 已重建布局，fitter 写完 rect
        yield return null;                      // 再多等一帧，确保读到的是稳定后的 rect
        _hotspotFollowPending = false;
        ApplyHotspotFollowBg();
    }

    /// <summary>
    /// 分辨率 / 旋转导致根节点尺寸变化时重跑一遍（覆盖「瘦屏旋转 / 分辨率变化」）。
    /// 走 Unity 的尺寸变化回调，不是每帧轮询（项目里 SafeAreaFitter / LoginUI 也是这么接的）。
    /// </summary>
    void OnRectTransformDimensionsChange()
    {
        if (_bgRect == null) return;   // 尚未初始化（Awake 里 ApplyBackgroundEnvelope 之前）不处理
        ApplyBackgroundEnvelope();
    }
}
