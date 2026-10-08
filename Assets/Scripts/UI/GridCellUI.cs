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
        if (cellBg != null) _artistBg = cellBg.color;
        if (rarityFrame != null) _artistFrame = rarityFrame.color;
        _artistCached = true;
    }

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
            // 统一色表：原 GetRarityColor 用的是 Color.blue / Color.green 纯色，与本项目柔和色板不符。
            Color rarityColor = RarityPalette.Get(item.rarity);
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
        // 2026-10-06 主人拍板：格子底图**恒为美术原色**，不再按占用/已穿戴刷代码色
        // （旧的 OccupiedBg / EquippedBg 已删）；稀有度只由图标外沿描边表达（EquipRarityRim）。
        if (cellBg != null) cellBg.color = _artistBg;
        if (rarityFrame != null) rarityFrame.color = _artistFrame;
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

    // 2026-09-17 删除本地 GetRarityColor：原先用 Color.green / Color.blue 等 Unity 内置纯色，
    // 与技能/佣兵的稀有度色板完全是两套。现统一取 RarityPalette。
}
