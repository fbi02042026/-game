using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 传送门
    private bool _portalActive = false;
    private bool _rewardSequenceStarted = false;
    private Transform _chuanSongMen;

    // 【2026-10-11 主人拍板】传送门改用主人新加的 chuansongmen 预制体（PortalGate，挂 BattleUI 下）。
    // 场景里那个旧的 chuansongmen 节点不再参与通关流程（保留不删）。
    private PortalGate _portalGate;
    /// <summary>要走进门的队伍（开门那一刻定好：玩家 + 当时活着的佣兵），避免每帧重新取列表。</summary>
    private readonly List<UnitBase> _portalParty = new List<UnitBase>();
    private readonly HashSet<UnitBase> _portalEntered = new HashSet<UnitBase>();
    private bool _heroEnteredPortal;
    private float _heroEnteredAt;
    private float _portalOpenedAt;
    /// <summary>
    /// 【2026-10-15 主人拍板】引导结尾也走传送门（「所有打完一关后都出现传送门」）：
    /// true = 这个门是引导局的出口，全员进门后不走正常关清场链，直接接引导撤离结算（settlement → 回城）。
    /// </summary>
    private bool _tutorialPortalPending;
    /// <summary>玩家进门后，最多再等佣兵这么久（秒）—— 超时也走结算，绝不把玩家锁在门口。</summary>
    const float PortalMercWaitTimeout = 2.5f;

    /// <summary>
    /// 旧 endPoint 传送门路径（已弃用）。正式通关走 chuansongmen：StageClearRewardDirector → NotifyChuanSongMenOpened。
    /// </summary>
    [System.Obsolete("使用 chuansongmen 通关导演，勿再调用 ActivatePortal")]
    void ActivatePortal()
    {
        GamePerf.Log("[BattleManager] ActivatePortal 已弃用，忽略（请走 chuansongmen）");
    }

    IEnumerator CoActivatePortal()
    {
        yield break;
    }

    /// <summary>开战时预挂传送门动画，避免清场瞬间 AddComponent</summary>
    public static void EnsurePortalAnimatorReady(Transform end)
    {
        if (end == null) return;
        var fx = end.GetComponent<PortalAnimator>();
        if (fx == null) fx = end.GetComponentInChildren<PortalAnimator>(true);
        if (fx == null)
            fx = end.gameObject.AddComponent<PortalAnimator>();
        fx.Warm();
        // 保持未激活，等真正通关再开
        if (fx.gameObject != end.gameObject)
            fx.gameObject.SetActive(false);
        else
            fx.enabled = false;
    }

    internal void ExtendCameraMaxX(float worldX)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) return;
        if (worldX > follow.maxX)
            follow.maxX = worldX;
    }

    /// <summary>
    /// 隐藏传送门（新一局开始时，打完怪才出现）
    /// </summary>
    void HidePortal()
    {
        if (endPoint != null)
        {
            // 隐藏EndPoint视觉（传送门），但保留碰撞/逻辑
            SpriteRenderer portalSr = endPoint.GetComponent<SpriteRenderer>();
            if (portalSr != null)
            {
                portalSr.enabled = false;
            }
            var portalFx = endPoint.GetComponentInChildren<PortalAnimator>(true);
            if (portalFx != null)
            {
                portalFx.enabled = false;
                if (portalFx.gameObject != endPoint.gameObject)
                    portalFx.gameObject.SetActive(false);
            }
        }
    }
    #endregion
}
