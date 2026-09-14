using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 网格格子UI
/// </summary>
[System.Serializable]
public class GridCellUI
{
    public GameObject root;             // 格子根对象
    public Image cellBg;                // 格子底色（有/无装备区分）
    public Image itemIcon;              // 装备图标
    public Image rarityFrame;           // 品质边框
    public GameObject lockedOverlay;    // 行锁定遮罩（天赋未解锁）
    public int gridX;                   // 格子X坐标
    public int gridY;                   // 格子Y坐标
    public EquipInstance equippedItem;  // 当前装备的物品

    Color _artistBg;
    Color _artistFrame;
    bool _artistCached;

    public RectTransform VisualRect
    {
        get
        {
            if (root != null) return root.GetComponent<RectTransform>();
            if (cellBg != null) return cellBg.rectTransform;
            return null;
        }
    }

    public void CaptureDefaultVisual()
    {
        if (_artistCached) return;
        if (cellBg != null)
        {
            Color c = cellBg.color;
            // 勿把程序占用色当成美术默认（重绑/二次 Capture 会把空格锁成深色）
            if (ApproxColor(c, OccupiedBg) || ApproxColor(c, EquippedBg))
                c = Color.white;
            _artistBg = c;
        }
        if (rarityFrame != null) _artistFrame = rarityFrame.color;
        _artistCached = true;
    }

    static bool ApproxColor(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.04f
            && Mathf.Abs(a.g - b.g) < 0.04f
            && Mathf.Abs(a.b - b.b) < 0.04f;
    }

    // 空格不染色，保留预制体图片本色；仅有装备时才换色
    static readonly Color OccupiedBg = new Color(0.24f, 0.30f, 0.38f, 0.95f);
    static readonly Color EquippedBg = new Color(0.30f, 0.26f, 0.16f, 0.95f);

    /// <summary>底行等：天赋未解锁时显示锁定遮罩，格子本身保持显示（不关节点，避免 GridLayout 重排）。</summary>
    public void SetRowLocked(bool locked)
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);
        if (lockedOverlay != null)
            lockedOverlay.SetActive(locked);
        if (locked)
        {
            equippedItem = null;
            if (itemIcon != null)
            {
                itemIcon.sprite = null;
                itemIcon.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 设置装备（单格）
    /// </summary>
    public void SetItem(EquipInstance item)
    {
        SetItemSpan(item, 1, 1, TownBackpackGrid.CellSize, TownBackpackGrid.CellSpacing);
    }

    /// <summary>
    /// 多格装备：从本格左上角向右下 spanning。
    /// </summary>
    public void SetItemSpan(EquipInstance item, int spanW, int spanH, float cellSize, float spacing)
    {
        equippedItem = item;
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
        if (item == null)
        {
            Clear();
            return;
        }

        if (itemIcon != null)
        {
            itemIcon.sprite = item.icon;
            itemIcon.preserveAspect = true;
            itemIcon.gameObject.SetActive(item.icon != null);
            var rt = itemIcon.rectTransform;
            const float pad = 4f;
            if (spanW <= 1 && spanH <= 1)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(pad, pad);
                rt.offsetMax = new Vector2(-pad, -pad);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
            }
            else
            {
                float totalW = spanW * cellSize + (spanW - 1) * spacing;
                float totalH = spanH * cellSize + (spanH - 1) * spacing;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(pad, -pad);
                rt.sizeDelta = new Vector2(totalW - pad * 2f, totalH - pad * 2f);
            }
        }
        if (rarityFrame != null)
        {
            Color rarityColor = GetRarityColor(item.rarity);
            rarityFrame.color = rarityColor;
        }
    }

    /// <summary>被相邻多格装备占用的格：不重复画图标。</summary>
    public void SetOccupiedNeighbor()
    {
        equippedItem = null;
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.gameObject.SetActive(false);
        }
    }

    public void SetEmptyVisual()
    {
        CaptureDefaultVisual();
        // 空格：还原美术默认色，不刷深色底
        if (cellBg != null) cellBg.color = _artistBg;
        if (rarityFrame != null) rarityFrame.color = _artistFrame;
    }

    public void SetOccupiedVisual(bool equipped)
    {
        CaptureDefaultVisual();
        if (cellBg != null)
            cellBg.color = equipped ? EquippedBg : OccupiedBg;
    }

    /// <summary>
    /// 清空格子
    /// </summary>
    public void Clear()
    {
        equippedItem = null;
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.gameObject.SetActive(false);
        }
        SetEmptyVisual();
    }

    Color GetRarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Common: return new Color(0.7f, 0.7f, 0.7f);
            case Rarity.Uncommon: return Color.green;
            case Rarity.Rare: return Color.blue;
            case Rarity.Epic: return new Color(0.6f, 0.2f, 0.8f);
            case Rarity.Legendary: return new Color(1f, 0.6f, 0f);
            default: return Color.white;
        }
    }
}
