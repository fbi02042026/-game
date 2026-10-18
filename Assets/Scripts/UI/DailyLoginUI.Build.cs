using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：构建与预制体绑定（Build/TryBindFullPrefab/BindBars/格子创建与 Build* 小节）
public partial class DailyLoginUI
{

    // ============================================================
    // 构建
    // ============================================================

    void Build()
    {
        // 完整弹窗预制体：画布与节点都在预制体上，直接按名字绑定（2026-09-23）
        if (TryBindFullPrefab()) return;

        // ---- 兜底：代码从零生成 ----
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        // 预制体接口：美术做了精修预制体（Resources/Prefabs/UI/DailyLoginFull）就铺它当底，
        // 找不到才用临时色块兜底
        bool hasSkin = TryLoadSkin();
        BuildBackdrop(!hasSkin);
        BuildHeader();
        BuildGrid();
        BuildCycleLine();
        BuildStreakLine();
        BuildInfoBar();

        _root.SetActive(false);
    }

    /// <summary>
    /// 完整弹窗预制体绑定（2026-09-23）：预制体自带 Canvas + 全部节点（见 Tools/每日登录/生成器），
    /// 这里只按名字抓引用，不再创建节点。任何必需节点缺失都返回 false 走代码生成兜底。
    /// 节点名即契约：Cell_N / DayTag/Label / IconHolder(+Fallback) / Reward / X2 / Rare / Mark
    ///               CycleBar(Tag/Name/ClaimBtn+Label) / StreakBar(Tag/Streak_天数) / InfoBar(Cap/Days/MercIcon/MercText)
    /// </summary>
    bool TryBindFullPrefab()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return false;                 // 不是完整弹窗预制体
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        // 先全部检查完再写入成员，避免绑一半失败后回退造成节点重复
        var bound = new List<Cell>();
        for (int i = 1; i <= DailyLoginDefs.Starter.Length; i++)
        {
            // 递归深度查找：Cell_1~8 在主人预制体里包在 CellsPanel 下，直接子级查找会失败，必须递归
            var t = FindChildDeep(transform, "Cell_" + i);
            if (t == null)
            {
                Debug.LogError("[DailyLoginUI] 预制体缺格子 Cell_" + i + "，回退代码生成");
                return false;
            }
            var bg = t.GetComponent<Image>();
            if (bg == null) return false;

            var c = new Cell { go = t.gameObject, bg = bg, day = i };
            // B5: 记录预制体底框原色（压暗一律从 base 重算，绝不写死颜色去覆盖美术底图配色）
            c.baseBgColor = bg != null ? bg.color : Color.white;

            var tag = FindChildDeep(t, "DayTag");
            c.dayText = tag != null ? tag.GetComponentInChildren<Text>() : null;
            // B5: 记录预制体天数文字原色（同样从 base 重算，避免换掉美术色）
            c.baseDayColor = c.dayText != null ? c.dayText.color : Color.white;

            var icon = FindChildDeep(t, "IconHolder");
            if (icon != null)
            {
                c.icon = icon.GetComponent<Image>();
                c.iconFallback = icon.GetComponentInChildren<Text>();
                // B7: 碎片卡（含佣兵头像）只建一次挂到 IconHolder 下并缓存；Refresh 只切显隐 + Apply（严禁每次重建）
                var irt = icon.GetComponent<RectTransform>();
                float fragSize = irt != null ? irt.sizeDelta.x : 70f;   // IconHolder 预制体 70×70，取其宽
                // 创建时先给默认稀有档占位，真正的 rarity/头像在 Refresh 里按奖励 Apply；头像卡默认隐藏
                var card = MercGrowUI.CreateFragmentCard(icon, "FragmentCard", MercRosterDefs.MercRarity.Rare, "", fragSize);
                c.fragCard = card;
                if (card.Root != null) card.Root.SetActive(false);
            }

            var reward = FindChildDeep(t, "Reward");
            c.rewardText = reward != null ? reward.GetComponent<Text>() : null;

            var x2 = FindChildDeep(t, "X2");
            c.x2Badge = x2 != null ? x2.gameObject : null;
            var rare = FindChildDeep(t, "Rare");
            c.rareBadge = rare != null ? rare.gameObject : null;
            var mark = FindChildDeep(t, "Mark");
            c.markText = mark != null ? mark.GetComponent<Text>() : null;
            // ★ 选中图片节点：按名字「选中」绑定；仅在 Refresh 里对当天可领的天 SetActive(true)
            var sel = FindChildDeep(t, "选中");
            c.selectedNode = sel != null ? sel.gameObject : null;

            c.btn = t.GetComponent<Button>();
            if (c.btn == null) c.btn = t.gameObject.AddComponent<Button>();
            c.btn.transition = Selectable.Transition.None;
            // 用 cell.day 而不是绑死的 i：轮回后 cell.day 会在 Refresh 里换成累计天数（9、10…）
            c.btn.onClick.AddListener(() => OnClickStarter(c.day));
            // B11: 全部组件绑完后统一记原色（CaptureBase），之后压暗一律从 base 重算
            CaptureCellBase(c);
            bound.Add(c);
        }

