using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-06 主人拍板】玩家战力（艺术字）+ 获得东西后飘「战力提升 N」与称赞语。
///
/// <b>战力口径</b>：只算<b>本局</b>，并把本局拿到的<b>所有</b>东西都算进去
/// （等级 + 技能稀有度/星级 + 佣兵等级/星级 + 主题协同 + <b>已穿戴装备</b>） —— 真源 <c>RunLoadout.TotalPower()</c>。
///
/// <b>常驻数字</b>：【2026-10-08 主人拍板】接<b>预制体里摆好的美术节点</b>
/// —— <c>BattleUI.prefab</c> 里「PlayerSlot → 战斗力」那层（玩家头像下面），
/// 标题「战斗力」是静态贴图不用代码管，代码只按位换数字图。
/// <b>不再运行时建树、不再 <c>Resources.Load</c> 图集</b>（旧版挂在头像框上、靠代码拼字形，已废弃）。
/// <b>飘字</b>：仍留在玩家头顶，拿到东西且战力真的涨了才飘，三档称赞（低/中/高）。
///
/// <remarks>
/// 数字图源 = <c>CharacterSlotUI.powerDigitSprites</c>（预制体里拖好的十张子图
/// <c>战斗力_0</c> … <c>战斗力_9</c>），数字位 = <c>CharacterSlotUI.powerDigitSlots</c>（6 个 Image）。
/// 两者都在预制体里，代码只做「按位换 sprite + 排版」。
///
/// ⚠ <b>换图 / 重切图后必查</b>：<c>powerDigitSprites</c> 的<b>顺序</b>必须是 0~9，
/// <c>[i]</c> 就是数字 i —— 错一位数字就全错。团结 Sprite Editor 重切时若遇到同名残留会
/// <b>自动跳号</b>（历史上出现过 _0~_7、_9、_10，缺 _8，真实数字 8 被叫成 _9），
/// 遇到战力数字长得不对，第一件事就是去核预制体里这十个引用的顺序。
/// 拼数字的入口只有 <see cref="PaintNumber"/> 一处（RefreshPower 调它）。
/// </remarks>
/// </summary>
public class PlayerPowerHud : MonoBehaviour
{
    public static PlayerPowerHud Instance { get; private set; }

    /// <summary>飘字离头顶再往上多少（世界单位）。要高低只改这一个。</summary>
    const float HeadGap = 0.42f;
    /// <summary>头顶高度拿不到 Sprite 时的兜底（世界单位）。</summary>
    const float HeadFallback = 1.15f;
    /// <summary>
    /// 飘字存活时长（秒）。
    /// 【2026-10-08 主人拍板】战斗内提示时间 ×2（1.7 → 3.4）——「战力提升 N」原来一闪就看不见了。
    /// </summary>
    const float GainLife = 3.4f;
    /// <summary>飘字往上飘多远（世界单位）。</summary>
    const float GainRise = 0.50f;

    // ===== 常驻数字：预制体「战斗力」节点（单位 = 像素，缩放由美术在预制体里定）=====
    /// <summary>
    /// 数字之间的字距（像素）。
    ///
    /// <para>数字位直接取<b>子图原始像素尺寸</b>（预制体里就是这么摆的），不再另乘缩放 ——
    /// 要改大小去改预制体「战斗力」节点的 <c>scale</c>，别在这里动。</para>
    /// </summary>
    const float FrameDigitTracking = 2f;

    /// <summary>战力最多显示几位（999999 够用；位数不够时左边不留空位）。</summary>
    const int MaxDigits = 6;

    // ===== 称赞三档（主人拍板：低 / 中 / 高三等，说的能区分出来就行）=====
    /// <summary>≥ 这个数算「中档」。要调档位只改这两个数。</summary>
    const int PraiseMidAt = 300;
    /// <summary>≥ 这个数算「高档」。</summary>
    const int PraiseHighAt = 700;

    Canvas _canvas;
    RectTransform _root;
    Text _gainText;

    // ===== 常驻数字：接预制体「战斗力」节点（不自己建树、不运行时加载图）=====
    CharacterSlotUI _powerSlot;
    bool _powerBound;

    Transform _follow;
    SpriteRenderer _bodySr;

    readonly int[] _digitBuf = new int[MaxDigits];

