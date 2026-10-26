using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 冒险界面 partial —— 动效
//   ① 左侧标签（LeftSidebar/ModeBtn_x）进页时从屏幕左外侧逐个滑入（相邻标签错峰并行，非串行等待）；【2026-10-24 主人拍板】
//   ② 右侧切模式（点左侧标签换 主线/无限/活动副本 节点）时新页面弹出。
// 铁律 32 合规：只做**临时**动效 —— 左侧标签只动 anchoredPosition 与 CanvasGroup.alpha（右侧仍只动 localScale），
// 基准值先进 _popBaseAnchoredPos/_popBaseScales 缓存、动画结束/页面隐藏一律还原，不写任何布局尺寸/Image.type。
public partial class AdventureUI
{
    // ── 左侧标签滑入参数 ──
    /// <summary>单颗标签滑入时长（秒）。【2026-10-24 主人拍板「快一倍」：0.38 → 0.19；同日再看还要再快一倍：0.19 → 0.095；2026-10-25 主人拍板「单个弹出延长20%」：0.095 × 1.2 → 0.114】</summary>
    const float TabSlideDur = 0.114f;
    /// <summary>相邻标签的出场错峰间隔（秒）。【2026-10-24 主人拍板：1 秒 → 0.5 → 0.25（两轮快一倍）；同日追加「也快50%」：0.25 ÷ 1.5 → 0.167；2026-10-25 主人拍板「间隔再短点」：0.167 → 0.10】</summary>
    const float TabPopStagger = 0.10f;
    /// <summary>滑入起始位置在屏幕左外侧的额外余量（像素），保证完全藏在屏外。</summary>
    const float TabSlideOutMargin = 80f;
    /// <summary>右侧页面弹出起始缩放（0.92 → 1 轻微放大，不夸张）。</summary>
    const float ModePopFromScale = 0.92f;
    /// <summary>右侧页面弹出时长（秒）。</summary>
    const float ModePopDur = 0.20f;

    /// <summary>左侧标签滑入的并行子协程，RestorePopAnims 统一停。</summary>
    readonly List<Coroutine> _tabSubCos = new List<Coroutine>();
    Coroutine _modePopCo;
    /// <summary>动效基准值缓存（key = 被动画的节点；首次动画前抓原值，还原用）。</summary>
    readonly Dictionary<Transform, Vector3> _popBaseScales = new Dictionary<Transform, Vector3>();
    /// <summary>左侧标签滑入动效的基准位置缓存（首次动画前抓适配后的 anchoredPosition，还原用）。
    /// 【2026-10-24 主人拍板】</summary>
    readonly Dictionary<Transform, Vector2> _popBaseAnchoredPos = new Dictionary<Transform, Vector2>();
    /// <summary>动效挂的 CanvasGroup（还原 alpha 用）。</summary>
    readonly Dictionary<Transform, CanvasGroup> _popGroups = new Dictionary<Transform, CanvasGroup>();

    // ────────────────────────────────────────────────────
    // 入口
    // ────────────────────────────────────────────────────

    /// <summary>滑入因适配延后补跑而挂起（CoFitWhenRootReady 补跑完适配后由 FlushLeftTabsPopPending 补放）。</summary>
    bool _leftTabsPopPending;

    /// <summary>左侧标签逐颗从屏幕左外侧滑入（ShowPage 调用；隐藏的入口按钮跳过）。
    /// 错峰语义：相邻标签开始滑入的时刻相差 TabPopStagger（并行错峰），非串行等待——每颗标签独立协程自带延迟。</summary>
    void PlayLeftTabsPopIn()
    {
        if (!gameObject.activeInHierarchy || modeButtons == null) return;
        // 【2026-10-26 修「打完关卡出来左侧标签拉开间隔」】战斗回城那帧 rect=0，适配走 CoFitWhenRootReady 延后补跑；
        // 此刻标签还在预制体位（隐藏钮占槽、可见钮间距 190），若现在起滑会把这个错位钉死整个动画期，
        // 且错基准存进 _popBaseAnchoredPos 后本实例每次进页都被 RestorePopAnims 拽回去 → 一直散开。
        // → 挂起，等适配真正落位后补放。
        if (_adventureFitWaitingRoot)
        {
            _leftTabsPopPending = true;
            return;
        }
        _leftTabsPopPending = false;
        RestorePopAnims();   // 先把上次可能停在半程的缩放/透明度复位，再从头弹
        int slot = 0;
        for (int i = 0; i < modeButtons.Length; i++)
        {
            var btn = modeButtons[i];
            if (btn == null || !btn.gameObject.activeSelf) continue;
            RefreshPopBasePos(btn.transform);   // 基准每次播放现抓当前落位，防缓存钉死旧值
            _tabSubCos.Add(StartCoroutine(CoPopOnce(btn.transform, slot * TabPopStagger)));
            slot++;
        }
    }

    /// <summary>适配补跑完成后补放挂起的滑入（仅 CoFitWhenRootReady 调用；页面已隐藏则协程已死，不会进来）。</summary>
    void FlushLeftTabsPopPending()
    {
        if (!_leftTabsPopPending) return;
        _leftTabsPopPending = false;
        PlayLeftTabsPopIn();
    }

    /// <summary>滑入基准缓存刷新为当前 anchoredPosition（须在 RestorePopAnims 之后、起协程之前调）。</summary>
    void RefreshPopBasePos(Transform t)
    {
        if (t is RectTransform rt) _popBaseAnchoredPos[t] = rt.anchoredPosition;
    }