        // 2026-09-23 主人决定：每日循环条 / 连击加成条不再在界面显示（连击改为双倍奖励）。
        // 所以这三条都按「可选」处理——缺了就跳过，BindBars/Refresh/Layout 内部都有判空。
        BindBars();

        // 绑定预制体上的关闭按钮（主人反馈：点了没反应——代码生成路径才建按钮，
        // 走预制体路径时那颗按钮没有监听）。铁律：以预制体那颗按钮为准，绝不新建；
        // 防重复绑定先 RemoveAllListeners，再挂 Hide。找不到则 no-op 不报错。
        var closeT = FindChildDeep(transform, "CloseButton");
        if (closeT != null)
        {
            var closeBtn = closeT.GetComponent<Button>();
            if (closeBtn == null) closeBtn = closeT.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;     // 与格子按钮风格一致（见上 c.btn）
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(Hide);
        }

        // 2026-09-25 主人要求：第 8 天底框改普通框——只有「限定英雄天」才用特殊框（详见 NormalizeCellFrames）
        NormalizeCellFrames(bound);

        _starterCells.AddRange(bound);
        _root = gameObject;                               // Layout/Refresh/Hide 全按根节点找名字
        _boundFull = true;                                // 完整预制体路径：节点位置以美术预制体为准
        return true;
    }

    // 【主人口径 2026-09-25】默认所有天都用普通框；只有主人点名要金框（限定英雄天）的天才填进这里。
    // 碎片天（mercfrag）也是普通框。将来主人说「第 N 天改金色框」，把 N 加进这个数组即可，代码其它地方不用动。
    private static readonly int[] GOLD_FRAME_DAYS = { };

    /// <summary>
    /// 2026-09-25 主人要求：默认所有天都是普通框，包括佣兵碎片天（mercfrag）。
    /// 金框（限定英雄天）不靠代码自动判定，而是**等主人点名**——只认 GOLD_FRAME_DAYS 白名单（当前为空 → 8 天全普通框）。
    ///
    /// 普通框 sprite 不从 Resources 加载（怕加载错图）：直接取**某个确定该用普通框的格子**的底图
    /// （主人预制体里摆好的原图，天然与美术资源一致）。只写 sprite，
    /// 绝不碰 sizeDelta / anchoredPosition / 层级顺序。预制体缺图（兜底路径）则整段 no-op。
    /// </summary>
    static void NormalizeCellFrames(List<Cell> cells)
    {
        if (cells == null || cells.Count == 0) return;

        // 1) 取普通框样本：第一个「非金框」的格子（不写死 Cell_1）
        Sprite normal = null;
        for (int i = 0; i < cells.Count; i++)
        {
            if (IsHeroDay(i + 1)) continue;
            var sample = cells[i]?.bg;
            if (sample == null) continue;
            normal = sample.sprite;
            break;
        }
        if (normal == null) return;   // 没有任何普通框底图（兜底路径）→ 不动

        // 2) 非金框的天一律换成普通框（只赋 sprite）
        for (int i = 0; i < cells.Count; i++)
        {
            if (IsHeroDay(i + 1)) continue;            // 金框天保留预制体的特殊框
            var bg = cells[i]?.bg;
            if (bg == null || bg.sprite == normal) continue;
            bg.sprite = normal;
        }
    }

    /// <summary>
    /// 该天（1 基天数）是否用金框（限定英雄天）：只认显式白名单 GOLD_FRAME_DAYS。
    /// 默认全 false（普通框）；主人点名某天要金框时，把天数加进 GOLD_FRAME_DAYS 即可。
    /// </summary>
    static bool IsHeroDay(int day)
    {
        return System.Array.IndexOf(GOLD_FRAME_DAYS, day) >= 0;
    }

    /// <summary>绑定三条横带的子节点引用（名字同兜底路径，Layout() 直接复用）。</summary>
    void BindBars()
    {
        var cycle = FindChildDeep(transform, "CycleBar");
        if (cycle != null)
        {
            var nameT = FindChildDeep(cycle, "Name");
            _cycleName = nameT != null ? nameT.GetComponent<Text>() : null;
            var btnT = FindChildDeep(cycle, "ClaimBtn");
            if (btnT != null)
            {
                _cycleBtn = btnT.GetComponent<Button>();
                if (_cycleBtn == null) _cycleBtn = btnT.gameObject.AddComponent<Button>();
                _cycleBtn.onClick.AddListener(OnClickCycle);
                _cycleState = btnT.GetComponentInChildren<Text>();
            }
        }

        var streak = FindChildDeep(transform, "StreakBar");
        if (streak != null)
        {
            var tagT = FindChildDeep(streak, "Tag");
            _streakTag = tagT != null ? tagT.GetComponent<Text>() : null;
            for (int i = 0; i < DailyLoginDefs.Streak.Length && i < _streakBtns.Length; i++)
            {
                var s = DailyLoginDefs.Streak[i];
                var bt = FindChildDeep(streak, "Streak_" + s.days);
                if (bt == null) continue;                 // 改了 Streak 天数后重跑生成器即可
                _streakBtns[i] = bt.GetComponent<Button>();
                if (_streakBtns[i] == null) _streakBtns[i] = bt.gameObject.AddComponent<Button>();
                int streakDays = s.days;
                _streakBtns[i].onClick.AddListener(() => OnClickStreak(streakDays));
                _streakLabels[i] = bt.GetComponentInChildren<Text>();
            }
        }

        var info = FindChildDeep(transform, "InfoBar");
        if (info != null)
        {
            var daysT = FindChildDeep(info, "Days");
            _infoDays = daysT != null ? daysT.GetComponent<Text>() : null;
            // B2: 收集 InfoBar 下所有 "MercText*" 文本（主人摆好的三行文案），代码只改 color 绝不改 text
            _infoMercTexts.Clear();
            CollectMercTexts(info, _infoMercTexts);
            var iconT = FindChildDeep(info, "MercIcon");
            _infoMercIcon = iconT != null ? iconT.GetComponent<Image>() : null;

            // B4: 底部信息条 = 累计大奖领取入口（与 CloseButton 同风格：None 过渡，先 RemoveAllListeners 再挂监听；用预制体已有节点不新建）
            var infoBtn = info.GetComponent<Button>();
            if (infoBtn == null) infoBtn = info.gameObject.AddComponent<Button>();
            infoBtn.transition = Selectable.Transition.None;
            infoBtn.onClick.RemoveAllListeners();
            infoBtn.onClick.AddListener(OnClickAccum);
        }
    }

    /// <summary>美术预制体存在则实例化为最底层皮肤，返回是否有皮肤（有就跳过临时色块）。</summary>
    bool TryLoadSkin()
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) return false;
        var skin = UnityEngine.Object.Instantiate(prefab, _root.transform, false);
        skin.name = "Skin";
        var srt = skin.GetComponent<RectTransform>();
        if (srt != null) Stretch(srt);
        skin.transform.SetSiblingIndex(0);
        return true;
    }

    void BuildBackdrop(bool useFallback)
    {
        if (!useFallback) return;   // 有美术皮肤时不铺色块，免得盖住图
        // 临时兜底：全屏双层底色（上红下深），中间用色带过渡
        var top = CreateImg(_root.transform, "BgTop", ColBgTop);
        AnchorFillRect(top.rectTransform, 0.62f, 1f);

        var bottom = CreateImg(_root.transform, "BgBottom", ColBgBottom);
        AnchorFillRect(bottom.rectTransform, 0f, 0.63f);

        var band = CreateImg(_root.transform, "BgBand", new Color(0.42f, 0.10f, 0.10f, 1f));
        AnchorFillRect(band.rectTransform, 0.58f, 0.66f);
    }

    void BuildHeader()
    {
        var title = CreateTxt(_root.transform, "Title", "每日登录", 56, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.5f, -60f, Y_TITLE, 420f, 72f);
        title.color = ColGold;

        var sub = CreateImg(_root.transform, "SubBar", ColGoldDeep);
        SetRect(sub.rectTransform, 0.5f, 0.5f, -110f, Y_SUB, 300f, 46f);
        var subTxt = CreateTxt(sub.transform, "Label", "领取丰厚奖励", 24, TextAnchor.MiddleCenter);
        Stretch(subTxt.rectTransform);
        subTxt.color = new Color(0.16f, 0.06f, 0.04f, 1f);

        // 提示卡（标题右下；宽度按窄屏 576 留边：半宽 288，x=150 时最宽 270）
        var tip = CreateImg(_root.transform, "TipCard", new Color(0.10f, 0.05f, 0.06f, 0.86f));
        SetRect(tip.rectTransform, 0.5f, 0.5f, 150f, Y_TIP, 270f, 92f);
        var tipTxt = CreateTxt(tip.transform, "Label", "连续登录，好礼不断！\n更有稀有角色相赠！", 18, TextAnchor.MiddleLeft);
        var trt = tipTxt.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(14f, 8f);
        trt.offsetMax = new Vector2(-14f, -8f);
        tipTxt.color = ColText;

        // 右上角关闭：全屏弹窗只给 X，不给点空白关闭（防误触把登录奖励划走）
        var close = CreateImg(_root.transform, "CloseButton", ColGoldDeep);
        var crt = close.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-28f, -28f);
        crt.sizeDelta = new Vector2(64f, 64f);
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.onClick.AddListener(Hide);
        var closeTxt = CreateTxt(close.transform, "Label", "X", 30, TextAnchor.MiddleCenter);
        Stretch(closeTxt.rectTransform);
        closeTxt.color = new Color(0.98f, 0.94f, 0.86f, 1f);
    }

    void BuildGrid()
    {
        for (int i = 0; i < DailyLoginDefs.Starter.Length; i++)
        {
            int row = i / 4;
            float y = row == 0 ? Y_ROW1 : Y_ROW2;
            var cell = CreateCell(_root.transform, "Day_" + (i + 1), 0f, y);
            cell.day = i + 1;
            _starterCells.Add(cell);
        }
    }

    Cell CreateCell(Transform parent, string name, float x, float y)
    {
        var bg = CreateImg(parent, name, ColCell);
        SetRect(bg.rectTransform, 0.5f, 0.5f, x, y, 160f, CELL_H);
        var c = new Cell { go = bg.gameObject, bg = bg };

        // 第 N 天标签条
        var tag = CreateImg(bg.transform, "DayTag", new Color(0.13f, 0.22f, 0.40f, 1f));
        AnchorTopStrip(tag.rectTransform, 36f);
        c.dayText = CreateTxt(tag.transform, "Label", "", 22, TextAnchor.MiddleCenter);
        Stretch(c.dayText.rectTransform);
        c.dayText.color = ColGold;

        // 图标区：优先真图，找不到退化为色块 + 奖励名首字
        // （注意：这里只放 Image，**不能加 Button**——加了空按钮会把整卡点击吃掉不冒泡）
        var iconHolder = CreateImg(bg.transform, "IconHolder", new Color(1f, 1f, 1f, 0.06f));
        SetRect(iconHolder.rectTransform, 0.5f, 0.5f, 0f, 34f, 96f, 96f);
        c.icon = iconHolder;
        c.iconFallback = CreateTxt(iconHolder.transform, "Fallback", "", 40, TextAnchor.MiddleCenter);
        Stretch(c.iconFallback.rectTransform);
        c.iconFallback.color = new Color(0.9f, 0.85f, 0.7f, 0.9f);

        // 奖励名（两行）
        c.rewardText = CreateTxt(bg.transform, "Reward", "", 18, TextAnchor.MiddleCenter, true);
        SetRect(c.rewardText.rectTransform, 0.5f, 0f, 0f, 34f, 150f, 52f);
        c.rewardText.color = ColText;

        // X2 角标（只有双倍日显示）
        var x2 = CreateImg(bg.transform, "X2", ColBlue);
        var xrt = x2.rectTransform;
        xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 1f);
        xrt.pivot = new Vector2(1f, 1f);
        xrt.anchoredPosition = new Vector2(-4f, -4f);
        xrt.sizeDelta = new Vector2(52f, 34f);
        var x2Txt = CreateTxt(x2.transform, "Label", "X2", 20, TextAnchor.MiddleCenter);
        Stretch(x2Txt.rectTransform);
        x2Txt.color = Color.white;
        c.x2Badge = x2.gameObject;

        // 限定角标（最后一日）
        var rare = CreateImg(bg.transform, "Rare", ColRed);
        var rrt = rare.rectTransform;
        rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 1f);
        rrt.pivot = new Vector2(1f, 1f);
        rrt.anchoredPosition = new Vector2(-4f, -4f);
        rrt.sizeDelta = new Vector2(64f, 34f);
        var rareTxt = CreateTxt(rare.transform, "Label", "限定", 20, TextAnchor.MiddleCenter);
        Stretch(rareTxt.rectTransform);
        rareTxt.color = new Color(1f, 0.9f, 0.5f, 1f);
        c.rareBadge = rare.gameObject;

        // ★ 45° 已领斜字（兜底路径同样要有，绑定路径由预制体提供）
        var markGo = new GameObject("Mark", typeof(RectTransform));
        markGo.transform.SetParent(bg.transform, false);
        markGo.layer = 5;
        var mrt = markGo.GetComponent<RectTransform>();
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.5f);
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.anchoredPosition = Vector2.zero;
        mrt.sizeDelta = new Vector2(220f, 44f);
        markGo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        c.markText = CreateTxt(markGo.transform, "Label", "已 领", 34, TextAnchor.MiddleCenter);
        Stretch(c.markText.rectTransform);
        c.markText.color = Color.white;
        var markOl = markGo.AddComponent<Outline>();
        markOl.effectColor = new Color(0f, 0f, 0f, 0.85f);
        markOl.effectDistance = new Vector2(2f, -2f);
        markGo.SetActive(false);

        c.btn = bg.gameObject.AddComponent<Button>();
        c.btn.transition = Selectable.Transition.None;
        // 同上：点的时候读 cell.day（Refresh 会把它换成累计天数）
        c.btn.onClick.AddListener(() => OnClickStarter(c.day));
        // B11: 兜底路径同样记原色，保证两条路径压暗行为一致
        CaptureCellBase(c);
        return c;
    }

    void BuildCycleLine()
    {
        var bar = CreateImg(_root.transform, "CycleBar", new Color(0.16f, 0.05f, 0.06f, 0.95f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0.03f, 0.5f);
        brt.anchorMax = new Vector2(0.97f, 0.5f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        brt.anchoredPosition = new Vector2(0f, Y_CYCLE);
        brt.sizeDelta = new Vector2(0f, 76f);

        var label = CreateTxt(bar.transform, "Tag", "每日循环", 20, TextAnchor.MiddleLeft);
        SetRect(label.rectTransform, 0f, 0.5f, 16f, 0f, 120f, 40f);
        label.color = ColGold;

        _cycleName = CreateTxt(bar.transform, "Name", "", 20, TextAnchor.MiddleLeft);
        SetRect(_cycleName.rectTransform, 0f, 0.5f, 140f, 0f, 300f, 44f);
        _cycleName.color = ColText;

        var btnImg = CreateImg(bar.transform, "ClaimBtn", ColGoldDeep);
        // 左锚（Layout 运行时按条宽摆到贴右），别用右锚，否则 Layout 的 x 换算会翻车
        SetRect(btnImg.rectTransform, 0f, 0.5f, 0f, 0f, 140f, 52f);
        _cycleBtn = btnImg.gameObject.AddComponent<Button>();
        _cycleBtn.onClick.AddListener(OnClickCycle);
        var btnTxt = CreateTxt(btnImg.transform, "Label", "领取", 22, TextAnchor.MiddleCenter);
        Stretch(btnTxt.rectTransform);
        btnTxt.color = new Color(0.16f, 0.06f, 0.04f, 1f);
        _cycleState = btnTxt;
    }

    void BuildStreakLine()
    {
        var bar = CreateImg(_root.transform, "StreakBar", new Color(0.16f, 0.05f, 0.06f, 0.95f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0.03f, 0.5f);
        brt.anchorMax = new Vector2(0.97f, 0.5f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        brt.anchoredPosition = new Vector2(0f, Y_STREAK);
        brt.sizeDelta = new Vector2(0f, 76f);

        _streakTag = CreateTxt(bar.transform, "Tag", "连击加成", 20, TextAnchor.MiddleLeft);
        SetRect(_streakTag.rectTransform, 0f, 0.5f, 16f, 0f, 150f, 40f);
        _streakTag.color = ColGold;

        for (int i = 0; i < DailyLoginDefs.Streak.Length && i < _streakBtns.Length; i++)
        {
            var s = DailyLoginDefs.Streak[i];
            var bImg = CreateImg(bar.transform, "Streak_" + s.days, new Color(0.22f, 0.18f, 0.12f, 1f));
            SetRect(bImg.rectTransform, 0f, 0.5f, 0f, 0f, 100f, 52f);
            _streakBtns[i] = bImg.gameObject.AddComponent<Button>();
            _streakLabels[i] = CreateTxt(bImg.transform, "Label", "", 15, TextAnchor.MiddleCenter);
            Stretch(_streakLabels[i].rectTransform);
            _streakLabels[i].color = ColText;
            int days = s.days;
            _streakBtns[i].onClick.AddListener(() => OnClickStreak(days));
        }
    }

    void BuildInfoBar()
    {
        var bar = CreateImg(_root.transform, "InfoBar", new Color(0.20f, 0.06f, 0.07f, 0.97f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0.03f, 0.5f);
        brt.anchorMax = new Vector2(0.97f, 0.5f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        brt.anchoredPosition = new Vector2(0f, Y_INFO);
        brt.sizeDelta = new Vector2(0f, 118f);

        // 左：累计登录 n / 8 天
        var cap = CreateTxt(bar.transform, "Cap", "累计登录", 20, TextAnchor.MiddleLeft);
        SetRect(cap.rectTransform, 0f, 0.5f, 20f, 30f, 200f, 34f);
        cap.color = ColGold;
        _infoDays = CreateTxt(bar.transform, "Days", "", 40, TextAnchor.MiddleLeft);
        SetRect(_infoDays.rectTransform, 0f, 0.5f, 20f, -18f, 320f, 56f);
        _infoDays.color = ColText;

        // 右：第 8 天限定佣兵预告（头像 + 文案）
        var iconHolder = CreateImg(bar.transform, "MercIcon", new Color(1f, 1f, 1f, 0.08f));
        SetRect(iconHolder.rectTransform, 0f, 0.5f, 340f, 0f, 88f, 88f);
        _infoMercIcon = iconHolder;
        // B2: 兜底路径也按 "MercText*" 命名，让 CollectMercTexts 能收集到（代码只改 color 绝不改 text）
        var mercTxt = CreateTxt(bar.transform, "MercText", "", 16, TextAnchor.MiddleLeft);
        SetRect(mercTxt.rectTransform, 0f, 0.5f, 396f, 0f, 420f, 80f);
        mercTxt.color = ColText;
    }
}