    float _gainT;
    int _shownPower = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>建 HUD 并跟住玩家（飘字用）。重复调用只会换跟随目标，不会重复建树。</summary>
    public static PlayerPowerHud Ensure(Transform hero)
    {
        if (Instance == null)
        {
            var go = new GameObject("PlayerPowerHud");
            var hud = go.AddComponent<PlayerPowerHud>();
            hud.BuildGainCanvas();
        }
        Instance.Bind(hero);
        return Instance;
    }

    /// <summary>换跟随目标（战斗重开 / Hero 重建）—— 只影响头顶飘字。</summary>
    public void Bind(Transform hero)
    {
        _follow = hero;
        _bodySr = hero != null ? hero.GetComponentInChildren<SpriteRenderer>() : null;
    }

    /// <summary>
    /// 【2026-10-08 主人拍板】常驻战力接<b>预制体里摆好的「战斗力」节点</b>
    /// —— <c>BattleUI.prefab</c> 里 <c>PlayerSlot → 战斗力</c> 那层（玩家头像下面）。
    ///
    /// <para>标题「战斗力」是节点 <c>zhandouli</c> 的静态贴图，代码不碰；
    /// 这里只认数字位 <c>powerDigitSlots</c> 与数字图 <c>powerDigitSprites</c>，
    /// 两者都由主人在预制体里拖好 —— <b>不再运行时建树、不再 <c>Resources.Load</c> 图集</b>。</para>
    ///
    /// <para>取不到（字段空 / 数字图不足 10 张）就打 Error 并<b>什么都不显示</b>：
    /// 不补建节点、不写文字兜底（项目口径：不要兜底，出错要让我看见）。</para>
    ///
    /// <para>幂等：同一个槽重复调用不会重复初始化；换槽（玩家槽重建）才重新接。</para>
    /// </summary>
    public void AttachToPowerNode(CharacterSlotUI slot)
    {
        if (slot == null || slot.powerDigitSlots == null || slot.powerDigitSlots.Length == 0
            || slot.powerDigitSprites == null || slot.powerDigitSprites.Length < 10)
        {
            Debug.LogError("[PlayerPowerHud] 接战力节点失败：玩家槽 powerDigitSlots 为空 " +
                           "或 powerDigitSprites 不足 10 张（数字 0~9）—— 预制体里没拖好，战力不显示");
            return;
        }
        if (_powerBound && _powerSlot == slot) return;

        _powerSlot = slot;
        _powerBound = true;
        _shownPower = -1;          // 强制下一帧重写数字
        RefreshPower();
    }

