using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>佣兵抽奖池 UI · 池格（top/PoolGrid/SlotN）刷新与锁格提示。</summary>
public partial class MercDrawPoolUI : MonoBehaviour
{
    void RefreshPoolSlot(GameObject slot, int index, bool open, string hireId)
    {
        // 预制体格（有卡组模板结构）走填充，不销毁子节点；非预制体格不该出现，直接报错
        if (slot.transform.Find("Iconmask") != null)
        {
            RefreshPrefabPoolSlot(slot, open, hireId);
            return;
        }

        Debug.LogError("[MercDrawPoolUI] 池格不是预制体格（缺 Iconmask），无法填充");
    }

    /// <summary>
    /// 预制体池格填充（2026-10-15 主人重做预制体后）：不销毁子节点，按状态只填
    /// 头像 / 名字 / quxiao / suo（框与尺寸保持预制体原样，2026-10-16 主人拍板）；空格与锁格清空卡面。
    /// </summary>
    void RefreshPrefabPoolSlot(GameObject slot, bool open, string hireId)
    {
        EnsurePoolSlotRole(slot);

        var icon = slot.transform.Find("Iconmask/Icon")?.GetComponent<Image>();
        var nameT = slot.transform.Find("namebg/Name")?.GetComponent<Text>();
        var qxTr = slot.transform.Find("quxiao");
        var suoTr = slot.transform.Find("suo");

        bool filled = open && !string.IsNullOrEmpty(hireId);
        if (!filled)
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
                icon.color = Color.white;
            }
            if (nameT != null) nameT.text = open ? "空格" : "暂未开启";
            HideRoleIcon(slot.transform);
            if (qxTr != null) qxTr.gameObject.SetActive(false);
            if (suoTr != null) suoTr.gameObject.SetActive(!open);
            return;
        }

        MercRosterDefs.TryGetByHireId(hireId, out var def);
        // 【2026-10-16 主人纠正】池格放全身立绘（GetStand），不是头像；取景是主人调好的，只换图
        var sp = MercPortraitSprites.GetStand(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetStand(def.AssetId);
        if (icon != null)
        {
            icon.sprite = sp;
            icon.enabled = sp != null;
            icon.color = Color.white;
        }
        if (nameT != null) nameT.text = def.Name;
        // 【2026-10-16 主人拍板】池格只换头像和名字，框/尺寸/节点一律保持预制体原样：
        // 稀有度框图比例(204x248=0.82)与卡槽(200x300=0.67)不一致，运行时换 Sliced 图会把框拉扁拉大。
        if (qxTr != null) qxTr.gameObject.SetActive(true);
        if (suoTr != null) suoTr.gameObject.SetActive(false);
    }

    /// <summary>池格缺 Role（Slot1-4）时运行时从模板 Slot0 克隆一份，布局参数原样复制。</summary>
    void EnsurePoolSlotRole(GameObject slot)
    {
        if (slot.transform.Find("Role") != null) return;
        var tmpl = CardTemplate();
        if (tmpl == null) return;
        var src = tmpl.transform.Find("Role");
        if (src == null) return;
        var clone = Object.Instantiate(src.gameObject);
        clone.name = "Role";
        clone.transform.SetParent(slot.transform, false);
        var srt = src as RectTransform;
        var crt = clone.GetComponent<RectTransform>();
        if (srt != null && crt != null)
        {
            crt.anchorMin = srt.anchorMin;
            crt.anchorMax = srt.anchorMax;
            crt.pivot = srt.pivot;
            crt.anchoredPosition = srt.anchoredPosition;
            crt.sizeDelta = srt.sizeDelta;
            crt.localScale = srt.localScale;
        }
    }

    /// <summary>锁格提示（第 4 格 = 通关第 2 章、第 5 格 = 通关第 3 章，条件在 SaveData.DrawPoolSlots）。</summary>
    static string LockHintOf(int index)
    {
        return index == 3 ? "通关第 2 章" : index == 4 ? "通关第 3 章" : "";
    }

    // ============================================================
    // 卡片构建（统一头像，主人拍板）
    // ============================================================

    /// <summary>
    /// 头像脸：稀有度头像框（跟头像同区，不铺全卡防拉变形）+ 头像 + 名字 + 职业徽记（左上）。
    /// usedForPool = 池格（更高瘦）。
    /// </summary>
    void BuildMercFace(Transform parent, string hireId, MercRosterDefs.Def def, string nameLabel, bool usedForPool)
    {
        var frame = NewImage(parent, "Frame", Color.white);
        var frt = frame.rectTransform;
        frt.anchorMin = new Vector2(0.5f, 1f);
        frt.anchorMax = new Vector2(0.5f, 1f);
        frt.pivot = new Vector2(0.5f, 1f);
        var frameSp = MercHireSession.LoadPortraitFrame(def.Rarity);
        frame.sprite = frameSp;
        frame.enabled = frameSp != null;
        var head = NewImage(parent, "Head", Color.white);
        var hrt = head.rectTransform;
        hrt.anchorMin = new Vector2(0.5f, 1f);
        hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        if (usedForPool)
        {
            frt.anchoredPosition = new Vector2(0f, -22f);
            frt.sizeDelta = new Vector2(88f, 88f);
            hrt.anchoredPosition = new Vector2(0f, -26f);
            hrt.sizeDelta = new Vector2(72f, 72f);
        }
        else
        {
            frt.anchoredPosition = new Vector2(0f, -6f);
            frt.sizeDelta = new Vector2(136f, 116f);
            hrt.anchoredPosition = new Vector2(0f, -10f);
            hrt.sizeDelta = new Vector2(120f, 100f);
        }
        var sp = MercPortraitSprites.GetHead(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetHead(def.AssetId);
        head.sprite = sp;
        head.preserveAspect = true;
        head.enabled = sp != null;

        var nameT = MakeText(parent, "Name", nameLabel, usedForPool ? 13 : 17);
        var nrt = nameT.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 0f);
        nrt.pivot = new Vector2(0.5f, 0f);
        nrt.offsetMin = new Vector2(2f, 4f);
        nrt.offsetMax = new Vector2(-2f, usedForPool ? 26f : 30f);

        var badge = NewImage(parent, "Badge", Color.white);
        var brt = badge.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f);
        brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(5f, -5f);
        brt.sizeDelta = new Vector2(30f, 30f);
        var badgeSp = MercGrowSprites.LoadJobBadge(def.JobName, def.Rarity);
        badge.sprite = badgeSp;
        badge.enabled = badgeSp != null;
    }
}
