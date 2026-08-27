using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险日志三期运行时面板：碎片合成 / 里程商店（不改 prefab）。
/// </summary>
public class AdventureLogPhase3Panel : MonoBehaviour
{
    enum Mode { None, Craft, Shop }

    AdventureLogUI _host;
    GameObject _bar;
    GameObject _panel;
    Text _title;
    Text _body;
    Mode _mode;
    readonly Button[] _actionBtns = new Button[6];
    readonly Text[] _actionLabels = new Text[6];

    public static AdventureLogPhase3Panel Ensure(AdventureLogUI host)
    {
        if (host == null) return null;
        var existing = host.GetComponent<AdventureLogPhase3Panel>();
        if (existing != null)
        {
            existing._host = host;
            existing.EnsureUi();
            return existing;
        }
        var p = host.gameObject.AddComponent<AdventureLogPhase3Panel>();
        p._host = host;
        p.EnsureUi();
        return p;
    }

    public void SetVisibleForTab(bool worldOrAch)
    {
        EnsureUi();
        if (_bar != null) _bar.SetActive(worldOrAch);
        if (!worldOrAch && _panel != null)
            _panel.SetActive(false);
    }

    void EnsureUi()
    {
        if (_bar != null) return;
        var canvas = _host != null ? _host.transform : transform;

        _bar = new GameObject("Phase3Bar", typeof(RectTransform));
        _bar.transform.SetParent(canvas, false);
        var barRt = _bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 0f);
        barRt.anchorMax = new Vector2(0.5f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.anchoredPosition = new Vector2(0f, 118f);
        barRt.sizeDelta = new Vector2(420f, 44f);

        MakeBarButton(_bar.transform, "碎片合成", new Vector2(-110f, 0f), OpenCraft);
        MakeBarButton(_bar.transform, "里程商店", new Vector2(110f, 0f), OpenShop);

        _panel = new GameObject("Phase3Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvas, false);
        var prt = _panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(520f, 560f);
        prt.anchoredPosition = Vector2.zero;
        var bg = _panel.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);

        _title = MakeText(_panel.transform, "Title", 22, TextAnchor.UpperCenter, new Vector2(0f, -28f), new Vector2(480f, 36f));
        _body = MakeText(_panel.transform, "Body", 16, TextAnchor.UpperLeft, new Vector2(0f, -70f), new Vector2(480f, 72f));
        _body.alignment = TextAnchor.UpperLeft;

        for (int i = 0; i < 6; i++)
        {
            float y = -160f - i * 52f;
            var row = MakeBarButton(_panel.transform, "—", new Vector2(0f, y), null, 460f, 44f);
            _actionBtns[i] = row.GetComponent<Button>();
            _actionLabels[i] = row.GetComponentInChildren<Text>();
        }

        MakeBarButton(_panel.transform, "关闭", new Vector2(0f, -520f + 40f), ClosePanel, 160f, 40f);
        _panel.SetActive(false);
        GameFonts.ApplyToHierarchy(_bar.transform);
        GameFonts.ApplyToHierarchy(_panel.transform);
        _bar.SetActive(false);
    }

    void OpenCraft()
    {
        _mode = Mode.Craft;
        RefreshPanel();
    }

    void OpenShop()
    {
        _mode = Mode.Shop;
        AdventureLogMileageShop.EnsureWeek();
        RefreshPanel();
    }

    void ClosePanel()
    {
        _mode = Mode.None;
        if (_panel != null) _panel.SetActive(false);
    }

    void RefreshPanel()
    {
        EnsureUi();
        if (_mode == Mode.None)
        {
            _panel.SetActive(false);
            return;
        }
        _panel.SetActive(true);

        if (_mode == Mode.Craft)
        {
            _title.text = "碎片合成";
            _body.text = AdventureLogFragments.FormatInventory();
            for (int i = 0; i < 6; i++)
            {
                bool on = i < AdventureLogFragments.Recipes.Length;
                _actionBtns[i].gameObject.SetActive(on);
                if (!on) continue;
                int idx = i;
                _actionLabels[i].text = AdventureLogFragments.FormatRecipeLine(idx);
                _actionBtns[i].interactable = AdventureLogFragments.CanCraft(idx);
                _actionBtns[i].onClick.RemoveAllListeners();
                _actionBtns[i].onClick.AddListener(() =>
                {
                    if (AdventureLogFragments.TryCraft(idx, out string msg))
                        UIManager.Instance?.ShowToast(msg);
                    else
                        UIManager.Instance?.ShowToast(msg ?? "无法合成");
                    RefreshPanel();
                    _host?.RefreshAfterPhase3();
                });
            }
        }
        else
        {
            _title.text = "里程商店";
            _body.text = AdventureLogMileage.FormatStatusLine() + "\n本周：" + AdventureLogMileageShop.CurrentWeekKey()
                         + $"\n招募卷 普{SaveSystem.Instance?.Data?.mercScrollCommon ?? 0}"
                         + $"/稀{SaveSystem.Instance?.Data?.mercScrollRare ?? 0}"
                         + $"/传{SaveSystem.Instance?.Data?.mercScrollLegendary ?? 0}";
            for (int i = 0; i < 6; i++)
            {
                bool on = i < AdventureLogMileageShop.Items.Length;
                _actionBtns[i].gameObject.SetActive(on);
                if (!on) continue;
                int idx = i;
                _actionLabels[i].text = AdventureLogMileageShop.FormatItemLine(idx);
                _actionBtns[i].interactable = AdventureLogMileageShop.CanBuy(idx, out _);
                _actionBtns[i].onClick.RemoveAllListeners();
                _actionBtns[i].onClick.AddListener(() =>
                {
                    if (AdventureLogMileageShop.TryBuy(idx, out string msg))
                        UIManager.Instance?.ShowToast(msg);
                    else
                        UIManager.Instance?.ShowToast(msg ?? "无法兑换");
                    RefreshPanel();
                });
            }
        }
    }

    static GameObject MakeBarButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, float w = 200f, float h = 40f)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        go.GetComponent<Image>().color = new Color(0.25f, 0.18f, 0.12f, 0.95f);
        var btn = go.GetComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);
        var tx = MakeText(go.transform, "Label", 18, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w - 8f, h - 4f));
        tx.text = label;
        return go;
    }

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
