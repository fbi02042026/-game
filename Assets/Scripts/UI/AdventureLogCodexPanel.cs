using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险日志图鉴（Paper1）：复用用户手做节点。
/// 怪物格模板：Paper1/怪物（iconbg/普通边框|boss边框/icon、name、boss）
/// 详情：Paper1/ActiveCard + ClaimAch
/// 佣兵按 普通/稀有/传奇 分页；怪物按场景章节分页。
/// </summary>
public class AdventureLogCodexPanel
{
    const int Cols = 4;
    const float CellGapX = 10f;
    const float CellGapY = 14f;
    const float HeaderH = 36f;
    const float PagePad = 8f;
    const float SectionGap = 16f;      // 章与章（区与区）之间的间距
    const float BottomBarReserve = 64f; // 底部翻页条留白，避免最后一行被箭头挡住

    readonly Transform _frame;
    GameObject _root;
    GameObject _paperTask;
    ScrollRect _scroll;
    RectTransform _content;
    GameObject _cellTemplate;
    Text _pageTitle;
    Button _prevBtn;
    Button _nextBtn;
    Text _pageLabel;

    // 详情弹层（复用 Paper1/ActiveCard）
    GameObject _detailRoot;
    Text _detailTitle;
    Text _detailDesc;
    Text _detailObj;
    Text _detailProg;
    Button _claimBtn;
    Text _claimLabel;
    Image _detailMaskImg;

    readonly List<GameObject> _spawned = new List<GameObject>();
    int _page;
    bool _mercMode;
    string _selectedId;
    bool _selectedIsMerc;
    bool _selectedBossOrLegend;

    static readonly string[] MercPageNames = { "普通", "稀有", "传奇" };

    public GameObject Root => _root;

    public AdventureLogCodexPanel(Transform frame)
    {
        _frame = frame;
        _paperTask = frame != null ? frame.Find("Paper")?.gameObject : null;
    }

    public void Ensure()
    {
        if (_root != null) return;
        if (_frame == null) return;

        var paper1 = _frame.Find("Paper1");
        if (paper1 == null)
        {
            Debug.LogError("[AdventureLogCodex] 找不到 Paper1，请在预制体 Frame 下保留手做 Paper1");
            return;
        }
        _root = paper1.gameObject;

        // 模板：怪物
        var cell = FindChildByNames(paper1, "怪物", "MonsterCell");
        if (cell != null)
        {
            if (cell.name != "MonsterCell")
                cell.name = "MonsterCell"; // 便于后续绑定，不改 prefab 文件
            _cellTemplate = cell.gameObject;
            _cellTemplate.SetActive(false);
            if (_cellTemplate.transform.parent != null
                && _cellTemplate.transform.parent.name != "Content")
            {
                // 模板放在 Paper1 下即可，克隆时挂到 Content
            }
        }

        _scroll = paper1.Find("Scroll")?.GetComponent<ScrollRect>();
        _content = _scroll != null ? _scroll.content : paper1.Find("Scroll/Viewport/Content") as RectTransform;
        if (_content == null)
            _content = FindDeep(paper1, "Content") as RectTransform;

        // 页标题：bg/OngoingHeader 或 OngoingHeader
        var header = paper1.Find("bg/OngoingHeader") ?? FindDeep(paper1, "OngoingHeader");
        if (header != null)
            _pageTitle = header.GetComponent<Text>() ?? header.GetComponentInChildren<Text>(true);

        EnsurePageButtons(paper1);
        BindDetail(paper1);

        if (_cellTemplate == null)
            Debug.LogError("[AdventureLogCodex] 找不到怪物格子模板（节点名「怪物」）");
        if (_content == null)
            Debug.LogError("[AdventureLogCodex] 找不到 Paper1/Scroll/.../Content");

        LayoutPageButtons(paper1);
        HideDetail();
        PrepareCodexScrollArea();
        _root.SetActive(false);
    }

