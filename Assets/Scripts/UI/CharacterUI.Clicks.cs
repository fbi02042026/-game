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
    /// 「背包」入口：AutoBind 已按预制体路径绑好（RightButtons/BackpackButton → 左列回退），
    /// 这里只兜一层「AutoBind 没绑到」的再查找。
    /// 【2026-10-26 主人拍板「背包要常显」】按钮定位后整体隐藏（入口退役），不再绑点击、不再运行时生成。
    /// </summary>
    void EnsureBackpackEntry()
    {
        // 【2026-10-26 主人拍板「背包要常显」】入口已退役：预制体里没有就不再运行时生成
        // （生成了也是马上隐藏）；预制体里有的只做定位+隐藏。
        if (backpackEntryButton == null)
        {
            backpackEntryButton = transform.Find("Content/Stage/RightButtons/BackpackButton")?.GetComponent<Button>()
                                  ?? transform.Find("Content/Stage/RightButtons/背包")?.GetComponent<Button>()
                                  ?? (leftButtonRoot != null
                                      ? leftButtonRoot.Find("BackpackButton")?.GetComponent<Button>()
                                        ?? leftButtonRoot.Find("背包")?.GetComponent<Button>()
                                      : null)
                                  ?? transform.Find("Content/Stage/LeftButton/BackpackButton")?.GetComponent<Button>();
        }
        if (backpackEntryButton != null)
        {
            // 随身仓库进页直接摊开，入口按钮不再需要 ——
            // 预制体里主人手摆的按钮整体隐藏不删（同 EnsureBoardEntry 的看板按钮口径）；开关要还原删掉这行即可。
            backpackEntryButton.onClick.RemoveAllListeners();
            backpackEntryButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 【2026-10-26 主人拍板「这个界面的背包要常显」】随身仓库 = 角色页固定面板：
    /// 进页直接摊开（ShowPage 调 EnsureBagAlwaysOpen），不再有「点背包按钮开关」的交互。
    /// 原开关入口 OpenBackpack 保留为「只开不关」（外部若还有调用也安全），收起分支删除。
    /// </summary>
    public void OpenBackpack()
    {
        EnsureBagAlwaysOpen();
    }

    /// <summary>
    /// 随身仓库常显的唯一开口：面板不在就摊开 + 互斥收起技能面板 + 刷新 + 瘦屏防溢出。
    /// 面板常显后「点面板外收起」的点击层不再启用（见 EnsureBagTapLayer）。
    /// </summary>
    void EnsureBagAlwaysOpen()
    {
        if (bagPanelRoot == null)
            bagPanelRoot = transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel");

        if (bagPanelRoot == null)
        {
            // 预制体里连面板都没有 = 预制体被改坏了，报错可见（不兜底）
            Debug.LogError("[CharacterUI] 预制体里找不到 Content/BackpackPanel，随身仓库无法显示");
            return;
        }

        if (!bagPanelRoot.gameObject.activeSelf)
            bagPanelRoot.gameObject.SetActive(true);
        if (_bagTapLayer != null)
            SetBagTapLayerActive(false);   // 常显口径下点击层永远不亮（残留即关）
        // 互斥：仓库摊开时不留技能面板
        HideSkillSelect();
        RefreshBag();
        ApplyBackpackPanelFit();     // 瘦屏防溢出 / 防裁切
    }

    /// <summary>
    /// 收起背包面板（2026-10-26 起仅「点面板外」层这一条路会用；随身仓库常显后该层不再启用，
    /// 本方法保留为对称出口，程序性收包时仍可用）。同时收起 SkillSelectUI（与展开时的互斥逻辑对称）。
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
