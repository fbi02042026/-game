using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面「当前目标」条（2026-09-19 新增，同日改版）。
///
/// 目的：玩家一进游戏就知道「现在该干嘛」（目标显化），点一下直达功能（降低操作成本）
/// —— 两件事都直接服务「在线时长 ↑ / 流失 ↓」。
///
/// 位置（改版重点）：**屏幕顶部**，主界面 TopBar（金币/体力条）下方，一进游戏就在视野里。
///   不再放底部导航上方——那里是「功能入口区」，玩家进游戏不会先看那里。
///   顶部让位 = max(刘海/状态栏安全区, TopBar 实测底边)，都量不到就按 120 的经验值下移。
///
/// 交互（改版重点）：整条 = 跳转（<see cref="QuestJumpRouter"/> 直达对应玩法）；
///   右侧「全部」按钮 = 展开 <see cref="QuestPanelUI"/> 看全部任务。
///   跳转拿不到目标界面 → 回退展开面板 + 一行提示，绝不让点击没反应。
///
/// 实现约定：全部运行时建树，不碰 prefab；自带 Canvas（sort 260）：
/// 高于大厅与切页遮罩，低于弹窗（900），所以弹窗打开时被盖住是正常的。
/// </summary>
public class QuestHudBar : MonoBehaviour
{
    public static QuestHudBar Instance { get; private set; }

    const int SortOrder = 260;
    const float BarHeight = 96f;
    /// <summary>宽度占屏宽 92%（左右各留 4%），水平居中。</summary>
    const float SideInset = 0.04f;
    /// <summary>TopBar 高度经验值：量不到实测值时兜底（与 TownSharedChrome.StretchTop 的 120 一致）。</summary>
    const float TopBarReserve = 120f;
    /// <summary>与 TopBar 之间留的呼吸空隙。</summary>
    const float GapUnderTopBar = 10f;
    /// <summary>右侧「全部」按钮占的宽度（跳转点击层给它让位，避免两个按钮重叠冒泡）。</summary>
    const float PanelBtnWidth = 92f;
    const float RefreshInterval = 0.5f;
    /// <summary>收缩胶囊：宽约 220，高 44，贴右侧。</summary>
    const float CapsuleWidth = 220f;
    const float CapsuleHeight = 44f;
    /// <summary>无操作多少秒后收缩（用 unscaledDeltaTime 累加，不接 Input）。</summary>
    const float IdleTimeout = 3f;
    /// <summary>任务完成态保持时长（不阻塞 0.5s 轮询）。</summary>
    const float CelebrateTime = 0.8f;
    /// <summary>两行之间的缝隙。</summary>
    const float RowGap = 4f;

    /// <summary>整条根节点（展开/收缩都在这一个对象上切换尺寸）。</summary>
    GameObject _bar;
    Image _bg;
    /// <summary>收缩成胶囊时的背景、文案、热区（展开时隐藏）。</summary>
    Image _capsuleBg;
    Text _capsuleLabel;
    Text _capsuleProgress;
    Button _capsuleHit;
    /// <summary>右侧「全部」按钮（展开面板）。</summary>
    Button _panelBtn;
    /// <summary>章节进度小徽章（3/10），独立于任务进度（①）。</summary>
    Text _chapterBadge;
    /// <summary>首次气泡 + 全屏点击捕捉（点任意处消失，只一次，⑥）。</summary>
    GameObject _bubble;
    Button _bubbleCatcher;

    RowUi _mainRow = new RowUi();
    RowUi _dailyRow = new RowUi();

    float _timer;
    bool _layoutDone;

    /// <summary>条上当前指向的任务：跳转时用（不进存档，纯 UI 态）。</summary>
    MainQuestDef _mainDef;
    DailyQuestView _daily;
    bool _hasDaily;
    bool _showingMain = true;

    // —— ④ 无操作收缩 ——
    float _idleTimer;
    bool _collapsed;

    // —— ⑧ 完成态庆祝 ——
    bool _celebrating;
    float _celebrateUntil;
    string _celebrateDesc;
    string _celebrateReward;
    string _celebrateTag;
    Color _celebrateTagColor;
    bool _celebrateIsMain;

    // —— 完成检测：上一帧展示的任务 id / def ——
    string _shownMainId;
    MainQuestDef _shownMainDef;
    string _shownDailyId;

