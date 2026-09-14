using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆「佣兵名册」解锁页（2026-09-14 改版）。
///
/// 旧规则：酒馆直接招募佣兵 → 进 hiredMercs → 下本出战。
/// 新规则：**酒馆只做解锁**，花金币把佣兵解锁进「招募池」；
/// 真正的招募挪进了战斗内的「佣兵三选一」（见 DraftPool.CollectRecruitMercs）。
///
/// 解锁门槛：普通无条件；稀有需酒馆 Lv2；传说需酒馆 Lv3（让酒馆升级仍有意义）。
/// </summary>
public class TavernUnlockUI : MonoBehaviour
{
    public static TavernUnlockUI Instance { get; private set; }

    GameObject _root;
    Text _countText;
    Text _hintText;
    Transform _content;
    readonly List<Cell> _cells = new List<Cell>();

    class Cell
    {
        public GameObject go;
        public Image bg;
        public Text nameText;
        public Text jobText;
        public Text rarityText;
        public Button btn;
        public Text btnLabel;
        public string hireId;
    }

    public static void Show()
    {
        Ensure().Open();
    }

    public static TavernUnlockUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TavernUnlockUI", typeof(RectTransform));
        UnityEngine.Object.DontDestroyOnLoad(go);
        var ui = go.AddComponent<TavernUnlockUI>();
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
        SetRect(panel.rectTransform, 0.5f, 0.5f, 0f, 0f, 980f, 1200f);

        var title = CreateTxt(panel.transform, "Title", "佣兵名册", 40, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.93f, 0f, 0f, 520f, 52f);

        _countText = CreateTxt(panel.transform, "Count", "已解锁 0/0", 24, TextAnchor.MiddleCenter);
        SetRect(_countText.rectTransform, 0.5f, 0.885f, 0f, 0f, 520f, 34f);
        _countText.color = new Color(1f, 0.86f, 0.5f);

        _hintText = CreateTxt(panel.transform, "Hint",
            "解锁后的佣兵会出现在战斗内的「佣兵」三选一中", 20, TextAnchor.MiddleCenter);
        SetRect(_hintText.rectTransform, 0.5f, 0.85f, 0f, 0f, 900f, 30f);
        _hintText.color = new Color(0.72f, 0.78f, 0.9f);

        var close = CreateBtn(panel.transform, "CloseButton", "关闭", new Vector2(430f, 545f), new Vector2(110f, 52f));
        close.onClick.AddListener(Hide);

        // 滚动列表
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(panel.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        SetRect(scrollRt, 0.5f, 0.5f, 0f, -30f, 920f, 940f);

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

        var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(280f, 300f);
        grid.spacing = new Vector2(12f, 12f);
        grid.padding = new RectOffset(16, 16, 16, 16);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewportGo.GetComponent<ScrollRect>();
        scroll.content = contentRt;
        scroll.viewport = viewportGo.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        BuildCells();
        _root.SetActive(false);
    }

    void BuildCells()
    {
        var roster = MercRosterDefs.All;
        for (int i = 0; i < roster.Count; i++)
            _cells.Add(CreateCell(_content, roster[i]));
    }

    Cell CreateCell(Transform parent, MercRosterDefs.Def def)
    {
        var bg = CreateImg(parent, "Merc_" + def.HireId, new Color(0.17f, 0.15f, 0.20f, 1f));
        var c = new Cell { go = bg.gameObject, bg = bg, hireId = def.HireId };

        c.nameText = CreateTxt(bg.transform, "Name", $"{def.Name}·{def.Nickname}", 26, TextAnchor.MiddleCenter);
        SetRect(c.nameText.rectTransform, 0.5f, 0.86f, 0f, 0f, 250f, 34f);

        c.jobText = CreateTxt(bg.transform, "Job", def.JobName, 20, TextAnchor.MiddleCenter);
        SetRect(c.jobText.rectTransform, 0.5f, 0.76f, 0f, 0f, 250f, 28f);
        c.jobText.color = new Color(0.78f, 0.84f, 0.95f);

        c.rarityText = CreateTxt(bg.transform, "Rarity", RarityName(def.Rarity), 20, TextAnchor.MiddleCenter);
        SetRect(c.rarityText.rectTransform, 0.5f, 0.67f, 0f, 0f, 250f, 28f);
        c.rarityText.color = RarityColor(def.Rarity);

        c.btn = CreateBtn(bg.transform, "UnlockBtn", "解锁", new Vector2(0f, -95f), new Vector2(220f, 52f));
        c.btnLabel = c.btn.GetComponentInChildren<Text>();
        c.btn.onClick.AddListener(() => OnClickUnlock(c));
        return c;
    }

