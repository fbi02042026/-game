using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面「任务」列表（2026-09-19 新增；2026-09-26 改版为**左上角可收纳列表**）。
///
/// 目的：玩家一进游戏就知道「现在该干嘛」（目标显化），点一下直达功能（降低操作成本）
/// —— 两件事都直接服务「在线时长 ↑ / 流失 ↓」。
///
/// 位置：屏幕**左上角**（TopBar 下方），容器宽度按内容自适应（约 320 逻辑像素、上限屏宽 46%），
///   不再横铺 92% 全屏宽——横条会压住主界面其它 UI。
///   顶部让位 = max(刘海/状态栏安全区, TopBar 实测底边)，都量不到就按 120 的经验值下移。
///
/// 形态（2026-09-26 主人要求）：
///   ◀ 任务 ▶        ← 标题行：黄色「任务」二字 + 左右各一个箭头，整行可点 = 展开 / 收纳
///     踏出第一步      ← 任务行：**只显示任务名称**，最多 3 条（主线优先，不足用每日补齐），行间距拉开
///   点任务名 → <see cref="QuestJumpRouter"/> 直达；跳转落不到界面 → 回退展开面板 + 一行提示，
///   绝不让点击没反应。标题行右侧「全部」小按钮 = 展开 <see cref="QuestPanelUI"/> 看全部任务。
///
/// 实现约定：全部运行时建树，不碰 prefab；自带 Canvas（sort 260）：
/// 高于大厅与切页遮罩，低于弹窗（900），所以弹窗打开时被盖住是正常的。
/// </summary>
public class QuestHudBar : MonoBehaviour
{
    public static QuestHudBar Instance { get; private set; }

    const int SortOrder = 260;

    // —— 尺寸（逻辑像素）——
    /// <summary>容器宽度（再按屏宽上限收敛，避免横着铺满）。</summary>
    const float PanelWidthUnits = 320f;
    /// <summary>宽度上限：屏宽 46%。</summary>
    const float MaxWidthRatio = 0.46f;
    /// <summary>左边距：屏宽 4%。</summary>
    const float LeftInsetRatio = 0.04f;
    const float TitleHeight = 46f;
    const float RowHeight = 38f;
    /// <summary>行间距：2026-09-26 主人要求「每个任务中间拉开点距离」，由 4 提到 14。</summary>
    const float RowGap = 14f;
    const float ListTopPad = 6f;
    const float BottomPad = 8f;
    /// <summary>最多显示几条任务名（主线优先，主线不足 3 条用每日补齐）。</summary>
    const int MaxRows = 3;

    /// <summary>标题字号（比任务行大一号，视觉上明显是标题）。</summary>
    const int TitleFontSize = 27;
    const int RowFontSize = 21;

    /// <summary>TopBar 高度经验值：量不到实测值时兜底（与 TownSharedChrome.StretchTop 的 120 一致）。</summary>
    const float TopBarReserve = 120f;
    /// <summary>与 TopBar 之间留的呼吸空隙。</summary>
    const float GapUnderTopBar = 10f;
    const float RefreshInterval = 0.5f;
    /// <summary>任务完成态保持时长（不阻塞 0.5s 轮询）。</summary>
    const float CelebrateTime = 0.8f;

    /// <summary>标题行里「全部」小按钮的宽度（标题行右侧，点它展开面板）。</summary>
    const float PanelBtnWidth = 60f;
    const float PanelBtnHeight = 30f;
    const float ArrowSize = 22f;

    /// <summary>
    /// 展开/收纳状态持久化 key（1 = 收纳）。
    /// 以后想改成「不持久化（每次进城镇都展开）」，把 Update/ToggleCollapse 里这两处
    /// PlayerPrefs 读写删掉、只留 _collapsed 字段即可。
    /// </summary>
    const string PP_COLLAPSED = "QuestHudCollapsed";

