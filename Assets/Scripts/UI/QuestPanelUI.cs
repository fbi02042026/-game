using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 任务面板（2026-09-19 新增）：点主界面「当前目标」条的「全部」展开。
/// 两个分页——主线 / 每日：
///   · 主线：本章全部任务，带完成打勾、进度、当前条高亮，点「前往」直达对应玩法
///   · 每日：今日可做的日常，点「前往」直达（登录→每日登录界面；通关 N 关→冒险页）
///   已完成的行不显示「前往」，只留标题上的打勾。
///   跳转与任务条共用 <see cref="QuestJumpRouter"/>，两处行为一致。
///
/// 与 DailyLoginUI 同层（sort 900），纯运行时建树，不碰 prefab。
/// </summary>
public class QuestPanelUI : MonoBehaviour
{
    public static QuestPanelUI Instance { get; private set; }

    const int SortOrder = 900;
    const float PanelWRatio = 0.92f;
    const float PanelHRatio = 0.86f;
    const float RowH = 96f;
    const float RowGap = 8f;

    GameObject _root;
    RectTransform _panelRt;
    Text _headerText;
    Button _tabMain;
    Button _tabDaily;
    Text _tabMainLabel;
    Text _tabDailyLabel;
    RectTransform _listRt;

    bool _mainTab = true;
    readonly List<GameObject> _rows = new List<GameObject>();

    /// <summary>打开面板。mainTab=true 停在「主线」分页。</summary>
    public static void Show(bool mainTab)
    {
        Ensure().Open(mainTab);
    }

    public static QuestPanelUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("QuestPanelUI", typeof(RectTransform));
        DontDestroyOnLoad(go);
        var ui = go.AddComponent<QuestPanelUI>();
        ui.Build();
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
    // 建树
    // ============================================================

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateImg(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

        var panel = CreateImg(_root.transform, "Panel", new Color(0.09f, 0.08f, 0.10f, 0.98f));
        _panelRt = panel.rectTransform;
        StretchRatio(_panelRt, (1f - PanelWRatio) / 2f, (1f - PanelHRatio) / 2f);

        var title = CreateTxt(panel.transform, "Title", "\u4efb\u52a1", 40, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.5f, 0f, 470f, 520f, 52f);

        // 分页
        _tabMain = CreateBtn(panel.transform, "TabMain", "\u4e3b\u7ebf", new Vector2(-120f, 396f), new Vector2(200f, 56f));
        _tabDaily = CreateBtn(panel.transform, "TabDaily", "\u6bcf\u65e5", new Vector2(120f, 396f), new Vector2(200f, 56f));
        _tabMainLabel = _tabMain.GetComponentInChildren<Text>();
        _tabDailyLabel = _tabDaily.GetComponentInChildren<Text>();
        _tabMain.onClick.AddListener(() => SetTab(true));
        _tabDaily.onClick.AddListener(() => SetTab(false));

        _headerText = CreateTxt(panel.transform, "Header", "", 22, TextAnchor.MiddleLeft);
        SetRect(_headerText.rectTransform, 0.5f, 0.5f, 0f, 336f, 900f, 32f);
        _headerText.color = new Color(1f, 0.86f, 0.5f);

        // 列表容器：宽度按面板比例，行按容器顶边往下排（不依赖固定像素宽）
        var list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(panel.transform, false);
        _listRt = list.GetComponent<RectTransform>();
        _listRt.anchorMin = new Vector2(0.04f, 0.5f);
        _listRt.anchorMax = new Vector2(0.96f, 0.5f);
        _listRt.pivot = new Vector2(0.5f, 0.5f);
        _listRt.anchoredPosition = new Vector2(0f, -60f);
        _listRt.sizeDelta = new Vector2(0f, 700f);

        var close = CreateBtn(panel.transform, "CloseButton", "\u5173\u95ed",
            new Vector2(0f, -470f), new Vector2(220f, 56f));
        close.onClick.AddListener(Hide);

        _root.SetActive(false);
        GameFonts.ApplyToHierarchy(transform);
    }