    void PrepareCodexScrollArea()
    {
        if (_scroll == null) return;
        _scroll.gameObject.SetActive(true);
        _scroll.enabled = true;
        _scroll.horizontal = false;
        _scroll.vertical = true;

        var scrollRt = _scroll.transform as RectTransform;
        if (scrollRt != null)
        {
            scrollRt.anchorMin = new Vector2(0.04f, 0.12f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.86f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            scrollRt.anchoredPosition = Vector2.zero;
        }

        if (_scroll.viewport != null)
        {
            var vp = _scroll.viewport;
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = Vector2.zero;
            vp.offsetMax = Vector2.zero;
        }

        if (_content != null)
        {
            var vlg = _content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;
            var csf = _content.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.enabled = false;
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
        }
    }

    void EnsurePageButtons(Transform paper1)
    {
        var prev = FindChildByNames(paper1, "PrevPage", "上一页");
        var next = FindChildByNames(paper1, "NextPage", "下一页");

        // 翻页条统一挂到 Paper1 根下，确保能锚定到页面最下方并浮在 Scroll 之上
        var bar = paper1.Find("PageBar") ?? FindDeep(paper1, "PageBar");
        if (bar == null)
        {
            var go = new GameObject("PageBar", typeof(RectTransform));
            go.transform.SetParent(paper1, false);
            bar = go.transform;
        }
        else if (bar.parent != paper1)
        {
            bar.SetParent(paper1, false);
        }
        bar.SetAsLastSibling();

        if (prev == null) prev = CreateTinyBtn(bar, "PrevPage", "<", new Vector2(-90f, 0f));
        else if (prev.parent != bar) prev.SetParent(bar, false);
        if (next == null) next = CreateTinyBtn(bar, "NextPage", ">", new Vector2(90f, 0f));
        else if (next.parent != bar) next.SetParent(bar, false);

        // 中间页码
        var labelTr = bar.Find("PageLabel");
        if (labelTr == null)
        {
            var lgo = new GameObject("PageLabel", typeof(RectTransform));
            lgo.transform.SetParent(bar, false);
            var lrt = (RectTransform)lgo.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(90f, 32f);
            var lt = lgo.AddComponent<Text>();
            lt.font = GameFonts.GetChinese();
            lt.fontSize = 20;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.color = new Color(0.28f, 0.18f, 0.1f, 1f);
            _pageLabel = lt;
        }
        else
        {
            _pageLabel = labelTr.GetComponent<Text>();
        }

        _prevBtn = prev.GetComponent<Button>() ?? prev.gameObject.AddComponent<Button>();
        _nextBtn = next.GetComponent<Button>() ?? next.gameObject.AddComponent<Button>();
        _prevBtn.transition = Selectable.Transition.None;
        _nextBtn.transition = Selectable.Transition.None;
        _prevBtn.onClick.RemoveAllListeners();
        _nextBtn.onClick.RemoveAllListeners();
        _prevBtn.onClick.AddListener(() => ShiftPage(-1));
        _nextBtn.onClick.AddListener(() => ShiftPage(1));

        LayoutPageButtons(paper1);
    }

    void LayoutPageButtons(Transform paper1)
    {
        if (paper1 == null) return;
        var bar = paper1.Find("PageBar") as RectTransform;
        if (bar == null) return;

        // 页面最下方居中
        bar.anchorMin = new Vector2(0.5f, 0f);
        bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 28f);
        bar.sizeDelta = new Vector2(260f, 44f);
        bar.SetAsLastSibling();

        var prev = bar.Find("PrevPage") as RectTransform;
        var next = bar.Find("NextPage") as RectTransform;
        if (prev != null) prev.anchoredPosition = new Vector2(-90f, 0f);
        if (next != null) next.anchoredPosition = new Vector2(90f, 0f);
    }

    void BindDetail(Transform paper1)
    {
        _detailRoot = paper1.Find("ActiveCard")?.gameObject;
        if (_detailRoot == null) return;
        _detailTitle = FindDeep(_detailRoot.transform, "ActiveTitle")?.GetComponent<Text>();
        _detailDesc = FindDeep(_detailRoot.transform, "ActiveDesc")?.GetComponent<Text>();
        var art = FindDeep(_detailRoot.transform, "Art");
        if (art != null)
        {
            _detailObj = art.Find("Objective")?.GetComponent<Text>();
            _detailProg = art.Find("Progress")?.GetComponent<Text>();
        }
        _detailMaskImg = FindDeep(_detailRoot.transform, "mask")?.Find("Image")?.GetComponent<Image>();

        var claim = paper1.Find("ClaimAch");
        if (claim != null)
        {
            _claimBtn = claim.GetComponent<Button>() ?? claim.gameObject.AddComponent<Button>();
            _claimLabel = FindDeep(claim, "T")?.GetComponent<Text>()
                          ?? claim.GetComponentInChildren<Text>(true);
            _claimBtn.transition = Selectable.Transition.None;
            _claimBtn.onClick.RemoveAllListeners();
            _claimBtn.onClick.AddListener(OnClaimReward);
        }
    }