    /// <summary>标题「任务」的字色：亮金 #FFD54A。</summary>
    static readonly Color TitleColor = new Color(1f, 0.84f, 0.29f);
    static readonly Color NameColor = new Color(0.94f, 0.94f, 0.97f);
    static readonly Color NameDimColor = new Color(0.62f, 0.62f, 0.66f);
    static readonly Color DoneColor = new Color(0.7f, 1f, 0.8f);

    /// <summary>整条根节点（展开/收纳都在这一个对象上切换尺寸）。</summary>
    GameObject _bar;
    Image _bg;
    /// <summary>首次气泡 + 全屏点击捕捉（点任意处消失，只一次，⑥）。</summary>
    GameObject _bubble;
    Button _bubbleCatcher;

    /// <summary>预建 3 行，按条数显隐（不运行时 new，避免每 0.5s 建树）。</summary>
    readonly RowUi[] _rows = new RowUi[MaxRows];
    /// <summary>与 _rows 一一对应的当前数据（复用对象，避免每次刷新分配）。</summary>
    readonly RowPlan[] _plan = new RowPlan[MaxRows];
    int _rowCount;

    float _timer;
    bool _layoutDone;
    /// <summary>true = 收纳（只留标题行）；由点标题行切换，并持久化到 PlayerPrefs。</summary>
    bool _collapsed;
    bool _showingMain = true;

    // —— ⑧ 完成态庆祝 ——
    bool _celebrating;
    float _celebrateUntil;
    string _celebrateName;

    // —— 完成检测：上一帧展示的任务 id / def ——
    string _shownMainId;
    MainQuestDef _shownMainDef;
    string _shownDailyId;

    /// <summary>首次气泡是否已出现过（同会话内只弹一次，跨会话靠 PlayerPrefs）。</summary>
    bool _bubbleShown;

    /// <summary>箭头贴图缓存（Resources 取不到时代码生成一个三角形）。</summary>
    static Sprite _arrowSprite;

    enum RowKind { None, Main, Daily, Fallback }

    /// <summary>单行 UI 引用集合（现在只有「任务名 + 透明热区」）。</summary>
    class RowUi
    {
        public GameObject root;
        public Text nameText;
        public Button hit;
        public Action onClick;
    }

    /// <summary>一行的展示数据（只保留名字 + 点击要用的跳转信息）。</summary>
    class RowPlan
    {
        public RowKind kind;
        public string name;
        public bool clickable;
        public MainQuestDef mainDef;
        public DailyQuestView daily;
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
        _layoutDone = false;      // 行数/高度可能变了，重新摆一次
        // 注意：2026-09-26 起收纳由玩家手动控制（持久化），
        // 所以这里**不再**强制 _collapsed = false，免得跟玩家的收纳选择打架。
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
        _bg.raycastTarget = false;   // 底衬不吃点击，点击交给标题行/每行的热区

        BuildTitleRow();

        for (int i = 0; i < MaxRows; i++)
        {
            _rows[i] = new RowUi();
            _plan[i] = new RowPlan();
            BuildRow(_rows[i], "Row" + i, i);
        }

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

    /// <summary>
    /// 标题行：左箭头 + 黄色「任务」+ 右箭头，整行是「展开/收纳」热区；
    /// 右侧「全部」小按钮在热区之后创建（层级更靠上），所以它的点击优先，不会被整行热区吃掉。
    /// </summary>
    void BuildTitleRow()
    {
        var go = new GameObject("TitleRow", typeof(RectTransform));
        go.transform.SetParent(_bg.transform, false);
        var row = go.transform as RectTransform;
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = Vector2.zero;
        row.sizeDelta = new Vector2(0f, TitleHeight);

        // 左箭头（尖朝下的贴图转 -90° → 指向左）
        CreateArrow(row, "ArrowL", 14f, -90f);
        // 右箭头（转 +90° → 指向右）
        CreateArrow(row, "ArrowR", 104f, 90f);

        var title = CreateTxt(row, "Title", "任务", TitleFontSize, TextAnchor.MiddleLeft);
        SetAnchors(title.rectTransform, 0f, 0f, 0f, 1f, 30f, 0f, 92f, 0f);
        title.color = TitleColor;

        // 整行热区（先建，位于「全部」之下）
        var hit = CreateHitBtn(row, "TitleHit", 0f, 0f);
        hit.onClick.AddListener(ToggleCollapse);

        // 右侧「全部」= 展开任务面板（后建 → 层级在上，点击优先）
        var panelImg = CreateImg(row, "AllBtn", new Color(0.28f, 0.25f, 0.34f, 0.95f));
        var prt = panelImg.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
        prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(-8f, 0f);
        prt.sizeDelta = new Vector2(PanelBtnWidth, PanelBtnHeight);
        var panelBtn = panelImg.gameObject.AddComponent<Button>();
        panelBtn.onClick.AddListener(OnClickPanel);
        var panelTxt = CreateTxt(panelImg.transform, "Label", "全部", 16, TextAnchor.MiddleCenter);
        Stretch(panelTxt.rectTransform);
        panelTxt.color = new Color(1f, 0.86f, 0.5f);
    }

