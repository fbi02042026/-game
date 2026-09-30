using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 刘海屏 / 挖孔屏 / 底部手势条适配：把自身 RectTransform 挪进（或内缩到）Screen.safeArea 之内。
///
/// 设计约定（与项目「UI 用运行时补节点，别改预制体」一致）：
/// 本组件在代码里 AddComponent 挂到需要避开的 UI 根节点上，不碰任何 .prefab。
///
/// 算法：
///   1. Screen.safeArea（像素）按 Screen.width/height 归一化成 0~1 的上/下不安全比例；
///   2. 乘以父级 RectTransform 的高度，得到逻辑单位的内缩量（父级即满屏 Canvas 根，故成立）；
///   3. 自身锚点强制全拉伸（anchorMin=(0,0) / anchorMax=(1,1)），用 offsetMin/offsetMax 表达矩形。
/// 左右永远不缩（竖屏设计宽度锁死），只处理上/下（由 edge 决定）。
///
/// 为什么必须「记录 base + 每次从 base 重算」：
///   ① 幂等 —— 重复 Apply 不会把内缩量累加进去；
///   ② 挂到的节点原始锚点未必是全拉伸（底部按钮栏常是 anchorMin=(0,0)/anchorMax=(1,0) 贴底锚定）。
///      直接把锚点改成全拉伸却复用原始 offset，offset 的语义会错位
///      （贴底锚定时 offsetMax.y 从底边往上量，全拉伸时从顶边往下量），矩形会跳到错误位置。
///      因此首次 Apply 先把「当前矩形」反推成「全拉伸语义下的四边内缩量」存为 base，之后一律从 base 重算。
///
/// 两种模式（重要）：
///   Shift（默认）：整体向安全区方向平移，尺寸不变 —— 适合固定高度的顶/底栏（默认用它，不会压扁按钮）。
///   Inset：从被遮挡的那一边裁掉，尺寸变小 —— 适合满屏面板（背景、弹窗遮罩）。
///   Offset（2026-09-28 新增）：只改 anchoredPosition，**完全不碰 anchor / sizeDelta / pivot**。
///       顶栏用这个：顶栏本来就是「贴顶锚定 + 固定高」，Shift 会把它的锚点强行改成全拉伸，
///       一旦父级尺寸变化（编辑器改 Game 视口 / 折叠屏），内缩量是按旧父级高算的会串位；
///       Offset 只做「整体下移 safeArea 顶部高度」，锚点语义保持原样，任何缩放/重锚点都不怕。
///       换算也不再依赖父级矩形高，而是用 Screen.safeArea(像素) ÷ Canvas.scaleFactor
///       （子节点单位 == 画布参考单位，这是精确值；父级高在非满屏容器里会算错）。
///
/// 已知限制（不要拿它去改根 Canvas）：
///   根 Canvas（自己就是最外层画笔）的 RectTransform 由 Canvas 组件每帧驱动，
///   手动改 anchoredPosition 会被覆盖 → 在根 Canvas 上 Offset 也是无效动作。
///   所以刘海避让一律作用在「顶栏那个子节点」上（Town 顶栏 / 战斗顶栏），不要挂到画布根。
/// </summary>
public class SafeAreaFitter : MonoBehaviour
{
    public enum Edge { Top, Bottom, Both }

    /// <summary>Shift = 平移保尺寸；Inset = 内缩裁边；Offset = 只挪 anchoredPosition（锚点/尺寸不动，顶栏用）。</summary>
    public enum FitMode { Shift, Inset, Offset }

    /// <summary>要避开的边：Top=只避上边，Bottom=只避下边，Both=上下都避。左右永远不缩。</summary>
    public Edge edge = Edge.Both;

    /// <summary>Shift = 平移（保尺寸，顶/底栏用）；Inset = 内缩（裁边，满屏面板用）；Offset = 只挪位置（推荐顶栏用）。</summary>
    public FitMode mode = FitMode.Shift;

    /// <summary>总开关。false 时恢复 base（还原到未适配的原始矩形），方便出问题一键关。</summary>
    public bool enabledFit = true;

    bool _baseCaptured;
    Vector2 _baseOffsetMin;
    Vector2 _baseOffsetMax;
    bool _applying;

    // —— Offset 模式专用 ——
    Vector2 _basePos;              // 未施加位移前的 anchoredPosition
    bool _basePosCaptured;
    float _appliedY;               // 本组件当前施加的 y 位移（用于「重锚点时反推基准」）
    float _sigMinY, _sigMaxY;      // 基准对应的锚点 y 签名（锚点变了就重新取基准）
    int _lastW = -1, _lastH = -1;  // Offset：上一次的屏幕尺寸 / 安全区 / 画布缩放（用于变化检测，不变就不干活）
    Rect _lastSafe = new Rect(0f, 0f, -1f, -1f);
    float _lastSf = -1f;

    void Awake() => Apply();
    void Start() => Apply();