    /// <summary>首次气泡是否已出现过（同会话内只弹一次，跨会话靠 PlayerPrefs）。</summary>
    bool _bubbleShown;

    enum RowKind { Main, Daily }

    /// <summary>单行 UI 引用集合（主线行 / 每日行共用一套结构）。</summary>
    class RowUi
    {
        public GameObject root;
        public Image tagBg;
        public Text tagText;
        public Text descText;
        public Text progressText;
        public Text rewardText;
        public Button hit;
        public Action onClick;
    }

    /// <summary>城镇启动完成后调用（重复调用安全）。</summary>
    public static QuestHudBar Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("QuestHudBar", typeof(RectTransform));
        DontDestroyOnLoad(go);
        var ui = go.AddComponent<QuestHudBar>();
        ui.Build();
        return ui;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoaded()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (GameSceneGate.IsTown) Ensure();
        };
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        MainQuestSystem.OnChanged -= OnQuestChanged;
        MainQuestSystem.OnChanged += OnQuestChanged;
    }

    void OnDestroy()
    {
        MainQuestSystem.OnChanged -= OnQuestChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>任务状态一变就下次 Update 立刻刷新，不等 0.5s。</summary>
    void OnQuestChanged()
    {
        _timer = 0f;
        _idleTimer = 0f;          // ④ 有新变化时重置无操作计时
        _collapsed = false;       // ④ 新任务完成自动展开一次
        _layoutDone = false;      // 展开需要重新摆位
    }

    public void RefreshNow()
    {
        Layout();
        Refresh();
    }

    // ============================================================
    // 建树
    // ============================================================

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);

        _bar = new GameObject("Bar", typeof(RectTransform));
        _bar.transform.SetParent(transform, false);

        _bg = CreateImg(_bar.transform, "Bg", new Color(0.08f, 0.07f, 0.11f, 0.72f));
        Stretch(_bg.rectTransform);
        _bg.raycastTarget = false;   // 底衬不吃点击，点击交给下面每行按钮

        // ③ 双条：主线一行（上）+ 每日一行（下），各自独立可点
        BuildRow(_mainRow, "MainRow", 0.5f, 1f);
        BuildRow(_dailyRow, "DailyRow", 0f, 0.5f);

        // ① 章节进度小徽章（独立于任务进度），贴右侧、整条居中
        _chapterBadge = CreateTxt(_bg.transform, "ChapterBadge", "", 16, TextAnchor.MiddleCenter);
        SetAnchors(_chapterBadge.rectTransform, 1f, 0.5f, 1f, 0.5f, -175f, -16f, -115f, 16f);
        _chapterBadge.color = new Color(0.8f, 0.8f, 0.86f);

        // 右侧「全部」按钮 = 展开面板
        var panelImg = CreateImg(_bg.transform, "AllBtn", new Color(0.28f, 0.25f, 0.34f, 0.95f));
        var prt = panelImg.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
        prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(-12f, 0f);
        prt.sizeDelta = new Vector2(PanelBtnWidth, 44f);
        _panelBtn = panelImg.gameObject.AddComponent<Button>();
        _panelBtn.onClick.AddListener(OnClickPanel);
        var panelTxt = CreateTxt(panelImg.transform, "Label", "全部", 18, TextAnchor.MiddleCenter);
        Stretch(panelTxt.rectTransform);
        panelTxt.color = new Color(1f, 0.86f, 0.5f);

        BuildCapsule();
        BuildBubble();

        _bar.SetActive(false);
        GameFonts.ApplyToHierarchy(transform);

        // P2-5 SafeArea 收敛：
        // 现有手搓 SafeAreaTopUnits 只「测量」刘海高度并下推 _bar.anchoredPosition（子节点定位），
        // 不修改根 RectTransform 的 anchor/offset，因此与新组件不会重复叠加。
        // 但本组件挂在 Canvas 根上（transform 是带 Canvas 的 GameObject，parent==null），
        // 没有父级 RectTransform 可换算内缩，SafeAreaFitter 实际是惰性 no-op；
        // 顶部已由 _bar 自身逻辑兜住。为绝对零回归，默认 enabledFit=false。
        // 待真机验证 SafeAreaFitter 在 Canvas 根上无副作用后，可改 enabledFit=true。
        if (GetComponent<SafeAreaFitter>() == null)
        {
            var qhbSafe = gameObject.AddComponent<SafeAreaFitter>();
            qhbSafe.edge = SafeAreaFitter.Edge.Top;
            qhbSafe.enabledFit = false;
        }
    }

    /// <summary>建一行（标签 + 描述 + 进度 + 奖励 + 整行热区）。minY/maxY 控制上下半行。</summary>
    void BuildRow(RowUi row, string name, float minY, float maxY)
    {
        row.root = new GameObject(name, typeof(RectTransform));
        row.root.transform.SetParent(_bg.transform, false);
        var rt = row.root.transform as RectTransform;
        rt.anchorMin = new Vector2(0f, minY);
        rt.anchorMax = new Vector2(1f, maxY);
        rt.offsetMin = new Vector2(0f, RowGap * 0.5f);
        rt.offsetMax = new Vector2(0f, -RowGap * 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        row.tagBg = CreateImg(row.root.transform, "Tag", new Color(0.35f, 0.24f, 0.10f, 1f));
        SetAnchors(row.tagBg.rectTransform, 0f, 0.5f, 0f, 0.5f, 8f, -16f, 66f, 16f);
        row.tagText = CreateTxt(row.tagBg.transform, "TagText", "", 18, TextAnchor.MiddleCenter);
        Stretch(row.tagText.rectTransform);
        row.tagText.color = new Color(1f, 0.86f, 0.5f);

        row.descText = CreateTxt(row.root.transform, "Desc", "", 22, TextAnchor.MiddleLeft);
        SetAnchors(row.descText.rectTransform, 0f, 0f, 1f, 1f, 72f, 4f, -350f, -4f);
        row.descText.color = new Color(0.94f, 0.94f, 0.97f);
        row.descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        row.descText.verticalOverflow = VerticalWrapMode.Truncate;

        // 右：任务当前进度（①）
        row.progressText = CreateTxt(row.root.transform, "Progress", "", 20, TextAnchor.MiddleRight);
        SetAnchors(row.progressText.rectTransform, 1f, 0f, 1f, 1f, -255f, 4f, -175f, -4f);
        row.progressText.color = new Color(1f, 0.86f, 0.5f);

        // 右：奖励标签（②）
        row.rewardText = CreateTxt(row.root.transform, "Reward", "", 16, TextAnchor.MiddleRight);
        SetAnchors(row.rewardText.rectTransform, 1f, 0f, 1f, 1f, -345f, 4f, -265f, -4f);
        row.rewardText.color = new Color(1f, 0.8f, 0.4f);

        // 整行热区（每行一个，互不包含）
        row.hit = CreateHitBtn(row.root.transform, "Hit", 0f, 0f);
        var rid = row;   // 捕获本行实例
        row.hit.onClick.AddListener(() => { if (rid.onClick != null) rid.onClick(); });
    }

    /// <summary>④ 收缩胶囊：只留标签 + 进度，点击展开回完整条。</summary>
    void BuildCapsule()
    {
        _capsuleBg = CreateImg(_bar.transform, "CapsuleBg", new Color(0.08f, 0.07f, 0.11f, 0.85f));
        Stretch(_capsuleBg.rectTransform);
        _capsuleBg.raycastTarget = false;
        _capsuleLabel = CreateTxt(_capsuleBg.transform, "CapLabel", "", 18, TextAnchor.MiddleLeft);
        SetAnchors(_capsuleLabel.rectTransform, 0f, 0f, 1f, 1f, 12f, 4f, -80f, -4f);
        _capsuleLabel.color = new Color(0.94f, 0.94f, 0.97f);
        _capsuleProgress = CreateTxt(_capsuleBg.transform, "CapProg", "", 18, TextAnchor.MiddleRight);
        SetAnchors(_capsuleProgress.rectTransform, 1f, 0f, 1f, 1f, -72f, 4f, -12f, -4f);
        _capsuleProgress.color = new Color(1f, 0.86f, 0.5f);
        _capsuleHit = CreateHitBtn(_capsuleBg.transform, "CapHit", 0f, 0f);
        _capsuleHit.onClick.AddListener(OnClickCapsule);
        _capsuleBg.gameObject.SetActive(false);
    }

    /// <summary>⑥ 首次气泡 + 全屏点击捕捉（点任意处消失，只一次）。</summary>
    void BuildBubble()
    {
        _bubbleCatcher = CreateHitBtn(transform, "BubbleCatcher", 0f, 0f);
        _bubbleCatcher.gameObject.SetActive(false);
        _bubbleCatcher.onClick.AddListener(DismissBubble);

        _bubble = new GameObject("Bubble", typeof(RectTransform));
        _bubble.transform.SetParent(transform, false);
        var bimg = CreateImg(_bubble.transform, "BubbleBg", new Color(0.2f, 0.18f, 0.26f, 0.98f));
        Stretch(bimg.rectTransform);
        bimg.raycastTarget = false;   // 让点击穿透到 catcher
        var btxt = CreateTxt(_bubble.transform, "BubbleTxt", "点这里直接过去", 18, TextAnchor.MiddleCenter);
        Stretch(btxt.rectTransform);
        btxt.color = Color.white;
        var brt = _bubble.transform as RectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -(BarHeight + 8f));
        brt.sizeDelta = new Vector2(200f, 40f);
        _bubble.SetActive(false);
    }

    /// <summary>
    /// 把横条贴到**屏幕顶部**（TopBar 下方）：
    /// 让位距离取「刘海安全区」与「TopBar 实测底边」的较大者，量不到就退回经验值。
    /// </summary>
    void Layout()
    {
        if (_bar == null) return;

        var rt = _bar.transform as RectTransform;
        if (rt == null) return;

        float top = TopOffset();
        if (_collapsed)
        {
            // ④ 收缩：右侧小胶囊（只留标签 + 进度）
            rt.anchorMin = rt.anchorMax = new Vector2(1f - SideInset, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(CapsuleWidth, CapsuleHeight);
            _bg.gameObject.SetActive(false);
            _capsuleBg.gameObject.SetActive(true);
        }
        else
        {
            rt.anchorMin = new Vector2(SideInset, 1f);
            rt.anchorMax = new Vector2(1f - SideInset, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(0f, BarHeight);
            _bg.gameObject.SetActive(true);
            _capsuleBg.gameObject.SetActive(false);
        }

        var root = transform as RectTransform;
        if (root != null && root.rect.height > 0f) _layoutDone = true;   // 量到了就不用每帧重摆
    }

    float TopOffset()
    {
        var root = transform as RectTransform;
        float safe = SafeAreaTopUnits(root);
        float chrome = TopBarBottomUnits(root);

        float y = Mathf.Max(safe, chrome);
        if (chrome <= 0f) y += TopBarReserve;   // 量不到 TopBar：按经验值下移，别压在资源条上

        return Mathf.Max(0f, y) + GapUnderTopBar;
    }

    /// <summary>
    /// 刘海 / 状态栏占用的顶部高度，换算成本 Canvas 的局部单位。
    /// 项目目前没有 SafeArea 工具，这里用 Screen.safeArea 归一化后乘根矩形高度；
    /// 真机换算有误差也不怕——TopBarBottomUnits 量得到时会取两者较大者兜住。
    /// </summary>
    static float SafeAreaTopUnits(RectTransform root)
    {
        if (Screen.height <= 0) return 0f;

        float h = root != null && root.rect.height > 0f ? root.rect.height : GameConfig.DESIGN_HEIGHT;
        Rect safe = Screen.safeArea;
        float topPx = Screen.height - (safe.y + safe.height);
        return topPx > 0f ? topPx / Screen.height * h : 0f;
    }

    /// <summary>主界面 TopBar 底边到屏幕顶的距离；量不到返回 0（调用方改用经验值）。</summary>
    float TopBarBottomUnits(RectTransform root)
    {
        var hall = GuildHallUI.Instance;
        if (hall == null || root == null || root.rect.height <= 0f) return 0f;

        var top = TownSharedChrome.FindDeep(hall.transform, "TopBar") as RectTransform;
        if (top == null) return 0f;

        var hallCanvas = hall.GetComponentInParent<Canvas>();
        var myCanvas = GetComponent<Canvas>();
        if (hallCanvas == null || myCanvas == null) return 0f;

        var hallCam = hallCanvas.worldCamera != null ? hallCanvas.worldCamera : Camera.main;
        var myCam = myCanvas.worldCamera != null ? myCanvas.worldCamera : Camera.main;
        if (hallCam == null || myCam == null) return 0f;

        var corners = new Vector3[4];
        top.GetWorldCorners(corners);                       // [0] = 左下
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(hallCam, corners[0]);
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, myCam, out local))
            return 0f;

        float fromTop = root.rect.height * 0.5f - local.y;
        return fromTop > 0f ? fromTop : 0f;
    }

    // ============================================================
    // 刷新
    // ============================================================

    void Update()
    {
        if (_bar == null) return;

        bool show = GameSceneGate.IsTown
                    && StoryProgress.TutorialDone
                    && SaveSystem.Instance != null && SaveSystem.Instance.Data != null;

        if (_bar.activeSelf != show)
        {
            _bar.SetActive(show);
            if (show)
            {
                _layoutDone = false;   // 换场景后重新量一次顶部让位
                _idleTimer = 0f;
                _collapsed = false;
                Layout();
                Refresh();
                _timer = RefreshInterval;
                TryShowBubble();       // ⑥ 首次气泡
            }
            return;
        }
        if (!show) return;

        // 顶部让位第一次量得到之后还要再摆一次（首帧 rect 可能是 0）
        if (!_layoutDone) Layout();

        // ④ 无操作收缩：用 unscaledDeltaTime 累加，不接 Input 事件系统
        if (!_collapsed && !_celebrating)
        {
            _idleTimer += Time.unscaledDeltaTime;
            if (_idleTimer >= IdleTimeout)
            {
                _collapsed = true;
                _layoutDone = false;
                Layout();
            }
        }

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = RefreshInterval;

        MainQuestSystem.Tick();
        Refresh();
    }

    void Refresh()
    {
        if (_mainRow.descText == null) return;

        int chapter = MainQuestSystem.CurrentChapter();
        var main = MainQuestSystem.CurrentView();
        var daily = MainQuestSystem.FirstOpenDaily();

        // ⑧ 完成态优先：保持 0.8s，不阻塞 0.5s 轮询
        if (_celebrating)
        {
            if (Time.unscaledTime < _celebrateUntil)
            {
                RenderCelebrate();
                return;
            }
            _celebrating = false;
            // 庆祝结束：把「下一任务」当作基线，避免重复触发
            _shownMainId = main != null ? main.def.id : null;
            _shownMainDef = main != null ? main.def : default(MainQuestDef);
            _shownDailyId = daily.HasValue ? daily.Value.id : null;
        }

        // —— 完成检测（主线）——
        if (_shownMainId != null && (main == null || main.def.id != _shownMainId))
        {
            var prevDef = _shownMainDef;
            // MainQuestDef 是 struct（值类型），不能与 null 比较 → 只用 id 判空
            if (!string.IsNullOrEmpty(prevDef.id) && MainQuestSystem.IsDone(prevDef))
            {
                StartCelebrate(true, prevDef, null);
                return;
            }
        }
        // —— 完成检测（每日）——
        if (_shownDailyId != null && (!daily.HasValue || daily.Value.id != _shownDailyId))
        {
            var prevDaily = FindDaily(_shownDailyId);
            if (prevDaily.HasValue && prevDaily.Value.done)
            {
                StartCelebrate(false, default(MainQuestDef), prevDaily);
                return;
            }
        }

        _mainDef = default(MainQuestDef);
        _daily = default(DailyQuestView);
        _hasDaily = false;

        // ③ 主线行
        string mainTag = "主线";
        Color mainColor = new Color(0.35f, 0.24f, 0.10f, 1f);
        string mainDesc;
        string mainProg;
        string mainReward = "";
        bool mainClickable = false;

        if (main != null)
        {
            _mainDef = main.def;
            _showingMain = true;
            mainDesc = "第" + chapter + "章 " + GameConfig.GetChapterMapName(chapter) + " · " + main.Desc;
            mainProg = main.ProgressText;                 // ① 当前任务进度
            if (main.def.rewardStones > 0)                // ② 天赋石奖励
                mainReward = "天赋石 ×" + main.def.rewardStones;
            mainClickable = true;
        }
        else if (!MainQuestSystem.IsChapterCleared(chapter))
        {
            _showingMain = true;
            mainDesc = "第" + chapter + "章 " + GameConfig.GetChapterMapName(chapter) + " · 通关本章";
            mainProg = "";
            mainClickable = true;
        }
        else
        {
            _showingMain = true;
            mainDesc = "本章主线已完成";                  // ③ 灰显占位，不隐藏
            mainProg = "";
            mainClickable = false;
        }

        bool showChapterBadge = main != null;
        _chapterBadge.text = showChapterBadge
            ? (MainQuestSystem.ChapterClearedCount(chapter) + "/" + MainQuestSystem.ChapterStageTotal)
            : "";
        _chapterBadge.gameObject.SetActive(showChapterBadge);

        // ③ 每日行（含 ⑤ 第三级兜底）
        string dailyTag = "每日";
        Color dailyColor = new Color(0.10f, 0.26f, 0.30f, 1f);
        string dailyDesc;
        string dailyProg;
        string dailyReward = "";
        bool dailyClickable = false;
        bool dailyFallback = false;

        if (daily.HasValue)
        {
            var d = daily.Value;
            _hasDaily = true;
            _daily = d;
            dailyDesc = d.desc;
            dailyProg = d.progress + "/" + d.need;
            dailyReward = d.rewardText;                  // ② 每日奖励文案
            dailyClickable = true;
        }
        else
        {
            string fb = FallbackHint();                  // ⑤ 成就 → 图鉴 → 全部完成
            if (fb != null)
            {
                dailyTag = "提示";
                dailyColor = new Color(0.20f, 0.16f, 0.26f, 1f);
                dailyDesc = fb;
                dailyProg = "";
                dailyClickable = true;
                dailyFallback = true;
            }
            else
            {
                dailyDesc = "全部完成";
                dailyProg = "";
                dailyClickable = false;
            }
        }

        RenderRow(_mainRow, RowKind.Main, mainTag, mainColor, mainDesc, mainProg, mainReward, mainClickable, false);
        RenderRow(_dailyRow, RowKind.Daily, dailyTag, dailyColor, dailyDesc, dailyProg, dailyReward, dailyClickable, dailyFallback);

        // ④ 胶囊文案（收缩时显示 标签 + 进度）
        if (main != null)
        {
            _capsuleLabel.text = "主线";
            _capsuleProgress.text = main.ProgressText;
        }
        else if (daily.HasValue)
        {
            _capsuleLabel.text = "每日";
            _capsuleProgress.text = daily.Value.progress + "/" + daily.Value.need;
        }
        else
        {
            _capsuleLabel.text = "任务";
            _capsuleProgress.text = "完成";
        }

        // 更新基线（用于下帧完成检测）
        _shownMainId = main != null ? main.def.id : null;
        _shownMainDef = main != null ? main.def : default(MainQuestDef);
        _shownDailyId = daily.HasValue ? daily.Value.id : null;
    }

    void RenderRow(RowUi row, RowKind kind, string tag, Color tagColor, string desc,
        string prog, string reward, bool clickable, bool isFallback)
    {
        row.tagText.text = tag;
        row.tagBg.color = tagColor;
        row.descText.text = desc;
        row.descText.color = clickable ? new Color(0.94f, 0.94f, 0.97f) : new Color(0.62f, 0.62f, 0.66f);
        row.progressText.text = prog;
        row.progressText.color = new Color(1f, 0.86f, 0.5f);
        row.rewardText.text = reward;
        row.rewardText.color = reward != "" ? new Color(1f, 0.8f, 0.4f) : Color.clear;
        var k = kind;
        row.onClick = clickable ? (() => OnRowClick(k, isFallback)) : null;
        row.hit.image.raycastTarget = true;
    }

    void OnRowClick(RowKind kind, bool isFallback)
    {
        if (isFallback)
        {
            OnClickPanel();
            return;
        }
        if (kind == RowKind.Main)
        {
            if (string.IsNullOrEmpty(_mainDef.id))
                QuestJumpRouter.GoChapter(MainQuestSystem.CurrentChapter());
            else
                QuestJumpRouter.GoMain(_mainDef);
            return;
        }
        // 每日行
        if (_hasDaily)
        {
            bool ok = QuestJumpRouter.GoDaily(_daily);
            if (!ok)
            {
                OnClickPanel();
                GlobalToastUI.Show("暂时无法前往，先看看任务列表");
            }
        }
    }

    /// <summary>「全部」：展开面板，停在条上显示的那一类，所见即所点。</summary>
    void OnClickPanel()
    {
        QuestPanelUI.Show(_showingMain);
    }

    /// <summary>④ 点击收缩胶囊 → 展开回完整条。</summary>
    void OnClickCapsule()
    {
        _collapsed = false;
        _idleTimer = 0f;
        _layoutDone = false;
        Layout();
    }

    // —— ⑧ 完成态 ——
    void StartCelebrate(bool isMain, MainQuestDef mainDef, DailyQuestView? daily)
    {
        _celebrating = true;
        _celebrateUntil = Time.unscaledTime + CelebrateTime;
        _celebrateIsMain = isMain;
        _idleTimer = 0f;
        if (isMain)
        {
            int ch = MainQuestSystem.CurrentChapter();
            _celebrateDesc = "第" + ch + "章 " + GameConfig.GetChapterMapName(ch) + " · " + MainQuestDefs.AutoDesc(mainDef);
            _celebrateReward = mainDef.rewardStones > 0 ? "天赋石 ×" + mainDef.rewardStones : "";
            _celebrateTag = "主线";
            _celebrateTagColor = new Color(0.35f, 0.24f, 0.10f, 1f);
        }
        else
        {
            var d = daily.Value;
            _celebrateDesc = d.desc;
            _celebrateReward = d.rewardText;
            _celebrateTag = "每日";
            _celebrateTagColor = new Color(0.10f, 0.26f, 0.30f, 1f);
        }
        RenderCelebrate();
    }

    void RenderCelebrate()
    {
        var row = _celebrateIsMain ? _mainRow : _dailyRow;
        row.tagText.text = _celebrateTag;
        row.tagBg.color = _celebrateTagColor;
        row.descText.text = _celebrateDesc + "  完成";
        row.descText.color = new Color(0.7f, 1f, 0.8f);     // 变亮（绿）
        row.progressText.text = "✓";
        row.progressText.color = new Color(0.6f, 1f, 0.7f);
        row.rewardText.text = _celebrateReward;
        row.rewardText.color = _celebrateReward != "" ? new Color(1f, 0.95f, 0.6f) : Color.clear;   // 奖励数字跳亮
    }

    DailyQuestView? FindDaily(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var list = MainQuestSystem.DailyViews();
        for (int i = 0; i < list.Count; i++)
            if (list[i].id == id) return list[i];
        return null;
    }

    // —— ⑤ 第三级兜底 ——
    string FallbackHint()
    {
        if (MainQuestSystem.HasOpenAchievement())
            return "还有可领的成就奖励";
        if (MainQuestSystem.HasUnrecordedCodex())
            return "去冒险日志看看未记录的图鉴";
        return null;
    }

    // —— ⑥ 首次气泡 ——
    void TryShowBubble()
    {
        if (_bubbleShown) return;
        if (PlayerPrefs.GetInt("qhb_bubble_seen", 0) > 0) return;
        ShowBubble();
    }

    void ShowBubble()
    {
        _bubbleShown = true;
        var brt = _bubble.transform as RectTransform;
        brt.anchoredPosition = new Vector2(0f, -(TopOffset() + BarHeight + 8f));
        _bubble.SetActive(true);
        _bubbleCatcher.gameObject.SetActive(true);
    }

    void DismissBubble()
    {
        _bubble.SetActive(false);
        _bubbleCatcher.gameObject.SetActive(false);
        PlayerPrefs.SetInt("qhb_bubble_seen", 1);
        PlayerPrefs.Save();
    }

    // ============================================================
    // UI 小工具
    // ============================================================

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>左右用比例锚点 + 像素内缩，保证窄屏也不出界。</summary>
    static void SetAnchors(RectTransform rt, float minX, float minY, float maxX, float maxY,
        float left, float bottom, float right, float top)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    /// <summary>透明点击层：无 sprite 的 Image 依然接收射线，用它承接点击。</summary>
    static Button CreateHitBtn(Transform parent, string name, float leftInset, float rightInset)
    {
        var img = CreateImg(parent, name, Color.clear);
        SetAnchors(img.rectTransform, 0f, 0f, 1f, 1f, leftInset, 0f, -rightInset, 0f);
        img.raycastTarget = true;
        return img.gameObject.AddComponent<Button>();
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
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }
}
