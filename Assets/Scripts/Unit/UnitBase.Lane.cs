using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 战斗单位基类 partial —— 2026-10-18 由 UnitBase.cs 纯位移拆出（无逻辑改动）
// 职责：车道与站位（车道容错偏移、SetLaneY/ApplyLaneY 并道归位、前后遮挡刷新）
public partial class UnitBase
{

    /// <summary>清空容错偏移（换局/复位）。</summary>
    protected void ClearLaneAlignBias()
    {
        _laneBiasOwner = null;
        _laneChaseAnchorOwner = null;
        _laneChaseAnchorY = 0f;
        LaneAlignBiasY = 0f;
        LaneAlignBiasX = 0f;
    }

    /// <summary>换目标或首次进战斗时抽一次偏移方向（上下 ± 与一点点左右），之后固定不变，避免每帧抖动。</summary>
    void RollLaneAlignBias(UnitBase chaseTarget)
    {
        _laneBiasOwner = chaseTarget;
        if (!GameConfig.ENABLE_LANE_ALIGN_TOLERANCE)
        {
            LaneAlignBiasY = 0f;
            LaneAlignBiasX = 0f;
            return;
        }
        float amp = GameConfig.LANE_ALIGN_TOLERANCE
            * Random.Range(Mathf.Clamp01(GameConfig.LANE_ALIGN_TOLERANCE_MIN_RATIO), 1f);
        LaneAlignBiasY = Random.value < 0.5f ? -amp : amp;
        LaneAlignBiasX = Mathf.Max(0f, Random.Range(0f, GameConfig.LANE_ALIGN_TOLERANCE_X));
    }

    public void SetLaneY(float offset)
    {
        float clamped = BattleLaneBounds.ClampLaneOffset(offset);
        LaneY = clamped;
    }

    /// <summary>把 LaneY 同步成当前脚底相对站立线的偏移（入场中断 / 刷怪纠偏）。</summary>
    public void SyncLaneYFromWorld()
    {
        var t = LaneMoveTransform;
        if (t == null) t = transform;
        SetLaneY(t.position.y - GROUND_Y);
    }

    /// <summary>目标实际站位车道（优先世界 Y，避免 LaneY 与脚底脱节）。</summary>
    public float GetWorldLaneOffset()
    {
        Transform moveTf = transform;
        if (this is Monster mon)
            moveTf = mon.GetBodyTransform();
        if (moveTf == null) moveTf = transform;
        return BattleLaneBounds.ClampLaneOffset(moveTf.position.y - GROUND_Y);
    }

    protected virtual Transform LaneMoveTransform => transform;

    protected void ApplyLaneY(float dt)
    {
        var t = LaneMoveTransform;
        if (t == null) return;
        var p = t.position;
        float target = FootY;
        if (Mathf.Abs(p.y - target) < 0.002f)
        {
            RefreshDepthSort();
            return;
        }
        p.y = Mathf.MoveTowards(p.y, target, GameConfig.BATTLE_LANE_MOVE_SPEED * dt);
        // SetWorldPosition 会清 velocity；保留水平速度，避免与追敌/手动位移打架
        float keepVx = rb != null ? rb.velocity.x : 0f;
        GameConfig.SetWorldPosition(t, p);
        if (rb != null)
            rb.velocity = new Vector2(keepVx, 0f);

        // 2026-09-27 主人反馈：「打完后又滑步到场景中间车道」。
        // 归道（击杀后回中间车道）时 X 速度为 0，AIUpdate 判定 isMoving=false 播的是站立，
        // 但身体在 Y 方向被搬 → 站着滑过去。这里在有实际 Y 位移时补一次走路动画。
        // 只补我方（主人报的就是我方滑步）；攻击锁 / 受击硬直期间不补，免得打断出手动作。
        if (isAlly && unitAnim != null && !unitAnim.InAttackLock && !unitAnim.InDamagedRecovery())
            unitAnim.SetMove(true, facingDir);

        RefreshDepthSort();
    }

    /// <summary>按脚底 Y 刷新前后遮挡（越靠下越前）。只动 SortingGroup，不改 SPUM 部件 order。</summary>
    protected void RefreshDepthSort()
    {
        var foot = LaneMoveTransform;
        if (foot == null) foot = transform;
        GameConfig.ApplyUnitSorting(transform, foot.position.y);
    }
}
