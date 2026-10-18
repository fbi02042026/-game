using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：UI 小工具（Stretch/AnchorFillRect/AnchorTopStrip/SetRect/CreateImg/CreateTxt）
public partial class DailyLoginUI
{

    // ============================================================
    // UI 小工具
    // ============================================================

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>按屏幕竖向比例铺一条横带（minY/maxY 为 0~1）。</summary>
    static void AnchorFillRect(RectTransform rt, float minY, float maxY)
    {
        rt.anchorMin = new Vector2(0f, minY);
        rt.anchorMax = new Vector2(1f, maxY);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>贴父级顶部的通栏标签条。</summary>
    static void AnchorTopStrip(RectTransform rt, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -h);
        rt.offsetMax = new Vector2(0f, 0f);
    }

    static void SetRect(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Image CreateImg(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text CreateTxt(Transform parent, string name, string content, int size, TextAnchor align,
        bool wrapVertical = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = wrapVertical ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }
}
