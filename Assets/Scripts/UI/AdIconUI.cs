using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「看广告」按钮的广告角标（2026-10-29 主人拍板）：把广告图（Resources/UI/Art/Common/广告）
/// 贴到看广告的按钮上，**不挡字**。
/// 消费点：复活选择窗（广告档把主人摆的钻石 icon 换成广告图，解决 10-25 报过的「观感不匹配」）/
/// 结算「看广告 金币×2」钮（克隆体没有现成 icon → 新建放左端，文字整体右移避让）。
/// 图源：主人 2026-10-29 放入的 Assets/Resources/UI/Art/Common/广告.png（场记板+播放键）。
/// </summary>
public static class AdIconUI
{
    /// <summary>广告图资源路径（Resources 相对路径，主人放入的原图不改名）。</summary>
    public const string SpritePath = "UI/Art/Common/广告";

    static Sprite _sprite;

    /// <summary>广告图（懒加载；取不到返回 null，调用方按铁律报 Error 不静默吞）。</summary>
    public static Sprite Sprite
        => _sprite != null ? _sprite : (_sprite = Resources.Load<Sprite>(SpritePath));

    /// <summary>
    /// 给按钮贴广告图，**不挡字**：
    /// ① 按钮<b>直接子节点</b>上有现成 Image（如复活钮主人摆的钻石 icon）→ 只换 sprite +
    ///    preserveAspect（位置/尺寸全按主人摆的不动，原位本就给图标留了空 → 不会挡字，文字不挪）；
    /// ② 没有现成子图 → 新建一个放按钮左端内侧，按钮里的 Text 整体右移让位。
    /// 返回是否贴上了（图缺失/按钮空 = false，调用方报 Error）。
    /// </summary>
    /// <param name="button">按钮节点（含本体 Image 与子 Text）。</param>
    /// <param name="iconSize">新建图标时的边长（换图分支不适用）。</param>
    public static bool Apply(Transform button, float iconSize = 48f)
    {
        if (button == null)
        {
            Debug.LogError("[AdIconUI] 按钮为空，广告图贴不上");
            return false;
        }
        var sprite = Sprite;
        if (sprite == null)
        {
            Debug.LogError("[AdIconUI] 广告图取不到：Resources/" + SpritePath + " —— 按钮不加广告图");
            return false;
        }
        var rt = button as RectTransform;
        if (rt == null) return false;

        // ① 直接子节点现成 Image（复活钮的钻石 icon）：只换图不换位，文字不动（铁律：不动主人排版）
        Image reuse = null;
        for (int i = 0; i < button.childCount; i++)
        {
            var im = button.GetChild(i).GetComponent<Image>();
            if (im != null) { reuse = im; break; }
        }
        if (reuse != null)
        {
            reuse.sprite = sprite;
            reuse.preserveAspect = true;   // 广告图近方形、钻石窄高，保比例防拉变形
            reuse.gameObject.SetActive(true);
            return true;
        }

        // ② 新建：放左端内侧（垂直居中），并把按钮里的 Text 整体右移避让
        var go = new GameObject("AdIcon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(button, false);
        float btnH = rt.rect.height > 0.01f ? rt.rect.height : 64f;
        float size = Mathf.Clamp(btnH - 16f, 28f, iconSize);
        var irt = go.GetComponent<RectTransform>();
        irt.sizeDelta = new Vector2(size, size);
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(12f + size * 0.5f, 0f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;   // 角标绝不挡点击

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            var lrt = label.rectTransform;
            if (lrt != null)
                lrt.anchoredPosition += new Vector2(size * 0.55f, 0f);   // 给左端图标让位，不挡字
        }
        return true;
    }
}
