using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 进关抽奖面板 partial —— 2026-10-18 由 BattleEntryDraftPanel.cs 纯位移拆出（无逻辑改动）
// 职责：构建按钮（Build/AddSlot、概率公示按钮、连抽提示、币池计数、EnsureClickable）
public partial class BattleEntryDraftPanel
{
    /// <summary>
    /// 建按钮索引：4 个占位节点（主人摆好的 RectTransform，图在子节点 icon 上）
    /// 各补一个 Button，并把 name / 金额 两个 Text 的引用抓到手。
    /// 不新建 Image、不新建 Text —— 那些是主人在预制体里调好的美术。
    /// </summary>
    void Build()
    {
        if (_built) return;
        _built = true;

        if (_panel == null) return;
        _draftRoot = FindDeep(_panel, NodeDraftRoot);
        var content = FindDeep(_panel, NodeContent);
        if (content == null)
            Debug.LogError("[BattleEntryDraftPanel] 找不到内容节点 BackpackPanel/" + NodeContent);
        else
            _contentRt = content as RectTransform;

        // 网格背包与 zhuangshi 同层，记下它没被动过时的 y —— 展开时按<b>同一个位移量</b>跟着走。
        // 不这么做就变成「zhuangshi 动了、网格背包没动」，主人要的整体移动就散了。
        var grid = FindDeep(_panel, NodeGrid) as RectTransform;
        if (grid == null)
            Debug.LogError("[BattleEntryDraftPanel] 找不到网格背包节点 BackpackPanel/" + NodeGrid);
        else
            _followers.Add(new ContentFollower { Rt = grid, BaseY = grid.anchoredPosition.y });

        if (_draftRoot == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到抽奖容器 BackpackPanel/" + NodeDraftRoot);
        }
        else
        {
            _draftRt = _draftRoot as RectTransform;
            // 顺序与预制体里主人摆的一致：普通 / 佣兵 / 装备 / 技能
            AddSlot("BtnNormal", null);
            AddSlot("BtnMerc", DraftCategory.Merc);
            AddSlot("BtnEquip", DraftCategory.Equip);
            AddSlot("BtnSkill", DraftCategory.Skill);

            // 概率公示按钮（见 BuildOddsButton 注释）
            BuildOddsButton();
            // 【2026-10-13 #9】连抽提示（见 BuildComboText 注释）
            BuildComboText();
            // 【2026-10-21 主人拍板「抽奖数都在顶部」】原 BuildPoolChip（按钮上方抽奖币计数）已拆，
            // 余额只走顶栏 DraftDisplay 抽奖币条（BattleUI.UpdateDraftCoin）。
        }

        var cont = FindDeep(_panel, NodeContinue);
        if (cont == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到「继续」按钮节点");
        }
        else
        {
            _continueBtn = EnsureClickable(cont);
            // 2026-10-04 主人拍板：「继续」按钮不要文字，只留美术图标。
            // 不管预制体自带还是别处补的 Text，一律关掉 —— 不留第二个文字入口。
            var contTexts = cont.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < contTexts.Length; i++)
                contTexts[i].gameObject.SetActive(false);
            if (_continueBtn != null) _continueBtn.onClick.AddListener(OnContinueClicked);
        }

        var bp = FindDeep(_panel, NodeBackpack);
        if (bp == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到「背包」按钮节点");
        }
        else
        {
            // 背包是纯图标按钮，不加文字
            _backpackBtn = EnsureClickable(bp);
            if (_backpackBtn != null) _backpackBtn.onClick.AddListener(OnBackpackClicked);
        }

