using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 战斗统计
    public void RecordDamageDealt(float amount, bool toBoss)
    {
        if (amount <= 0f) return;
        RunStats.DamageDealt += amount;
        if (toBoss) RunStats.BossDamageDealt += amount;
    }

    public void RecordDamageTaken(float amount)
    {
        if (amount <= 0f) return;
        RunStats.DamageTaken += amount;
    }

    public void RecordCrit()
    {
        RunStats.CritCount++;
    }

    public static string GetAllyMvpKey(UnitBase u)
    {
        if (u == null) return BattleRunStats.PlayerMvpKey;
        if (u is Mercenary merc)
        {
            if (!string.IsNullOrEmpty(merc.hireId)) return merc.hireId;
            if (!string.IsNullOrEmpty(merc.mercId)) return merc.mercId;
            return "merc";
        }
        return BattleRunStats.PlayerMvpKey;
    }

    public static string GetAllyMvpDisplayName(UnitBase u)
    {
        if (u is Mercenary merc)
        {
            // 2026-10-09 主人拍板：结算 MVP 必须显示花名册**真名**（塔克），不是战斗名签用的**花名**（重盾）。
            // 只在这一个出口改优先级，不动 Mercenary.SetDisplayName 的全局口径（那会影响所有战斗名签）。
            MercRosterDefs.Def def;
            if (!string.IsNullOrEmpty(merc.hireId) && MercRosterDefs.TryGetByHireId(merc.hireId, out def)
                && !string.IsNullOrEmpty(def.Name))
                return def.Name;
            if (!string.IsNullOrEmpty(merc.mercId) && MercRosterDefs.TryGetByAssetId(merc.mercId, out def)
                && !string.IsNullOrEmpty(def.Name))
                return def.Name;
            if (!string.IsNullOrEmpty(merc.DisplayName)) return merc.DisplayName;
            // 【2026-10-11 主人拍板「佣兵名字要统一」】旧写法在这里还留了一条「退回昵称（重盾）」的兜底，
            // 结果花名册没真名时结算 MVP 会写出第二个叫法 → 删掉；缺真名就退回编号，不拿昵称顶（铁律 14）。
            return !string.IsNullOrEmpty(merc.mercId) ? merc.mercId : "佣兵";
        }
        var save = SaveSystem.Instance?.Data;
        return save != null && !string.IsNullOrEmpty(save.playerDisplayName)
            ? save.playerDisplayName : "冒险者";
    }

    public void RecordAllyDamage(UnitBase source, float amount)
    {
        if (source == null || !source.isAlly || amount <= 0f) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(source), GetAllyMvpDisplayName(source));
        c.damage += amount;
    }

    public void RecordAllyKill(UnitBase source)
    {
        if (source == null || !source.isAlly) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(source), GetAllyMvpDisplayName(source));
        c.kills++;
    }

    public void RecordAllyHeal(UnitBase healer, float amount)
    {
        if (healer == null || !healer.isAlly || amount <= 0f) return;
        var c = RunStats.EnsureAlly(GetAllyMvpKey(healer), GetAllyMvpDisplayName(healer));
        c.healingDone += amount;
    }

    public void ClearAllMonsters()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i];
            if (m == null) continue;
            m.OnDead -= OnMonsterDead;
            if (PoolManager.Instance != null)
                PoolManager.Instance.Release(m.gameObject);
            else
                Destroy(m.gameObject);
        }
        monsters.Clear();
        _aliveMonsterCache = -1;
    }

    // ============================================================
    // 特殊关卡
    // ============================================================

    // 商人/诅咒/锻造/附魔：本版不进池，保留方法供后续开放
    [System.Obsolete("本版未开放商人关")]
    void LoadMerchantStage(StageData stage)
    {
        UIManager.Instance?.ShowToast("本版本未开放商人关");
        ChapterManager.Instance?.OnStageComplete();
        UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
    }

    [System.Obsolete("本版未开放诅咒关")]
    void LoadCurseStage(StageData stage)
    {
        UIManager.Instance?.ShowToast("本版本未开放诅咒关");
        ChapterManager.Instance?.OnStageComplete();
        UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
    }

    /// <summary>
    /// 锻造关：弹锻造窗（发强化材料），关掉后继续推关。
    /// </summary>
    void LoadForgeStage()
    {
        CraftStagePopupUI.ShowForge(() =>
        {
            ChapterManager.Instance?.OnStageComplete();
            UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
        });
    }

    void LoadEnchantStage()
    {
        CraftStagePopupUI.ShowEnchant(() =>
        {
            ChapterManager.Instance?.OnStageComplete();
            UIManager.Instance?.ShowStageSelectUI(ChapterManager.Instance?.availableNextStages);
        });
    }

    void LoadRestStage()
    {
        // 【2026-10-11 主人报「老虎机轮到恢复关卡后弹不出界面 卡到这了」】
        // 进恢复关先打一条：主人下次卡住时，Editor.log 里有这行 = 已经进关、问题在弹窗本身；
        // 没有这行 = 压根没进关（卡在关卡图/老虎机那一段），查 BattleStageMapUI。
        Debug.Log("[BattleManager] 进入恢复关 → 准备弹 RestStagePopup");
        // 弹窗里已经回过 50% 血；关掉后继续推关
        UIManager.Instance.ShowRestUI(() =>
        {
            ChapterManager.Instance.OnStageComplete();
            UIManager.Instance.ShowStageSelectUI(ChapterManager.Instance.availableNextStages);
        }, null);
    }

    List<CurseBuff> GenerateCurseOptions()
    {
        // 2026-09-29：攻击% 按职业分流到 Attack / MagicAttack，否则法师/牧师选「嗜血」=
        // 加在用不上的物攻上，选「疾风」= 扣了个自己没有的东西。
        AttrType atk = PlayerJobBaseStats.CurrentAttackAttr();
        return new List<CurseBuff>
        {
            new CurseBuff { buffName = "嗜血：攻击+30%，生命-15%", buff = new AttrBonusData { attrType = atk, value = 0.3f, isPercent = true }, debuff = new AttrBonusData { attrType = AttrType.MaxHp, value = -0.15f, isPercent = true } },
            new CurseBuff { buffName = "疾风：攻速+40%，攻击-20%", buff = new AttrBonusData { attrType = AttrType.AttackSpeed, value = 0.4f, isPercent = true }, debuff = new AttrBonusData { attrType = atk, value = -0.2f, isPercent = true } },
            new CurseBuff { buffName = "坚壁：生命+50%，移速-30%", buff = new AttrBonusData { attrType = AttrType.MaxHp, value = 0.5f, isPercent = true }, debuff = new AttrBonusData { attrType = AttrType.MoveSpeed, value = -0.3f, isPercent = true } }
        };
    }
    #endregion
}
