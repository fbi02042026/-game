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
    const string LockOverlayName = "LockedOverlay";
    const string LockIconName = "LockIcon";

    public struct ItemPlacement
    {
        public int x, y, w, h;
        public EquipInstance equip;
        /// <summary>道具。2026-09-15 起背包格子主要就是装它；与 equip 互斥。</summary>
        public ItemInstance item;
        public bool equipped;

        public bool IsItem => item != null;
    }

    /// <param name="cellAt">按 (x,y) 取真实格子；有它就不依赖 GridLayoutGroup 的数值。</param>
    public static void ClearAndPlace(
        RectTransform gridContainer,
        GridLayoutGroup layout,
        IList<ItemPlacement> items,
        Func<int, int, RectTransform> cellAt = null,
        bool enableDrag = false)
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
            bool isItem = p.item != null;
            if (p.w < 1 || p.h < 1) continue;
            if (!isItem && p.equip == null) continue;

            var go = new GameObject($"Item_{p.x}_{p.y}", typeof(RectTransform));
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

            Sprite sprite;
            if (isItem) sprite = ItemDefs.LoadIcon(p.item.defId);
            else { EquipIcons.Resolve(p.equip); sprite = p.equip.icon; }

            bool hasIcon = sprite != null;
            // 图标画在子节点上，宿主只负责跨格占位；多格时子节点按宽等比后垂直居中
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            var img = iconGo.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.enabled = true;
            PlaceIconInHost(rt, iconRt, img, sprite, p.h, isItem ? null : p.equip);

            if (isItem)
            {
                img.color = Color.white;
            }
            else
            {
                EquipRarityMaterials.Apply(img, p.equip.rarity);
                img.color = !hasIcon
                    ? (p.equipped ? new Color(0.35f, 0.35f, 0.4f, 0.95f) : new Color(0.45f, 0.5f, 0.62f, 0.95f))
                    : (p.equipped ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white);
            }

            if (!hasIcon)
                AddNameFallback(go.transform,
                    isItem ? (p.item.Name ?? "道具") : (EquipUiText.EquipTitleWithHand(p.equip) ?? "装备"));

            if (isItem)
            {
                // 可叠加道具显示右下角数量
                if (p.item.count > 1) AddCountBadge(go.transform, p.item.count);
            }
            else
            {
                if (WeaponLoadoutRules.IsLoadoutItem(p.equip))
                    AddHandBadge(go.transform, p.equip);

                if (p.equipped)
                    AddEquippedBadge(go.transform, p.h);
            }

            // 道具恒开射线（要点开「使用/丢弃」浮层）；拖拽本身仍只在开箱整理阶段生效
            bool interactive = enableDrag || isItem;
            if (interactive)
            {
                img.raycastTarget = true;
                var hostImg = go.GetComponent<Image>();
                if (hostImg == null)
                {
                    hostImg = go.AddComponent<Image>();
                    hostImg.color = new Color(1f, 1f, 1f, 0.01f);
                }
                hostImg.raycastTarget = true;
                var drag = go.GetComponent<BattleBackpackItemDrag>();
                if (drag == null) drag = go.AddComponent<BattleBackpackItemDrag>();
                drag.Equip = isItem ? null : p.equip;
                drag.Item = isItem ? p.item : null;
                drag.GridX = p.x;
                drag.GridY = p.y;
                drag.Width = p.w;
                drag.Height = p.h;
            }
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

    /// <summary>
    /// 宿主负责跨格占位；图标子节点在宿主内居中。
    /// 多格装备按宽度等比缩放，避免 Image.preserveAspect 在高矩形里视觉贴顶。
    /// 盾牌图标再缩 20%，格子占位不变。
    /// </summary>
    static void PlaceIconInHost(RectTransform host, RectTransform iconRt, Image img, Sprite sprite, int gridH, EquipInstance equip = null)
    {
        if (host == null || iconRt == null || img == null) return;

        if (sprite == null || gridH <= 1)
        {
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            img.preserveAspect = true;
            if (ShouldShrinkShieldIcon(equip))
                iconRt.localScale = Vector3.one * 0.8f;
            else
                iconRt.localScale = Vector3.one;
            return;
        }

        float hostW = Mathf.Max(1f, host.rect.width);
        float hostH = Mathf.Max(1f, host.rect.height);
        float sprW = Mathf.Max(1f, sprite.rect.width);
        float sprH = Mathf.Max(1f, sprite.rect.height);
        float aspect = sprW / sprH;

        float drawW = hostW;
        float drawH = drawW / aspect;
        if (drawH > hostH)
        {
            drawH = hostH;
            drawW = drawH * aspect;
        }

        if (ShouldShrinkShieldIcon(equip))
        {
            drawW *= 0.8f;
            drawH *= 0.8f;
        }

        img.preserveAspect = false;
        iconRt.localScale = Vector3.one;
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(drawW, drawH);
        iconRt.anchoredPosition = Vector2.zero;
    }

    static bool ShouldShrinkShieldIcon(EquipInstance equip)
    {
        if (equip == null) return false;
        if (WeaponLoadoutRules.IsShield(equip)) return true;
        return equip.slotType == EquipSlotType.OffHand
               && equip.gridWidth == 2 && equip.gridHeight == 2;
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

    static void AddHandBadge(Transform parent, EquipInstance equip)
    {
        string badge = EquipUiText.WeaponHandBadge(equip);
        if (string.IsNullOrEmpty(badge)) return;

        var go = new GameObject("HandBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.2f, 0.75f);
        bg.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(2f, -2f);
        rt.sizeDelta = new Vector2(68f, 24f);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var t = textGo.AddComponent<Text>();
        t.text = badge;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 18;
        t.color = new Color(0.85f, 0.95f, 1f, 1f);
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
    }

    /// <summary>堆叠道具右下角的数量，如「×12」。</summary>
    static void AddCountBadge(Transform parent, int count)
    {
        var go = new GameObject("CountBadge", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = "×" + count;
        t.alignment = TextAnchor.LowerRight;
        t.fontSize = 15;
        t.color = Color.white;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 描边，保证压在任何道具/装备底图上都能看清
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(2f, 1f);
        tr.offsetMax = new Vector2(-3f, -1f);
    }

    /// <summary>
    /// 「已装备」贴在跨格区域顶部。多格装备（如 1×2 剑）图标因 preserveAspect 落在上格，
    /// 原先贴底会看起来像空下格上的标签。
    /// </summary>
    static void AddEquippedBadge(Transform parent, int gridH = 1)
    {
        var go = new GameObject("EquippedBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        // 多格：贴在上半格底边附近（图标常见位置）；单格：贴底
        if (gridH > 1)
        {
            rt.anchorMin = new Vector2(0.08f, 0.5f);
            rt.anchorMax = new Vector2(0.92f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 22f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 22f);
        }

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

    /// <summary>
    /// 未解锁行格子的外观：半透明黑遮罩 + 居中锁图标。
    /// 锁图优先复用项目现有的 Resources/UI/Common/锁（RuntimeLockSprite 内部已做了
    /// “有图用图、无图代码画”的兜底）；不新建美术资源、不改预制体。
    /// 返回的遮罩默认隐藏，由调用方按“行号 >= 已解锁行数”决定是否显示。
    /// </summary>
    public static GameObject EnsureLockOverlay(Transform cell)
    {
        if (cell == null) return null;

        var exist = FindDirectChild(cell, LockOverlayName);
        if (exist != null)
        {
            EnsureLockIcon(exist);
            return exist.gameObject;
        }

        var go = new GameObject(LockOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(cell, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var mask = go.GetComponent<Image>();
        mask.sprite = null;
        mask.color = new Color(0f, 0f, 0f, 0.55f);
        // 吃掉点击：未解锁格不可点、不可放物品（数据层本就拒绝，这里只补上交互层）
        mask.raycastTarget = true;

        EnsureLockIcon(go.transform);
        go.SetActive(false);
        return go;
    }

    /// <summary>
    /// 锁图标按格子的 44% 占比居中；用锚点而非 sizeDelta，
    /// 这样即使创建时 GridLayout 还没重建、rect 为 0 也不会缩成一点。
    /// </summary>
    static void EnsureLockIcon(Transform overlay)
    {
        if (overlay == null) return;
        if (FindDirectChild(overlay, LockIconName) != null) return;

        var go = new GameObject(LockIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(overlay, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.28f, 0.28f);
        rt.anchorMax = new Vector2(0.72f, 0.72f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = RuntimeLockSprite.Get();
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        return null;
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