    /// <summary>
    /// Offset 模式专用兜底：画布 scaleFactor / 屏幕尺寸 / 安全区只要变了就重算一次。
    /// （AddComponent 的瞬间 CanvasScaler 可能还没量准，只靠 Awake/Start 会算错一次然后一直错。）
    /// 什么都没变时每帧只做几次比较，不做任何写入。
    /// </summary>
    void LateUpdate()
    {
        if (mode != FitMode.Offset) return;
        var rt = transform as RectTransform;
        if (rt == null) return;

        int w = Screen.width, h = Screen.height;
        Rect safe = Screen.safeArea;
        float sf = ScaleFactorOf(rt);
        if (w == _lastW && h == _lastH && safe == _lastSafe && Mathf.Approximately(sf, _lastSf))
            return;
        _lastW = w; _lastH = h; _lastSafe = safe; _lastSf = sf;
        ApplyOffset(rt);
    }

    void OnRectTransformDimensionsChange()
    {
        // 改 anchor/offset 本身会再次触发本回调，加守卫防递归
        if (_applying) return;
        Apply();
    }

    void Apply()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;

        // Offset 模式：只挪 anchoredPosition，不需要父级矩形做参照，也不看父级是否量好尺寸。
        if (mode == FitMode.Offset)
        {
            ApplyOffset(rt);
            return;
        }

