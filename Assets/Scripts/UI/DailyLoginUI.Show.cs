using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：打开与弹出时机（Open/Hide、等主界面再弹、Layout 重排、节点查找工具）
public partial class DailyLoginUI
{

    // ============================================================
    // 打开 / 刷新
    // ============================================================

    void Open()
    {
        if (_root == null) Build();
        _root.SetActive(true);
        ShowVisuals();   // 2026-10-15 主人拍板：等待期/关闭期只收画面（HideVisuals）不停宿主；这里放回来
        Canvas.ForceUpdateCanvases();
        Layout();
        Refresh();
        // 竖屏背景适配：背景只做整体等比缩放（cover），其余节点保持预制体原样。
        // 此 UI 是 DontDestroyOnLoad 单例 Canvas，UICanvasSetup 覆盖不到，必须显式调。
        // 仅更瘦屏生效，标准 9:16 直接 return 不动任何东西（内部已判）。
        ApplyFit();
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        // 2026-10-15 主人拍板：只收画面、不停宿主 GO——宿主一 SetActive(false)，其上的协程当场停
        // （等主界面的 CoShowWhenMainUiIdle 死在第一个 yield），且下次请求重新激活宿主时
        // 已激活的子节点会闪一帧。显隐统一走 HideVisuals / ShowVisuals。
        HideVisuals();
        // 【2026-10-10 主人要求】「登录界面关了后应该回到主页 不应该还是天赋」——
        // 关掉登录时若人还停在天赋页，顺手把天赋页收掉，让玩家回到城镇主界面。
        if (TalentUI.Instance != null && TalentUI.Instance.IsOpen)
            TalentUI.Instance.Hide();
        // 【2026-10-16 主人拍板】玩家关掉登录界面，这时才提示「您有新的邮件」——
        // 引导收尾串行链的最后一环（欢迎信在 MarkTutorialDone 时已入邮，只登记未弹）。
        NewMailToast.FlushPending();
    }

    // ============================================================
    // 【2026-10-10 主人要求】弹出时机：等玩家回到主界面再弹
    // 主人原话「当玩家返回主界面的时候再弹出每日登录」。
    // 以前是进 Town 就弹 —— 引导刚把玩家带进天赋页时，登录会盖在天赋页上，
    // 关掉又还停在天赋页（主人原话「登录界面关了后应该回到主页 不应该还是天赋」）。
    // 现在改为：只登记，等天赋页关了（= 回到主界面）才真正打开。
    // ============================================================

    static bool _pendingShow;
    static bool _waitingIdle;

    // 2026-10-15 主人拍板「领取后就不用再弹了」：进程级标志，领过任意一笔登录奖励后本次进程不再自动弹，
    // 重启游戏后自动清零，新的一天有可领时照常弹。
    static bool _autoPopConsumed;

    /// <summary>
    /// 登记「领过一笔」——同一语义只留这一个出口，四个回调领奖成功后统一调它。
    /// 2026-10-16 修：领过就要抑制今天的自动弹出，且必须落盘。原因：
    ///   · 旧实现只在 TownSceneBootstrap 的「自动弹出」分支写 loginPopupShownDay；
    ///     走手动入口（主界面每日登录按钮）领取时该字段从不记录。重新进游戏后
    ///     static 的 _autoPopConsumed 被重置，HasClaimable() 仍为 true 且
    ///     loginPopupShownDay 为空/非今天 → 又会自动弹（即本次报的 bug）。
    ///   · 把"领过"也写入持久化的 loginPopupShownDay，保证无论自动/手动领取、
    ///     是否重启，当天都不再自动弹（手动按钮与红点提醒不受影响，仍可随时打开面板）。
    /// </summary>
    static void MarkClaimConsumed()
    {
        _autoPopConsumed = true;
        var d = SaveSystem.Instance?.Data;
        if (d != null && d.loginPopupShownDay != ShopDefs.TodayKey())
        {
            d.loginPopupShownDay = ShopDefs.TodayKey();
            SaveSystem.Instance.Save();
        }
    }

    /// <summary>登记「有可领的每日登录」，回到主界面时再弹（不在这里直接开）。</summary>
    public static void RequestShowOnMainUi()
    {
        var ui = Ensure();
        if (ui == null) return;
        // 2026-10-15 主人拍板「领取后就不用再弹了」：领过就停；
        // 没领就关掉的情况仍会保留提醒（符合主人「领取后不再弹」的字面口径）。
        if (_autoPopConsumed) return;
        _pendingShow = true;              // 静态字段：不能用实例引用访问（CS0176）
        if (!_waitingIdle)
        {
            // 【2026-10-15 主人拍板】宿主 GO 必须全程保持激活：Unity 规则——GO 一 SetActive(false)，
            // 其上的协程当场停。完整预制体路径下 _root 就是 gameObject 本身（TryBindFullPrefab :304），
            // 以前在协程里收 _root = 收整层 = 协程死在第一个 yield（天赋页开着等主界面时永远回不来），
            // 且 _waitingIdle 永远卡 true → 整个会话每日登录都弹不出来。
            // 现在：宿主激活不动，画面改由 HideVisuals 收直接子节点（同帧执行，首帧渲染前已收起，不闪）。
            ui.gameObject.SetActive(true);
            ui.HideVisuals();
            ui.StartCoroutine(ui.CoShowWhenMainUiIdle());
        }
    }

