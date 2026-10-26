using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆功能页：只做场景 + 2×2 功能入口。
/// 资源条、底部五入口一律复用主界面节点（TownSharedChrome），禁止自建。
/// 字体：中文 fusion-pixel，数字 PixelFont（经 GameFonts.ApplyToHierarchy）。
/// </summary>
public class TavernUI : MonoBehaviour, ITownPage
{
    public static TavernUI Instance { get; private set; }

    public MainNavTab Tab => MainNavTab.Tavern;

    [Header("功能入口")]
    public Button recruitButton;
    public Button trustButton;
    public Button questButton;
    public Button intelButton;

    [Header("布局")]
    public float bottomNavReserve = 150f;
    public float topBarReserve = 120f;

    [Header("立绘动态（可选，留空则自动找）")]
    public RectTransform portraitRoot;

    bool _built;
    bool _preloaded;
    bool _playWelcomeBack;

    /// <summary>酒馆闲话图标（主人手摆的「对话」按钮，TavernUI.prefab 右上角）。</summary>
    Button _gossipButton;

    /// <summary>
    /// 【2026-10-19 修复】进镇预热会「点亮一遍」本页（CoWarmupPages 逐页 ShowPage 再藏回），
    /// 置 true 后下一次 ShowPage 只建 UI、不发老板娘问候/欢迎彩蛋、不消耗当日问候——
    /// 问候只属于玩家真实进酒馆，否则人还在公会大厅就弹酒馆台词（主人报「又去裂隙了」跑出来）。
    /// </summary>
    public bool SuppressEggOnce;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>进 Town 时调用一次：建 UI、绑字体、Canvas 配置，然后隐藏待命</summary>
    public void PreloadOnce()
    {
        if (_preloaded) return;

        if (transform.Find("FeatureGrid") != null)
        {
            AutoBind();
            _built = true;
        }
        else if (!_built)
            BuildContentOnly();

        StripLocalChrome();
        EnsureVisibleTransform();
        ConfigureHostCanvasOnce();
        GameFonts.ApplyToHierarchy(transform);
        EnsurePortraitMotion();
        ApplyTavernSceneEnvelope();
        WireClicks();
        TavernUI.WarmGuildOverlayCache();

        _preloaded = true;
        gameObject.SetActive(false);
    }

    /// <summary>轻量显示：禁止 Resources.Load / Instantiate / 全树 ApplyToHierarchy</summary>
    public void ShowPage()
    {
        if (!_preloaded) PreloadOnce();
        EnsureVisibleTransform();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        // 【2026-10-25 主人拍板】酒馆主窗口不显示资源条：整条顶栏（含资源列与背景）收起；
        // 冒险/角色/天赋/日志页不受影响（它们仍走「只留资源列」），见 SetTopBarResourceOnly。
        SetGuildHallOverlayMode(true, fromTavernPage: true);
        TownPageDim.Ensure(transform);
        SoftenOpaqueHallCover();

        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);

