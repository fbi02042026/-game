using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务面板（2026-09-19 初版；2026-09-26 按主人新美术重做版式）。
///
/// 两个分页——主线 / 每日，可切换、有选中/未选中两态；任务条做成可上下滑动的列表
/// （ScrollView + VerticalLayoutGroup + ContentSizeFitter），主线/每日共用同一套行模板。
///
/// 构建策略（与 RewardPopupUI 一致，主人认可）：
///   优先 Resources.Load 预制体 Prefabs/UI/QuestPanel（自带 Canvas 与完整节点树），
///   找不到预制体时退回纯代码色块兜底（同一套 sprite），两条路共用 WireUp 绑定代码。
///
/// 层级：Canvas sortingOrder 走 GameConfig.UiSort.TownPopup(900)，与每日登录界面同级。
/// 字体：Open 时 GameFonts.ApplyToHierarchy 统一刷（中文 fusion-pixel / 数字 PixelFont）。
///
/// 业务：Show(bool mainTab) / Instance 对外；「前往」走 QuestJumpRouter；
///   已完成的行不显示「前往」（只留状态图标）。MainQuestSystem / MainQuestDefs /
///   QuestJumpRouter 的内部逻辑一律不动，本类只读取它们的数据。
/// </summary>
public class QuestPanelUI : MonoBehaviour
{
    public static QuestPanelUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/QuestPanel";
    const int SortOrder = GameConfig.UiSort.TownPopup;   // 900，与每日登录界面同级

    // 资源图（Resources/UI/Quest/*）
    const string SP_PANEL = "UI/Quest/QuestPanel";
    const string SP_TAB_ON = "UI/Quest/QuestTabOn";
    const string SP_TAB_OFF = "UI/Quest/QuestTabOff";
    const string SP_ROW = "UI/Quest/QuestRow";
    const string SP_CLOSE = "UI/Quest/QuestClose";

    // 尺寸（720×1280 竖屏参考分辨率，与生成器脚本保持一致）
    const float PANEL_W = 662f;
    const float PANEL_H = 1100f;
    const float ROW_H = 96f;
    const float ROW_GAP = 8f;

    // 配色
    static readonly Color GOLD = new Color(1f, 0.86f, 0.5f);
    static readonly Color DIM = new Color(0.55f, 0.55f, 0.58f);

    // ---- 节点引用 ----
    Image _tabMainImg;
    Image _tabDailyImg;
    Text _tabMainLabel;
    Text _tabDailyLabel;
    Text _headerText;
    RectTransform _content;
    GameObject _rowTemplate;

    bool _mainTab = true;
    readonly List<GameObject> _rows = new List<GameObject>();

    // 运行时缓存的 Tab 胶囊 sprite（兜底与预制体两路都走 Resources 读取）
    Sprite _spTabOn;
    Sprite _spTabOff;

    /// <summary>打开面板。mainTab=true 停在「主线」分页。</summary>
    public static void Show(bool mainTab)
    {
        Ensure().Open(mainTab);
    }

    public static QuestPanelUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        bool hasPrefab = prefab != null;

        GameObject root;
        if (hasPrefab)
        {
            root = UnityEngine.Object.Instantiate(prefab);
            root.name = "QuestPanel";
        }
        else
        {
            root = new GameObject("QuestPanel", typeof(RectTransform));
        }
        DontDestroyOnLoad(root);

        var ui = root.GetComponent<QuestPanelUI>();
        if (ui == null) ui = root.AddComponent<QuestPanelUI>();
        ui.Init(hasPrefab);
        return ui;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 初始化
    // ============================================================

