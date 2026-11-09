using System.Collections;
using UnityEngine;

/// <summary>
/// 竞技场开场「对战」动画（2026-10-30 主人拍板：从竞技场进入的战斗，点「开始战斗/继续」后播一次）。
///
/// 节点 = BattleUI 根下主人手摆的「对战」（bg + 对 + 战，挂 Animator）；
/// 控制器 Resources/Effect/Ani/对战/对战.controller 的默认状态名就叫「对战」
///（剪辑 duizhan.anim，0.5 秒：bg 缩放 3→1 砸落、「对」从左滑入、「战」从右滑入）。
///
/// 口径：
/// · 节点在预制体里默认是亮的 → 战斗 UI 一初始化就整根藏掉（BattleUI.Awake 调 HideForBattle）：
///   非竞技场战斗全程不可见；竞技场战斗也要等玩家点「开始战斗/继续」才亮；
/// · 播放入口只有一个：BattleManager.OnLiveStartFight（LiveMode「开始战斗/继续」回调）调
///   TryPlayForArena——内部自查 Mode.Id == BattleModeId.Arena，非竞技场直接回调开战；
///   0.5 秒，播完自动藏回并回调开战（竞技场首波等动画放完才刷，见 OnLiveStartFight）；
/// · 动画走节点自带 Animator 的默认状态（Play 重置到 0 帧）。节点缺失 / 控制器取不到 /
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
    /// <summary>对战控制器：动画资源已挪进 Resources/Effect/Ani/对战/，按路径取，不依赖预制体引用。</summary>
    const string ControllerPath = "Effect/Ani/对战/对战";
    /// <summary>三张图按路径装图：预制体里的 sprite guid 引用落不了盘（56 字符 meta 口径），
    /// 图本来就在 Resources/UI/Art/对战/，运行时按路径装，不复制不移动资源（主人 2026-11-09 确认）。</summary>
    const string ArtDir = "UI/Art/对战/";
    const string BgNodeName = "bg";
    const string DuiNodeName = "对";
    const string ZhanNodeName = "战";

    static void EnsureArt(Transform node)
    {
        ApplySprite(node, BgNodeName, "对战底图");
        ApplySprite(node, DuiNodeName, "对");
        ApplySprite(node, ZhanNodeName, "战");
    }

    /// <summary>只补空的 Image：主人后来在预制体里手拖了图就不覆盖；取不到图 LogError，不静默。</summary>
    static void ApplySprite(Transform root, string child, string file)
    {
        var target = root.Find(child);
        if (target == null) { Debug.LogError($"[BattleVsIntro] 「对战」节点缺子节点：{child}"); return; }
        var img = target.GetComponent<UnityEngine.UI.Image>();
        if (img == null) { Debug.LogError($"[BattleVsIntro] 「对战」节点子节点 {child} 没有 Image 组件"); return; }
        if (img.sprite != null) return;
        var sprite = Resources.Load<Sprite>(ArtDir + file);
        if (sprite == null) { Debug.LogError($"[BattleVsIntro] 对战图缺失：Resources/{ArtDir}{file}"); return; }
        img.sprite = sprite;
    }

    /// <summary>战斗 UI 初始化时调：藏掉「对战」节点（幂等）。</summary>
    public static void HideForBattle()
    {
        _playing = null;   // 上一局的协程宿主已随场景销毁，句柄作废
        var node = ResolveNode();
        if (node == null) return;
        node.gameObject.SetActive(false);
    }

    /// <summary>
    /// 竞技场「开始战斗/继续」点击后调：亮出节点、默认状态拉回 0 帧播一次，
    /// 播完藏回后回调 onFinished 再开战（主人 2026-11-09 拍板「动画播完消失后再开始」）。
    /// 只在竞技场生效；没播成（非竞技场 / 节点 / 控制器 / 状态异常）一律立刻回调，绝不把战斗卡住。
    /// </summary>
    public static void TryPlayForArena(System.Action onFinished)
    {
        if (!TryStart(BattleManager.Instance, onFinished)) onFinished?.Invoke();
    }

    /// <returns>true = 动画已起播，回调交给协程；false = 没播成，调用方立刻开战。</returns>
    static bool TryStart(BattleManager bm, System.Action onFinished)
    {
        if (bm == null || bm.Mode == null || bm.Mode.Id != BattleModeId.Arena) return false;

        var node = ResolveNode();
        if (node == null) return false;   // ResolveNode 已 LogError
        var animator = node.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[BattleVsIntro] 「对战」节点没有 Animator 组件，对战动画播不了（预制体节点被改动？）");
            node.gameObject.SetActive(false);
            return false;
        }
        if (animator.runtimeAnimatorController == null)
        {
            // 【2026-11-09】动画资源已挪进 Resources/Effect/Ani/对战/：预制体里那条 hex32 控制器引用
            // 落不了盘，按路径取（AGENTS §2.3「运行时资源一律 Resources.Load 按路径加载」）。取不到才报错放行。
            var ctrl = Resources.Load<RuntimeAnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                Debug.LogError($"[BattleVsIntro] 对战控制器缺失：Resources/{ControllerPath}" +
                               "（预制体控制器引用断链，按路径也没取到），对战动画播不了");
                node.gameObject.SetActive(false);
                return false;
            }
            bool hasClip = false;
            foreach (var c in ctrl.animationClips) { if (c != null) { hasClip = true; break; } }
            if (!hasClip)
                Debug.LogError("[BattleVsIntro] 对战控制器的状态里没有剪辑（m_Motion 引用断链），动画会空转 —— " +
                               "请在编辑器里把 Assets/Resources/Effect/Ani/对战/duizhan.anim 重新拖到 对战.controller 的「对战」状态上");
            animator.runtimeAnimatorController = ctrl;
        }

        var ui = BattleUI.Instance;
        if (ui == null)
        {
            Debug.LogError("[BattleVsIntro] BattleUI 不存在，对战动画没法播");
            node.gameObject.SetActive(false);
            return false;
        }
        if (_playing != null) { bm.StopCoroutine(_playing); _playing = null; }

        EnsureArt(node);
        // 【2026-10-31 修「点继续没看到对战动画」】LiveMode 点「开始战斗/继续」后抽奖面板保持
        // 常显（展开态，10-29 拍板），「对战」节点若排在背包面板之前会被整块盖住 →
        // 播放前拉到 BattleUI 同级最后（最上层），0.5 秒播完照旧藏回，不影响面板常显。
        node.SetAsLastSibling();
        node.gameObject.SetActive(true);
        animator.Play(StateName, 0, 0f);
        animator.Update(0f);
        // 状态名对不上时 Play 是无声空转 → 当场验一次，坏在哪一眼看见
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Animator.StringToHash(StateName))
        {
            Debug.LogError($"[BattleVsIntro] 控制器里没有名为「{StateName}」的状态（状态被改名了？），对战动画播不了");
            node.gameObject.SetActive(false);
            return false;
        }
        _playing = bm.StartCoroutine(CoHideWhenDone(animator, node, onFinished));
        return true;
    }

    /// <summary>状态播完（normalizedTime ≥ 1）即藏回；MaxHoldSec 只兜状态异常时的收尾。
    /// 【2026-11-09 主人拍板】先藏回节点、再回调开战：「动画播完消失后再开始」。</summary>
    static IEnumerator CoHideWhenDone(Animator animator, Transform node, System.Action onFinished)
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
        onFinished?.Invoke();
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
