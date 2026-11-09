using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 剧情立绘唯一布局入口：原图像素尺寸 × 屏幕系数（720×1280 为基准，只放大不缩小）+ RectMask 裁切；单/双仅槽位不同。
/// 2026-09-28 前的旧规「禁止按屏幕比例缩放」已被主人推翻：大分辨率机型立绘要跟着放大。
/// </summary>
public static class StoryPortraitPresenter
{
    public enum Slot
    {
        Left,
        Right,
        Center
    }

    public struct LayoutSnapshot
    {
        public Vector2 amin, amax, pivot, pos;
        public Vector3 scale;
    }

    public struct Context
    {
        public RectTransform CanvasRt;
        public Image DialogueBox;
        public float MobileLiftY;
        public Action<Transform> PlaceBehindDialogueBox;
        /// <summary>
        /// 立绘内容归一化开关（2026-10-15 主人）：把「角色内容」缩放到统一高、脚底对齐，
        /// 抵消各图透明留白差异。默认 false —— 只有情报对话开启，主线剧情排版完全不受影响。
        /// </summary>
        public bool NormalizeSize;
    }

    public static LayoutSnapshot Capture(Image img)
    {
        var lay = new LayoutSnapshot();
        if (img == null) return lay;
        var rt = img.rectTransform;
        lay.amin = rt.anchorMin;
        lay.amax = rt.anchorMax;
        lay.pivot = rt.pivot;
        lay.pos = rt.anchoredPosition;
        lay.scale = rt.localScale;
        if (lay.scale.sqrMagnitude < 1e-6f)
            lay.scale = Vector3.one;
        return lay;
    }

