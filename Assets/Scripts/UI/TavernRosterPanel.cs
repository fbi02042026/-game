using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆名册/出战：展示 permanentMercs 与可出战槽，可招募弓手。
/// </summary>
public class TavernRosterPanel : MonoBehaviour
{
    static TavernRosterPanel _instance;
    GameObject _root;
    Text _body;

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
        Refresh();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    void Refresh()
    {
        var data = SaveSystem.Instance?.Data;
        var sb = new StringBuilder();
        int slots = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetMaxMercSlots()
            : Mathf.Clamp(data?.townLevel?.tavern ?? 1, 0, 2);
        sb.AppendLine($"【佣兵名册】出战槽 {slots}");
        sb.AppendLine();

        var list = data?.permanentMercs;
        if (list == null || list.Count == 0)
            sb.AppendLine("暂无永久佣兵。可点「招募弓手」加入一只。");
        else
        {
            sb.AppendLine("名册：");
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null) continue;
                bool deploy = i < slots;
                sb.AppendLine($"  {(deploy ? "★出战" : "·待命")} {m.mercId}  Lv{Mathf.Max(1, m.level)} 好感{Mathf.Max(0, m.favorLevel)}");
            }
        }

        var active = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetActiveMercIds()
            : new List<string>();
        sb.AppendLine();
        sb.AppendLine($"开战将带出：{(active.Count > 0 ? string.Join("、", active) : "（无）")}");
        if (_body != null) _body.text = sb.ToString();
    }

    void OnRecruitArcher()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            UIManager.Instance?.ShowToast("存档未就绪");
            return;
        }
        if (data.permanentMercs == null)
            data.permanentMercs = new List<MercenaryData>();

        for (int i = 0; i < data.permanentMercs.Count; i++)
        {
            if (data.permanentMercs[i] != null && data.permanentMercs[i].mercId == "gongshou101")
            {
                UIManager.Instance?.ShowToast("已拥有弓手101");
                Refresh();
                return;
            }
        }

        data.permanentMercs.Add(new MercenaryData { mercId = "gongshou101", favorLevel = 1, level = 1 });
        if (data.townLevel == null) data.townLevel = new TownLevel();
        if (data.townLevel.tavern < 1) data.townLevel.tavern = 1;
        SaveSystem.Instance.Save();
        UIManager.Instance?.ShowToast("已招募：弓手101");
        Refresh();
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
        var rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(_root.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.GetComponent<Button>().onClick.AddListener(Close);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(560f, 480f);
        panel.GetComponent<Image>().color = new Color(0.14f, 0.1f, 0.08f, 0.97f);

        var title = MakeText(panel.transform, "酒馆 · 佣兵名册", 28);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0.05f, 0.88f);
        trt.anchorMax = new Vector2(0.95f, 0.98f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        _body = MakeText(panel.transform, "", 20);
        _body.alignment = TextAnchor.UpperLeft;
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0.06f, 0.28f);
        brt.anchorMax = new Vector2(0.94f, 0.86f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;

        MakeBtn(panel.transform, "Recruit", "招募弓手", new Vector2(-110f, 28f), OnRecruitArcher);
        MakeBtn(panel.transform, "Close", "关闭", new Vector2(110f, 28f), Close);
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
        rt.sizeDelta = new Vector2(180f, 48f);
        go.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.2f, 1f);
        var t = MakeText(go.transform, label, 22);
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
