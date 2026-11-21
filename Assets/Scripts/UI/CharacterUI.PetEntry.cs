using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色页「宠物」入口：背包面板顶边上方左侧，不重叠。
/// 定位口径（2026-11-21 定）：挂在 Popups 下、与 BackpackPanel 同级——
///   ① 层级在背包之上，不会被背包盖住；
///   ② y 用背包 anchoredPosition 在 Popups 同坐标系纯数值推算，不跨容器做世界坐标换算。
///      旧版把背包 top 换算到 Content 得出 y=841，Fit 改写背包变换后按钮被顶出屏幕。
/// x 对齐属性末行左沿（世界→Popups 局部换算正常，历次都得到 16）。
/// 尺寸沿用 Stage/LeftButton 现读值。图标走 Resources（铁律16），只换 Sprite 不改预制体。
/// </summary>
public partial class CharacterUI
{
    Button _petEntryButton;

    void EnsurePetEntry()
    {
        if (_petEntryButton != null) return;
        var content = transform.Find("Content");
        var attrPanel = content != null ? content.Find("AttrPanel") : null;
        // AttrCritRoot 是 AttrPanel 的直接子节点（panl 只是底板，不是父节点）
        var lastRow = attrPanel != null ? attrPanel.Find("AttrCritRoot") : null;
        var popups = transform.Find("Popups");
        var bag = popups != null ? popups.Find("BackpackPanel") : null;
        var refBtn = content != null ? content.Find("Stage/LeftButton") : null;
        if (content == null || lastRow == null || popups == null || bag == null)
        {
            Debug.LogError("[CharacterUI] 缺 Content/AttrPanel/AttrCritRoot 或 Popups/BackpackPanel，宠物入口无法定位");
            return;
        }

        var popupsRt = (RectTransform)popups;
        var lastRt = (RectTransform)lastRow;
        var bagRt = (RectTransform)bag;
        var refRt = refBtn != null ? (RectTransform)refBtn : null;
        // 图标放大 30%（2026-11-21）：沿用参考按钮尺寸做基准，乘放大系数；y 定位同步用放大后的高度
        const float ICON_SCALE = 1.3f;
        float btnW = (refRt != null ? refRt.sizeDelta.x : 106f) * ICON_SCALE;
        float btnH = (refRt != null ? refRt.sizeDelta.y : 106f) * ICON_SCALE;

        var go = new GameObject("PetButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(popups, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);

        // x：属性末行左沿，换算到 Popups 局部
        var leftWorld = lastRt.TransformPoint(new Vector3(lastRt.rect.xMin, 0f, 0f));
        var leftLocal = popupsRt.InverseTransformPoint(leftWorld);

        // y：背包顶边（同坐标系纯数值）再往上留一个按钮高 + 12 间隙。
        float bagAnchorY = Mathf.Lerp(popupsRt.rect.yMin, popupsRt.rect.yMax, bagRt.anchorMin.y);
        float bagTopY = bagAnchorY + bagRt.anchoredPosition.y + bagRt.rect.height * (1f - bagRt.pivot.y);
        float entryAnchorY = Mathf.Lerp(popupsRt.rect.yMin, popupsRt.rect.yMax, rt.anchorMin.y);
        float entryAnchorX = Mathf.Lerp(popupsRt.rect.xMin, popupsRt.rect.xMax, rt.anchorMin.x);
        var anchorOffset = new Vector2(leftLocal.x - entryAnchorX,
                                       bagTopY + 12f + btnH * 0.5f - entryAnchorY);
        rt.anchoredPosition = anchorOffset;
        rt.sizeDelta = new Vector2(btnW, btnH);
        Debug.Log("[CharacterUI] 宠物入口已创建 pos=" + anchorOffset + " bagTopY=" + bagTopY);

        var img = go.GetComponent<Image>();
        var sp = Resources.Load<Sprite>("UI/NavCharacter/宠物");
        if (sp != null)
        {
            img.sprite = sp;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
            Debug.LogError("[CharacterUI] 宠物入口图缺失：Resources/UI/NavCharacter/宠物.png（请确认 Texture Type=Sprite）");

        img.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => PetUI.Open());
        _petEntryButton = btn;
    }
}
