using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 《跑酷小游戏》V1 —— 自建「入队横条 + 天降二选一窗口」（EndlessRunGame 分部）。
///
/// 【独立性】主人拍板：跑酷内部自成一套。本文件不引用 / 不修改任何战斗 / 结算 / 花名册 /
/// 稀有度调色板等其它玩法系统；
/// 横条底图、按钮底图、名字文案、颜色全用现成图 + 本文件内 C# 常量，UI 节点运行时挂在跑酷 Canvas 下。
/// 【时间】横条 UI 动效走 Time.unscaledDeltaTime；慢动作只由 _runTimeScale 乘在三处 tick 的 dt 上，
/// 绝不触碰全局时间缩放；窗口期 UpdateInput / OnQueueJumpEdge 早退，顶替改由按钮回调驱动。
/// 【字号】fusion-pixel，全部 12 的倍数（36 / 24）。
/// </summary>
public partial class EndlessRunGame
{
    // ============================================================
    // 资源路径（现成图，按路径加载，取不到 LogError，不兜底）
    // ============================================================
    const string SP_JOIN_BANNER_BG = "UI/Art/battle/佣兵加入提示框";
    const string SP_BTN_REPLACE = "UI/Art/Common/确定按钮";
    const string SP_BTN_KEEP = "UI/Art/Common/取消按钮";
    const string SP_RUN_FONT = "Fonts/fusion-pixel";

    // ============================================================
    // 横条规格（动画参数照战斗横条的既定规格，节点 / 文案 / 颜色全自建）
    // ============================================================
    const float RUN_SLIDE_IN_SEC = 0.30f;      // 左侧屏外 → 中间（二次缓出，起步快末端收住）
    const float RUN_CREEP_SEC = 1.60f;         // 过中间后向右极慢缓行总时长（前半段不透明 = 稍作停留）
    const float RUN_CREEP_DISTANCE = 50f;      // 过中间后缓行总距离 px
    const float RUN_BANNER_Y_OFFSET_UP = 100f; // 整体上移 100（221 + 100 = 321），播完写回原位防累加
    const float RUN_BANNER_W = 517f;
    const float RUN_BANNER_H = 189f;
    const float RUN_BANNER_HOME_Y = 221f;
    const string RUN_JOIN_LINE = "— 加入小队 —";

    // 名字 / 颜色：名字取项目花名册(MercRosterDefs) 的真实值(马库斯/洛恩/塔克)，存为本文件常量以保持跑酷独立；不运行时查花名册、不用稀有度调色板。
    const string RUN_NAME_H001 = "马库斯";
    const string RUN_NAME_H002 = "洛恩";
    const string RUN_NAME_H003 = "塔克";
    static readonly Color RUN_COLOR_H001 = new Color(1f, 0.72f, 0.20f, 1f);
    static readonly Color RUN_COLOR_H002 = new Color(0.35f, 0.90f, 0.85f, 1f);
    static readonly Color RUN_COLOR_H003 = new Color(1f, 0.50f, 0.75f, 1f);

    // ============================================================
    // 慢动作 / 窗口状态（分部共享；默认 1f）
    // ============================================================
    float _runTimeScale = 1f;           // 三处 tick 的 dt 乘它；窗口内置 slowmo_scale(0.1)
    bool _choiceOpen;                   // 天降二选一窗口是否开启
    string _choiceHireId;               // 本次窗口对应的 hireId（横条 / 队列同源）

    // ============================================================
    // 横条节点 / 状态
    // ============================================================
    RectTransform _bannerRt;
    Image _bannerIcon;
    Text _bannerName, _bannerLine;
    CanvasGroup _bannerCg;
    Vector2 _bannerHomePos;
    Coroutine _bannerCo;
    Font _runFont;

    // ============================================================
    // 二选一窗口节点
    // ============================================================
    RectTransform _choiceRoot;

    // ============================================================
    // 拾取入口（T6 PickupCoins 调）：未满员直接入队 + 横条；满员开窗口 + 慢动作 + 天降待定
    // ============================================================
    void OnPickMercHead(string hireId)
    {
        if (_choiceOpen || SuppressWaveSpawn) return;   // 防重入：天降窗口未结束不叠加第二只
        if (string.IsNullOrEmpty(hireId))
        {
            Debug.LogError("[跑酷] MercHead 拾取缺少 hireId，不弹窗口 / 不入队。");
            return;
        }
        if (_mercs.Count < 2)
        {
            EnqueueMerc(hireId);                 // 现有入口：有空槽直接天降入队（不慢放、不弹按钮）
            ShowJoinBanner(hireId);
        }
        else
        {
            OpenChoiceWindow(hireId);
        }
    }

