using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵养成 UI 构件（纯代码补节点，不依赖预制体）：
///   · <b>本命碎片卡</b> = 碎片底版 + 人物头像，用**底版图自身的 alpha** 当遮罩裁头像
///   · <b>职业徽记角标</b> = 6 职业 × 普通/稀有/传奇，可带持有数量
///
/// 素材键与加载见 <see cref="MercGrowSprites"/>。两层顺序由 <see cref="FrameOnTop"/> 控制：
///   true （默认）= 底版压在头像上层 → 底版纹样一定看得见（相框罩在照片上）
///   false       = 底版在下、头像盖在上面 → 头像填满底版形状（纹样只从头像透明处露出）
/// 视觉上想换一种，改这一个开关即可，不用动节点。
/// </summary>
public static class MercGrowUI
{
    /// <summary>底版是否压在头像上层。</summary>
    public static bool FrameOnTop = true;

    // ==================== 本命碎片卡 ====================

    /// <summary>碎片卡节点引用。子节点名固定为 Clip / Clip/Portrait / Frame，方便外部微调。</summary>
    public class FragmentCard
    {
        public GameObject Root;
        public RectTransform Rect;
        /// <summary>遮罩层：底版图自身 alpha 作为裁剪形状（showMaskGraphic 见 FrameOnTop）。</summary>
        public Image Clip;
        /// <summary>人物头像，被底版形状裁剪。</summary>
        public Image Portrait;
        /// <summary>底版图层。FrameOnTop=false 时为 null（此时由 Clip 自己显示底版）。</summary>
        public Image Frame;

        public MercRosterDefs.MercRarity Rarity;
        public string HireId;

        public void Apply(MercRosterDefs.MercRarity rarity, string hireIdOrAssetId)
        {
            Rarity = rarity;
            HireId = hireIdOrAssetId;

            var baseSp = MercGrowSprites.LoadFragmentBase(rarity);
            if (Clip != null)
            {
                Clip.sprite = baseSp;
                Clip.enabled = baseSp != null;
                var mask = Clip.GetComponent<Mask>();
                if (mask != null) mask.enabled = baseSp != null;   // 没底版就不裁，宁可露全头像
            }
            if (Frame != null)
            {
                Frame.sprite = baseSp;
                Frame.enabled = baseSp != null;
            }
            if (Portrait != null)
            {
                var head = MercPortraitSprites.GetHead(hireIdOrAssetId);
                if (head == null) head = MercPortraitSprites.GetStand(hireIdOrAssetId);
                Portrait.sprite = head;
                Portrait.enabled = head != null;
            }
        }
    }

    /// <summary>建一张本命碎片卡。<paramref name="size"/> 为底版边长（方形）。</summary>
    public static FragmentCard CreateFragmentCard(Transform parent, string name,
        MercRosterDefs.MercRarity rarity, string hireIdOrAssetId, float size)
    {
        var root = NewNode(name, parent, size, size);
        var card = new FragmentCard
        {
            Root = root,
            Rect = root.GetComponent<RectTransform>(),
        };

        // 遮罩层：拿底版图自己当 Mask，FrameOnTop 时它只负责裁、不显示
        var clipGo = NewStretchImage("Clip", root.transform, null);
        card.Clip = clipGo.GetComponent<Image>();
        clipGo.AddComponent<Mask>().showMaskGraphic = !FrameOnTop;

        var portraitGo = NewStretchImage("Portrait", clipGo.transform, null);
        card.Portrait = portraitGo.GetComponent<Image>();

        if (FrameOnTop)
        {
            var frameGo = NewStretchImage("Frame", root.transform, null);
            card.Frame = frameGo.GetComponent<Image>();
        }

        card.Apply(rarity, hireIdOrAssetId);
        return card;
    }

    // ==================== 职业徽记 ====================

    public static Image CreateBadge(Transform parent, string name, string jobName,
        MercRosterDefs.MercRarity rarity, float size)
    {
        var img = NewFixedImage(name, parent, size).GetComponent<Image>();
        RefreshBadge(img, jobName, rarity);
        return img;
    }

    public static Image CreateBadgeForMerc(Transform parent, string name, string hireIdOrAssetId,
        MercRosterDefs.MercRarity rarity, float size)
    {
        var img = NewFixedImage(name, parent, size).GetComponent<Image>();
        RefreshBadge(img, MercRosterDefs.GetJobName(hireIdOrAssetId), rarity);
        return img;
    }

    public static void RefreshBadge(Image badge, string jobName, MercRosterDefs.MercRarity rarity)
    {
        if (badge == null) return;
        var sp = MercGrowSprites.LoadJobBadge(jobName, rarity);
        badge.sprite = sp;
        badge.enabled = sp != null;
    }

    /// <summary>徽记右下角的持有数量。首次调用时补建，之后只改文本。</summary>
    public static Text EnsureBadgeCount(Image badge)
    {
        if (badge == null) return null;
        var found = badge.transform.Find("Count");
        if (found != null) return found.GetComponent<Text>();

        var go = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(badge.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(56f, 26f);

        var t = go.GetComponent<Text>();
        t.fontSize = Mathf.Max(14, Mathf.RoundToInt(badge.rectTransform.rect.width * 0.42f));
        t.alignment = TextAnchor.LowerRight;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    /// <summary>写徽记数量；<paramref name="count"/> &lt; 0 表示不显示。</summary>
    public static void SetBadgeCount(Image badge, int count)
    {
        if (badge == null) return;
        if (count < 0)
        {
            var old = badge.transform.Find("Count");
            if (old != null) old.gameObject.SetActive(false);
            return;
        }
        var t = EnsureBadgeCount(badge);
        if (t == null) return;
        t.gameObject.SetActive(true);
        t.text = count.ToString();
    }

    // ==================== 节点工具 ====================

    /// <summary>固定尺寸节点（锚点居中），由调用方自己设 anchoredPosition。</summary>
    static GameObject NewNode(string name, Transform parent, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static GameObject NewFixedImage(string name, Transform parent, float size)
    {
        var go = NewNode(name, parent, size, size);
        AddImage(go);
        return go;
    }

    /// <summary>铺满父节点的图层，无命中（装饰用）。</summary>
    static GameObject NewStretchImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;   // 底版/头像是方形图，按 rect 铺满即可
        img.color = Color.white;
        return go;
    }

    static Image AddImage(GameObject go)
    {
        if (go.GetComponent<CanvasRenderer>() == null) go.AddComponent<CanvasRenderer>();
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.sprite = null;
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.color = Color.white;
        return img;
    }
}