        EnsurePortraitMotion();
        RefreshGossipButton();   // 【2026-10-26 信任交流】跨天进酒馆恢复闲话次数（置灰→可点）
        if (SuppressEggOnce)
        {
            // 预热点亮：只建 UI，问候/欢迎彩蛋全部跳过且不消耗（真实进店才发）
            SuppressEggOnce = false;
        }
        else if (_playWelcomeBack)
        {
            _playWelcomeBack = false;
            var tease = portraitRoot != null
                ? portraitRoot.GetComponent<TavernLandladyTease>()
                : null;
            if (tease != null)
                StartCoroutine(tease.CoWelcomeBackDrunk());
        }
        else
        {
            // 【2026-10-18 彩蛋批次】今日首次进酒馆：老板娘打招呼（深夜 0~5 点走专属台词）
            string greet = TavernEggState.ConsumeDailyGreeting();
            if (!string.IsNullOrEmpty(greet))
                UIManager.Instance?.ShowToast(greet, true);
        }
    }

    /// <summary>解禁后下一次进酒馆播「酒醒欢迎」。</summary>
    public void MarkWelcomeBack() => _playWelcomeBack = true;

    /// <summary>轻量隐藏</summary>
    public void HidePage()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        SetGuildHallOverlayMode(false);
    }

    public void Show()
    {
        // 2026-09-26 主人拍板：酒馆暂不开（佣兵功能调好再开）。开关在 GameConfig.TAVERN_ENABLED。
        if (!GameConfig.TAVERN_ENABLED)
        {
            GlobalToastUI.Show("酒馆暂未开放");
            return;
        }
        ShowPage();
    }
    public void Hide() => HidePage();

    /// <summary>预制体全屏不透明底会盖死大厅；改为透出 TownPageDim。</summary>
    void SoftenOpaqueHallCover()
    {
        string[] names = { "TavernBackground", "Background", "Bg", "bg" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = transform.Find(names[i]) ?? FindDeep(transform, names[i]);
            if (t == null) continue;
            // 内容区场景节点保留（TavernScene / portrait）
            if (t.name.IndexOf("Scene", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            var img = t.GetComponent<Image>();
            if (img == null) continue;
            var rt = img.rectTransform;
            var page = transform as RectTransform;
            bool fullBleed = page != null && page.rect.height > 1f
                && rt.rect.height / page.rect.height > 0.8f;
            if (!fullBleed && t.parent != transform) continue;
            // 关掉不透明全屏底，大厅 + Dim 可见
            img.enabled = false;
            t.gameObject.SetActive(false);
        }
    }

    /// <summary>预制体/编辑器里 scale 可能被存成 0，导致「酒馆空白」</summary>
    void EnsureVisibleTransform()
    {
        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 嵌在公会下时去掉独立 Canvas，走父 Canvas；保留 scale=1。
    /// </summary>
    bool _canvasConfigured;

    void ConfigureHostCanvasOnce()
    {
        if (_canvasConfigured) return;
        EnsureVisibleTransform();
        TownPageCanvas.Configure(gameObject, 5, stripCanvasWhenNested: true);
        _canvasConfigured = true;
    }

    /// <summary>
    /// 功能页以大厅为底 + 半透罩，不再整栏藏大厅装饰（立绘/场景底要露出来）。
    /// 仅关易误点热点；顶栏仍走 SetTopBarResourceOnly。
    /// </summary>
    static readonly string[] GuildHideWhenTavern = { };

    static Transform[] _guildHideCache;
    static Button[] _guildHotspotCache;

    public static void WarmGuildOverlayCache()
    {
        if (GuildHallUI.Instance != null)
            EnsureGuildHideCache(GuildHallUI.Instance);
    }

    public static void SetGuildHallOverlayMode(bool tavernOpen, bool fromTavernPage = false)
    {
        var hall = GuildHallUI.Instance;
        if (hall == null) return;

        EnsureGuildHideCache(hall);
        bool show = !tavernOpen;

        if (_guildHideCache != null)
        {
            for (int i = 0; i < _guildHideCache.Length; i++)
            {
                Transform t = _guildHideCache[i];
                if (t == null) continue;
                if (t.gameObject.activeSelf != show)
                    t.gameObject.SetActive(show);
            }
        }

        SetTopBarResourceOnly(!show, fromTavernPage);

        if (_guildHotspotCache != null)
        {
            for (int i = 0; i < _guildHotspotCache.Length; i++)
            {
                Button b = _guildHotspotCache[i];
                if (b == null) continue;
                if (b.gameObject.activeSelf != show)
                    b.gameObject.SetActive(show);
            }
        }

        // 切回公会：再次隐藏未完成入口（热点缓存会把公告栏/武器库/执照厅点亮）
        if (show)
            hall.HideUnfinishedHallButtons();
    }

    static void EnsureGuildHideCache(GuildHallUI hall)
    {
        // 名单变更后重建，避免旧缓存仍含 Mail/Shop 等并在切回时点亮
        if (_guildHideCache != null && _guildHideCache.Length != GuildHideWhenTavern.Length)
            _guildHideCache = null;
        if (_guildHideCache != null) return;

        var nodes = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < GuildHideWhenTavern.Length; i++)
        {
            Transform t = FindDeep(hall.transform, GuildHideWhenTavern[i]);
            if (t != null) nodes.Add(t);
        }
        _guildHideCache = nodes.ToArray();

        _guildHotspotCache = new[]
        {
            hall.noticeBoardButton,
            hall.licenseHallButton,
            hall.armoryButton,
            hall.receptionistButton
        };
    }

    /// <summary>功能页只留金币/体力资源条，隐藏公会名等主界面顶栏装饰。
    /// 【2026-10-25 主人拍板】fromTavernPage 且 resourceOnly 时（酒馆主窗口）：资源条也不显示，
    /// 整条 TopBar 收起（含 ResourceStrip / 体力Panel / TopBarBg）。收整条而非只藏资源列，
    /// 是因为酒馆里开招募弹窗借走 ResourceStrip、关窗 Release 归还主界面时会重新 SetActive(true)，
    /// 只藏列会被它点亮复发；TopBar 整体inactive 时归还也看不见，切走时统一在这里还原。</summary>
    static void SetTopBarResourceOnly(bool resourceOnly, bool fromTavernPage = false)
    {
        var hall = GuildHallUI.Instance;
        if (hall == null) return;
        Transform top = TownSharedChrome.FindDeep(hall.transform, "TopBar");
        if (top == null) return;

        bool barOn = !resourceOnly || !fromTavernPage;
        if (top.gameObject.activeSelf != barOn)
            top.gameObject.SetActive(barOn);
        if (!barOn) return;

        for (int i = 0; i < top.childCount; i++)
        {
            Transform c = top.GetChild(i);
            if (c == null) continue;
            if (c.name == "GoldPanel" || c.name == "体力Panel") continue;
            // 【2026-10-24 主人拍板「提取顶条资源条」】金币/钻石列已迁入 ResourceStrip 全局唯一条
            // （原生节点迁移非复制，主界面顶条显示的就是它），功能页照旧保留资源列显隐。
            if (c.name == "ResourceStrip") continue;
            if (c.name.IndexOf("Stamina", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (c.name == "TopBarBg") continue;
            bool on = !resourceOnly;
            if (c.gameObject.activeSelf != on)
                c.gameObject.SetActive(on);
        }
    }

    public static void ClearGuildHideCache()
    {
        _guildHideCache = null;
        _guildHotspotCache = null;
    }

    /// <summary>兼容旧调用：资源条在主界面，无需酒馆再刷</summary>
    public void RefreshGold()
    {
        // 故意不回调 RefreshAllHudStatic，避免与 GuildHallUI 互相递归
    }

    void StripLocalChrome()
    {
        // 历史运行时/错误预制体可能自带 TopBar，统一移除
        DestroyChildNamed(transform, "TopBar");
        DestroyChildNamed(transform, "SharedResourceBar");

        Transform localNav = transform.Find("BottomNav");
        if (localNav != null && localNav.IsChildOf(transform)
            && localNav.GetComponentInParent<GuildHallUI>() == null)
            Destroy(localNav.gameObject);
    }

    static void DestroyChildNamed(Transform root, string name)
    {
        Transform t = root.Find(name);
        if (t != null) Destroy(t.gameObject);
    }

    void WireClicks()
    {
        ApplyFeatureCardLayout();
        if (recruitButton != null)
        {
            // 【2026-10-17 主人拍板】开启佣兵招募入口：下排「佣兵招募」卡直接打开三选一招募弹窗。
            // （2026-10-15 的锁卡口径作废；三选一弹窗 = MercenaryRecruitPopupUI，含金币/招募卷双支付、
            //   手动刷新 30 分钟冷却、每日 0 点自动刷新）
            recruitButton.onClick.RemoveAllListeners();
            recruitButton.gameObject.SetActive(true);
            recruitButton.onClick.AddListener(() => MercenaryRecruitPopupUI.Show());
        }
        if (trustButton != null)
        {
            // 【2026-10-26 主人拍板】右下卡改「佣兵羁绊」＝佣兵好感界面入口（MercFavorUI，
            // 预制体 Resources/Prefabs/Town/佣兵好感：对话/送礼攒好感，满值解锁羁绊地图）。
            // 原「信任交流卡复用为酒馆闲话」口径作废——闲话已单独挪到场景「对话」按钮
            // （本文件下方 gossipBtn 接线，TavernChatPopupUI），本卡只开好感界面。
            trustButton.gameObject.SetActive(true);
            trustButton.onClick.RemoveAllListeners();
            trustButton.onClick.AddListener(() => MercFavorUI.Open());
            RetitleCard(trustButton.transform, "佣兵羁绊", "对话送礼攒好感，解锁羁绊地图");
        }
        if (questButton != null)
        {
            // 【2026-10-13 主人拍板】原隐藏的「酒馆任务」卡位复用为「佣兵抽奖池」入口：
            // 勾选下场想抽到的佣兵，局内招募只从勾选的里面出（空池 = 不筛选）。
            // 【2026-10-17 主人拍板】改名「佣兵雇佣队列」，与酒馆「佣兵招募」（三选一抽全池）错开叫法。
            questButton.gameObject.SetActive(true);
            questButton.onClick.RemoveAllListeners();
            questButton.onClick.AddListener(() => MercDrawPoolUI.Show());
            RetitleCard(questButton.transform, "佣兵队列", "配置下场战斗可雇佣的佣兵");   // 2026-10-18 主人拍板改名
        }
        if (intelButton != null)
        {
            intelButton.gameObject.SetActive(true);
            intelButton.onClick.RemoveAllListeners();
            intelButton.onClick.AddListener(() => InformantIntelDirector.StartDaily());
        }

        // 【2026-10-26 主人拍板「信任交流」】主人手摆的「对话」按钮（TavernUI.prefab 右上角）＝酒馆闲话图标
        // （设计文档 §2.9）：每日 3 次，点击 = 随机已拥有佣兵 +1 信任 + TavernLore 剧情补充；用完置灰。
        var gossipBtn = FindButton("对话");
        if (gossipBtn != null)
        {
            _gossipButton = gossipBtn;
            gossipBtn.onClick.RemoveAllListeners();
            gossipBtn.onClick.AddListener(OnGossipClicked);
            RefreshGossipButton();
        }
    }

    /// <summary>
    /// 功能卡对调（2026-10-18 主人拍板）：上排 佣兵招募 | 佣兵队列，下排 佣兵情报 | 信任交流(未开放)。
    /// 不改预制体：按现场四卡的 x/y 分布识别「上左/上右/下左/下右」四个槽位，再按目标布局落位
    /// （预制体与代码兜底的初始摆位不同，按分布识别两种都成立；已在目标位的卡赋同值无副作用）。
    /// </summary>
    void ApplyFeatureCardLayout()
    {
        var r = recruitButton != null ? recruitButton.transform as RectTransform : null;
        var q = questButton != null ? questButton.transform as RectTransform : null;
        var i = intelButton != null ? intelButton.transform as RectTransform : null;
        var t = trustButton != null ? trustButton.transform as RectTransform : null;
        if (r == null || q == null || i == null || t == null) return;

        var cards = new[] { r, q, i, t };
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int k = 0; k < cards.Length; k++)
        {
            var p = cards[k].anchoredPosition;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        float midX = (minX + maxX) * 0.5f;
        float midY = (minY + maxY) * 0.5f;

        Vector2 topLeft = Vector2.zero, topRight = Vector2.zero, botLeft = Vector2.zero, botRight = Vector2.zero;
        for (int k = 0; k < cards.Length; k++)
        {
            var p = cards[k].anchoredPosition;
            bool left = p.x < midX;
            bool top = p.y >= midY;
            if (left && top) topLeft = p;
            else if (!left && top) topRight = p;
            else if (left) botLeft = p;
            else botRight = p;
        }

        r.anchoredPosition = topLeft;     // 佣兵招募 → 上左
        q.anchoredPosition = topRight;    // 佣兵队列 → 上右
        i.anchoredPosition = botLeft;     // 佣兵情报 → 下左
        t.anchoredPosition = botRight;    // 信任交流 → 下右
    }

    static void RetitleCard(Transform card, string title, string desc)
    {
        if (card == null) return;
        var t = card.Find("Title");
        var titleText = t != null ? t.GetComponent<Text>() : null;
        if (titleText != null) titleText.text = title;
        var d = card.Find("Desc");
        var descText = d != null ? d.GetComponent<Text>() : null;
        if (descText != null) descText.text = desc;
    }

    static void Wire(Button btn, string toast)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => UIManager.Instance?.ShowToast(toast));
    }

    // ———— 酒馆闲话（2026-10-26 主人拍板「信任交流」）————
    // 点「对话」按钮 → 播一段酒馆闲话剧情（复用 StoryDirector，与主线/战后同一套弹窗表现），
    // 末拍三选一话题（选项原生支持，见 StoryBeat.choices），选完 = 随机已拥有佣兵 +1 信任 + 剧情补充。每日 3 次。

    /// <summary>点「对话」按钮：起闲话剧情。</summary>
    void OnGossipClicked()
    {
        // 剧情正在播不重入（与 TavernChatPopupUI 同款拦截，防叠窗）
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;

        if (string.IsNullOrEmpty(PickRandomOwnedMerc(data)))
        {
            UIManager.Instance?.ShowToast("酒馆里还没有能聊天的佣兵，先去招募一位吧。");
            return;
        }
        if (data.TavernGossipLeft() <= 0)
        {
            UIManager.Instance?.ShowToast("今天的闲话聊完了，明天再来！");
            RefreshGossipButton();
            return;
        }

        var beats = new List<StoryBeat>
        {
            new StoryBeat
            {
                rightName = "老板娘", rightPortraitId = StoryPortraits.Innkeeper, speaker = 1,
                text = "哟，坐下聊！今天想听点什么？",
            },
            new StoryBeat
            {
                rightName = "老板娘", rightPortraitId = StoryPortraits.Innkeeper, speaker = 1,
                text = TavernLore.InnkeeperLine(),
            },
            new StoryBeat
            {
                speaker = 0,
                text = "聊点什么？",
                choices = new[] { "问问佣兵们的近况", "听听裂隙里的传闻", "安静地喝一杯" },
            },
        };
        StoryDirector.Ensure().Play(beats, null, picked => OnGossipPicked(data, picked));
    }

    /// <summary>
    /// 选项回调：扣当日次数 + 随机已拥有佣兵 +1 信任 + 按话题给剧情补充。
    /// 玩家中途跳过（没走到选项拍）不扣次数、不给好感——次数是好感额度，看白片不耗。
    /// </summary>
    void OnGossipPicked(SaveData data, int picked)
    {
        if (!data.ConsumeTavernGossip())
        {
            UIManager.Instance?.ShowToast("今天的闲话聊完了，明天再来！");
            RefreshGossipButton();
            return;
        }

        string hireId = PickRandomOwnedMerc(data);
        int after = data.AddMercTrust(hireId, 1);
        string name = hireId;
        if (MercRosterDefs.TryGetByHireId(hireId, out var def) && !string.IsNullOrEmpty(def.Name))
            name = def.Name;

        string extra;
        switch (picked)
        {
            case 0: // 佣兵近况 → 老队员的记忆
            {
                string q = TavernLore.SquadQuote();
                extra = string.IsNullOrEmpty(q) ? TavernLore.InnkeeperLine() : q;
                break;
            }
            case 1: // 裂隙传闻 → 已解锁佣兵侧写
            {
                string lore = TavernLore.MercLoreLine();
                extra = string.IsNullOrEmpty(lore) ? TavernLore.InnkeeperLine() : lore;
                break;
            }
            default: // 安静喝一杯 → 空座位（照走 NoteSeatViewed，C26 链路保留：第 5 次查看发奖励）
            {
                var stage = TavernLore.ResolveSeatStage();
                string prop = TavernLore.SeatProp(stage);
                string text = TavernLore.SeatText(stage);
                TavernLore.NoteSeatViewed();
                extra = string.IsNullOrEmpty(prop) ? text : prop + "\n" + text;
                break;
            }
        }

        UIManager.Instance?.ShowToast(extra + $"\n（{name} 的好感 +1，当前 {after}）", true);
        RefreshGossipButton();
        SaveSystem.Instance?.Save();   // 选完就落盘，中途杀进程别白嫖
    }

    /// <summary>随机一名已拥有佣兵（CopyTo 拷贝再抽，避免遍历中改集合）。</summary>
    static string PickRandomOwnedMerc(SaveData data)
    {
        if (data.unlockedMercIds == null || data.unlockedMercIds.Count == 0) return null;
        string[] pool = new string[data.unlockedMercIds.Count];
        data.unlockedMercIds.CopyTo(pool);
        return pool[Random.Range(0, pool.Length)];
    }

    /// <summary>今日次数用完置灰（只动 interactable，不改颜色不动视觉）；跨天 ShowPage 时恢复。</summary>
    void RefreshGossipButton()
    {
        if (_gossipButton == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        bool hasLeft = data != null && data.TavernGossipLeft() > 0;
        if (_gossipButton.interactable != hasLeft)
            _gossipButton.interactable = hasLeft;
    }

    void AutoBind()
    {
        if (recruitButton == null) recruitButton = FindButton("Recruit");
        if (trustButton == null) trustButton = FindButton("Trust");
        if (questButton == null) questButton = FindButton("Quest");
        if (intelButton == null) intelButton = FindButton("Intel");
    }

    Button FindButton(string name)
    {
        var t = FindDeep(transform, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    /// <summary>只搭建酒馆内容区，不含资源条/底栏</summary>
    public void BuildIfNeeded() => BuildContentOnly();

    void BuildContentOnly()
    {
        if (transform.Find("FeatureGrid") != null)
        {
            AutoBind();
            _built = true;
            return;
        }

        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        Stretch(rt);

        var bg = CreateImage(transform, "TavernBackground", new Color(0.22f, 0.12f, 0.08f, 1f));
        Stretch(bg.rectTransform);

        var scene = CreateImage(transform, "TavernScene", new Color(0.45f, 0.28f, 0.18f, 0.55f));
        Stretch(scene.rectTransform);
        scene.rectTransform.offsetMin = new Vector2(0f, bottomNavReserve);
        scene.rectTransform.offsetMax = new Vector2(0f, -topBarReserve);
        var hint = CreateText(scene.transform, "SceneHint", "【酒馆场景插画槽】", 22, new Color(1f, 1f, 1f, 0.35f), number: false);
        Stretch(hint.rectTransform);

        var grid = CreateRect(transform, "FeatureGrid");
        SetAnchored(grid, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, bottomNavReserve + 210f), new Vector2(640f, 360f));

        recruitButton = CreateFeatureCard(grid.transform, "Recruit", "佣兵招募", "解锁佣兵，让其进入战斗三选一池",
            new Vector2(-160f, 85f), new Color(0.55f, 0.42f, 0.28f));   // 2026-10-18 主人拍板：标题与预制体统一叫「佣兵招募」
        trustButton = CreateFeatureCard(grid.transform, "Trust", "信任交流", "提升信任解锁故事与事件",
            new Vector2(160f, 85f), new Color(0.65f, 0.32f, 0.35f));
        questButton = CreateFeatureCard(grid.transform, "Quest", "佣兵队列", "配置下场战斗可雇佣的佣兵",
            new Vector2(-160f, -95f), new Color(0.4f, 0.45f, 0.55f));   // 2026-10-18 主人拍板改名；位置由 ApplyFeatureCardLayout 统一对调
        intelButton = CreateFeatureCard(grid.transform, "Intel", "佣兵情报", "查看佣兵资料与背景故事",
            new Vector2(160f, -95f), new Color(0.35f, 0.4f, 0.5f));

        _built = true;
        GameFonts.ApplyToHierarchy(transform);
    }

    Button CreateFeatureCard(Transform parent, string name, string title, string desc, Vector2 pos, Color accent)
    {
        var go = CreateRect(parent, name);
        SetAnchored(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(300f, 150f));

        var panel = CreateImage(go.transform, "CardBg", new Color(0.08f, 0.06f, 0.05f, 0.78f));
        Stretch(panel.rectTransform);

        var border = CreateImage(go.transform, "Border", new Color(accent.r, accent.g, accent.b, 0.55f));
        Stretch(border.rectTransform);
        border.rectTransform.offsetMin = new Vector2(2f, 2f);
        border.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var inner = CreateImage(go.transform, "Inner", new Color(0.1f, 0.07f, 0.05f, 0.92f));
        Stretch(inner.rectTransform);
        inner.rectTransform.offsetMin = new Vector2(5f, 5f);
        inner.rectTransform.offsetMax = new Vector2(-5f, -5f);

        var icon = CreateImage(go.transform, "Icon", accent);
        SetAnchored(icon.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 8f), new Vector2(64f, 64f));

        var titleT = CreateText(go.transform, "Title", title, 26, new Color(1f, 0.92f, 0.75f), number: false);
        titleT.fontStyle = FontStyle.Bold;
        titleT.alignment = TextAnchor.MiddleLeft;
        var titleRt = titleT.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(100f, -48f);
        titleRt.offsetMax = new Vector2(-16f, -10f);

        var descT = CreateText(go.transform, "Desc", desc, 16, new Color(0.85f, 0.8f, 0.7f, 0.9f), number: false);
        var descRt = descT.rectTransform;
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.offsetMin = new Vector2(100f, 14f);
        descRt.offsetMax = new Vector2(-16f, -52f);
        descT.alignment = TextAnchor.UpperLeft;
        descT.horizontalOverflow = HorizontalWrapMode.Wrap;
        descT.verticalOverflow = VerticalWrapMode.Truncate;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = panel;
        return btn;
    }

    static GameObject CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = CreateRect(parent, name);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color, bool number)
    {
        var go = CreateRect(parent, name);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.font = number ? GameFonts.GetNumber() : GameFonts.GetChinese();
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAnchored(GameObject go, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    void EnsurePortraitMotion()
    {
        RectTransform target = portraitRoot != null ? portraitRoot : ResolvePortraitRoot();
        if (target == null) return;

        if (portraitRoot == null)
            portraitRoot = target;

        PortraitIdleMotion.EnsureOn(target, 0f, tavernSway: true);
        TavernLandladyTease.EnsureOn(target);
    }

    RectTransform ResolvePortraitRoot()
    {
        string[] names = { "人物", "Portrait", "Character", "TavernPortrait", "立绘" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeep(transform, names[i]);
            if (t != null)
                return t as RectTransform ?? t.GetComponent<RectTransform>();
        }

        Transform scene = FindDeep(transform, "TavernScene");
        if (scene == null) return null;

        var images = scene.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null || img.sprite == null) continue;
            string n = img.gameObject.name;
            if (n == "SceneHint" || n == "bg" || n == "TavernBackground") continue;
            return img.rectTransform;
        }

        return null;
    }

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

    /// <summary>酒馆底图（TavernScene，~800×1424 中心锚定）运行时 envelope 铺满，覆盖更瘦屏上下空区。</summary>
    void ApplyTavernSceneEnvelope()
    {
        Transform scene = transform.Find("TavernScene") ?? FindDeep(transform, "TavernScene");
        if (scene == null) return;
        var rt = scene as RectTransform;
        if (rt == null) return;
        UiLayoutStretch.ApplyEnvelopeImage(rt);
    }
}
