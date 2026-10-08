using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽奖概率公示弹窗（2026-10-05 主人拍板）。
///
/// 主人原话：「保底这个不用写到明面上，但是概率需要告诉玩家……可以再（抽）按钮旁边加个感叹号之类的，
/// 点击弹出弹窗上面写的概率」；「太复杂了 直接说佣兵技能装备分别占多少百分比 然后稀有和传奇的占多少就行」。
///
/// <para>所以弹窗只有两段，别的都不写：<br/>
/// ① <b>抽到什么</b>：技能 / 佣兵 / 装备 各占百分之多少；<br/>
/// ② <b>什么品质</b>：传说 / 稀有 / 普通 / 金币 各占百分之多少（没有「史诗」这一档）。</para>
///
/// <para>概率**不写死**，按 <see cref="DraftPool.BuildOddsRows"/> 从当前真实卡池现算 ——
/// 玩家解锁的技能变多、佣兵池变了，弹窗上的数字会跟着变，永远是真的。
/// 保底定序一个字都不提（主人要求）。</para>
///
/// <para>点「!」开、点「关闭」或遮罩关。全程运行时建树，不碰 prefab；
/// 沿用项目惯例挂 BattleUI 下做子 Canvas（<see cref="GameConfig.SORT_POPUP"/> + GraphicRaycaster）。</para>
/// </summary>
public class DraftOddsPopupUI : MonoBehaviour
{
    const float PanelW = 620f;
    const float PanelH = 560f;

    const float RowH = 36f;
    const float HeadH = 34f;
    const float SectionGap = 12f;

    static DraftOddsPopupUI _inst;
    GameObject _root;

    public static void Toggle()
    {
        var ui = Ensure();
        if (ui == null) return;
        // ⚠ 必须调实例方法 Close：静态 Hide() 用实例点出来会报 CS0176
        if (ui._root != null && ui._root.activeSelf) ui.Close();
        else ui.Build();
    }

    public static void Hide()
    {
        if (_inst == null || _inst._root == null) return;
        _inst.Close();
    }

    static DraftOddsPopupUI Ensure()
    {
        if (_inst != null) return _inst;

        Transform parent = BattleUI.Instance != null ? BattleUI.Instance.transform : null;
        if (parent == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null)
        {
            Debug.LogError("[DraftOddsPopup] 找不到 UI 父节点");
            return null;
        }

        var go = new GameObject("DraftOddsPopup", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        _inst = go.AddComponent<DraftOddsPopupUI>();
        return _inst;
    }

    void Build()
    {
        if (_root != null) Destroy(_root);
        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);

        var rootRt = _root.GetComponent<RectTransform>();
        Stretch(rootRt);
        var c = _root.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        c.sortingOrder = GameConfig.SORT_POPUP;
        if (_root.GetComponent<GraphicRaycaster>() == null)
            _root.AddComponent<GraphicRaycaster>();

        // 遮罩：点它就关（非模态，不逼玩家做决定）
        var veilImg = CreateImage("Veil", _root.transform, new Color(0f, 0f, 0f, 0.55f));
        Stretch(veilImg.rectTransform);
        var veilBtn = veilImg.gameObject.AddComponent<Button>();
        veilBtn.targetGraphic = veilImg;
        // ⚠ 这里必须是实例方法 Close：静态 Hide() 与实例方法同名会被编译器判成 CS0111
        veilBtn.onClick.AddListener(Close);

        var panel = CreateImage("Panel", _root.transform, new Color(0.10f, 0.11f, 0.14f, 0.98f));
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(PanelW, PanelH);

        var title = CreateText("Title", panel.transform, "抽奖概率", 34, new Color(1f, 0.92f, 0.72f));
        SetTop(title.rectTransform, -16f, new Vector2(PanelW - 40f, 44f));

        var sub = CreateText("Sub", panel.transform, "下面是单次抽奖的真实比例，随当前卡池实时变化。",
            20, new Color(0.74f, 0.74f, 0.78f));
        SetTop(sub.rectTransform, -60f, new Vector2(PanelW - 40f, 34f));

        var list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(panel.transform, false);
        var lrt = list.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(34f, 96f);
        lrt.offsetMax = new Vector2(-34f, -104f);
        var listRoot = list.transform;

        var rows = DraftPool.BuildOddsRows();
        if (rows.Count == 0)
        {
            var empty = CreateText("Empty", listRoot, "当前卡池为空", 22, new Color(0.8f, 0.8f, 0.84f));
            Stretch(empty.rectTransform);
        }
        else
        {
            float y = 0f;
            string lastSection = null;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                // 换段就插一个小标题：玩家一眼分清「抽到什么」和「什么品质」
                if (r.Section != lastSection)
                {
                    lastSection = r.Section;
                    if (y < 0f) y -= SectionGap;
                    var head = CreateText("Head_" + i, listRoot,
                        r.Section == DraftPool.SEC_TYPE ? "抽到什么" : "什么品质",
                        22, new Color(0.95f, 0.86f, 0.62f));
                    SetRow(head.rectTransform, y, HeadH);
                    head.alignment = TextAnchor.MiddleLeft;
                    y -= HeadH;
                }
                AddRow(listRoot, i, r, y);
                y -= RowH;
            }
        }