    /// <summary>
    /// 只收画面、不停宿主 GO（宿主停了协程就再也起不来）。
    /// 完整预制体路径下 _root == gameObject，收 _root 就是收整层；改为收全部直接子节点
    /// （Bg_Full / InfoBar / CloseButton / Image / CellsPanel / Title），渲染与射线一起没。
    /// </summary>
    void HideVisuals()
    {
        if (_root != null && _root != gameObject) { _root.SetActive(false); return; }
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c != null) c.gameObject.SetActive(false);
        }
    }

    /// <summary>放回画面（Open 前调；与 HideVisuals 严格对称）。</summary>
    void ShowVisuals()
    {
        if (_root != null && _root != gameObject) { _root.SetActive(true); return; }
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c != null) c.gameObject.SetActive(true);
        }
    }

    System.Collections.IEnumerator CoShowWhenMainUiIdle()
    {
        _waitingIdle = true;
        // 画面在 RequestShowOnMainUi 里已由 HideVisuals 收起（同帧、不闪）；这里只等回主界面
        while (_pendingShow)
        {
            // 【2026-10-16 主人拍板】回主界面的判定加严：天赋页关了 **且** 首次引导收完了才弹，
            // 防止引导遮罩/收尾气泡还挂着时登录弹进来交叉。
            if (!TalentPageOpen() && !GuideStillBusy())
            {
                _pendingShow = false;
                Open();
                break;
            }
            yield return new WaitForSeconds(0.25f);
        }
        _waitingIdle = false;
    }

    /// <summary>天赋页是否还开着（挡着主界面）。只判这一种：主人反馈的场景就是天赋页。</summary>
    static bool TalentPageOpen() => TalentUI.Instance != null && TalentUI.Instance.IsOpen;

    /// <summary>
    /// 首次引导（FirstRunGuide）是否还在跑。2026-10-16 串行化新增：
    /// 引导没收完（游标未到 Done）就不算回到主界面，登录弹窗继续等。
    /// 引导不存在 / 已完成的玩家恒为 false，不影响老档。
    /// </summary>
    static bool GuideStillBusy()
    {
        var g = FirstRunGuide.Instance;
        return g != null && !g.IsFinished;
    }

    /// <summary>按面板实际宽度重排 8 格与三条横带（逻辑宽 576~720 浮动，必须运行时算）。</summary>
    void Layout()
    {
        // 完整预制体路径：全部节点位置由美术预制体摆好，代码严禁反向"纠正"排版，直接跳过整段重排。
        if (_boundFull) return;
        var rt = transform as RectTransform;
        if (rt == null) return;
        float w = rt.rect.width;
        if (w <= 0f) return;

        float inner = w - 48f;
        float gap = 12f;
        float cellW = (inner - gap * 3f) / 4f;
        float left = -w / 2f + 24f;

        for (int i = 0; i < _starterCells.Count; i++)
        {
            var c = _starterCells[i];
            if (c?.bg == null) continue;
            int col = i % 4;
            float x = left + col * (cellW + gap) + cellW / 2f;
            int row = i / 4;
            c.bg.rectTransform.sizeDelta = new Vector2(cellW, CELL_H);
            c.bg.rectTransform.anchoredPosition = new Vector2(x, row == 0 ? Y_ROW1 : Y_ROW2);
        }

        LayoutBar("CycleBar", "Name", "ClaimBtn", 120f, 140f);
        LayoutBar("StreakBar", null, null, 160f, 0f);
        LayoutStreakButtons("StreakBar");
        LayoutInfoBar("InfoBar");
    }

    /// <summary>横带内部排版：左标签固定、中段文字弹性、右按钮贴边。</summary>
    void LayoutBar(string barName, string nameNode, string btnNode, float tagW, float btnW)
    {
        var bar = FindChild(_root.transform, barName) as RectTransform;
        if (bar == null) return;
        float wBar = bar.rect.width;
        if (wBar <= 0f) return;

        if (!string.IsNullOrEmpty(nameNode))
        {
            var nameT = FindChild(bar, nameNode) as RectTransform;
            if (nameT != null)
            {
                float x = tagW + 20f;
                float wName = wBar - x - btnW - 32f;
                nameT.anchoredPosition = new Vector2(x + wName * 0.5f, nameT.anchoredPosition.y);
                nameT.sizeDelta = new Vector2(Mathf.Max(120f, wName), nameT.sizeDelta.y);
            }
        }

        if (!string.IsNullOrEmpty(btnNode))
        {
            var btnT = FindChild(bar, btnNode) as RectTransform;
            if (btnT != null)
                btnT.anchoredPosition = new Vector2(wBar - btnW / 2f - 16f, btnT.anchoredPosition.y);
        }
    }

    void LayoutStreakButtons(string barName)
    {
        var bar = FindChild(_root.transform, barName) as RectTransform;
        if (bar == null) return;
        float wBar = bar.rect.width;
        if (wBar <= 0f) return;

        float tagW = 160f, edge = 16f, gap = 10f;
        float usable = wBar - tagW - edge * 2f - gap * 3f;
        float bw = usable / 4f;
        for (int i = 0; i < _streakBtns.Length; i++)
        {
            if (_streakBtns[i] == null) continue;
            var brt = _streakBtns[i].GetComponent<RectTransform>();
            float x = -wBar / 2f + tagW + edge + bw / 2f + i * (bw + gap);
            brt.anchoredPosition = new Vector2(x, 0f);
            brt.sizeDelta = new Vector2(bw, 52f);
        }
    }

    void LayoutInfoBar(string barName)
    {
        var bar = FindChild(_root.transform, barName) as RectTransform;
        if (bar == null) return;
        float wBar = bar.rect.width;
        if (wBar <= 0f) return;

        float iconX = wBar * 0.40f;
        var iconT = FindChild(bar, "MercIcon") as RectTransform;
        if (iconT != null) iconT.anchoredPosition = new Vector2(iconX, 0f);

        var textT = FindChild(bar, "MercText") as RectTransform;
        if (textT != null)
        {
            float x = iconX + 56f;
            float wTxt = wBar - x - 16f;
            textT.anchoredPosition = new Vector2(x + wTxt * 0.5f, 0f);
            textT.sizeDelta = new Vector2(Mathf.Max(160f, wTxt), textT.sizeDelta.y);
        }
    }

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    /// <summary>
    /// 递归深度查找：从 parent 的每个直接子节点出发沿整棵子树 DFS，
    /// 返回第一个名字匹配的节点；找不到返回 null。
    /// 与 FindChild 只查直接子级不同，本方法能穿透 CellsPanel 这类中间容器，
    /// 因此 Cell_1~8 即便包在 CellsPanel 下也能被找到（主人预制体结构）。
    /// </summary>
    static Transform FindChildDeep(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c == null) continue;
            var found = FindChildDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 在 InfoBar 下按名字（**忽略大小写**）查找 effect / glow 子节点，任意深度 DFS。找到返回该节点，找不到返回 null。
    /// 主人新加的节点叫小写 "effect"、且可能包在深层；这里不限于直接子级、也不大小写敏感，
    /// 因此既兼容旧的 "Effect"/"Glow"，也能命中小写的 "effect"。找不到就返回 null（调用方 no-op，绝不新建节点）。
    /// </summary>
    static Transform FindInfoEffect(Transform infoBar)
    {
        if (infoBar == null) return null;
        for (int i = 0; i < infoBar.childCount; i++)
        {
            var c = infoBar.GetChild(i);
            if (c == null) continue;
            if (c.name.Equals("effect", System.StringComparison.OrdinalIgnoreCase)
                || c.name.Equals("glow", System.StringComparison.OrdinalIgnoreCase))
                return c;
            var deep = FindInfoEffect(c);   // 任意深度递归
            if (deep != null) return deep;
        }
        return null;
    }

    /// <summary>
    /// B2: 递归收集 root 下所有名字以 "MercText" 开头的 Text（主人摆好的多行文案：MercText / MercText (1) / MercText (2)）。
    /// 收集后代码只改它们的 color，绝不改 text（文案以预制体为准）。
    /// </summary>
    static void CollectMercTexts(Transform root, List<Text> outList)
    {
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c == null) continue;
            if (c.name.StartsWith("MercText") && c.GetComponent<Text>() != null)
                outList.Add(c.GetComponent<Text>());
            CollectMercTexts(c, outList);
        }
    }

    /// <summary>
    /// 在 Starter 新手奖励数组里找「第一个佣兵日」的索引（0 基）。返回 -1 表示没有佣兵日。
    /// UI 的「限定」角标与底部佣兵预告都从数据里取，不再硬编码最后一格——
    /// 主人改表（如把佣兵从 D8 挪到 D4）后 UI 自动跟随，不会再现 D8 变碎片就头像空的问题。
    /// </summary>
    static int FindStarterMercSlot()
    {
        var list = DailyLoginDefs.Starter;
        for (int i = 0; i < list.Length; i++)
            if (list[i].grant == DailyLoginDefs.Grant.Merc) return i;
        return -1;
    }
}
