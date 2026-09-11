using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇功能页统一半透黑罩：大厅主界面当底，内容叠在罩上。
/// </summary>
public static class TownPageDim
{
    public const string NodeName = "TownPageDim";
    public const float DefaultAlpha = 0.5f;

    /// <summary>在功能页根下最底层挂半透黑；已有则只刷新颜色与置底。</summary>
    public static Image Ensure(Transform pageRoot, float alpha = DefaultAlpha)
    {
        if (pageRoot == null) return null;

        Transform existing = pageRoot.Find(NodeName);
        Image img;
        if (existing != null)
        {
            img = existing.GetComponent<Image>();
            if (img == null) img = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            var go = new GameObject(NodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(pageRoot, false);
            img = go.GetComponent<Image>();
        }

        var rt = img.rectTransform;
        UiLayoutStretch.ApplyFillScreen(rt);
        img.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        img.raycastTarget = true;
        img.transform.SetAsFirstSibling();
        return img;
    }

    public static void Remove(Transform pageRoot)
    {
        if (pageRoot == null) return;
        var t = pageRoot.Find(NodeName);
        if (t != null)
            Object.Destroy(t.gameObject);
    }
}