    /// <summary>头顶飘字用的世界空间画布（2026-10-06 起这里只负责飘字）。</summary>
    void BuildGainCanvas()
    {
        var canvasGo = new GameObject("GainCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = GameConfig.SORT_VFX + 6;   // 压在特效之上，别被盖住
        _root = canvasGo.GetComponent<RectTransform>();
        // 世界空间：100 像素 = 1 世界单位
        _root.localScale = Vector3.one * 0.01f;
        _root.sizeDelta = new Vector2(280f, 260f);

        _gainText = MakeArtText("Gain", _root, 30, new Color(1f, 0.78f, 0.30f));
        var rt = _gainText.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(280f, 52f);
        _gainText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 建文字节点的唯一建法 —— 现在只给<b>飘字</b>用（含中文，数字图集里没有）。
    /// 常驻战力已改成接预制体节点（纯换 sprite），不再有文字兜底。
    /// </summary>
    static Text MakeArtText(string name, Transform parent, int fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;

        var ol = go.GetComponent<Outline>();
        ol.effectColor = new Color(0.28f, 0.08f, 0.34f, 0.95f);
        ol.effectDistance = new Vector2(2.2f, -2.2f);
        return t;
    }

    void LateUpdate()
    {
        if (_follow == null) { if (_canvas != null) _canvas.gameObject.SetActive(false); return; }
        if (_canvas != null && !_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

        float topY = HeadFallback;
        if (_bodySr != null) topY = _bodySr.bounds.max.y - _follow.position.y;
        transform.position = new Vector3(_follow.position.x, _follow.position.y + topY + HeadGap, _follow.position.z);

        TickGain();
    }

    /// <summary>常驻战力数字：只在数字真的变了的时候重写，避免每帧重摆字形。</summary>
    public void RefreshPower()
    {
        int power = RunLoadout.TotalPower();
        if (power == _shownPower) return;
        _shownPower = power;

        if (_powerBound && _powerSlot != null) PaintNumber(power);
    }

    /// <summary>
    /// 【拼数字的唯一出口】按位取<b>预制体里拖好的</b>数字子图，从左到右排成一行（整行水平居中）。
    /// 每个位的宽高 = <b>子图原始像素</b>（预制体里就是这么摆的），所以「1」窄、「8」宽，跟美术一致。
    /// 纵向位置 <c>y</c> 保持预制体里美术摆的值不动，代码只排横向 —— 不抢美术的排版。
    /// </summary>
    void PaintNumber(int value)
    {
        if (!_powerBound || _powerSlot == null) return;
        var slots = _powerSlot.powerDigitSlots;
        var sprites = _powerSlot.powerDigitSprites;
        if (slots == null || sprites == null || sprites.Length < 10) return;
        for (int i = 0; i < 10; i++)
        {
            if (sprites[i] == null)
            {
                Debug.LogError($"[PlayerPowerHud] 战力数字图缺第 {i} 张（powerDigitSprites[{i}] 为空），数字不刷新");
                return;
            }
        }

        int n = Mathf.Max(0, value);
        int count = 0;
        do
        {
            _digitBuf[count++] = n % 10;
            n /= 10;
        } while (n > 0 && count < MaxDigits && count < slots.Length);

        // 先量总宽（含字距）才能把整行居中：数字位 anchor/pivot 都是中心（预制体里这么摆的）
        float totalW = 0f;
        for (int i = 0; i < count; i++)
            totalW += sprites[_digitBuf[i]].rect.width + FrameDigitTracking;
        totalW -= FrameDigitTracking;

        float x = -totalW * 0.5f;
        for (int slot = 0; slot < count; slot++)
        {
            var sp = sprites[_digitBuf[count - 1 - slot]];   // 高位在左：缓存是倒序的
            var img = slots[slot];
            if (img == null) continue;
            float w = sp.rect.width;
            img.sprite = sp;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(w, sp.rect.height);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, rt.anchoredPosition.y);
            img.enabled = true;
            x += w + FrameDigitTracking;
        }
        for (int slot = count; slot < slots.Length; slot++)
            if (slots[slot] != null) slots[slot].enabled = false;
    }

    /// <summary>
    /// 拿到东西、战力真的涨了 → 头顶飘「战力提升 N」+ 三档称赞。
    /// delta ≤ 0 什么都不做（降战力没有发生，别乱飘）。
    /// </summary>
    public void ShowGain(int delta)
    {
        if (_gainText == null || delta <= 0) return;
        RefreshPower();
        _gainText.text = $"战力提升 {delta}　{PraiseOf(delta)}";
        _gainText.gameObject.SetActive(true);
        _gainT = 0f;
    }

    /// <summary>三档称赞：低「小有长进」/ 中「实力大增」/ 高「势不可挡」。</summary>
    public static string PraiseOf(int delta)
    {
        if (delta >= PraiseHighAt) return "势不可挡";
        if (delta >= PraiseMidAt) return "实力大增";
        return "小有长进";
    }

    void TickGain()
    {
        if (_gainText == null || !_gainText.gameObject.activeSelf) return;
        _gainT += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(_gainT / GainLife);
        // 往上飘 + 末段淡出
        _gainText.rectTransform.anchoredPosition = new Vector2(0f, GainRise * 100f * k);
        var c = _gainText.color;
        c.a = k > 0.65f ? Mathf.Clamp01((1f - k) / 0.35f) : 1f;
        _gainText.color = c;
        if (k >= 1f) _gainText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 对外唯一入口：战力涨了（发奖成功后算出的差值）→ 头顶飘「战力提升 N」+ 三档称赞。
    /// 没有 HUD（不在战斗里）就静默跳过，不建树、不报错。
    /// </summary>
    public static void NotifyGain(int delta)
    {
        if (Instance == null || delta <= 0) return;
        Instance.ShowGain(delta);
    }

    /// <summary>对外唯一入口：战力可能变了但没涨（比如换了件同级装备）→ 只刷数字，不飘字。</summary>
    public static void NotifyPowerChanged()
    {
        if (Instance == null) return;
        Instance.RefreshPower();
    }

    /// <summary>战斗重开 / 回城：收掉 HUD。下次 Ensure 会重建。</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance._follow = null;
        if (Instance._canvas != null) Instance._canvas.gameObject.SetActive(false);
    }
}
