using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 剧情立绘唯一布局入口：原图像素尺寸 + RectMask 裁切；单/双仅槽位不同。
/// 禁止按屏幕比例缩放/拉伸立绘。
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
        Context ctx)
    {
        ResetHost(left, leftLay);
        ResetHost(right, rightLay);
        ApplySlot(left, leftSp, Slot.Left, profile, ctx);
        ApplySlot(right, rightSp, Slot.Right, profile, ctx);
    }

    public static void ApplySolo(
        Image center, Image hideSide,
        Sprite sp,
        LayoutSnapshot centerLay, LayoutSnapshot hideLay,
        StoryPortraitLayout.Profile profile,
        Context ctx)
    {
        ResetHost(center, centerLay);
        ResetHost(hideSide, hideLay);
        if (hideSide != null)
            hideSide.gameObject.SetActive(false);
        ApplySlot(center, sp, Slot.Center, profile, ctx);
    }

    static void ApplySlot(Image img, Sprite sp, Slot slot, StoryPortraitLayout.Profile profile, Context ctx)
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

        // 原图像素尺寸，禁止按屏幕比例缩放/拉伸；仅 RectMask 裁切
        ApplyNativePixelSize(img, out float w, out float h);
        EnsureClipMask(img, profile.clipHeightFrac, w, h);

        var hostRt = GetHostRt(img);
        hostRt.pivot = new Vector2(0.5f, 0f);
        hostRt.anchorMin = hostRt.anchorMax = new Vector2(anchorX, 0f);
        hostRt.anchoredPosition = new Vector2(0f, ResolveBottomY(hostRt, profile, ctx));
        ctx.PlaceBehindDialogueBox?.Invoke(hostRt);
        ClampInsideCanvas(hostRt, ctx.CanvasRt);
    }

    static void ApplyNativePixelSize(Image img, out float w, out float h)
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
        img.rectTransform.sizeDelta = new Vector2(w, h);
    }

    static float ResolveBottomY(RectTransform hostRt, StoryPortraitLayout.Profile profile, Context ctx)
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

        float top = canvasH - 24f;
        float maxY = top - hostRt.sizeDelta.y;
        return Mathf.Clamp(desired, 0f, Mathf.Max(0f, maxY));
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
