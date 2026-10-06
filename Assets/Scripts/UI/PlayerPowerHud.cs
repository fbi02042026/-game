using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-06 主人拍板】玩家战力（艺术字）+ 获得东西后飘「战力提升 N」与称赞语。
///
/// <b>战力口径</b>：只算<b>本局</b>，并把本局拿到的<b>所有</b>东西都算进去
/// （等级 + 技能稀有度/星级 + 佣兵等级/星级 + 主题协同 + <b>已穿戴装备</b>） —— 真源 <c>RunLoadout.TotalPower()</c>。
///
/// <b>常驻数字</b>：【2026-10-06 主人二次拍板】搬到<b>左上角玩家头像框</b>（那里才是玩家一眼看过去的地方），
/// 挂在头像框节点下 —— 跟着头像框一起缩放 / 移动，不再靠世界坐标跟人跑。
/// <b>飘字</b>：仍留在玩家头顶，拿到东西且战力真的涨了才飘，三档称赞（低/中/高）。
///
/// <remarks>
/// 主人给的图：<c>Assets/Resources/UI/战斗力/战斗力.png</c>
/// （2026-10-06 晚主人重做过：1020×375，上排「战斗力」标题 514×174，下排十个数字各约 130 高）。
/// 该图已经是 <c>spriteMode: Multiple</c>，按名字取子图，不重切图、不动 <c>.meta</c>、不加新美术资源。
///
/// ⚠ <b>换图 / 重切图后必查</b>：这里靠<b>子图名</b>认字 —— 必须是 <c>战斗力</c> 加
/// <c>战斗力_0</c> … <c>战斗力_9</c> 十个，<b>一个都不能少、一个都不能错号</b>。
/// 团结 Sprite Editor 重切时若遇到同名残留会<b>自动跳号</b>（本轮就出现过 _0~_7、_9、_10，
/// 缺 _8，真实数字 8 被叫成 _9、9 被叫成 _10），结果 <see cref="LoadArtSprites"/> 判不齐 →
/// 打 Error 并退回文字。遇到战力突然变成普通文字，第一件事就是去核这个命名表。
/// 拼数字的入口只有 <see cref="PaintNumber"/> 一处（RefreshPower 调它），要换图只改 <see cref="PowerArtRes"/>。
/// </remarks>
/// </summary>
public class PlayerPowerHud : MonoBehaviour
{
    public static PlayerPowerHud Instance { get; private set; }

    /// <summary>飘字离头顶再往上多少（世界单位）。要高低只改这一个。</summary>
    const float HeadGap = 0.42f;
    /// <summary>头顶高度拿不到 Sprite 时的兜底（世界单位）。</summary>
    const float HeadFallback = 1.15f;
    /// <summary>飘字存活时长（秒）。</summary>
    const float GainLife = 1.7f;
    /// <summary>飘字往上飘多远（世界单位）。</summary>
    const float GainRise = 0.50f;

    // ===== 主人给的艺术字（唯一图源）=====
    /// <summary>战力美术图集：Unity 已按名字切好子图的原始 PNG。</summary>
    const string PowerArtRes = "UI/战斗力/战斗力";
    /// <summary>标题子图名（上排「战斗力」三个字）。</summary>
    const string PowerLabelSpriteName = "战斗力";

    // ===== 常驻字：UI 空间（挂在头像框下，单位 = 像素）=====
    /// <summary>
    /// 美术图像素 → UI 单位。
    /// 【2026-10-06 主人二次校准】在上一版基础上再<b>缩小 30%</b>（0.34 × 0.7）：
    /// 按主人重做的那版图量过 —— 标题 174px 高 × 0.238 ≈ 41px，数字约 131px 高 × 0.238 ≈ 31px。
    /// 要大小只改这一个。
    /// </summary>
    const float FrameArtScale = 0.238f;
    /// <summary>
    /// 块高（像素）：标题与数字<b>横向并排、垂直居中</b>，所以就是两者较高的那个 ≈ 41。
    /// </summary>
    const float FrameBlockHeight = 42f;
    /// <summary>「战斗力」三个字与右侧数字之间的水平间隙（像素）。</summary>
    const float FrameLabelGap = 6f;
    /// <summary>数字之间的字距（像素）。</summary>
    const float FrameDigitTracking = 2.2f;
    /// <summary>块的初始宽度（像素）。真正宽度每次刷新按「标题 + 数字」实测重算，见 <see cref="PaintNumber"/>。</summary>
    const float FrameBlockWidthInit = 200f;
    /// <summary>
    /// 战力块离头像框<b>顶边</b>再往上多少（像素）。
    /// 【2026-10-06 主人校准】字要摆在头像框<b>上面</b>（不是下面）。
    /// 主人要是觉得高了 / 低了，<b>只改这一个数</b>（正 = 往上、负 = 往下）。
    /// </summary>
    const float FrameBlockOffsetY = 4f;

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

