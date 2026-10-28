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

        // 【2026-10-28 主人拍板】弹窗内指路气泡（金框+气泡）作废：与弹窗排版叠在一起
        // （气泡顶到标题横幅上）。现在引导收尾直接回主界面，登录弹窗照常自动打开，
        // 玩家愿意点就点。EnsureLoginGuide 整套代码保留不再调用（死代码，见待办清单）。
        // EnsureLoginGuide();

        // 【2026-10-24 诊断·主人报「弹窗点不动」】弹窗在屏期间开点击透视取证（600s 自过期，重复弹只顺延）：
        // 复现时点一下格子或关闭按钮，Editor.log 的 [ClickPeek] 行当场报出射线最顶层打到了谁——
        // top=Cell_N/CloseButton → 点击其实到了按钮（查 EventSystem 派发层）；
        // top=别的节点 → 隐形遮挡当场报名字；一条 [ClickPeek] 都没有 → Input 层问题。
        FirstRunGuide.BeginClickPeek(600f);
    }

    public void Hide()
    {
        RemoveLoginGuide();   // 【2026-10-20】没领就关窗：只撤画面不置位，下次打开弹窗还指
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
        // 【2026-10-20 弹窗内新手指路】领到任意一笔就算教完：气泡立刻撤、本进程不再出现
        _guideConsumed = true;
        Instance?.RemoveLoginGuide();
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
    /// 弹窗画面此刻是否在屏上（2026-10-20 加）。宿主 GO 常驻激活（协程挂上面），显隐只动
    /// 子节点：完整预制体路径 HideVisuals 收全部直接子节点、兜底路径收 _root——所以
    /// 「有任一活着的直接子节点」＝画面在屏，两条路径都成立。TownIdleNudge 等闲置引导据此让位。
    /// </summary>
    public static bool PopupVisible
    {
        get
        {
            var ui = Instance;
            if (ui == null) return false;
            for (int i = 0; i < ui.transform.childCount; i++)
                if (ui.transform.GetChild(i).gameObject.activeInHierarchy) return true;
            return false;
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

    // ============================================================
    // 弹窗内新手指路（2026-10-20 主人拍板）：
    // 新手引导不加「领取每日登录」步骤（登录弹窗 TownPopup=900 会盖住引导遮罩 620，无法并轨），
    // 改成弹窗内自带一次指路：金框圈住第一格「今天可领」的签到格子 + 赛琳口吻气泡
    // （2026-09-23 拍板后弹窗预制体已无 CycleBar/ClaimBtn，领取入口就是格子本身），
    // 只有「首次引导本会话刚出师」的新玩家能看到；领到任意一笔立刻撤、本进程不再出现。
    // ============================================================

    /// <summary>指路气泡文案（赛琳口吻，与 FirstRunGuide 收尾语气一致）。</summary>
    const string LoginGuideText = "点这个亮着的格子，今天的签到奖励就到手了。以后每天都来，别偷懒。";
    const float LoginGuideRingWidth = 5f;
    const float LoginGuideFontSize = 32f;
    static readonly Color LoginGuideRingColor = new Color(1f, 0.84f, 0.42f, 1f);
    static readonly Color LoginGuideBubbleColor = new Color(0.08f, 0.09f, 0.14f, 0.92f);
    static readonly Color LoginGuideTextColor = new Color(0.99f, 0.95f, 0.85f, 1f);

    RectTransform _guideLayer;
    /// <summary>本进程已指路过（领过任意一笔即置位），之后重开弹窗不再出现。</summary>
    static bool _guideConsumed;

    /// <summary>Open() 末尾调：满足「新玩家刚出师 + 还有可领奖励 + 本进程没指路过」才挂一次指路气泡。</summary>
    void EnsureLoginGuide()
    {
        if (_guideConsumed) return;
        if (!FirstRunGuide.FinishedThisSession) return;   // 老玩家/重启后的玩家不打扰
        if (!DailyLoginSystem.HasClaimable()) return;     // 没东西可领就不指
        if (_guideLayer != null) return;

        // 目标 = 8 日格里第一格「今天可领」的格子（弹窗预制体 2026-09-23 起就没有 CycleBar/ClaimBtn，
        // 领取入口是格子本身；Open() 里 Refresh 已跑完，cell.day 是轮回后的真实累计天数）。
        // 找不到可领格子就不挂，绝不新建按钮。
        RectTransform claimRt = null;
        int loginDays = DailyLoginSystem.LoginDays;
        for (int i = 0; i < _starterCells.Count; i++)
        {
            var c = _starterCells[i];
            if (c?.bg == null) continue;
            if (loginDays < c.day || DailyLoginSystem.IsStarterClaimed(c.day)) continue;
            if (!c.bg.gameObject.activeInHierarchy) continue;
            claimRt = c.bg.rectTransform;
            break;
        }
        if (claimRt == null) return;

        var rootRt = transform as RectTransform;
        if (rootRt == null || rootRt.rect.width <= 0f) return;
        var canvas = GetComponent<Canvas>();
        Camera cam = canvas != null && canvas.worldCamera != null
            ? canvas.worldCamera : UICanvasSetup.ResolveUiCamera();
        if (cam == null) return;

        // 可领格子四角 → 弹窗根局部坐标（弹窗与格子同一个 Canvas，Open 时布局已回流，
        // 一次换算即可，不需要每帧跟随）
        var corners = new Vector3[4];
        claimRt.GetWorldCorners(corners);
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRt, RectTransformUtility.WorldToScreenPoint(cam, corners[i]), cam, out local))
                return;
            minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y); maxY = Mathf.Max(maxY, local.y);
        }

        // 引导层：铺满根、置于弹窗最上层、所有元素不接射线（不挡玩家点领取/关闭）
        var layerGo = new GameObject("DailyLoginGuide", typeof(RectTransform));
        layerGo.transform.SetParent(transform, false);
        layerGo.layer = 5;
        _guideLayer = layerGo.GetComponent<RectTransform>();
        Stretch(_guideLayer);
        layerGo.transform.SetAsLastSibling();

        const float pad = 10f;
        float x0 = minX - pad, x1 = maxX + pad, y0 = minY - pad, y1 = maxY + pad;
        PlaceGuideStrip(NewGuideImage("RingTop"), x0 - LoginGuideRingWidth, x1 + LoginGuideRingWidth, y1, y1 + LoginGuideRingWidth);
        PlaceGuideStrip(NewGuideImage("RingBottom"), x0 - LoginGuideRingWidth, x1 + LoginGuideRingWidth, y0 - LoginGuideRingWidth, y0);
        PlaceGuideStrip(NewGuideImage("RingLeft"), x0 - LoginGuideRingWidth, x0, y0, y1);
        PlaceGuideStrip(NewGuideImage("RingRight"), x1, x1 + LoginGuideRingWidth, y0, y1);

        // 气泡：借 FirstRunGuide 同款「引导底框」图（取不到退深色块）；贴高亮框上方，顶部放不下翻下方，最后夹进屏幕
        var bubble = NewGuideImage("Bubble");
        var plate = FirstRunGuide.LoadGuidePlateSprite();
        if (plate != null)
        {
            bubble.sprite = plate;
            bubble.type = Image.Type.Simple;
            bubble.color = Color.white;
        }
        else
        {
            bubble.color = LoginGuideBubbleColor;
        }
        var brt = bubble.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        float halfH = 52f;
        float halfW = Mathf.Max(320f, rootRt.rect.width - 40f) * 0.5f;
        float bx = Mathf.Clamp((x0 + x1) * 0.5f, rootRt.rect.xMin + halfW + 8f, rootRt.rect.xMax - halfW - 8f);
        float by = y1 + 24f + halfH;
        if (by + halfH > rootRt.rect.yMax - 8f)
            by = y0 - 24f - halfH;
        by = Mathf.Clamp(by, rootRt.rect.yMin + halfH + 8f, rootRt.rect.yMax - halfH - 8f);
        brt.anchoredPosition = new Vector2(bx, by);
        brt.sizeDelta = new Vector2(halfW * 2f, halfH * 2f);

        var txt = NewGuideText(brt, "Text", LoginGuideText);
        var trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(26f, 8f);
        trt.offsetMax = new Vector2(-26f, -8f);

        GameFonts.ApplyToHierarchy(layerGo.transform);
    }

    /// <summary>撤指路层：领取成功（MarkClaimConsumed）与关窗（Hide）都调，幂等。</summary>
    void RemoveLoginGuide()
    {
        if (_guideLayer != null)
        {
            Destroy(_guideLayer.gameObject);
            _guideLayer = null;
        }
    }

    Image NewGuideImage(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_guideLayer, false);
        go.layer = 5;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    Text NewGuideText(Transform parent, string name, string content)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = (int)LoginGuideFontSize;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = LoginGuideTextColor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    void PlaceGuideStrip(Image img, float xmin, float xmax, float ymin, float ymax)
    {
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((xmin + xmax) * 0.5f, (ymin + ymax) * 0.5f);
        rt.sizeDelta = new Vector2(Mathf.Max(0f, xmax - xmin), Mathf.Max(0f, ymax - ymin));
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
