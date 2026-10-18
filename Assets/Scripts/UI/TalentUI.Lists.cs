using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 天赋界面 partial —— 2026-10-18 由 TalentUI.cs 纯位移拆出（无逻辑改动）
// 职责：列表构建（EnsureLists、滚动布局、模板行高烘焙、锁图标补建）
public partial class TalentUI
{

    void EnsureLists()
    {
        if (_listsBuilt) return;
        if (leftContent == null || rightContent == null)
            AutoBindFromHierarchy();
        if (leftNodeTemplate == null || rightRowTemplate == null)
        {
            Debug.LogWarning("[TalentUI] 缺少节点模板，请重新生成预制体");
            return;
        }

        if (rightExtraRowTemplate == null)
        {
            var alt = transform.Find("RightRowTemplate (1)");
            if (alt != null) rightExtraRowTemplate = alt.gameObject;
        }

        leftNodeTemplate.SetActive(false);
        rightRowTemplate.SetActive(false);
        if (rightExtraRowTemplate != null) rightExtraRowTemplate.SetActive(false);

        // 列表只用 LayoutGroup 排，禁止手写改每个框的坐标/宽高
        EnsureScrollContentLayout(leftContent, 4f);    // 【2026-10-14 主人拍板】左列天赋条缩小，行距 8 → 4
        EnsureScrollContentLayout(rightContent, 8f);   // 右列不动

        for (int i = 0; i < TalentDefs.Left.Length; i++)
        {
            var go = Instantiate(leftNodeTemplate, leftContent);
            go.name = "LeftNode_" + (i + 1);
            go.SetActive(true);
            PrepareListItemUnderContent(go.transform as RectTransform, leftContent, LeftNodeW, LeftNodeH);
            var view = BindLeftNode(go, i);
            _leftViews.Add(view);
            int idx = i;
            view.button?.onClick.AddListener(() => OnClickLeft(idx));
        }

        // 【2026-10-18 主人拍板】右列互斥对（流派·物理专精 R_F1 + 流派·魔法专精 R_F2）合并成
        // **一行双选项**：二选一在同一个节点里，不再上下铺两行。判定规则：Mutex 且与下一节点
        // 同互斥组、同门槛 → 合一行（node=前者，node2=后者），跳过后者的独立行。
        // 数据模型与存档不动（仍是 R_F1/R_F2 两个节点），只合 UI 表现。
        for (int i = 0; i < TalentDefs.RightNodes.Length; )
        {
            var node = TalentDefs.RightNodes[i];
            var pair2 = i + 1 < TalentDefs.RightNodes.Length ? TalentDefs.RightNodes[i + 1] : null;
            bool merged = pair2 != null
                && node.type == TalentDefs.RightNodeType.Mutex
                && pair2.type == TalentDefs.RightNodeType.Mutex
                && !string.IsNullOrEmpty(node.mutexGroup)
                && node.mutexGroup == pair2.mutexGroup
                && node.unlockLeftIndex == pair2.unlockLeftIndex;

            var go = Instantiate(rightRowTemplate, rightContent);
            go.name = merged ? "RightRow_" + (i + 1) + "_" + node.id + "_" + pair2.id : "RightRow_" + (i + 1);
            go.SetActive(true);
            PrepareListItemUnderContent(go.transform as RectTransform, rightContent, RightRowW, RightRowH);
            // visualIndex 按视图序号计（合并行让「节点序号=行号」不再一一对应，连线判断用的是视图序）
            var view = BindRightNode(go, _rightViews.Count, node);
            if (merged)
            {
                view.node2 = pair2;
                BindMutexPairRow(view, go, node, pair2);
                i += 2;
            }
            else
            {
                i += 1;
            }
            _rightViews.Add(view);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(leftContent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rightContent);

        // 以预制体表现为准：左列模板的序号/图标按预制体探出卡片左界，让出同量空间防被视口裁
        FitLeftContentToPrefabOverhang(leftContent, _leftViews);

        WireScroll(leftScroll, leftContent);
        WireScroll(rightScroll, rightContent);
        // 左右栏不再同步（2026-10-13 主人拍板「分开动」）—— 各自独立滚动，别再加回同步。

        _listsBuilt = true;
        WireClicks();
    }

    /// <summary>Content 用 VerticalLayoutGroup 排布；不拉伸子项宽高。spacing 由调用方按列给（左右列可不同）。</summary>
    static void EnsureScrollContentLayout(RectTransform content, float spacing)
    {
        if (content == null) return;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 4, 4);
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.reverseArrangement = true; // 与原先「底端先解锁」一致：L1 在视觉底部

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// 【2026-10-16 主人拍板「以预制体的表现为准」】左列模板按预制体排版，序号(Number)与
    /// 图标(Iconbg)探出卡片左界（预制体实测 anchoredPosition.x≈-50.2 / -36）。原先整列
    /// UpperCenter 居中，这截探出会被 ScrollRect 视口的 RectMask2D 裁掉——表现就是
    /// 「L1 L2 只露出来一点」。这里按预制体序列化数据（anchors/anchoredPosition/pivot/
    /// sizeDelta）实测左探出量，给 leftContent 加同量左内边距并改左对齐：整行左缘顶到视口
    /// 左缘，与预制体表现一致（不缩放、不裁切）。探出量随预制体改动自动适配，改模板不用改代码。
    /// 纯数据推算、不读 rect/世界坐标，隐藏态（Prewarm 预热实例）也算得准。
    /// </summary>
    static void FitLeftContentToPrefabOverhang(RectTransform content, List<LeftNodeView> views)
    {
        if (content == null || views == null || views.Count == 0) return;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) return;

        var chain = new List<RectTransform>();
        float overhang = 0f;
        for (int i = 0; i < views.Count; i++)
        {
            var cardGo = views[i] != null ? views[i].root : null;
            var card = cardGo != null ? cardGo.transform as RectTransform : null;
            if (card == null) continue;
            float cardW = card.sizeDelta.x;   // PrepareListItemUnderContent 已烘焙的设计宽
            if (cardW <= 0f) continue;
            var kids = card.GetComponentsInChildren<RectTransform>(true);
            for (int k = 0; k < kids.Length; k++)
            {
                var rt = kids[k];
                if (rt == null || rt == card) continue;
                // 收集 card → rt 的链（rt 自己收尾）
                chain.Clear();
                for (var p = rt; p != null && p != card; p = p.parent as RectTransform)
                    chain.Add(p);
                if (chain.Count == 0) continue;
                chain.Reverse();   // 变成 card 的子 → … → rt
                float x = 0f;      // 当前节点本地原点(pivot)在 card 本地的 x
                float parentW = cardW;
                float minLeft = 0f;
                for (int c = 0; c < chain.Count; c++)
                {
                    var node = chain[c];
                    x += node.anchorMin.x * parentW + node.anchoredPosition.x;
                    float w = (node.anchorMax.x - node.anchorMin.x) * parentW + node.sizeDelta.x;
                    float left = x - node.pivot.x * w;   // 本节点左缘在 card 本地的 x
                    if (c == 0) minLeft = left;
                    else if (left < minLeft) minLeft = left;
                    parentW = w;
                }
                if (-minLeft > overhang) overhang = -minLeft;
            }
        }
        if (overhang <= 0.5f) return;   // 模板没探出：保持原居中排版，不动
        vlg.childAlignment = TextAnchor.UpperLeft;
        int padLeft = Mathf.CeilToInt(overhang);
        var pad = vlg.padding;
        if (pad.left != padLeft)
        {
            vlg.padding = new RectOffset(padLeft, pad.right, pad.top, pad.bottom);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);   // 当场落位，别等下一帧画布重建
        }
    }

    /// <summary>
    /// 模板原挂在根下（大负边距 stretch）。进 Content 后只烘焙为固定高宽，
    /// 不改子节点；宽锁模板设计宽，高用调用方的行高常量。
    /// </summary>
    static void PrepareListItemUnderContent(RectTransform item, RectTransform content, float defaultW, float defaultH)
    {
        if (item == null) return;
        // 模板是 stretch 时 sizeDelta.y 是负边距不是高度，abs 会算出几百的假高度；只有正值才可信
        float h = item.sizeDelta.y > 8f ? item.sizeDelta.y : defaultH;
        float w = defaultW;
        item.anchorMin = item.anchorMax = new Vector2(0.5f, 1f);
        item.pivot = new Vector2(0.5f, 1f);
        item.sizeDelta = new Vector2(w, h);
        item.anchoredPosition = Vector2.zero;
        item.localScale = Vector3.one;
    }

    static void WireScroll(ScrollRect scroll, RectTransform content)
    {
        if (scroll == null || content == null) return;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.content = content;
        if (scroll.viewport == null)
        {
            var vp = content.parent as RectTransform;
            if (vp != null) scroll.viewport = vp;
        }
        // 内容比视口高才能滑；自下而上时滚到底，最先解锁的在视野底部
        scroll.verticalNormalizedPosition = 0f;
    }

    // 【2026-10-13 主人拍板】「这个天赋左侧和右侧分开动吧 别一起动了」→ 删除左右栏滚动同步：
    // 原 WireScrollSync / OnLeftScroll / OnRightScroll / _scrollSyncing（把两栏位置互相镜像）整条删掉，
    // 两栏各滑各的。各自起始位置仍由 WireScroll 设成 0（滚到底 = 最先解锁的在视野底部）。
    // ⚠ 别再为了「对齐」把两栏重新锁在一起。

    /// <summary>右列缺 Lock 时按手做模板补一个右下角锁（不改已有坐标）。</summary>
    void EnsureRightLock(RightNodeView v)
    {
        if (v == null || v.root == null) return;
        if (v.lockIcon != null) return;
        var existing = v.root.transform.Find("Lock");
        if (existing != null)
        {
            v.lockIcon = existing.GetComponent<Image>();
            if (v.lockIcon != null) return;
        }

        var go = new GameObject("Lock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(v.root.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(4.4f, -14.2f);
        rt.sizeDelta = new Vector2(36f, 55f);
        rt.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        v.lockIcon = go.GetComponent<Image>();
        v.lockIcon.raycastTarget = false;
        v.lockIcon.preserveAspect = true;
        var lockSp = sprLock ?? Resources.Load<Sprite>("UI/Common/锁");
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            if (lockSp == null)
                lockSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Common/锁.png");
        }
#endif
        if (lockSp != null)
            v.lockIcon.sprite = lockSp;
        go.SetActive(false);
    }
}
