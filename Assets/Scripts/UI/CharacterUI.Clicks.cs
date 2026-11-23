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
        EnsureJobSwitchEntry();
        // 宠物入口不在这里建：它按背包顶边定位，必须等 ShowPage 里 EnsureBagAlwaysOpen(Fit) 之后再建
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
    /// 【2026-11-20 主人拍板「角色界面右侧加一个切换职业的按钮」】
    /// 复用现成的 BuildSideBtn 在右列运行时建按钮（不碰 prefab / meta，只传纵向 y，与看板 -270 / 背包 -180 同列 90 间距）。
    /// 点击走 PlayerJobSelectUI 的换职业双模式（钻石二次确认 + TryChangeJob 退 80%）；成功后 RefreshAll 同步装备栏/属性/立绘。
    /// </summary>
    void EnsureJobSwitchEntry()
    {
        if (jobSwitchButton == null && talentButton != null)
        {
            Transform parent = talentButton.transform.parent;   // Content/Stage/RightButtons
            if (parent != null)
            {
                BuildSideBtn(parent, "JobSwitchButton", "切换职业", -360f, new Color(0.55f, 0.42f, 0.2f, 1f));
                jobSwitchButton = parent.Find("JobSwitchButton")?.GetComponent<Button>();
            }
        }
        if (jobSwitchButton != null)
        {
            jobSwitchButton.onClick.RemoveAllListeners();
            jobSwitchButton.onClick.AddListener(OpenJobChange);
            // 【2026-11-20 主人换图】用主人放进 Resources 的「切换职业」图标替掉纯色块；
            // 只换 Sprite / 不碰 prefab / 不写尺寸。等比缩放 preserveAspect，绝不拉伸。
            var art = jobSwitchButton.GetComponent<Image>();
            if (art != null)
            {
                var sp = Resources.Load<Sprite>("UI/NavCharacter/切换职业");
                if (sp != null)
                {
                    art.sprite = sp;
                    art.color = Color.white;   // 有图后去掉原纯色块的染色
                    art.preserveAspect = true;
                    // 图是带边框的完整按钮外观（双人头像+箭头，语义已足），
                    // 清掉运行时建的纯文字占位，避免文字压在图上（铁律：真图进槽先清占位）。
                    var label = jobSwitchButton.transform.Find("Label");
                    if (label != null) label.gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogError("[CharacterUI] 切换职业按钮图标缺失：Resources/UI/NavCharacter/切换职业.png。" +
                                   "请确认该图 Texture Type = Sprite (2D and UI)，否则加载不到。");
                }
            }
        }
    }

    void OpenJobChange()
    {
        // 【2026-11-20 主人拍板】按钮常显；没通第1章第5关时点击只提示，不弹职业界面。
        if (!TalentSystem.CanChangeJobByProgress(out string reason))
        {
            UIManager.Instance?.ShowToast(reason);
            return;
        }
        // 【2026-11-23】换完职业除了属性/立绘，角色页 6 槽装备栏也要切到新职业那套：
        // EnsureEquipBar 只在 ShowPage 跑过一次，不在这里补，装备栏会一直显示旧职业的武器套。
        PlayerJobSelectUI.ShowChangeJob(_ =>
        {
            RefreshAll();
            EnsureEquipBar();
        });
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
        {
            bagPanelRoot.gameObject.SetActive(true);
            // 只在「这次真的打开」时重置：每次进页允许再播一次「格子快满 → 可左右滑」的引导
            ResetBagSwipeHint();
        }
        if (_bagTapLayer != null)
            SetBagTapLayerActive(false);   // 常显口径下点击层永远不亮（残留即关）
        // 互斥：仓库摊开时不留技能面板
        HideSkillSelect();
        RefreshBag();                // 里面会按 used/unlocked 判定是否播「格子快满」引导箭头
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
