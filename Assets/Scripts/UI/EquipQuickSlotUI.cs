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
    /// <summary>
    /// 【2026-10-06 主人拍板】右上角「新」角标：本拍抽奖刚拿到的<b>装备</b>才显示，点「继续」后清。
    /// 节点按需运行时补建（不显示就不建，不往预制体里落东西）。
    /// </summary>
    [System.NonSerialized] public Text newBadge;
    /// <summary>【2026-10-09 主人拍板】槽位上补的透明点击层按钮（运行时建，不落预制体）。</summary>
    [System.NonSerialized] public Button statTipButton;

    public void Bind(EquipInstance item)
    {
        boundItem = item;
        if (root != null) root.SetActive(true);
        // 【2026-10-09 主人拍板】装备槽可点：点开属性浮框，再点同一个关闭
        WireStatTipClick();

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
        // 2026-10-06 主人拍板：装备槽下面的「头 / 脚」底字一律去掉，只隐藏文字节点
        if (slotLabel != null) slotLabel.gameObject.SetActive(false);
        // 【2026-10-06 主人拍板】稀有度**只画槽外沿那圈描边**，绝不加到装备图片上（图标恒为原样）。
        EquipRarityRim.Apply(root != null ? root.transform : null, has ? (Rarity?)item.rarity : null);
        // 【2026-10-06 主人拍板】「新」角标：判据只有 NewLootMarks.Has 一处（点「继续」后统一清）。
        SetNewBadge(has && NewLootMarks.Has(NewLootMarks.KindEquip, item.templateId));
    }

    public void Clear() => Bind(null);

    /// <summary>
    /// 右上角「新」角标显隐。不显示时<b>不建节点</b>（避免给每个空槽都挂一棵子树）；
    /// 已经建过的就复用。与技能槽的「新」同一口径：红色、右上角、字号 14。
    /// </summary>
    public void SetNewBadge(bool isNew)
    {
        if (newBadge == null)
        {
            if (!isNew || root == null) return;
            newBadge = EnsureNewBadge(root.transform);
        }
        newBadge.text = isNew ? "新" : "";
        newBadge.gameObject.SetActive(isNew);
    }

    /// <summary>
    /// 【2026-10-09 主人拍板】给装备槽补一层透明点击层 + Button，点一下弹装备属性框、再点一下关掉。
    /// 不碰预制体、不加美术：透明 Image 只负责接射线（槽位底图 raycastTarget 未必开着），
    /// Button 挂在它上面，与槽里原有的图标 / 稀有度描边 / 「新」角标共存，不影响拖拽换装。
    /// 幂等：已有就复用，只重绑 onClick。
    /// </summary>
    void WireStatTipClick()
    {
        if (root == null) return;
        if (statTipButton == null)
        {
            var hit = new GameObject("EquipStatClick", typeof(RectTransform), typeof(Image));
            hit.transform.SetParent(root.transform, false);
            hit.transform.SetAsLastSibling();
            var rt = hit.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = hit.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);   // 全透明，纯接射线
            img.raycastTarget = true;
            statTipButton = hit.AddComponent<Button>();
            statTipButton.transition = Selectable.Transition.None;
            statTipButton.targetGraphic = img;
        }
        statTipButton.onClick.RemoveAllListeners();
        statTipButton.onClick.AddListener(OnStatTipClick);
    }

    /// <summary>空槽不弹空框：没装备就把已开的浮框收掉。</summary>
    void OnStatTipClick()
    {
        var ui = BattleUI.Instance;
        if (ui == null) return;
        var anchor = root != null ? root.GetComponent<RectTransform>() : null;
        if (boundItem == null || anchor == null)
        {
            EquipStatTipUI.Ensure(ui.transform)?.Hide();
            return;
        }
        EquipStatTipUI.Toggle(boundItem, anchor, root);
    }

    static Text EnsureNewBadge(Transform parent)
    {
        var go = new GameObject("EquipNew", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.58f, 0.76f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<Text>();
        t.text = "新";
        t.alignment = TextAnchor.UpperRight;
        t.fontSize = 14;
        t.color = new Color(1f, 0.34f, 0.28f);
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;
        return t;
    }
}
