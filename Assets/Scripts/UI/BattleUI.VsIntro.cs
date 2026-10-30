using System.Collections;
using UnityEngine;

/// <summary>
/// 竞技场开场「对战」动画（2026-10-30 主人拍板：从竞技场进入的战斗，点「开始战斗/继续」后播一次）。
///
/// 节点 = BattleUI 根下主人手摆的「对战」（bg + 对 + 战，挂 Animator）；
/// 控制器 Assets/Art/Effects/Ani/对战/对战.controller 的默认状态名就叫「对战」
///（剪辑 duizhan.anim，0.5 秒：bg 缩放 3→1 砸落、「对」从左滑入、「战」从右滑入）。
///
/// 口径：
/// · 节点在预制体里默认是亮的 → 战斗 UI 一初始化就整根藏掉（BattleUI.Awake 调 HideForBattle）：
///   非竞技场战斗全程不可见；竞技场战斗也要等玩家点「开始战斗/继续」才亮；
/// · 播放入口只有一个：BattleManager.OnLiveStartFight（LiveMode「开始战斗/继续」回调）调
///   TryPlayForArena——内部自查 Mode.Id == BattleModeId.Arena，非竞技场静默跳过；
///   0.5 秒非阻塞，不挡首波刷怪，播完自动藏回；
/// · 动画走节点自带 Animator 的默认状态（Play 重置到 0 帧）。节点缺失 / 控制器断链 /
///   状态名对不上一律 LogError 让主人看见，不做任何兜底补播（铁律 12/14）。
/// </summary>
public static class BattleVsIntro
{
    /// <summary>BattleUI 根下主人手摆的动画节点名（BattleUI.prefab 里唯一）。</summary>
    const string NodeName = "对战";
    /// <summary>控制器默认状态名（与 对战.controller 的 AnimatorState.m_Name 同值，改名要两边对齐）。</summary>
    const string StateName = "对战";
    /// <summary>异常时收尾上限：duizhan.anim 实际 0.5s，略放宽；绝不让动画盖屏常驻。</summary>
    const float MaxHoldSec = 0.9f;

    static Transform _node;
    static Coroutine _playing;

    /// <summary>战斗 UI 初始化时调：藏掉「对战」节点（幂等）。</summary>
    public static void HideForBattle()
    {
        _playing = null;   // 上一局的协程宿主已随场景销毁，句柄作废
        var node = ResolveNode();
        if (node == null) return;
        node.gameObject.SetActive(false);
    }

    /// <summary>
    /// 竞技场「开始战斗/继续」点击后调：亮出节点、默认状态拉回 0 帧播一次，播完自动藏回。
    /// 只在竞技场战斗生效（内部自查 Mode，调用方免管）；非竞技场静默跳过。
    /// </summary>
    public static void TryPlayForArena()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.Mode == null || bm.Mode.Id != BattleModeId.Arena) return;

        var node = ResolveNode();
        if (node == null) return;   // ResolveNode 已 LogError
        var animator = node.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[BattleVsIntro] 「对战」节点没有 Animator 组件，对战动画播不了（预制体节点被改动？）");
            node.gameObject.SetActive(false);
            return;
        }
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError("[BattleVsIntro] 「对战」节点的 Animator 没绑上控制器（BattleUI.prefab 的控制器引用断链），" +
                           "对战动画播不了——请主人在预制体里重新拖一次 Assets/Art/Effects/Ani/对战/对战.controller");
            node.gameObject.SetActive(false);
            return;
        }

        var ui = BattleUI.Instance;
        if (ui == null)
        {
            Debug.LogError("[BattleVsIntro] BattleUI 不存在，对战动画没法播");
            node.gameObject.SetActive(false);
            return;
        }
        if (_playing != null) { ui.StopCoroutine(_playing); _playing = null; }

        node.gameObject.SetActive(true);
        animator.Play(StateName, 0, 0f);
        animator.Update(0f);
        // 状态名对不上时 Play 是无声空转 → 当场验一次，坏在哪一眼看见
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Animator.StringToHash(StateName))
        {
            Debug.LogError($"[BattleVsIntro] 控制器里没有名为「{StateName}」的状态（状态被改名了？），对战动画播不了");
            node.gameObject.SetActive(false);
            return;
        }
        _playing = ui.StartCoroutine(CoHideWhenDone(animator, node));
    }

    /// <summary>状态播完（normalizedTime ≥ 1）即藏回；MaxHoldSec 只兜状态异常时的收尾。</summary>
    static IEnumerator CoHideWhenDone(Animator animator, Transform node)
    {
        float t = 0f;
        while (t < MaxHoldSec)
        {
            t += Time.deltaTime;   // Animator 是 Normal 更新（跟时间缩放），等待用同一把尺子
            if (animator != null && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f) break;
            yield return null;
        }
        _playing = null;
        if (node != null) node.gameObject.SetActive(false);
    }

    /// <summary>节点唯一出口：BattleUI 根的直接子节点「对战」。找不到 LogError（不兜底）。</summary>
    static Transform ResolveNode()
    {
        if (_node != null) return _node;
        var ui = BattleUI.Instance;
        if (ui == null)
        {
            Debug.LogError("[BattleVsIntro] BattleUI 不存在，找不到「对战」节点");
            return null;
        }
        _node = ui.transform.Find(NodeName);
        if (_node == null)
            Debug.LogError("[BattleVsIntro] BattleUI 预制体缺节点：「对战」（主人手摆的开场动画节点没找到）");
        return _node;
    }
}
