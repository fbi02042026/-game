using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色界面「看板」选择弹窗（运行时代码建树，不改任何预制体）
/// 列出「已永久解锁的佣兵」（走 SaveSystem.Data.IsMercUnlocked），头像 + 名字 + 职业 + 稀有度
/// 当前生效项打勾；点击即设为看板立绘。另有「恢复默认」换回玩家自己
/// 选择持久化到 PlayerPrefs（见 CharacterUI.BoardKey）；后续建议迁入 SaveData
/// 风格对齐刚新建的 BackpackPopupUI：全屏遮罩、关闭按钮、ScrollRect 列表
/// </summary>
public class BoardSelectPopupUI : MonoBehaviour
{
    public static BoardSelectPopupUI Instance { get; private set; }

    const float PanelW = 560f;
    const float PanelH = 640f;

    /// <summary>玩家自己的看板 key，必须与 CharacterUI.BoardKey 的默认值一致。</summary>
    const string PlayerBoardId = "player";

    GameObject _dismiss;
    GameObject _panel;
    // 每行：hireId + 打勾文本引用，方便刷新勾选
    readonly List<RowRef> _rows = new List<RowRef>();
    ScrollRect _scroll;
    Transform _list;
    string _current;

    class RowRef
    {
        public string hireId;
        public Text check;
    }

    /// <summary>重复调用返回同一实例；挂在 parent 下（建议 CharacterUI.transform）。</summary>
    public static BoardSelectPopupUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        if (parent == null) return null;

