using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽中瞬间的「开奖卡」演出（2026-10-12 主人拍板）。
///
/// <para>背景：主人原话「现在没有抽中奖的喜悦 玩家对抽奖这个没有概念 怎么优化一下」。
/// 排查下来抽中的**全部反馈只有一句 Toast**（「恭喜获得 XXX」，一闪就没了）——
/// 抽奖作为主玩法却没有任何「开奖」的仪式感，玩家自然对它没概念。</para>
///
/// <para>做法：抽中生效后在屏幕中上方弹出一张卡（名字 + 类型 + 稀有度配色），
/// 弹入 → 停留 → 淡出，**整套控制在 1.0~1.5 秒内**，不拖慢连抽节奏
/// （与 2026-09-29 主人定的「点了直接出结果、一秒不等」不冲突：那是**出结果前**不等待，
/// 这是出结果**之后**的演出）。</para>
///
/// <para>稀有度分档（唯一出口 <see cref="TierOf"/>）：</para>
/// <para>· 普通：白灰边，停 0.6s；</para>
/// <para>· 稀有：蓝边，停 0.75s；</para>
/// <para>· 传说：金边 + 更亮光晕 + **全屏轻闪一下**，停 1.1s。</para>
/// <para>2026-10-13 抽奖优化 #2：三档无史诗 —— <c>HoldEpic</c> 已停用（史诗档仅旧存档回退路径可达，落入普通档停留）；
/// 卡面新增第 3 行（品质词 · 战力 +N · 称赞词），卡体放大到 460×156 居中偏上。</para>
///
/// <para>实现约定（沿用 EquipStatTipUI / SkillInfoTipUI 同一套）：运行时建树、
/// 不落预制体、不建美术、不 Resources.Load 图片；不吃射线（免得挡住连抽按钮）。</para>
/// </summary>
public class DraftPrizePopupUI : MonoBehaviour
{
    public static DraftPrizePopupUI Instance { get; private set; }

    const float CardWidth = 460f;
    const float CardHeight = 156f;
    /// <summary>弹入时长（秒）：scale 0.55 → 1.12。</summary>
    const float InDur = 0.14f;
    /// <summary>弹入过冲回落到 1.0 的时长（秒）。</summary>
    const float SettleDur = 0.08f;
    /// <summary>淡出时长（秒）：轻微放大 + 整体透明。</summary>
    const float OutDur = 0.18f;
    /// <summary>卡片中心离屏幕中心的垂直偏移（像素，锚点居中）。2026-10-13 #2：卡体放大后居中偏上，不压顶栏 HUD。</summary>
    const float CardOffsetY = 150f;

    // 各档停留时长（秒）：稀有度越高停越久，传说最久也就 1.1s，不拖节奏。
    // 【2026-10-13 #2】HoldEpic（0.90s）已停用：品质只有三档（普通/稀有/传说），无史诗。
    const float HoldCommon = 0.60f;
    const float HoldRare = 0.75f;
    const float HoldLegend = 1.10f;

    /// <summary>全屏轻闪（仅传说）的峰值不透明度与时长。【2026-10-13 #8】0.28 → 0.42（峰值仍 ≤0.45，光敏安全硬约束）。</summary>
    const float FlashAlpha = 0.42f;
    /// <summary>全屏轻闪时长（三角波）。【2026-10-13 #8】260ms（≤300ms 硬约束）。</summary>
    const float FlashDur = 0.26f;

    RectTransform _cardRt;
    CanvasGroup _cg;
    Image _frame;
    Image _glow;
    Image _icon;
    Text _kindText;
    Text _nameText;
    /// <summary>第 3 行（2026-10-13 #2）：品质词 · 战力 +N · 称赞词，富文本分色。</summary>
    Text _thirdText;
    Image _flash;
    Coroutine _co;
    /// <summary>【2026-10-13 #8】边框白闪协程（传说专属），打断/销毁时须停掉，免得旧卡的闪覆盖新卡。</summary>
    Coroutine _frameFlashCo;
    /// <summary>
    /// 演出结束的回调（2026-10-13 #1）：BattleManager 的延后 Toast 等这个唯一通知再弹，不写死时长。
    /// 被下一张卡打断时同样会补发一次（连抽别把上一张的 Toast 吞了）。
    /// </summary>
    System.Action _onFinished;