    // ============================================================
    // 天降二选一窗口（换 / 不换）
    // ============================================================
    void OpenChoiceWindow(string hireId)
    {
        if (_choiceOpen) return;
        _choiceOpen = true;
        _choiceHireId = hireId;
        _runTimeScale = RunTrackTable.GetFloat("slowmo_scale", 0.1f);   // 0.1 倍慢动作
        EnsureChoiceWindow();
        if (_choiceRoot != null)
        {
            _choiceRoot.gameObject.SetActive(true);
            _choiceRoot.SetAsLastSibling();       // 层级高于 BuildUi 的全屏 RaycastBackdrop，否则点不到
        }
        EnqueueMerc(hireId);                     // 满员：走现有入口建 _pending（PendingDrop 天降待定）
    }

    /// <summary>关闭窗口：恢复 1.0、清窗口标志、隐藏按钮。幂等（换 / 不换 / 落地超时都调）。</summary>
    void CloseChoiceWindow()
    {
        if (!_choiceOpen) return;
        _choiceOpen = false;
        _runTimeScale = 1f;
        if (_choiceRoot != null) _choiceRoot.gameObject.SetActive(false);
    }

    /// <summary>「换」：复用替换逻辑 → 恢复 1.0 → 播横条。</summary>
    void OnChoiceReplace()
    {
        if (!_choiceOpen) return;
        ReplacePending();                       // Queue.cs：old.anim=ReplaceJumpOut / _pending.anim=Drop / Insert(0)
        string hireId = _choiceHireId;
        CloseChoiceWindow();
        ShowJoinBanner(hireId);
    }

    /// <summary>「不换」：恢复 1.0、清窗口、不播横条；_pending 继续落地后由 TickMercAnims 原地销毁。</summary>
    void OnChoiceKeep()
    {
        if (!_choiceOpen) return;
        CloseChoiceWindow();
    }

    /// <summary>重开 / 关闭时清残留（里程回落触发 ResetQueue 调），防慢动作 / 按钮卡死。</summary>
    void ResetRunOverlays()
    {
        _choiceOpen = false;
        _runTimeScale = 1f;
        if (_choiceRoot != null) _choiceRoot.gameObject.SetActive(false);
        if (_bannerRt != null) _bannerRt.gameObject.SetActive(false);
        if (_bannerCo != null) { StopCoroutine(_bannerCo); _bannerCo = null; }
    }

    // ============================================================
    // 二选一窗口建树（跑酷 Canvas 下独立建，无暗幕、无模态、不再挂嵌套画布）
    // ============================================================
    void EnsureChoiceWindow()
    {
        if (_choiceRoot != null || _canvas == null) return;
        var replaceSp = Resources.Load<Sprite>(SP_BTN_REPLACE);
        var keepSp = Resources.Load<Sprite>(SP_BTN_KEEP);
        if (replaceSp == null) Debug.LogError("[跑酷] 「换」按钮图缺失：" + SP_BTN_REPLACE);
        if (keepSp == null) Debug.LogError("[跑酷] 「不换」按钮图缺失：" + SP_BTN_KEEP);
        EnsureRunFont();

        // 根节点铺满但不挂 Image（无暗幕 / 无模态）：除两颗按钮外不拦截任何射线。
        var root = NewHudRect("RunChoiceWindow", _canvas.transform);
        StretchRect(root);
        BuildChoiceButton(root, "RunChoiceReplace", "换", replaceSp, new Vector2(-150f, 200f), OnChoiceReplace);
        BuildChoiceButton(root, "RunChoiceKeep", "不换", keepSp, new Vector2(150f, 200f), OnChoiceKeep);
        root.gameObject.SetActive(false);
        _choiceRoot = root;
    }

