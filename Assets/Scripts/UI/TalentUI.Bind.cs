using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 天赋界面 partial —— 2026-10-18 由 TalentUI.cs 纯位移拆出（无逻辑改动）
// 职责：节点绑定与合并行刷新（BindLeftNode/BindRightNode/BindMutexPairRow/RefreshMutexPairRow）
public partial class TalentUI
{

    LeftNodeView BindLeftNode(GameObject go, int index0)
    {
        var v = new LeftNodeView { index = index0 + 1, root = go };
        v.button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        v.icon = go.transform.Find("Icon")?.GetComponent<Image>()
                 ?? go.transform.Find("icon")?.GetComponent<Image>()
                 ?? FindDeepImage(go.transform, "Icon")
                 ?? FindDeepImage(go.transform, "icon");
        v.nameText = go.transform.Find("NameText")?.GetComponent<Text>();
        v.effectText = go.transform.Find("EffectText")?.GetComponent<Text>();
        v.check = go.transform.Find("Check")?.GetComponent<Image>();
        // 【第15轮·主人预制体】连线拆成两个模板节点：lineheng（横）/ lineshu（竖）。
        // 代码只绑定，不改几何/贴图（以主人预制体为准）；显隐与亮暗由 RefreshLeft 控制。
        var linehengTf = go.transform.Find("Lineheng");
        if (linehengTf != null) v.line = linehengTf.GetComponent<Image>();
        else Debug.LogError("[TalentUI] 左列模板缺 Lineheng 节点（主人预制体应有），横向连线不显示");
        var lineshuTf = go.transform.Find("Lineshu");
        if (lineshuTf != null) v.lineV = lineshuTf.GetComponent<Image>();
        else Debug.LogError("[TalentUI] 左列模板缺 Lineshu 节点（主人预制体应有），竖向连线不显示");
        EnsureLeftVerticalLineGeometry(go, v.lineV);
        // 【2026-11-19 主人拍板】左侧层级标记：只用「LvN」纯文字（太阳图后期主人自己补）。
        // 每行第一列显示一个：Lv1=L1~L3、Lv2=L4~L6……挂节点左侧外，运行时建树不改预制体。
        if (index0 % TalentDefs.LeftPerRow == 0)
        {
            var tag = CreateText(go.transform, "LevelTag", "Lv" + (index0 / TalentDefs.LeftPerRow + 1),
                20, new Color(1f, 0.86f, 0.32f, 1f));
            tag.alignment = TextAnchor.MiddleCenter;
            // 放格子内左上角（不探出左侧，避免被 ScrollRect 视口裁掉；美术后期替换时也在这个位置）
            SetAnchored(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(2f, -4f), new Vector2(48f, 24f));
            v.levelTag = tag;
        }
        var upgradeTf = go.transform.Find("升级") ?? go.transform.Find("Upgrade");
        if (upgradeTf != null)
        {
            v.upgradeButton = upgradeTf.gameObject;
            v.upgradeCostText = upgradeTf.GetComponentInChildren<Text>(true);
            var upBtn = upgradeTf.GetComponent<Button>() ?? upgradeTf.gameObject.AddComponent<Button>();
            upBtn.onClick.RemoveAllListeners();
            int upIdx = index0;
            upBtn.onClick.AddListener(() => OnClickLeft(upIdx));
        }
        var def = TalentDefs.Left[index0];
        if (v.nameText != null) v.nameText.text = def.name;
        if (v.effectText != null) v.effectText.text = def.effect.display;
        if (v.icon != null)
        {
            var sp = TalentIcons.GetLeftAttr(index0 % 4);
            if (sp != null) ApplySprite(v.icon, sp, true);
        }
        // 布局交给 Content 的 VerticalLayoutGroup；禁止再改本节点/子节点坐标
        EnsureLeftOrderLabel(v, index0);
        return v;
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】左列每行左边加序号 L1…L40，玩家看得到「点到 L10 会开右侧」。
    /// 【2026-10-14 主人拍板】序号改用**主人在模板里摆好的 Number 节点**（主人原话「我在天赋界面的
    /// 左列加了个Number节点 把每行的数字加上」）——位置/字号/颜色底全按预制体，代码只填文本；
    /// 旧的运行时建 OrderText + 手调坐标那套（21:51 那版）整段删除。
    /// 【2026-11-05 拍板】右列门槛改挂人物等级 → 原「门槛行金色」配色废除，序号统一灰。
    /// ⚠ 序号 = TalentDefs.Left 的次序 i+1；reverseArrangement 只影响视觉上下，不影响编号。
    /// </summary>
    void EnsureLeftOrderLabel(LeftNodeView v, int index0)
    {
        if (v == null || v.root == null) return;

        if (v.orderText == null)
        {
            var node = FindDeepNamed(v.root.transform, "Number");
            v.orderText = node != null ? node.GetComponent<Text>() : null;
        }
        if (v.orderText == null)
        {
            Debug.LogError("[TalentUI] 左列模板缺 Number 节点，L" + (index0 + 1) + " 不显示编号");
            return;
        }

        int no = index0 + 1;
        v.orderText.text = "L" + no;
        // 【2026-11-05 拍板】右列门槛改挂人物等级、与左列解耦 →「门槛行金色」失去意义，序号统一灰色。
        v.orderText.color = new Color(0.80f, 0.80f, 0.82f, 1f);
    }

    RightNodeView BindRightNode(GameObject go, int visualIndex0, TalentDefs.TalentRightNode node)
    {
        var v = new RightNodeView
        {
            node = node,
            visualIndex = visualIndex0,
            root = go
        };
        v.titleText = go.transform.Find("TitleText")?.GetComponent<Text>();
        v.costText = go.transform.Find("CostText")?.GetComponent<Text>();
        v.lockIcon = go.transform.Find("Lock")?.GetComponent<Image>();
        v.diamond = go.transform.Find("Diamond")?.GetComponent<Image>();
        // 【2026-10-13 主人拍板】右列一行两段连线：Line 在菱形上方（冲上）、Line (1) 在菱形下方（冲下）。
        // Line (1) 是主人在预制体里手工复制出来的节点；显隐规则见 RefreshOneRightNode。
        v.lineUp = go.transform.Find("Line")?.GetComponent<Image>();
        v.lineDown = go.transform.Find("Line (1)")?.GetComponent<Image>();
        // 【2026-11-19 自适应】连线/菱形运行时对齐到选项卡片中轴（只改 X）。
        // Mutex 合并行两张卡并排、轨道另有语义，跳过不动。
        if (node.type != TalentDefs.RightNodeType.Mutex) EnsureRightLinkAxis(go, v);
        EnsureRightLock(v);
        if (v.titleText != null) v.titleText.text = node.name;
        if (v.costText != null) v.costText.text = "";

        // 整行点击：解锁 / 升级 / JobChoice 弹 6选1。右列每行一个节点，无分行选项按钮。
        var rowBtn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        rowBtn.targetGraphic = go.GetComponent<Image>();
        rowBtn.onClick.RemoveAllListeners();
        rowBtn.onClick.AddListener(() => OnClickRightNode(visualIndex0));
        v.button = rowBtn;

        // 单选项节点：把唯一选项显示到第 1 个 Opt 槽（图标+名）。JobChoice 不在此显示，靠弹层选。
        var singleOpt = (!node.IsJobChoice && node.options != null && node.options.Length > 0) ? node.options[0] : null;
        for (int o = 0; o < 3; o++)
        {
            var opt = go.transform.Find("Opt_" + o);
            if (opt == null) continue;
            bool show = o == 0 && singleOpt != null;
            opt.gameObject.SetActive(show);
            if (show)
            {
                var label = opt.Find("Label")?.GetComponent<Text>();
                var iconT = opt.Find("Icon") ?? opt.Find("icon");
                var icon = iconT != null ? iconT.GetComponent<Image>() : null;
                if (label != null) label.text = singleOpt.name;
                if (icon != null)
                {
                    var sp = TalentIcons.GetTalent(singleOpt.name);
                    if (sp != null) ApplySprite(icon, sp, true);
                }
            }
            // 整行点击即可，禁用子选项独立响应（避免点中选项区不触发整行）
            var ob = opt.GetComponent<Button>();
            if (ob != null) ob.enabled = false;
        }
        return v;
    }

    /// <summary>
    /// 【2026-10-18 主人拍板】右列互斥对（流派·物理专精 / 流派·魔法专精）合一行：
    /// 两个选项各占一个 Opt 槽。点击矩阵：
    /// · 未选任一 → 点行或点任一选项都开二选一弹窗（带软引导+费用）；
    /// · 已选其一 → 点已选侧/行其它区域 = 升级费用确认；点未选侧 = 互斥引导 toast。
    /// </summary>
    void BindMutexPairRow(RightNodeView v, GameObject go, TalentDefs.TalentRightNode n1, TalentDefs.TalentRightNode n2)
    {
        v.pairIcons = new Image[2];
        v.pairLabels = new Text[2];

        if (v.titleText != null) v.titleText.text = "流派·专精";

        for (int o = 0; o < 2; o++)
        {
            var opt = go.transform.Find("Opt_" + o);
            if (opt == null) continue;
            opt.gameObject.SetActive(true);

            var node = o == 0 ? n1 : n2;
            string optName = OptionDisplayName(node);
            var label = opt.Find("Label")?.GetComponent<Text>();
            var iconT = opt.Find("Icon") ?? opt.Find("icon");
            v.pairLabels[o] = label;
            v.pairIcons[o] = iconT != null ? iconT.GetComponent<Image>() : null;
            if (label != null) label.text = optName;
            if (v.pairIcons[o] != null)
            {
                var sp = TalentIcons.GetTalent(optName);
                if (sp != null) ApplySprite(v.pairIcons[o], sp, true);
            }

            // 选项槽独立可点（与普通行「整行点击」不同：这里两点都要有说法）
            var ob = opt.GetComponent<Button>() ?? opt.gameObject.AddComponent<Button>();
            ob.enabled = true;
            ob.targetGraphic = opt.GetComponent<Image>();
            ob.onClick.RemoveAllListeners();
            var view = v;
            int opt0 = o;
            ob.onClick.AddListener(() => OnClickMutexOption(view, opt0));
        }
        // 模板第三个槽（Opt_2）合并行用不上，隐藏
        var opt2 = go.transform.Find("Opt_2");
        if (opt2 != null) opt2.gameObject.SetActive(false);

        // 整行点击：未选 → 二选一弹窗；已选 → 升级费用确认（BindRightNode 先绑的 OnClickRightNode 在此覆盖）
        var rowBtn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        rowBtn.targetGraphic = go.GetComponent<Image>();
        rowBtn.onClick.RemoveAllListeners();
        var pairView = v;
        rowBtn.onClick.AddListener(() => OnClickMutexPairRow(pairView));
        v.button = rowBtn;
    }

    /// <summary>合并行刷新：标题/费用/锁/菱形/连线/置灰/红点/「新」/选项槽置灰，口径与普通行同源。</summary>
    void RefreshMutexPairRow(RightNodeView v, Dictionary<string, int> talents, int playerLv)
    {
        var n1 = v.node;
        var n2 = v.node2;
        if (n1 == null || n2 == null) return;
        var data = SaveSystem.Instance?.Data;

        int lv1 = TalentDefs.GetRightNodeLevel(talents, n1.id);
        int lv2 = TalentDefs.GetRightNodeLevel(talents, n2.id);
        bool picked = lv1 > 0 || lv2 > 0;
        var pickedNode = lv1 > 0 ? n1 : (lv2 > 0 ? n2 : null);
        int pickedLv = lv1 > 0 ? lv1 : lv2;
        // 【2026-11-05 拍板】门槛/上限 = 人物等级（旧口径 = 左列已点数，已废除）
        bool thresholdLocked = !picked && playerLv < n1.unlockLeftIndex;

        if (v.titleText != null)
            v.titleText.text = pickedNode != null
                ? pickedNode.name + "  Lv" + pickedLv
                : "流派·专精";
        if (v.costText != null)
        {
            if (pickedNode != null)
            {
                int cap = TalentDefs.RightNodeLevelCap(pickedNode, playerLv);
                if (pickedLv >= pickedNode.maxLevel) v.costText.text = "MAX";
                else if (pickedLv >= cap) v.costText.text = "升到" + TalentDefs.LevelNeededForNextLevel(pickedNode, pickedLv) + "级可升级";   // 【2026-11-05 拍板】门槛改人物等级
                else
                {
                    int upCost = pickedNode.costByLevel != null && pickedLv < pickedNode.costByLevel.Length
                        ? pickedNode.costByLevel[pickedLv] : 0;
                    v.costText.text = "升级 " + upCost;
                }
            }
            else
            {
                int cost = n1.costByLevel != null && n1.costByLevel.Length > 0 ? n1.costByLevel[0] : 0;
                v.costText.text = thresholdLocked ? "Lv" + n1.unlockLeftIndex + " 解锁" : "解锁 " + cost;
            }
        }

        bool locked = thresholdLocked;
        EnsureRightLock(v);
        if (v.lockIcon != null)
        {
            v.lockIcon.gameObject.SetActive(locked);
            if (locked) ApplyLockSprite(v.lockIcon);
        }
        if (v.diamond != null)
            ApplySprite(v.diamond, picked ? sprDiamondOn : sprDiamondOff, true);

        // 连线与普通行同一口径（visualIndex 0 在视觉最底，reverseArrangement）
        bool showUp = v.visualIndex < _rightViews.Count - 1;
        bool showDown = v.visualIndex > 0;
        if (v.lineUp != null)
        {
            v.lineUp.gameObject.SetActive(showUp);
            if (showUp) ApplySprite(v.lineUp, picked ? sprRightLinkOn : sprRightLinkOff, false);
        }
        if (v.lineDown != null)
        {
            v.lineDown.gameObject.SetActive(showDown);
            if (showDown) ApplySprite(v.lineDown, picked ? sprRightLinkOn : sprRightLinkOff, false);
        }

        if (v.root != null) v.root.SetActive(true);
        SetRowGray(v.root, locked ? 0.72f : 1f);

        // 红点：任一可动作（未选且够石可解锁 / 已选且够石可升级）
        bool canAct = false;
        if (data != null)
        {
            if (!picked)
            {
                int cost = n1.costByLevel != null && n1.costByLevel.Length > 0 ? n1.costByLevel[0] : 0;
                canAct = !thresholdLocked && data.talentPoints >= cost;
            }
            else
            {
                int cap = TalentDefs.RightNodeLevelCap(pickedNode, playerLv);
                if (pickedLv < pickedNode.maxLevel && pickedLv < cap)
                {
                    int upCost = pickedNode.costByLevel != null && pickedLv < pickedNode.costByLevel.Length
                        ? pickedNode.costByLevel[pickedLv] : 0;
                    canAct = data.talentPoints >= upCost;
                }
            }
        }
        SetRowRedDot(v.root, ref v.redDot, canAct);

        // 「新」：两口子任一是新就算（口径与普通行同源 TalentDefs.IsNewlyOpen）
        bool isNew = TalentDefs.IsNewlyOpen(n1, talents, playerLv)
                     || TalentDefs.IsNewlyOpen(n2, talents, playerLv);
        SetRowNewTag(v.root, ref v.newTag, isNew);

        // 选项槽：已选另一流派 → 这侧置灰表达互斥（门槛锁由整行 CanvasGroup 统一压暗，不再叠加）
        for (int o = 0; o < 2; o++)
        {
            if (v.pairIcons != null && o < v.pairIcons.Length && v.pairIcons[o] != null)
                SetGraphicAlpha(v.pairIcons[o].gameObject, picked && (o == 0 ? lv1 : lv2) <= 0 ? 0.4f : 1f);
            if (v.pairLabels != null && o < v.pairLabels.Length && v.pairLabels[o] != null)
                SetGraphicAlpha(v.pairLabels[o].gameObject, picked && (o == 0 ? lv1 : lv2) <= 0 ? 0.4f : 1f);
        }

        // 与普通行同口径：锁定行也保持可点，点了给说法（见 OnClickMutexPairRow）
        if (v.button != null) v.button.interactable = true;
    }

    /// <summary>
    /// 【2026-11-19 自适应】左列竖线只连「本格图标顶 → 上一行图标底」。
    /// 预制体里 Lineshu 是按 100 高的预览格子摆的（PosY 90.1 / 高 100），运行时格子被 GridLayoutGroup
    /// 改成 116 高，原封不动套进来就会一路上穿进上一行图标（表现＝每个图标身上都插着一根线）。
    /// 这里按运行时格高 LeftNodeH + 行距 LeftGapY 重算一遍 Y 与长度；X、宽度、贴图仍以预制体为准。
    /// </summary>
    static void EnsureLeftVerticalLineGeometry(GameObject node, Image lineV)
    {
        if (node == null || lineV == null) return;
        float iconH = 96f;
        var iconbg = node.transform.Find("Iconbg") as RectTransform;
        if (iconbg != null && iconbg.sizeDelta.y > 8f) iconH = iconbg.sizeDelta.y;
        float y0 = LeftNodeH * 0.5f + iconH * 0.5f;                     // 本格图标顶边
        float y1 = LeftNodeH + LeftGapY + (LeftNodeH - iconH) * 0.5f;   // 上一格图标底边
        float h = Mathf.Max(4f, y1 - y0);
        var rt = lineV.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y0);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
    }