    // ===== 常驻字：UI 版（挂在头像框下）=====
    RectTransform _frameHost;
    RectTransform _frameBlock;
    Image _labelImage;
    Image[] _digitImgs;
    Text _powerText;               // 美术图缺失时的文字兜底（正常不显示）

    Transform _follow;
    SpriteRenderer _bodySr;

    Sprite _labelSprite;
    readonly Sprite[] _digitSprites = new Sprite[10];
    readonly int[] _digitBuf = new int[MaxDigits];
    bool _artTried;
    bool _artReady;

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
    /// 【2026-10-06 主人二次拍板】把常驻战力字挂到<b>玩家头像框</b>上。
    ///
    /// <para>宿主 = <c>CharacterSlotUI.frameImage</c> 的那层（预制体里叫 PlayerSlot）。
    /// 挂在它下面，位置 / 缩放都跟着头像框走，不用自己算屏幕坐标。</para>
    ///
    /// <para>默认落在头像框<b>正上方</b>、水平居中（2026-10-06 主人校准：「应该放到头像的上面」）。
    /// 要上下微调只改 <see cref="FrameBlockOffsetY"/>。</para>
    ///
    /// <para>幂等：同一个宿主重复调用不会再建树；换宿主（玩家槽重建）才重建。</para>
    /// </summary>
    public void AttachToFrame(RectTransform frameRt)
    {
        if (frameRt == null)
        {
            Debug.LogError("[PlayerPowerHud] 挂战力字失败：头像框 RectTransform 为空");
            return;
        }
        if (_frameHost == frameRt && _frameBlock != null) return;

        if (_frameBlock != null) Destroy(_frameBlock.gameObject);
        _frameHost = frameRt;

        var blockGo = new GameObject("PowerFrameBlock", typeof(RectTransform));
        blockGo.transform.SetParent(frameRt, false);
        _frameBlock = blockGo.GetComponent<RectTransform>();
        // 挂在头像框<b>顶边之上</b>：pivot 压自身<b>底边中点</b>，anchoredPosition.y 为正 = 往上
        _frameBlock.anchorMin = new Vector2(0.5f, 1f);
        _frameBlock.anchorMax = new Vector2(0.5f, 1f);
        _frameBlock.pivot = new Vector2(0.5f, 0f);
        _frameBlock.anchoredPosition = new Vector2(0f, FrameBlockOffsetY);
        _frameBlock.sizeDelta = new Vector2(FrameBlockWidthInit, FrameBlockHeight);

        bool art = LoadArtSprites();
        if (art)
        {
            _labelImage = MakeGlyph("PowerLabel", _frameBlock, _labelSprite, FrameArtScale);
            var lrt = _labelImage.rectTransform;
            // 【2026-10-06 主人校准】数字改放<b>字的右侧</b> → 标题贴块左边缘、垂直居中，
            // 具体 x 由 PaintNumber 按实测宽度写（保持整行居中）。
            lrt.anchorMin = new Vector2(0f, 0.5f);
            lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;

            _digitImgs = new Image[MaxDigits];
            for (int i = 0; i < MaxDigits; i++)
            {
                var img = MakeGlyph($"PowerDigit{i}", _frameBlock, null, 1f);
                var rt = img.rectTransform;
                // 同样贴左边缘、垂直居中：从左往右排，与标题同一条中线
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                img.enabled = false;
                _digitImgs[i] = img;
            }
        }

        // 美术图缺失时（图没进 Resources / 子图名改了）才亮的文字兜底 —— 会带 Error 日志，不静默
        _powerText = MakeArtText("PowerFallback", _frameBlock, 30, new Color(1f, 0.92f, 0.55f));
        var prt = _powerText.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(FrameBlockWidthInit, FrameBlockHeight);
        _powerText.gameObject.SetActive(!art);

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
    /// 读主人的艺术字（唯一图源）：<c>Resources/UI/战斗力/战斗力</c> 下的子图。
    /// 子图名 <c>战斗力</c> = 标题，<c>战斗力_N</c> = 数字 N。
    /// 任何一个缺了就整体判不齐 → 打 Error 并让上层退回文字（不静默用别的图凑数）。
    /// </summary>
    bool LoadArtSprites()
    {
        if (_artTried) return _artReady;
        _artTried = true;

        var all = Resources.LoadAll<Sprite>(PowerArtRes);
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null || string.IsNullOrEmpty(s.name)) continue;
                if (s.name == PowerLabelSpriteName) { _labelSprite = s; continue; }
                int us = s.name.LastIndexOf('_');
                if (us < 0 || us >= s.name.Length - 1) continue;
                if (int.TryParse(s.name.Substring(us + 1), out int d) && d >= 0 && d <= 9)
                    _digitSprites[d] = s;
            }
        }

        _artReady = _labelSprite != null;
        for (int i = 0; i < 10; i++)
            if (_digitSprites[i] == null) _artReady = false;

        if (!_artReady)
        {
            Debug.LogError($"[PlayerPowerHud] 战力美术图不全：Resources/{PowerArtRes} " +
                           $"(标题={(_labelSprite != null ? "有" : "缺")}，数字 " +
                           $"{System.Array.FindAll(_digitSprites, s => s != null).Length}/10) → 暂时退回文字显示");
        }
        return _artReady;
    }

    /// <summary>按原始长宽比摆一个字形（图集子图）；sprite 传 null 表示先占位、之后按数字换。</summary>
    static Image MakeGlyph(string name, Transform parent, Sprite sprite, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;      // 尺寸由我们按子图原始比例算好，不让 Unity 再插一手
        if (sprite != null)
            img.rectTransform.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
        return img;
    }

    /// <summary>
    /// 艺术字的唯一建法 —— 只给「美术图缺失时的文字兜底」和飘字用（这俩含中文，图集里没有）。
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

        if (_artReady && _digitImgs != null) { PaintNumber(power); return; }
        if (_powerText != null) _powerText.text = power.ToString();
    }

    /// <summary>
    /// 【拼数字的唯一出口】按位取图集子图，从左到右排成一行（整行居中、共底基线）。
    /// 每个字的显示宽高 = 子图原始像素 × <see cref="FrameArtScale"/>，所以「1」窄、「8」宽，跟美术画的一致。
    /// </summary>
    void PaintNumber(int value)
    {
        if (_digitImgs == null) return;

        int n = Mathf.Max(0, value);
        int count = 0;
        do
        {
            _digitBuf[count++] = n % 10;
            n /= 10;
        } while (n > 0 && count < MaxDigits);

        // 先量数字总宽（含字距），才能算整块多宽
        float digitsW = 0f;
        for (int i = 0; i < count; i++)
            digitsW += _digitSprites[_digitBuf[i]].rect.width * FrameArtScale + FrameDigitTracking;
        digitsW -= FrameDigitTracking;

        // 【2026-10-06 主人校准】横向排：块宽 = 标题 + 间隙 + 数字，块本身在头像框上居中，
        // 于是标题落左端、数字紧跟其右 —— 位数变化时整块自动重新居中，不会忽左忽右。
        float labelW = _labelSprite != null ? _labelSprite.rect.width * FrameArtScale : 0f;
        float contentW = labelW + FrameLabelGap + digitsW;
        if (_frameBlock != null)
            _frameBlock.sizeDelta = new Vector2(contentW, FrameBlockHeight);
        if (_labelImage != null)
            _labelImage.rectTransform.anchoredPosition = new Vector2(-contentW * 0.5f, 0f);

        float x = -contentW * 0.5f + labelW + FrameLabelGap;
        for (int slot = 0; slot < count; slot++)
        {
            var sp = _digitSprites[_digitBuf[count - 1 - slot]];   // 高位在左：缓存是倒序的
            var img = _digitImgs[slot];
            float w = sp.rect.width * FrameArtScale;
            float h = sp.rect.height * FrameArtScale;
            img.sprite = sp;
            img.rectTransform.sizeDelta = new Vector2(w, h);
            img.rectTransform.anchoredPosition = new Vector2(x, 0f);
            img.enabled = true;
            x += w + FrameDigitTracking;
        }
        for (int slot = count; slot < _digitImgs.Length; slot++)
            _digitImgs[slot].enabled = false;
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
