using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆佣兵招募：从形象池三选一。
/// 同形象可反复招募，但姓名/等级/星级/技能不同。
/// UI 代码搭建；酒馆场景预制体资源由用户自行替换，本面板不覆盖预制体。
/// </summary>
public class TavernRosterPanel : MonoBehaviour
{
    static TavernRosterPanel _instance;
    GameObject _root;
    Text _body;
    Text _title;
    readonly List<Image> _cardBgs = new List<Image>();
    readonly List<Text> _cardLabels = new List<Text>();
    readonly List<Image> _cardIcons = new List<Image>();
    List<MercenaryData> _offers = new List<MercenaryData>();
    int _selected;

    public static TavernRosterPanel Instance => _instance;
    public bool IsOpen => _root != null && _root.activeSelf;

    public static void Show()
    {
        Ensure().Open();
    }

    static TavernRosterPanel Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("TavernRosterPanel");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<TavernRosterPanel>();
        return _instance;
    }

    void Awake() => _instance = this;

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Open()
    {
        TownSaveAlign.AlignAll();
        BuildIfNeeded();
        RerollOffers();
        Refresh();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    void RerollOffers()
    {
        _offers = MercenaryOfferGenerator.GenerateOffers();
        _selected = 0;
    }

    void Refresh()
    {
        var data = SaveSystem.Instance?.Data;
        var mm = MercenaryManager.Instance;
        int slots = mm != null
            ? mm.GetMaxMercSlots()
            : Mathf.Clamp(data?.townLevel?.tavern ?? 1, 0, 2);

        var sb = new StringBuilder();
        sb.AppendLine($"【本局雇佣】出战槽 {slots}（下本结束离队）");
        sb.AppendLine();

        var list = data?.hiredMercs;
        if (list == null || list.Count == 0)
            sb.AppendLine("暂无雇佣。请点「招募佣兵」打开招募界面。");
        else
        {
            for (int i = 0; i < list.Count; i++)
                sb.AppendLine(MercenaryOfferGenerator.FormatRosterLine(list[i], i < slots));
        }

        var active = mm != null ? mm.GetActiveMercIds() : new List<string>();
        sb.AppendLine();
        sb.AppendLine($"开战将带出模板：{(active.Count > 0 ? string.Join("、", active) : "（无）")}");
        if (_body != null) _body.text = sb.ToString();

        RefreshCards();
    }

    void RefreshCards()
    {
        for (int i = 0; i < _cardLabels.Count; i++)
        {
            MercenaryData offer = (i < _offers.Count) ? _offers[i] : null;
            if (_cardLabels[i] != null)
                _cardLabels[i].text = offer != null ? MercenaryOfferGenerator.FormatCard(offer) : "—";
            if (_cardBgs[i] != null)
                _cardBgs[i].color = (i == _selected)
                    ? new Color(0.55f, 0.42f, 0.22f, 1f)
                    : new Color(0.28f, 0.22f, 0.18f, 1f);
            if (_cardIcons[i] != null && offer != null && MercenaryManager.Instance != null)
            {
                var icon = MercenaryManager.Instance.GetIcon(offer.mercId);
                _cardIcons[i].sprite = icon;
                _cardIcons[i].enabled = icon != null;
                _cardIcons[i].preserveAspect = true;
            }
        }
    }

    void Select(int idx)
    {
        if (idx < 0 || idx >= _offers.Count) return;
        _selected = idx;
        RefreshCards();
    }

    void OnConfirmRecruit()
    {
        MercenaryRecruitPopupUI.Show();
        if (_root != null) _root.SetActive(false);
    }

    static MercenaryData CloneOffer(MercenaryData src)
    {
        return new MercenaryData
        {
            mercId = src.mercId,
            displayName = src.displayName,
            uid = src.uid,
            favorLevel = src.favorLevel,
            level = src.level,
            star = src.star,
            skillId = src.skillId
        };
    }

    void BuildIfNeeded()
    {
        if (_root != null) return;
        var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas);
        canvas.sortingOrder = 900;

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(_root.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.GetComponent<Button>().onClick.AddListener(Close);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640f, 620f);
        panel.GetComponent<Image>().color = new Color(0.14f, 0.1f, 0.08f, 0.97f);

        _title = MakeText(panel.transform, "酒馆 · 佣兵招募（三选一）", 26);
        var trt = _title.rectTransform;
        trt.anchorMin = new Vector2(0.05f, 0.91f);
        trt.anchorMax = new Vector2(0.95f, 0.98f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        _body = MakeText(panel.transform, "", 16);
        _body.alignment = TextAnchor.UpperLeft;
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0.06f, 0.58f);
        brt.anchorMax = new Vector2(0.94f, 0.9f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;

        float cardW = 170f;
        float gap = 18f;
        float startX = -((cardW + gap));
        for (int i = 0; i < 3; i++)
        {
            var card = new GameObject("Card" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(panel.transform, false);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cardW, 210f);
            crt.anchoredPosition = new Vector2(startX + i * (cardW + gap), -40f);
            var img = card.GetComponent<Image>();
            img.color = new Color(0.28f, 0.22f, 0.18f, 1f);
            _cardBgs.Add(img);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(card.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, -8f);
            irt.sizeDelta = new Vector2(64f, 64f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.color = Color.white;
            _cardIcons.Add(iconImg);

            var label = MakeText(card.transform, "", 15);
            label.alignment = TextAnchor.UpperCenter;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.06f, 0.04f);
            lrt.anchorMax = new Vector2(0.94f, 0.62f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            _cardLabels.Add(label);

            int idx = i;
            card.GetComponent<Button>().onClick.AddListener(() => Select(idx));
        }

        MakeBtn(panel.transform, "Confirm", "招募选中", new Vector2(-110f, 28f), OnConfirmRecruit);
        MakeBtn(panel.transform, "Reroll", "换一批", new Vector2(110f, 28f), () =>
        {
            RerollOffers();
            Refresh();
        });
        MakeBtn(panel.transform, "Close", "关闭", new Vector2(0f, -28f), Close);
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    static Text MakeText(Transform parent, string content, int size)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    static void MakeBtn(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(160f, 44f);
        go.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.2f, 1f);
        var t = MakeText(go.transform, label, 20);
        Stretch(t.rectTransform);
        go.GetComponent<Button>().onClick.AddListener(onClick);
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