    public static void RestoreLayout(Image img, LayoutSnapshot lay)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        rt.anchorMin = lay.amin;
        rt.anchorMax = lay.amax;
        rt.pivot = lay.pivot;
        rt.anchoredPosition = lay.pos;
        rt.localScale = lay.scale.sqrMagnitude < 1e-6f ? Vector3.one : lay.scale;
        img.gameObject.SetActive(true);
    }

    public static void ResetHost(Image img, LayoutSnapshot lay)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        if (rt.parent != null && rt.parent.name == "PortraitClip")
        {
            var clip = rt.parent;
            var originalParent = clip.parent as RectTransform;
            int sib = clip.GetSiblingIndex();
            rt.SetParent(originalParent, false);
            rt.SetSiblingIndex(sib);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(clip.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(clip.gameObject);
        }
        RestoreLayout(img, lay);
    }

    public static void ApplyDual(
        Image left, Image right,
        Sprite leftSp, Sprite rightSp,
        LayoutSnapshot leftLay, LayoutSnapshot rightLay,
        StoryPortraitLayout.Profile profile,
        Context ctx,
        string leftPortraitId, string rightPortraitId)
    {
        ResetHost(left, leftLay);
        ResetHost(right, rightLay);
        ApplySlot(left, leftSp, Slot.Left, profile, ctx, leftPortraitId);
        ApplySlot(right, rightSp, Slot.Right, profile, ctx, rightPortraitId);
    }

    public static void ApplySolo(
        Image center, Image hideSide,
        Sprite sp,
        LayoutSnapshot centerLay, LayoutSnapshot hideLay,
        StoryPortraitLayout.Profile profile,
        Context ctx,
        string portraitId)
    {
        ResetHost(center, centerLay);
        ResetHost(hideSide, hideLay);
        if (hideSide != null)
            hideSide.gameObject.SetActive(false);
        ApplySlot(center, sp, Slot.Center, profile, ctx, portraitId);
    }

    static void ApplySlot(Image img, Sprite sp, Slot slot, StoryPortraitLayout.Profile profile, Context ctx, string portraitId)
    {
        if (img == null) return;
        img.sprite = sp;
        img.enabled = sp != null;
        img.gameObject.SetActive(sp != null);
        if (sp == null) return;

        img.color = Color.white;
        var portraitRt = img.rectTransform;
        float anchorX = slot == Slot.Left ? 0.18f : slot == Slot.Right ? 0.82f : 0.5f;
        // 仅 X 朝向翻转，禁止沿用预制体 0.9 等缩放（会显得被拉大/压扁）
        float faceX = slot == Slot.Right ? -1f : 1f;
        portraitRt.localScale = new Vector3(faceX, 1f, 1f);
        portraitRt.anchorMin = portraitRt.anchorMax = new Vector2(anchorX, 0f);
        portraitRt.pivot = new Vector2(0.5f, 0f);
        portraitRt.anchoredPosition = Vector2.zero;

        // 原图像素尺寸 × 屏幕系数；仅 RectMask 裁切，不拉伸变形
        // 2026-09-28 主人拍板：以 720×1280 设计分辨率的效果为标准，逻辑画布更高（更瘦屏）时
        // 立绘等比放大，标准屏保持原效果；系数下限 1，**只放大不缩小**（修「大分辨率机上立绘偏小」）。
        float scale = ResolvePortraitScale(ctx);
        ApplyNativePixelSize(img, scale, out float w, out float h);
        // 【2026-11-05 主人报「引导的这两个立绘太大」】额外缩放表（StoryPortraitLayout.PortraitExtraScale）：
        // 引导坐姿两图随归一化放大到 968 高后过大，统一缩 20%；只缩尺寸，遮罩/位置旋钮不动。
        float extraScale = StoryPortraitLayout.GetExtraScale(portraitId);
        if (!Mathf.Approximately(extraScale, 1f))
        {
            w *= extraScale;
            h *= extraScale;
            img.rectTransform.sizeDelta = new Vector2(w, h);
        }

        // 【2026-10-15 主人报「人物现在大小不一致」「玩家的形象偏小偏下」】
        // 上面按「整张 sprite 像素」定尺寸，但各图透明留白不同：留白多的图角色就偏小、偏下。
        // 这里按实测内容包围盒把角色高统一到 PortraitTargetCharHeightPx，并把脚底对齐到同一基准线。
        // 仅 ctx.NormalizeSize 开启时生效（情报对话），主线剧情一律走原逻辑。
        float footAdjustPx = 0f;
        if (ctx.NormalizeSize && StoryPortraitLayout.TryGetContentFit(portraitId, out var fit))
        {
            float rawH = sp.rect.height;
            float k = StoryPortraitLayout.PortraitTargetCharHeightPx / Mathf.Max(1f, rawH * fit.charHeightFrac);
            w *= k;
            h *= k;
            img.rectTransform.sizeDelta = new Vector2(w, h);
            footAdjustPx = StoryPortraitLayout.PortraitFootBaselinePx * scale - fit.belowFrac * h;
        }

        EnsureClipMask(img, StoryPortraitLayout.GetClipHeightFrac(portraitId), w, h);

        // 【2026-11-09 主人「立绘往上，mask 不跟着往上」】只把立绘图片在遮罩里往上挪 50；
        // PortraitClip（裁切窗）的位置 / 尺寸一律不动 —— 裁切线仍贴对话框顶，脚不会被抬出来。
        // 必须放在 EnsureClipMask 之后：该方法会把图片锚点设为父顶、anchoredPosition 归零。
        // 直接赋值（不累加）：ResetHost 每次重建 clip，重复调用不会越抬越高。
        float standingLift = StoryPortraitLayout.GetStandingLiftPx(portraitId);
        if (standingLift > 0f)
        {
            var p = portraitRt.anchoredPosition;
            portraitRt.anchoredPosition = new Vector2(p.x, standingLift);
        }

        var hostRt = GetHostRt(img);
        hostRt.pivot = new Vector2(0.5f, 0f);
        hostRt.anchorMin = hostRt.anchorMax = new Vector2(anchorX, 0f);
        // 【2026-10-15 主人，2026-11-07 下调 20】引导局坐姿立绘（会长/前台小姐）额外上抬 60；其他人物不在名单、恒 0，位置照旧
        float bottomY = ResolveBottomY(hostRt, profile, ctx, portraitId);
        bottomY += StoryPortraitLayout.GetTutorialDeskLiftPx(portraitId);
        bottomY += footAdjustPx;
        hostRt.anchoredPosition = new Vector2(0f, bottomY);
        ctx.PlaceBehindDialogueBox?.Invoke(hostRt);
        ClampInsideCanvas(hostRt, ctx.CanvasRt);

        // 【2026-11-09 主人「上和左右的裁边都到屏幕外，只有下面的裁边保持跟原来设定一致」】
        // 放在最后：ResolveBottomY / PlaceBehindDialogueBox / ClampInsideCanvas 都已按原尺寸裁切窗算完，
        // 底边 y 与 11-04 定稿完全一致；这里只把裁切窗上 / 左 / 右三边推到画布外，底边原地不动。
        ExpandClipEdges(hostRt, portraitRt, ctx.CanvasRt);
    }

    /// <summary>
    /// 立绘屏幕系数：逻辑画布高 / 设计高（720×1280 为基准）。
    /// 标准屏=1（维持美术定稿效果）；更瘦屏（如 1240×2772 逻辑高≈1610）≈1.26 等比放大；
    /// 更宽/更矮屏钳回 1，只放大不缩小。裁切窗用缩放后的尺寸，比例关系不变。
    /// 瘦屏再叠一层自适应放大（StoryPortraitLayout.TallScreenScaleBoost，按 k-1 线性，
    /// 2026-10-19 主人：大分辨率手机立绘偏下 → 放大+上移试试水）；标准屏 k=1 该层恒为 0。
    /// </summary>
    static float ResolvePortraitScale(Context ctx)
    {
        float canvasH = ctx.CanvasRt != null ? ctx.CanvasRt.rect.height : 0f;
        if (canvasH < 64f) canvasH = GameConfig.DESIGN_HEIGHT;
        float k = canvasH / Mathf.Max(1f, GameConfig.DESIGN_HEIGHT);
        if (k > 1f)
            k *= 1f + (k - 1f) * StoryPortraitLayout.TallScreenScaleBoost;
        return Mathf.Max(1f, k);
    }

    static void ApplyNativePixelSize(Image img, float scale, out float w, out float h)
    {
        w = 0f;
        h = 0f;
        if (img == null) return;
        img.preserveAspect = true;
        img.type = Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 1f;
        var sp = img.sprite;
        if (sp == null)
        {
            img.enabled = false;
            return;
        }
        img.enabled = true;
        // 直接用 Sprite 源像素矩形，避免错误导入 / PPU 导致变形
        w = sp.rect.width;
        h = sp.rect.height;
        if (w < 1f) w = sp.texture != null ? sp.texture.width : 1f;
        if (h < 1f) h = sp.texture != null ? sp.texture.height : 1f;
        img.rectTransform.sizeDelta = new Vector2(w * scale, h * scale);
    }

    static float ResolveBottomY(RectTransform hostRt, StoryPortraitLayout.Profile profile, Context ctx, string portraitId)
    {
        float canvasH = ctx.CanvasRt != null ? ctx.CanvasRt.rect.height : GameConfig.DESIGN_HEIGHT;
        if (canvasH < 64f) canvasH = GameConfig.DESIGN_HEIGHT;

        float desired = canvasH * profile.bottomScreenFrac + ctx.MobileLiftY;
        if (ctx.DialogueBox != null)
        {
            var boxRt = ctx.DialogueBox.rectTransform;
            Canvas.ForceUpdateCanvases();
            float boxTop = boxRt.anchoredPosition.y + boxRt.rect.height;
            desired = Mathf.Max(desired, boxTop + profile.dialogueTopGapPx);
        }
        desired += profile.bottomOffsetPx;
        desired += StoryPortraitLayout.GetBottomOffsetPx(portraitId);

        // 【2026-11-09 口径更新：仅剧情对话立绘这块覆盖 10-15「遮罩永远不要动」；别处遮罩照旧、不要动】
        // 本链路裁切窗的上 / 左 / 右三边由 ExpandClipEdges 统一推到画布外，只留底边裁切；
        // 底边 y 仍由本方法算，一律不要挪。本文件只服务 DialogueUI 的剧情对话立绘，其它界面不走这里。
        // 下移补偿只保留给基准窗高=整图的坐姿立绘（会长/前台小姐）：不补则 0.72 基准的可见顶边
        // 整体上浮（522 高 ≈146px）。站立立绘基准窗高已在 0.72 原位（脚从来是底边切线在框后裁掉的），
        // 不再做任何补偿——10-15 主人报「脚露出来」即窗被开到整图所致，回 0.72 原位即愈；
        // 禁止钳窗底 / 挪遮罩底边来凑位置（上一版那么修已被主人否掉）。
        float hostH = hostRt != null ? hostRt.sizeDelta.y : 0f;
        float maskFrac = Mathf.Clamp(StoryPortraitLayout.GetClipHeightFrac(portraitId), 0.01f, 1f);
        if (hostH > 1f && maskFrac >= 0.99f && profile.clipHeightFrac > 0.01f && profile.clipHeightFrac < 1f)
        {
            float fullH = hostH / maskFrac;
            desired -= fullH * (1f - profile.clipHeightFrac);
        }

        float top = canvasH - 24f;
        // 【2026-10-19 主人：大分辨率手机立绘偏下 → 放大+上移试试水】
        // 瘦屏（逻辑高>1280）按 k-1 线性自适应上移；顶部封顶线同步放宽——
        // 否则立绘顶贴着 24px 安全线，上移量会被下方 maxY 钳制原封吃掉。
        // 放宽量封顶 TallScreenTopMaxLiftPx（防独眼等头顶留白小的图裁发梢）。
        // 标准屏 k=1 时 lift 恒 0，封顶线照旧，效果与现在完全一致。
        // 【2026-10-23 主人拍板：瘦屏立绘再上抬】封顶从一刀切 48 改为按各图头顶留白个体化：
        // 留白大的图（玩家 213px）吃满 lift，留白小的图（独眼 25px）维持 48 防裁发梢，
        // 表外立绘返回 48 照旧。scale 与 ApplySlot 定尺寸同源（ResolvePortraitScale）。
        float kLift = canvasH / Mathf.Max(1f, GameConfig.DESIGN_HEIGHT);
        float lift = kLift > 1f ? (kLift - 1f) * StoryPortraitLayout.TallScreenLiftPx : 0f;
        desired += lift;
        top += Mathf.Min(lift, StoryPortraitLayout.GetTopMaxLiftPx(portraitId, ResolvePortraitScale(ctx)));
        float maxY = top - hostRt.sizeDelta.y;
        // 只封顶（顶边不入屏顶 24px 安全区），放开 0 下限：整图高度可超画布，
        // 底部允许垂到画布外/对话框后面；保留 0 下限会把超高立绘整体压到 y=0、顶边反被屏幕裁掉。
        return Mathf.Min(desired, maxY);
    }

    public static RectTransform GetHostRtPublic(Image portrait) => GetHostRt(portrait);

    static RectTransform GetHostRt(Image portrait)
    {
        if (portrait == null) return null;
        var rt = portrait.rectTransform;
        if (rt.parent != null && rt.parent.name == "PortraitClip")
            return rt.parent as RectTransform;
        return rt;
    }

    static void EnsureClipMask(Image portrait, float clipHeightFrac, float clipWidth, float displayHeight)
    {
        if (portrait == null || portrait.sprite == null) return;

        var rt = portrait.rectTransform;
        RectTransform clipRt;
        if (rt.parent != null && rt.parent.name == "PortraitClip")
        {
            clipRt = rt.parent as RectTransform;
        }
        else
        {
            var originalParent = rt.parent as RectTransform;
            var clipGo = new GameObject("PortraitClip", typeof(RectTransform), typeof(RectMask2D));
            clipRt = clipGo.GetComponent<RectTransform>();
            clipRt.SetParent(originalParent, false);
            clipRt.anchorMin = rt.anchorMin;
            clipRt.anchorMax = rt.anchorMax;
            clipRt.pivot = rt.pivot;
            clipRt.anchoredPosition = rt.anchoredPosition;
            clipRt.sizeDelta = rt.sizeDelta;
            clipRt.localScale = Vector3.one;
            int sib = rt.GetSiblingIndex();
            rt.SetParent(clipRt, false);
            rt.SetSiblingIndex(0);
            clipRt.SetSiblingIndex(sib);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
        }

        clipRt.pivot = new Vector2(0.5f, 0f);
        float h = displayHeight > 1f ? displayHeight : rt.sizeDelta.y;
        if (h < 1f) h = portrait.sprite.rect.height;
        // 裁切窗宽=原图宽，高=原图高×裁切比例；不把图拉进更大的框
        clipRt.sizeDelta = new Vector2(Mathf.Max(1f, clipWidth), h * Mathf.Clamp01(clipHeightFrac));
    }

    /// <summary>
    /// 【2026-11-09 主人拍板】把裁切窗的上 / 左 / 右三边推到画布外，只留底边裁切。
    /// clip 的 pivot=(0.5,0)，改 sizeDelta 只会让顶边往上扩、左右往外扩，底边原地不动；
    /// 底边 y 由 ResolveBottomY 定（本方法在它之后调用，不改它的输入）。
    /// 图片顶边原本贴裁切窗顶边（anchor/pivot=(0.5,1)），窗变高后必须按差值把图片拉回原屏幕位置，
    /// 否则人物会被一起顶到画布外；拉回时保留外面已设好的站立上抬量（p.y）。
    /// </summary>
    static void ExpandClipEdges(RectTransform clipRt, RectTransform imageRt, RectTransform canvasRt)
    {
        if (clipRt == null || canvasRt == null) return;
        float span = Mathf.Max(canvasRt.rect.width, canvasRt.rect.height);
        if (span < 64f) span = GameConfig.DESIGN_HEIGHT;

        float oldH = clipRt.sizeDelta.y;
        float newW = Mathf.Max(clipRt.sizeDelta.x, span * 3f);
        float newH = Mathf.Max(oldH, span * 2f);
        clipRt.sizeDelta = new Vector2(newW, newH);

        if (imageRt != null)
        {
            var p = imageRt.anchoredPosition;
            imageRt.anchoredPosition = new Vector2(p.x, p.y - (newH - oldH));
        }
    }

    static void ClampInsideCanvas(RectTransform rt, RectTransform canvasRt)
    {
        if (rt == null || canvasRt == null) return;
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        var canvasCorners = new Vector3[4];
        rt.GetWorldCorners(corners);
        canvasRt.GetWorldCorners(canvasCorners);
        float pad = 8f;
        float minCx = canvasCorners[0].x + pad;
        float maxCx = canvasCorners[2].x - pad;

        float minX = corners[0].x, maxX = corners[0].x;
        for (int i = 1; i < 4; i++)
        {
            if (corners[i].x < minX) minX = corners[i].x;
            if (corners[i].x > maxX) maxX = corners[i].x;
        }

        Vector3 shift = Vector3.zero;
        if (minX < minCx) shift.x += minCx - minX;
        if (maxX > maxCx) shift.x -= maxX - maxCx;
        if (shift.sqrMagnitude > 1e-6f)
            rt.position += shift;
    }
}