    /// <summary>【2026-10-13 #8】传说全屏闪/震屏的最小节流护栏：距上次 &lt;3s 不重复闪（光敏安全，完整节流进第二阶段）。</summary>
    static float _lastLegendFxTime = -10f;
    /// <summary>【2026-10-13 #9】上一次开奖时刻（unscaled），连抽 &lt;2.5s 时演出降一档（只降演出不降信息）。</summary>
    static float _lastShowTime = -10f;

    /// <summary>
    /// 卡框美术（2026-10-13 主人拍板：「获得装备什么的那个弹窗的底框用项目的素材 不要纯色块」）。
    /// 用项目标配深色金框 —— 概率公示弹窗 / 装备掉落弹窗 / 替换确认弹窗都是这张（原在
    /// Assets/Art/UI/NavCharacter/，为供运行时 Resources.Load 已按铁律4同名挪进
    /// Resources/UI/NavCharacter/，guid 未动，5 个预制体引用不受影响）。
    /// 九宫格（spriteBorder）已在导入设置里配好：Sliced 拉伸不变形。
    /// </summary>
    const string FrameArtRes = "UI/NavCharacter/角色_0002s_0010_图层-7";

    /// <summary>稀有度档次（0 普通 / 1 稀有 / 2 史诗 / 3 传说）—— 决定停留时长与光效。</summary>
    public const int TierCommon = 0;
    public const int TierRare = 1;
    public const int TierEpic = 2;
    public const int TierLegend = 3;

    // ============================================================
    // 对外入口（唯一）
    // ============================================================

