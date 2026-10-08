using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-06 主人拍板】装备稀有度的表达方式<b>只剩这一处</b>：给格子（装备槽）加一圈稀有度描边。
///
/// 主人原话：
/// 「装备不要带描边，而且在装备格子里也不要带效果，只在格子加个描边效果显示装备的稀有度」
/// 「加到 icon 底框上，只要不加到装备图片上就行」
///
/// 由此定死三条口径：
/// ① 装备图片（图标层）<b>不许</b>再挂稀有度材质 / 描边 / 发光 —— 只显示装备自己的美术图；
/// ② 格子内部底图保持美术预制体原样，<b>不再</b>用代码写死的颜色去刷（旧实现刷深蓝 / 铜色，盖掉美术底图）；
/// ③ 稀有度只画一圈边：颜色取 <see cref="RarityPalette"/>（全局唯一色表），
///    画在宿主（格子 / 装备快捷槽）内沿，压在图标之上，只占边缘几像素。
///
/// 节点<b>按需建、幂等复用</b>（同名节点已存在就只改色改显隐），不往预制体里落任何东西、
/// 不动 <c>.prefab</c> / <c>.meta</c>。
/// </summary>
public static class EquipRarityRim
{
    const string RimName = "RarityRim";
    /// <summary>描边粗细（像素）。要粗一点只改这一个数。</summary>
    const float RimThickness = 3f;
    /// <summary>描边相对宿主边界内缩多少（不让描边糊出格子外框）。</summary>
    const float RimInset = 1f;

    /// <summary>
    /// 画 / 刷新稀有度描边。<paramref name="rarity"/> 传 null（空格子 / 道具格）时把已有描边收起来，不删节点。
    /// 普通（白灰）也照画 —— 主人要的是「看格子就知道稀有度」，有装备就得能看出档位。
    /// </summary>
    public static void Apply(Transform host, Rarity? rarity)
    {
        if (host == null) return;

        var rim = FindRim(host);
        if (rarity == null)
        {
            if (rim != null) rim.gameObject.SetActive(false);
            return;
        }

        if (rim == null)
        {
            rim = Build(host);
            if (rim == null) return;
        }
        rim.gameObject.SetActive(true);

        Color c = RarityPalette.Get(rarity.Value);
        for (int i = 0; i < rim.childCount; i++)
        {
            var img = rim.GetChild(i).GetComponent<Image>();
            if (img != null) img.color = c;
        }
    }

    static Transform FindRim(Transform host)
    {
        for (int i = 0; i < host.childCount; i++)
        {
            var c = host.GetChild(i);
            if (c != null && c.name == RimName) return c;
        }
        return null;
    }

    /// <summary>建四条边（上 / 下 / 左 / 右）。用锚点撑开，格子 rect 尚未算好时也不会缩成一点。</summary>
    static Transform Build(Transform host)
    {
        var go = new GameObject(RimName, typeof(RectTransform));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(RimInset, RimInset);
        rt.offsetMax = new Vector2(-RimInset, -RimInset);
        rt.localScale = Vector3.one;

        AddEdge(go.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, RimThickness));
        AddEdge(go.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, RimThickness));
        AddEdge(go.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(RimThickness, 0f));
        AddEdge(go.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f), new Vector2(RimThickness, 0f));

        // 只在这里置顶一次：压在图标之上、角标之下。
        // （不要在 Apply 里反复置顶 —— 角标是 Apply 之后才补建的，反复置顶会让描边盖住角标。）
        go.transform.SetAsLastSibling();
        return go.transform;
    }

    static void AddEdge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.sprite = null;               // 纯色块，不需要图
        img.raycastTarget = false;       // 绝不能吃点击（背包拖拽 / 点开道具浮层都靠下层）
    }
}