    /// <summary>建一行任务名（只显示名字 + 整行透明热区）。index 决定纵向位置，位置固定不重排。</summary>
    void BuildRow(RowUi row, string name, int index)
    {
        row.root = new GameObject(name, typeof(RectTransform));
        row.root.transform.SetParent(_bg.transform, false);
        var rt = row.root.transform as RectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -(TitleHeight + ListTopPad + index * (RowHeight + RowGap)));
        rt.sizeDelta = new Vector2(0f, RowHeight);

        row.nameText = CreateTxt(row.root.transform, "Name", "", RowFontSize, TextAnchor.MiddleLeft);
        SetAnchors(row.nameText.rectTransform, 0f, 0f, 1f, 1f, 12f, 0f, -8f, 0f);
        row.nameText.color = NameColor;

        // 整行热区（每行一个，互不包含）
        row.hit = CreateHitBtn(row.root.transform, "Hit", 0f, 0f);
        var r = row;   // 捕获本行实例
        row.hit.onClick.AddListener(() => { if (r.onClick != null) r.onClick(); });
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
        var btxt = CreateTxt(_bubble.transform, "BubbleTxt", "点任务名直接过去", 16, TextAnchor.MiddleCenter);
        Stretch(btxt.rectTransform);
        btxt.color = Color.white;
        _bubble.SetActive(false);
    }

    // ============================================================
    // 展开 / 收纳
    // ============================================================

    /// <summary>点标题行：展开 ⇄ 收纳，并持久化。</summary>
    void ToggleCollapse()
    {
        _collapsed = !_collapsed;
        PlayerPrefs.SetInt(PP_COLLAPSED, _collapsed ? 1 : 0);
        PlayerPrefs.Save();
        _layoutDone = false;
        Refresh();          // 内部会重新算高度并摆位
    }

    // ============================================================
    // 布局
    // ============================================================

    /// <summary>
    /// 把容器贴到**屏幕左上角**（TopBar 下方）：
    /// 纵向让位取「刘海安全区」与「TopBar 实测底边」的较大者，量不到就退回经验值；
    /// 横向贴左（屏宽 4%），宽度按内容自适应并受屏宽 46% 上限约束。
    /// </summary>
    void Layout()
    {
        if (_bar == null) return;

        var rt = _bar.transform as RectTransform;
        if (rt == null) return;

        var root = transform as RectTransform;
        float w = PanelRectWidth(root);

        int n = Mathf.Max(1, _rowCount);
        float h = TitleHeight;
        if (!_collapsed)
            h += ListTopPad + n * RowHeight + (n - 1) * RowGap + BottomPad;

        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(w * LeftInsetRatio, -TopOffset());
        rt.sizeDelta = new Vector2(Mathf.Min(PanelWidthUnits, w * MaxWidthRatio), h);

        if (root != null && root.rect.height > 0f) _layoutDone = true;   // 量到了就不用每帧重摆
    }

