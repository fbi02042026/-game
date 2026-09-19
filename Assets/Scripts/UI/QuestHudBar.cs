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
    const float BarHeight = 68f;
    /// <summary>宽度占屏宽 92%（左右各留 4%），水平居中。</summary>
    const float SideInset = 0.04f;
    /// <summary>TopBar 高度经验值：量不到实测值时兜底（与 TownSharedChrome.StretchTop 的 120 一致）。</summary>
    const float TopBarReserve = 120f;
    /// <summary>与 TopBar 之间留的呼吸空隙。</summary>
    const float GapUnderTopBar = 10f;
    /// <summary>右侧「全部」按钮占的宽度（跳转点击层给它让位，避免两个按钮重叠冒泡）。</summary>
    const float PanelBtnWidth = 92f;
    const float RefreshInterval = 0.5f;

    GameObject _bar;
    Image _bg;
    Image _tagBg;
    Text _tagText;
    Text _descText;
    Text _progressText;
    Button _hit;
    Button _panelBtn;
    float _timer;
    bool _layoutDone;
    bool _showingMain = true;

    /// <summary>条上当前指向的任务：跳转时用（不进存档，纯 UI 态）。</summary>
    MainQuestDef _mainDef;
    DailyQuestView _daily;
    bool _hasDaily;

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
        _bg.raycastTarget = false;   // 底衬不吃点击，点击交给下面两个按钮

        // ① 整条（除右侧「全部」）= 跳转；②「全部」= 展开面板。
        // 两个按钮是兄弟且互不重叠，避免点「全部」冒泡触发整条跳转。
        _hit = CreateHitBtn(_bar.transform, "Jump", 0f, PanelBtnWidth + 20f);
        _hit.onClick.AddListener(OnClickJump);

        var panelImg = CreateImg(_bar.transform, "AllBtn", new Color(0.28f, 0.25f, 0.34f, 0.95f));
        var prt = panelImg.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
        prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(-12f, 0f);
        prt.sizeDelta = new Vector2(PanelBtnWidth, 44f);
        _panelBtn = panelImg.gameObject.AddComponent<Button>();
        _panelBtn.onClick.AddListener(OnClickPanel);
        var panelTxt = CreateTxt(panelImg.transform, "Label", "\u5168\u90e8", 18, TextAnchor.MiddleCenter);
        Stretch(panelTxt.rectTransform);
        panelTxt.color = new Color(1f, 0.86f, 0.5f);

        // 左：主线 / 每日 标签
        _tagBg = CreateImg(_bg.transform, "Tag", new Color(0.35f, 0.24f, 0.10f, 1f));
        SetAnchors(_tagBg.rectTransform, 0f, 0.5f, 0f, 0.5f, 10f, -16f, 58f, 16f);

        _tagText = CreateTxt(_tagBg.transform, "TagText", "", 18, TextAnchor.MiddleCenter);
        Stretch(_tagText.rectTransform);
        _tagText.color = new Color(1f, 0.86f, 0.5f);

        // 中：一句话目标（超长省略）
        _descText = CreateTxt(_bg.transform, "Desc", "", 22, TextAnchor.MiddleLeft);
        SetAnchors(_descText.rectTransform, 0f, 0f, 1f, 1f, 70f, 4f, -258f, -4f);
        _descText.color = new Color(0.94f, 0.94f, 0.97f);
        _descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _descText.verticalOverflow = VerticalWrapMode.Truncate;

        // 右：进度 +「前往」提示（提示整条可点，点了直达）
        _progressText = CreateTxt(_bg.transform, "Progress", "", 20, TextAnchor.MiddleRight);
        SetAnchors(_progressText.rectTransform, 1f, 0f, 1f, 1f, -250f, 4f, -182f, -4f);
        _progressText.color = new Color(1f, 0.86f, 0.5f);

        var goHint = CreateTxt(_bg.transform, "GoHint", "\u524d\u5f80", 18, TextAnchor.MiddleCenter);
        SetAnchors(goHint.rectTransform, 1f, 0f, 1f, 1f, -176f, 4f, -110f, -4f);
        goHint.color = new Color(0.72f, 0.86f, 1f);

        _bar.SetActive(false);
        GameFonts.ApplyToHierarchy(transform);
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

        rt.anchorMin = new Vector2(SideInset, 1f);
        rt.anchorMax = new Vector2(1f - SideInset, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -TopOffset());
        rt.sizeDelta = new Vector2(0f, BarHeight);

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
                Layout();
                Refresh();
                _timer = RefreshInterval;
            }
            return;
        }
        if (!show) return;

        // 顶部让位第一次量得到之后还要再摆一次（首帧 rect 可能是 0）
        if (!_layoutDone) Layout();

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = RefreshInterval;

        MainQuestSystem.Tick();
        Refresh();
    }

    void Refresh()
    {
        if (_descText == null) return;

        _mainDef = default(MainQuestDef);
        _daily = default(DailyQuestView);
        _hasDaily = false;

        int chapter = MainQuestSystem.CurrentChapter();
        var main = MainQuestSystem.CurrentView();

        if (main == null && !MainQuestSystem.IsChapterCleared(chapter))
        {
            // 本章还没配主线任务（第 3 章及以后 TODO）→ 仍给章节级牵引，别让条变空
            SetTag("\u4e3b\u7ebf", new Color(0.35f, 0.24f, 0.10f, 1f));
            _descText.text = "\u7b2c" + chapter + "\u7ae0 " + GameConfig.GetChapterMapName(chapter)
                             + " \u00b7 \u901a\u5173\u672c\u7ae0";
            _progressText.text = MainQuestSystem.ChapterClearedCount(chapter) + "/"
                                 + MainQuestSystem.ChapterStageTotal;
            _descText.color = new Color(0.94f, 0.94f, 0.97f);
            _showingMain = true;
            return;
        }

        if (main != null)
        {
            _showingMain = true;
            _mainDef = main.def;
            SetTag("\u4e3b\u7ebf", new Color(0.35f, 0.24f, 0.10f, 1f));
            _descText.text = "\u7b2c" + chapter + "\u7ae0 " + GameConfig.GetChapterMapName(chapter)
                             + " \u00b7 " + main.Desc;
            _progressText.text = MainQuestSystem.ChapterClearedCount(chapter) + "/"
                                 + MainQuestSystem.ChapterStageTotal;
            _descText.color = new Color(0.94f, 0.94f, 0.97f);
            return;
        }

        // 主线做完 / 本章已通关 → 退到每日
        var daily = MainQuestSystem.FirstOpenDaily();
        if (daily.HasValue)
        {
            var d = daily.Value;
            _showingMain = false;
            _hasDaily = true;
            _daily = d;
            SetTag("\u6bcf\u65e5", new Color(0.10f, 0.26f, 0.30f, 1f));
            _descText.text = d.desc;
            _progressText.text = d.progress + "/" + d.need;
            _descText.color = new Color(0.94f, 0.94f, 0.97f);
            return;
        }

        _showingMain = false;
        SetTag("\u4eca\u65e5", new Color(0.16f, 0.16f, 0.18f, 1f));
        _descText.text = "\u5168\u90e8\u5b8c\u6210";
        _progressText.text = "";
        _descText.color = new Color(0.62f, 0.62f, 0.66f);
    }

    void SetTag(string text, Color color)
    {
        if (_tagText != null) _tagText.text = text;
        if (_tagBg != null) _tagBg.color = color;
    }

    // ============================================================
    // 点击
    // ============================================================

    /// <summary>整条 = 跳转。跳不动就回退展开面板 + 一行提示。</summary>
    void OnClickJump()
    {
        if (!_showingMain && !_hasDaily)
        {
            // 没有可跳的目标（全部完成）：直接展开面板，别让点击没反应
            OnClickPanel();
            return;
        }

        bool ok = _showingMain
            ? (string.IsNullOrEmpty(_mainDef.id)
                ? QuestJumpRouter.GoChapter(MainQuestSystem.CurrentChapter())
                : QuestJumpRouter.GoMain(_mainDef))
            : QuestJumpRouter.GoDaily(_daily);

        if (ok) return;

        // 跳转不可用（不在城镇 / 酒馆被禁入 / 界面未就绪）
        // → 回退：展开任务面板 + 一行提示，至少让玩家看到清单
        OnClickPanel();
        GlobalToastUI.Show("\u6682\u65f6\u65e0\u6cd5\u524d\u5f80\uff0c\u5148\u770b\u770b\u4efb\u52a1\u5217\u8868");
    }

    /// <summary>「全部」：展开面板，停在条上显示的那一类，所见即所点。</summary>
    void OnClickPanel()
    {
        QuestPanelUI.Show(_showingMain);
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