        var parent = rt.parent as RectTransform;
        // 父级（通常是 Canvas 内容根）还没量好尺寸时等下一次，避免用 0 高度算出错误内缩。
        // 挂在 Canvas 根节点上（parent 不是 RectTransform）时同样直接放弃 —— 惰性 no-op，不影响外观。
        if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f) return;

        if (!_baseCaptured)
            CaptureBase(rt, parent);

        _applying = true;
        try
        {
            if (!enabledFit)
            {
                // 一键关：全拉伸 + base，还原到挂本组件前的外观
                SetFullStretch(rt, _baseOffsetMin, _baseOffsetMax);
                return;
            }

            float topN = 0f, bottomN = 0f;
            if (Screen.width > 0 && Screen.height > 0)
            {
                Rect safe = Screen.safeArea;
                topN = (Screen.height - (safe.y + safe.height)) / Screen.height;
                bottomN = safe.y / Screen.height;
            }
            float ph = parent.rect.height;
            float topU = topN * ph;
            float botU = bottomN * ph;

            Vector2 omin = _baseOffsetMin;
            Vector2 omax = _baseOffsetMax;

            bool fitTop = edge == Edge.Top || edge == Edge.Both;
            bool fitBottom = edge == Edge.Bottom || edge == Edge.Both;

            if (mode == FitMode.Shift)
            {
                // 平移：上下边同向移动同样距离，尺寸保住（顶/底栏不会被压扁）
                if (fitTop) { omin.y -= topU; omax.y -= topU; }
                if (fitBottom) { omin.y += botU; omax.y += botU; }
            }
            else
            {
                // 内缩：只动被遮挡的那一侧，矩形变小（满屏面板不会溢出安全区）
                if (fitTop) omax.y -= topU;
                if (fitBottom) omin.y += botU;
            }

            SetFullStretch(rt, omin, omax);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>
    /// Offset 模式：只改 anchoredPosition，anchor / sizeDelta / pivot 一律不动。
    /// 位移量 = Screen.safeArea 的不安全高度(像素) ÷ Canvas.scaleFactor（精确换算，不看父级尺寸）。
    /// 幂等：base 只在「首次」或「锚点被别的布局逻辑（如 UiLayoutStretch）改过」时记录，
    /// 且记录时先减掉本组件已施加的位移，所以重锚定不会导致二次叠加或位置跳变。
    /// </summary>
    void ApplyOffset(RectTransform rt)
    {
        bool fitTop = edge == Edge.Top || edge == Edge.Both;
        bool fitBottom = edge == Edge.Bottom || edge == Edge.Both;

        float dy = 0f;
        if (enabledFit && (fitTop || fitBottom) && Screen.width > 0 && Screen.height > 0)
        {
            float sf = ScaleFactorOf(rt);
            Rect safe = Screen.safeArea;
            float topPx = Mathf.Max(0f, Screen.height - (safe.y + safe.height));
            float botPx = Mathf.Max(0f, safe.y);
            if (fitTop) dy -= topPx / sf;     // 避上边 = 整体往下挪
            if (fitBottom) dy += botPx / sf;  // 避下边 = 整体往上挪
        }

        bool anchorsChanged = !Mathf.Approximately(rt.anchorMin.y, _sigMinY)
                              || !Mathf.Approximately(rt.anchorMax.y, _sigMaxY);
        if (!_basePosCaptured || anchorsChanged)
        {
            // 反推「没有本组件时」的位置：当前值减掉本组件已经施加的位移。
            _basePos = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y - _appliedY);
            _sigMinY = rt.anchorMin.y;
            _sigMaxY = rt.anchorMax.y;
            _basePosCaptured = true;
        }

        _appliedY = dy;
        _applying = true;
        try
        {
            // x 一律不动（左右不缩）。值没变就不写，避免无谓地弄脏布局。
            var want = new Vector2(rt.anchoredPosition.x, _basePos.y + dy);
            if (rt.anchoredPosition != want)
                rt.anchoredPosition = want;
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>屏幕像素 → 画布参考单位的换算系数（取最外层带 CanvasScaler 的 Canvas）。</summary>
    static float ScaleFactorOf(RectTransform rt)
    {
        var canvases = rt.GetComponentsInParent<Canvas>(true);
        for (int i = canvases.Length - 1; i >= 0; i--)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (c.GetComponent<CanvasScaler>() != null && c.scaleFactor > 0f)
                return c.scaleFactor;
        }
        return 1f;
    }

    /// <summary>纯工具：把节点矩形四边换算成父级本地坐标（原点=父级左下角）。</summary>
    static void RectMinMaxInParent(RectTransform rt, RectTransform parent, out Vector2 min, out Vector2 max)
    {
        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;
        min = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMin.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMin.y)) + rt.offsetMin;
        max = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMax.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMax.y)) + rt.offsetMax;
    }

    /// <summary>给一个「顶部横条」节点挂上 Offset 适配（幂等：已挂就复用，只校正参数）。</summary>
    public static SafeAreaFitter EnsureTopOffset(RectTransform rt)
    {
        if (rt == null) return null;
        var f = rt.GetComponent<SafeAreaFitter>();
        if (f == null) f = rt.gameObject.AddComponent<SafeAreaFitter>();
        f.edge = Edge.Top;
        f.mode = FitMode.Offset;
        f.enabledFit = true;
        return f;
    }

    /// <summary>
    /// 在 root 子树里按名字找「顶部横条」并挂 Offset 适配。规则：
    ///   - 候选里只处理**最外层**那个（父级链上已有别的命中节点就跳过），避免父子同时下推变成双倍；
    ///   - 只处理矩形中心位于父级上半部的节点（同名但长在屏幕中下部的节点不碰）；
    ///   - 返回实际挂上的个数。
    /// 注意：不要挂到根 Canvas 上（其 RectTransform 由 Canvas 每帧驱动，改了无效，见类注释）。
    /// </summary>
    public static int ApplyTopOffset(Transform root, params string[] nodeNames)
    {
        if (root == null || nodeNames == null || nodeNames.Length == 0) return 0;

        var hits = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < nodeNames.Length; i++)
        {
            if (string.IsNullOrEmpty(nodeNames[i])) continue;
            CollectByName(root, nodeNames[i], hits);
        }
        if (hits.Count == 0) return 0;

        int applied = 0;
        for (int i = 0; i < hits.Count; i++)
        {
            var t = hits[i];
            if (t == null) continue;

            bool nested = false;
            for (int j = 0; j < hits.Count; j++)
            {
                if (i == j || hits[j] == null || hits[j] == t) continue;
                if (t.IsChildOf(hits[j])) { nested = true; break; }
            }
            if (nested) continue;

            var rt = t as RectTransform;
            if (rt == null) continue;
            if (!IsTopArea(rt)) continue;

            if (EnsureTopOffset(rt) != null) applied++;
        }
        return applied;
    }

    static void CollectByName(Transform root, string name, System.Collections.Generic.List<Transform> into)
    {
        if (root == null) return;
        if (root.name == name && !into.Contains(root)) into.Add(root);
        for (int i = 0; i < root.childCount; i++)
            CollectByName(root.GetChild(i), name, into);
    }

    /// <summary>矩形中心是否落在父级上半部（本地坐标原点在父级左下角，中心 y &gt; 0 即上半）。</summary>
    static bool IsTopArea(RectTransform rt)
    {
        var parent = rt.parent as RectTransform;
        if (parent == null || parent.rect.height <= 0f) return true;   // 判不了就放行
        Vector2 min, max;
        RectMinMaxInParent(rt, parent, out min, out max);
        return (min.y + max.y) * 0.5f > 0f;
    }

    static void SetFullStretch(RectTransform rt, Vector2 omin, Vector2 omax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = omin;
        rt.offsetMax = omax;
    }

    /// <summary>
    /// 把「当前矩形」换算成全拉伸语义下的四边内缩量，作为 base。
    /// Unity 语义：offsetMin = rect.min - Lerp(parent.min, parent.max, anchorMin)
    ///            offsetMax = rect.max - Lerp(parent.min, parent.max, anchorMax)
    /// 全拉伸时 anchorMin=(0,0)→parent.min、anchorMax=(1,1)→parent.max，故：
    ///   baseOffsetMin = curMin - parent.min
    ///   baseOffsetMax = curMax - parent.max   （注意是 curMax - pMax，方向不能反）
    /// </summary>
    void CaptureBase(RectTransform rt, RectTransform parent)
    {
        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;

        // 注意：anchor 是逐轴的（x 用父级 x 区间插值，y 用父级 y 区间插值），
        // 不能写成 Vector2.Lerp(pMin, pMax, anchor)——第三个参数只接受 float。
        Vector2 curMin = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMin.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMin.y)) + rt.offsetMin;
        Vector2 curMax = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMax.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMax.y)) + rt.offsetMax;

        _baseOffsetMin = curMin - pMin;
        _baseOffsetMax = curMax - pMax;
        _baseCaptured = true;
    }
}
