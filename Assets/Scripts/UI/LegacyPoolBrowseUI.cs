using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇武器库：只读浏览遗产池（开战前三选一从中抽取）。
/// </summary>
public class LegacyPoolBrowseUI : MonoBehaviour
{
    public static LegacyPoolBrowseUI Instance { get; private set; }

    GameObject _root;
    Text _title;
    Text _hint;
    readonly List<Image> _cardBgs = new List<Image>();
    readonly List<Text> _cardLabels = new List<Text>();
    List<EquipmentData> _items = new List<EquipmentData>();

    public static void Show()
    {
        Ensure().Open();
    }

    static LegacyPoolBrowseUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("LegacyPoolBrowseUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<LegacyPoolBrowseUI>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open()
    {
        TownSaveAlign.AlignAll();
        var pool = SaveSystem.Instance?.Data?.legacyEquipPool;
        _items = pool != null ? new List<EquipmentData>(pool) : new List<EquipmentData>();
        BuildIfNeeded();
        Refresh();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        EnsureEventSystem();
        GameFonts.ApplyToHierarchy(transform);
    }

    void BuildIfNeeded()
    {
        if (_root != null) return;

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleLegacyPool);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateUi("Dim", _root.transform, typeof(Image));
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        dim.GetComponent<Image>().raycastTarget = true;

        var panel = CreateUi("Panel", _root.transform, typeof(Image));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(660f, 520f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.97f);

        _title = CreateText(panel.transform, "Title", "武器库 · 遗产", 30, TextAnchor.MiddleCenter);
        AnchorTop(_title.rectTransform, -20f, 600f, 40f);

        _hint = CreateText(panel.transform, "Hint", "开战前可从遗产中三选一穿装", 18, TextAnchor.MiddleCenter);
        _hint.rectTransform.anchoredPosition = new Vector2(0f, 170f);

        for (int i = 0; i < 9; i++)
        {
            var card = CreateUi("Card" + i, panel.transform, typeof(Image));
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(190f, 88f);
            int row = i / 3;
            int col = i % 3;
            crt.anchoredPosition = new Vector2((col - 1) * 205f, 70f - row * 100f);
            var img = card.GetComponent<Image>();
            img.color = new Color(0.28f, 0.28f, 0.32f, 1f);
            _cardBgs.Add(img);
            var label = CreateText(card.transform, "L", "", 15, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 6f);
            _cardLabels.Add(label);
        }

        var ok = CreateUi("Close", panel.transform, typeof(Image), typeof(Button));
        var ort = ok.GetComponent<RectTransform>();
        ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0f);
        ort.pivot = new Vector2(0.5f, 0f);
        ort.anchoredPosition = new Vector2(0f, 22f);
        ort.sizeDelta = new Vector2(200f, 48f);
        ok.GetComponent<Image>().color = new Color(0.35f, 0.4f, 0.5f, 1f);
        CreateText(ok.transform, "T", "关闭", 24, TextAnchor.MiddleCenter);
        ok.GetComponent<Button>().onClick.AddListener(Close);
    }

    void Refresh()
    {
        int n = _items.Count;
        if (_title != null)
            _title.text = n > 0 ? $"武器库 · 遗产（{n}）" : "武器库 · 遗产为空";
        if (_hint != null)
            _hint.text = n > 0 ? "开战前可从遗产中三选一穿装" : "死亡/撤离带回的装备会出现在这里";

        for (int i = 0; i < _cardBgs.Count; i++)
        {
            bool has = i < _items.Count && _items[i] != null;
            _cardBgs[i].gameObject.SetActive(has);
            if (!has) continue;
            var d = _items[i];
            _cardLabels[i].text = $"{DisplayName(d)}\n稀有{d.rarity} ★{d.star}";
        }
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    static string DisplayName(EquipmentData d)
    {
        if (d == null) return "装备";
        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(d.equipId) : null;
        if (tpl != null && !string.IsNullOrEmpty(tpl.equipName))
            return tpl.equipName;
        return string.IsNullOrEmpty(d.equipId) ? "装备" : d.equipId;
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static GameObject CreateUi(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        for (int i = 0; i < comps.Length; i++)
            go.AddComponent(comps[i]);
        return go;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = CreateUi(name, parent, typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Stretch(go.GetComponent<RectTransform>());
        return t;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void AnchorTop(RectTransform rt, float y, float w, float h)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
