using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 《跑酷小游戏》V1 —— T5 HUD（EndlessRunGame 分部，2026-11-09）。
/// 规格来源：Docs/《跑酷小游戏》_待办任务清单.md §2.4 HUD 行 / 🔴 HUD 红线 / §6 / §10。
/// 【挂点】Update 家族全被占（T2 Update / T3 LateUpdate / T4 FixedUpdate）：OnEnable 懒初始化 +
/// HudLoop 协程每帧轮询（yield return null），内部闸门 !_built || !_playing 早退；零输入 API 调用。
/// 【HUD 红线】不挂主界面资源条组件、不接主界面金币刷新；金币条只显示本局 RunGold（T6 契约），
/// 绝不显示账户余额；经验条本期只显示 Lv.X（T6 未暴露级内经验，进度条待主人拍板后补）。
/// 【升级表现】订阅 LeveledUp，Canvas 层飘字「队伍升级！！」跟随队伍 1.8s 上浮淡出。
/// </summary>
public partial class EndlessRunGame
{
    // ---- 资源路径 ----
    const string SP_HEART = "UI/Art/Icons/表情包/爱心";
    const string SP_GOLD_BG = "UI/Art/Main/资源条/金币底";   // TopBar GoldBg 同款底图（prefab 内 guid 已悬空，按同路径取图）
    const string SP_HUD_COIN = "UI/Art/Icons/Common/icon_gold";  // HUD 金币图标（项目通用惯例，CoinIcon guid 悬空）；与 World.cs 的场景金币道具图 SP_COIN 是两个资源，勿合并
    const string FONT_FUSION = "Fonts/fusion-pixel";
    const string FONT_TOPBAR = "Fonts/fusion-pixel";            // TopBar GoldText 引用的字体
    const string LEVELUP_TEXT = "队伍升级！！";

    // ---- 爱心 ----
    const float HEART_SIZE = 48f;
    const float HEART_GAP = 10f;
    static readonly Color HeartFull = Color.white;
    static readonly Color HeartEmpty = new Color(0.5f, 0.5f, 0.5f, 0.35f);   // 同图灰度 + 降透明（§9-6 选项一，待主人拍板）

    // ---- 升级飘字 ----
    const float FLY_HEAD_PAD_U = 1.2f;   // 头顶偏移（unit）
    const float FLY_RISE_PX = 70f;       // 1.8s 总上浮量（canvas px）

    // ---- HUD 节点 / 状态 ----
    RectTransform _hudRoot;
    Image[] _hearts;
    Text _metersText, _lvText, _goldText, _flyText;
    Sprite _heartSprite, _goldBgSprite, _hudCoinSprite;
    Font _fusionFont, _topbarFont;
    Coroutine _hudLoop, _flyRoutine;
    bool _hudReady, _hudSubscribed;
    int _hudHeartCount;
    int _hudLastHp = -1, _hudLastMeters = -1, _hudLastLevel = int.MinValue;
    long _hudLastGold = long.MinValue;

    /// <summary>激活时启动 HUD 协程（Update 家族被占，用协程轮询；禁用期 Unity 自动停协程）。</summary>
    void OnEnable()
    {
        if (_hudLoop != null) StopCoroutine(_hudLoop);
        _hudLoop = StartCoroutine(HudLoop());
    }

    /// <summary>HUD 主循环：每帧轮询 PlayerHp / Meters / RunGold / RunLevel，变化才刷 UI。</summary>
    IEnumerator HudLoop()
    {
        while (true)
        {
            yield return null;
            if (!_built || !_playing) continue;
            if (!_hudReady) EnsureHud();
            if (!_hudReady) continue;
            RefreshHud();
        }
    }

    // ============================================================
    // 建树
    // ============================================================

