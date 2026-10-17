using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 新手引导战斗
    public void BeginTutorialPowerFantasy()
    {
        if (!IsTutorialRun) return;
        TutorialPowerFantasy = true;
        Debug.Log("[BattleManager] 引导拿剑爽点：一刀一个 + 后续加怪");
    }
    /// <summary>导演节拍：按 tutorial_battle 步进刷一波。count/进场方向/HP 档来自表，不由导演发明。</summary>
    public void QueueTutorialStep(int order, float? anchorX = null, UnitBase forcedTarget = null)
    {
        Planner.QueueTutorialStep(order, anchorX, forcedTarget);
    }

    public void QueueTutorialWave(int count)
    {
        Planner.QueueTutorialWave(count);
    }

    public Mercenary SpawnTutorialMerc(string mercId, float hpRatio)
    {
        return SpawnTutorialMercAt(mercId, hpRatio, 7.5f, stunned: true);
    }

    /// <summary>
    /// 引导救援：牧师刷在玩家前方固定距离，原地眩晕；周围刷怪围殴她。
    /// </summary>
    public Mercenary SpawnTutorialMercAt(string mercId, float hpRatio, float aheadDist, bool stunned)
    {
        var mm = MercenaryManager.Instance;
        // 2026-09-27：以前失败只返回 null，导演侧只看到「牧师入队失败：merc 为空」这半句，
        // 不知道卡在哪一关。现在按 fail-closed 把原因打出来（不静默沿用、不拿别的单位顶替）。
        if (mm == null)
        {
            Debug.LogError("[BattleManager] SpawnTutorialMercAt 失败：MercenaryManager.Instance 为空");
            return null;
        }
        if (hero == null)
        {
            Debug.LogError("[BattleManager] SpawnTutorialMercAt 失败：hero 为空（玩家已阵亡或本局已收尾）");
            return null;
        }

        float heroX = UnitBase.GetCombatX(hero);
        float z = unitRoot != null ? unitRoot.position.z : hero.transform.position.z;
        Vector3 pos = new Vector3(heroX + aheadDist, UnitBase.GROUND_Y, z);
        string useId = string.IsNullOrEmpty(mercId) ? StoryProgress.TutorialMercId : mercId;
        var merc = mm.SpawnMercenary(useId, pos, 1);
        if (merc == null)
        {
            Debug.LogError($"[BattleManager] SpawnTutorialMercAt 失败：SpawnMercenary 返回空 mercId={useId}");
            return null;
        }
        MercRosterDefs.GetSkillIds(useId, out string active, out string passive);
        merc.SetupBattleSkills(active, passive);
        merc.SetDisplayName(StoryProgress.TutorialMercDisplayName);
        merc.SetHireId(StoryProgress.TutorialMercHireId);
        if (!allyUnits.Contains(merc))
            allyUnits.Add(merc);
        merc.OnDead += OnMercenaryDead;
        RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
        float maxHp = merc.attr != null ? merc.attr.GetAttr(AttrType.MaxHp) : 100f;
        merc.currentHp = Mathf.Max(1f, maxHp * Mathf.Clamp01(hpRatio));
        merc.Face(-1);
        if (stunned)
            merc.SetTutorialStunned(true);
        ExtendCameraMaxX(pos.x + 6f);
        return merc;
    }
    /// <summary>在受害者周围刷怪并强制锁定打他（引导围殴）。走 SpawnWave 参数，不再另开刷怪轨。</summary>
    public void SpawnTutorialAmbushAround(UnitBase victim, int count)
    {
        Planner.SpawnTutorialAmbushAround(victim, count);
    }

    /// <summary>诱饵埋伏：从锚点左右两侧刷怪。走 SpawnWave 参数（bilateralEnter）。</summary>
    public void SpawnTutorialFlankAmbush(int count, float? anchorX = null)
    {
        Planner.SpawnTutorialFlankAmbush(count, anchorX);
    }

    public void RetargetAllMonsters(UnitBase target)
    {
        if (monsters == null) return;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i] as Monster;
            if (m == null || m.isDead) continue;
            m.SetForcedTarget(target);
        }
    }

    public void ClearMonsterForcedTargets()
    {
        if (monsters == null) return;
        for (int i = 0; i < monsters.Count; i++)
        {
            var m = monsters[i] as Monster;
            if (m == null) continue;
            m.SetForcedTarget(null);
        }
    }

    // ============================================================
    // 波次生成
    // ============================================================

    #endregion
}
