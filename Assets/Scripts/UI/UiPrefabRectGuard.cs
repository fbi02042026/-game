using UnityEngine;

/// <summary>
/// 保护用户在预制体里调好的 RectTransform（Background / BgArt 等 Fixed 节点）：
/// 实例化后前几帧若被别的脚本改掉，自动还原。勿挂在 BgStretch / Dim 等需 FillScreen 的节点上。
/// </summary>
[DisallowMultipleComponent]
public sealed class UiPrefabRectGuard : MonoBehaviour
{
    struct Snap
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector3 localScale;

        public static Snap Capture(RectTransform rt)
        {
            return new Snap
            {
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                pivot = rt.pivot,
                anchoredPosition = rt.anchoredPosition,
                sizeDelta = rt.sizeDelta,
                offsetMin = rt.offsetMin,
                offsetMax = rt.offsetMax,
                localScale = rt.localScale
            };
        }

        public void Apply(RectTransform rt)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            rt.localScale = localScale;
        }

        public bool Matches(RectTransform rt)
        {
            return rt.anchorMin == anchorMin
                   && rt.anchorMax == anchorMax
                   && rt.pivot == pivot
                   && rt.anchoredPosition == anchoredPosition
                   && rt.sizeDelta == sizeDelta
                   && rt.offsetMin == offsetMin
                   && rt.offsetMax == offsetMax
                   && rt.localScale == localScale;
        }
    }

    RectTransform _rt;
    Snap _snap;
    int _framesLeft;

    public static void Attach(RectTransform rt, int guardFrames = 3)
    {
        if (rt == null) return;
        var g = rt.GetComponent<UiPrefabRectGuard>();
        if (g == null) g = rt.gameObject.AddComponent<UiPrefabRectGuard>();
        g.Begin(guardFrames);
    }

    public static void Attach(Transform root, string childName, int guardFrames = 3)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return;
        Attach(root.Find(childName) as RectTransform, guardFrames);
    }

    void Begin(int guardFrames)
    {
        _rt = transform as RectTransform;
        if (_rt == null) return;
        _snap = Snap.Capture(_rt);
        _framesLeft = Mathf.Max(1, guardFrames);
    }

    void Awake()
    {
        if (_rt == null)
            Begin(3);
    }

    void LateUpdate()
    {
        if (_framesLeft <= 0 || _rt == null) return;
        if (!_snap.Matches(_rt))
            _snap.Apply(_rt);
        _framesLeft--;
    }
}