    void BuildChoiceButton(Transform parent, string name, string label, Sprite sp,
        Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var rt = NewHudRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(200f, 80f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp;
        img.raycastTarget = true;
        if (sp == null) img.color = Color.clear;   // 取不到图已 LogError，不摆纯色占位
        var btn = rt.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var txt = NewHudText("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            _runFont, 36, Color.white, TextAnchor.MiddleCenter);
        txt.text = label;
        StretchRect(txt.rectTransform);            // 铺满按钮；Text 不拦截射线（NewHudText 已关 raycastTarget）
    }

    // ============================================================
    // 入队横条建树
    // ============================================================
    void EnsureJoinBanner()
    {
        if (_bannerRt != null || _canvas == null) return;
        var bg = Resources.Load<Sprite>(SP_JOIN_BANNER_BG);
        if (bg == null) Debug.LogError("[跑酷横条] 底图缺失：" + SP_JOIN_BANNER_BG);
        EnsureRunFont();

        var root = NewHudRect("RunJoinBanner", _canvas.transform);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(RUN_BANNER_W, RUN_BANNER_H);
        root.anchoredPosition = new Vector2(0f, RUN_BANNER_HOME_Y);
        var bgImg = root.gameObject.AddComponent<Image>();
        bgImg.sprite = bg;
        bgImg.raycastTarget = false;
        bgImg.enabled = bg != null;                // 取不到底图不摆纯色白块（已 LogError）

        var iconRt = NewHudRect("icon", root);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(-160f, 0f);
        iconRt.sizeDelta = new Vector2(140f, 150f);
        var iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = false;                    // 有立绘才点亮，避免空图白块
        _bannerIcon = iconImg;

        _bannerName = NewHudText("name", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(60f, 22f), new Vector2(280f, 56f), _runFont, 36, Color.white, TextAnchor.MiddleCenter);
        _bannerName.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        _bannerLine = NewHudText("Text1", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(60f, -30f), new Vector2(280f, 44f), _runFont, 24,
            new Color(1f, 0.9f, 0.6f, 1f), TextAnchor.MiddleCenter);
        _bannerLine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _bannerLine.text = RUN_JOIN_LINE;

        _bannerCg = root.gameObject.AddComponent<CanvasGroup>();
        root.gameObject.SetActive(false);
        _bannerRt = root;
        _bannerHomePos = root.anchoredPosition;
    }

    void EnsureRunFont()
    {
        if (_runFont != null) return;
        _runFont = Resources.Load<Font>(SP_RUN_FONT);
        if (_runFont == null) Debug.LogError("[跑酷] fusion-pixel 字体缺失：" + SP_RUN_FONT);
    }

    // ============================================================
    // 横条播放（动画参数逐字照战斗横条 CoPlay 的既定值）
    // ============================================================
    void ShowJoinBanner(string hireId)
    {
        EnsureJoinBanner();
        if (_bannerRt == null) return;
        if (_bannerCo != null) StopCoroutine(_bannerCo);
        _bannerRt.gameObject.SetActive(true);
        _bannerRt.SetAsLastSibling();               // 高于全屏 RaycastBackdrop
        _bannerCo = StartCoroutine(CoPlayJoinBanner(hireId));
    }

    IEnumerator CoPlayJoinBanner(string hireId)
    {
        // ── 屏外准备帧：取图 / SetActive / 赋值全在屏外做完，yield return null 后才起跑（防卡一下）──
        var portrait = MercPortraitSprites.GetStand(hireId);
        if (portrait == null)
            Debug.LogError("[跑酷横条] 立绘缺失：MercPortraitSprites.GetStand(" + hireId + ") 返回 null。");
        var canvasRt = _canvas != null ? _canvas.transform as RectTransform : null;
        float canvasW = canvasRt != null ? canvasRt.rect.width : Screen.width;
        float offscreen = canvasW * 0.5f + _bannerRt.rect.width;   // 整块完全在屏外

        Vector2 rawPos = _bannerHomePos;                            // 原位（播完写回它，防偏移累加）
        Vector2 basePos = new Vector2(rawPos.x, rawPos.y + RUN_BANNER_Y_OFFSET_UP);
        _bannerRt.anchoredPosition = new Vector2(-offscreen, basePos.y);
        _bannerCg.alpha = 1f;
        if (_bannerIcon != null)
        {
            _bannerIcon.sprite = portrait;
            _bannerIcon.enabled = portrait != null;
        }
        if (_bannerName != null)
        {
            _bannerName.text = RunDisplayName(hireId);
            _bannerName.color = RunNameColor(hireId);
        }
        yield return null;   // ← 准备帧到此为止，下一帧才开始滑入

        // ① 左侧屏外 → 中间（起步快、末端收住，二次缓出）
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / RUN_SLIDE_IN_SEC;
            float e = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            _bannerRt.anchoredPosition = new Vector2(Mathf.Lerp(-offscreen, basePos.x, e), basePos.y);
            yield return null;
        }

        // ② 不停步但明显放慢：向右极慢缓行 50px；前半段完全不透明，后半段才渐隐
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / RUN_CREEP_SEC;
            float k = Mathf.Clamp01(t);
            float e = 1f - (1f - k) * (1f - k);   // 二次缓出：越走越慢
            _bannerCg.alpha = k <= 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            _bannerRt.anchoredPosition = new Vector2(basePos.x + RUN_CREEP_DISTANCE * e, basePos.y);
            yield return null;
        }
        _bannerCg.alpha = 1f;                       // 复位，避免下次播放半透明

        _bannerRt.anchoredPosition = rawPos;        // 写回原位（不是带偏移的 basePos，防 YOffset 累加）
        _bannerRt.gameObject.SetActive(false);
        _bannerCo = null;
    }

    // ============================================================
    // 内部名字 / 颜色：名字值取自项目花名册(MercRosterDefs D 行)，不运行时耦合花名册
    // ============================================================
    static string RunDisplayName(string hireId)
    {
        switch (hireId)
        {
            case "H001": return RUN_NAME_H001;
            case "H002": return RUN_NAME_H002;
            case "H003": return RUN_NAME_H003;
            default:
                Debug.LogError("[跑酷横条] 未登记的 hireId（不查花名册、不兜底造名）：" + hireId);
                return hireId ?? "";
        }
    }

    static Color RunNameColor(string hireId)
    {
        switch (hireId)
        {
            case "H001": return RUN_COLOR_H001;
            case "H002": return RUN_COLOR_H002;
            case "H003": return RUN_COLOR_H003;
            default:
                Debug.LogError("[跑酷横条] 未登记的 hireId 颜色（不用稀有度调色板）：" + hireId);
                return Color.white;
        }
    }
}