    public void ShowMonsters()
    {
        Ensure();
        _mercMode = false;
        _page = Mathf.Clamp(_page, 1, MonsterSpreadCount());
        if (_paperTask != null) _paperTask.SetActive(false);
        if (_root != null) _root.SetActive(true);
        PrepareCodexScrollArea();
        HideDetail();
        Rebuild();
    }

    public void ShowMercs()
    {
        Ensure();
        _mercMode = true;
        if (_paperTask != null) _paperTask.SetActive(false);
        if (_root != null) _root.SetActive(true);
        PrepareCodexScrollArea();
        Rebuild();
    }

    public void Hide()
    {
        ClearSpawned();
        HideDetail();
        CodexInfoPopupUI.HideActive();
        var paper1 = ResolvePaper1();
        if (paper1 != null)
            paper1.gameObject.SetActive(false);
        if (_paperTask != null)
            _paperTask.SetActive(true);
    }

    Transform ResolvePaper1()
    {
        if (_root != null) return _root.transform;
        return _frame != null ? _frame.Find("Paper1") : null;
    }

    // 一页放两章 → 页数 = ceil(章数 / 2)
    static int MonsterSpreadCount()
    {
        return Mathf.Max(1, Mathf.CeilToInt(AdventureCodex.MaxMonsterChapter() / 2f));
    }

    void ShiftPage(int delta)
    {
        if (_mercMode) return; // 佣兵单页展示三区域，不翻页
        _page = Mathf.Clamp(_page + delta, 1, MonsterSpreadCount());
        HideDetail();
        Rebuild();
    }

    void Rebuild()
    {
        ClearSpawned();
        if (_content == null || _cellTemplate == null) return;

        // 佣兵单页三区域 → 隐藏翻页箭头/页码；怪物 → 显示
        if (_prevBtn != null) _prevBtn.gameObject.SetActive(!_mercMode);
        if (_nextBtn != null) _nextBtn.gameObject.SetActive(!_mercMode);
        if (_pageLabel != null) _pageLabel.gameObject.SetActive(!_mercMode);

        if (_mercMode) RebuildMerc();
        else RebuildMonster();

        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        GameFonts.ApplyToHierarchy(_content);
    }

    void RebuildMonster()
    {
        int spreads = MonsterSpreadCount();
        int spread = Mathf.Clamp(_page, 1, spreads);
        _page = spread;
        int totalCh = AdventureCodex.MaxMonsterChapter();
        int c1 = spread * 2 - 1;
        int c2 = c1 + 1;

        SetPageTitle("怪物图鉴");
        SetPageLabel($"{spread}/{spreads}");

        float y = -PagePad;
        y = SpawnChapterSection(c1, y);
        if (c2 <= totalCh) y = SpawnChapterSection(c2, y);
        y -= BottomBarReserve;
        SetContentHeight(-y + PagePad);
    }

    float SpawnChapterSection(int chapter, float y)
    {
        bool unlocked = AdventureCodex.ChapterUnlocked(chapter);
        string title = GameConfig.GetChapterMapName(chapter);
        y = SpawnSectionBanner(unlocked ? title : $"{title}（未解锁）", y);

        var list = AdventureCodex.MonstersForChapter(chapter);
        float cellW, cellH;
        GetCellSize(out cellW, out cellH);
        int col = 0;
        float rowTop = y;
        float gridW = Cols * cellW + (Cols - 1) * CellGapX;
        float originX = -gridW * 0.5f;

        for (int i = 0; i < list.Count; i++)
        {
            if (col == 0) rowTop = y;
            float x = originX + col * (cellW + CellGapX) + cellW * 0.5f;
            SpawnMonsterCell(list[i], unlocked, new Vector2(x, rowTop - cellH * 0.5f), cellW, cellH);
            col++;
            if (col >= Cols)
            {
                col = 0;
                y -= cellH + CellGapY;
            }
        }
        if (col != 0) y -= cellH + CellGapY;
        return y - SectionGap;
    }

    void RebuildMerc()
    {
        SetPageTitle("佣兵图鉴");
        SetPageLabel("");

        float y = -PagePad;
        for (int r = 0; r < MercPageNames.Length; r++)
            y = SpawnMercSection((MercRosterDefs.MercRarity)r, MercPageNames[r], y);
        y -= BottomBarReserve;
        SetContentHeight(-y + PagePad);
    }

