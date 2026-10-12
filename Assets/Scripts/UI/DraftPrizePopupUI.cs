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
/// <para>· 史诗：紫边 + 外圈光晕，停 0.9s；</para>
/// <para>· 传说：金边 + 更亮光晕 + **全屏轻闪一下**，停 1.1s。</para>
///
/// <para>实现约定（沿用 EquipStatTipUI / SkillInfoTipUI 同一套）：运行时建树、
/// 不落预制体、不建美术、不 Resources.Load 图片；不吃射线（免得挡住连抽按钮）。</para>
/// </summary>
public class DraftPrizePopupUI : MonoBehaviour
{
    public static DraftPrizePopupUI Instance { get; private set; }

    const float CardWidth = 300f;
    const float CardHeight = 132f;
    /// <summary>弹入时长（秒）：scale 0.55 → 1.12。</summary>
    const float InDur = 0.14f;
    /// <summary>弹入过冲回落到 1.0 的时长（秒）。</summary>
    const float SettleDur = 0.08f;
    /// <summary>淡出时长（秒）：轻微放大 + 整体透明。</summary>
    const float OutDur = 0.18f;
    /// <summary>卡片中心离屏幕上缘的距离（像素，锚上中）。</summary>
    const float TopOffset = -230f;

    // 各档停留时长（秒）：稀有度越高停越久，传说最久也就 1.1s，不拖节奏。
    const float HoldCommon = 0.60f;
    const float HoldRare = 0.75f;
    const float HoldEpic = 0.90f;
    const float HoldLegend = 1.10f;

    /// <summary>全屏轻闪（仅传说）的峰值不透明度与时长。</summary>
    const float FlashAlpha = 0.28f;
    const float FlashDur = 0.30f;

    RectTransform _cardRt;
    CanvasGroup _cg;
    Image _frame;
    Image _glow;
    Image _icon;
    Text _kindText;
    Text _nameText;
    Image _flash;
    Coroutine _co;

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
    public static void Show(Transform parent, string title, string kindLabel, Color tint, Sprite icon, int tier)
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
        ui.Play(title, kindLabel, tint, icon, tier);
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
        _cardRt.anchorMin = new Vector2(0.5f, 1f);
        _cardRt.anchorMax = new Vector2(0.5f, 1f);
        _cardRt.pivot = new Vector2(0.5f, 0.5f);
        _cardRt.anchoredPosition = new Vector2(0f, TopOffset);
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

        // 类型行（上） + 名字（下）：左边让开图标位（16+84=100，从 112 起），右边内缩 16。
        // 卡高 132：kind 距顶 18 高 24 → 下边余 90；name 顶内缩 44、底内缩 12。
        _kindText = MakeText(_cardRt, "Kind", 20, TextAnchor.MiddleLeft, new Vector2(112f, 90f), new Vector2(-16f, -18f));
        _nameText = MakeText(_cardRt, "Name", 26, TextAnchor.MiddleLeft, new Vector2(112f, 12f), new Vector2(-16f, -44f));

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

    void Play(string title, string kindLabel, Color tint, Sprite icon, int tier)
    {
        _frame.color = tint;
        _kindText.text = kindLabel ?? "";
        _kindText.color = Color.Lerp(tint, Color.white, 0.45f);
        _nameText.text = string.IsNullOrEmpty(title) ? "获得奖励" : title;
        _nameText.color = tint;

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

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoPlay(tier));
    }

    IEnumerator CoPlay(int tier)
    {
        float hold = HoldCommon;
        if (tier == TierRare) hold = HoldRare;
        else if (tier == TierEpic) hold = HoldEpic;
        else if (tier == TierLegend) hold = HoldLegend;

        _cg.alpha = 1f;
        _cardRt.localScale = Vector3.one * 0.55f;

        // 弹入
        yield return ScaleOver(InDur, 0.55f, 1.12f);
        // 回落
        yield return ScaleOver(SettleDur, 1.12f, 1.0f);

        // 传说：全屏轻闪一下（0.3s），不阻塞停留计时
        if (tier == TierLegend) StartCoroutine(CoFlash());

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
