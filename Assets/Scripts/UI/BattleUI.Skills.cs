using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 底部 4 个主动技槽与 6 个装备快捷槽的刷新，以及技能/自动战斗相关的点击回调。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    /// <summary>刷新底部 4 个主动技槽：本局构筑技能，充能满自动释放。</summary>
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
                slot.SetLabelVisible(true);
                slot.SetEnergyFill(0f);
                if (slot.cooldownText != null) slot.cooldownText.gameObject.SetActive(false);
                continue;
            }

            var active = RunDraftDirector.BuildRunSkill(id, job);
            int star = RunLoadout.StarOf(id);
            slot.SetAvatar(active != null ? active.icon : null);
            slot.SetLabelVisible(false);
            if (slot.cooldownText != null)
            {
                slot.cooldownText.gameObject.SetActive(true);
                slot.cooldownText.text = $"★{star}";
            }
        }
    }

    /// <summary>刷新底部 6 个装备快捷槽：头/胸甲/手/脚/左手/右手。</summary>
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
            slot.SetEnergyFill(has && bm != null ? bm.GetPlayerSkillEnergy(i) : 0f);
            if (slot.cooldownText == null || !has) continue;
            float cd = sys != null ? sys.GetPlayerSkillCooldownRatio(i) : 0f;
            slot.cooldownText.text = cd > 0.01f
                ? cd.ToString("0.0") + "s"
                : "★" + RunLoadout.StarOf(ids[i]);
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
    /// 暂停：打开暂停面板
    /// </summary>
    void OnPause()
    {
        if (pausePanel != null)
        {
            var panel = pausePanel.GetComponent<PausePanel>();
            if (panel != null)
            {
                panel.Show();
            }
            else
            {
                // 兜底：如果没有PausePanel组件，简单切换timeScale
                pausePanel.SetActive(!pausePanel.activeSelf);
                Time.timeScale = pausePanel.activeSelf ? 0f : 1f;
            }
        }
    }

    /// <summary>
    /// 玩家技能释放（点击头像）— 已改为被动，默认不可手动。
    /// </summary>
    void OnPlayerSkillClick()
    {
        if (!PlayerSkillPassive.AllowManualCast)
            return;

        if (TutorialDirector.IsTutorialBattle
            && TutorialDirector.Instance != null
            && !TutorialDirector.Instance.AllowBattleSkillClick)
            return;

        if (BattleManager.Instance != null)
        {
            bool success = BattleManager.Instance.TryUsePlayerSkill();
            if (!success)
                UIManager.Instance?.ShowToast("技能能量不足");
        }
    }

    void OnMercSkillClick(int mercIndex)
    {
        if (BattleManager.Instance == null) return;
        bool success = BattleManager.Instance.TryUseMercSkill(mercIndex);
        if (!success)
            UIManager.Instance?.ShowToast("佣兵技能未就绪");
    }

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
        if (skillIndex == 0 && playerSlot != null)
        {
            playerSlot.SetEnergy(energyRatio);
        }
        else if (skillIndex == 1 && mercSlot1 != null)
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
}
