using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 装备快捷槽（新底部布局 BackpackPanel/zhuangbei 下的 6 个格子：头/胸甲/手/脚/左手/右手）。
/// 只做展示与点击，穿戴/卸下仍走背包。
/// </summary>
[System.Serializable]
public class EquipQuickSlotUI
{
    public GameObject root;
    public Image iconImage;
    public Text slotLabel;                              // 空槽底字（头/胸甲/…）
    public EquipSlotType slotType = EquipSlotType.Head;
    [System.NonSerialized] public EquipInstance boundItem;
    /// <summary>美术在该节点上放的占位图（临时图）；空槽时还原它，别把节点整个藏掉。</summary>
    [System.NonSerialized] public Sprite placeholderSprite;

    public void Bind(EquipInstance item)
    {
        boundItem = item;
        if (root != null) root.SetActive(true);

        bool has = item != null;
        if (has)
        {
            item.template?.ResolveIcon();
            if (item.icon == null && item.template != null)
                item.icon = item.template.icon ?? EquipIcons.Get(item.template.iconFileName);
        }
        bool showIcon = has && item.icon != null;

        if (iconImage != null)
        {
            // 空槽保留节点可见，回退到美术放的占位图 —— 直接 SetActive(false) 会把槽位变成空洞
            iconImage.gameObject.SetActive(true);
            iconImage.preserveAspect = true;
            iconImage.sprite = showIcon ? item.icon : placeholderSprite;
            iconImage.enabled = iconImage.sprite != null;
        }
        if (slotLabel != null) slotLabel.gameObject.SetActive(!showIcon);
    }

    public void Clear() => Bind(null);
}
