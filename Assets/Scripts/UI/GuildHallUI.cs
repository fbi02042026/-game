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

    void Awake()
    {
        Instance = this;
        UICanvasSetup.ApplyOn(gameObject, UICanvasSetup.ResolveUiCamera());
        UiPrefabRectGuard.Attach(transform, "Background");
        GameFonts.ApplyToHierarchy(transform);
        AutoBindMissingRefs();
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

    void HideUnfinishedHallButtons()
    {
        SetBtnHidden(shopButton);
        SetBtnHidden(noticeButton);
        SetBtnHidden(rankButton);
        SetBtnHidden(noticeBoardButton); // 公告栏
        SetBtnHidden(armoryButton);      // 武器库（遗产浏览暂隐）
        SetBtnHidden(licenseHallButton);  // 执照厅
        // 金币/体力加号保留，按存档发放
    }

    void WireHallClicks()
    {
        // 导航改由 MainBottomNav 负责，这里只接大厅自身控件
        HideUnfinishedHallButtons();

        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => BattleSettingsPanel.Ensure().Open(SettingsHost.Town));
        if (mailButton != null)
            mailButton.onClick.AddListener(OnMailClicked);
        if (licenseHallButton != null)
            licenseHallButton.onClick.AddListener(OnLicenseHall);
        if (armoryButton != null)
            armoryButton.onClick.AddListener(LegacyPoolBrowseUI.Show);
        if (activityButton != null)
            activityButton.onClick.AddListener(() => AchievementMilestoneUI.Show());
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
}
