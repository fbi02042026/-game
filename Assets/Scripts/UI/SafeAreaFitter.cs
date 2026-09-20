using UnityEngine;

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
/// </summary>
public class SafeAreaFitter : MonoBehaviour
{
    public enum Edge { Top, Bottom, Both }

    /// <summary>Shift = 平移保尺寸；Inset = 内缩裁边。</summary>
    public enum FitMode { Shift, Inset }

    /// <summary>要避开的边：Top=只避上边，Bottom=只避下边，Both=上下都避。左右永远不缩。</summary>
    public Edge edge = Edge.Both;

    /// <summary>Shift = 平移（保尺寸，顶/底栏用）；Inset = 内缩（裁边，满屏面板用）。</summary>
    public FitMode mode = FitMode.Shift;

    /// <summary>总开关。false 时恢复 base（还原到未适配的原始矩形），方便出问题一键关。</summary>
    public bool enabledFit = true;

    bool _baseCaptured;
    Vector2 _baseOffsetMin;
    Vector2 _baseOffsetMax;
    bool _applying;

    void Awake() => Apply();
    void Start() => Apply();

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