        var foot = CreateText("Foot", panel.transform, "品质越高越稀有，好东西本来就该难抽。",
            18, new Color(0.62f, 0.62f, 0.66f));
        SetTop(foot.rectTransform, -PanelH + 70f, new Vector2(PanelW - 40f, 30f));

        var close = CreateButton(panel.transform, "BtnClose", "关闭",
            new Color(0.30f, 0.32f, 0.38f), Close);
        close.anchorMin = new Vector2(0.5f, 0f);
        close.anchorMax = new Vector2(0.5f, 0f);
        close.pivot = new Vector2(0.5f, 0f);
        close.anchoredPosition = new Vector2(0f, 22f);
        close.sizeDelta = new Vector2(220f, 62f);

        GameFonts.ApplyToHierarchy(_root.transform);
        _root.SetActive(true);
    }

    /// <summary>一行「名字 ……… 百分比」。左对齐名字、右对齐数字，中间空着，简单清爽。</summary>
    static void AddRow(Transform parent, int index, DraftPool.OddsRow r, float y)
    {
        var row = new GameObject("Row" + index, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rrt = row.GetComponent<RectTransform>();
        SetRow(rrt, y, RowH);

        var name = CreateText("Name", row.transform, r.Label, 24, r.Color);
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(0.55f, 1f);
        nrt.offsetMin = new Vector2(10f, 0f);
        nrt.offsetMax = Vector2.zero;
        name.alignment = TextAnchor.MiddleLeft;

        var pct = CreateText("Pct", row.transform, $"{r.Percent:0.0}%", 26, r.Color);
        var prt = pct.rectTransform;
        prt.anchorMin = new Vector2(0.55f, 0f);
        prt.anchorMax = new Vector2(1f, 1f);
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = new Vector2(-10f, 0f);
        pct.alignment = TextAnchor.MiddleRight;
    }

    /// <summary>
    /// 关掉自己（实例版）。
    /// ⚠ 不能叫 Hide：外面还有个 <see cref="Hide()"/> 静态入口，同名同参数会报 CS0111。
    /// </summary>
    void Close()
    {
        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }
    }

    // ——— 与项目里其它运行时建 UI 的写法保持一致 ———

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(string name, Transform parent, string text, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;
        return t;
    }

    static RectTransform CreateButton(Transform parent, string name, string label, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        var img = CreateImage(name, parent, bg);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var t = CreateText("Label", img.transform, label, 26, Color.white);
        Stretch(t.rectTransform);
        return img.rectTransform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>统一用「父级顶部为基准」的上锚点，避免各处 y 正负号不一致。</summary>
    static void SetTop(RectTransform rt, float y, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = size;
    }

    /// <summary>列表里的行：顶部左对齐往下排，宽度撑满父级。</summary>
    static void SetRow(RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, height);
    }
}
