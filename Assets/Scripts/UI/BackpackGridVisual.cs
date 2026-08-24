using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包多格装备：在 GridContainer 上铺 Overlay，图标视觉铺满占格区域。
/// 已穿戴的装备仍显示在背包，变暗并叠「已装备」。
/// </summary>
public static class BackpackGridVisual
{
    const string LayerName = "ItemOverlayLayer";
    const float IconPad = 2f;

    public struct ItemPlacement
    {
        public int x, y, w, h;
        public EquipInstance equip;
        public bool equipped;
    }

    /// <param name="cellAt">按 (x,y) 取真实格子；有它就不依赖 GridLayoutGroup 的数值。</param>
    public static void ClearAndPlace(
        RectTransform gridContainer,
        GridLayoutGroup layout,
        IList<ItemPlacement> items,
        Func<int, int, RectTransform> cellAt = null)
    {
        if (gridContainer == null) return;
        var layer = EnsureLayer(gridContainer);
        ClearLayer(layer);

        if (items == null) return;
        bool canUseCells = cellAt != null && cellAt(0, 0) != null;
        if (layout == null && !canUseCells)
        {
            Debug.LogWarning("[BackpackGridVisual] 既没有 GridLayoutGroup 也取不到格子，装备无法定位");
            return;
        }

        float cellW = layout != null ? layout.cellSize.x : 0f;
        float cellH = layout != null ? layout.cellSize.y : 0f;
        float spacingX = layout != null ? layout.spacing.x : 0f;
        float spacingY = layout != null ? layout.spacing.y : 0f;
        int padL = layout != null ? layout.padding.left : 0;
        int padT = layout != null ? layout.padding.top : 0;

        for (int i = 0; i < items.Count; i++)
        {
            var p = items[i];
            if (p.equip == null || p.w < 1 || p.h < 1) continue;

            var go = new GameObject($"Item_{p.x}_{p.y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(layer, false);
            var rt = go.GetComponent<RectTransform>();

            if (canUseCells && TryPlaceByCells(rt.parent as RectTransform ?? gridContainer, cellAt, p, rt))
            {
                // 用真实格子中心对齐到 Overlay 层，避免角色页/战斗页坐标空间不一致
            }
            else
            {
                float left = padL + p.x * (cellW + spacingX);
                float top = padT + p.y * (cellH + spacingY);
                float totalW = p.w * cellW + (p.w - 1) * spacingX;
                float totalH = p.h * cellH + (p.h - 1) * spacingY;

                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(left + IconPad, -(top + IconPad));
                rt.sizeDelta = new Vector2(Mathf.Max(0f, totalW - IconPad * 2f), Mathf.Max(0f, totalH - IconPad * 2f));
            }

            if (p.equip.icon == null)
            {
                p.equip.template?.ResolveIcon();
                if (p.equip.icon == null && p.equip.template != null)
                    p.equip.icon = p.equip.template.icon;
            }

            var img = go.GetComponent<Image>();
            img.sprite = p.equip.icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            bool hasIcon = p.equip.icon != null;
            img.enabled = true;
            // 没图标也留色块，避免「进了背包但看不见」
            img.color = !hasIcon
                ? (p.equipped ? new Color(0.35f, 0.35f, 0.4f, 0.95f) : new Color(0.45f, 0.5f, 0.62f, 0.95f))
                : (p.equipped ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white);

            if (!hasIcon)
                AddNameFallback(go.transform, p.equip.equipName ?? "装备");

            if (p.equipped)
                AddEquippedBadge(go.transform);
        }
    }

    /// <summary>
    /// 用左上格与右下格的世界角点，换算到图标父节点（Overlay）空间，按格子并集的正中心摆放。
    /// </summary>
    static bool TryPlaceByCells(RectTransform space,
        Func<int, int, RectTransform> cellAt, ItemPlacement p, RectTransform rt)
    {
        RectTransform first = cellAt(p.x, p.y);
        if (first == null || space == null) return false;
        RectTransform last = cellAt(p.x + p.w - 1, p.y + p.h - 1) ?? first;

        var corners = new Vector3[4];
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        AccumulateLocalBounds(space, first, corners, ref minX, ref minY, ref maxX, ref maxY);
        if (last != first)
            AccumulateLocalBounds(space, last, corners, ref minX, ref minY, ref maxX, ref maxY);

        if (maxX <= minX || maxY <= minY) return false;

        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(
            Mathf.Max(0f, (maxX - minX) - IconPad * 2f),
            Mathf.Max(0f, (maxY - minY) - IconPad * 2f));
        // Overlay 层 pivot 在正中：父空间 (0,0) 就是格子区域中心，图标 pivot 也在正中，直接对上
        rt.anchoredPosition = center;
        rt.localScale = Vector3.one;
        return true;
    }

    static void AccumulateLocalBounds(RectTransform space, RectTransform target, Vector3[] corners,
        ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        target.GetWorldCorners(corners);
        for (int i = 0; i < 4; i++)
        {
            Vector3 local = space.InverseTransformPoint(corners[i]);
            if (local.x < minX) minX = local.x;
            if (local.x > maxX) maxX = local.x;
            if (local.y < minY) minY = local.y;
            if (local.y > maxY) maxY = local.y;
        }
    }

    static void AddNameFallback(Transform parent, string name)
    {
        var go = new GameObject("NameFallback", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = string.IsNullOrEmpty(name) ? "装备" : name;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 14;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(4f, 4f);
        tr.offsetMax = new Vector2(-4f, -4f);
    }

    static void AddEquippedBadge(Transform parent)
    {
        var go = new GameObject("EquippedBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 22f);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var t = textGo.AddComponent<Text>();
        t.text = "已装备";
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 14;
        t.color = new Color(1f, 0.92f, 0.55f, 1f);
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
    }

    static Transform EnsureLayer(RectTransform gridContainer)
    {
        var exist = gridContainer.Find(LayerName);
        if (exist != null)
        {
            EnsureLayerStretched(exist as RectTransform);
            return exist;
        }
        var go = new GameObject(LayerName, typeof(RectTransform));
        go.transform.SetParent(gridContainer, false);
        EnsureLayerStretched(go.GetComponent<RectTransform>());
        go.transform.SetAsLastSibling();
        return go.transform;
    }

    /// <summary>
    /// Overlay 层必须铺满 GridContainer。
    /// GridContainer 上有 GridLayoutGroup 时，这一层会被当成一个格子塞进去压成小方块，
    /// 装备就全跑到左上角第一格里看不见了 —— 所以必须 ignoreLayout。
    /// </summary>
    static void EnsureLayerStretched(RectTransform rt)
    {
        if (rt == null) return;
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }

    static void ClearLayer(Transform layer)
    {
        if (layer == null) return;
        for (int i = layer.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(layer.GetChild(i).gameObject);
    }
}