        // 预制体此刻存的是主人手调出来的<b>展开态</b>（面板 750 / zhuangshi 80 / DraftRoot -239）。
        // 先按真值表拍回收起态，之后 Begin→SetExpanded(true) 才能从 0 平滑动到 1；
        // 不拍这一步，第一次展开会先把面板从 750 抽回 600 再展开，看着就是「跳一下」。
        SetDraftVisible(false);
        ApplyState(0f);
    }

    /// <summary>
    /// 在一个占位节点上补出可点的抽奖按钮。
    /// 名字 / 金额两个 Text 走预制体里主人调好的，这里只拿到引用，不重建。
    /// </summary>
    void AddSlot(string nodeName, DraftCategory? cat)
    {
        var t = FindDeep(_draftRoot, nodeName);
        if (t == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 抽奖容器下找不到占位节点: " + nodeName);
            return;
        }

        var rt0 = t as RectTransform;

        // 【2026-10-07 主人拍板】定向招募 = 装备 / 佣兵 / 技能 三个按钮，**通过第一章后才开放**；
        // 没过第一章只留「普通」（随机）抽奖。
        // 平时用「隐藏」而不是「置灰」：置灰还会占位、玩家点了才知道不能抽，隐藏更干净。
        // 只在这里动显隐（单一出口），SetDraftVisible 管的是整个容器，不会把它们再打开。
        bool focusUnlocked = SaveSystem.Instance?.Data?.HasClearedChapter(1) == true;
        if (cat.HasValue)
        {
            t.gameObject.SetActive(focusUnlocked);
        }
        else if (!focusUnlocked && rt0 != null)
        {
            // 四个按钮原本摆在 x = -255 / -85 / 85 / 255。没过第一章只剩第一个时会偏在最左边
            // —— 把它挪到容器中间（x = 0）；解锁后<b>一个坐标都不动</b>，直接用主人摆好的四格。
            rt0.anchoredPosition = new Vector2(0f, rt0.anchoredPosition.y);
        }

        var slot = new DraftSlot { Root = t as RectTransform, Cat = cat };
        int idx = _slots.Count;

        slot.Btn = EnsureClickable(t);

        var nameNode = FindDeep(t, "name");
        if (nameNode != null) slot.NameText = nameNode.GetComponent<Text>();
        // 价格节点是按钮的<b>直接</b>子节点，跟 icon / name 平级 —— 不是在 name 下面。
        // 早期写成 FindDeep(nameNode, ...) 导致永远拿不到引用、价格数字不显示。
        var costNode = FindDeep(t, NodeCost);
        if (costNode != null) slot.CostText = costNode.GetComponent<Text>();

        // 【2026-10-13 #7】价格下方的「还差 N」红字（币不够才显示），挂在 Cost 节点下方，不动主人调好的美术。
        if (costNode != null)
        {
            var sf = new GameObject("Shortfall", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            sf.transform.SetParent(costNode, false);
            var sfrt = sf.GetComponent<RectTransform>();
            sfrt.anchorMin = new Vector2(0.5f, 0f);
            sfrt.anchorMax = new Vector2(0.5f, 0f);
            sfrt.pivot = new Vector2(0.5f, 1f);
            sfrt.anchoredPosition = new Vector2(0f, -2f);
            sfrt.sizeDelta = new Vector2(160f, 24f);
            var sft = sf.GetComponent<Text>();
            sft.fontSize = 16;
            sft.color = new Color(1f, 0.42f, 0.42f);   // #FF6B6B
            sft.alignment = TextAnchor.UpperCenter;
            sft.raycastTarget = false;
            sft.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sff = GameFonts.GetChinese();
            if (sff != null) sft.font = sff;
            slot.ShortfallText = sft;
            sf.SetActive(false);
        }

        // 按钮左侧的货币图标（2026-10-07 主人拍板）：建在 Cost 左外侧，不动主人调好的美术。
        if (costNode != null) BuildCoinIcon(slot, costNode);
        else Debug.LogError("[BattleEntryDraftPanel] 抽奖按钮 " + nodeName + " 找不到价格节点 " + NodeCost + "，货币图标没地方挂");

        if (slot.Btn != null)
        {
            int captured = idx;
            slot.Btn.onClick.AddListener(() => OnDraftClicked(captured));
        }
        _slots.Add(slot);
    }

    /// <summary>
    /// 概率公示按钮（美术图「提示」金币问号）：贴在「普通抽奖」按钮右边，点了弹 <see cref="DraftOddsPopupUI"/>。
    ///
    /// 2026-10-05 主人拍板：「概率需要告诉玩家……可以再（抽）按钮旁边加个感叹号之类的，
    /// 点击弹出弹窗上面写的概率」「可以加到 DraftRoot 节点里」。预制体里没有这个节点
    ///（主人没摆，也不许我们改 prefab），所以运行时建一个挂到 <c>DraftRoot</c> 下 ——
    /// 跟抽奖按钮<b>同层</b>，抽奖按钮自己被置灰 / 缩放 / 换锚点都不会带着「!」一起跑。
    ///
    /// ⚠ 2026-10-15 主人拍板换成美术图（Resources.Load 路径加载，原图 80x72 非正方开 preserveAspect），不碰主人在预制体里调好的任何美术。
    /// </summary>
    void BuildOddsButton()
    {
        if (_draftRt == null || _slots.Count == 0) return;
        var normal = _slots[0].Root;
        if (normal == null) return;

        var go = new GameObject("BtnOdds", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(_draftRt, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // 【2026-10-13 #6】58 → 88：顺手修触摸目标不达标（美术规格 §5.5）。
        // 【2026-10-16 主人口令】图标有点大 → 缩小 30%（88 → 61.6）。
        rt.sizeDelta = new Vector2(61.6f, 61.6f);
        // 用世界角点换成容器本地坐标，抽奖按钮是什么锚点都不影响（直接读 anchoredPosition 会被锚点坑）。
        var corners = new Vector3[4];
        normal.GetWorldCorners(corners);
        Vector2 center = _draftRt.InverseTransformPoint((corners[0] + corners[2]) * 0.5f);
        Vector2 right = _draftRt.InverseTransformPoint(corners[2]);
        float halfW = Mathf.Abs(right.x - center.x);
        // 【2026-10-13 #6】按钮更宽，间隙从 38 拉到 60，免得贴着普通按钮。
        // 【2026-10-16 主人口令】往上移 10 像素。
        rt.anchoredPosition = new Vector2(center.x + halfW + 60f, center.y + 10f);

        var img = go.GetComponent<Image>();
        // 2026-10-15 主人拍板：概率按钮换成美术图 Art/UI/Common/提示（金币问号），
        // 不再用「深色圆底 + ! 文本」；取不到图 LogError，不兜底造图。
        var iconSp = Resources.Load<Sprite>("Art/UI/Common/提示");
        if (iconSp == null)
            Debug.LogError("[BattleEntryDraftPanel] 缺少美术图 Art/UI/Common/提示（概率按钮图标不显示）");
        img.sprite = iconSp;
        img.preserveAspect = true; // 原图 80x72 非正方，防拉变形
        img.color = Color.white;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => DraftOddsPopupUI.Toggle());

        _oddsBtn = btn;
    }

    /// <summary>【2026-10-13 #9】「已连抽 ×N」提示：挂在抽奖容器上方，≥2 才显示（不新建美术节点）。</summary>
    void BuildComboText()
    {
        if (_draftRt == null || _comboText != null) return;
        var go = new GameObject("ComboHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(_draftRt, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 120f);   // 抽奖按钮上方
        rt.sizeDelta = new Vector2(260f, 28f);
        var t = go.GetComponent<Text>();
        t.fontSize = 16;
        t.color = new Color(1f, 0.72f, 0.2f);   // #FFB833
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;
        go.SetActive(false);
        _comboText = t;
    }

    /// <summary>【2026-10-13 #9】刷新连抽提示：≥2 显示「已连抽 ×N」；达到 3/5/8 连抽闪一次金。</summary>
    void RefreshCombo()
    {
        if (_comboText == null) return;
        bool show = _comboCount >= 2;
        _comboText.gameObject.SetActive(show);
        if (show) _comboText.text = $"已连抽 ×{_comboCount}";
        if (_comboCount == 3 || _comboCount == 5 || _comboCount == 8)
        {
            if (_comboFlashCo != null) StopCoroutine(_comboFlashCo);
            _comboFlashCo = StartCoroutine(CoComboFlash());
        }
    }

    /// <summary>【2026-10-13 #9】连抽里程碑（3/5/8）金闪三次；保守实现为闪提示本身，不碰按钮 icon 美术。</summary>
    IEnumerator CoComboFlash()
    {
        if (_comboText == null) { _comboFlashCo = null; yield break; }
        Color gold = new Color(1f, 0.72f, 0.2f);
        Color dim = new Color(0.85f, 0.88f, 0.95f);
        for (int i = 0; i < 3; i++)
        {
            _comboText.color = gold;
            _comboText.transform.localScale = Vector3.one * 1.15f;
            yield return new WaitForSecondsRealtime(0.06f);
            _comboText.color = dim;
            _comboText.transform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.06f);
        }
        _comboText.color = gold;                 // 还原基准金色
        _comboText.transform.localScale = Vector3.one;
        _comboFlashCo = null;
    }

    /// <summary>
    /// 抽奖币池变化的轻量刷新出口（<see cref="BattleManager.AddRunGold"/> / SpendBattleGold 调）：
    /// 【2026-10-21 主人拍板「抽奖数都在顶部」】原按钮上方 DraftPoolChip 计数已拆，
    /// 现在只刷顶栏 DraftDisplay 抽奖币条（BattleUI.UpdateDraftCoin）。
    /// 面板不存在（未建/已销毁）时静默跳过，下次 Show 自然带上最新值。
    /// </summary>
    public static void NotifyPoolGoldChanged(long coins)
    {
        // 顶栏 DraftDisplay 抽奖币条（2026-10-18 主人摆的节点）—— 余额唯一显示处
        BattleUI.Instance?.UpdateDraftCoin(coins);
    }

    /// <summary>
    /// 给已有节点补 Button，让它能点。
    /// 优先用节点自己的 Image 做点击反馈；本体没有 Image 时（抽奖按钮就是这种，
    /// 图在子节点 icon 上）用 icon 的 Image —— <b>绝不在本体上硬加 Image</b>，
    /// 否则会多出一块白底盖住美术。
    /// 注意：不创建任何 Text，名字/金额都是主人在预制体里调好的。
    /// </summary>
    static Button EnsureClickable(Transform t)
    {
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        var g = t.GetComponent<Image>();
        if (g == null)
        {
            var icon = FindDeep(t, "icon");
            if (icon != null) g = icon.GetComponent<Image>();
        }
        if (g != null) btn.targetGraphic = g;
        return btn;
    }

}
