using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇商店页（2026-09-15 新增）。
///
/// 入口：公会大厅的 ShopButton（原本是未接通入口，被 HideUnfinishedHallButtons 隐藏，现已接通）。
/// 定位：**花钱买时间**——商店不卖任何独占内容，只让你比纯推图更早拿到。
/// 技能货架上的每个技能都能通过章节兜底免费获得，买断只是提前。
///
/// 四个货架：技能解锁 / 抽卡券 / 资源补给 / 材料。
/// 购买与发货逻辑全在 <see cref="ShopSystem"/>，本类只管显示与点击。
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    GameObject _root;
    Text _walletText;
    Text _resultText;
    Transform _content;
    ShopDefs.Kind _kind = ShopDefs.Kind.Skill;

    readonly List<Row> _rows = new List<Row>();
    readonly List<Button> _tabButtons = new List<Button>();
    readonly List<Text> _tabLabels = new List<Text>();

    class Row
    {
        public GameObject go;
        public Image bg;
        public Text nameText;
        public Text descText;
        public Text limitText;
        public Button btn;
        public Text btnLabel;
        public string itemId;
    }

    // ============================================================
    // 生命周期
    // ============================================================

    public static void Show()
    {
        Ensure().Open();
    }

    public static ShopUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ShopUI", typeof(RectTransform));
        UnityEngine.Object.DontDestroyOnLoad(go);
        var ui = go.AddComponent<ShopUI>();
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
    // 构建（纯代码，不依赖预制体）
    // ============================================================

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateImg(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

        var panel = CreateImg(_root.transform, "Panel", new Color(0.09f, 0.08f, 0.10f, 0.98f));
        SetRect(panel.rectTransform, 0.5f, 0.5f, 0f, 0f, 1000f, 1180f);

        var title = CreateTxt(panel.transform, "Title", "冒险者商店", 40, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.955f, 0f, 0f, 520f, 52f);

        _walletText = CreateTxt(panel.transform, "Wallet", "", 22, TextAnchor.MiddleCenter);
        SetRect(_walletText.rectTransform, 0.5f, 0.912f, 0f, 0f, 940f, 32f);
        _walletText.color = new Color(1f, 0.86f, 0.5f);

        var hint = CreateTxt(panel.transform, "Hint",
            "商店只卖时间，不卖独占内容——所有商品都能通过推图免费拿到", 18, TextAnchor.MiddleCenter);
        SetRect(hint.rectTransform, 0.5f, 0.878f, 0f, 0f, 940f, 26f);
        hint.color = new Color(0.66f, 0.72f, 0.84f);

        BuildTabs(panel.transform);

        var close = CreateBtn(panel.transform, "CloseButton", "关闭", new Vector2(440f, 540f), new Vector2(110f, 52f));
        close.onClick.AddListener(Hide);

        // 结果提示（抽卡结果可能很长，单起一行）
        _resultText = CreateTxt(panel.transform, "Result", "", 20, TextAnchor.MiddleCenter);
        SetRect(_resultText.rectTransform, 0.5f, 0.5f, 0f, -520f, 940f, 70f);
        _resultText.color = new Color(0.66f, 0.95f, 0.7f);

        // 滚动列表
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(panel.transform, false);
        SetRect(scrollGo.GetComponent<RectTransform>(), 0.5f, 0.5f, 0f, -55f, 940f, 830f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        Stretch(viewportGo.GetComponent<RectTransform>());
        var vpImg = viewportGo.GetComponent<Image>();
        vpImg.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = true;

        _content = new GameObject("Content", typeof(RectTransform)).transform;
        _content.SetParent(viewportGo.transform, false);
        var contentRt = _content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewportGo.GetComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = viewportGo.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        BuildRows();

        _root.SetActive(false);
    }

    void BuildTabs(Transform panel)
    {
        var kinds = ShopDefs.Kinds;
        float startX = -352f;
        for (int i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            var btn = CreateBtn(panel.transform, "Tab_" + kind, ShopDefs.KindName(kind),
                new Vector2(startX + i * 184f, 400f), new Vector2(170f, 60f));
            btn.onClick.AddListener(() => OnClickTab(kind));
            _tabButtons.Add(btn);
            _tabLabels.Add(btn.GetComponentInChildren<Text>());
        }
    }

    void BuildRows()
    {
        for (int i = 0; i < ShopDefs.All.Length; i++)
            _rows.Add(CreateRow(_content, ShopDefs.All[i]));
    }

    Row CreateRow(Transform parent, ShopDefs.Item item)
    {
        var bg = CreateImg(parent, "Item_" + item.id, new Color(0.17f, 0.15f, 0.20f, 1f));
        bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 152f;
        var rt = bg.rectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 152f);

        var c = new Row { go = bg.gameObject, bg = bg, itemId = item.id };

        c.nameText = CreateTxt(bg.transform, "Name", item.name, 26, TextAnchor.MiddleLeft);
        SetRect(c.nameText.rectTransform, 0f, 1f, 24f, -24f, 600f, 34f);
        c.nameText.rectTransform.pivot = new Vector2(0f, 1f);

        bool isSkillRow = !string.IsNullOrEmpty(item.skillId);
        if (isSkillRow)
        {
            var rarEnum = SkillDraftMeta.Rarity(item.skillId);
            c.nameText.text = $"{SkillRarityUtil.DisplayName(rarEnum)} · {item.name}";
            c.nameText.color = SkillRarityUtil.Tint(rarEnum);
        }

        c.descText = CreateTxt(bg.transform, "Desc", item.desc.Replace("\n", "　"), 18, TextAnchor.UpperLeft);
        SetRect(c.descText.rectTransform, 0f, 1f, 24f, -62f, 620f, 74f);
        c.descText.rectTransform.pivot = new Vector2(0f, 1f);
        c.descText.color = new Color(0.78f, 0.82f, 0.9f);

        c.limitText = CreateTxt(bg.transform, "Limit", "", 18, TextAnchor.MiddleLeft);
        SetRect(c.limitText.rectTransform, 0f, 0f, 24f, 18f, 600f, 26f);
        c.limitText.rectTransform.pivot = new Vector2(0f, 0f);
        c.limitText.color = new Color(0.72f, 0.68f, 0.55f);

        c.btn = CreateBtn(bg.transform, "BuyBtn", "购买", new Vector2(0f, 0f), new Vector2(180f, 58f));
        SetRect(c.btn.GetComponent<RectTransform>(), 1f, 0.5f, -110f, 0f, 180f, 58f);
        c.btnLabel = c.btn.GetComponentInChildren<Text>();
        c.btn.onClick.AddListener(() => OnClickBuy(c));
        return c;
    }

    // ============================================================
    // 打开 / 刷新
    // ============================================================

    void Open()
    {
        if (_root == null) Build();
        if (_resultText != null) _resultText.text = "";
        RedDot.Set(RedDot.Shop, false);
        Refresh();
        _root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    public static bool IsOpen => Instance != null && Instance._root != null && Instance._root.activeSelf;

    void OnClickTab(ShopDefs.Kind kind)
    {
        if (_kind == kind) return;
        _kind = kind;
        if (_resultText != null) _resultText.text = "";
        Refresh();
    }

    void Refresh()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        long diamond = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);
        long stone = ResourceWallet.Get(data, ResourceWallet.ResourceType.TalentPoint);
        if (_walletText != null)
            _walletText.text = $"金币 {gold}　钻石 {diamond}　天赋石 {stone}";

        for (int i = 0; i < _tabButtons.Count; i++)
        {
            bool on = (int)_kind == i;
            if (_tabLabels[i] != null)
                _tabLabels[i].color = on ? new Color(1f, 0.86f, 0.45f) : new Color(0.7f, 0.72f, 0.8f);
            if (_tabLabels[i] != null)
                _tabLabels[i].fontSize = on ? 26 : 22;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var item = ShopDefs.Get(row.itemId);
            if (item == null) continue;

            bool show = item.kind == _kind;
            row.go.SetActive(show);
            if (!show) continue;

            bool onShelf = ShopSystem.IsOnShelf(item, data);
            bool can = ShopSystem.CanBuy(item, data, out string reason);
            int left = ShopSystem.RemainingToday(item, data);

            if (row.limitText != null)
            {
                string limit = item.dailyLimit > 0
                    ? $"今日限购 {item.dailyLimit} 次　剩余 {left}"
                    : "不限购";
                if (item.kind == ShopDefs.Kind.Fragment)
                    limit = $"碎片 {SkillFragmentSystem.ProgressText(item.skillId)}　{limit}";
                row.limitText.text = limit;
            }

            if (!onShelf)
            {
                row.bg.color = new Color(0.13f, 0.12f, 0.15f, 1f);
                SetBtn(row, $"第{item.showFromChapter}章上架", false, new Color(0.5f, 0.48f, 0.5f, 1f));
            }
            else if (!can)
            {
                row.bg.color = new Color(0.15f, 0.14f, 0.16f, 1f);
                SetBtn(row, reason, false, new Color(0.55f, 0.5f, 0.5f, 1f));
            }
            else
            {
                row.bg.color = new Color(0.17f, 0.15f, 0.20f, 1f);
                SetBtn(row, $"{item.price} {ShopDefs.CurrencyName(item.currency)}", true,
                    new Color(0.98f, 0.85f, 0.4f, 1f));
            }
        }
    }

    void SetBtn(Row row, string label, bool interactable, Color textColor)
    {
        if (row.btn != null) row.btn.interactable = interactable;
        if (row.btnLabel != null)
        {
            row.btnLabel.text = label;
            row.btnLabel.color = textColor;
        }
    }

    void OnClickBuy(Row row)
    {
        var item = ShopDefs.Get(row.itemId);
        if (item == null) return;

        var result = ShopSystem.Buy(item);
        if (!result.ok)
        {
            GlobalToastUI.Show(result.message);
            return;
        }

        if (_resultText != null) _resultText.text = result.message;
        GlobalToastUI.Show(item.kind == ShopDefs.Kind.Gacha ? "抽卡完成" : "购买成功");
        Debug.Log($"[Shop] 购买 {item.id}，结果：{result.message}");
        Refresh();
    }

    // ============================================================
    // UI 小工具
    // ============================================================

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
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
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
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
