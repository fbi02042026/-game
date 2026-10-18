using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-18 由 CharacterUI.cs 纯位移拆出（无逻辑改动）
// 职责：体力药水条目（背包底部懒建条目、使用确认与消耗）
public partial class CharacterUI
{

    // ── 体力药水条目（背包底部空档：图标 + 数量 + 使用；有药水才显示）──

    /// <summary>懒建 + 刷新体力药水条目。挂在 BackpackPanel 内（翻页按钮同一行空档），随面板一起显隐。</summary>
    void RefreshStaminaPotionRow()
    {
        if (bagPanelRoot == null) return;

        // 防重影：域重载后字段归零但旧节点可能残留
        if (_potionRow == null)
        {
            var existing = bagPanelRoot.Find("StaminaPotionRow");
            if (existing != null) Destroy(existing.gameObject);
        }

        if (_potionRow == null)
        {
            var row = new GameObject("StaminaPotionRow", typeof(RectTransform), typeof(CanvasRenderer));
            row.transform.SetParent(bagPanelRoot, false);
            var rowRt = (RectTransform)row.transform;
            Set(rowRt, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, BagPageButtonY, 340f, 44f);

            // 药水图标（与邮件/每日登录同图：icon_stamina_potion）
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            var iconRt = (RectTransform)iconGo.transform;
            Set(iconRt, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, -128f, 0f, 40f, 40f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;
            var sp = Resources.Load<Sprite>("Art/UI/Icons/Common/icon_stamina_potion");
            if (sp != null) iconImg.sprite = sp;
            else Debug.LogError("[CharacterUI] 缺少体力药水图标: Art/UI/Icons/Common/icon_stamina_potion");

            var label = CreateTxt(row.transform, "CountLabel", string.Empty, 22, new Color(1f, 0.95f, 0.8f, 1f));
            Set(label.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, -22f, 0f, 180f, 40f);
            _potionCountLabel = label;

            var useImg = CreateImg(row.transform, "UseButton", new Color(0.55f, 0.35f, 0.2f, 1f));
            Set(useImg.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 112f, 0f, 96f, 44f);
            var useBtn = useImg.gameObject.AddComponent<Button>();
            useBtn.targetGraphic = useImg;
            useBtn.onClick.AddListener(OnUseStaminaPotion);
            var useTxt = CreateTxt(useImg.transform, "Label", "使用", 24, Color.white);
            Stretch(useTxt.rectTransform);

            GameFonts.ApplyToHierarchy(row.transform);
            _potionRow = row;
        }

        int count = StaminaSystem.PotionCount;
        _potionRow.SetActive(count > 0);
        if (_potionCountLabel != null)
            _potionCountLabel.text = "体力药水 " + count;
    }

    void OnUseStaminaPotion()
    {
        if (StaminaSystem.PotionCount <= 0) return;
        // 【2026-10-18 主人拍板】使用药水会超出上限：先确认「多余的会浪费」，确认后才使用
        int waste = StaminaSystem.GetPotionWaste();
        if (waste > 0)
        {
            StaminaConfirmPopup.Show(
                $"使用体力药水后将超出上限，多余的 {waste} 点会浪费。\n是否仍要使用？",
                "仍要使用",
                ApplyStaminaPotion);
            return;
        }
        ApplyStaminaPotion();
    }

    void ApplyStaminaPotion()
    {
        if (!StaminaSystem.UsePotion(out string msg)) return;
        UIManager.Instance?.ShowToast(msg, true);
        RefreshStaminaPotionRow();
    }
}
