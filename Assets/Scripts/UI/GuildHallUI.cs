using System.Collections;
using System.Collections.Generic;
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
    // 2026-10-22 主人拍板（T14）：顶条钻石余额只绑显示，加号不绑任何点击、不写扣钻石逻辑
    public Text diamondText;
    [Tooltip("体力未满时显示回满倒计时，可空（自动在体力Panel下创建）")]
    public Text staminaRegenText;

    [Header("玩家名牌（topleft 下，2026-10-25 主人加的）")]
    public Text playerNameText;   // 玩家名牌/Name：玩家起的名字
    public Text playerLevelText;  // 玩家名牌/Level：玩家等级
    public Image playerAvatarIcon; // 玩家名牌/Icon：当前头像（点击打开头像选择框，确认后这里同步换图）
    // 【2026-10-28 更正】10-27 的需求是「角色页」头像去掉，本文件（主界面名牌）头像被那次提交误伤隐藏，当日恢复——主界面头像保留。
    // 【2026-10-25 主人反馈「左上角的头像有点小」】名牌 Icon 本体放大倍率：
    // 只乘在 Icon 的 localScale 上（预制体作者刻度 0.9 之上 ×1.3），名牌底板/文字一律不动；
    // 绑定时做一次，幂等；要再调大小只改这一个常量。
    const float NameplateIconScale = 1.3f;

    [Header("左侧")]
    public Button mailButton;
    public Button dailyLoginButton;   // 主界面右列的 DailyLoginButton 节点（预制体引用），点击打开每日登录面板
    public Button noticeButton;
    public Button activityButton;

    [Header("看板人物（后期替换人物用，大小不动）")]
    [Tooltip("看板节点下 show 子节点的 Image，预留替换接口。后期只换 sprite，不动尺寸/位置。")]
    public Image boardCharacterImage;
    [Range(0f, 0.08f)] public float boardBreathAmount = 0.01f;   // 呼吸幅度（复用剧情立绘同款 PortraitIdleMotion），0=不呼吸
    // 2026-10-16 主人拍板：看板立绘去掉上下浮动(boardBobPixels)/微倾(boardTiltDegrees)，只留底部为轴的呼吸

    [Header("看板切换动画（Turn 按钮，旧退新进的过场）")]
    [Range(0.05f, 0.5f)] public float turnSwapOutSeconds = 0.16f; // 旧立绘缩小淡出时长
    [Range(0.05f, 0.8f)] public float turnSwapInSeconds = 0.24f;  // 新立绘放大淡入时长
    [Range(0.1f, 0.9f)] public float turnSwapShrink = 0.7f;       // 退出缩到基准的倍数（0.7 = 向后变小 0.3 倍）

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
    Button _turnButton;
    string _turnBoardId;   // 主界面看板当前显示的人物 id（佣兵 HireId；空串/null = 预制体默认图 = 前台小姐）
    Sprite _defaultBoardSprite;   // 捕获的预制体默认看板图（=前台小姐），轮换"回到前台"时复用，避免依赖运行时资源路径
    /// <summary>【2026-10-26 主人拍板】看板当前人物 id（含进主界面恢复的上次选择）。
    /// 气泡台词池 / 专属台词 / 点击说话都按它取，SpeechBubbleTalker 只读。</summary>
    public string CurrentBoardId => _turnBoardId;

    /// <summary>看板当前是否默认图（=前台小姐）。false = 玩家点 Turn 换成了佣兵立绘，前台表情彩蛋此时不生效。</summary>
    public bool BoardIsDefaultReceptionist => string.IsNullOrEmpty(_turnBoardId);
    Coroutine _turnSwapCo; // 看板切换过场动画协程（播完置 null；未播完时忽略连点）

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureTopBarFromPrefab();
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
        // 【2026-10-26 主人拍板「记忆上次替换的人物」】先恢复上次看板人物，再建气泡 ——
        // SpeechBubbleTalker.Awake 建台词池时按恢复后的 CurrentBoardId 取池，顺序不能反。
        RestoreBoardCharacter();
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
        // 【2026-10-25 主人需求】主界面各布局入场动效（左侧从左/右上从右/底部从下滑入）；
        // 片头期间自动转 pending，等 SetTownChromeVisible(true) 揭开大厅时才播。
        TownEntranceMotion.Schedule(this);
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
        // 【2026-10-25 主人需求】片头揭开大厅时补播入场动效（Schedule 在片头期间转了 pending）
        if (visible) TownEntranceMotion.PlayPending();
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

    // ── 体力加号购买成功飘字（2026-10-20 主人要求：点体力加号购买体力要弹字提示「体力+XX」）──

    /// <summary>
    /// 体力加号领取/购买成功后，在加号上方飘「体力图标 + 体力+XX」（上浮渐隐约 0.9 秒）。
    /// 纯运行时白模、用完即毁，不动预制体；样式与 PlayerJobSelectUI 的体力消耗飘字同口径。
    /// </summary>
    public static void FloatStaminaGain(int amount)
    {
        if (Instance == null || !Instance.isActiveAndEnabled) return;
        if (Instance.staminaPlusButton == null || amount <= 0) return;
        Instance.StartCoroutine(Instance.CoStaminaGainFloat(amount));
    }

    IEnumerator CoStaminaGainFloat(int amount)
    {
        GameObject floatGo = BuildStaminaGainFloat(amount);
        var group = floatGo != null ? floatGo.GetComponent<CanvasGroup>() : null;
        var rt = floatGo != null ? (RectTransform)floatGo.transform : null;
        const float Life = 0.9f;   // 飘字时长（秒）
        const float Rise = 34f;    // 上浮距离（像素）
        float baseY = rt != null ? rt.anchoredPosition.y : 0f;
        for (float t = 0f; t < Life; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / Life);
            if (rt != null) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY + Rise * k);
            if (group != null) group.alpha = 1f - k;
            yield return null;
        }
        if (floatGo != null) Destroy(floatGo);
    }

    /// <summary>在体力加号顶边上方挂临时飘字（体力图标 + "体力+N"），纯运行时白模、用完即毁。</summary>
    GameObject BuildStaminaGainFloat(int amount)
    {
        var btnRt = staminaPlusButton.transform as RectTransform;
        if (btnRt == null) return null;

        // 上次协程被打断可能留了残影，先清掉再建新的
        var old = btnRt.Find("StaminaGainFloat");
        if (old != null) Destroy(old.gameObject);

        var go = new GameObject("StaminaGainFloat", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(btnRt, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 10f);
        rt.sizeDelta = new Vector2(200f, 58f);

        // 深色半透明底板（同 PlayerJobSelectUI 体力飘字口径）
        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var brt = (RectTransform)bg.transform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        var bimg = bg.GetComponent<Image>();
        bimg.color = new Color(0.08f, 0.07f, 0.1f, 0.62f);
        bimg.raycastTarget = false;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(go.transform, false);
        var irt = (RectTransform)icon.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(44f, 44f);
        irt.anchoredPosition = new Vector2(-56f, 0f);
        var img = icon.GetComponent<Image>();
        // 与资源条同一体力图标（邮件/每日登录体力格用的是药水图，不混用）
        img.sprite = Resources.Load<Sprite>("UI/Art/Icons/Common/icon_stamina");
        img.raycastTarget = false;
        bool hasIcon = img.sprite != null;
        icon.SetActive(hasIcon);

        var labelGo = new GameObject("Num", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(140f, 58f);
        lrt.anchoredPosition = new Vector2(hasIcon ? 20f : 0f, 0f);
        var t = labelGo.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = 38;
        t.fontStyle = FontStyle.Bold;
        t.color = new Color(1f, 0.78f, 0.30f, 1f);   // 同 PlayerPowerHud 获得类飘字的暖金色
        t.alignment = TextAnchor.MiddleCenter;
        t.text = $"体力+{amount}";
        t.raycastTarget = false;
        return go;
    }

    public void RefreshAllHud()
    {
        RefreshGold();
        RefreshStamina();
        RefreshDiamond();   // 2026-10-22 主人拍板（T14）：钻石余额并入现有 HUD 刷新链（ResourceWallet.Add/TrySpend 会自动调 RefreshAllHudStatic）
        RefreshNameplate(); // 2026-10-25 主人加的 topleft 玩家名牌：名字/等级并入同一刷新链
    }

    /// <summary>
    /// 玩家名牌刷新：名字 = StoryProgress.GetPlayerName()（唯一出口，未起名回落默认名），
    /// 等级 = LevelSystem.CurrentLevel（存档永久账，2026-10-25 矫正后跨关/跨章累计）。
    /// 只写文本，不动尺寸/位置。
    /// </summary>
    void RefreshNameplate()
    {
        if (playerNameText != null)
            playerNameText.text = StoryProgress.GetPlayerName();
        if (playerLevelText != null)
            playerLevelText.text = Mathf.Max(1, LevelSystem.CurrentLevel).ToString();
        // 名牌头像：按存档槽位设图（真源 AvatarDefs；头像选择框确认后走 RefreshAllHud 到这里换图）
        if (playerAvatarIcon != null)
        {
            var avatarSp = AvatarDefs.LoadSprite(AvatarDefs.CurrentSlot());
            if (avatarSp != null) playerAvatarIcon.sprite = avatarSp;
            else Debug.LogError("[GuildHall] 名牌头像图取不到：槽位 " + AvatarDefs.CurrentSlot() + "（不兜底）");
        }
    }

    /// <summary>名牌头像点击入口：打开头像选择弹窗（2026-10-25 主人需求；2026-10-28 随主界面头像一并恢复）。</summary>
    void OpenAvatarSelect() => AvatarSelectPopup.Open();

    /// <summary>顶条钻石余额显示（T14）：只读 ResourceWallet 的 Diamond，不做任何扣费/加号点击逻辑。</summary>
    void RefreshDiamond()
    {
        if (diamondText == null) return;
        var data = SaveSystem.Instance?.Data;
        long diamond = data != null ? ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond) : 0;
        diamondText.text = FormatResource(diamond);
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
        if (dailyLoginButton != null) RedDot.Bind(dailyLoginButton.transform, RedDot.DailyLogin);
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

    /// <summary>
    /// 【2026-10-25 主人拍板「顶条拆出来变成预制体，我去调位置」】
    /// 顶条真源 = <c>Resources/Prefabs/Town/TopBar.prefab</c>（体力/金币/钻石三列、标题徽章、
    /// 邮件/设置按钮、顶条底图都在里面）；GuildHallUI.prefab 已不再内嵌 TopBar —— 主人在
    /// TopBar.prefab 里调位置，所有共用顶条的界面（大厅/冒险/角色/酒馆…）同步生效。
    /// Awake 最先按路径实例化挂回大厅根下原位（原内嵌 TopBar 是根下第 3 个子节点，序号保留），
    /// 之后的按名绑定链路（AutoBindMissingRefs / WireHallClicks→ResolveHallButtonRefs /
    /// ResourceStrip.RegisterHall / TownSharedChrome）全部照常工作。
    /// 缺预制体 LogError 不兜底（AGENTS.md §4）。
    /// </summary>
    void EnsureTopBarFromPrefab()
    {
        if (transform.Find("TopBar") != null) return;   // 幂等：重入不重复建
        var prefab = Resources.Load<GameObject>("Prefabs/Town/TopBar");
        if (prefab == null)
        {
            Debug.LogError("[GuildHall] 顶条预制体取不到：Resources/Prefabs/Town/TopBar —— 顶条不显示（不兜底）");
            return;
        }
        var go = Instantiate(prefab, transform, false);
        go.name = "TopBar";
        go.transform.SetSiblingIndex(2);   // 原内嵌 TopBar 在大厅根下的兄弟序号（渲染顺序保持）
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
                // 【2026-10-25 修复「体力一直显示 999」】顶条拆成独立预制体后主人把数字节点
                // 改名为「体力Text」（TopBar.prefab 真源），还按老内嵌顶条的「GoldText」找
                // 永远绑不上 → staminaText 恒 null → 预制体默认值 99999 一直挂在屏上。
                Transform t = FindDeepChild(staminaPanel, "体力Text");
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

        // 2026-10-22 主人拍板（T14）：顶条钻石余额按名绑定钻石Panel/钻石Text；只绑显示，加号不接任何点击
        if (diamondText == null)
        {
            Transform diamondPanel = FindDeepChild(transform, "钻石Panel");
            if (diamondPanel != null)
            {
                Transform t = FindDeepChild(diamondPanel, "钻石Text");
                if (t != null) diamondText = t.GetComponent<Text>();
            }
            if (diamondText == null)
                Debug.LogError("[GuildHall] 未找到顶条钻石余额节点 钻石Panel/钻石Text（应在主界面预制体顶条），钻石余额将无法显示");
        }

        // 【2026-10-25 主人加的 topleft 玩家名牌】名字 = StoryProgress.GetPlayerName()，等级 = LevelSystem.CurrentLevel；
        // 节点缺失 LogError 不兜底（AGENTS.md §4）。
        if (playerNameText == null || playerLevelText == null)
        {
            Transform plate = FindDeepChild(transform, "玩家名牌");
            if (plate == null)
            {
                Debug.LogError("[GuildHall] 未找到玩家名牌节点 玩家名牌（应在主界面预制体 topleft 下），玩家名字/等级不显示");
            }
            else
            {
                if (playerNameText == null)
                {
                    Transform t = FindDeepChild(plate, "Name");
                    if (t != null) playerNameText = t.GetComponent<Text>();
                }
                if (playerLevelText == null)
                {
                    Transform t = FindDeepChild(plate, "Level");
                    if (t != null) playerLevelText = t.GetComponent<Text>();
                }
                if (playerNameText == null || playerLevelText == null)
                    Debug.LogError("[GuildHall] 玩家名牌下缺 Name/Level 文本节点，玩家名字/等级显示不完整");
            }
        }

        // 【2026-10-25 主人加的 玩家名牌/Icon】当前头像：sprite 按存档槽位主动设图
        // （名牌 Icon 的 prefab sprite 引用是悬空旧 hex32，走路径加载，AGENTS.md §2.3）；
        // 运行时补 Button 接线（预制体只有 Image），点击打开头像选择框。缺节点 LogError 不兜底。
        // 【2026-10-28 恢复】10-27 需求是「角色页」头像去掉，主界面名牌头像被 8c43649c 误伤隐藏，
        // 当日按 8c43649c^ 原样还原（Icon 放大 1.3 + Button 接线）；名字/等级不受影响。
        if (playerAvatarIcon == null)
        {
            Transform plateForIcon = FindDeepChild(transform, "玩家名牌");
            Transform iconT = plateForIcon != null ? FindDeepChild(plateForIcon, "Icon") : null;
            if (iconT != null)
            {
                playerAvatarIcon = iconT.GetComponent<Image>();
                // 【2026-10-25 主人反馈「左上角的头像有点小」】Icon 本体放大（Awake 只跑一次，幂等）
                iconT.localScale *= NameplateIconScale;
                var openBtn = iconT.GetComponent<Button>();
                if (openBtn == null) openBtn = iconT.gameObject.AddComponent<Button>();
                openBtn.transition = Selectable.Transition.None;
                openBtn.onClick.RemoveAllListeners();
                openBtn.onClick.AddListener(OpenAvatarSelect);
            }
            else
                Debug.LogError("[GuildHall] 玩家名牌下缺 Icon 节点，头像不显示也无法点开头像选择框");
        }

        // 【2026-10-24 主人拍板「提取顶条资源条」】金/钻引用绑完后再提取（迁移保留引用，主界面刷新照常生效）；
        // 幂等：场景重载后旧列随场景销毁，这里自动从新 TopBar 重新提取。见 ResourceStrip.cs。
        ResourceStrip.RegisterHall(transform);
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
        // 2026-10-16 主人拍板：信件按钮放出来（OnMailClicked 一键领完所有未领邮件）
        SetBtnHidden(activityButton);    // 名义活动，实际开里程页，易误导
        SetBtnHidden(noticeBoardButton); // 公告栏
        SetBtnHidden(armoryButton);      // 武器库（遗产浏览）
        SetBtnHidden(licenseHallButton); // 执照厅
        // 2026-10-15 主人拍板：商城按钮隐藏（ShopUI 代码与 wiring 保留，入口先藏；要恢复删这行即可）
        SetBtnHidden(shopButton);
        // 按节点名再兜底（引用丢失时仍能藏）
        HideNamed("NoticeButton", "RankButton", "ActivityButton",
            "NoticeBoard", "Armory", "LicenseHall", "ShopButton");
        // 保留：底栏五入口、设置、咨询台、金币/体力加号
    }

    void ResolveHallButtonRefs()
    {
        // 【2026-10-26 修「主界面邮件/设置打不开」】主人把按钮重新摆在 topright 下，节点名是中文
        // 「邮件」「设置」（GuildHallUI.prefab），且序列化引用为空——只按旧英文名找会全落空、
        // 点击事件接不上。英文名找不到时按中文名兜底（找得到就不动，最小增量）。
        if (mailButton == null) mailButton = FindBtn("MailButton");
        if (mailButton == null) mailButton = FindBtn("邮件");
        if (dailyLoginButton == null) dailyLoginButton = FindBtn("DailyLoginButton");
        // 看板人物：预留替换接口——绑「看板/show」的 Image（无则按节点名兜底；大小不动）
        if (boardCharacterImage == null)
        {
            var boardNode = FindDeepChild(transform, "看板");
            var showT = boardNode != null ? boardNode.Find("show") : null;
            if (showT != null) boardCharacterImage = showT.GetComponent<Image>();
        }
        // 看板人物：复用剧情立绘同款待机——只保留以底部为轴的呼吸缩放
        // （主人 2026-10-16：不要上下浮动/微倾，脚来回飘看着怪，tavernSway 关掉）
        if (boardCharacterImage != null)
            PortraitIdleMotion.EnsureOn(boardCharacterImage.rectTransform, 0.3f, tavernSway: false, breathAmount: boardBreathAmount);
        if (noticeButton == null) noticeButton = FindBtn("NoticeButton");
        if (activityButton == null) activityButton = FindBtn("ActivityButton");
        if (rankButton == null) rankButton = FindBtn("RankButton");
        if (shopButton == null) shopButton = FindBtn("ShopButton");
        if (settingsButton == null) settingsButton = FindBtn("SettingsButton");
        if (settingsButton == null) settingsButton = FindBtn("设置");
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
        // 2026-10-16 主人拍板：信件按钮放出来——点击一键领完所有未领邮件
        if (mailButton != null)
        {
            mailButton.onClick.RemoveAllListeners();
            mailButton.onClick.AddListener(OnMailClicked);
        }
        // 每日登录按钮：使用主界面预制体右列的 DailyLoginButton 节点（主人 2026-10-16 加的），点击打开每日登录面板
        EnsureDailyLoginButton();
        // 竹篓纳福入口（2026-10-28 主人拍板）：每日登录下方一格，运行时克隆生成（不动预制体）；
        // 玩法未上线前点击弹占位告知，接线只在 MiniGameEntryUI.OnClicked 一处。
        MiniGameEntryUI.EnsureInGuildHall(dailyLoginButton);
        // 切换人物按钮：主界面预制体 HallScene 下的 Turn 节点（主人 2026-10-16 加的），点击随机换看板人物
        EnsureTurnButton();
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

    /// <summary>每日登录按钮：使用主界面预制体右列的 DailyLoginButton 节点（不运行时生成）。点击打开每日登录面板。</summary>
    void EnsureDailyLoginButton()
    {
        if (dailyLoginButton == null)
        {
            Debug.LogError("[GuildHall] 未找到每日登录按钮节点 DailyLoginButton（应在主界面预制体右列）");
            return;
        }
        dailyLoginButton.onClick.RemoveAllListeners();
        dailyLoginButton.onClick.AddListener(() => DailyLoginUI.Show());
    }

    /// <summary>
    /// 切换人物按钮：主界面预制体 HallScene 下的 Turn 节点（主人 2026-10-16 加的，只摆了 Image+Shadow）。
    /// 运行时补 Button（不改预制体）+ 呼吸挂 PortraitIdleMotion（剧情同款，底部为轴，幅度 0.1）；
    /// 点击在「前台小姐 + 玩家已拥有的女性佣兵」里顺次轮换看板人物。
    /// </summary>
    void EnsureTurnButton()
    {
        if (_turnButton == null)
        {
            Transform t = FindDeepChild(transform, "Turn");
            if (t == null)
            {
                Debug.LogError("[GuildHall] 未找到切换人物按钮节点 Turn（应在主界面预制体 HallScene 下）");
                return;
            }
            _turnButton = t.GetComponent<Button>();
            if (_turnButton == null)
            {
                _turnButton = t.gameObject.AddComponent<Button>();
                _turnButton.transition = Selectable.Transition.None;   // 不改美术颜色反馈
            }
            // 呼吸跟剧情立绘同款（PortraitIdleMotion）：底部为轴、XY 等比、3.2s 周期；幅度主人定 0.05（2026-10-16 由 0.1 减半）
            PortraitIdleMotion.EnsureOn(t as RectTransform, 0.7f, tavernSway: false, breathAmount: 0.05f);
        }
        _turnButton.onClick.RemoveAllListeners();
        _turnButton.onClick.AddListener(OnTurnClicked);
    }

    /// <summary>
    /// 【2026-10-26 主人拍板「记忆上次替换的人物」】进主界面恢复上次看板人物。
    /// 真源 = SaveData.lastBoardCharacterId（"player"/佣兵 HireId；空 = 默认前台小姐，老档天然为空）。
    /// 立绘取不到不兜底：LogError 后保持默认图（铁律 14）；恢复成功后 CurrentBoardId 即生效，
    /// 气泡台词池由 SpeechBubbleTalker.Awake 按它建（Awake 时序：本方法先于 EnsureSpeechBubbleTalker）。
    /// </summary>
    void RestoreBoardCharacter()
    {
        // 捕获预制体默认看板图（=前台小姐），轮换"回到前台"时复用；必须在任何 SetBoardCharacter 之前捕获。
        if (boardCharacterImage != null && _defaultBoardSprite == null)
            _defaultBoardSprite = boardCharacterImage.sprite;
        string id = SaveSystem.Instance?.Data?.lastBoardCharacterId;
        if (string.IsNullOrEmpty(id)) return;
        if (boardCharacterImage == null)
        {
            Debug.LogError("[GuildHall] 看板记忆恢复中止：boardCharacterImage 未绑定（看板/show 节点缺失）");
            return;
        }
        Sprite sp = MercPortraitSprites.GetStand(id) ?? MercPortraitSprites.GetHead(id);
        if (sp == null)
        {
            Debug.LogError($"[GuildHall] 看板记忆恢复失败：{id} 的立绘与头像都取不到（Resources/Icons/MercStand|MercHead/{id}），保持默认前台");
            return;
        }
        SetBoardCharacter(sp);
        _turnBoardId = id;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 看板轮换池的女性名单（2026-10-26 主人拍板「优先只有女性职业出来 不要男性的」）
    // 性别判定 = 22 张立绘（Resources/Icons/MercStand/H0XX.png）逐一目检，遵守
    // MercRosterDefs.cs:76 教训「定性别必须先看立绘，不许凭名字凭空猜」——
    // 图鉴文案不可靠（AdventureLogCatalog 里路加写"她"，立绘是光头男僧）。
    // ⚠ 图鉴姓名与立绘存在错位（H013 图鉴叫"紫发法师莫娜"、立绘是金发女骑士；H019 同理），
    //   一律以立绘外观为准；以后换立绘要重核这份名单。
    // 边缘（未入池，主人可一句话调整）：H012 塞拉（设定女扮男装、立绘少年样）、
    //   H014 伊芙（兜帽长袍看不出女）；player（红发少年）按「不要男性的」一并出池。
    // ────────────────────────────────────────────────────────────────────────
    // 用 List 而非 HashSet：看板轮换需固定顺序（"轮流"可预期），List 书写顺序即轮换顺序。
    static readonly List<string> BoardFemaleIds = new List<string>
    {
        "H005",   // 米娅位：粉发少女弓手
        "H013",   // 金发女骑士（角盔+高跟鞋）
        "H015",   // 艾拉位：白发马尾女弓手
        "H016",   // 杜娅位：黑长发女战士
        "H017",   // 光头女僧侣
        "H019",   // 艾琳位：紫发女法师
    };

    /// <summary>
    /// 点 Turn：在「前台小姐 + 玩家拥有的女性佣兵」名单里顺次轮换（保证能切回前台），走「旧退新进」过场。
    /// 名单首位固定为前台小姐（空串哨兵），其后按 BoardFemaleIds 书写顺序接玩家已拥有的女性佣兵；
    /// 每次点击取当前人物的下一位（到末尾回到首位），形成闭环，不会再出现"切到佣兵回不了前台"。
    /// </summary>
    void OnTurnClicked()
    {
        if (_turnSwapCo != null) return;   // 上一次过场没播完，忽略连点
        if (boardCharacterImage == null)
        {
            Debug.LogError("[GuildHall] boardCharacterImage 未绑定（看板/show 节点缺失），无法切换看板");
            return;
        }

        // 【2026-10-26 主人拍板「优先只有女性职业出来 不要男性的」】轮换名单只收外观为女性的佣兵；
        // player 立绘是红发少年（男），不进池。前台小姐（空串）永远在首位，保证可切回。
        var roster = new List<string> { "" };   // 空串 = 前台小姐（预制体默认图）
        var unlocked = SaveSystem.Instance?.Data?.unlockedMercIds;
        if (unlocked != null)
        {
            var set = new HashSet<string>(unlocked);
            // 按 BoardFemaleIds 固定顺序收已拥有的女性佣兵，顺序可预期（"轮流"而非随机）
            foreach (string id in BoardFemaleIds)
                if (set.Contains(id)) roster.Add(id);
        }
        if (roster.Count <= 1) return;   // 只有前台（玩家无女性佣兵）时无需切换

        // 当前人物在名单中的位置：前台（_turnBoardId 为 null 或 ""）映射到索引 0，点击后前进到首位佣兵；
        // 当前是名单里的佣兵则取其索引；不在名单里（如老档残留）当作 -1，下一点回到前台重新同步。
        int cur;
        if (string.IsNullOrEmpty(_turnBoardId))
            cur = 0;
        else
        {
            cur = roster.IndexOf(_turnBoardId);
            if (cur < 0) cur = -1;
        }
        int next = (cur + 1) % roster.Count;
        string pick = roster[next];

        Sprite sp;
        string saveId;
        if (string.IsNullOrEmpty(pick))
        {
            // 回到前台小姐：复用捕获的预制体默认图（运行时不依赖资源路径）
            sp = _defaultBoardSprite;
            saveId = "";
        }
        else
        {
            sp = MercPortraitSprites.GetStand(pick) ?? MercPortraitSprites.GetHead(pick);
            if (sp == null)
            {
                Debug.LogError($"[GuildHall] 看板切换失败：{pick} 的立绘与头像都取不到（Resources/Icons/MercStand|MercHead/{pick}），保持当前人物");
                return;
            }
            saveId = pick;
        }
        if (sp == null)
        {
            Debug.LogError("[GuildHall] 看板切换失败：前台小姐默认图未捕获（_defaultBoardSprite 为空），保持当前人物");
            return;
        }
        _turnBoardId = pick;
        // 【2026-10-26 主人拍板「记忆上次替换的人物」】看板选择落存档（立即落盘，中途杀进程不丢），
        // 并让气泡轮播池切成本人物的台词（空串 = 前台御姐池）。
        var save = SaveSystem.Instance;
        if (save?.Data != null)
        {
            save.Data.lastBoardCharacterId = saveId;
            save.Save();
        }
        GetComponent<SpeechBubbleTalker>()?.RefreshLinePool(pick);
        _turnSwapCo = StartCoroutine(TurnSwapRoutine(sp));
    }

    /// <summary>
    /// 看板切换过场（主人口径 2026-10-16）：旧立绘向后变小（缩到基准的 turnSwapShrink 倍）并淡出，
    /// 换图后新立绘从同一倍数放大回基准并淡入。全程以底部为轴（人物原地退去/走回，不往中间缩），
    /// 与 PortraitIdleMotion 呼吸同款位移补偿公式。动画期间停看板呼吸，播完恢复（OnEnable 自动复位基准与相位）。
    /// </summary>
    IEnumerator TurnSwapRoutine(Sprite next)
    {
        var rt = boardCharacterImage.rectTransform;
        var cg = EnsureTurnSwapCanvasGroup();
        var motion = boardCharacterImage.GetComponent<PortraitIdleMotion>();
        if (motion != null) motion.enabled = false;   // 动画期间停呼吸，别和协程抢写 scale/位置

        Vector3 baseScale = rt.localScale;
        Vector2 basePos = rt.anchoredPosition;
        float h = Mathf.Abs(rt.rect.height);   // 底部为轴补偿用（与 PortraitIdleMotion 同款公式）
        float pivotY = rt.pivot.y;

        // ① 旧立绘：缩小到 shrink 倍 + 淡出（加速退去）
        float t = 0f;
        while (t < turnSwapOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.01f, turnSwapOutSeconds));
            float k = Mathf.Lerp(1f, turnSwapShrink, u * u);
            rt.localScale = baseScale * k;
            cg.alpha = 1f - u;
            rt.anchoredPosition = basePos + new Vector2(0f, h * pivotY * baseScale.y * (k - 1f));
            yield return null;
        }
        rt.localScale = baseScale * turnSwapShrink;
        cg.alpha = 0f;

        // ② 换图（SetBoardCharacter 里保比例 + 重采基准）
        SetBoardCharacter(next);

        // ③ 新立绘：从 shrink 倍放大回基准 + 淡入（减速落位）
        t = 0f;
        while (t < turnSwapInSeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.01f, turnSwapInSeconds));
            float ease = 1f - (1f - u) * (1f - u);
            float k = Mathf.Lerp(turnSwapShrink, 1f, ease);
            rt.localScale = baseScale * k;
            cg.alpha = ease;
            rt.anchoredPosition = basePos + new Vector2(0f, h * pivotY * baseScale.y * (k - 1f));
            yield return null;
        }
        rt.localScale = baseScale;
        cg.alpha = 1f;
        rt.anchoredPosition = basePos;

        if (motion != null) motion.enabled = true;   // 恢复呼吸（OnEnable 会重采基准、相位归零，无跳变）
        _turnSwapCo = null;
        // 【2026-10-26 主人拍板「只有换了才知道」】新人物上台开腔说专属台词（没有专属的人物保持安静）
        SpeechBubbleTalker.RevealBoardLine(_turnBoardId);
    }

    /// <summary>看板淡入淡出用的 CanvasGroup，挂 show 节点上，首次切换时补建（不改预制体）。</summary>
    CanvasGroup EnsureTurnSwapCanvasGroup()
    {
        var cg = boardCharacterImage.GetComponent<CanvasGroup>();
        return cg != null ? cg : boardCharacterImage.gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// 【预留接口】替换看板人物。后期接人物 / 时装数据后调用本方法即可：只换 sprite，不动节点尺寸与位置（满足「大小不用调」）。
    /// 人物来源、轮换 / 解锁等具体逻辑后期再说，本方法只负责把图贴到「看板/show」节点上。
    /// </summary>
    public void SetBoardCharacter(Sprite sprite)
    {
        if (boardCharacterImage == null)
        {
            Debug.LogError("[GuildHall] boardCharacterImage 未绑定（看板/show 节点缺失）");
            return;
        }
        boardCharacterImage.sprite = sprite;   // 仅替换人物图，rect / size 保持预制体原样
        boardCharacterImage.preserveAspect = true;   // 各佣兵立绘长宽比不同，保比例防拉变形（预制体 show 默认未开）
        boardCharacterImage.GetComponent<PortraitIdleMotion>()?.RefreshBase();   // 重采基准，呼吸基准随新图刷新
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
        // 2026-10-16 邮件 UI 改造（T8）：点信件按钮打开邮件列表窗（列表/详情/单领/一键领取/删除已读见 MailListUI / MailDetailUI）
        MailListUI.Show();
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
                staminaRegenText.text = StaminaSystem.FormatCountdown(StaminaSystem.SecondsToFull);
            }
        }
    }

    /// <summary>资源数字格式化【2026-10-24 提为公共静态唯一出口】：MercAscendUI 克隆顶条的金币/钻石数字同源走这里，不许抄第二份。
    /// 紧凑格式（数字+单位，省字符避免超框）：&lt;1万原样千分位；≥1万带「万」；≥1亿带「亿」。</summary>
    public static string FormatResource(long v)
    {
        if (v >= ResourceWallet.DEFAULT_MAX)
            return ResourceWallet.DEFAULT_MAX.ToString();
        if (v >= 100000000L)
            return (v / 100000000f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "亿";
        if (v >= 10000L)
            return (v / 10000f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "万";
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
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(parent, name);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 瘦屏：热点跟随背景底图等比放大（本界面专用，不进 UiLayoutStretch 通用规则）
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 需要跟随背景底图一起等比放大的热点节点名；以后加热点直接往这个数组里加即可。
    /// 这些节点在预制体里是「锚点居中 + 固定绝对偏移」，底图被 EnvelopeParent 放大后它们不会跟着走，
    /// 于是美术里的咨询台 / 气泡和热区框就错开了。
    /// Turn（切换看板人物按钮，主人 2026-10-16 加的）同为「锚点居中 + 固定绝对偏移」，2026-10-26 补进名单。
    /// </summary>
    static readonly string[] HotspotFollowNames = { "Receptionist", "SpeechBubble", "Turn" };

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
            // 挂了呼吸（PortraitIdleMotion）的热点（如 Turn）每帧按「自己的基准」改写 scale/pos；
            // 不重采基准的话，这里刚写入的 k 缩放下一帧就被呼吸组件按旧基准拉回去，两者打架。
            // RefreshBase 内部会立即用新基准重放一次呼吸，无跳变。
            rt.GetComponent<PortraitIdleMotion>()?.RefreshBase();
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