    void Open(bool mainTab)
    {
        _mainTab = mainTab;
        _root.SetActive(true);
        Canvas.ForceUpdateCanvases();

        // 面板宽度随屏浮动（720×1280 MatchHeight，逻辑宽 576~720），
        // 表头必须按实测宽度收缩，否则窄屏上文字会顶出面板。
        if (_panelRt != null && _headerText != null)
        {
            float w = _panelRt.rect.width - 56f;
            if (w > 0f) _headerText.rectTransform.sizeDelta = new Vector2(w, 32f);
        }

        Refresh();
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    void SetTab(bool main)
    {
        _mainTab = main;
        Refresh();
    }

    // ============================================================
    // 刷新
    // ============================================================

    void Refresh()
    {
        if (_tabMainLabel != null)
            _tabMainLabel.color = _mainTab ? new Color(1f, 0.86f, 0.5f) : new Color(0.55f, 0.55f, 0.58f);
        if (_tabDailyLabel != null)
            _tabDailyLabel.color = !_mainTab ? new Color(1f, 0.86f, 0.5f) : new Color(0.55f, 0.55f, 0.58f);

        ClearRows();
        if (_mainTab) RefreshMain();
        else RefreshDaily();
    }

    void RefreshMain()
    {
        int chapter = MainQuestSystem.CurrentChapter();
        int cleared = MainQuestSystem.ChapterClearedCount(chapter);
        int total = MainQuestSystem.ChapterStageTotal;
        _headerText.text = "\u7b2c" + chapter + "\u7ae0 " + GameConfig.GetChapterMapName(chapter)
                           + "　\u8fdb\u5ea6 " + cleared + "/" + total;

        var views = MainQuestSystem.ChapterViews(chapter);
        if (views.Count == 0)
        {
            AddRow("\u672c\u7ae0\u4e3b\u7ebf\u4efb\u52a1\u914d\u7f6e\u4e2d", "\u672a\u914d\u7f6e", "", null);
            return;
        }

        for (int i = 0; i < views.Count; i++)
        {
            var v = views[i];
            string reward = v.def.rewardStones > 0
                ? "\u5956\u52b1\uff1a\u5929\u8d4b\u77f3 \u00d7" + v.def.rewardStones
                : "\u5956\u52b1\uff1a\u968f\u7ae0\u8282\u91cc\u7a0b\u7891\u53d1\u653e";
            string progress = v.done ? "" : (v.need > 1 ? v.progress + "/" + v.need : "");

            AddRow((v.done ? "\u2713 " : (v.isCurrent ? "\u25b6 " : "\u25cb ")) + v.def.title,
                v.Desc + "　" + reward, progress, v.done ? null : (Action)delegate { OnMainAction(v); });
        }
    }

    void RefreshDaily()
    {
        var list = MainQuestSystem.DailyViews();
        int remain = 0;
        for (int i = 0; i < list.Count; i++)
            if (!list[i].done) remain++;
        _headerText.text = "\u4eca\u65e5\u5269\u4f59\u5956\u52b1 " + remain + " \u9879";

        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            AddRow((d.done ? "\u2713 " : "\u25cb ") + d.title,
                d.desc + "　\u5956\u52b1\uff1a" + d.rewardText,
                d.progress + "/" + d.need, d.done ? null : (Action)delegate { OnDailyAction(d); });
        }
    }

    /// <summary>一条任务行：标题 / 描述+奖励 / 进度 /（未完成时）「前往」按钮。</summary>
    void AddRow(string title, string desc, string progress, Action onClick)
    {
        if (_listRt == null) return;

        var bg = CreateImg(_listRt, "Row", new Color(0.16f, 0.14f, 0.19f, 1f));
        var rt = bg.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, RowH);
        rt.anchoredPosition = new Vector2(0f, -_rows.Count * (RowH + RowGap));
        _rows.Add(bg.gameObject);

        var titleTxt = CreateTxt(bg.transform, "Title", title, 24, TextAnchor.MiddleLeft);
        SetAnchors(titleTxt.rectTransform, 0f, 0.5f, 1f, 1f, 42f, 2f, -150f, -6f);
        titleTxt.color = new Color(0.95f, 0.92f, 0.86f);

        var descTxt = CreateTxt(bg.transform, "Desc", desc, 18, TextAnchor.MiddleLeft);
        SetAnchors(descTxt.rectTransform, 0f, 0f, 1f, 0.5f, 42f, 6f, -150f, -2f);
        descTxt.color = new Color(0.72f, 0.74f, 0.80f);

        if (!string.IsNullOrEmpty(progress))
        {
            var progTxt = CreateTxt(bg.transform, "Progress", progress, 20, TextAnchor.MiddleRight);
            SetAnchors(progTxt.rectTransform, 1f, 0.5f, 1f, 1f, -140f, 2f, -12f, -6f);
            progTxt.color = new Color(1f, 0.86f, 0.5f);
        }

        // 已完成（或无动作）：不放按钮，只留标题上的打勾
        if (onClick == null)
        {
            GameFonts.ApplyToHierarchy(bg.transform);
            return;
        }

        var btn = CreateBtn(bg.transform, "Action", "\u524d\u5f80", Vector2.zero, new Vector2(128f, 44f));
        var brt = btn.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(1f, 0f);
        brt.anchoredPosition = new Vector2(-12f, 12f);

        btn.onClick.AddListener(() => onClick());
        GameFonts.ApplyToHierarchy(bg.transform);
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
        // 与任务条共用同一套跳转；跳不动就留在面板上给一行提示，不静默
        if (!QuestJumpRouter.GoMain(v.def))
        {
            GlobalToastUI.Show("\u6682\u65f6\u65e0\u6cd5\u524d\u5f80\uff0c\u7a0d\u540e\u518d\u8bd5");
            return;
        }
        Hide();
    }

    void OnDailyAction(DailyQuestView d)
    {
        // 「每日登录」打开每日登录界面；「今日通关」送进冒险页
        if (!QuestJumpRouter.GoDaily(d))
        {
            GlobalToastUI.Show("\u6682\u65f6\u65e0\u6cd5\u524d\u5f80\uff0c\u7a0d\u540e\u518d\u8bd5");
            return;
        }
        // 面板收起即可；下次 Open 会重新拉一次进度
        Hide();
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

    static void StretchRatio(RectTransform rt, float insetX, float insetY)
    {
        rt.anchorMin = new Vector2(insetX, insetY);
        rt.anchorMax = new Vector2(1f - insetX, 1f - insetY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static void SetRect(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void SetAnchors(RectTransform rt, float minX, float minY, float maxX, float maxY,
        float left, float bottom, float right, float top)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
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

    static Button CreateBtn(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var img = CreateImg(parent, name, new Color(0.25f, 0.22f, 0.28f, 1f));
        SetRect(img.rectTransform, 0.5f, 0.5f, pos.x, pos.y, size.x, size.y);
        var btn = img.gameObject.AddComponent<Button>();
        var txt = CreateTxt(img.transform, "Label", label, 20, TextAnchor.MiddleCenter);
        Stretch(txt.rectTransform);
        return btn;
    }
}