    /// <summary>
    /// 【2026-11-19 自适应】右列连线（Line / Line (1)）与菱形对齐到选项卡片的中轴。
    /// 卡片 Opt_0 是「锚左 + localScale 0.61」，连线是「锚中心」，行宽又由代码钉成 RightRowW；
    /// 预制体里手调的 X 到了运行时会偏开（2026-11-19 实测：卡片中轴≈行中心 −3.7、连线 +69.2，差 73px）。
    /// 这里只改 X，Y、长度、贴图、显隐全不动。找不到卡片时退回菱形自身的 X（保持原样）。
    /// </summary>
    static void EnsureRightLinkAxis(GameObject row, RightNodeView v)
    {
        if (row == null || v == null) return;
        float x = float.NaN;
        var opt = row.transform.Find("Opt_0") as RectTransform;
        if (opt != null && opt.sizeDelta.x > 1f)
            x = opt.anchoredPosition.x + opt.sizeDelta.x * opt.localScale.x * 0.5f - RightRowW * 0.5f;
        else if (v.diamond != null)
            x = v.diamond.rectTransform.anchoredPosition.x;
        if (float.IsNaN(x)) return;
        SetRailX(v.lineUp, x);
        SetRailX(v.lineDown, x);
        if (v.diamond != null) SetRailX(v.diamond, x);
    }

    static void SetRailX(Image img, float x)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }
}
