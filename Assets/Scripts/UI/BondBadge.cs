using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「羁绊」角标唯一构建出口（2026-11-09 口径）：
/// 战斗内佣兵头像左上角 / 佣兵队列列表卡左上角，两处共用同一套外观常量，禁止各写一份。
/// 判据 = SaveData.IsMercBondCleared(hireId)，首通羁绊副本后才显示；
/// 全部运行时建节点，预制体一字不动。
/// </summary>
public static class BondBadge
{
    public const string NodeName = "BondBadge";
    public const string Label = "羁绊";

    // 外观只在这里定：黑色半透底 + 白字，fusion-pixel 24（12 的倍数）
    static readonly Color BackColor = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color TextColor = Color.white;
    const int FontSize = 24;
    static readonly Vector2 Size = new Vector2(110f, 36f);
    const float Inset = 2f;

    /// <summary>
    /// 在 parent 左上角挂/刷新角标。卡片池复用安全：节点已存在只改显隐，不重复叠。
    /// hireId 为空会 LogError，未通关 / 存档未就绪一律隐藏（fail closed，不静默按已通关放行）。
    /// </summary>
    public static void Apply(RectTransform parent, string hireId)
    {
        if (parent == null)
        {
            Debug.LogError("[BondBadge] 角标父节点为空，挂不上");
            return;
        }

        bool show = false;
        if (string.IsNullOrEmpty(hireId))
        {
            Debug.LogError("[BondBadge] 角标缺 hireId（父节点 " + parent.name + "），按不显示处理");
        }
        else
        {
            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            show = data != null && data.IsMercBondCleared(hireId);
        }

        var exist = parent.Find(NodeName);
        GameObject go;
        if (exist != null) go = exist.gameObject;
        else if (show) go = Create(parent);
        else return;   // 未通关且没建过节点：什么都不用做

        if (go == null) return;
        go.SetActive(show);
        go.transform.SetAsLastSibling();
    }

    static GameObject Create(RectTransform parent)
    {
        var font = GameFonts.GetChinese();
        if (font == null)
        {
            Debug.LogError("[BondBadge] fusion-pixel 字体取不到，角标不建（不拿其它字体顶替）");
            return null;
        }

        var go = new GameObject(NodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // 左上角
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Inset, -Inset);
        rt.sizeDelta = Size;
        var img = go.GetComponent<Image>();
        img.color = BackColor;
        img.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var t = textGo.AddComponent<Text>();
        t.text = Label;
        t.font = font;
        t.fontSize = FontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = TextColor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return go;
    }
}
