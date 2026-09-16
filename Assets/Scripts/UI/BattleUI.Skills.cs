using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 底部 4 个主动技槽与 5 个装备快捷槽的刷新，以及技能/自动战斗相关的点击回调。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    /// <summary>
    /// 刷新底部 4 个技能槽：全部为被动技能，充能满后由 PlayerSkillPassive 自动释放，
    /// 头像/槽位不再接手动释放。
    /// </summary>
    public void UpdateRunSkillSlots()
    {
        if (runSkillSlots == null || runSkillSlots.Count == 0) return;

        var job = PlayerJobDefs.GetSelected();
        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;

        for (int i = 0; i < runSkillSlots.Count; i++)
        {
            var slot = runSkillSlots[i];
            if (slot == null) continue;

            string id = ids != null && i < ids.Count ? ids[i] : null;
            if (string.IsNullOrEmpty(id))
            {
                slot.SetAvatar(null);
                slot.SetSkillName(null, "无");      // 空槽标注「无」
                slot.SetLevelText("");
                slot.SetEnergyFillVisible(GameConfig.PLAYER_SKILL_USE_ENERGY);
                slot.SetEnergyFill(0f);
                if (slot.cooldownText != null) slot.cooldownText.gameObject.SetActive(false);
                continue;
            }

            var active = RunDraftDirector.BuildRunSkill(id, job);
            int star = RunLoadout.StarOf(id);
            slot.SetAvatar(active != null ? active.icon : null);
            // 底字不再写死「被动」：有技能就显示技能名
            slot.SetSkillName(active != null ? active.skillName : id);
            // 右下角等级：本作技能没有独立等级，用星级表示
            slot.SetLevelText($"★{star}");
        }
    }

    /// <summary>刷新底部 5 个装备快捷槽：头 / 胸 / 脚 / 主手 / 副手（暂不做「手」和「披风」）。</summary>
    public void UpdateEquipQuickSlots()
    {
        if (equipQuickSlots == null || equipQuickSlots.Count == 0) return;
        var bag = GridBackpackSystem.Instance;
        for (int i = 0; i < equipQuickSlots.Count; i++)
        {
            var slot = equipQuickSlots[i];
            if (slot == null) continue;
            slot.Bind(bag != null ? bag.GetEquippedInSlot(slot.slotType) : null);
        }
    }

    /// <summary>
    /// 刷新佣兵自带技能槽（MercSlot1/skill、MercSlot2/skill）。
    /// 注意：这是**佣兵自己的技能**，和上面 4 个玩家被动技槽是两套，不要共用。
    /// 佣兵技能同样由 MercSkillCaster 自动释放，这里只表现 图标 / 充能 / 冷却。
    /// 没有对应佣兵或该佣兵没配技能时整槽隐藏，不露空框。
    /// </summary>
    public void UpdateMercSkillSlots()
    {
        if (mercSkillSlots == null || mercSkillSlots.Count == 0) return;
        var mm = MercenaryManager.Instance;
        var mercs = mm != null ? mm.GetActiveMercs() : null;

        for (int i = 0; i < mercSkillSlots.Count; i++)
        {
            var slot = mercSkillSlots[i];
            if (slot == null || slot.root == null) continue;

            var m = (mercs != null && i < mercs.Count) ? mercs[i] : null;
            var caster = m != null ? m.SkillCaster : null;
            bool has = caster != null && caster.HasActiveSkill;
            // 空槽也不隐藏，保持槽位可见
            slot.root.SetActive(true);
            if (!has)
            {
                slot.SetAvatar(null);
                slot.SetSkillName(null, "无");
                slot.SetLevelText("");
                slot.SetEnergyFill(0f);
                if (slot.cooldownText != null) slot.cooldownText.gameObject.SetActive(false);
                continue;
            }

            slot.SetAvatar(MercSkillTable.LoadIcon(caster.ActiveSkillId));
            slot.SetEnergyFill(BattleManager.Instance != null ? BattleManager.Instance.GetMercSkillEnergy(i) : 0f);
        }
    }

    string _lastMercSkillKey = "\u0000";

    /// <summary>逐帧刷佣兵技能充能 / 冷却；佣兵进出队或换人时整槽重建。</summary>
    void TickMercSkillSlots()
    {
        if (mercSkillSlots == null || mercSkillSlots.Count == 0) return;
        var mm = MercenaryManager.Instance;
        var mercs = mm != null ? mm.GetActiveMercs() : null;

        string key = "";
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count && i < mercSkillSlots.Count; i++)
                key += (mercs[i] != null && mercs[i].SkillCaster != null
                           ? mercs[i].SkillCaster.ActiveSkillId : "-") + ",";
        }
        if (!string.Equals(key, _lastMercSkillKey))
        {
            _lastMercSkillKey = key;
            UpdateMercSkillSlots();
        }

        for (int i = 0; i < mercSkillSlots.Count; i++)
        {
            var slot = mercSkillSlots[i];
            if (slot == null || slot.root == null || !slot.root.activeSelf) continue;
            var m = (mercs != null && i < mercs.Count) ? mercs[i] : null;
            var caster = m != null ? m.SkillCaster : null;
            if (caster == null) continue;

            slot.SetEnergyFill(BattleManager.Instance != null ? BattleManager.Instance.GetMercSkillEnergy(i) : 0f);
            if (slot.cooldownText == null) continue;
            float cd = caster.CooldownRemain;
            slot.cooldownText.text = cd > 0.05f ? cd.ToString("0.0") + "s" : "";
            slot.cooldownText.gameObject.SetActive(cd > 0.05f);
        }
    }

    string _lastSkillKey = "\u0000";

    /// <summary>逐帧（0.1s 节流）刷技能槽充能 / 冷却；技能列表变了就整槽重建。</summary>
    void TickRunSkillSlots()
    {
        if (runSkillSlots == null || runSkillSlots.Count == 0) return;

        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        string key = ids == null ? "" : string.Join(",", ids);
        if (!string.Equals(key, _lastSkillKey))
        {
            _lastSkillKey = key;
            UpdateRunSkillSlots();
        }

        var bm = BattleManager.Instance;
        var sys = SkillSystem.Instance;
        for (int i = 0; i < runSkillSlots.Count; i++)
        {
            var slot = runSkillSlots[i];
            if (slot == null) continue;
            bool has = ids != null && i < ids.Count;
            // 纯冷却制：能量条隐藏（PLAYER_SKILL_USE_ENERGY 置 true 可退回）
            slot.SetEnergyFillVisible(GameConfig.PLAYER_SKILL_USE_ENERGY);
            if (GameConfig.PLAYER_SKILL_USE_ENERGY)
                slot.SetEnergyFill(has && bm != null ? bm.GetPlayerSkillEnergy(i) : 0f);
            if (slot.cooldownText == null || !has) continue;
            // 星级已经挪到右下角 level 节点，这里只显示剩余冷却。
            // 注意要取「剩余秒数」而不是 0~1 的比例 —— 以前把比例当秒打出来，显示一直是 0.0s
            float cd = sys != null ? sys.GetPlayerSkillCooldownRemaining(i) : 0f;
            slot.cooldownText.text = cd > 0.05f ? cd.ToString("0.0") + "s" : "";
            slot.cooldownText.gameObject.SetActive(cd > 0.05f);
        }
    }

    /// <summary>自动战斗未接线：隐藏按钮；若仍被点到只 Toast，不改战斗 AI。</summary>
    void BindAutoBattleUnavailable()
    {
        if (autoButton == null) return;
        autoButton.onClick.RemoveListener(OnAutoBattleUnavailableClicked);
        autoButton.onClick.AddListener(OnAutoBattleUnavailableClicked);
        autoButton.gameObject.SetActive(false);
        if (BattleManager.Instance != null)
            BattleManager.Instance.isAutoBattle = false;
    }

    void OnAutoBattleUnavailableClicked()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.isAutoBattle = false;
        UIManager.Instance?.ShowToast("未开放");
    }

    /// <summary>
    /// 暂停已收进设置弹窗（SettingsPopupUI：继续/撤离），这里不再维护独立的暂停面板。
    /// 头像/槽位也不再接手动释放：技能由 PlayerSkillPassive / MercSkillCaster 自动放。
    /// </summary>

    /// <summary>更新技能区圆形头像</summary>
    public void UpdateSkillAvatars()
    {
        var mm = MercenaryManager.Instance;
        if (playerSkillAvatar != null)
            playerSkillAvatar.SetAvatar(mm != null ? mm.GetPlayerIcon() : null);

        bool tutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        if (GameConfig.SOLO_PLAYER_BATTLE && !tutorialMerc) return;

        if (tutorialMerc)
        {
            var mercs = mm != null ? mm.GetActiveMercs() : null;
            Mercenary m0 = (mercs != null && mercs.Count > 0) ? mercs[0] : null;
            // 教程技能圆优先佣兵头像（H011），勿错绑技能图
            string hire = m0 != null && !string.IsNullOrEmpty(m0.hireId)
                ? m0.hireId
                : StoryProgress.TutorialMercHireId;
            Sprite icon = MercPortraitSprites.GetHead(hire)
                ?? (m0 != null && mm != null ? mm.GetIcon(m0.mercId) : null)
                ?? GetMercSkillIcon(m0);
            merc1SkillAvatar?.SetAvatar(icon);
            if (mercSlot1 != null && m0 != null)
                mercSlot1.SetEnergyEnabled(m0.SkillCaster != null && m0.SkillCaster.HasActiveSkill && !MercSkillMigrate.IsMercSkillAutoCast());
            return;
        }

        var mercIds = mm != null ? mm.GetActiveMercIds() : new List<string>();
        var mercsList = mm != null ? mm.GetActiveMercs() : null;
        if (merc1SkillAvatar != null)
            merc1SkillAvatar.SetAvatar(GetMercSkillIconAt(mercsList, 0) ?? GetMercPortraitAt(mm, mercIds, 0));
        if (merc2SkillAvatar != null)
            merc2SkillAvatar.SetAvatar(GetMercSkillIconAt(mercsList, 1) ?? GetMercPortraitAt(mm, mercIds, 1));
    }

    static Sprite GetMercSkillIcon(Mercenary m)
    {
        if (m?.SkillCaster == null || !m.SkillCaster.HasActiveSkill) return null;
        return MercSkillTable.LoadIcon(m.SkillCaster.ActiveSkillId);
    }

    static Sprite GetMercSkillIconAt(List<Mercenary> mercs, int index)
    {
        if (mercs == null || index < 0 || index >= mercs.Count) return null;
        return GetMercSkillIcon(mercs[index]);
    }

    static Sprite GetMercPortraitAt(MercenaryManager mm, List<string> mercIds, int index)
    {
        if (mm == null || mercIds == null || index < 0 || index >= mercIds.Count) return null;
        var mercs = mm.GetActiveMercs();
        if (mercs != null && index < mercs.Count && mercs[index] != null && !string.IsNullOrEmpty(mercs[index].hireId))
            return MercPortraitSprites.GetHead(mercs[index].hireId) ?? mm.GetIcon(mercs[index].mercId);
        var hireIds = mm.GetActiveMercHireIds();
        if (hireIds != null && index < hireIds.Count && !string.IsNullOrEmpty(hireIds[index]))
            return MercPortraitSprites.GetHead(hireIds[index]) ?? mm.GetIcon(mercIds[index]);
        return mm.GetIcon(mercIds[index]);
    }

    /// <summary>
    /// 更新技能头像能量（玩家=0, 佣兵1=1, 佣兵2=2）
    /// </summary>
    public void UpdateSkillEnergy(int skillIndex, float energyRatio)
    {
        // skillIndex 0 = 玩家。**玩家头像下第二条已改为「雷击奥义充能」**（见 SetUltCharge），
        // 这里不能再写 skill 能量，否则两边抢同一个 lanBarFill 会互相覆盖。
        // 玩家 4 个被动技各自有独立能量条（RunSkillBarUI），第二条不必再重复显示技能能量峰值。
        if (skillIndex == 0) return;

        if (skillIndex == 1 && mercSlot1 != null)
        {
            if (!mercSlot1.EnergyEnabled) energyRatio = 0f;
            mercSlot1.SetEnergy(energyRatio);
        }
        else if (skillIndex == 2 && mercSlot2 != null)
        {
            if (!mercSlot2.EnergyEnabled) energyRatio = 0f;
            mercSlot2.SetEnergy(energyRatio);
        }
    }

    /// <summary>
    /// 技槽拖拽结束 → 写回 RunLoadout 并重建技能。
    /// 规则：技能数 &lt; 2 不排（1 个技能默认就是最优先，没什么可排）；
    /// 空槽不参与——有几个技能就只有前几个槽能拖（2 个技能 = 只有前两个能调）。
    /// </summary>
    void OnSkillSlotReordered(int from, int to)
    {
        var ids = RunLoadout.SkillIds();
        int owned = ids != null ? ids.Count : 0;
        if (owned < 2) return;
        if (from < 0 || to < 0 || from >= owned || to >= owned) return;
        if (!RunLoadout.MoveSkill(from, to)) return;

        RunLoadout.Save();
        var dir = RunDraftDirector.Instance;
        if (dir == null && BattleManager.Instance != null)
            dir = RunDraftDirector.Ensure(BattleManager.Instance);
        if (dir != null) dir.RebuildPlayerSkills();
        RunSkillBarUI.Refresh();
        RefreshBattleHud();
    }

    /// <summary>
    /// 按「整理阶段」+「已拥有技能数」开/关技槽拖拽。
    /// 只有 1 个技能时不可拖；2 个技能时只有前 2 个槽可拖（第 3、4 是空槽）。
    /// </summary>
    public void RefreshSkillSlotDragState()
    {
        if (runSkillSlots == null) return;
        var ids = RunLoadout.SkillIds();
        int owned = ids != null ? ids.Count : 0;
        bool allow = BattleLootMode.Active && owned >= 2;
        for (int i = 0; i < runSkillSlots.Count; i++)
        {
            var av = runSkillSlots[i];
            if (av == null || av.root == null) continue;
            var chip = av.root.GetComponent<SkillOrderChip>();
            if (chip == null) continue;
            chip.DragEnabled = allow && i < owned;
        }
    }
}
