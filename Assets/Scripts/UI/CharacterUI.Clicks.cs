using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-18 由 CharacterUI.cs 纯位移拆出（无逻辑改动）
// 职责：点击入口（WireClicks、天赋/技能打开、背包按钮与面板显隐互斥）
public partial class CharacterUI
{

    void WireClicks()
    {
        if (_wired) return;
        _wired = true;
        if (talentButton != null)
        {
            talentButton.onClick.RemoveAllListeners();
            talentButton.onClick.AddListener(OpenTalent);
            // 2026-09-29：天赋入口红点 —— 右列有新天赋可点时亮，玩家打开一次天赋页后清。
            RedDot.Bind(talentButton.transform, RedDot.Talent);
        }
        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(OpenSkillSelect);
        }
        // 2026-09-25 主人明确：LeftSkillButton 已改名且暂时不要了，原入口废弃。
        // 不再隐藏/停用任何节点（leftSkillButton、carriedSkillIcon 保持 null，不动）。

        EnsureBackpackEntry();
        EnsureBoardEntry();
        EnsureBagPageButtons();
        if (flipPortraitButton != null)
        {
            flipPortraitButton.onClick.RemoveAllListeners();
            flipPortraitButton.onClick.AddListener(FlipPortrait);
        }
        if (bagExpandButton != null)
        {
            bagExpandButton.onClick.RemoveAllListeners();
            bagExpandButton.onClick.AddListener(() =>
                UIManager.Instance?.ShowToast("扩容请在天赋中解锁背包行"));
        }
    }

    public void OpenTalent()
    {
        TalentUI ui = TalentUI.Instance;
        if (ui == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Talent/TalentUI");
            if (prefab == null)
            {
                UIManager.Instance?.ShowToast("天赋界面未就绪");
                return;
            }
            var go = Instantiate(prefab);
            go.name = "TalentUI";
            ui = go.GetComponent<TalentUI>();
        }
        if (ui == null) return;
        if (ui.onClosed == null) ui.onClosed = new UnityEngine.Events.UnityEvent();
        ui.onClosed.RemoveAllListeners();
        ui.onClosed.AddListener(() =>
        {
            if (gameObject.activeInHierarchy)
                RefreshAttrs();
        });
        ui.Show();
    }

    public void OpenSkillSelect()
    {
        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect == null)
        {
            UIManager.Instance?.ShowToast("技能界面未就绪");
            return;
        }
        skillSelect.Show();
    }

    /// <summary>
    /// 「背包」入口：预制体优先。
    /// 2026-09-25 起主人在 Content/Stage/LeftButton/背包 里摆好了按钮（AutoBind 里绑到 backpackEntryButton），
    /// 此时直接复用，不再运行时生成，避免出现第二个背包按钮；
    /// 只有预制体里真的没有时才在天赋按钮同父节点（右侧按钮列）下用 BuildSideBtn 兜底。
    /// 点击统一在这里绑一次（RemoveAllListeners 保证不会重复监听），打开独立背包弹窗。
    /// </summary>
    void EnsureBackpackEntry()
    {
        if (backpackEntryButton == null && talentButton != null)
        {
            Transform parent = talentButton.transform.parent;
            if (parent != null)
            {
                // 技能按钮（SkillButton）在 BuildSideBtn 约定里 y = -90，背包入口排在其下方 y = -180
                BuildSideBtn(parent, "BackpackButton", "背包", -180f, new Color(0.45f, 0.4f, 0.25f, 1f));
                backpackEntryButton = parent.Find("BackpackButton")?.GetComponent<Button>();
            }
        }
        if (backpackEntryButton != null)
        {
            backpackEntryButton.onClick.RemoveAllListeners();
            backpackEntryButton.onClick.AddListener(OpenBackpack);
        }
    }

    /// <summary>
    /// 「背包」按钮：切换界面内的 Content/BackpackPanel（展开/收起）。
    /// 展开时把 SkillSelectUI 收掉，两个面板互斥，避免叠在一起。
    /// 【2026-10-17 主人拍板】独立背包弹窗 BackpackPopupUI 已删，背包只用预制体里的面板。
    /// </summary>
    public void OpenBackpack()
    {
        if (bagPanelRoot == null)
            bagPanelRoot = transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel");

        if (bagPanelRoot == null)
        {
            // 预制体里连面板都没有 = 预制体被改坏了，报错可见（不兜底）
            Debug.LogError("[CharacterUI] 预制体里找不到 Content/BackpackPanel，背包无法打开");
            return;
        }

        {
            bool open = !bagPanelRoot.gameObject.activeSelf;
            if (!open)
            {
                CloseBackpackPanel();
                return;
            }

            bagPanelRoot.gameObject.SetActive(true);
            EnsureBagTapLayer();          // 面板下面垫一层全屏透明点击层
            SetBagTapLayerActive(true);
            // 互斥：展开背包时收起技能面板
            HideSkillSelect();
            RefreshBag();
            ApplyBackpackPanelFit();     // 面板打开后做一次瘦屏防溢出 / 防裁切
        }
    }

    /// <summary>
    /// 收起背包面板的唯一入口：`OpenBackpack()` 的收起分支与「点击面板外」层都调它，
    /// 避免两处各写一份导致逻辑不一致。同时收起 SkillSelectUI（与展开时的互斥逻辑对称）。
    /// </summary>
    void CloseBackpackPanel()
    {
        if (bagPanelRoot != null && bagPanelRoot.gameObject.activeSelf)
            bagPanelRoot.gameObject.SetActive(false);
        SetBagTapLayerActive(false);
        HideSkillSelect();
    }

    /// <summary>收起技能选择面板（与展开背包时的互斥处理完全一致）。</summary>
    void HideSkillSelect()
    {
        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect != null)
            skillSelect.Hide();
        else
        {
            var skillRoot = transform.Find("SkillSelectUI") ?? FindDeep(transform, "SkillSelectUI");
            if (skillRoot != null) skillRoot.gameObject.SetActive(false);
        }
    }
}