    /// <summary>本 Canvas 根矩形的宽度；量不到就用设计宽兜底。</summary>
    static float PanelRectWidth(RectTransform root)
    {
        return root != null && root.rect.width > 0f ? root.rect.width : GameConfig.DESIGN_WIDTH;
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
                _collapsed = PlayerPrefs.GetInt(PP_COLLAPSED, 0) > 0;   // 沿用上次的展开/收纳
                _layoutDone = false;   // 换场景后重新量一次顶部让位
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

        // 2026-09-26：原来的「无操作 3 秒自动收缩成右侧胶囊」已整套移除——
        // 现在收纳由玩家点标题行手动控制（<see cref="ToggleCollapse"/>），
        // 自动收缩会和手动收纳互相打架（刚展开就自己收回去），故不再保留。
        // 相应字段：CapsuleWidth / CapsuleHeight / IdleTimeout / _idleTimer / _capsule* 均已删除。

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = RefreshInterval;

        MainQuestSystem.Tick();
        Refresh();
    }

    void Refresh()
    {
        if (_rows[0] == null || _rows[0].nameText == null) return;

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

        // —— 组装最多 3 条任务名：主线优先，主线不足 3 条用每日补齐 ——
        int n = 0;
        _showingMain = false;

        var views = MainQuestSystem.ChapterViews(MainQuestSystem.CurrentChapter());
        for (int i = 0; i < views.Count && n < MaxRows; i++)
        {
            var v = views[i];
            if (v.done) continue;

            var p = _plan[n];
            p.kind = RowKind.Main;
            p.name = MainName(v.def);          // 只显示任务名
            p.clickable = true;
            p.mainDef = v.def;
            p.daily = default(DailyQuestView);
            n++;
            _showingMain = true;
        }

        if (n < MaxRows)
        {
            var dailies = MainQuestSystem.DailyViews();
            for (int i = 0; i < dailies.Count && n < MaxRows; i++)
            {
                var d = dailies[i];
                if (d.done) continue;

                var p = _plan[n];
                p.kind = RowKind.Daily;
                p.name = DailyName(d);         // 只显示任务名
                p.clickable = true;
                p.mainDef = default(MainQuestDef);
                p.daily = d;
                n++;
            }
        }

        // 一条都没有：给一行兜底（成就/图鉴提示，或「全部完成」占位），不让列表空着
        if (n == 0)
        {
            string fb = FallbackHint();
            var p = _plan[0];
            p.kind = fb != null ? RowKind.Fallback : RowKind.None;
            p.name = fb != null ? fb : "全部完成";
            p.clickable = fb != null;
            p.mainDef = default(MainQuestDef);
            p.daily = default(DailyQuestView);
            n = 1;
        }

        _rowCount = n;
        for (int i = 0; i < MaxRows; i++)
        {
            var row = _rows[i];
            bool on = i < n && !_collapsed;
            if (row.root.activeSelf != on) row.root.SetActive(on);
            if (i < n) RenderRow(row, _plan[i]);
        }

        Layout();

        // 更新基线（用于下帧完成检测）
        _shownMainId = main != null ? main.def.id : null;
        _shownMainDef = main != null ? main.def : default(MainQuestDef);
        _shownDailyId = daily.HasValue ? daily.Value.id : null;
    }

    /// <summary>主线任务显示名：优先 title，没配就用自动文案兜底。</summary>
    static string MainName(MainQuestDef def)
    {
        return !string.IsNullOrEmpty(def.title) ? def.title : MainQuestDefs.AutoDesc(def);
    }

    /// <summary>每日任务显示名：优先 title，没配就用 desc 兜底。</summary>
    static string DailyName(DailyQuestView d)
    {
        return !string.IsNullOrEmpty(d.title) ? d.title : d.desc;
    }

    void RenderRow(RowUi row, RowPlan p)
    {
        row.nameText.text = p.name;
        row.nameText.color = p.clickable ? NameColor : NameDimColor;
        row.onClick = p.clickable ? (Action)(() => OnRowClick(p)) : null;
        row.hit.image.raycastTarget = true;
    }

