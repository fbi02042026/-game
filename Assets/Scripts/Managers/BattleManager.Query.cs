using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 战斗查询与增益
    public void OnMercenaryDead(UnitBase merc)
    {
        allyUnits.Remove(merc);
        // 【2026-10-17 诊断日志】玩家报「佣兵没死就消失」——佣兵平时没血条，真机大伤害秒杀看不出来。
        // 这里把阵亡实锤打出来：谁、多少血死的、死在哪，下次复现直接对日志。
        if (merc is Mercenary md)
            Debug.LogWarning($"[BattleManager] 佣兵阵亡：{md.DisplayName}({md.mercId}) 血量={md.currentHp:0.}/max={md.attr?.GetAttr(AttrType.MaxHp) ?? 0f:0.} pos={merc.transform.position:F1}");
        else
            Debug.LogWarning($"[BattleManager] 佣兵阵亡：{merc.name}");
        // 2026-10-06 主人拍板：阵亡不再零成本 —— 记进本局构筑，下一关按 20% 血复活（每过一关养回 30%）。
        if (merc is Mercenary m)
            RunLoadout.MarkMercFallen(!string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId);
        TryPlayMercDeathLine(merc);
    }

    void TryPlayMercDeathLine(UnitBase unit)
    {
        if (Rules.SkipMercDeathBanter) return;
        if (!(unit is Mercenary m) || m == null) return;
        string key = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
        string line = MercLineTable.Pick(key, MercLineTable.Scene.Death);
        if (string.IsNullOrEmpty(line)) return;

        // 死亡动画期间单位仍在：优先头顶气泡；否则 Toast
        if (unit != null && unit.gameObject != null && unit.gameObject.activeInHierarchy
            && (BattleHeadTalkUI.Instance == null || !BattleHeadTalkUI.Instance.IsShowing))
        {
            BattleHeadTalkUI.Ensure().PlayLine(unit, line, 1.25f);
            return;
        }
        if (UIManager.Instance != null)
            UIManager.Instance.ShowToast(line);
        else
            GlobalToastUI.Show(line);
    }

    void ApplyChapter1RunModifiersFromSave()
    {
        if (Rules.SkipChapter1RunModifiers) return;
        string c = StoryProgress.GetChoice(1);
        if (c == "A") runMonsterAtkSpeedMul = 1.05f;
        else if (c == "B") runGoldGainMul = 1.1f;
    }

    public void ApplyChapter1ChoiceModifiers(string choiceId)
    {
        if (choiceId == "A") runMonsterAtkSpeedMul = 1.05f;
        else if (choiceId == "B") runGoldGainMul = 1.1f;
    }

    public int GetAliveMonsterCount() => CountAliveMonsters();

    /// <summary>是否还有「已排队但未刷出」的波次（教程步/普通波都算）。用于清场判定，避免刷怪空窗被误判为已清。</summary>
    public bool HasPendingWaves => Planner != null && Planner.FindNextUnspawnedWaveIndex() >= 0;

    /// <summary>
    /// 玩家技能充能（V6）：<b>每个技能槽各一条</b>，受击时给所有「未满 且 不在冷却中」的槽同时注入。
    /// 冷却中的槽不充 —— 这条是天然错峰的关键：短 CD 技能先攒满先放，长 CD 的护盾后放，
    /// 不需要额外的「充能队列」逻辑，也不会出现四个技能同时爆发出。
    /// 总量由 GameConfig.SKILL_ENERGY_CHARGE_MUL 统一压制（这是手感主旋钮）。
    /// </summary>
    public void AddPlayerSkillEnergy(float rawAmount)
    {
        if (playerSkillEnergy == null || rawAmount <= 0f) return;

        float mul = Mathf.Max(0f, GameConfig.SKILL_ENERGY_CHARGE_MUL);
        float add = rawAmount * mul;
        if (add <= 0f) return;

        var sys = SkillSystem.Instance;
        var skills = sys != null ? sys.GetPlayerSkills() : null;
        int count = skills != null
            ? Mathf.Min(playerSkillEnergy.Length, skills.Count)
            : playerSkillEnergy.Length;

        for (int i = 0; i < count; i++)
        {
            if (playerSkillEnergy[i] >= MAX_SKILL_ENERGY) continue;      // 已满，不浪费

            if (skills != null)
            {
                var s = skills[i];
                if (s != null && !string.IsNullOrEmpty(s.skillId) && sys.IsOnCooldown(s.skillId))
                    continue;                                            // 冷却中，不充能
            }

            playerSkillEnergy[i] = Mathf.Min(MAX_SKILL_ENERGY, playerSkillEnergy[i] + add);
        }
        BattleUI.Instance?.UpdateSkillEnergy(0, PlayerSkillEnergyPeak);
    }

    /// <summary>
    /// 把所有技能槽的能量充满（V6：技能改为各自一条能量后，这里不再只充一条）。
    /// 目前无正式调用方，是留给剧情/教程的显式钩子 —— 教程最后一波会用它保证玩家一定看到技能放出。
    /// </summary>
    /// <summary>
    /// 教程钩子：让玩家的技能进入「可预期的一段冷却」后自然放出来（教程最后一波靠它让玩家看到技能）。
    /// 2026-09-27 主人拍板：这里**不再清冷却**（ClearPlayerSkillCooldowns 会让技能瞬间可放，
    /// 与主人口径「技能刚上来要先走一遍冷却」冲突）→ 改为重新挂一遍完整开场冷却。
    /// </summary>
    public void FillPlayerSkillEnergy()
    {
        // 纯冷却制：技能本来就不看能量，要做的是「解除全局间隔 + 重新挂完整冷却」，让它走完 CD 再放
        ResetPlayerSkillGcd();
        PrimePlayerSkillOpeningCooldowns();

        if (GameConfig.PLAYER_SKILL_USE_ENERGY && playerSkillEnergy != null)
        {
            for (int i = 0; i < playerSkillEnergy.Length; i++)
                playerSkillEnergy[i] = MAX_SKILL_ENERGY;
            BattleUI.Instance?.UpdateSkillEnergy(0, MAX_SKILL_ENERGY);
        }
    }

    /// <summary>兼容旧的「单条能量」HUD：返回所有技能槽里充得最高的那条。</summary>
    public float PlayerSkillEnergyPeak
    {
        get
        {
            if (playerSkillEnergy == null) return 0f;
            float peak = 0f;
            for (int i = 0; i < playerSkillEnergy.Length; i++)
                if (playerSkillEnergy[i] > peak) peak = playerSkillEnergy[i];
            return peak;
        }
    }

    /// <summary>盟友受击回能：传入 finalDamage/MaxHp（打满血约攒满一槽）。</summary>
    public void AddCombatSkillEnergy(UnitBase unit, float amount)
    {
        if (unit == null || !unit.isAlly || amount <= 0f || !isInBattle) return;
        if (_stageCleared || _portalActive) return;

        // 玩家侧：纯冷却制下不充能（PLAYER_SKILL_USE_ENERGY 置 true 可退回原行为）。
        if (unit is Hero)
        {
            if (GameConfig.PLAYER_SKILL_USE_ENERGY)
                AddPlayerSkillEnergy(amount);
            return;
        }

        // 2026-10-06 主人拍板：佣兵改「自然回复制」，受击回蓝这条通道整段删除（旧实现不留）。
        // 佣兵蓝条只由 TickMercMp 每秒回，受击不再给任何蓝 —— 否则血线越低回得越快，与紧张感口径相反。
        return;
    }

    /// <summary>引导开箱拿剑后进入强伤+多怪爽点。</summary>
    #endregion
}
