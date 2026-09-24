using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用「获得奖励」弹窗（2026-09-23 新增）。
///
/// 用法：
///   RewardPopupUI.Show(iconSprite, "钻石 ×100");                       // 单个
///   RewardPopupUI.Show(new List<RewardPopupUI.Entry> { ... });         // 多个（面板自动变高）
///
/// 结构：预制体 Resources/Prefabs/UI/RewardPopup（Tools/奖励弹窗/生成器产出，自带画布），
///       面板底图九宫格拉伸——奖励多时面板自动变高；格子按每行最多 4 个自动换行。
///       找不到预制体时用纯色块兜底，保证功能可用。
/// 字体：Open 时 GameFonts.ApplyToHierarchy 统一刷（中文 fusion-pixel / 数字 PixelFont）。
/// </summary>
public class RewardPopupUI : MonoBehaviour
{
    public static RewardPopupUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/RewardPopup";

    /// <summary>一条奖励 = 图标 + 文案（文案自带数量，如「钻石 ×100」）。</summary>
    public struct Entry
    {
        public Sprite icon;
        public string label;
        public Entry(Sprite icon, string label) { this.icon = icon; this.label = label; }
    }

    // ---- 排版常量（逻辑坐标 720×1280）----
    // 2026-09-23 主人定版：底框只上下拉伸（宽度永远 PANEL_W），标题贴上沿、按钮贴下沿跟着走；
    // 奖励先横排居中，一排最多 5 个，多了换排（5×96 + 4×10 = 520 ≤ 面板 540）。
    const float ART_SCALE = 0.55f;      // 素材原图偏大，统一缩到逻辑分辨率
    const float PANEL_W = 540f;         // 982 * 0.55 ≈ 540（宽度固定，不许横向拉伸）
    const float TITLE_H = 160f;         // 290 * 0.55 ≈ 160
    const float CELL_W = 96f;           // 缩小格子，5 个一排塞得进
    const float CELL_H = 88f;
    const float LABEL_H = 34f;          // 格子下方文字（最多两行）
    const float ROW_GAP = 16f;
    const float COL_GAP = 10f;
    const int MAX_PER_ROW = 5;
    const float TOP_PAD = 150f;         // 标题占用（面板内顶部）
    const float BOTTOM_PAD = 96f;       // 按钮占用（面板内底部）

    GameObject _panel;
    RectTransform _grid;
    RectTransform _cellTemplate;
    Button _confirmBtn;

    // ============================================================
    // 对外入口
    // ============================================================

    public static void Show(Sprite icon, string label)
        => Show(new List<Entry> { new Entry(icon, label) });

    public static void Show(List<Entry> items)
    {
        if (items == null || items.Count == 0) return;
        Ensure().Open(items);
    }

