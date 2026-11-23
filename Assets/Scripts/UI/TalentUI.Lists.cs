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
        // 【2026-11-23】职业变了（钻石换职业）→ 右列视图已换成另一套表，整列必须重建。
        if (_listsBuilt && _listsBuiltJobId != (int)PlayerJobDefs.GetSelected())
            RebuildListsForJobChange();
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
        // 【2026-11-19 三列网格】左列改 GridLayoutGroup 固定 3 列（原单列 VerticalLayoutGroup）；右列不动。
        EnsureGridContentLayout(leftContent, LeftGridColumns, LeftNodeW, LeftNodeH, LeftGapX, LeftGapY);   // 【2026-11-19 主人拍板】列距 8 不变，行距 36→54（再大 50%）
        EnsureScrollContentLayout(rightContent, 8f);   // 右列先建骨架，下方按左列总高覆盖 spacing

        // 【2026-11-20 主人拍板】开启/点亮顺序按连线走蛇形：左→右、右→左、左→右……
        // 数据 index = 解锁顺序，必须落在连线的第 i 个视觉槽位；GridLayoutGroup 的 child 顺序是
        // 视觉 row-major（每行左→右、自下而上），奇数行左右镜像。只改视觉映射，存档/解锁/等级门槛一律不动。
        for (int k = 0; k < TalentDefs.Left.Length; k++) _leftViews.Add(null);
        for (int slot = 0; slot < TalentDefs.Left.Length; slot++)
        {
            int row = slot / TalentDefs.LeftPerRow;
            int vcol = slot % TalentDefs.LeftPerRow;
            int i = row * TalentDefs.LeftPerRow
                    + (row % 2 == 0 ? vcol : (TalentDefs.LeftPerRow - 1 - vcol));
            var go = Instantiate(leftNodeTemplate, leftContent);
            go.name = "LeftNode_" + (i + 1);
            go.SetActive(true);
            PrepareListItemUnderContent(go.transform as RectTransform, leftContent, LeftNodeW, LeftNodeH);
            var view = BindLeftNode(go, i);
            _leftViews[i] = view;
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
            // 【2026-11-19 主人拍板】界面隐藏 R_F1/R_F2（流派二选一）：不生成行，右列拉成一条直线。
            // 数据/存档保留；两个 Mutex 节点都不生成，合并行逻辑自然不再触发。
            if (TalentDefs.IsHiddenRightNode(node.id)) { i += 1; continue; }
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
        // 【2026-11-21 主人拍板】右列图标对准左列从上数第 1/4/7 行（隔两行一个）：
        // 行心距 = 左列「一格高 + 一行距」× 3（原 ×2 是第 1/3/5 行，本次按主人要求改为隔两行）。
        // 不再按右列视图数均匀铺满总高（旧算法让图标落在 Lv6/中间/Lv1，与行号对不上）。
        var rightVlg = rightContent.GetComponent<VerticalLayoutGroup>();
        if (_rightViews.Count > 1 && rightVlg != null)
        {
            float tplH = rightRowTemplate.GetComponent<RectTransform>().sizeDelta.y;
            float rowH = tplH > 8f ? tplH : RightRowH;
            rightVlg.spacing = Mathf.Max(4f, (LeftNodeH + LeftGapY) * 3f - rowH);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(rightContent);

        // 读数前先把两列滚到同一基准（normalized=0，L1/R1 在视野底部）——世界 y 差值才有意义。
        // 滚动同步 WireScrollSync 仍在末尾，不动。
        WireScroll(leftScroll, leftContent);
        WireScroll(rightScroll, rightContent);

        // 【2026-11-21 主人拍板】右列底部图标（R1，reverseArrangement 下 _rightViews[0] 在视野底部）
        // 基准仍是实测锚点 _leftViews[1]（Lv2，从底数第 2 个）——只改纵向 y，基准不动：
        // 行距已由「隔 1 行」改为「隔 2 行」（×3），R1 锚在 Lv2 后，上面各行按新行心距自然顺延，
        // 形成第 1/4/7 行各一个图标的节奏（具体落点由行距公式决定，不写死坐标）。
        // 从两边图标的世界 y 现读差值，换算到父节点本地坐标后补进每个右列行的 Opt_0（所有行同模板），不写死手摆坐标。
        if (_leftViews.Count > 1 && _rightViews.Count > 0)
        {
            var lIcon = _leftViews[1].root != null ? _leftViews[1].root.transform.Find("Iconbg") as RectTransform : null;
            var rIcon = _rightViews[0].root != null ? _rightViews[0].root.transform.Find("Opt_0/Icon") as RectTransform : null;
            if (lIcon != null && rIcon != null)
            {
                float dy = lIcon.position.y - rIcon.position.y;
                if (Mathf.Abs(dy) > 0.5f)
                {
                    foreach (var rv in _rightViews)
                    {
                        var opt = rv.root != null ? rv.root.transform.Find("Opt_0") as RectTransform : null;
                        if (opt == null) continue;
                        // 世界 y 差 → 本地坐标增量：除以父链总缩放，画布缩放下才准。
                        float parentScaleY = opt.parent != null ? opt.parent.lossyScale.y : 1f;
                        if (Mathf.Abs(parentScaleY) < 0.001f) parentScaleY = 1f;
                        opt.anchoredPosition = new Vector2(opt.anchoredPosition.x, opt.anchoredPosition.y + dy / parentScaleY);
                    }
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rightContent);
                }
            }
        }
        StretchRightLinkLines();   // 行距/首行对齐落位后重算连线两段，两端仍精确顶卡边

        // 【2026-11-19 三列网格】左探出适配是单列时代产物，Grid 居中排版下跳过（否则会把 grid 强改左对齐）。

        // 【2026-11-19 主人拍板】左右双向滚动同步（覆盖 10-13「分开动」口径）。WireScroll 已提前到读数前。
        WireScrollSync();

        _listsBuilt = true;
        _listsBuiltJobId = (int)PlayerJobDefs.GetSelected();
        WireClicks();
    }

    /// <summary>
    /// 【2026-11-23】换职业后重建左右两列（右列随职业换表；左列与职业无关，但同一套构建流程一起重建最省事）。
    /// 先清视图引用再清节点；子节点**先 SetActive(false) 再 Destroy** —— Destroy 帧末才生效，
    /// 只 Destroy 不隐藏会让同帧的 LayoutGroup 把旧行算进布局，新列表就排歪了。
    /// </summary>
    void RebuildListsForJobChange()
    {
        _leftViews.Clear();
        _rightViews.Clear();
        _fxBatch.Clear();
        _pendingRowIndex = -1;
        _pendingNode = null;
        ClearListChildren(leftContent, leftNodeTemplate);
        ClearListChildren(rightContent, rightRowTemplate, rightExtraRowTemplate);
        _listsBuilt = false;
        Debug.Log($"[TalentUI] 职业已变更为 {PlayerJobDefs.GetSelected()}，重建左右列（右列按新职业取表）");
    }

    /// <summary>清空列表容器里的运行时行，跳过模板节点本身（模板可能是容器的子节点，删了后面就建不出来）。</summary>
    static void ClearListChildren(RectTransform content, params GameObject[] keep)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (child == null) continue;
            bool keepIt = false;
            for (int k = 0; k < keep.Length; k++)
            {
                if (keep[k] == null) continue;
                if (child.gameObject == keep[k] || child.IsChildOf(keep[k].transform)) { keepIt = true; break; }
            }
            if (keepIt) continue;
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// 【2026-11-19 三列网格】左列 Content 用 GridLayoutGroup 固定列数排布（替代单列 VerticalLayoutGroup）。
    /// 节点尺寸取 cell 固定值；若 content 已有 VerticalLayoutGroup 先移除（旧存档/旧预制体残留），避免两套 Layout 冲突。
    /// </summary>
    static void EnsureGridContentLayout(RectTransform content, int columns, float cellW, float cellH, float spacingX, float spacingY)
    {
        if (content == null) return;
        var oldV = content.GetComponent<VerticalLayoutGroup>();
        if (oldV != null) Destroy(oldV);
        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();
        // 【2026-11-19 主人拍板】滚动区上下边界压住首末行内容 → 内容上下各留 24 内边距，
        // 滑到顶/底时首行末行不再贴着视口边缘（4px 那版看着像被切了一半）。
        grid.padding = new RectOffset(0, 0, ContentPadY, ContentPadY);
        grid.spacing = new Vector2(spacingX, spacingY);
        grid.cellSize = new Vector2(cellW, cellH);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.startCorner = GridLayoutGroup.Corner.LowerLeft;   // 【2026-11-19】L1 在最下，向上一行行开
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// 【2026-11-19 主人拍板】右列连线只填「卡与卡之间的空隙」：冲上段从自家卡顶缘拉到上邻卡底缘、
    /// 冲下段从自家卡底缘拉到下邻卡顶缘，两端精确顶到卡边——不留缝，也不再穿到相邻卡片上面
    /// （旧口径「中心距+12 穿卡、靠卡图遮住」跨行遮不住：上行冲下段画在下行卡上面＝线压图标）。
    /// 卡上下缘从 Opt_0 实际 RectTransform 现读（锚左下 / pivot 左下：底缘 = anchoredPosition.y）。
    /// 只改纵向长度与 y 落点；X 中轴仍由 EnsureRightLinkAxis 管，这里不动。
    /// </summary>
    void StretchRightLinkLines()
    {
        var vlg = rightContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) return;
        float tplH = rightRowTemplate.GetComponent<RectTransform>().sizeDelta.y;
        float rowH = tplH > 8f ? tplH : RightRowH;
        float spacing = vlg.spacing;
        foreach (var v in _rightViews)
        {
            var opt = v.root != null ? v.root.transform.Find("Opt_0") as RectTransform : null;
            if (opt == null)
            {
                Debug.LogError("[TalentUI] 右列行缺 Opt_0，连线长度算不出（不兜底）");
                continue;
            }
            float cardBottom = opt.anchoredPosition.y;
            float cardTop = cardBottom + opt.sizeDelta.y * opt.localScale.y;
            float pitch = rowH + spacing;                                 // 相邻两行的行心距
            float len = Mathf.Max(4f, pitch - (cardTop - cardBottom));    // 恰好＝卡与卡的视觉空隙
            StretchRail(v.lineUp, cardTop, len, true);                    // 锚行底：起点=自家卡顶缘
            StretchRail(v.lineDown, cardBottom - rowH * 0.5f, len, false); // 锚行中：起点=自家卡底缘
        }
    }

    /// <summary>连线单段重排：up=true 冲上段（锚行底/pivot 底、起点行中心向上），false 冲下段（锚行中/pivot 顶、起点行中心向下）。</summary>
    static void StretchRail(Image img, float startY, float len, bool up)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, len);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, up ? 0f : 0.5f);
        rt.pivot = new Vector2(0.5f, up ? 0f : 1f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, startY);
    }

    /// <summary>Content 用 VerticalLayoutGroup 排布；不拉伸子项宽高。spacing 由调用方按列给（左右列可不同）。</summary>
    static void EnsureScrollContentLayout(RectTransform content, float spacing)
    {
        if (content == null) return;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, ContentPadY, ContentPadY);
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

    // 【2026-11-19 主人拍板】左右栏双向滚动同步：滑一边另一边跟着动。
    // 【本轮改回】按 verticalNormalizedPosition 同步——左列 30 行、右列 13 行内容高度悬殊，
    // 像素 1:1 必然首尾对不齐；normalized 同步才能保证「第一个同一行、最后一个对齐」。
    void WireScrollSync()
    {
        if (leftScroll != null)
        {
            leftScroll.onValueChanged.RemoveAllListeners();
            leftScroll.onValueChanged.AddListener(OnLeftScrolled);
        }
        if (rightScroll != null)
        {
            rightScroll.onValueChanged.RemoveAllListeners();
            rightScroll.onValueChanged.AddListener(OnRightScrolled);
        }
    }

    void OnLeftScrolled(Vector2 v)
    {
        if (_scrollSyncing || rightScroll == null) return;
        _scrollSyncing = true;
        rightScroll.verticalNormalizedPosition = v.y;
        _scrollSyncing = false;
    }

    void OnRightScrolled(Vector2 v)
    {
        if (_scrollSyncing || leftScroll == null) return;
        _scrollSyncing = true;
        leftScroll.verticalNormalizedPosition = v.y;
        _scrollSyncing = false;
    }
    bool _scrollSyncing;

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
