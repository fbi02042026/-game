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
        // 【2026-11-23 主人拍板】右列第一格对准左列第 5 行，后面每格隔 5 行（Lv5/10/15/…/50）。
        // 行心距 = 左列「一格高 + 一行距」× 5；每格落点由行距公式决定，不写死坐标。
        const int rightLevelRows = 5;   // 一格对应几行（= 几级），首行锚点与行距共用这一个真源
        var rightVlg = rightContent.GetComponent<VerticalLayoutGroup>();
        if (_rightViews.Count > 1 && rightVlg != null)
        {
            float tplH = rightRowTemplate.GetComponent<RectTransform>().sizeDelta.y;
            float rowH = tplH > 8f ? tplH : RightRowH;
            rightVlg.spacing = Mathf.Max(4f, (LeftNodeH + LeftGapY) * rightLevelRows - rowH);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(rightContent);

        // 【2026-11-24 修·对齐根因】左右滚动同步走的是 normalized（比例）换算：两列 content 总高不相等时，
        // 比例换算只有「基准行」对得上，越往上滚错位越大 —— 表现就是只有第一个右侧图标和左侧对齐。
        // 右列行数 = 左列行数 ÷ rightLevelRows、行距已按 5 行高撑开，把总高差额补到右列 padding.top：
        // 只加顶部空白，行位置与行距都不变，但两列总高一相等，比例同步即等价像素同步，全程对齐。
        if (leftContent != null && rightVlg != null)
        {
            float lh = leftContent.rect.height;
            float rh = rightContent.rect.height;
            if (lh > 1f && rh > 1f && lh - rh > 0.5f)
            {
                rightVlg.padding.top += Mathf.RoundToInt(lh - rh);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rightContent);
            }
        }

        // 读数前先把两列滚到同一基准（normalized=0，L1/R1 在视野底部）——世界 y 差值才有意义。
        // 滚动同步 WireScrollSync 仍在末尾，不动。
        WireScroll(leftScroll, leftContent);
        WireScroll(rightScroll, rightContent);
        // 【2026-11-24 主人拍板】左右滑动列表当作同一个：右列视口世界矩形对齐左列，
        // 上下裁剪边界完全一致（预制体里 LeftColumn/RightColumn 的高度不同，视口天然差几像素）。
        AlignRightViewportToLeft();

        // 【2026-11-24 修·逐行对齐】R1 锚左列第 5 行，之后每行隔 5 行（Lv5/10/15/…）。
        // 过去只按 R1 算一次整列平移：右列只要有一行的落点/行高与「5 行节距」有差，除基准行外就一路带偏 ——
        // 表现就是只有第一个右侧图标和左侧对齐，越往上越偏。改成每行各按「左列第 (i+1)×rightLevelRows 行
        // 首格的图标中心」现读现对：网格行号不写死，误差不累积，右列行距也被钉成与左列 5 行严格一致。
        // 只平移行的直接子节点（行根仍归 VerticalLayoutGroup 排布，content 尺寸/滚动基准不变），视口与 Mask 不动。
        if (_leftViews.Count > 0 && _rightViews.Count > 0 && rightScroll != null)
        {
            // WireScroll 刚改了 normalized 位置，先刷新画布再读世界坐标，否则读到的是滚动前的旧值。
            Canvas.ForceUpdateCanvases();
            var scrollRt = rightScroll.transform as RectTransform;
            float scaleY = scrollRt != null && scrollRt.parent != null ? scrollRt.parent.lossyScale.y : 1f;
            if (Mathf.Abs(scaleY) < 0.001f) scaleY = 1f;
            // 两侧统一取「六边形外框的几何中心」：左 Iconbg pivot=(0.5,0.5)，position 就是中心；
            // 右 Opt_0 pivot=(0,0)，position 是框左下角，必须 TransformPoint(rect.center) 才是中心。
            // （旧代码右取 Opt_0/Icon 内层图案且用 pivot 点比较，语义与 pivot 双重错配。）
            float dyLimit = (LeftNodeH + LeftGapY) * rightLevelRows * 2f;
            for (int i = 0; i < _rightViews.Count; i++)
            {
                int targetRow = (i + 1) * rightLevelRows - 1;      // 左列 0-based 行号
                int li = targetRow * TalentDefs.LeftPerRow;        // 该行首格
                if (li >= _leftViews.Count) continue;              // 左列没有对应行：该行保持原位
                var lv = _leftViews[li];
                var rv = _rightViews[i];
                if (lv == null || lv.root == null || rv == null || rv.root == null) continue;
                var lIcon = lv.root.transform.Find("Iconbg") as RectTransform;
                var rIcon = rv.root.transform.Find("Opt_0") as RectTransform;
                if (lIcon == null || rIcon == null) continue;
                float dy = (lIcon.TransformPoint(lIcon.rect.center).y
                            - rIcon.TransformPoint(rIcon.rect.center).y) / scaleY;
                if (Mathf.Abs(dy) <= 0.5f) continue;
                if (Mathf.Abs(dy) > dyLimit)
                {
                    Debug.LogError("[TalentUI] 右列第 " + (i + 1) + " 行对齐偏移异常 dy=" + dy
                        + "（目标左列行 " + targetRow + "），左右行数映射对不上，不兜底");
                    continue;
                }
                for (int ci = 0; ci < rv.root.transform.childCount; ci++)
                {
                    var child = rv.root.transform.GetChild(ci) as RectTransform;
                    if (child == null) continue;
                    child.anchoredPosition = new Vector2(child.anchoredPosition.x,
                        child.anchoredPosition.y + dy);
                }
            }
            Canvas.ForceUpdateCanvases();
        }
        StretchRightLinkLines();   // 线 y 从已平移的 Opt_0 现读，偏移自动带上；视口不动＝超出的线被 Mask 裁掉

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
    /// 【2026-11-24 修·定稿】连线两端只按「相邻两行六边形的实际可见边缘」现读，
    /// 不再用模板行高 + spacing 推算行距：特殊行的图标落点偏离标准节距时，推算会把线头留缝或压到图标上。
    /// 只改纵向长度与 y 落点；X 中轴仍由 EnsureRightLinkAxis 管，这里不动。
    /// </summary>
    void StretchRightLinkLines()
    {
        int n = _rightViews.Count;
        if (n == 0) return;
        var localBottom = new float[n];   // 六边形可见下缘（相对行根左下）
        var localTop = new float[n];      // 六边形可见上缘（相对行根左下）
        var rowBottom = new float[n];     // 行根下缘（公共坐标）
        var hexBottom = new float[n];     // 六边形可见下缘（公共坐标）
        var hexTop = new float[n];        // 六边形可见上缘（公共坐标）
        var hasHex = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var v = _rightViews[i];
            var root = v.root != null ? v.root.transform as RectTransform : null;
            var opt = v.root != null ? v.root.transform.Find("Opt_0") as RectTransform : null;
            if (root == null || opt == null)
            {
                Debug.LogError("[TalentUI] 右列行缺 Opt_0，连线长度算不出（不兜底）");
                continue;
            }
            // 六边形贴图 preserveAspect 居中绘制在 Opt_0 格里（绘制高 ≈110 / 格高 155，上下各缩进 ≈22）。
            // 缩进从 sprite 实际尺寸现读（不写死），随图标放大 localScale 同步。
            float edgeInset = 0f;
            var hexImg = opt.GetComponent<Image>();
            if (hexImg != null && hexImg.sprite != null && hexImg.preserveAspect
                && opt.sizeDelta.x > 1f && opt.sizeDelta.y > 1f)
            {
                var sr = hexImg.sprite.rect;
                float k = Mathf.Min(opt.sizeDelta.x / sr.width, opt.sizeDelta.y / sr.height);
                edgeInset = Mathf.Max(0f, (opt.sizeDelta.y - sr.height * k) * 0.5f);
            }
            float scaleY = Mathf.Abs(opt.localScale.y) < 0.001f ? 1f : opt.localScale.y;
            localBottom[i] = opt.anchoredPosition.y + edgeInset * scaleY;
            localTop[i] = opt.anchoredPosition.y + opt.sizeDelta.y * scaleY - edgeInset * scaleY;
            rowBottom[i] = root.anchoredPosition.y - root.sizeDelta.y;
            hexBottom[i] = rowBottom[i] + localBottom[i];
            hexTop[i] = rowBottom[i] + localTop[i];
            hasHex[i] = true;
        }
        for (int i = 0; i < n; i++)
        {
            var v = _rightViews[i];
            if (!hasHex[i]) continue;
            // visualIndex 0 在视觉最底（reverseArrangement）⇒ i+1 是上一行。
            // 【2026-11-24 定稿·线只填空隙】lineUp 只画「本行图标顶 → 上一行图标底」这一段空白：
            // 线段整段落在两枚六边形之间的空白里、不进入任何图标区域 ⇒ 行间渲染顺序无论谁在上层，
            // 都不可能压住图标。旧口径画到相邻行中心，线必然穿过图标本身，叠在上层就露在剑上。
            // 两端各压进尖端 2：六边形贴图上下 alpha 留白仅 1~2px，留这 2 逻辑像素刚好贴死、不露缝。
            if (v.lineUp != null)
            {
                if (i + 1 < n && hasHex[i + 1])
                {
                    const float tipOverlap = 2f;
                    // 【2026-11-24 主人拍板】所有连线整体往下 10：段长不变、只把起点下移 linkDown，
                    // 线在两枚六边形之间相对图标整体下移。只改这一处；框/图标/行距/中轴一概不动。
                    const float linkDown = 10f;
                    float startY = localTop[i] - tipOverlap - linkDown;
                    float len = hexBottom[i + 1] - hexTop[i] + tipOverlap * 2f;
                    if (len > 2f) StretchRail(v.lineUp, startY, len, true);
                    else v.lineUp.gameObject.SetActive(false);   // 两行贴到一起时不留线头
                }
            }
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

    /// <summary>
    /// 【2026-11-24 主人拍板】右列视口与左列视口用同一个世界矩形：只改 rightScroll 的 y/sizeDelta.y，
    /// 不动 x、不动 content、不动 Mask 组件；两列的上下裁剪边界由此完全一致。
    /// 左右 Column 的本地尺寸不同，直接抄 anchoredPosition 会偏移，按世界边界反算到 RightColumn 本地。
    /// </summary>
    void AlignRightViewportToLeft()
    {
        if (leftScroll == null || rightScroll == null) return;
        var lvp = leftScroll.viewport as RectTransform;
        if (lvp == null) return;
        var rcol = rightScroll.transform.parent as RectTransform;
        if (rcol == null) return;

        Canvas.ForceUpdateCanvases();
        var lb = lvp.TransformPoint(new Vector3(0f, lvp.rect.yMin, 0f));
        var lt = lvp.TransformPoint(new Vector3(0f, lvp.rect.yMax, 0f));
        var bl = rcol.InverseTransformPoint(lb);
        var tl = rcol.InverseTransformPoint(lt);
        float h = Mathf.Max(1f, tl.y - bl.y);
        var rt = rightScroll.transform as RectTransform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h - rcol.rect.height);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
            (tl.y + bl.y) * 0.5f - rcol.rect.center.y);
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