    static RewardPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        // 完整弹窗预制体优先（自带画布）
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<Canvas>() != null)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "RewardPopup";
            DontDestroyOnLoad(go);
            var ui = go.GetComponent<RewardPopupUI>();
            if (ui == null) ui = go.AddComponent<RewardPopupUI>();
            ui.BuildFromPrefab();
            return ui;
        }

        // 兜底：纯代码生成（预制体没生成过时也能用）
        var fb = new GameObject("RewardPopup", typeof(RectTransform));
        DontDestroyOnLoad(fb);
        var ui2 = fb.AddComponent<RewardPopupUI>();
        ui2.BuildFallback();
        return ui2;
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
    // 构建
    // ============================================================

    void BuildFromPrefab()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) { BuildFallback(); return; }
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        var panelT = FindChild(transform, "Panel");
        _panel = panelT != null ? panelT.gameObject : null;
        var gridT = panelT != null ? FindChild(panelT, "Grid") : null;
        _grid = gridT as RectTransform;
        var tplT = FindChild(transform, "CellTemplate");
        _cellTemplate = tplT as RectTransform;
        var btnT = panelT != null ? FindChild(panelT, "ConfirmBtn") : null;

        if (_panel == null || _grid == null || _cellTemplate == null || btnT == null)
        {
            Debug.LogError("[RewardPopup] 预制体缺 Panel/Grid/CellTemplate/ConfirmBtn，回退纯色兜底");
            BuildFallback();
            return;
        }

        _confirmBtn = btnT.GetComponent<Button>();
        if (_confirmBtn == null) _confirmBtn = btnT.gameObject.AddComponent<Button>();
        _confirmBtn.onClick.AddListener(Hide);

        _cellTemplate.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>兜底：无预制体时用色块搭一个能用的（排版逻辑与预制体版完全一致）。</summary>
    void BuildFallback()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // 遮罩：点空白不关闭（防误触），但挡住下层
        var mask = CreateImg(transform, "Mask", new Color(0f, 0f, 0f, 0.55f));
        Stretch(mask.rectTransform);

        _panel = CreateImg(transform, "Panel", new Color(0.10f, 0.10f, 0.18f, 1f)).gameObject;
        var prt = _panel.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(PANEL_W, 420f);

        var gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(_panel.transform, false);
        _grid = (RectTransform)gridGo.transform;

        // 模板格子（色块版）
        var tpl = CreateImg(transform, "CellTemplate", new Color(0.16f, 0.14f, 0.10f, 1f));
        _cellTemplate = tpl.rectTransform;
        _cellTemplate.sizeDelta = new Vector2(CELL_W, CELL_H);
        var icon = CreateImg(tpl.transform, "Icon", new Color(1f, 1f, 1f, 0.12f));
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(0f, 10f);
        irt.sizeDelta = new Vector2(62f, 62f);
        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(tpl.transform, false);
        var lrt = (RectTransform)lblGo.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, -6f);
        lrt.sizeDelta = new Vector2(100f, LABEL_H);
        var txt = lblGo.AddComponent<Text>();
        txt.fontSize = 16; txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.95f, 0.93f, 0.88f, 1f);
        tpl.gameObject.SetActive(false);

        var btn = CreateImg(_panel.transform, "ConfirmBtn", new Color(0.72f, 0.50f, 0.14f, 1f));
        var brt = btn.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 26f);
        brt.sizeDelta = new Vector2(222f, 46f);
        _confirmBtn = btn.gameObject.AddComponent<Button>();
        _confirmBtn.transition = Selectable.Transition.None;
        _confirmBtn.onClick.AddListener(Hide);
        var btxt = CreateTxt(btn.transform, "Label", "确 定", 26, TextAnchor.MiddleCenter);
        Stretch(btxt.rectTransform);
        btxt.color = new Color(0.16f, 0.06f, 0.04f, 1f);

        gameObject.SetActive(false);
    }

    // ============================================================
    // 打开 / 排版
    // ============================================================

    void Open(List<Entry> items)
    {
        if (_panel == null) BuildFallback();
        gameObject.SetActive(true);
        // DDOL 弹窗每次 Show 重新绑一次相机（切场景后旧相机会失效）
        var cv = GetComponent<Canvas>();
        if (cv != null) UICanvasSetup.RefreshPopup(cv, GameConfig.UiSort.TownPopup);
        Layout(items);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    /// <summary>清空旧格子 → 按数量生成 → 面板九宫格拉伸到合适高度。</summary>
    void Layout(List<Entry> items)
    {
        // 清空 Grid
        for (int i = _grid.childCount - 1; i >= 0; i--)
            Destroy(_grid.GetChild(i).gameObject);

        int n = items.Count;
        int perRow = Mathf.Min(MAX_PER_ROW, n);
        int rows = Mathf.CeilToInt((float)n / perRow);
        float rowH = CELL_H + LABEL_H + ROW_GAP;
        float panelH = TOP_PAD + rows * rowH + BOTTOM_PAD;

        var prt = _panel.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(PANEL_W, panelH);   // 九宫格：中间被拉伸，边框不变形

        // 标题：压在面板上沿
        var titleT = FindChild(_panel.transform, "Title");
        if (titleT != null)
        {
            var trt = (RectTransform)titleT;
            trt.sizeDelta = new Vector2(835f * ART_SCALE, TITLE_H);
            trt.anchoredPosition = new Vector2(0f, panelH / 2f - 52f);
        }

        // 按钮：底部居中
        if (_confirmBtn != null)
        {
            var brt = _confirmBtn.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, -panelH / 2f + 52f);
            brt.sizeDelta = new Vector2(404f * ART_SCALE, 84f * ART_SCALE);
        }

        // 格子：每行居中排布
        for (int i = 0; i < n; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int inRow = (row == rows - 1) ? (n - row * perRow) : perRow;

            var cell = Instantiate(_cellTemplate.gameObject, _grid, false);
            cell.name = "Cell_" + (i + 1);
            cell.SetActive(true);
            var crt = (RectTransform)cell.transform;

            float x = (col - (inRow - 1) * 0.5f) * (CELL_W + COL_GAP);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(x, -TOP_PAD - row * rowH);   // 从标题下方开始排
            crt.sizeDelta = new Vector2(CELL_W, CELL_H);

            var icon = cell.transform.Find("Icon") as RectTransform;
            if (icon != null)
            {
                var img = icon.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = items[i].icon;
                    img.preserveAspect = true;
                    img.color = items[i].icon != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
                }
            }
            var lbl = cell.transform.Find("Label");
            if (lbl != null)
            {
                var t = lbl.GetComponent<Text>();
                if (t != null) t.text = items[i].label ?? "";
            }
        }
    }

    // ============================================================
    // 小工具（与 DailyLoginUI 同款）
    // ============================================================

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    static Image CreateImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateTxt(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