    void OnRowClick(RowPlan p)
    {
        if (p == null) return;

        if (p.kind == RowKind.Fallback || p.kind == RowKind.None)
        {
            OnClickPanel();
            return;
        }
        if (p.kind == RowKind.Main)
        {
            if (string.IsNullOrEmpty(p.mainDef.id))
                QuestJumpRouter.GoChapter(MainQuestSystem.CurrentChapter());
            else
                QuestJumpRouter.GoMain(p.mainDef);
            return;
        }
        // 每日行：跳转拿不到目标界面 → 回退展开面板 + 一行提示
        if (!QuestJumpRouter.GoDaily(p.daily))
        {
            OnClickPanel();
            GlobalToastUI.Show("暂时无法前往，先看看任务列表");
        }
    }

    /// <summary>「全部」：展开面板，停在列表里显示的那一类，所见即所点。</summary>
    void OnClickPanel()
    {
        QuestPanelUI.Show(_showingMain);
    }

    // —— ⑧ 完成态 ——
    void StartCelebrate(bool isMain, MainQuestDef mainDef, DailyQuestView? daily)
    {
        _celebrating = true;
        _celebrateUntil = Time.unscaledTime + CelebrateTime;
        _celebrateName = isMain ? MainName(mainDef) : (daily.HasValue ? DailyName(daily.Value) : "任务");
        RenderCelebrate();
    }

    void RenderCelebrate()
    {
        var row = _rows.Length > 0 ? _rows[0] : null;
        if (row == null || row.nameText == null || row.root == null) return;

        // 收纳状态就不亮这一行（没有背景衬托，浮在面板外反而难看）；
        // 完成本身还有 GlobalToast 提示，信息不会丢。
        if (row.root.activeSelf != !_collapsed) row.root.SetActive(!_collapsed);
        if (_collapsed) return;

        row.nameText.text = _celebrateName + "  完成";
        row.nameText.color = DoneColor;
        row.onClick = null;                                   // 庆祝期间不响应跳转
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
        float w = PanelRectWidth(transform as RectTransform);
        float panelW = Mathf.Min(PanelWidthUnits, w * MaxWidthRatio);
        // 跟着容器走：左上角、居中对齐于容器宽度
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(w * LeftInsetRatio + panelW * 0.5f,
                                           -(TopOffset() + TitleHeight + 10f));
        brt.sizeDelta = new Vector2(200f, 38f);
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

    /// <summary>
    /// 建一个箭头 Image：优先用现成贴图 Resources/UI/Common/箭头01.png（尖朝下），
    /// 靠 RectTransform 旋转 ±90° 得到左/右朝向；取不到就用代码生成的三角形兜底。
    /// 不用 Unicode 的 ▶◀ —— 项目中文像素字体不一定收录，会显示豆腐块。
    /// </summary>
    static Image CreateArrow(Transform parent, string name, float x, float zAngle)
    {
        var img = CreateImg(parent, name, TitleColor);
        img.sprite = ArrowSprite();
        img.preserveAspect = true;
        img.raycastTarget = false;          // 箭头不抢点击，交给标题行热区
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(ArrowSize, ArrowSize);
        rt.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        return img;
    }

    static Sprite ArrowSprite()
    {
        if (_arrowSprite == null)
        {
            _arrowSprite = Resources.Load<Sprite>("UI/Common/箭头01");
            if (_arrowSprite == null) _arrowSprite = GenerateArrowSprite();
        }
        return _arrowSprite;
    }

    /// <summary>
    /// 兜底：代码生成一个 32×32、尖朝下的白色三角（写法参考 TargetIndicator.GenerateRingSprite）。
    /// 只在 Resources 贴图缺失时用到，生成一次后缓存在 <see cref="_arrowSprite"/>。
    /// </summary>
    static Sprite GenerateArrowSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 归一化到 0..1；ny=1 是上边（底边），ny=0 是下边（尖）
                float nx = (x + 0.5f) / size;
                float ny = (y + 0.5f) / size;
                bool inside = Mathf.Abs(nx - 0.5f) <= 0.5f * (1f - ny);
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
