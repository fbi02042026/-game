using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 天空王国城堡地图界面（2026-11-22 主人拍板：主界面入口 → 本界面 → 点城门 → 大地图）。
/// 全屏展示 <see cref="SlgDefs.KingdomMapPath"/>（853×1843 竖图，contain 适配居中，黑边露底色），
/// 底部城门位置放透明热区按钮，点击 = 关本界面 + SlgMapUI.Show()。
/// 全代码构建（同 SlgMapUI 模式，不依赖预制体）；图片 Resources.Load 路径加载（铁律 16）。
/// </summary>
public static class SlgKingdomUI
{
    const int SortOrder = 12300;   // 低于大地图 12500：点城门后大地图盖上来

    /// <summary>城门热区（相对图面的归一化锚点）：x 0.32~0.68、y 0.74~0.92（城门拱门+城墙段，2026-11-22 按图目测）。</summary>
    const float GateXMin = 0.32f, GateXMax = 0.68f;
    const float GateYMin = 0.74f, GateYMax = 0.92f;

    static Canvas _canvas;

    /// <summary>打开天空王国城堡地图（未解锁给提示）。</summary>
    public static void Show()
    {
        if (!SlgSystem.IsUnlocked())
        {
            GlobalToastUI.ShowQueued("通关第 1 章第 10 关后开启城镇入口");
            return;
        }
        EnsureCanvas();
        if (_canvas == null) return;
        _canvas.gameObject.SetActive(true);
        UICanvasSetup.RefreshPopup(_canvas, SortOrder);
    }

    public static void Hide()
    {
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    /// <summary>清档复位（DDOL 静态 Canvas 随档销毁）。</summary>
    public static void ResetForNewGame()
    {
        if (_canvas != null)
        {
            Object.Destroy(_canvas.gameObject);
            _canvas = null;
        }
    }

    static void EnsureCanvas()
    {
        if (_canvas != null) return;

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(es);
        }

        var root = new GameObject("SlgKingdomCanvas", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(root);
        _canvas = root.GetComponent<Canvas>();
        UICanvasSetup.ApplyPopup(_canvas, SortOrder);

        // 底色宿主（吞掉空白点击 + 黑边）
        var host = NewImage("Host", _canvas.transform, new Color(0.05f, 0.04f, 0.06f, 0.96f));
        host.raycastTarget = true;
        Stretch(host.rectTransform);

        // 王国地图图面：contain 适配（不拉伸，按 853×1843 原比例取最大内接矩形）。
        // 尺寸用 Canvas 逻辑坐标（CanvasScaler 缩放后的 UI 单位），不用 Screen 物理像素——
        // 换算真源 = canvas.scaleFactor（UICanvasSetup.ApplyPopup 已配好），与 SlgMapUI 用 view.rect 同口径。
        var map = NewImage("KingdomMap", _canvas.transform, Color.white);
        map.sprite = LoadSprite(SlgDefs.KingdomMapPath);
        map.preserveAspect = true;
        map.raycastTarget = false;
        var mapRt = map.rectTransform;
        mapRt.anchorMin = mapRt.anchorMax = mapRt.pivot = new Vector2(0.5f, 0.5f);
        float sf = _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
        float screenW = Screen.width / sf, screenH = Screen.height / sf;
        float aspect = 853f / 1843f;
        float h = Mathf.Min(screenH, screenW / aspect);
        float w = h * aspect;
        mapRt.sizeDelta = new Vector2(w, h);
        mapRt.anchoredPosition = Vector2.zero;

        // 城门热区（图面子物体的归一化锚点，自动跟随图面尺寸）
        var gateGo = new GameObject("GateBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        gateGo.transform.SetParent(mapRt, false);
        var gateRt = (RectTransform)gateGo.transform;
        gateRt.anchorMin = new Vector2(GateXMin, GateYMin);
        gateRt.anchorMax = new Vector2(GateXMax, GateYMax);
        gateRt.pivot = new Vector2(0.5f, 0.5f);
        gateRt.offsetMin = Vector2.zero;
        gateRt.offsetMax = Vector2.zero;
        var gateImg = gateGo.GetComponent<Image>();
        gateImg.color = new Color(1f, 1f, 0.7f, 0.01f);   // 近透明热区，能吃射线就行
        gateImg.raycastTarget = true;
        gateGo.GetComponent<Button>().onClick.AddListener(OnGateClicked);

        // 城门上方提示（挂在图面上，跟随图面）
        var hint = NewText("GateHint", mapRt, "点击城门 · 进入大地图", 30, TextAnchor.MiddleCenter);
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0.18f, 0.665f);
        hintRt.anchorMax = new Vector2(0.82f, 0.72f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;
        hint.color = new Color(1f, 0.92f, 0.6f, 1f);

        // 返回主界面（屏幕左上）
        var back = NewButton("BackBtn", _canvas.transform, "返回",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(120f, 64f), new Vector2(16f, -40f));
        back.onClick.AddListener(Hide);
    }

    static void OnGateClicked()
    {
        Hide();
        SlgMapUI.Show();
    }

    // ── 本文件自用小工具（与 SlgMapUI 同款；不拆 SlgMapUI 的私有件，保持两界面零耦合）──

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text NewText(string name, Transform parent, string value, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = value;
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = align;
        t.color = new Color(0.99f, 0.98f, 0.88f, 1f);
        t.raycastTarget = false;
        return t;
    }

    static Button NewButton(string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var t = NewText("Label", rt, label, 28, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform);
        return go.GetComponent<Button>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Sprite LoadSprite(string path)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp == null) Debug.LogError("[SlgKingdom] 缺少美术资源：" + path);
        return sp;
    }
}
