using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包道具的操作浮层：点格子弹出，显示<b>道具名</b> + 使用/丢弃 + 关闭。
/// - 点浮层外面任意处 = 取消（全屏透明遮罩接射线）
/// - 右上角 × 关闭
/// - 按 2026-09-15 需求：使用/丢弃<b>不做二次确认</b>
/// - 不可用的按钮置灰而不是点了报错；不可丢弃的任务道具直接不显示「丢弃」
/// </summary>
public class BackpackItemActionUI : MonoBehaviour
{
    public static BackpackItemActionUI Instance { get; private set; }

    GameObject _dismiss;
    GameObject _panel;
    Text _title;
    Text _desc;
    Button _useBtn;
    Button _dropBtn;

    GridBackpackSystem.BackpackItem _cur;

    /// <summary>挂在 BattleUI 下（保证在 Canvas 内）。重复调用返回同一个实例。</summary>
    public static BackpackItemActionUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        if (parent == null) return null;

        var go = new GameObject("BackpackItemActionUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        Instance = go.AddComponent<BackpackItemActionUI>();
        Instance.Build();
        return Instance;
    }

    void Build()
    {
        // 全屏透明遮罩：点它即取消
        _dismiss = new GameObject("Dismiss", typeof(RectTransform), typeof(Image));
        _dismiss.transform.SetParent(transform, false);
        var dImg = _dismiss.GetComponent<Image>();
        dImg.color = new Color(0f, 0f, 0f, 0f);
        dImg.raycastTarget = true;
        Stretch(_dismiss.GetComponent<RectTransform>());
        var dBtn = _dismiss.AddComponent<Button>();
        dBtn.onClick.AddListener(Hide);
        _dismiss.SetActive(false);

        // 浮层本体
        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        var pImg = _panel.GetComponent<Image>();
        pImg.color = new Color(0.12f, 0.12f, 0.15f, 0.96f);
        var pRt = _panel.GetComponent<RectTransform>();
        pRt.sizeDelta = new Vector2(260f, 170f);
        _panel.SetActive(false);

        // 道具名
        _title = MakeText(_panel.transform, "Title", 16, TextAnchor.MiddleCenter);
        var tRt = _title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -22f);
        tRt.sizeDelta = new Vector2(-70f, 28f);

        // 说明
        _desc = MakeText(_panel.transform, "Desc", 12, TextAnchor.UpperCenter);
        _desc.color = new Color(0.75f, 0.75f, 0.8f, 1f);
        var dRt = _desc.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0.05f, 1f);
        dRt.anchorMax = new Vector2(0.95f, 1f);
        dRt.pivot = new Vector2(0.5f, 1f);
        dRt.anchoredPosition = new Vector2(0f, -54f);
        dRt.sizeDelta = new Vector2(0f, 44f);

        // 按钮行
        _useBtn = MakeButton(_panel.transform, "Use", "使用", new Vector2(-62f, 44f));
        _dropBtn = MakeButton(_panel.transform, "Drop", "丢弃", new Vector2(62f, 44f));

        // 关闭
        var close = MakeButton(_panel.transform, "Close", "×", new Vector2(108f, -18f), new Vector2(34f, 34f));
        close.onClick.AddListener(Hide);
    }

    /// <summary>在屏幕坐标处弹出某个道具的操作面板。</summary>
    public void Show(GridBackpackSystem.BackpackItem item, Vector2 screenPos)
    {
        if (item == null || item.item == null) return;
        _cur = item;

        var def = item.item.Def;
        _title.text = item.item.count > 1 ? $"{item.item.Name} ×{item.item.count}" : item.item.Name;
        _desc.text = def != null && !string.IsNullOrEmpty(def.desc) ? def.desc : "";

        bool inBattle = BattleManager.Instance != null && BattleManager.Instance.isInBattle;
        bool canUse = def != null && def.IsUsable && (!inBattle || def.canUseInBattle);
        bool canDrop = def == null || def.canDrop;

        _useBtn.gameObject.SetActive(true);
        SetBtnEnabled(_useBtn, canUse);
        _dropBtn.gameObject.SetActive(canDrop);

        _useBtn.onClick.RemoveAllListeners();
        _useBtn.onClick.AddListener(OnUse);
        _dropBtn.onClick.RemoveAllListeners();
        _dropBtn.onClick.AddListener(OnDrop);

        // 位置：贴着点到的图标，夹在屏幕内
        var rt = _panel.GetComponent<RectTransform>();
        rt.position = screenPos;
        ClampIntoScreen(rt);

        _dismiss.SetActive(true);
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        _cur = null;
        if (_panel != null) _panel.SetActive(false);
        if (_dismiss != null) _dismiss.SetActive(false);
    }

    void OnUse()
    {
        var bag = GridBackpackSystem.Instance;
        if (bag == null || _cur == null) { Hide(); return; }
        bag.UseItem(_cur);
        Hide();
    }

    void OnDrop()
    {
        var bag = GridBackpackSystem.Instance;
        if (bag == null || _cur == null) { Hide(); return; }
        bag.DropItemStack(_cur);
        Hide();
    }

    // ===== 构建辅助 =====

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size ?? new Vector2(100f, 42f);

        var txt = MakeText(go.transform, "Text", 15, TextAnchor.MiddleCenter);
        Stretch(txt.GetComponent<RectTransform>());
        txt.text = label;

        return go.AddComponent<Button>();
    }

    static void SetBtnEnabled(Button btn, bool on)
    {
        if (btn == null) return;
        btn.interactable = on;
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = on ? new Color(0.25f, 0.35f, 0.5f, 1f) : new Color(0.25f, 0.25f, 0.28f, 1f);
        var t = btn.GetComponentInChildren<Text>(true);
        if (t != null)
            t.color = on ? Color.white : new Color(0.55f, 0.55f, 0.58f, 1f);
    }

    static void ClampIntoScreen(RectTransform rt)
    {
        if (rt == null) return;
        var c = rt.position;
        float halfW = rt.sizeDelta.x * 0.5f;
        float halfH = rt.sizeDelta.y * 0.5f;
        c.x = Mathf.Clamp(c.x, halfW, Screen.width - halfW);
        c.y = Mathf.Clamp(c.y, halfH, Screen.height - halfH);
        rt.position = c;
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
