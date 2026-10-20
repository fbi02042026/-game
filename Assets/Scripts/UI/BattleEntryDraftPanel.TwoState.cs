using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 进关抽奖面板 partial —— 2026-10-18 由 BattleEntryDraftPanel.cs 纯位移拆出（无逻辑改动）
// 职责：两态动画与诊断（SetExpanded/ApplyState、map 与世界层位移、SetPivotLocalY、FindDeep/LogState）
public partial class BattleEntryDraftPanel
{
    /// <summary>抽奖按钮容器 + 「继续」按钮的显隐。开战后彻底隐藏，战斗画面里不留抽奖入口。</summary>
    void SetDraftVisible(bool on)
    {
        if (_draftRoot != null) _draftRoot.gameObject.SetActive(on);
        if (_continueBtn != null) _continueBtn.gameObject.SetActive(on);
    }

    /// <summary>
    /// 600 ↔ 750 整套两态切换 —— 本类唯一的展开/收起出口，只改 <c>_animK</c> 目标值，
    /// 所有节点的数值都由 ApplyState 按同一条进度插值写出来，不会出现某个节点漏掉不同步。
    /// </summary>
    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        if (_panel == null) return;
        // ⚠ 遮罩 zhezhao 不在这里动 —— 它归 BattleUI.TickBattleMask() 独家管（每帧按 IsLotteryOpen 判）。
        // 两边都改过一次，结果就是遮罩把抽奖按钮/装备/背包一起压掉（2026-10-05 根因）。
        LogMapBaseOnce();
        float to = expanded ? 1f : 0f;
        if (_animCo != null) StopCoroutine(_animCo);
        // 【2026-10-20 玩家反馈修复】宿主父链未激活时 StartCoroutine 会直接抛异常
        //（"game object is inactive"），把调用方协程一起带死。这里不走动画、
        // 直接 ApplyState 同步落位：两态数值一步到位，面板照样出来/收起，只是没有过渡。
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[BattleEntryDraftPanel] 两态切换时宿主未激活，跳过动画直接落位");
            _animCo = null;
            ApplyState(to);
            return;
        }
        _animCo = StartCoroutine(CoAnimState(to));
    }

    IEnumerator CoAnimState(float to)
    {
        float from = _animK;
        if (Mathf.Approximately(from, to))
        {
            ApplyState(to);
            yield break;
        }
        float t = 0f;
        while (t < AnimSec)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / AnimSec);
            // 整套两态只走<b>一条</b>进度：所有节点同一个 k，绝不出现谁快谁慢、谁先到位。
            ApplyState(Mathf.Lerp(from, to, EaseOut(p)));
            yield return null;
        }
        ApplyState(to);
        // 2026-10-05 主人报「zhuangshi 动对了，抽奖按钮没移到上面来」：
        // 数值层面 draftY 一直是按真值表写下去的（Begin 日志可证），所以只能落在
        // 「看不见」上 —— 要不没激活、要不落在屏幕外、要不动画结束后被别处拽回去。
        // 这里在动画跑完后再量一次真实屏幕位置，一次把三种可能分开。
        LogState(to >= 1f ? "展开完成" : "收回完成");
        if (to >= 1f) StartCoroutine(CoVerifyDraftOnScreen());
    }

    /// <summary>展开动画结束后 0.6s 再量一次，看抽奖容器有没有被别处拽回原位。</summary>
    IEnumerator CoVerifyDraftOnScreen()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        LogState("展开后0.6s");
        LogDraftLocalRect();
    }

    /// <summary>
    /// 在<b>面板本地坐标</b>里量抽奖容器的落点。
    ///
    /// <para>⚠ 2026-10-05 教训：上一版量的是「世界/屏幕 Y」，但战斗画布不是 Overlay，
    /// 世界单位跟像素不是一个量纲（面板 750 量出来只有 6 个单位），数字根本没法比。
    /// 这里一律换算到面板本地——「离面板底边多少逻辑像素」，跟主人在团结里看到的是同一把尺子。</para>
    /// </summary>
    void LogDraftLocalRect()
    {
        if (_draftRt == null || _panel == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 抽奖容器/面板缺失，无法量落点");
            return;
        }
        // 先强制布局算完，否则量到的是上一帧的旧矩形
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        Rect pr = _panel.rect;

        // DraftRoot 中心换算到面板本地
        float centerY = _panel.InverseTransformPoint(_draftRt.position).y - pr.yMin;

        // 首个按钮的图标：上下边也换算到面板本地
        float iconLo = -1f, iconHi = -1f;
        bool hasIcon = false;
        var icon = _slots.Count > 0 && _slots[0].Root != null ? FindDeep(_slots[0].Root, "icon") : null;
        var ic = icon as RectTransform;
        if (ic != null)
        {
            var w = new Vector3[4];
            ic.GetWorldCorners(w);
            iconLo = Mathf.Min(_panel.InverseTransformPoint(w[0]).y, _panel.InverseTransformPoint(w[1]).y) - pr.yMin;
            iconHi = Mathf.Max(_panel.InverseTransformPoint(w[0]).y, _panel.InverseTransformPoint(w[1]).y) - pr.yMin;
            hasIcon = true;
        }

        // 展开时容器中心应落在「0.5×面板高 + 主人的 PosY」：面板 750、PosY -250 → 本地 125。
        float target = pr.height * DraftAnchorY + DraftExpandedPosY;
        bool landedRight = Mathf.Abs(centerY - target) < 12f;

        Debug.Log(        $"[BattleEntryDraftPanel] 落点实测 面板h={pr.height:F0}({pr.yMin:F0}~{pr.yMax:F0})" +
                  $" 面板锚点y={_panel.anchorMin.y:F2} | DraftRoot 实测中心={centerY:F0}" +
                  $" 目标={target:F0}（=0.5×{pr.height:F0}+({DraftExpandedPosY:F0})）" +
                  (landedRight ? " ✓正确" : " ✗不对") +
                  $"（本地坐标，离面板底边） 当前PosY={_draftRt.anchoredPosition.y:F0}" +
                  $" amin.y={_draftRt.anchorMin.y:F2} pivot.y={_draftRt.pivot.y:F2}" +
                  (hasIcon ? $" 图标 {iconLo:F0}~{iconHi:F0}" : "（图标缺失）") +
                  $" | 遮罩={(IsLotteryOpen ? "关" : "开(会压住)")}");

        // fail closed：抽奖按钮没落在面板里就是看不见，必须报出来，不许静默过去。
        float lo = hasIcon ? iconLo : centerY;
        float hi = hasIcon ? iconHi : centerY;
        if (hi <= 0f || lo >= pr.height)
        {
            Debug.LogError($"[BattleEntryDraftPanel] 抽奖按钮落在面板外面（看不见）：" +
                           $"图标 {lo:F0}~{hi:F0}，面板 0~{pr.height:F0}，" +
                           $"DraftRoot 中心={centerY:F0}（应在 {target:F0}），" +
                           $"amin.y={_draftRt.anchorMin.y:F2}。" +
                           $"落点=0.5×面板高+({DraftExpandedPosY:F0})，若仍越界说明面板/容器的锚点或高度" +
                           $"被别处改过 —— 报给主人，不要私自加补偿。");
        }
    }

    /// <summary>
    /// 按两态真值表插值写值。k=0 完全是收起态、k=1 完全是展开态。
    /// ⚠ 值全部来自上面那组主人拍板常量 / map 的运行时原值，不许在这里做任何推算。
    /// </summary>
    void ApplyState(float k)
    {
        _animK = k;
        float panelH = Mathf.Lerp(HeightCollapsed, HeightExpanded, k);
        if (_panel != null)
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, panelH);

        // 内容整体上移：zhuangshi 是位移真源，GridContainer 加<b>同一个位移量</b>跟着走。
        float contentY = Mathf.Lerp(ContentCollapsedY, ContentExpandedY, k);
        SetY(_contentRt, contentY);
        float delta = contentY - ContentCollapsedY;
        for (int i = 0; i < _followers.Count; i++)
        {
            var f = _followers[i];
            if (f != null && f.Rt != null) SetY(f.Rt, f.BaseY + delta);
        }

        // 抽奖容器：主人给的是 Inspector 的 Pos Y（锚点 0.5 语义），
        // 先换算成「父级本地 Y = 0.5×面板高 + PosY」，再按<b>当时真实的锚点</b>反算回 Pos Y 写下去。
        float draftPosY = Mathf.Lerp(DraftCollapsedPosY, DraftExpandedPosY, k);
        SetPivotLocalY(_draftRt, panelH * DraftAnchorY + draftPosY, panelH);

        // map：中心 Y 250 → 350（主人给的绝对值），缩放 1.13 → 1。
        // 写的是「Pos Y / 中心 Y」这把尺子，不是「离父级底边多少」—— 换算错尺子会把 map 顶飞。
        ApplyMapState(k);

        // 【2026-10-06 主人拍板】map 上移时，<b>战斗场景里的玩家</b>（世界单位）要跟着一起上移：
        // 主人原话「map 上移时 玩家没有跟着移动上去」—— 背景上去了、人还在原地，看着像人掉下去了。
        ApplyWorldLift(k);
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】map 上移 100 逻辑像素时，世界层要抬多少（世界单位）。
    /// <para>两把尺子不一样：map 走 UI 逻辑像素（250→350 = +100），世界层是 Unity 单位。
    /// 按「竖屏可视高 ≈ 1280 逻辑像素 = 2 × orthographicSize(5.4) ≈ 10.8 世界单位」折算，
    /// 100 像素 ≈ 0.85 世界单位。<b>主人在团结里看着差一点，只改这一个数。</b></para>
    /// </summary>
    const float WorldLiftY = 0.85f;

    /// <summary>
    /// 世界层跟随位移：只挪 <c>unitRoot</c> 这一层（玩家/佣兵都是它的子节点，会整体跟着走），
    /// 基准恒取 <c>UnitBase.GROUND_Y</c>（主人给的站立线），<b>不缓存</b>——
    /// 二次进战斗 unitRoot 重建也不会算错。
    /// </summary>
    void ApplyWorldLift(float k)
    {
        var bm = BattleManager.Instance;
        var root = bm != null ? bm.unitRoot : null;
        if (root == null) return;
        var p = root.position;
        p.y = UnitBase.GROUND_Y + Mathf.Lerp(0f, WorldLiftY, k);
        root.position = p;
    }

    static void SetY(RectTransform rt, float y)
    {
        if (rt == null) return;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    /// <summary>
    /// 把节点 rt 的 <b>pivot</b> 摆到「<c>rt.parent</c> 的 rect 空间」的 <c>localY</c>（底边为 0）。
    /// <para>· 点锚点（<c>anchorMin.y == anchorMax.y</c>）：
    ///   <c>anchorRef = parent.rect.yMin + anchorMin.y × 父高</c>，pivot 本地 Y = <c>anchorRef + apos.y</c>
    ///   ⇒ <b><c>apos.y = localY - anchorRef</c></b>。
    ///   pivot 已经在 apos 的定义里了，<b>绝不能再加减 <c>pivot.y × 高</c></b> —— 2026-10-05 就是
    ///   多算了这一项，把 -250 写成 -200，抽奖按钮直接掉到面板外面（主人报「一个也没改」）。</para>
    /// <para>· 竖向拉伸锚点（<c>anchorMin.y != anchorMax.y</c>）：<c>anchoredPosition.y</c> 无效，
    ///   改成按差值整体推 <c>offsetMin/offsetMax</c>。</para>
    /// <remarks>⚠ 尺子恒为「节点自己的父级」。竖屏适配会把锚点重锚成贴底（实测 amin.y 0.5→0），
    /// 同一个 Pos Y 的含义就变了，所以只能先定本地落点、再按当时锚点反算。</remarks>
    /// </summary>
    static void SetPivotLocalY(RectTransform rt, float localY, float parentH = -1f)
    {
        if (rt == null) return;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        if (!Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y))
        {
            // 拉伸锚点：整体平移差值（offsetMin/offsetMax 一起推，矩形才不会被拉变形）
            float d = localY - PivotLocalY(rt);
            rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y + d);
            rt.offsetMax = new Vector2(rt.offsetMax.x, rt.offsetMax.y + d);
            return;
        }

        float h = parentH > 0f ? parentH : parent.rect.height;
        float anchorRef = parent.rect.yMin + rt.anchorMin.y * h;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, localY - anchorRef);
    }

    /// <summary>rt 的 pivot 在「它自己父级 rect 空间」里的 Y（底边为 0），与 <see cref="SetPivotLocalY"/> 同一把尺子。</summary>
    static float PivotLocalY(RectTransform rt)
    {
        if (rt == null) return 0f;
        var parent = rt.parent as RectTransform;
        if (parent == null) return 0f;
        return parent.InverseTransformPoint(rt.position).y - parent.rect.yMin;
    }

    /// <summary>ease-out：起步快、末端平滑收住。</summary>
    static float EaseOut(float x)
    {
        float u = 1f - Mathf.Clamp01(x);
        return 1f - u * u;
    }

    static string Name(DraftCategory? c)
    {
        if (!c.HasValue) return "普通";
        switch (c.Value)
        {
            case DraftCategory.Merc: return "佣兵";
            case DraftCategory.Equip: return "装备";
            case DraftCategory.Skill: return "技能";
            default: return "普通";
        }
    }

    // ============================================================
    // 小工具
    // ============================================================

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        var direct = root.Find(name);
        if (direct != null) return direct;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>
    /// 2026-10-04 主人拉的两态数值（改代码前先对着这组值核一遍）：
    ///   收起：面板 600 / zhuangshi 0 / DraftRoot PosY -400（本地 -100，藏在面板下）/ map 中心Y 250、scale 1.13
    ///   展开：面板 750 / zhuangshi 80 / DraftRoot PosY -250（本地 125）/ map 中心Y 350、scale 1
    ///   （map 的中心 Y = Inspector 的 Pos Y；拉伸锚点下看日志的 <c>mapPosY</c>，
    ///    <c>mapBottom / mapTop</c> 是离父级底/顶的距离，是另一把尺子，别拿来对 250/350）
    ///   （DraftRoot 的 -400 / -250 是 <b>Inspector 的 Pos Y</b>，锚点 0.5 —— 见常量处注释）
    /// 遮罩 zhezhao 不在本表内 —— 它归 BattleUI.TickBattleMask() 管（展开时由本类的
    /// <see cref="IsLotteryOpen"/> 通知它让开）。
    /// </summary>
    void LogState(string tag)
    {
        float draftLocal = _draftRt != null ? PivotLocalY(_draftRt) : -9999f;
        Debug.Log($"[BattleEntryDraftPanel] {tag} k={_animK:F2}" +
                  $" panelH={(_panel != null ? _panel.sizeDelta.y : -1f)}" +
                  $" contentY={(_contentRt != null ? _contentRt.anchoredPosition.y : -9999f)}" +
                  $" draftPosY={(_draftRt != null ? _draftRt.anchoredPosition.y : -9999f)}" +
                  $" draft本地Y={draftLocal:F0}" +
                  $" draftActive={(_draftRoot != null && _draftRoot.gameObject.activeSelf)}" +
                  $" mapBottom={(_mapRt != null ? _mapRt.offsetMin.y : -9999f)}" +
                  $" mapTop={(_mapRt != null ? -_mapRt.offsetMax.y : -9999f)}" +
                  $" mapPosY={(_mapRt != null ? _mapRt.anchoredPosition.y : -9999f)}" +
                  $" mapScale={(_mapRt != null ? _mapRt.localScale.x : -1f):F3}");
    }
}
