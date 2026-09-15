using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 节点绑定：Awake 里按名字自动补引用、绑定自检、系统就绪后的重绑。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    /// <summary>按节点名自动补全未拖拽的引用</summary>
    void AutoBindMissingRefs()
    {
        if (stageLabel == null) stageLabel = FindUIText("StageLabel");
        if (difficultyLabel == null) difficultyLabel = FindUIText("DifficultyLabel");
        if (goldText == null) goldText = FindUIText("GoldText");
        if (talentStoneText == null)
            talentStoneText = FindUIText("TalentText") ?? FindUIText("TalentStoneText") ?? FindUIText("DiamondText");
        if (enchantStoneText == null) enchantStoneText = FindUIText("EnchantText");
        if (decomposeMatText == null) decomposeMatText = FindUIText("DecomposeText");

        // 若金币误绑到分解文字，纠正
        if (goldText != null && goldText.gameObject.name.IndexOf("Decompose", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var realGold = FindUIText("GoldText");
            if (realGold != null) goldText = realGold;
        }

        if (settingsButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "SettingsButton");
            if (t != null) settingsButton = t.GetComponent<Button>();
        }
        if (characterPanel == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "CharacterPanel");
            if (t != null) characterPanel = t.gameObject;
        }
        if (characterButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "CharacterButton");
            if (t != null) characterButton = t.GetComponent<Button>();
        }
        if (autoButton == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, "AutoButton");
            if (t != null) autoButton = t.GetComponent<Button>();
        }
        if (lootConfirmButton == null)
        {
            // 拾取模式的确认按钮：旧预制体叫「整理」，新美术还没出，两种名字都认
            Transform backpack = FindDeepChildIgnoreCase(transform, "BackpackPanel")
                ?? FindDeepChildIgnoreCase(transform, "Backpack");
            Transform t = null;
            if (backpack != null)
                t = FindDeepChildIgnoreCase(backpack, "确定")
                    ?? FindDeepChildIgnoreCase(backpack, "Confirm")
                    ?? FindDeepChildIgnoreCase(backpack, "ConfirmButton")
                    ?? FindDeepChildIgnoreCase(backpack, "整理")
                    ?? FindDeepChildIgnoreCase(backpack, "OrganizeButton");
            if (t == null)
                t = FindDeepChildIgnoreCase(transform, "确定")
                    ?? FindDeepChildIgnoreCase(transform, "ConfirmButton")
                    ?? FindDeepChildIgnoreCase(transform, "整理")
                    ?? FindDeepChildIgnoreCase(transform, "OrganizeButton");
            if (t != null) lootConfirmButton = t.GetComponent<Button>();
        }

        if (questDesc == null) questDesc = FindUIText("QuestDesc");
        if (questProgress == null) questProgress = FindUIText("QuestProgress");
        if (questTitle == null) questTitle = FindUIText("QuestTitle");
        if (questRewardIcon == null)
        {
            Transform rt = FindDeepChildIgnoreCase(transform, "jianglitubiao");
            if (rt != null) questRewardIcon = rt.GetComponent<Image>();
        }
        if (questRewardAmount == null)
        {
            Transform rt = FindDeepChildIgnoreCase(transform, "jianglishuzi");
            if (rt != null) questRewardAmount = rt.GetComponent<Text>();
        }
        if (questPanel == null)
        {
            Transform qt = FindDeepChildIgnoreCase(transform, "QuestPanel")
                ?? FindDeepChildIgnoreCase(transform, "QuestText")
                ?? FindDeepChildIgnoreCase(transform, "TaskPanel")
                ?? FindDeepChildIgnoreCase(transform, "Task");
            if (qt != null) questPanel = qt.gameObject;
            else if (questDesc != null) questPanel = questDesc.transform.parent?.gameObject;
        }

        BindProgressBar();
        // 引用一律按名字绑齐；未解锁槽只是不改 Fill / 不加 Button
        BindCharacterSlot(ref playerSlot, "PlayerSlot", true);
        BindCharacterSlot(ref mercSlot1, "MercSlot1", false);
        BindCharacterSlot(ref mercSlot2, "MercSlot2", false);

        BindSkillAvatar(ref playerSkillAvatar, "SkillBtn1", "PlayerSkill");
        BindSkillAvatar(ref merc1SkillAvatar, "SkillBtn2", "MercSkill1");
        BindSkillAvatar(ref merc2SkillAvatar, "SkillBtn3", "MercSkill2");

        // 新底部布局：skill 4 槽 / zhuangbei 5 槽（头/胸/脚/主手/副手）
        BindBottomQuickSlots();

        EnsureGridCellsBound();
        FixCharacterBarLayout();
        // MercenaryManager 可能尚未创建，系统就绪后再 Wire / Configure
        WireSlotSkillClicks();
        ApplySoloBattleHud();
        ReportBindingStatus();
    }

    /// <summary>
    /// 绑定自检：把没绑上的引用一次性打到 Console（前缀 [BattleUI-绑定]），
    /// 美术/策划对照补节点后重跑即可看到清单变短。
    /// </summary>
    void ReportBindingStatus()
    {
        var miss = new List<string>();
        void Add(string field, bool ok) { if (!ok) miss.Add(field); }

        Add("StageLabel", stageLabel != null);
        Add("DifficultyLabel", difficultyLabel != null);
        Add("GoldText", goldText != null);
        Add("天赋石文本", talentStoneText != null || enchantStoneText != null);
        Add("DecomposeText", decomposeMatText != null);
        Add("SettingsButton(右上角)", settingsButton != null);

        Add("ProgressBar", progressContainer != null);
        Add("PlayerMarker", playerMarker != null);
        Add("EndFlag", endFlag != null);
        if (progressNodes == null || progressNodes.Count == 0) miss.Add("ProgressNodes(Node_0..9)");

        Add("QuestPanel", questPanel != null);
        Add("QuestTitle", questTitle != null);
        Add("QuestDesc", questDesc != null);
        Add("QuestProgress", questProgress != null);
        Add("jianglitubiao", questRewardIcon != null);
        Add("jianglishuzi", questRewardAmount != null);

        void AddSlot(string tag, CharacterSlotUI s)
        {
            if (s == null || s.root == null) { miss.Add(tag + ".root"); return; }
            Add(tag + ".Portrait", s.portrait != null);
            Add(tag + ".HPBarFill", s.hpBarFill != null);
            Add(tag + ".HPText", s.hpText != null);
            Add(tag + ".lanBarFill", s.lanBarFill != null);
            Add(tag + ".lanText", s.lanText != null);
            Add(tag + ".LockedOverlay", s.lockedOverlay != null);
            Add(tag + ".Glow(光边)", s.glowBorder != null);
            Add(tag + ".LevelLabel", s.levelLabel != null);
            Add(tag + ".NameText", s.nameText != null);
            Add(tag + ".PortraitPlaceholder", s.portraitPlaceholder != null);
        }
        AddSlot("PlayerSlot", playerSlot);
        AddSlot("MercSlot1", mercSlot1);
        AddSlot("MercSlot2", mercSlot2);

        Add("SkillBar(主动技槽容器)", skillSlotRoot != null);
        Add("zhuangbei(装备槽容器)", equipSlotRoot != null);
        if (runSkillSlots == null || runSkillSlots.Count == 0) miss.Add("主动技槽(0 个)");
        if (equipQuickSlots == null || equipQuickSlots.Count == 0) miss.Add("装备快捷槽(0 个)");
        if (gridCells == null || gridCells.Count == 0) miss.Add("GridContainer 格子(0 个)");

        Add("拾取确定按钮", lootConfirmButton != null);
        Add("CharacterButton", characterButton != null);
        Add("CharacterPanel", characterPanel != null);
        Add("AutoButton", autoButton != null);

        string quick = equipQuickSlots != null && equipQuickSlots.Count > 0
            ? string.Join("/", equipQuickSlots.ConvertAll(s => s.slotType.ToString()))
            : "-";
        Debug.Log($"[BattleUI-绑定] 技能槽={runSkillSlots?.Count ?? 0} 装备槽={(equipQuickSlots?.Count ?? 0)}({quick}) " +
                  $"格子={gridCells?.Count ?? 0} 进度点={progressNodes?.Count ?? 0}");
        Debug.Log(miss.Count == 0
            ? "[BattleUI-绑定] 全部就绪 ✅"
            : "[BattleUI-绑定] 缺失 " + miss.Count + " 项 -> " + string.Join(" | ", miss));
    }

    /// <summary>GameRoot/佣兵系统就绪后重绑：Fill、点击、进度条、槽位刷新</summary>
    public void RebindAfterSystemsReady()
    {
        BattleSideHud.EnsureOn(transform);
        BindProgressBar();
        ApplySoloBattleHud();
        int maxSlots = GameConfig.SOLO_PLAYER_BATTLE ? 0
            : MercenaryManager.Instance != null ? MercenaryManager.Instance.GetMaxMercSlots() : 0;
        if (maxSlots > 0) ApplyFillBars(mercSlot1);
        if (maxSlots > 1) ApplyFillBars(mercSlot2);
        WireSlotSkillClicks();
        BindAutoBattleUnavailable();
        UpdateCharacterSlots();
        UpdateSkillAvatars();
        UpdateRunSkillSlots();
        UpdateEquipQuickSlots();
        int stageIdx = BattleManager.Instance != null && BattleManager.Instance.currentStage != null
            ? BattleManager.Instance.currentStage.stageIndex : 0;
        UpdateStageProgress(stageIdx);
    }

    void BindProgressBar()
    {
        Transform t = FindDeepChildIgnoreCase(transform, "ProgressBar")
            ?? FindDeepChildIgnoreCase(transform, "Progress")
            ?? FindDeepChildIgnoreCase(transform, "StageProgress");
        if (t != null) progressContainer = t;
        if (progressContainer == null) return;

        Transform markerT = FindDeepChildIgnoreCase(progressContainer, "PlayerMarker");
        if (markerT != null)
        {
            playerMarker = markerT.GetComponent<Image>();
            if (playerMarker == null) playerMarker = markerT.GetComponentInChildren<Image>(true);
        }

        Transform flagT = FindDeepChildIgnoreCase(progressContainer, "EndFlag");
        if (flagT != null)
        {
            endFlag = flagT.GetComponent<Image>();
            if (endFlag == null) endFlag = flagT.GetComponentInChildren<Image>(true);
        }

        // 每次按 Node_0..Node_9 重建，丢掉预制体里多余的空 fileID
        progressNodes = new List<Image>(GameConfig.STAGES_PER_CHAPTER);
        for (int i = 0; i < GameConfig.STAGES_PER_CHAPTER; i++)
        {
            Transform n = FindDeepChildIgnoreCase(progressContainer, $"Node_{i}");
            if (n == null) continue;
            var img = n.GetComponent<Image>();
            if (img == null)
            {
                img = n.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = false;
            }
            progressNodes.Add(img);
        }
    }

    static void ApplyFillBars(CharacterSlotUI slot)
    {
        if (slot == null) return;
        ConfigureFillBar(slot.hpBarFill);
        ConfigureFillBar(slot.lanBarFill);
        ConfigureFillBar(slot.energyRing);
    }

    void BindCharacterSlot(ref CharacterSlotUI slot, string rootName, bool configureFills = true)
    {
        if (slot == null) slot = new CharacterSlotUI();
        if (slot.root == null)
        {
            Transform t = FindDeepChildIgnoreCase(transform, rootName);
            if (t != null) slot.root = t.gameObject;
        }
        if (slot.root == null) return;
        Transform root = slot.root.transform;

        if (slot.portrait == null)
        {
            // 用户约定：Portrait 是头像位；PlayerSlot 是头像框背景，不能被当成头像图层
            Transform portraitRoot = FindDeepChildIgnoreCase(root, "Portrait");
            if (portraitRoot != null)
                slot.portrait = portraitRoot.GetComponent<Image>();
            if (slot.portrait == null)
                slot.portrait = FindImageNamedNoFallback(root, "Portrait");
        }
        // 占位图：有真实头像时隐藏
        if (slot.portraitPlaceholder == null)
        {
            Transform ph = FindDeepChildIgnoreCase(root, "PortraitPlaceholder");
            if (ph != null) slot.portraitPlaceholder = ph.gameObject;
        }

        if (slot.levelLabel == null)
            slot.levelLabel = FindTextNamed(root, "LevelLabel", "Level", "Lv");
        if (slot.nameText == null)
            slot.nameText = FindTextNamed(root, "Name", "NameText", "PlayerName");

        // HP：优先 HPBarBg/HPBarFill，避免误绑到底板
        if (slot.hpBarFill == null)
        {
            Transform hpBg = FindDeepChildIgnoreCase(root, "HPBarBg")
                ?? FindDeepChildIgnoreCase(root, "HPBarBG");
            if (hpBg != null)
                slot.hpBarFill = FindImageNamedNoFallback(hpBg, "HPBarFill", "Fill");
            if (slot.hpBarFill == null)
                slot.hpBarFill = FindImageNamedNoFallback(root, "HPBarFill", "HPFill", "HpFill");
        }
        if (slot.hpText == null)
            slot.hpText = FindTextNamed(root, "HPText", "HpText");

        // 蓝条/技能能量：lanBarBg/lanBarFill
        if (slot.lanBarFill == null)
        {
            Transform lanBg = FindDeepChildIgnoreCase(root, "lanBarBg")
                ?? FindDeepChildIgnoreCase(root, "LanBarBg");
            if (lanBg != null)
                slot.lanBarFill = FindImageNamedNoFallback(lanBg, "lanBarFill", "LanBarFill", "Fill");
            if (slot.lanBarFill == null)
                slot.lanBarFill = FindImageNamedNoFallback(root, "lanBarFill", "LanBarFill");
        }
        if (slot.lanText == null)
            slot.lanText = FindTextNamed(root, "lanText", "LanText");

        // 兼容旧能量环命名
        if (slot.energyRing == null)
            slot.energyRing = FindImageNamedNoFallback(root, "Energy", "EnergyRing", "Ring");

        if (slot.glowBorder == null)
        {
            Transform g = FindDeepChildIgnoreCase(root, "Glow")
                ?? FindDeepChildIgnoreCase(root, "GlowBorder")
                ?? FindDeepChildIgnoreCase(root, "SkillGlow");
            if (g != null) slot.glowBorder = g.GetComponent<Image>();
        }
        // 按用户要求：不对头像框做任何运行时改造，不再改尺寸/新增节点

        if (slot.lockedOverlay == null)
        {
            Transform l = FindDeepChildIgnoreCase(root, "LockedOverlay")
                ?? FindDeepChildIgnoreCase(root, "Locked")
                ?? FindDeepChildIgnoreCase(root, "Lock");
            if (l != null) slot.lockedOverlay = l.gameObject;
        }
        // 新预制体没有锁遮罩节点：运行时补一个（默认隐藏），这样「未解锁」才有可见效果
        slot.EnsureLockedOverlay();
        // 右上角技能角标容器先备好（有技能图标时才显示）
        slot.EnsureSkillBadge();

        // 未解锁槽：不改 Image.Filled，完全保留美术默认
        if (configureFills) ApplyFillBars(slot);
    }

    static void ConfigureFillBar(Image img)
    {
        if (img == null) return;
        if (img.type != Image.Type.Filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    void ApplySoloBattleHud()
    {
        // 单人模式也保留三个头像位：未解锁显示「锁定」，不要藏掉第 3 个
        bool showTutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        SetSlotRootActive(playerSlot, true);
        SetSlotRootActive(mercSlot1, true);
        SetSlotRootActive(mercSlot2, true);

        // 单人/引导：两格伙伴都按未解锁处理，清掉占位血量数字
        bool lockExtraSlots = (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle) && !showTutorialMerc;
        if (lockExtraSlots)
            mercSlot1?.ShowUnavailable(MercLockedHint);
        else
            mercSlot1?.SetLocked(false);

        if (GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle)
            mercSlot2?.ShowUnavailable(MercLockedHint);
        else
            mercSlot2?.SetLocked(false);

        SetAvatarRootActive(merc1SkillAvatar, showTutorialMerc || !(GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle));
        SetAvatarRootActive(merc2SkillAvatar, !(GameConfig.SOLO_PLAYER_BATTLE || TutorialDirector.IsTutorialBattle));
    }

    public void ApplySoloBattleHudPublic() => ApplySoloBattleHud();

    static void SetSlotRootActive(CharacterSlotUI slot, bool active)
    {
        if (slot?.root != null) slot.root.SetActive(active);
    }

    static void SetAvatarRootActive(SkillAvatarUI avatar, bool active)
    {
        if (avatar?.root != null) avatar.root.SetActive(active);
    }

    /// <summary>
    /// 技能全部改为被动、充能满自动释放（PlayerSkillPassive / MercSkillCaster），
    /// 头像与槽位不再挂 Button 做手动释放，这里保留空实现供既有调用点继续呼叫。
    /// </summary>
    void WireSlotSkillClicks() { }

    void BindSkillAvatar(ref SkillAvatarUI avatar, params string[] names)
    {
        if (avatar == null) avatar = new SkillAvatarUI();
        if (avatar.root != null) return;
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(transform, names[i]);
            if (t == null) continue;
            avatar.root = t.gameObject;
            if (avatar.avatarImage == null)
                avatar.avatarImage = FindImageNamed(t, "Avatar", "Icon", "Portrait", "Mask");
            if (avatar.energyRing == null)
                avatar.energyRing = FindImageNamed(t, "Energy", "EnergyRing", "Ring");
            if (avatar.glowBorder == null)
            {
                Transform g = FindDeepChildIgnoreCase(t, "Glow");
                if (g != null) avatar.glowBorder = g.GetComponent<Image>();
            }
            break;
        }
    }
}