    // ============================================================
    // 打开 / 刷新
    // ============================================================

    void Open()
    {
        if (_root == null) Build();
        Refresh();
        _root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Refresh()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.unlockedMercIds == null) data.unlockedMercIds = new HashSet<string>();

        int tavern = data.townLevel != null ? Mathf.Max(1, data.townLevel.tavern) : 1;
        long gold = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
        int total = MercRosterDefs.All.Count;

        if (_countText != null)
            _countText.text = $"已解锁 {data.unlockedMercIds.Count}/{total}　金币 {gold}";

        for (int i = 0; i < _cells.Count; i++)
        {
            var c = _cells[i];
            if (!MercRosterDefs.TryGetByHireId(c.hireId, out var def)) continue;

            bool unlocked = data.IsMercUnlocked(def.HireId);
            bool gateOpen = IsGateOpen(def, tavern);
            int cost = UnlockCost(def);

            if (unlocked)
            {
                c.bg.color = new Color(0.14f, 0.22f, 0.16f, 1f);
                SetBtn(c, "已在招募池", false, new Color(0.42f, 0.62f, 0.42f, 1f));
            }
            else if (!gateOpen)
            {
                c.bg.color = new Color(0.17f, 0.15f, 0.20f, 1f);
                SetBtn(c, $"需酒馆 Lv{RequiredTavernLevel(def)}", false, new Color(0.5f, 0.45f, 0.5f, 1f));
            }
            else
            {
                c.bg.color = new Color(0.17f, 0.15f, 0.20f, 1f);
                bool afford = gold >= cost;
                SetBtn(c, $"解锁 {cost}", afford,
                    afford ? new Color(0.95f, 0.78f, 0.35f, 1f) : new Color(0.55f, 0.5f, 0.4f, 1f));
            }
        }
    }

    void SetBtn(Cell c, string label, bool interactable, Color textColor)
    {
        if (c.btn != null) c.btn.interactable = interactable;
        if (c.btnLabel != null)
        {
            c.btnLabel.text = label;
            c.btnLabel.color = textColor;
        }
    }

    void OnClickUnlock(Cell c)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (!MercRosterDefs.TryGetByHireId(c.hireId, out var def)) return;

        int tavern = data.townLevel != null ? Mathf.Max(1, data.townLevel.tavern) : 1;
        if (!IsGateOpen(def, tavern))
        {
            GlobalToastUI.Show($"需要酒馆 Lv{RequiredTavernLevel(def)}");
            return;
        }

        int cost = UnlockCost(def);
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, cost, save: false, notify: false))
        {
            GlobalToastUI.Show("金币不足");
            return;
        }

        data.UnlockMerc(def.HireId);
        AdventureCodex.MarkMercSeen(def.HireId);
        SaveSystem.Instance.Save();
        GlobalToastUI.Show($"{def.Name} 已加入招募池");
        Debug.Log($"[TavernUnlock] 解锁佣兵 {def.HireId} {def.Name}，花费 {cost} 金币");
        Refresh();
    }

    // ============================================================
    // 规则
    // ============================================================

    /// <summary>解锁价格：用花名册的 RecruitGold，为 0 时按稀有度兜底。</summary>
    public static int UnlockCost(MercRosterDefs.Def def)
    {
        if (def.RecruitGold > 0) return def.RecruitGold;
        switch (def.Rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 5000;
            case MercRosterDefs.MercRarity.Rare: return 1500;
            default: return 500;
        }
    }

    /// <summary>酒馆等级门槛：普通无条件，稀有 Lv2，传说 Lv3。</summary>
    public static int RequiredTavernLevel(MercRosterDefs.Def def)
    {
        switch (def.Rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 3;
            case MercRosterDefs.MercRarity.Rare: return 2;
            default: return 1;
        }
    }

    public static bool IsGateOpen(MercRosterDefs.Def def, int tavernLevel)
    {
        return tavernLevel >= RequiredTavernLevel(def);
    }

    static string RarityName(MercRosterDefs.MercRarity r)
    {
        switch (r)
        {
            case MercRosterDefs.MercRarity.Legendary: return "传说";
            case MercRosterDefs.MercRarity.Rare: return "稀有";
            default: return "普通";
        }
    }

    static Color RarityColor(MercRosterDefs.MercRarity r)
    {
        switch (r)
        {
            case MercRosterDefs.MercRarity.Legendary: return new Color(1f, 0.72f, 0.32f);
            case MercRosterDefs.MercRarity.Rare: return new Color(0.58f, 0.78f, 1f);
            default: return new Color(0.82f, 0.86f, 0.92f);
        }
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
