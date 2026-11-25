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

    /// <summary>模板内解锁粒子节点名（左右模板一致，主人已加）。集中一处，绑定与播放共用。</summary>
    const string UnlockFxNodeName = "shengjieffect";

    /// <summary>
    /// 【2026-11-20】绑定期取粒子引用并立即停掉：prefab 里 playOnAwake=1，
    /// 列表实例化出 80+13 个节点时会全部自动播一遍。StopEmittingAndClear 连已发射的也清掉。
    /// 找不到就 LogError（不兜底），以主人已加节点为准。
    /// </summary>
    static ParticleSystem FindAndStopUnlockFx(GameObject go)
    {
        var tf = go != null ? go.transform.Find(UnlockFxNodeName) : null;
        var fx = tf != null ? tf.GetComponent<ParticleSystem>() : null;
        if (fx == null)
        {
            Debug.LogError("[TalentUI] 节点缺 " + UnlockFxNodeName + " 粒子（模板应有）：" + (go != null ? go.name : "null"));
            return null;
        }
        // 🔴 只 Stop 不够：playOnAwake 在每次 GameObject 重新激活时都会再播一遍（Prewarm/Hide/Show）。
        // 直接把运行实例的标志关掉，之后只有 PlayUnlockFx 能播；不写 prefab。
        fx.playOnAwake = false;
        fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return fx;
    }

    /// <summary>
    /// 【2026-11-20】单点解锁成功的粒子反馈唯一出口；一键升级不调用此方法。
    /// 先 StopEmittingAndClear 再 Play，保证连续点击不叠加残影。
    /// </summary>
    static void PlayUnlockFx(ParticleSystem fx)
    {
        if (fx == null) return;   // 绑定期已 LogError，这里不再重复刷日志
        fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        fx.Play();
    }

    LeftNodeView BindLeftNode(GameObject go, int index0)
    {
        var v = new LeftNodeView { index = index0 + 1, root = go };
        // 【2026-11-20】解锁粒子：取一次缓存 + 立即停。prefab 两个 shengjieffect 都是 playOnAwake=1，
        // 不 Stop 的话进页面实例化 80 个左节点就会齐播一遍；只有单点解锁成功才允许 Play。
        v.unlockFx = FindAndStopUnlockFx(go);
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
        // 【2026-11-19 主人拍板】横线同样按运行时格子重算（预制体是旧单列宽格子时代的手摆值，
        // 三列 116 格下左端会探进左邻图标，表现＝线头压在左邻六边形框上）。
        EnsureLeftHorizontalLineGeometry(go, v.line);
        // 【2026-11-19 主人拍板】左列行底图（图标身后那块浅色圆角方框）退场：只压 alpha 不关组件，
        // raycastTarget 保持 true——整格点击热区不变，按钮 transition 乘色后也依旧不可见。
        var rowBg = go.GetComponent<Image>();
        if (rowBg != null) { var bgC = rowBg.color; bgC.a = 0f; rowBg.color = bgC; }
        // 【2026-11-19 主人拍板】左侧每一行 = 1 级：等级标记只放在每行第一格（复用主人摆好的 Number 节点），
        // 原先运行时另建的 LevelTag 小字整段退场（与 Number 叠在一格里重复表达）。
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
    /// 【2026-11-19 主人拍板】左侧每一行 = 1 级：只在每行首格标「LvN」（Lv1 在最下），
    /// 同格其余两格的 L 序号隐藏——原先每格一个 L1…L90，读起来像「一格一级」。
    /// ⚠ 等级真源 = 行号（index0 / LeftPerRow + 1）；reverseArrangement 只影响视觉上下，不影响编号。
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
            Debug.LogError("[TalentUI] 左列模板缺 Number 节点，Lv" + (index0 / TalentDefs.LeftPerRow + 1) + " 不显示等级");
            return;
        }

        // 【2026-11-20 主人拍板】Lv 固定在视觉左端：蛇形后奇数行左端=data 末列（文本行号仍按 data index）。
        int dataRow = index0 / TalentDefs.LeftPerRow;
        int dataCol = index0 % TalentDefs.LeftPerRow;
        bool rowStart = (dataRow % 2 == 0) ? (dataCol == 0) : (dataCol == TalentDefs.LeftPerRow - 1);
        // 非行首格：整块等级标记隐藏（不占位、不写空串留白块）
        v.orderText.gameObject.SetActive(rowStart);
        if (!rowStart)
        {
            v.levelTag = null;
            return;
        }

        int row = index0 / TalentDefs.LeftPerRow + 1;
        v.orderText.text = "Lv" + row;
        // 【2026-11-19 主人拍板】Lv 标签跟行对齐 + 大两号：
        // 纵向落到行中心（prefab 手摆 y=49.4 偏上 → 运行时 y=0；只改 y，x/sizeDelta 不碰）；
        // 字号 30 → 34。锚点(0,0.5) 保持，仍贴在格子左侧。
        var orderRt = v.orderText.rectTransform;
        orderRt.anchoredPosition = new Vector2(orderRt.anchoredPosition.x, 0f);
        v.orderText.fontSize = 34;
        // 【2026-11-05 拍板】右列门槛改挂人物等级、与左列解耦 →「门槛行金色」失去意义，序号统一灰色。
        v.orderText.color = new Color(0.80f, 0.80f, 0.82f, 1f);
        v.levelTag = v.orderText;
    }

    RightNodeView BindRightNode(GameObject go, int visualIndex0, TalentDefs.TalentRightNode node)
    {
        var v = new RightNodeView
        {
            node = node,
            visualIndex = visualIndex0,
            root = go
        };
        // 【2026-11-20】同上：右列粒子绑定时即停，杜绝列表构建时齐播。
        v.unlockFx = FindAndStopUnlockFx(go);
        v.titleText = go.transform.Find("TitleText")?.GetComponent<Text>();
        v.costText = go.transform.Find("CostText")?.GetComponent<Text>();
        v.lockIcon = go.transform.Find("Lock")?.GetComponent<Image>();
        v.diamond = go.transform.Find("Diamond")?.GetComponent<Image>();
        // 【2026-10-13 主人拍板】右列一行两段连线：Line 在菱形上方（冲上）、Line (1) 在菱形下方（冲下）。
        // Line (1) 是主人在预制体里手工复制出来的节点；显隐规则见 RefreshOneRightNode。
        v.lineUp = go.transform.Find("Line")?.GetComponent<Image>();
        v.lineDown = go.transform.Find("Line (1)")?.GetComponent<Image>();
        // 【2026-11-19 主人拍板】主卡 Opt_0 运行时缩放（倍数见 RightIconScale，预制体不动）。
        // 缩放放在连线对齐之前——EnsureRightLinkAxis 读 Opt_0 的 localScale 算中轴，自动跟随新尺寸。
        // 互斥合并行（两张卡并排）另有语义，不缩、不在此处理。
        // 【2026-11-23 主人拍板】右列图标框统一紫色六边形（天赋_0002_图层-2）：预制体里 Opt_* 本体与
        // 「选中」高亮框的 sprite 引用指向已删除的 图层 21.png（运行时为 null＝白框），这里按路径补。
        for (int o = 0; o < 3; o++)
        {
            var optT = go.transform.Find("Opt_" + o);
            if (optT != null) ApplyHexFrame(optT, true);
        }
        if (node.type != TalentDefs.RightNodeType.Mutex)
        {
            var mainOpt = go.transform.Find("Opt_0") as RectTransform;
            if (mainOpt != null)
            {
                mainOpt.localScale = Vector3.one * RightIconScale;
                // 【2026-11-24 主人拍板】右列整列在紫色滚动区内水平居中：预制体 Opt_0 手摆 x=97，
                // 图标放大到 1.5246 后框中心右移约 47px 顶到视口右缘被裁。按行宽与缩放后框宽现读回中（只改 X）。
                mainOpt.anchoredPosition = new Vector2(
                    RightRowW * 0.5f - mainOpt.sizeDelta.x * RightIconScale * 0.5f,
                    mainOpt.anchoredPosition.y);
            }
        }
        // 【2026-11-19 自适应】连线/菱形运行时对齐到选项卡片中轴（只改 X）。
        // Mutex 合并行两张卡并排、轨道另有语义，跳过不动。
        if (node.type != TalentDefs.RightNodeType.Mutex) EnsureRightLinkAxis(go, v);
        EnsureRightLock(v);
        // 【2026-11-23 主人拍板】右侧不要天赋名字：整行只留图标+连线，TitleText 节点隐藏（保留不删）。
        // 旧实现图标上移而 TitleText 留在行根 →「字和图标分家」；隐藏后不再有这个名字文字。
        if (v.titleText != null)
        {
            v.titleText.text = node.name;
            v.titleText.gameObject.SetActive(false);
        }
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
                if (label != null)
                {
                    label.text = singleOpt.name;
                    // 【2026-11-19 主人拍板】卡片下方名字不显示（图标即语义），节点保留只隐藏
                    label.gameObject.SetActive(false);
                }
                if (icon != null)
                {
                    var sp = TalentIcons.GetTalent(singleOpt.name);
                    if (sp != null) ApplySprite(icon, sp, true);
                    // 【2026-11-23 主人拍板】图标与紫色六边形框同心：框是 Opt_0 本体（preserveAspect 居中），
                    // 图标按自身锚点与父框高度现读回中；只改纵向 y，x / sizeDelta / 贴图 / prefab 一概不动。
                    // 【2026-11-25 主人拍板】右侧图标整体下移 15（几何居中后再往下 15，框不动；原 11-24 为 10）。
                    const float iconDown = 15f;
                    var iconRt = icon.rectTransform;
                    var parentRt = iconRt.parent as RectTransform;
                    if (parentRt != null)
                    {
                        float parentH = parentRt.rect.height > 1f ? parentRt.rect.height : parentRt.sizeDelta.y;
                        float anchorY = (iconRt.anchorMin.y + iconRt.anchorMax.y) * 0.5f;
                        iconRt.anchoredPosition = new Vector2(iconRt.anchoredPosition.x,
                            (0.5f - anchorY) * parentH - iconDown);
                    }
                }
            }
            // 整行点击即可，禁用子选项独立响应（避免点中选项区不触发整行）
            var ob = opt.GetComponent<Button>();
            if (ob != null) ob.enabled = false;
        }
        return v;
    }

    /// <summary>
    /// 【2026-11-23 主人拍板】图标框补图：预制体里 Opt_* 本体与「选中」高亮框（节点名 U+9009 U+4E2D）
    /// 的 sprite 引用指向已删除的 图层 21.png，运行时解析为 null（白框）。
    /// <para>forceSelf=true 时本节点无条件换成紫六边形（右列图标框是本需求主线）；
    /// 「选中」子节点只补 null，已有贴图（Icon / bg / 连线）一律不动。</para>
    /// <para>贴图取不到直接 LogError，不兜底。</para>
    /// </summary>
    void ApplyHexFrame(Transform node, bool forceSelf)
    {
        if (node == null) return;
        if (sprRightHex == null)
        {
            Debug.LogError("[TalentUI] 六边形框贴图缺失（sprRightHex = UI/Talent/天赋_0002_图层-2）");
            return;
        }
        var own = node.GetComponent<Image>();
        if (own != null && (forceSelf || own.sprite == null)) ApplySprite(own, sprRightHex, true);
        for (int i = 0; i < node.childCount; i++)
        {
            var child = node.GetChild(i);
            if (child.name != "选中") continue;
            var img = child.GetComponent<Image>();
            if (img != null && img.sprite == null) ApplySprite(img, sprRightHex, true);
        }
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

        // 红点真值提前算：未选且够石可解锁 / 已选且够石可升级；灰度与红点共用同一个 canAct。
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
        // 【2026-11-25 主人拍板】与普通行/左列同口径：已选且能升=亮；未选且可解锁=暗；
        // MAX / 升到N级 / 天赋石不足 / 等级门槛 = 灰。锁图标只在门槛时挂。
        float rowAlpha = canAct ? (picked ? 1f : 0.5f) : 0.72f;
        EnsureRightLock(v);
        if (v.lockIcon != null)
        {
            v.lockIcon.gameObject.SetActive(thresholdLocked);
            if (thresholdLocked) ApplyLockSprite(v.lockIcon);
        }
        if (v.diamond != null)
        {
            ApplySprite(v.diamond, picked ? sprDiamondOn : sprDiamondOff, true);
            if (!picked && canAct) v.diamond.color = new Color(0.32f, 0.32f, 0.32f, 1f);
            else if (!canAct) v.diamond.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        // 连线与普通行同一口径（visualIndex 0 在视觉最底，reverseArrangement）
        bool showUp = v.visualIndex < _rightViews.Count - 1;
        // 【2026-11-24 定稿】下段 Line(1) 统一不显示：每段连接只由下方行的上段 Line 画。
        // reverseArrangement 下上一行后渲染，下段会压在下一行六边形图标之上（表现＝线穿过剑）。
        // 上方行的连接由它自己那行的上段负责，隐藏下段不丢任何一段线。
        if (v.lineUp != null)
        {
            v.lineUp.gameObject.SetActive(showUp);
            if (showUp) ApplySprite(v.lineUp, picked ? sprRightLinkOn : sprRightLinkOff, false);
        }
        if (v.lineDown != null)
            v.lineDown.gameObject.SetActive(false);

        if (v.root != null) v.root.SetActive(true);
        SetRowGray(v.root, rowAlpha);

        // 红点：11-19 主人拍板右列不要红点——合并行与普通行同口径（canAct 仍用于灰度判定）
        SetRowRedDot(v.root, ref v.redDot, false);

        // 【2026-11-19 主人拍板】右列红色「新」标记也退场（截图里右上角的红点即它）；节点保留恒隐藏。
        SetRowNewTag(v.root, ref v.newTag, false);

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
    /// 【2026-11-19 主人拍板】左列横线（Lineheng）按运行时格子重算：预制体按旧单列宽格子手摆
    /// （锚右缘 -92 / 长 60），三列网格（116 格 + 8 列距）下左端探进左邻图标 18px。
    /// 只重算长度与 x：右端缩进自家图标 4px（同格内自家图标后画、天然盖住），
    /// 左端精确顶到左邻图标右缘（不重叠＝不压图标，也不留缝）；y、线宽、贴图仍以预制体为准。
    /// </summary>
    static void EnsureLeftHorizontalLineGeometry(GameObject node, Image lineH)
    {
        if (node == null || lineH == null) return;
        float iconW = 96f;
        var iconbg = node.transform.Find("Iconbg") as RectTransform;
        if (iconbg != null && iconbg.sizeDelta.x > 8f) iconW = iconbg.sizeDelta.x;
        float cellHalf = LeftNodeW * 0.5f;
        float neighborRight = -(LeftNodeW + LeftGapX) + iconW * 0.5f;   // 左邻图标右缘
        float rightEnd = -iconW * 0.5f + 4f;                            // 自家图标左缘内缩 4（被自家图标盖住）
        float len = Mathf.Max(4f, rightEnd - neighborRight);
        var rt = lineH.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);            // 与预制体同口径：锚格子右缘 / pivot 右端
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(len, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(rightEnd - cellHalf, rt.anchoredPosition.y);
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
