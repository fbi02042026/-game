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
    /// <summary>【2026-10-20 主人拍板】名字字色按稀有度走；空格/锁格还原预制体原色（同框图留档模式）。</summary>
    readonly Dictionary<GameObject, Color> _slotDefaultNameColors = new Dictionary<GameObject, Color>();

    void RefreshPoolSlot(GameObject slot, int index, bool open, string hireId)
    {
        // 预制体格（有卡组模板结构）走填充，不销毁子节点；非预制体格不该出现，直接报错
        if (slot.transform.Find("Iconmask") != null)
        {
            RefreshPrefabPoolSlot(slot, index, open, hireId);
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
    void RefreshPrefabPoolSlot(GameObject slot, int index, bool open, string hireId)
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

        // 名字字色留档（同框图模式）：格子复用，空格还原原色，防残留上一任佣兵的稀有度色
        if (nameT != null && !_slotDefaultNameColors.ContainsKey(slot))
            _slotDefaultNameColors[slot] = nameT.color;

        bool filled = open && !string.IsNullOrEmpty(hireId);
        if (!filled)
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
                icon.color = Color.white;
            }
            if (nameT != null)
            {
                // 锁格名字直接显示解锁条件（2026-10-23：第 4 格通关第 2 章、第 5 格 100 钻）
                nameT.text = open ? "空格" : LockHintOf(index);
                if (_slotDefaultNameColors.TryGetValue(slot, out var defColor))
                    nameT.color = defColor;
            }
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
        // 【2026-10-20 主人拍板】名字颜色与稀有度对齐（RarityPalette），与下方已拥有/未拥有列表卡同口径
        if (nameT != null)
        {
            nameT.text = def.Name;
            if (hasDef) nameT.color = RarityPalette.Get(def.Rarity);
        }
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
        // 【2026-10-23 主人拍板】quxiao 红「−」下架钮作废：队列中的佣兵不许取消（防抽奖池变小），
        // 换人走已拥有列表卡详情弹窗的「替换」；节点在预制体保留，代码只负责压掉不点亮。
        if (qxTr != null) qxTr.gameObject.SetActive(false);
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

    /// <summary>
    /// 锁格提示（第 4 格 = 通关第 2 章；第 5 格 = 2026-10-23 主人拍板改 100 钻解锁，条件在 SaveData.DrawPoolSlots）。
    /// </summary>
    static string LockHintOf(int index)
    {
        return index == 3 ? "通关第 2 章" : index == 4 ? $"花费{SaveData.DrawPoolPaidUnlockCost}钻石解锁" : "";
    }

    // ============================================================
    // 卡片构建（统一头像，主人拍板）
    // ============================================================

    // 【2026-10-23 主人拍板】BuildMercFace 已删除：唯一调用点在被废弃的 RefreshReplacePanel 里。
}