    float SpawnMercSection(MercRosterDefs.MercRarity rarity, string title, float y)
    {
        y = SpawnSectionBanner(title, y);

        float cellW, cellH;
        GetCellSize(out cellW, out cellH);
        int col = 0;
        float rowTop = y;
        float gridW = Cols * cellW + (Cols - 1) * CellGapX;
        float originX = -gridW * 0.5f;

        var list = AdventureLogCatalog.Mercs;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            if (AdventureCodex.GetMercRarity(e) != rarity) continue;
            if (col == 0) rowTop = y;
            float x = originX + col * (cellW + CellGapX) + cellW * 0.5f;
            SpawnMercCell(e, new Vector2(x, rowTop - cellH * 0.5f), cellW, cellH);
            col++;
            if (col >= Cols)
            {
                col = 0;
                y -= cellH + CellGapY;
            }
        }
        if (col != 0) y -= cellH + CellGapY;
        return y - SectionGap;
    }

    void GetCellSize(out float w, out float h)
    {
        w = 100f;
        h = 140f;
        if (_cellTemplate == null) return;
        var rt = _cellTemplate.GetComponent<RectTransform>();
        if (rt != null)
        {
            // 模板可能是零尺寸锚点，取 iconbg + name 估算
            var iconbg = FindDeep(_cellTemplate.transform, "iconbg") as RectTransform;
            var name = FindDeep(_cellTemplate.transform, "name") as RectTransform;
            if (iconbg != null && iconbg.rect.width > 1f) w = iconbg.rect.width;
            float nameH = name != null ? Mathf.Abs(name.anchoredPosition.y) + name.rect.height * 0.5f : 30f;
            h = (iconbg != null ? iconbg.rect.height : 100f) + nameH + 8f;
            if (rt.rect.width > 1f) w = Mathf.Max(w, rt.rect.width);
            if (rt.rect.height > 1f) h = Mathf.Max(h, rt.rect.height);
        }
    }

    void SetContentHeight(float h)
    {
        if (_content == null) return;
        var sd = _content.sizeDelta;
        sd.y = Mathf.Max(180f, h);
        sd.x = 0f;
        _content.sizeDelta = sd;
    }

    void SetPageTitle(string text)
    {
        if (_pageTitle != null) _pageTitle.text = text ?? "";
    }

    void SetPageLabel(string text)
    {
        if (_pageLabel != null) _pageLabel.text = text ?? "";
    }

    float SpawnSectionBanner(string text, float y)
    {
        // 复用格子名条风格：简单 Text 横条
        var go = new GameObject("SectionHeader", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        float width = _content.rect.width > 1f ? _content.rect.width - 20f : 420f;
        rt.sizeDelta = new Vector2(width, HeaderH);
        rt.anchoredPosition = new Vector2(0f, y);

        var img = go.AddComponent<Image>();
        UiKeyedBackgrounds.ApplyLogFrame(img, "标签底", false);
        if (img.sprite == null) img.color = new Color(0.35f, 0.22f, 0.14f, 1f);

        var tGo = new GameObject("Label", typeof(RectTransform));
        tGo.transform.SetParent(go.transform, false);
        var tr = (RectTransform)tGo.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var tx = tGo.AddComponent<Text>();
        tx.font = GameFonts.GetChinese();
        tx.fontSize = 20;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.color = Color.white;
        tx.text = text ?? "";

        _spawned.Add(go);
        return y - HeaderH - 10f;
    }

    void SpawnMonsterCell(AdventureLogCatalog.MonsterEntry e, bool chapterUnlocked, Vector2 pos, float cellW, float cellH)
    {
        var go = Object.Instantiate(_cellTemplate, _content, false);
        go.SetActive(true);
        go.name = "Cell_" + e.Id;
        PlaceCell(go, pos, cellW, cellH);

        bool isBoss = e.Kind == "首领";
        bool seen = chapterUnlocked && AdventureCodex.IsSeenMonster(e.Id);
        bool viewed = AdventureCodex.IsViewedMonster(e.Id);
        ApplyMonsterFrame(go.transform, isBoss);
        ApplyPortrait(go.transform, AdventureCodex.LoadMonsterSprite(e), seen && chapterUnlocked);
        ApplyName(go.transform, (!chapterUnlocked || !seen) ? "？？？" : e.Name);
        ApplyBossTag(go.transform, isBoss && chapterUnlocked);
        ApplyRedDot(go.transform, seen && !viewed);

        WireClick(go, () =>
        {
            if (!chapterUnlocked || !seen)
            {
                UIManager.Instance?.ShowToast(chapterUnlocked ? "尚未遇见这只怪物" : "该章节尚未解锁");
                return;
            }
            AdventureCodex.MarkMonsterViewed(e.Id);
            ApplyRedDot(go.transform, false);
            bool defeated = AdventureCodex.IsDefeatedMonster(e.Id);
            string body = defeated ? e.Desc : e.Lore;
            string tip = defeated
                ? e.Lore
                : "（已遭遇：趣闻已解锁；击败后解锁完整描述）";
            string meta = string.IsNullOrEmpty(e.Kind) ? e.Place : e.Kind + "  ·  " + e.Place;
            CodexInfoPopupUI.Show(e.Name, meta, body, tip, AdventureCodex.LoadMonsterSprite(e));
        });
        _spawned.Add(go);
    }

    void SpawnMercCell(AdventureLogCatalog.MercEntry e, Vector2 pos, float cellW, float cellH)
    {
        var go = Object.Instantiate(_cellTemplate, _content, false);
        go.SetActive(true);
        go.name = "Cell_" + e.Id;
        PlaceCell(go, pos, cellW, cellH);

        var rarity = AdventureCodex.GetMercRarity(e);
        bool unlocked = AdventureLogCatalog.MercUnlocked(e);
        bool seen = unlocked || AdventureCodex.IsSeenMerc(e.Id);
        bool viewed = AdventureCodex.IsViewedMerc(e.Id);
        bool legendary = rarity == MercRosterDefs.MercRarity.Legendary;

        if (legendary)
        {
            ApplyMonsterFrame(go.transform, true);
            var bossFr = FindChildByNames(go.transform, "boss边框");
            var img = bossFr?.GetComponent<Image>();
            if (img != null) img.color = MercRarityColor(rarity);
        }
        else
        {
            ApplyMonsterFrame(go.transform, false);
            ApplyMercFrameTint(go.transform, rarity);
        }
        ApplyPortrait(go.transform, AdventureCodex.LoadMercSprite(e), seen);
        string display = string.IsNullOrEmpty(e.Nickname) ? e.Name : e.Nickname;
        ApplyName(go.transform, seen ? display : "？？？");
        ApplyBossTag(go.transform, legendary && seen);
        var bossLabel = FindDeep(go.transform, "boss")?.GetComponentInChildren<Text>(true);
        if (bossLabel != null && legendary)
            bossLabel.text = "传奇";
        ApplyRedDot(go.transform, seen && !viewed);

        WireClick(go, () =>
        {
            if (!seen)
            {
                UIManager.Instance?.ShowToast("尚未结识该角色");
                return;
            }
            AdventureCodex.MarkMercViewed(e.Id);
            ApplyRedDot(go.transform, false);
            string meta = string.IsNullOrEmpty(e.Role) ? e.Place : e.Role + "  ·  " + e.Place;
            CodexInfoPopupUI.Show(display, meta, e.Desc, e.Lore, AdventureCodex.LoadMercSprite(e));
        });
        _spawned.Add(go);
    }

    void PlaceCell(GameObject go, Vector2 pos, float cellW, float cellH)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        if (rt.sizeDelta.x < 1f || rt.sizeDelta.y < 1f)
            rt.sizeDelta = new Vector2(cellW, cellH);
        rt.anchoredPosition = pos;
    }

    void ApplyMonsterFrame(Transform cell, bool boss)
    {
        var normal = FindChildByNames(cell, "普通边框");
        var bossFr = FindChildByNames(cell, "boss边框");
        if (normal != null) normal.gameObject.SetActive(!boss);
        if (bossFr != null) bossFr.gameObject.SetActive(boss);
        // 复原染色
        var img = (boss ? bossFr : normal)?.GetComponent<Image>();
        if (img != null) img.color = Color.white;
    }

    void ApplyMercFrameTint(Transform cell, MercRosterDefs.MercRarity rarity)
    {
        var normal = FindChildByNames(cell, "普通边框");
        var bossFr = FindChildByNames(cell, "boss边框");
        if (bossFr != null) bossFr.gameObject.SetActive(false);
        if (normal != null)
        {
            normal.gameObject.SetActive(true);
            var img = normal.GetComponent<Image>();
            if (img != null) img.color = MercRarityColor(rarity);
        }
    }

    static Color MercRarityColor(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Rare:
                return new Color(0.55f, 0.78f, 1f, 1f);
            case MercRosterDefs.MercRarity.Legendary:
                return new Color(1f, 0.82f, 0.28f, 1f);
            default:
                return Color.white;
        }
    }

    void ApplyPortrait(Transform cell, Sprite sp, bool lit)
    {
        var icon = FindDeep(cell, "icon")?.GetComponent<Image>();
        if (icon == null) return;
        icon.sprite = sp;
        icon.enabled = sp != null;
        icon.preserveAspect = true;
        icon.color = lit ? Color.white : new Color(0f, 0f, 0f, 0.92f);
    }

    void ApplyName(Transform cell, string text)
    {
        var name = FindDeep(cell, "name")?.GetComponent<Text>();
        if (name != null) name.text = text ?? "";
    }

    void ApplyBossTag(Transform cell, bool on)
    {
        var boss = FindDeep(cell, "boss");
        if (boss != null) boss.gameObject.SetActive(on);
    }

    void ApplyRedDot(Transform cell, bool on)
    {
        var red = FindDeep(cell, "RedDot");
        if (red == null)
        {
            // 在 iconbg 右上角挂一个
            var iconbg = FindDeep(cell, "iconbg");
            if (iconbg == null || !on) return;
            var go = new GameObject("RedDot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(iconbg, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            rt.sizeDelta = new Vector2(16f, 16f);
            var img = go.GetComponent<Image>();
            img.sprite = RedDot.Sprite;
            img.color = Color.white;
            red = go.transform;
        }
        red.gameObject.SetActive(on);
    }

    void WireClick(GameObject go, UnityEngine.Events.UnityAction action)
    {
        var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    void OpenDetail(bool merc, string id, string title, string meta, string place,
        string desc, string lore, bool bossOrLegend, Sprite portrait)
    {
        _selectedId = id;
        _selectedIsMerc = merc;
        _selectedBossOrLegend = bossOrLegend;

        if (_detailRoot != null) _detailRoot.SetActive(true);
        if (_detailTitle != null) _detailTitle.text = title ?? "";
        if (_detailDesc != null) _detailDesc.text = desc ?? "";
        if (_detailObj != null) _detailObj.text = string.IsNullOrEmpty(meta) ? place : meta + "  ·  " + place;
        if (_detailProg != null) _detailProg.text = lore ?? "";
        if (_detailMaskImg != null && portrait != null)
        {
            _detailMaskImg.sprite = portrait;
            _detailMaskImg.preserveAspect = true;
            _detailMaskImg.color = Color.white;
        }

        // 资源奖走日志里程等级；图鉴详情只展示文案
        if (_claimBtn != null)
            _claimBtn.gameObject.SetActive(false);
    }

    void HideDetail()
    {
        _selectedId = null;
        if (_detailRoot != null) _detailRoot.SetActive(false);
        if (_claimBtn != null) _claimBtn.gameObject.SetActive(false);
    }

    void OnClaimReward()
    {
        // 兼容旧按钮：改领日志里程
        int lv = AdventureLogMileage.FirstClaimableLevel();
        if (lv <= 0)
        {
            UIManager.Instance?.ShowToast("暂无可领里程等级");
            return;
        }
        if (!AdventureLogMileage.ClaimLevel(lv))
        {
            UIManager.Instance?.ShowToast("领取失败");
            return;
        }
        UIManager.Instance?.ShowToast($"已领取日志里程 Lv{lv}");
        if (_claimLabel != null) _claimLabel.text = "已领取";
        if (_claimBtn != null) _claimBtn.interactable = false;
    }

    void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Object.Destroy(_spawned[i]);
        _spawned.Clear();
    }

    static Transform CreateTinyBtn(Transform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(40f, 32f);
        go.GetComponent<Image>().color = new Color(0.4f, 0.28f, 0.18f, 0.95f);
        var tGo = new GameObject("T", typeof(RectTransform));
        tGo.transform.SetParent(go.transform, false);
        var tr = (RectTransform)tGo.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        var tx = tGo.AddComponent<Text>();
        tx.font = GameFonts.GetChinese();
        tx.fontSize = 20;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.color = Color.white;
        tx.text = label;
        return go.transform;
    }

    static Transform FindChildByNames(Transform root, params string[] names)
    {
        if (root == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            var t = FindDeep(root, names[i]);
            if (t != null) return t;
        }
        return null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
