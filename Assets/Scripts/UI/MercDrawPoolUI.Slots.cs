using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>佣兵抽奖池 UI · 池格（top/PoolGrid/SlotN）刷新与锁格提示。</summary>
public partial class MercDrawPoolUI : MonoBehaviour
{
    // 【2026-10-16 主人拍板】池格框/名字底牌也按稀有度走（与列表卡同口径）：
    // 三档框图 + 三档名字框图都是现成美术（酒馆头像/ 目录），只换图、不代码改色。
    // 预制体原图首次刷新留档，空格/锁格还原，不污染预制体序列化值。
    readonly Dictionary<GameObject, Sprite> _slotDefaultFrames = new Dictionary<GameObject, Sprite>();
    readonly Dictionary<GameObject, Sprite> _slotDefaultNamePlates = new Dictionary<GameObject, Sprite>();

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
    /// 头像 / 名字 / quxiao / suo（尺寸保持预制体原样）；空格与锁格清空卡面。
    /// 【2026-10-16 主人拍板】池格卡框 + 名字底牌也按稀有度换图（传奇金/稀有蓝/普通银三档现成美术，
    /// 卡框 <see cref="MercHireSession.LoadTavernCardFrame"/> / 名字底牌 <see cref="MercHireSession.LoadTavernNamePlate"/>，
    /// 只换图不代码改色）。
    /// </summary>
    void RefreshPrefabPoolSlot(GameObject slot, bool open, string hireId)
    {
        // 替换点选标记（SwapSel）不残留：每次重刷先清，点选态由 Refresh 末尾统一重打
        var staleMark = slot.transform.Find("SwapSel");
        if (staleMark != null) Destroy(staleMark.gameObject);

        EnsurePoolSlotRole(slot);

        var icon = slot.transform.Find("Iconmask/Icon")?.GetComponent<Image>();
        var nameT = slot.transform.Find("namebg/Name")?.GetComponent<Text>();
        var nameBgImg = slot.transform.Find("namebg")?.GetComponent<Image>();
        var qxTr = slot.transform.Find("quxiao");
        var suoTr = slot.transform.Find("suo");
        var slotImg = slot.GetComponent<Image>();

        // 预制体原图首次刷新留档（out var 作用域问题：变量在方法级声明，后面还原才能用）
        Sprite defaultFrame = null;
        if (slotImg != null)
        {
            if (!_slotDefaultFrames.TryGetValue(slot, out defaultFrame))
            {
                defaultFrame = slotImg.sprite;
                _slotDefaultFrames[slot] = defaultFrame;
            }
        }

        Sprite defaultNamePlate = null;
        if (nameBgImg != null)
        {
            if (!_slotDefaultNamePlates.TryGetValue(slot, out defaultNamePlate))
            {
                defaultNamePlate = nameBgImg.sprite;
                _slotDefaultNamePlates[slot] = defaultNamePlate;
            }
        }

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
            // 空格/锁格还原预制体原框/原名字底牌
            if (slotImg != null) slotImg.sprite = defaultFrame;
            if (nameBgImg != null) nameBgImg.sprite = defaultNamePlate;
            HideRoleIcon(slot.transform);
            if (qxTr != null) qxTr.gameObject.SetActive(false);
            if (suoTr != null) suoTr.gameObject.SetActive(!open);
            return;
        }

        bool hasDef = MercRosterDefs.TryGetByHireId(hireId, out var def);
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
        // 卡框 + 名字底牌按稀有度换图（传奇/稀有/普通三档都是现成美术，主人拍板不代码改色）；取不到报错不静默
        var cardFrame = hasDef ? MercHireSession.LoadTavernCardFrame(def.Rarity) : null;
        var namePlate = hasDef ? MercHireSession.LoadTavernNamePlate(def.Rarity) : null;
        if (hasDef && (cardFrame == null || namePlate == null))
            Debug.LogError("[MercDrawPoolUI] 池格换稀有度框/名字底牌失败：hireId=" + hireId
                + "（cardFrame=" + (cardFrame != null) + " namePlate=" + (namePlate != null) + "）");
        if (slotImg != null && cardFrame != null)
        {
            slotImg.sprite = cardFrame;
            slotImg.type = Image.Type.Sliced;
            slotImg.color = Color.white;
        }
        if (nameBgImg != null && namePlate != null)
        {
            nameBgImg.sprite = namePlate;
            nameBgImg.type = Image.Type.Sliced;
            nameBgImg.color = Color.white;
        }
        // 【2026-10-16 主人拍板】尺寸/节点一律保持预制体原样，只换框图/名字底牌/立绘。
        if (qxTr != null) qxTr.gameObject.SetActive(true);
        if (suoTr != null) suoTr.gameObject.SetActive(false);
    }

    /// <summary>池格缺 Role（Slot1-4）时运行时从 top 池模板 Slot0 克隆一份，布局参数原样复制。
    /// （2026-10-17 注：Role 来源必须仍是池 Slot0——列表模板 Panel/Slot 的 Role 被主人停在场外，不能当源）</summary>
    void EnsurePoolSlotRole(GameObject slot)
    {
        if (slot.transform.Find("Role") != null) return;
        GameObject tmpl = null;
        for (int i = 0; i < _poolSlots.Count; i++)
            if (_poolSlots[i] != null && _poolSlots[i].name == "Slot0") { tmpl = _poolSlots[i]; break; }
        if (tmpl == null && _poolSlots.Count > 0) tmpl = _poolSlots[0];
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