    void Init(bool hasPrefab)
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);

        // 找不到预制体才纯代码补树；预制体已带完整节点，直接复用
        if (!hasPrefab) BuildFallbackTree();

        WireUp();
        ApplyTab();
        gameObject.SetActive(false);
        GameFonts.ApplyToHierarchy(transform);
    }

    /// <summary>找到预制体节点并绑定（两路共用）。</summary>
    void WireUp()
    {
        _spTabOn = LoadSprite(SP_TAB_ON);
        _spTabOff = LoadSprite(SP_TAB_OFF);

        var rootT = transform;
        var dim = FindChild(rootT, "Dim");
        var panel = FindChild(rootT, "Panel");
        if (panel == null)
        {
            Debug.LogError("[QuestPanelUI] 找不到 Panel 节点，无法构建任务面板");
            return;
        }

        var tm = FindChild(panel, "TabMain");
        var td = FindChild(panel, "TabDaily");
        if (tm != null)
        {
            _tabMainImg = tm.GetComponent<Image>();
            _tabMainLabel = FindChild(tm, "Label")?.GetComponent<Text>();
            BindButton(tm, () => SetTab(true));
        }
        if (td != null)
        {
            _tabDailyImg = td.GetComponent<Image>();
            _tabDailyLabel = FindChild(td, "Label")?.GetComponent<Text>();
            BindButton(td, () => SetTab(false));
        }

        _headerText = FindChild(panel, "Header")?.GetComponent<Text>();

        var sv = FindChild(panel, "ScrollView");
        _content = FindChild(sv, "Content") as RectTransform;
        _rowTemplate = FindChild(_content, "RowTemplate")?.gameObject;

        BindButton(dim, Hide);
        BindButton(FindChild(panel, "CloseBtn"), Hide);
    }

    // ============================================================
    // 兜底建树（无预制体时；节点名与生成器完全一致）
    // ============================================================

    void BuildFallbackTree()
    {
        var rootRt = transform as RectTransform;
        Stretch(rootRt);

        // Dim：全屏半透明黑（兼点击关闭）
        var dim = CreateImg(transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>();

        // Panel：图层1 底框
        var panel = CreateImg(transform, "Panel", Color.white);
        panel.sprite = LoadSprite(SP_PANEL);
        panel.type = Image.Type.Sliced;
        SetAnchored(panel.rectTransform, 0.5f, 0.5f, 0f, 0f, PANEL_W, PANEL_H);

        // Title
        var title = CreateTxt(panel.transform, "Title", "任务", 40, GOLD, TextAnchor.MiddleCenter);
        SetAnchored(title.rectTransform, 0.5f, 0.5f, 0f, 480f, PANEL_W - 80f, 64f);

        // TabMain / TabDaily
        BuildTab(panel.transform, "TabMain", "主线", new Vector2(-120f, 400f));
        BuildTab(panel.transform, "TabDaily", "每日", new Vector2(120f, 400f));

        // Header
        _headerText = CreateTxt(panel.transform, "Header", "", 22, GOLD, TextAnchor.MiddleLeft);
        SetAnchored(_headerText.rectTransform, 0f, 0.5f, -PANEL_W / 2f + 24f, 330f, PANEL_W - 48f, 32f);

        // ScrollView + RectMask2D
        var scroll = CreateImg(panel.transform, "ScrollView", new Color(0f, 0f, 0f, 0f));
        scroll.raycastTarget = false;
        var scrollRt = scroll.rectTransform;
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(20f, 90f);
        scrollRt.offsetMax = new Vector2(-20f, -150f);
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.inertia = true;
        scroll.gameObject.AddComponent<RectMask2D>();

        // Content
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(scroll.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = ROW_GAP;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentRt;
        _content = contentRt;

        // RowTemplate
        var row = CreateImg(content.transform, "RowTemplate", Color.white);
        row.sprite = LoadSprite(SP_ROW);
        row.type = Image.Type.Sliced;
        var rowRt = row.rectTransform;
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = Vector2.zero;
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = ROW_H;
        le.flexibleHeight = 0f;
        BuildRowChildren(row.transform);
        row.gameObject.SetActive(false);
        _rowTemplate = row.gameObject;

        // CloseBtn
        var close = CreateImg(panel.transform, "CloseBtn", Color.white);
        close.sprite = LoadSprite(SP_CLOSE);
        close.type = Image.Type.Sliced;
        SetAnchored(close.rectTransform, 0.5f, 0.5f, 0f, -480f, 220f, 56f);
        close.gameObject.AddComponent<Button>();
        var closeLabel = CreateTxt(close.transform, "Label", "关闭", 24, Color.white, TextAnchor.MiddleCenter);
        Stretch(closeLabel.rectTransform);
    }

    static void BuildTab(Transform parent, string name, string label, Vector2 pos)
    {
        var img = CreateImg(parent, name, Color.white);
        img.sprite = LoadSprite(SP_TAB_OFF);
        img.type = Image.Type.Sliced;
        SetAnchored(img.rectTransform, 0.5f, 0.5f, pos.x, pos.y, 200f, 56f);
        img.gameObject.AddComponent<Button>();
        var txt = CreateTxt(img.transform, "Label", label, 26, DIM, TextAnchor.MiddleCenter);
        Stretch(txt.rectTransform);
    }

    static void BuildRowChildren(Transform row)
    {
        var status = CreateTxt(row, "StatusIcon", "○", 30, GOLD, TextAnchor.MiddleCenter);
        SetAnchors(status.rectTransform, 0f, 0.5f, 0f, 0.5f, 24f, -20f, 64f, 20f);

        var title = CreateTxt(row, "Title", "任务标题", 24, new Color(0.95f, 0.92f, 0.86f), TextAnchor.MiddleLeft);
        SetAnchors(title.rectTransform, 0f, 1f, 1f, 1f, 76f, -48f, -170f, -12f);

        var sub = CreateTxt(row, "Sub", "任务描述 / 奖励：天赋石 ×1", 18,
            new Color(0.62f, 0.74f, 0.66f), TextAnchor.MiddleLeft);
        SetAnchors(sub.rectTransform, 0f, 0f, 1f, 0f, 76f, 8f, -170f, 38f);

        var go = CreateImg(row, "GoBtn", new Color(1f, 1f, 1f, 0f));
        go.raycastTarget = true;
        go.gameObject.AddComponent<Button>();
        SetAnchors(go.rectTransform, 1f, 0.5f, 1f, 0.5f, -170f, -24f, -20f, 24f);
        var goLabel = CreateTxt(go.transform, "Label", "前往", 22, Color.white, TextAnchor.MiddleCenter);
        Stretch(goLabel.rectTransform);
    }

    // ============================================================
    // 打开 / 关闭 / 切 Tab
    // ============================================================

    void Open(bool mainTab)
    {
        _mainTab = mainTab;
        gameObject.SetActive(true);
        var cv = GetComponent<Canvas>();
        if (cv != null) UICanvasSetup.RefreshPopup(cv, SortOrder);
        Refresh();
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
    }

    void SetTab(bool main)
    {
        if (_mainTab == main) return;
        _mainTab = main;
        ApplyTab();
        Refresh();
    }

    /// <summary>选中态换 QuestTabOn、未选中换 QuestTabOff，文字色同步（亮/暗）。</summary>
    void ApplyTab()
    {
        if (_tabMainImg != null) _tabMainImg.sprite = _mainTab ? _spTabOn : _spTabOff;
        if (_tabDailyImg != null) _tabDailyImg.sprite = _mainTab ? _spTabOff : _spTabOn;
        if (_tabMainLabel != null) _tabMainLabel.color = _mainTab ? GOLD : DIM;
        if (_tabDailyLabel != null) _tabDailyLabel.color = _mainTab ? DIM : GOLD;
    }

    // ============================================================
    // 刷新列表
    // ============================================================

    void Refresh()
    {
        ApplyTab();
        ClearRows();
        if (_mainTab) RefreshMain();
        else RefreshDaily();
    }

    void RefreshMain()
    {
        int chapter = MainQuestSystem.CurrentChapter();
        var views = MainQuestSystem.ChapterViews(chapter);

        int total = views.Count;
        int done = 0;
        for (int i = 0; i < views.Count; i++)
            if (views[i].done) done++;

        if (_headerText != null)
            _headerText.text = "第" + chapter + "章 " + GameConfig.GetChapterMapName(chapter)
                               + "　" + done + "/" + total;

        if (total == 0)
        {
            AddRow("本章主线任务配置中", "未配置", 0, null);
            return;
        }

        for (int i = 0; i < views.Count; i++)
        {
            var v = views[i];
            string reward = v.def.rewardStones > 0
                ? "奖励：天赋石 ×" + v.def.rewardStones
                : "奖励：随章节里程碑里程序释放";
            int status = v.done ? 2 : (v.isCurrent ? 1 : 0);
            AddRow(v.def.title, v.Desc + "　" + reward, status,
                v.done ? null : (Action)delegate { OnMainAction(v); });
        }
    }

    void RefreshDaily()
    {
        var list = MainQuestSystem.DailyViews();

        int total = list.Count;
        int done = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i].done) done++;

        if (_headerText != null)
            _headerText.text = "今日任务 " + done + "/" + total;

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            int status = d.done ? 2 : 0;
            AddRow(d.title, d.desc + "　奖励：" + d.rewardText, status,
                d.done ? null : (Action)delegate { OnDailyAction(d); });
        }
    }

    /// <summary>
    /// 一条任务行：状态图标（▶进行中 / ○未完成 / ✓完成，金色）+ 标题 + 副标题（灰绿小字）
    /// + 右侧「前往」按钮（落在图层5 自带蓝色胶囊位；已完成行不显示）。
    /// status：2=完成，1=进行中，0=未完成。
    /// </summary>
    void AddRow(string title, string sub, int status, Action onClick)
    {
        if (_content == null || _rowTemplate == null) return;

        var row = UnityEngine.Object.Instantiate(_rowTemplate, _content, false);
        row.name = "Row_" + _rows.Count;
        row.gameObject.SetActive(true);
        _rows.Add(row.gameObject);

        var statusTxt = FindChild(row.transform, "StatusIcon")?.GetComponent<Text>();
        if (statusTxt != null)
        {
            statusTxt.text = status == 2 ? "✓" : (status == 1 ? "▶" : "○");
            statusTxt.color = GOLD;
        }

        var t = FindChild(row.transform, "Title")?.GetComponent<Text>();
        if (t != null) t.text = title;

        var s = FindChild(row.transform, "Sub")?.GetComponent<Text>();
        if (s != null) s.text = sub;

        var go = FindChild(row.transform, "GoBtn");
        if (go != null)
        {
            if (onClick != null)
            {
                go.gameObject.SetActive(true);
                BindButton(go, onClick);
            }
            else
            {
                go.gameObject.SetActive(false);
            }
        }

        GameFonts.ApplyToHierarchy(row.transform);
    }

    void ClearRows()
    {
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null) Destroy(_rows[i]);
        _rows.Clear();
    }

    // ============================================================
    // 动作
    // ============================================================

    void OnMainAction(MainQuestView v)
    {
        if (!QuestJumpRouter.GoMain(v.def))
        {
            GlobalToastUI.Show("暂时无法前往，稍后再试");
            return;
        }
        Hide();
    }

    void OnDailyAction(DailyQuestView d)
    {
        if (!QuestJumpRouter.GoDaily(d))
        {
            GlobalToastUI.Show("暂时无法前往，稍后再试");
            return;
        }
        Hide();
    }

    // ============================================================
    // 小工具
    // ============================================================

    static void BindButton(Transform t, Action a)
    {
        if (t == null) return;
        var b = t.GetComponent<Button>();
        if (b == null) b = t.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.interactable = true;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => a());
    }

    static Sprite LoadSprite(string path)
    {
        return Resources.Load<Sprite>(path);
    }

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    static void SetAnchored(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void SetAnchors(RectTransform rt, float minX, float minY, float maxX, float maxY,
        float left, float bottom, float right, float top)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    static Image CreateImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateTxt(Transform parent, string name, string content, int size, Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }
}