    /// <summary>
    /// 弹一张开奖卡。重复调用会打断上一次（连抽时不会被前一张卡拖住）。
    /// </summary>
    /// <param name="parent">挂点；传 null 时自动找场景里的 Canvas 根。</param>
    /// <param name="title">奖励名（装备名 / 技能名 / 佣兵名）。</param>
    /// <param name="kindLabel">类型行文案，如「获得装备」。</param>
    /// <param name="tint">稀有度主色（走 <see cref="RarityPalette"/>，别自己抄色值）。</param>
    /// <param name="icon">图标；null 时不显示图标位（不兜底造图）。</param>
    /// <param name="powerDelta">【2026-10-13 #2】实际生效战力增量（BattleManager 发奖后算的差，非卡面预估值）；≤0 不显示「战力 +0」。</param>
    /// <param name="rarityLabel">【2026-10-13 #2】第 3 行品质词（调用方走 <see cref="SkillRarityUtil.DisplayName"/>，装备先过 <see cref="DraftPool.MapRarity"/>）；金币卡传 null。</param>
    /// <param name="note">【2026-10-13 #2/#4】第 3 行特例文案（装备被拒换 = 「已拆成强化石」；金币 = 「回血 N」），替换战力/称赞段；null 走常规段。</param>
    /// <param name="onFinished">【2026-10-13 #1】演出结束（淡出完成 / 被下一张打断）时回调一次 —— 延后 Toast 等这个唯一通知再弹。</param>
    public static void Show(Transform parent, string title, string kindLabel, Color tint, Sprite icon, int tier,
                            int powerDelta, string rarityLabel, string note, System.Action onFinished = null)
    {
        if (parent == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[DraftPrizePopup] 找不到任何 Canvas 挂点，开奖卡弹不出来");
                return;
            }
            parent = canvas.transform;
        }
        var ui = Ensure(parent);
        if (ui == null) return;
        ui.Play(title, kindLabel, tint, icon, tier, powerDelta, rarityLabel, note, onFinished);
    }

    static DraftPrizePopupUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("DraftPrizePopupUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<DraftPrizePopupUI>();
        Instance.Build();
        return Instance;
    }

    // ============================================================
    // 建树
    // ============================================================

    void Build()
    {
        var rootRt = GetComponent<RectTransform>();
        Stretch(rootRt);

        // 与 EquipStatTipUI / SkillInfoTipUI 同口径：自带嵌套 Canvas 抬到 1000，压在所有战斗层之上。
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        // 刻意不加 GraphicRaycaster：卡是纯演出，不吃射线，别挡住连抽按钮。

        // 全屏轻闪层（只有传说档才亮）
        var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        flashGo.transform.SetParent(transform, false);
        Stretch(flashGo.GetComponent<RectTransform>());
        _flash = flashGo.GetComponent<Image>();
        _flash.color = new Color(1f, 0.92f, 0.6f, 0f);
        _flash.raycastTarget = false;
        _flash.gameObject.SetActive(false);

        // 卡片
        var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
        card.transform.SetParent(transform, false);
        _cardRt = card.GetComponent<RectTransform>();
        // 【2026-10-13 #2】卡体放大到 460×156 并改**居中锚**（0.5,0.5）+ 偏上 150px：
        // 1280/1600 两档屏型都不压顶栏 HUD；旧写法锚上中会在矮屏上贴顶。
        _cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        _cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        _cardRt.pivot = new Vector2(0.5f, 0.5f);
        _cardRt.anchoredPosition = new Vector2(0f, CardOffsetY);
        _cardRt.sizeDelta = new Vector2(CardWidth, CardHeight);
        _cg = card.GetComponent<CanvasGroup>();

        // 外圈光晕（比卡大一圈，史诗/传说才开）
        var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(_cardRt, false);
        Stretch(glowGo.GetComponent<RectTransform>());
        var grt = glowGo.GetComponent<RectTransform>();
        grt.offsetMin = new Vector2(-18f, -18f);
        grt.offsetMax = new Vector2(18f, 18f);
        _glow = glowGo.GetComponent<Image>();
        _glow.raycastTarget = false;
        _glow.gameObject.SetActive(false);
        glowGo.transform.SetAsFirstSibling();

        // 卡底 + 卡框
        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(_cardRt, false);
        Stretch(bgGo.GetComponent<RectTransform>());
        var bg = bgGo.GetComponent<Image>();
        bg.color = new Color(0.10f, 0.10f, 0.13f, 0.94f);
        bg.raycastTarget = false;

        var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frameGo.transform.SetParent(_cardRt, false);
        Stretch(frameGo.GetComponent<RectTransform>());
        _frame = frameGo.GetComponent<Image>();
        _frame.raycastTarget = false;
        _frame.color = Color.white;
        // 2026-10-13 主人拍板：卡框改用项目素材（深色金框），不再只有纯色块。
        // 缺图只 LogError、不静默兜底（铁律：缺真值要喊出来）；Sliced 走导入设置里配好的九宫格。
        _frame.sprite = Resources.Load<Sprite>(FrameArtRes);
        if (_frame.sprite == null)
            Debug.LogError("[DraftPrizePopup] 卡框素材缺失：" + FrameArtRes
                           + "（应按铁律4从 Assets/Art/UI/NavCharacter/ 同名挪入 Resources/UI/NavCharacter/）");
        else
            _frame.type = Image.Type.Sliced;

        // 图标位（左）
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(_cardRt, false);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(16f, 0f);
        irt.sizeDelta = new Vector2(84f, 84f);
        _icon = iconGo.GetComponent<Image>();
        _icon.raycastTarget = false;
        _icon.preserveAspect = true;

        // 三行文字：左边让开图标位（16+84=100，从 112 起），右边内缩 16（可用宽 332px，放得下 4 位数战力）。
        // 卡高 156：kind 距顶 16 高 28；name 居中一带（与图标中线对齐）；third 距底 14 高 30。
        _kindText = MakeText(_cardRt, "Kind", 20, TextAnchor.MiddleLeft, new Vector2(112f, 112f), new Vector2(-16f, -16f));
        _nameText = MakeText(_cardRt, "Name", 26, TextAnchor.MiddleLeft, new Vector2(112f, 48f), new Vector2(-16f, -46f));
        // 【2026-10-13 #2】第 3 行：品质词 · 战力 +N · 称赞词（24px，富文本分色在 Play 里拼）
        // 右内缩 -16 → -90（2026-10-13 换金框后）：卡框右下角有角饰带（右侧约 83px 宽），
        // 第 3 行贴底最容易和它撞上，左边让到 x≤370；kind / name 在竖直中段，不撞角饰，仍 -16。
        _thirdText = MakeText(_cardRt, "Third", 24, TextAnchor.MiddleLeft, new Vector2(112f, 14f), new Vector2(-90f, -112f));

        gameObject.SetActive(false);
    }

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return t;
    }

    // ============================================================
    // 播放
    // ============================================================

    void Play(string title, string kindLabel, Color tint, Sprite icon, int tier,
              int powerDelta, string rarityLabel, string note, System.Action onFinished)
    {
        // 【2026-10-13 #1】上一张卡还在演就被打断 = 它的演出到此为止 —— 先把它的结束通知补发出去，
        // 连抽时不会把上一张的延后 Toast 吞掉（新卡的 Toast 由新卡自己那轮结束再发）。
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
            var prev = _onFinished;
            _onFinished = null;
            prev?.Invoke();
        }
        // 【2026-10-13 #8】上一张卡的边框白闪若还在跑，一并停掉，免得把新卡的框色刷回去。
        if (_frameFlashCo != null)
        {
            StopCoroutine(_frameFlashCo);
            _frameFlashCo = null;
            _frame.color = tint;
        }

        _frame.color = tint;
        _kindText.text = kindLabel ?? "";
        _kindText.color = Color.Lerp(tint, Color.white, 0.45f);
        _nameText.text = string.IsNullOrEmpty(title) ? "获得奖励" : title;
        _nameText.color = tint;

        // 【2026-10-13 #2】第 3 行：品质词（品质色）· 战力 +N（固定绿 #7CE07C）· 称赞词（固定灰）。
        // 特例：note 非空（装备被拒换「已拆成强化石」/ 金币「回血 N」）时替换战力+称赞段；
        // powerDelta ≤0 且无 note 时只留品质词 —— 任何一抽都不显示「战力 +0」。
        // 色值只问 RarityPalette；称赞词只问 PlayerPowerHud.PraiseOf（唯一出口）。
        string third = "";
        if (!string.IsNullOrEmpty(rarityLabel))
            third += "<color=#" + ColorUtility.ToHtmlStringRGB(tint) + ">" + rarityLabel + "</color>";
        if (!string.IsNullOrEmpty(note))
        {
            if (third.Length > 0) third += " · ";
            third += "<color=#" + ColorUtility.ToHtmlStringRGB(RarityPalette.Common) + ">" + note + "</color>";
        }
        else if (powerDelta > 0)
        {
            if (third.Length > 0) third += " · ";
            third += "<color=#" + ColorUtility.ToHtmlStringRGB(RarityPalette.PowerGain) + ">战力 +" + powerDelta + "</color>";
            third += " · <color=#" + ColorUtility.ToHtmlStringRGB(RarityPalette.Common) + ">"
                  + PlayerPowerHud.PraiseOf(powerDelta) + "</color>";
        }
        _thirdText.text = third;

        bool hasIcon = icon != null;
        _icon.gameObject.SetActive(hasIcon);
        if (hasIcon)
        {
            _icon.sprite = icon;
            _icon.color = Color.white;
        }

        // 光晕：史诗起才开，传说更亮
        bool glow = tier >= TierEpic;
        _glow.gameObject.SetActive(glow);
        if (glow)
            _glow.color = new Color(tint.r, tint.g, tint.b, tier >= TierLegend ? 0.42f : 0.24f);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        _onFinished = onFinished;
        _co = StartCoroutine(CoPlay(tier));
    }

    IEnumerator CoPlay(int tier)
    {
        // 【2026-10-13 #2】HoldEpic 停用：三档无史诗，Epic 档（仅旧存档回退路径可达）落入普通档停留。
        float hold = HoldCommon;
        if (tier == TierRare) hold = HoldRare;
        else if (tier == TierLegend) hold = HoldLegend;

        // 【2026-10-13 #9】连抽降档：距上次开奖 <2.5s 时演出降一档（只压停留时长，第 3 行信息不降）。
        float sinceLast = Time.unscaledTime - _lastShowTime;
        _lastShowTime = Time.unscaledTime;
        bool rapid = sinceLast >= 0f && sinceLast < 2.5f;
        if (rapid) hold = Mathf.Min(hold, HoldCommon);

        _cg.alpha = 1f;
        _cardRt.localScale = Vector3.one * 0.55f;

        // 弹入
        yield return ScaleOver(InDur, 0.55f, 1.12f);
        // 回落
        yield return ScaleOver(SettleDur, 1.12f, 1.0f);

        // 【2026-10-13 #8】传说专属爆点：全屏闪 + 震屏 + 边框白闪×3。
        // 全屏闪/震屏只跟品质=传说，不参与「按战力增量取 Max」（金色全屏闪是传说的专属视觉签名）。
        // ≥3s 最小节流护栏：连抽传说不连发闪屏（光敏安全）。
        if (tier == TierLegend)
        {
            float now = Time.unscaledTime;
            if (now - _lastLegendFxTime >= 3f)
            {
                _lastLegendFxTime = now;
                CombatJuice.Shake(0.18f, 0.12f);
                StartCoroutine(CoFlash());
                _frameFlashCo = StartCoroutine(CoFrameFlash());
            }
        }

        // 史诗/传说：光晕脉动
        float t = 0f;
        while (t < hold)
        {
            t += Time.unscaledDeltaTime;
            if (_glow != null && _glow.gameObject.activeSelf)
            {
                float baseA = tier >= TierLegend ? 0.42f : 0.24f;
                float a = baseA * (0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
                var c = _glow.color;
                c.a = a;
                _glow.color = c;
            }
            yield return null;
        }

        // 淡出
        float u = 0f;
        while (u < OutDur)
        {
            u += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(u / OutDur);
            _cg.alpha = 1f - k;
            _cardRt.localScale = Vector3.one * (1f + 0.08f * k);
            yield return null;
        }

        _co = null;
        gameObject.SetActive(false);
        // 【2026-10-13 #1】演出结束的唯一通知：延后 Toast 等这一下再弹（与停留时长常量同源，不写死秒数）。
        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
    }

    IEnumerator CoFlash()
    {
        _flash.gameObject.SetActive(true);
        float t = 0f;
        while (t < FlashDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FlashDur);
            // 0 → 峰值 → 0（三角波）
            float a = k < 0.5f ? (k / 0.5f) : (1f - (k - 0.5f) / 0.5f);
            _flash.color = new Color(1f, 0.92f, 0.6f, a * FlashAlpha);
            yield return null;
        }
        _flash.gameObject.SetActive(false);
    }

    /// <summary>
    /// 【2026-10-13 #8】传说档边框 3 次 60ms 白闪。每帧先记基准色（Play 里设的 tint），
    /// 闪完恢复，绝不留白框；被打断（Play 已 StopCoroutine）即止。
    /// </summary>
    IEnumerator CoFrameFlash()
    {
        if (_frame == null) { _frameFlashCo = null; yield break; }
        Color baseTint = _frame.color;
        for (int i = 0; i < 3; i++)
        {
            _frame.color = Color.white;
            yield return new WaitForSecondsRealtime(0.06f);
            _frame.color = baseTint;
            yield return new WaitForSecondsRealtime(0.06f);
        }
        _frameFlashCo = null;
    }

    IEnumerator ScaleOver(float dur, float from, float to)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            _cardRt.localScale = Vector3.one * Mathf.Lerp(from, to, k);
            yield return null;
        }
        _cardRt.localScale = Vector3.one * to;
    }

    void OnDestroy()
    {
        // 演出途中就被销毁（切场景 / 挂点被拆）= 演出到此为止 —— 结束通知照发，别把延后 Toast 吞了（2026-10-13 #1）
        if (_co != null)
        {
            _co = null;
            var cb = _onFinished;
            _onFinished = null;
            cb?.Invoke();
        }
        if (Instance == this) Instance = null;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
