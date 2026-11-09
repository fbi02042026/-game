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
            // 2026-11-09 主人拍板：字在按钮的子节点（预制体里的 Text），这里只管父节点显隐，
            // 不再关子节点；新换的按钮底图本身不带字，关掉就成了空白按钮。
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
        // 【2026-11-09 主人拍板】map / 世界层不再跟着面板两态走：预制体已直接摆成战斗中的
        // 常驻位置（中心Y 350 / scale 1.4）。这里只把面板 UI 拍回收起态，并一次性把 map
        // 与世界层落到终态（初始化期、Loading 底下完成，玩家看不到任何位移）。
        ApplyPanelState(0f);
        ApplyMapState(1f);
        ApplyWorldLift(1f);
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
        // 【2026-10-29 主人拍板】无限裂隙**先不开启定向抽** —— 只留随机抽，表现复用
        // 「没过第一章」这一套（隐藏三定向 + 随机按钮挪容器中间）。机制全保留，以后要开只删这个判定。
        bool isEndless = BattleManager.InstanceQuiet != null && BattleManager.InstanceQuiet.IsEndlessRift;
        bool focusUnlocked = SaveSystem.Instance?.Data?.HasClearedChapter(1) == true && !isEndless;
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

        // 【2026-10-29 主人拍板·实时抽 CD】按钮 CD 遮罩：Filled Vertical（Origin=Top），
        // CD 期间 fillAmount 1→0 = 遮罩从上往下消退（主人给的二选一「转圈 或 遮罩从上到下的过渡」取后者）。
        // 挂在按钮占位节点最上层（AddChild 默认排最后），raycastTarget=false 绝不挡点击；
        // 中央叠一个剩余秒数。LiveMode 才会亮出来（TickLiveCooldown 管显隐与数值）。
        var cdGo = new GameObject("CdMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cdGo.transform.SetParent(t, false);
        var cdRt = cdGo.GetComponent<RectTransform>();
        cdRt.anchorMin = Vector2.zero;
        cdRt.anchorMax = Vector2.one;
        cdRt.offsetMin = Vector2.zero;
        cdRt.offsetMax = Vector2.zero;
        var cdImg = cdGo.GetComponent<Image>();
        cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Vertical;
        cdImg.fillOrigin = (int)Image.OriginVertical.Top;
        cdImg.fillAmount = 0f;
        cdImg.color = new Color(0f, 0f, 0f, 0.55f);
        cdImg.raycastTarget = false;
        slot.CdMask = cdImg;

        var cdTextGo = new GameObject("CdSec", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        cdTextGo.transform.SetParent(cdGo.transform, false);
        var cdTextRt = cdTextGo.GetComponent<RectTransform>();
        cdTextRt.anchorMin = new Vector2(0.5f, 0.5f);
        cdTextRt.anchorMax = new Vector2(0.5f, 0.5f);
        cdTextRt.pivot = new Vector2(0.5f, 0.5f);
        cdTextRt.sizeDelta = new Vector2(80f, 40f);
        var cdText = cdTextGo.GetComponent<Text>();
        cdText.fontSize = 26;
        cdText.color = Color.white;
        cdText.alignment = TextAnchor.MiddleCenter;
        cdText.raycastTarget = false;
        var cdFont = GameFonts.GetChinese();
        if (cdFont != null) cdText.font = cdFont;
        slot.CdText = cdText;
        cdGo.SetActive(false);

        if (slot.Btn != null)
        {
            int captured = idx;
            slot.Btn.onClick.AddListener(() => OnDraftClicked(captured));
        }
        _slots.Add(slot);
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
        // 【2026-10-29 修·主人报「战斗到第二波就不让抽奖，按钮一直显示币不够」】
        // 余额一变必须重算按钮置灰：击杀直发抽奖币（AddRunGold）只涨顶栏数字，
        // 按钮的「还差 N」/ interactable 却停在最后一抽时的旧余额上；置灰时点按钮
        // 连 OnDraftClicked 都不触发 → RefreshLive 永远走不到 → 死锁。
        // RefreshLive 开头有 !_liveMode 守卫，裂隙/引导旧面板路径不受影响。
        Instance?.RefreshLive();
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