    /// <summary>懒初始化：爱心数量取 PlayerHp 初值（不重复读 player_hp），订阅 LeveledUp。</summary>
    void EnsureHud()
    {
        if (_hudReady || !_built || _canvas == null) return;
        _hudHeartCount = Mathf.Max(1, PlayerHp);

        _heartSprite = Resources.Load<Sprite>(SP_HEART);
        if (_heartSprite == null) Debug.LogError("[跑酷HUD] 爱心图加载失败：" + SP_HEART);
        _goldBgSprite = Resources.Load<Sprite>(SP_GOLD_BG);
        if (_goldBgSprite == null) Debug.LogError("[跑酷HUD] 金币条底图加载失败：" + SP_GOLD_BG);
        _hudCoinSprite = Resources.Load<Sprite>(SP_HUD_COIN);
        if (_hudCoinSprite == null) Debug.LogError("[跑酷HUD] 金币图标加载失败：" + SP_HUD_COIN);
        _fusionFont = Resources.Load<Font>(FONT_FUSION);
        if (_fusionFont == null) Debug.LogError("[跑酷HUD] 字体加载失败：" + FONT_FUSION);
        _topbarFont = Resources.Load<Font>(FONT_TOPBAR);
        if (_topbarFont == null) Debug.LogError("[跑酷HUD] 字体加载失败：" + FONT_TOPBAR);

        _hudRoot = NewHudRect("RunHudRoot", _canvas.transform);
        StretchRect(_hudRoot);

        BuildHearts();
        BuildMetersText();
        BuildBottomBar();

        if (!_hudSubscribed)
        {
            _hudSubscribed = true;
            LeveledUp += OnLeveledUp;
        }
        _hudLastHp = _hudLastMeters = _hudLastLevel = -1;
        _hudLastGold = long.MinValue;
        _hudReady = true;
    }

