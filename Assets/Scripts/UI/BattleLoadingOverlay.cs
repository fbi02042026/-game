using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 切 Battle 场景时的轻量 Loading（小游戏可行：只遮战斗加载，Town 内页签不切场景）。
/// </summary>
public static class BattleLoadingOverlay
{
    static GameObject _root;

    public static void Show(string tip = "进入冒险…")
    {
        Hide();
        _root = new GameObject("BattleLoadingOverlay", typeof(RectTransform));
        Object.DontDestroyOnLoad(_root);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        _root.AddComponent<GraphicRaycaster>();

        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(_root.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        Stretch(bgRt);
        bgGo.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        var textGo = new GameObject("Tip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(_root.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        Stretch(rt);
        var t = textGo.GetComponent<Text>();
        t.text = tip;
        t.fontSize = 28;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.font = GameFonts.GetChinese();
    }

    public static void Hide()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
            _root = null;
        }
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
