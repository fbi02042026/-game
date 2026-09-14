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

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(has);
            iconImage.sprite = has ? item.icon : null;
            iconImage.enabled = has && item.icon != null;
        }
        if (slotLabel != null) slotLabel.gameObject.SetActive(!has);
    }

    public void Clear() => Bind(null);
}