    /// <summary>顶部 3 颗爱心：满血实心，死一次灰度降透明（同图，无第二张空心图）。</summary>
    void BuildHearts()
    {
        var row = NewHudRect("Hearts", _hudRoot);
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 0.5f);
        row.anchoredPosition = new Vector2(30f, -90f);
        row.sizeDelta = new Vector2(_hudHeartCount * (HEART_SIZE + HEART_GAP), HEART_SIZE);
        _hearts = new Image[_hudHeartCount];
        for (int i = 0; i < _hudHeartCount; i++)
        {
            var rt = NewHudRect("Heart" + i, row);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(i * (HEART_SIZE + HEART_GAP) + HEART_SIZE * 0.5f, 0f);
            rt.sizeDelta = new Vector2(HEART_SIZE, HEART_SIZE);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = _heartSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            _hearts[i] = img;
        }
        RefreshHearts();
    }

    /// <summary>里程文本：爱心右侧，FloorToInt(Meters) + "m"，fusion-pixel 24（12 的倍数）。</summary>
    void BuildMetersText()
    {
        float x = 30f + _hudHeartCount * (HEART_SIZE + HEART_GAP) + 20f;
        _metersText = NewHudText("MetersText", _hudRoot,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -90f), new Vector2(220f, 48f),
            _fusionFont, 24, Color.white, TextAnchor.MiddleLeft);
    }

    /// <summary>底部一排：左 Lv 文本（经验，T6 未暴露级内经验只显示 Lv.X）；右 GoldPanel 纯代码复刻。</summary>
    void BuildBottomBar()
    {
        _lvText = NewHudText("LvText", _hudRoot,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 60f), new Vector2(160f, 40f),
            _fusionFont, 24, Color.white, TextAnchor.MiddleLeft);

        // GoldPanel 复刻（视觉参数取自 TopBar.prefab：面板 160×56 / GoldText 26 号 字色(1,0.85,0.35)）。
        var panel = NewHudRect("GoldPanel", _hudRoot);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-30f, 60f);
        panel.sizeDelta = new Vector2(160f, 56f);

        var bg = NewHudRect("GoldBg", panel);
        StretchRect(bg);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = _goldBgSprite;
        bgImg.raycastTarget = false;

        var coin = NewHudRect("CoinIcon", panel);
        coin.anchorMin = coin.anchorMax = new Vector2(0f, 0.5f);
        coin.pivot = new Vector2(0.5f, 0.5f);
        coin.anchoredPosition = new Vector2(2.4f, -2.5f);
        coin.sizeDelta = new Vector2(65f, 65f);
        var coinImg = coin.gameObject.AddComponent<Image>();
        coinImg.sprite = _hudCoinSprite;
        coinImg.preserveAspect = true;
        coinImg.raycastTarget = false;

        // PlusButton 等效隐藏：不建（不实例化 TopBar.prefab，避免带出主界面脚本）。
        _goldText = NewHudText("GoldText", panel,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2.2f, -2.9f), new Vector2(110f, 40f),
            _topbarFont, 26, new Color(1f, 0.85f, 0.35f, 1f), TextAnchor.MiddleCenter);
    }

    // ============================================================
    // 每帧刷新
    // ============================================================

    void RefreshHud()
    {
        if (PlayerHp != _hudLastHp) { _hudLastHp = PlayerHp; RefreshHearts(); }
        int m = Mathf.FloorToInt(Meters);
        if (m != _hudLastMeters) { _hudLastMeters = m; _metersText.text = m + "m"; }
        if (RunGold != _hudLastGold) { _hudLastGold = RunGold; _goldText.text = RunGold.ToString(); }
        if (RunLevel != _hudLastLevel) { _hudLastLevel = RunLevel; _lvText.text = "Lv." + RunLevel; }
    }

    void RefreshHearts()
    {
        for (int i = 0; i < _hearts.Length; i++)
            _hearts[i].color = i < PlayerHp ? HeartFull : HeartEmpty;
    }

    // ============================================================
    // 升级飘字（Canvas 层，跟随队伍最前单位上浮淡出）
    // ============================================================

    /// <summary>订阅 T6 契约事件 LeveledUp：领队头顶弹「队伍升级！！」。</summary>
    void OnLeveledUp()
    {
        if (_flyRoutine != null) StopCoroutine(_flyRoutine);
        _flyRoutine = StartCoroutine(FlyRoutine());
    }

    /// <summary>时长/字号走 RunTrackTable（levelup_duration / levelup_font_size），跟随最前单位，上浮淡出。</summary>
    IEnumerator FlyRoutine()
    {
        float dur = RunTrackTable.GetFloat("levelup_duration", 1.8f);
        int fs = Mathf.RoundToInt(RunTrackTable.GetFloat("levelup_font_size", 24f));
        EnsureFlyText(fs);
        _flyText.text = LEVELUP_TEXT;
        _flyText.gameObject.SetActive(true);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, dur));
            _flyText.rectTransform.anchoredPosition = LeaderCanvasPos() + new Vector2(0f, FLY_RISE_PX * k);
            var c = _flyText.color;
            c.a = 1f - k;
            _flyText.color = c;
            yield return null;
        }
        _flyText.gameObject.SetActive(false);
        _flyRoutine = null;
    }

    /// <summary>飘字节点（Canvas 层，白字黑描边；fusion-pixel 字号 24 = 12 的倍数）。</summary>
    void EnsureFlyText(int fs)
    {
        if (_flyText != null) { _flyText.fontSize = fs; return; }
        var rt = NewHudRect("LevelUpFly", _hudRoot);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 56f);
        var txt = rt.gameObject.AddComponent<Text>();
        txt.font = _fusionFont;
        txt.fontSize = fs;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
        txt.supportRichText = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = rt.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.5f);
        ol.effectDistance = new Vector2(2f, -2f);
        _flyText = txt;
    }

    /// <summary>队伍最前单位（_mercs[0] 即最前挡刀位；无佣兵取玩家）在 Canvas 上的头顶坐标。</summary>
    Vector2 LeaderCanvasPos()
    {
        Transform t = null;
        if (_mercs.Count > 0 && _mercs[0] != null && _mercs[0].go != null) t = _mercs[0].go.transform;
        if (t == null && _playerGo != null) t = _playerGo.transform;
        if (t == null || _canvas == null || _playCam == null) return Vector2.zero;
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(_playCam, t.position + Vector3.up * FLY_HEAD_PAD_U);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_hudRoot, sp, _canvas.worldCamera, out Vector2 local);
        return local;
    }

    // ============================================================
    // 工具
    // ============================================================

    RectTransform NewHudRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Text NewHudText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pos, Vector2 size, Font font, int fontSize, Color color, TextAnchor align)
    {
        var rt = NewHudRect(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var txt = rt.gameObject.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = align;
        txt.raycastTarget = false;
        txt.supportRichText = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }
}