        var go = new GameObject("BoardSelectPopupUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        Instance = go.AddComponent<BoardSelectPopupUI>();
        Instance.Build();
        return Instance;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void Build()
    {
        // 全屏透明遮罩：点它即取消
        _dismiss = new GameObject("Dismiss", typeof(RectTransform), typeof(Image));
        _dismiss.transform.SetParent(transform, false);
        var dImg = _dismiss.GetComponent<Image>();
        dImg.color = new Color(0f, 0f, 0f, 0.55f);
        dImg.raycastTarget = true;
        Stretch(_dismiss.GetComponent<RectTransform>());
        var dBtn = _dismiss.AddComponent<Button>();
        dBtn.transition = Selectable.Transition.None;
        dBtn.onClick.AddListener(Hide);

        // 弹窗底框：复用现有面板图（角色页背包同款），拿不到就退化为纯色面板
        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        var pImg = _panel.GetComponent<Image>();
        var frame = LoadFrameSprite();
        if (frame != null)
        {
            pImg.sprite = frame;
            pImg.type = Image.Type.Sliced;
            pImg.color = Color.white;
        }
        else
        {
            pImg.color = new Color(0.13f, 0.12f, 0.16f, 0.98f);
        }
        var pRt = _panel.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(PanelW, PanelH);

        // 标题
        var title = MakeText(_panel.transform, "Title", "选择看板", 26, TextAnchor.MiddleCenter);
        var tRt = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -28f);
        tRt.sizeDelta = new Vector2(0f, 40f);

        // 关闭
        var close = MakeButton(_panel.transform, "Close", "×", new Vector2(PanelW * 0.5f - 28f, -28f), new Vector2(40f, 40f));
        close.onClick.AddListener(Hide);

        // 列表
        var listWrap = new GameObject("ListWrap", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        listWrap.transform.SetParent(_panel.transform, false);
        var lwImg = listWrap.GetComponent<Image>();
        lwImg.color = new Color(0.1f, 0.09f, 0.13f, 0.6f);
        var lwMask = listWrap.GetComponent<Mask>();
        lwMask.showMaskGraphic = false;
        var lwRt = listWrap.GetComponent<RectTransform>();
        lwRt.anchorMin = new Vector2(0f, 0f);
        lwRt.anchorMax = new Vector2(1f, 1f);
        lwRt.offsetMin = new Vector2(12f, 12f);
        lwRt.offsetMax = new Vector2(-12f, -132f);

        _scroll = listWrap.GetComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.elasticity = 0.08f;

        var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        list.transform.SetParent(listWrap.transform, false);
        Stretch(list.GetComponent<RectTransform>());
        _list = list.transform;

        var vlg = list.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = list.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _scroll.content = list.GetComponent<RectTransform>();
        _scroll.viewport = listWrap.GetComponent<RectTransform>();

        // 恢复默认（玩家自己）：必须在 ListWrap 之后创建，
        // 否则同层级的 ListWrap（Image raycast 开着）会盖住它导致点不动、换不回玩家。
        var reset = MakeButton(_panel.transform, "Reset", "恢复默认（玩家）", new Vector2(0f, -92f), new Vector2(PanelW - 24f, 40f));
        reset.onClick.AddListener(() => SelectBoard(PlayerBoardId));
        reset.transform.SetAsLastSibling();

        GameFonts.ApplyToHierarchy(transform);
    }

    void Refresh()
    {
        if (_list == null) return;

        // 清空旧行
        for (int i = _list.childCount - 1; i >= 0; i--)
            Destroy(_list.GetChild(i).gameObject);
        _rows.Clear();

        _current = CharacterUI.Instance != null ? CharacterUI.Instance.GetCurrentBoardId() : PlayerBoardId;

        // 玩家自己永远排第一：候选集合不能只有佣兵，否则换成佣兵后换不回来
        AddPlayerRow();

        // 已解锁佣兵（排在玩家之后）
        var data = SaveSystem.Instance?.Data;
        var all = MercRosterDefs.All;
        for (int i = 0; i < all.Count; i++)
        {
            var def = all[i];
            if (data == null || !data.IsMercUnlocked(def.HireId))
                continue;
            AddMercRow(def);
        }

        if (_rows.Count == 0)
        {
            var empty = MakeText(_list, "Empty", "（暂无可解锁的佣兵）", 18, TextAnchor.MiddleCenter);
            var ert = empty.GetComponent<RectTransform>();
            ert.anchorMin = new Vector2(0f, 1f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(0.5f, 1f);
            ert.sizeDelta = new Vector2(0f, 40f);
            empty.color = new Color(0.7f, 0.7f, 0.75f, 1f);
        }
    }

    /// <summary>
    /// 玩家自己这一行，永远排第一。id 用 "player"，与 CharacterUI.BoardKey 的默认值一致，
    /// 这样「默认 = 玩家」且随时能切回玩家。
    /// </summary>
    void AddPlayerRow()
    {
        AddMercRow(PlayerBoardId, PlayerIdentity.DisplayName,
                   "本命 · 冒险者", new Color(1f, 0.85f, 0.45f, 1f),
                   MercPortraitSprites.GetHead(PlayerBoardId));
    }

    void AddMercRow(MercRosterDefs.Def def)
    {
        if (string.IsNullOrEmpty(def.HireId)) return;   // Def 是 struct，不能判 null
        string rarityText;
        Color rarityCol;
        switch (def.Rarity)
        {
            case MercRosterDefs.MercRarity.Legendary:
                rarityText = "传说 · " + def.JobName; rarityCol = new Color(1f, 0.78f, 0.25f, 1f); break;
            case MercRosterDefs.MercRarity.Rare:
                rarityText = "稀有 · " + def.JobName; rarityCol = new Color(0.45f, 0.7f, 1f, 1f); break;
            default:
                rarityText = "普通 · " + def.JobName; rarityCol = new Color(0.85f, 0.85f, 0.9f, 1f); break;
        }
        AddMercRow(def.HireId, def.Name, rarityText, rarityCol, MercPortraitSprites.GetHead(def.HireId));
    }

    void AddMercRow(string hireId, string displayName, string jobText, Color jobColor, Sprite headSprite)
    {
        if (string.IsNullOrEmpty(hireId)) return;
        var row = new GameObject("Row_" + hireId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.transform.SetParent(_list, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 72f);
        var img = row.GetComponent<Image>();
        img.color = new Color(0.2f, 0.18f, 0.24f, 0.9f);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 72f;
        le.flexibleWidth = 1f;

        // 头像
        var head = new GameObject("Head", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        head.transform.SetParent(rt, false);
        var hrt = head.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0.5f);
        hrt.anchorMax = new Vector2(0f, 0.5f);
        hrt.pivot = new Vector2(0f, 0.5f);
        hrt.anchoredPosition = new Vector2(42f, 0f);
        hrt.sizeDelta = new Vector2(56f, 56f);
        var hImg = head.GetComponent<Image>();
        if (headSprite != null)
        {
            hImg.sprite = headSprite;
            hImg.preserveAspect = true;
            hImg.enabled = true;
        }
        // 头像不拦截点击，整行点击交给行 Button
        hImg.raycastTarget = false;

        // 名字
        var name = MakeText(rt, "Name", displayName, 20, TextAnchor.MiddleLeft);
        var nrt = name.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = new Vector2(80f, 0f);
        nrt.offsetMax = new Vector2(-70f, -4f);

        // 职业 + 稀有度（文本与颜色由调用方按 Def 算好；玩家行走玩家自己的一套）
        var job = MakeText(rt, "Job", jobText, 16, TextAnchor.MiddleLeft);
        var jrt = job.GetComponent<RectTransform>();
        jrt.anchorMin = new Vector2(0f, 0f);
        jrt.anchorMax = new Vector2(1f, 0.5f);
        jrt.offsetMin = new Vector2(80f, 4f);
        jrt.offsetMax = new Vector2(-70f, 0f);
        job.color = jobColor;

        // 打勾
        var check = MakeText(rt, "Check", _current == hireId ? "\u2713" : "", 26, TextAnchor.MiddleCenter);
        var crt2 = check.GetComponent<RectTransform>();
        crt2.anchorMin = new Vector2(1f, 0f);
        crt2.anchorMax = new Vector2(1f, 1f);
        crt2.pivot = new Vector2(1f, 0.5f);
        crt2.offsetMin = new Vector2(-60f, 0f);
        crt2.offsetMax = new Vector2(-10f, 0f);
        check.color = new Color(0.5f, 1f, 0.6f, 1f);

        var rowRef = new RowRef { hireId = hireId, check = check };
        _rows.Add(rowRef);

        var btn = row.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => SelectBoard(hireId));
    }

    void SelectBoard(string id)
    {
        if (CharacterUI.Instance != null)
            CharacterUI.Instance.ApplyBoardSelection(id);
        _current = id;
        // 刷新勾选态，保持弹窗打开供预览
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i].check != null)
                _rows[i].check.text = _rows[i].hireId == id ? "\u2713" : "";
    }

    static Sprite LoadFrameSprite()
    {
        // 复用角色页背包面板同款底框（运行时代码加载，不改美术资源）
        var sp = Resources.Load<Sprite>("UI/NavCharacter/角色_0002s_0009_图层-6-拷贝");
        if (sp != null) return sp;
        return null;
    }

    static Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.3f, 0.28f, 0.34f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size ?? new Vector2(100f, 42f);

        var txt = MakeText(go.transform, "Text", label, 22, TextAnchor.MiddleCenter);
        Stretch(txt.GetComponent<RectTransform>());
        txt.text = label;

        return go.AddComponent<Button>();
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