    /// <summary>右侧当前模式页面弹出（OnSelectMode 调用，RefreshAll 把节点显出来之后）。</summary>
    void PlayRightModePopIn()
    {
        if (!gameObject.activeInHierarchy) return;
        string nodeName = RightNodeNameOfMode(_selectedMode);
        if (string.IsNullOrEmpty(nodeName)) return;
        var node = transform.Find("RightContent/" + nodeName);
        if (node == null) return;
        if (_modePopCo != null) StopCoroutine(_modePopCo);
        _modePopCo = StartCoroutine(CoNodePopIn(node));
    }

    /// <summary>模式 → 右侧页面节点名（与 RefreshMapLayers 的显隐口径同源）。</summary>
    string RightNodeNameOfMode(int mode)
    {
        if (IsMainMode(mode)) return MainNodeName;
        if (IsEndlessMode(mode)) return EndlessNodeName;
        if (IsActivityMode(mode)) return ActivityNodeName;
        return null;
    }

    /// <summary>停协程并还原所有被动画节点的缩放/透明度（动画开头与 OnDisable 都走这里）。</summary>
    void RestorePopAnims()
    {
        // 必须显式停掉并行子协程：页面隐藏时 OnDisable 会调这里，若不 Stop，
        // 重新进页后残协程会继续写 anchoredPosition 造成错乱
        for (int i = 0; i < _tabSubCos.Count; i++)
        {
            if (_tabSubCos[i] != null) StopCoroutine(_tabSubCos[i]);
        }
        _tabSubCos.Clear();
        if (_modePopCo != null) { StopCoroutine(_modePopCo); _modePopCo = null; }
        foreach (var kv in _popBaseScales)
        {
            if (kv.Key == null) continue;
            kv.Key.localScale = kv.Value;
        }
        // 【2026-10-24 主人拍板】滑入动效半途被打断时把标签归位，防止永久停在屏幕外
        foreach (var kv in _popBaseAnchoredPos)
        {
            if (kv.Key == null) continue;
            if (kv.Key is RectTransform rt) rt.anchoredPosition = kv.Value;
        }
        foreach (var kv in _popGroups)
        {
            if (kv.Value == null) continue;
            kv.Value.alpha = 1f;
        }
    }

    // ────────────────────────────────────────────────────
    // 协程
    // ────────────────────────────────────────────────────

    /// <summary>单节点滑入：【2026-10-24 主人拍板】延迟期间先摆在屏幕左外侧 + 透明（避免闪现），
    /// 之后 x 从屏外滑到基准位（ease-out-cubic，不过冲防撞出右边界），alpha 在滑行前 40% 内淡入。</summary>
    IEnumerator CoPopOnce(Transform t, float delay)
    {
        var rt = (RectTransform)t;
        Vector2 basePos = GetPopBasePos(t);
        var cg = GetPopGroup(t);
        // 标签是父级横向拉伸（sizeDelta.x = 0），屏外偏移按实际宽度现算；宽度拿不到时回落常量 LEFT_W
        float tabW = rt.rect.width > 0f ? rt.rect.width : LEFT_W;
        Vector2 fromPos = basePos + new Vector2(-(tabW + TabSlideOutMargin), 0f);
        rt.anchoredPosition = fromPos;
        cg.alpha = 0f;
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        float time = 0f;
        while (time < TabSlideDur)
        {
            time += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(time / TabSlideDur);
            float ease = 1f - (1f - u) * (1f - u) * (1f - u);   // ease-out-cubic
            rt.anchoredPosition = new Vector2(Mathf.Lerp(fromPos.x, basePos.x, ease), basePos.y);
            cg.alpha = Mathf.Clamp01(u / 0.4f);   // 前 40% 淡入，「从无到有」
            yield return null;
        }
        rt.anchoredPosition = basePos;
        cg.alpha = 1f;
    }

    /// <summary>右侧页面弹出：缩放 0.92→1（轻回弹）+ 透明度 0→1，快速不拖节奏。</summary>
    IEnumerator CoNodePopIn(Transform node)
    {
        Vector3 baseScale = GetPopBaseScale(node);
        var cg = GetPopGroup(node);
        float time = 0f;
        while (time < ModePopDur)
        {
            time += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(time / ModePopDur);
            node.localScale = baseScale * Mathf.Lerp(ModePopFromScale, 1f, BackOut(u));
            cg.alpha = u;
            yield return null;
        }
        node.localScale = baseScale;
        cg.alpha = 1f;
        _modePopCo = null;
    }

    // ────────────────────────────────────────────────────
    // 工具
    // ────────────────────────────────────────────────────

    Vector3 GetPopBaseScale(Transform t)
    {
        if (!_popBaseScales.TryGetValue(t, out var baseScale))
        {
            baseScale = t.localScale;
            _popBaseScales[t] = baseScale;
        }
        return baseScale;
    }

    /// <summary>滑入动效的基准 anchoredPosition（懒抓，模式同 GetPopBaseScale；ShowPage 时适配已做完，抓到的就是正确值）。</summary>
    Vector2 GetPopBasePos(Transform t)
    {
        if (!_popBaseAnchoredPos.TryGetValue(t, out var basePos))
        {
            basePos = ((RectTransform)t).anchoredPosition;
            _popBaseAnchoredPos[t] = basePos;
        }
        return basePos;
    }

    CanvasGroup GetPopGroup(Transform t)
    {
        if (!_popGroups.TryGetValue(t, out var cg) || cg == null)
        {
            cg = t.GetComponent<CanvasGroup>();
            if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
            cg.interactable = true;      // 只借 alpha，不动交互（锁定态由 RightContent 根的 CanvasGroup 管）
            cg.blocksRaycasts = true;
            _popGroups[t] = cg;
        }
        return cg;
    }

    /// <summary>回弹缓动（ease-out-back）：0→1，中段越过 1 到 ~1.1 再回来，「弹出」手感。</summary>
    static float BackOut(float u)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = u - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
