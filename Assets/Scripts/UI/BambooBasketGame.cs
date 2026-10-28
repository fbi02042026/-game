using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 《竹篓纳福》玩法 v1 —— 掉落/滑动/接取/60s（第一批）+ 物理堆叠容器（第二批）
/// + **炸弹 / 实物发放 / 连击 / 竹篓图 / 底图**（2026-10-28 第三批）。
///
/// 规格来源：`Docs/《竹篓纳福》_待办任务清单.md`（§二 定稿规则 / §四 道具表 / §五 难度曲线 / §八-5 物理红线）。
///
/// 【架构】玩法世界 = **独立正交相机 + 专属层 MiniGameWorld**（TagManager layer 8）：
///   - 相机 clear 纯色 + 玩法底图 sprite（cover 模式：高度铺满 1280、宽超出裁掉，不变形）；
///     cullingMask 只含玩法层，主城一概不进画面；depth=max(90, 主相机+1)；UI Canvas(900) 盖在玩法层之上；
///   - 物理坐标换算 **1 unit = 100 逻辑像素**（INV_PX2U=0.01）；
///   - 层隔离：Physics2D.IgnoreLayerCollision 玩法层与其它 31 层全隔离；
///   - 池物体出生带 Kinematic+不模拟 Rigidbody2D，接住后原地切 Dynamic（零组件增删）。
///
/// 【炸弹（§二-5 / §二-10 / §八-6）】接住后照常进容器堆叠；进入堆叠后每帧检测接触
/// （OverlapCircleAll，同层），**碰到谁炸谁、一次碰多个炸多个**；不炸其它炸弹（无双弹合爆）、
/// 不炸相邻链（被炸的直接消失，不引发二次爆）。被炸道具放大→淡出 + 相机轻震 2px（原规格"容器轻震"，
/// 改抖相机实现——抖容器会连物理 transform 一起挪，抖相机纯视觉零物理风险）；烟花=色块闪替代
/// （ETFX Firework 预制体不在 Resources，待接）。炸弹下落全程红色轻闪（tint 闪，描边等美术）。
///
/// 【发放（§六 实物直发）】金币/钻石走 <see cref="ResourceWallet.Add"/>(save:false, notify:false)
/// 实时入账（逐次 Save 太频，**结算时统一存一次盘**）；礼物按品质几率抽（普 70/稀 25/传奇 5 草版）
/// 进 SaveData.favorGifts（AddFavorGift 唯一出口）。传奇每日限 1（§九-5）**主人未拍板，暂未做**。
///
/// 【连击（§二-9 纯表现）】连续接住普通道具 +1，漏接普通道具清零，≥3 连击中上弹字，结算报最高连击。
/// </summary>
public class BambooBasketGame : MonoBehaviour
{
    public static BambooBasketGame Instance { get; private set; }

    const int SortOrder = GameConfig.UiSort.TownPopup;   // 900，与每日登录同级，盖住主界面

    // ---- 坐标换算：1 unit = 100 逻辑像素 ----
    const float INV_PX2U = 0.01f;

    // ---- 层 / 相机 ----
    const string WORLD_LAYER = "MiniGameWorld";   // TagManager.layers[8]
    const float CAM_DEPTH = 90f;
    static readonly Color BG_FALLBACK_COLOR = new Color(0.11f, 0.09f, 0.16f, 1f);   // 底图加载失败时的纯色

    // ---- 布局（720×1280 逻辑像素，原点屏幕中心，y 向上）----
    const float HUD_Y = 580f;
    const float SPAWN_Y = 520f;
    const float BASKET_Y = -400f;     // 竹篓接取中心
    const float PLAYER_Y = -480f;     // 主角中心
    const float WARN_Y = -550f;       // 容器警戒线（=堆满判定）
    const float DESPAWN_Y = -620f;    // 漏接回收线（无惩罚）
    const float MOVE_X_LIMIT = 326f;

    // ---- 容器（U 形，§三-4）----
    const float CONTAINER_HALF_W = 230f;   // 内宽 460
    const float WALL_T = 16f;              // 壁厚
    const float WALL_H = 170f;             // 壁高
    const float FLOOR_Y = -600f;           // 容器底顶面
    static readonly Color CONTAINER_COLOR = new Color(0.45f, 0.30f, 0.16f, 1f);

    // ---- 手感 / 物理（[PLACEHOLDER]：标定目标=60 秒接住 15–25 个；真机试玩后调）----
    const float ROUND_SECONDS = 60f;
    const float BASE_FALL_SPEED = 300f;
    const float BASKET_RADIUS = 46f;
    const int POOL_SIZE = 28;
    const float STACK_GRAVITY_SCALE = 1.8f;
    const float MAX_FALL_U = 4.5f;
    const float MAX_DRIFT_U = 2f;
    const float SETTLE_SPEED_SQR = 1f;

    // ---- 爆炸表现参数（[PLACEHOLDER]）----
    const float BLOW_DURATION = 0.25f;     // 被炸道具放大淡出时长
    const float BLOW_SCALE_TO = 1.6f;      // 放大终值（×原渲染缩放）
    const float FLASH_DURATION = 0.28f;    // 爆炸闪光时长
    const float SHAKE_SECONDS = 0.18f;     // 相机轻震时长
    const float SHAKE_AMPLITUDE = 0.02f;   // 轻震幅度（unit）= 2px（§二-11）
    const int FLASH_POOL = 4;

    // ---- 资源（Resources 路径，铁律 16：按路径加载）----
    const string SP_GOLD = "UI/Icons/Common/icon_gold";
    const string SP_DIAMOND = "UI/Icons/Common/icon_diamond_blue";
    const string SP_GIFT = "UI/Art/邮件/icon礼物";   // ⚠️ 礼物图待主人从 21 张里指定（§九-3）
    const string SP_BOMB_S = "UI/Art/小游戏/接东西/32px-item214";   // 圆雷（主人 10-28 已移入 Resources）
    const string SP_BOMB_L = "UI/Art/小游戏/接东西/32px-item374";   // 炮竹
    const string SP_ZHULOU = "UI/Art/小游戏/接东西/zhulou";         // 竹篓（100×76）
    // 底图：主人放进来的两张里选了方图（1024²，竖屏 cover 裁切损失最小）；要换另一张改这里
    const string SP_BG = "UI/Art/小游戏/接东西/2D_pixel_art_game_background___2026-10-27T02-15-59";

    class DropDef
    {
        public string id;
        public string path;
        public int weight;       // 基础权重（§四）
        public float radius;     // 碰撞半径（px，与渲染解耦 §四原则 3）
        public float renderSize; // 渲染槽位 S=72 / M=96
        public bool rare;
        public bool isBomb;      // 炸弹：进堆叠后碰到谁炸谁
    }

    // 权重与 §四 一致（合计 100）；§五 动态占比在 RollDef 里按时间段放大炸弹/压缩普通
    static readonly DropDef[] DEFS = new DropDef[]
    {
        new DropDef { id = "coin",    path = SP_GOLD,     weight = 52, radius = 36f, renderSize = 72f, rare = false, isBomb = false },
        new DropDef { id = "diamond", path = SP_DIAMOND,  weight = 14, radius = 40f, renderSize = 96f, rare = true,  isBomb = false },
        new DropDef { id = "gift",    path = SP_GIFT,     weight = 8,  radius = 40f, renderSize = 96f, rare = true,  isBomb = false },
        new DropDef { id = "bomb_s",  path = SP_BOMB_S,   weight = 12, radius = 34f, renderSize = 72f, rare = false, isBomb = true  },
        new DropDef { id = "bomb_l",  path = SP_BOMB_L,   weight = 14, radius = 38f, renderSize = 72f, rare = false, isBomb = true  },
    };

    class Drop
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Rigidbody2D body;
        public CircleCollider2D col;
        public DropDef def;
        public bool alive;
        public bool inPhysics;          // 已转物理（在容器里堆叠）
        public bool exploding;          // 被炸表现进行中（放大淡出）
        public float blowT;             // 被炸表现进度
        public Vector2 logicPos;        // 下落阶段逻辑坐标（UI px）
        public Vector3 blowBaseScale;   // 被炸前渲染缩放
    }

    RectTransform _root;
    Canvas _canvas;
    Text _scoreText;
    Text _timeText;
    Text _comboText;
    RectTransform _playerRt;
    RectTransform _basketRt;
    GameObject _worldRoot;
    Camera _playCam;
    PhysicsMaterial2D _physMat;
    int _worldLayer = -1;
    readonly List<Drop> _pool = new List<Drop>();
    readonly List<Drop> _active = new List<Drop>();
    readonly List<SpriteRenderer> _flashPool = new List<SpriteRenderer>();
    readonly List<int> _flashLive = new List<int>();      // 活跃闪光的池下标
    readonly List<float> _flashT = new List<float>();
    static Sprite _whiteSprite;

    bool _playing;
    float _timeLeft;
    float _spawnTimer;
    float _spawnInterval;
    float _fallSpeed;
    float _stackCheckTimer;
    float _flashClock;                 // 炸弹红闪时钟
    float _shakeTimer;
    int _coinCount, _diamondCount, _giftCommon, _giftRare, _giftLegend, _blownCount;
    int _combo, _comboBest;
    float _comboHideTimer;

    bool _dragging;
    float _dragStartFingerX;
    float _dragStartPlayerX;
    bool _appPaused;

    // ============================================================
    // 生命周期 / 对外
    // ============================================================

    public static void Show() => Ensure().Open();

    public static BambooBasketGame Ensure()
    {
        if (Instance != null) return Instance;
        var root = new GameObject("BambooBasketGame", typeof(RectTransform));
        DontDestroyOnLoad(root);
        var ui = root.AddComponent<BambooBasketGame>();
        ui.Build();
        return ui;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnApplicationPause(bool paused)
    {
        // §八-8：切后台冻结计时与掉落；物理随引擎暂停，回来只在未结算时恢复。
        if (paused)
        {
            _appPaused = true;
            _playing = false;
        }
        else if (_appPaused)
        {
            _appPaused = false;
            if (gameObject.activeSelf && _timeLeft > 0f) _playing = true;
        }
    }

    // ============================================================
    // 建树（UI 层 + 玩法世界层）
    // ============================================================

    void Build()
    {
        _worldLayer = LayerMask.NameToLayer(WORLD_LAYER);
        if (_worldLayer < 0)
        {
            Debug.LogError("[竹篓纳福] 找不到层 " + WORLD_LAYER + "（TagManager.layers[8]），玩法无法渲染，检查 ProjectSettings/TagManager.asset");
            return;
        }

        // 玩法层与其它所有层物理隔离（不改工程碰撞矩阵；玩法层没有别人，Ignore 无副作用）
        for (int l = 0; l < 32; l++)
        {
            if (l == _worldLayer) continue;
            Physics2D.IgnoreLayerCollision(_worldLayer, l, true);
        }
        _physMat = new PhysicsMaterial2D("BambooStack") { friction = 0.8f, bounciness = 0f };   // §八-5：摩擦加大、不弹

        BuildWorld();
        BuildUi();
        BuildPool();
        BuildFlashPool();

        GameFonts.ApplyToHierarchy(transform);
        gameObject.SetActive(false);   // 五坑⑤
    }

    /// <summary>玩法世界：独立正交相机 + 底图/容器。坐标：1 unit = 100 逻辑像素，原点=屏幕中心。</summary>
    void BuildWorld()
    {
        _worldRoot = new GameObject("BambooBasketWorld");
        _worldRoot.transform.SetParent(transform, false);
        _worldRoot.layer = _worldLayer;

        var camGo = new GameObject("BambooBasketCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
        camGo.layer = _worldLayer;
        _playCam = camGo.AddComponent<Camera>();
        _playCam.orthographic = true;
        _playCam.orthographicSize = 640f * INV_PX2U;      // 半高 640px → 全高 12.8 unit，与 UI 高度匹配
        _playCam.clearFlags = CameraClearFlags.SolidColor;
        _playCam.backgroundColor = BG_FALLBACK_COLOR;
        _playCam.cullingMask = 1 << _worldLayer;          // 只渲染玩法层，主城一概不见
        // depth 至少比主相机高一档：保证玩法画面盖在主城之上
        float camDepth = CAM_DEPTH;
        var mainCam = Camera.main;
        if (mainCam != null) camDepth = Mathf.Max(camDepth, mainCam.depth + 1f);
        _playCam.depth = camDepth;
        _playCam.stereoTargetEye = StereoTargetEyeMask.None;

        // 底图（cover 模式：高度铺满 12.8 unit、宽超出裁掉，不变形）；失败 LogError 后退纯色
        var bg = NewBlock("Bg", Color.white, 10f, 10f);
        bg.transform.SetParent(_worldRoot.transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.sortingOrder = -10;
        var bgSp = Resources.Load<Sprite>(SP_BG);
        if (bgSp != null)
        {
            bg.sprite = bgSp;
            bg.color = Color.white;
            float hU = bgSp.rect.height / Mathf.Max(1f, bgSp.pixelsPerUnit);
            float k = hU > 0.001f ? 12.8f / hU : 1f;
            bg.transform.localScale = new Vector3(k, k, 1f);
        }
        else
        {
            Debug.LogError("[竹篓纳福] 底图加载失败，暂用纯色背景。Resources 路径：" + SP_BG);
            bg.color = BG_FALLBACK_COLOR;
        }

        // U 形容器：底 + 左右壁（视觉块与碰撞盒同尺寸同位置）
        MakeContainerPiece("Floor", 0f, FLOOR_Y - WALL_T * 0.5f, (CONTAINER_HALF_W + WALL_T) * 2f, WALL_T);
        MakeContainerPiece("WallL", -CONTAINER_HALF_W - WALL_T * 0.5f, FLOOR_Y + WALL_H * 0.5f - WALL_T * 0.5f, WALL_T, WALL_H + WALL_T);
        MakeContainerPiece("WallR", CONTAINER_HALF_W + WALL_T * 0.5f, FLOOR_Y + WALL_H * 0.5f - WALL_T * 0.5f, WALL_T, WALL_H + WALL_T);
    }

    /// <summary>纯色块（SpriteRenderer）：白块 + 缩放到 wPx×hPx 逻辑像素。调用方自行设 sortingOrder / 父节点。</summary>
    SpriteRenderer NewBlock(string name, Color color, float wPx, float hPx)
    {
        var go = new GameObject(name);
        go.layer = _worldLayer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite;
        sr.color = color;
        go.transform.localScale = BlockScale(wPx, hPx);
        return sr;
    }

    /// <summary>容器一块：视觉 SpriteRenderer + 静态 BoxCollider2D（尺寸一致）。</summary>
    void MakeContainerPiece(string name, float cxPx, float cyPx, float wPx, float hPx)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_worldRoot.transform, false);
        go.layer = _worldLayer;
        go.transform.localPosition = new Vector3(cxPx * INV_PX2U, cyPx * INV_PX2U, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite;
        sr.color = CONTAINER_COLOR;
        sr.sortingOrder = 2;
        go.transform.localScale = BlockScale(wPx, hPx);

        var col = go.AddComponent<BoxCollider2D>();
        col.sharedMaterial = _physMat;
        // ⚠️ collider 尺寸会被 transform.localScale 放大：白块本体 0.04 unit，缩放后正好等于视觉块尺寸，
        //    所以本地 size 固定 0.04（不要再乘 INV_PX2U，否则二次放大、碰撞盒大 25 倍）。
        col.size = new Vector2(0.04f, 0.04f);
    }

    void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        _canvas = canvas;
        UICanvasSetup.ApplyPopup(canvas, SortOrder);
        _root = transform as RectTransform;

        _scoreText = MakeText("Score", "接住 0", 34, TextAnchor.MiddleLeft);
        Anchor(_scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, HUD_Y), new Vector2(240f, 48f), new Vector2(0f, 0.5f));
        _timeText = MakeText("Timer", "60", 34, TextAnchor.MiddleRight);
        Anchor(_timeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, HUD_Y), new Vector2(240f, 48f), new Vector2(1f, 0.5f));

        // 连击弹字（中上，≥3 连击才显示，§二-9 纯表现）
        _comboText = MakeText("Combo", "", 40, TextAnchor.MiddleCenter);
        _comboText.color = new Color(1f, 0.72f, 0.2f);
        Anchor(_comboText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, HUD_Y - 90f), new Vector2(400f, 60f), new Vector2(0.5f, 0.5f));
        _comboText.gameObject.SetActive(false);

        // 主角（占位白块，SPUM 通道待接）+ 头顶竹篓（zhulou.png，主人 10-28 已给）
        _playerRt = MakeBox("Player", new Color(1f, 1f, 1f, 0.85f), new Vector2(68f, 96f));
        Anchor(_playerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, PLAYER_Y), new Vector2(68f, 96f), new Vector2(0.5f, 0.5f));
        _basketRt = MakeBox("Basket", Color.white, new Vector2(92f, 56f));
        Anchor(_basketRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, BASKET_Y), new Vector2(92f, 56f), new Vector2(0.5f, 0.5f));
        var basketImg = _basketRt.GetComponent<Image>();
        var zhuSp = Resources.Load<Sprite>(SP_ZHULOU);
        if (zhuSp != null)
        {
            basketImg.sprite = zhuSp;
            basketImg.preserveAspect = true;   // 100×76 进 92×56 槽位不拉伸
        }
        else
        {
            Debug.LogError("[竹篓纳福] 竹篓图加载失败，Resources 路径：" + SP_ZHULOU);
        }

        // 警戒线（UI 画，叠在玩法层上；§三-4 红色，虚线化等美术）
        var line = MakeBox("WarnLine", new Color(1f, 0.25f, 0.25f, 0.9f), new Vector2(560f, 4f));
        Anchor(line, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, WARN_Y), new Vector2(560f, 4f), new Vector2(0.5f, 0.5f));

        // 关闭按钮（中途退出：直接收摊，不结算）
        var closeNode = new GameObject("Close", typeof(RectTransform));
        closeNode.transform.SetParent(transform, false);
        var closeImg = closeNode.AddComponent<Image>();
        closeImg.color = new Color(0f, 0f, 0f, 0.45f);
        var closeBtn = closeNode.AddComponent<Button>();
        Anchor(closeNode.transform as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, HUD_Y - 70f), new Vector2(120f, 56f), new Vector2(1f, 0.5f));
        var closeLabel = MakeText("CloseLabel", "收摊", 26, TextAnchor.MiddleCenter);
        closeLabel.transform.SetParent(closeNode.transform, false);
        Anchor(closeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 56f), new Vector2(0.5f, 0.5f));
        closeBtn.onClick.AddListener(Close);
    }

    void BuildPool()
    {
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var go = new GameObject("Drop_" + i);
            go.transform.SetParent(_worldRoot.transform, false);
            go.layer = _worldLayer;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1;

            // 出生即带物理组件但不模拟（接住后原地切 Dynamic，零组件增删）
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = false;
            rb.gravityScale = STACK_GRAVITY_SCALE;
            rb.sleepMode = RigidbodySleepMode2D.StartAwake;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<CircleCollider2D>();
            col.enabled = false;
            col.sharedMaterial = _physMat;

            go.transform.localPosition = new Vector3(0f, SPAWN_Y * INV_PX2U, 0f);
            go.SetActive(false);
            _pool.Add(new Drop { go = go, sr = sr, body = rb, col = col });
        }
    }

    /// <summary>爆炸闪光池：被炸处的白橙色块放大淡出（烟花特效待接，ETFX 不在 Resources）。</summary>
    void BuildFlashPool()
    {
        for (int i = 0; i < FLASH_POOL; i++)
        {
            var sr = NewBlock("Flash_" + i, Color.white, 60f, 60f);
            sr.transform.SetParent(_worldRoot.transform, false);
            sr.sortingOrder = 3;
            sr.color = new Color(1f, 0.85f, 0.4f, 0f);
            sr.gameObject.SetActive(false);
            _flashPool.Add(sr);
        }
    }

    // ============================================================
    // 开局 / 结算
    // ============================================================

    void Open()
    {
        gameObject.SetActive(true);
        var canvas = GetComponent<Canvas>();
        if (canvas != null) UICanvasSetup.RefreshPopup(canvas, SortOrder);

        for (int i = _active.Count - 1; i >= 0; i--) ReleaseDrop(_active[i]);
        _active.Clear();
        ClearFlashes();

        _coinCount = _diamondCount = _giftCommon = _giftRare = _giftLegend = _blownCount = 0;
        _combo = _comboBest = 0;
        _comboHideTimer = 0f;
        _comboText.gameObject.SetActive(false);
        _timeLeft = ROUND_SECONDS;
        _spawnTimer = 0f;
        _stackCheckTimer = 0f;
        _shakeTimer = 0f;
        if (_playCam != null) _playCam.transform.localPosition = new Vector3(0f, 0f, -10f);
        _dragging = false;
        SetPlayerX(0f);
        RefreshHud();
        _playing = true;
    }

    void Close()
    {
        _playing = false;
        // 实物发放逐次入账时没存盘（save:false），中途退出也要存一次，不然白接了
        var saveSys = SaveSystem.Instance;
        if (saveSys != null) saveSys.Save();
        gameObject.SetActive(false);
    }

    void Settle(string reason)
    {
        _playing = false;
        // 实物发放逐次入账时未存盘（save:false），结算统一存一次
        var saveSys = SaveSystem.Instance;
        if (saveSys != null) saveSys.Save();
        // 结算后 Update 停跑：表现层收尾（相机归位 / 连击字收起），避免残影
        _shakeTimer = 0f;
        if (_playCam != null) _playCam.transform.localPosition = new Vector3(0f, 0f, -10f);
        _comboHideTimer = 0f;
        _comboText.gameObject.SetActive(false);

        var body = reason
            + "\n金币 " + _coinCount + " · 钻石 " + _diamondCount
            + "\n礼物 " + (_giftCommon + _giftRare + _giftLegend)
            + "（普 " + _giftCommon + " / 稀 " + _giftRare + " / 传 " + _giftLegend + "）";
        if (_blownCount > 0) body += "\n被炸飞 " + _blownCount + " 个";
        if (_comboBest >= 2) body += "\n最高连击 x" + _comboBest;

        StaminaConfirmPopup.ShowNotice("收篓啦", body, "再来一把", Show);
    }

    // ============================================================
    // 每帧
    // ============================================================

    void Update()
    {
        if (!_playing) return;

        _timeLeft -= Time.deltaTime;
        if (_timeLeft <= 0f)
        {
            _timeLeft = 0f;
            RefreshHud();
            Settle("时间到！");
            return;
        }

        ApplyDifficulty();
        UpdateDrag();
        UpdateSpawn();
        UpdateFalls();
        ClampStackVelocity();
        UpdateBombContacts();
        UpdateBlowing();
        UpdateFlashes();
        UpdateShake();
        UpdateBombFlash();
        UpdateComboFade();
        UpdateStackCheck();
        RefreshHud();
    }

    /// <summary>§五 难度曲线：生成间隔 / 下落速度。</summary>
    void ApplyDifficulty()
    {
        float t = ROUND_SECONDS - _timeLeft;
        if (t <= 20f)
        {
            _spawnInterval = 1.2f;
            _fallSpeed = BASE_FALL_SPEED * 1.0f;
        }
        else if (t <= 50f)
        {
            float k = (t - 20f) / 30f;
            _spawnInterval = Mathf.Lerp(1.2f, 0.6f, k);
            _fallSpeed = BASE_FALL_SPEED * Mathf.Lerp(1.0f, 1.4f, k);
        }
        else
        {
            _spawnInterval = 0.45f;
            _fallSpeed = BASE_FALL_SPEED * 1.5f;
        }
    }

    void UpdateSpawn()
    {
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;
        _spawnTimer = _spawnInterval;

        var def = RollDef();
        if (def == null) return;
        var d = TakeDrop();
        if (d == null) return;

        var sp = Resources.Load<Sprite>(def.path);
        if (sp == null)
        {
            Debug.LogError("[竹篓纳福] 掉落图加载失败，Resources 路径：" + def.path);
            return;
        }
        d.def = def;
        d.sr.sprite = sp;
        d.sr.color = Color.white;
        FitRenderSize(d, sp, def.renderSize);

        d.inPhysics = false;
        d.exploding = false;
        d.body.bodyType = RigidbodyType2D.Kinematic;
        d.body.simulated = false;
        d.col.enabled = false;
        d.logicPos = new Vector2(Random.Range(-300f, 300f), SPAWN_Y);
        ApplyLogicPos(d);
        d.alive = true;
        d.go.SetActive(true);
        _active.Add(d);
    }

    /// <summary>下落阶段：逻辑坐标匀速下落 → 接住转物理 → 漏接回收。</summary>
    void UpdateFalls()
    {
        float basketX = _playerRt.anchoredPosition.x;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var d = _active[i];
            if (d.inPhysics) continue;

            d.logicPos.y -= _fallSpeed * Time.deltaTime;
            ApplyLogicPos(d);

            // 接取：中心距 ≤ 竹篓半径 + 道具半径（与渲染尺寸无关，§四原则 3）
            if (d.logicPos.y <= BASKET_Y + 20f && d.logicPos.y >= BASKET_Y - 80f)
            {
                float dx = Mathf.Abs(d.logicPos.x - basketX);
                if (dx <= BASKET_RADIUS + d.def.radius)
                {
                    EnterPhysics(d, basketX);
                    OnCaught(d);
                    continue;
                }
            }

            if (d.logicPos.y <= DESPAWN_Y)   // 漏接：无任何惩罚（§二-7）
            {
                // 连击（§二-9 纯表现）：漏接普通道具断连；炸弹不算
                if (!d.def.isBomb) _combo = 0;
                ReleaseDrop(d);
                _active.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 接住 → 原地切 Dynamic 进容器堆叠。
    /// 落点 x 钳在容器口内（竹篓在主角头上、容器固定在底部，主角站在容器外接住时道具从"容器口正上方"倒入）。
    /// </summary>
    void EnterPhysics(Drop d, float basketX)
    {
        float r = d.def.radius;
        float cx = Mathf.Clamp(basketX, -CONTAINER_HALF_W + r, CONTAINER_HALF_W - r);
        d.inPhysics = true;
        // ⚠️ collider 半径同样被 localScale 放大：先除回渲染缩放，保证实际半径 = r 像素（§四原则 3）
        float renderScale = Mathf.Max(0.0001f, d.sr.transform.localScale.x);
        d.col.radius = (r * INV_PX2U) / renderScale;
        d.col.enabled = true;
        d.go.transform.localPosition = new Vector3(cx * INV_PX2U, (BASKET_Y - 40f) * INV_PX2U, 0f);
        d.body.bodyType = RigidbodyType2D.Dynamic;
        d.body.simulated = true;
        d.body.WakeUp();
        d.body.velocity = new Vector2(0f, -_fallSpeed * INV_PX2U);   // 速度连续，无顿挫
    }

    /// <summary>接到实物（§六：接到实时入账；存盘统一在结算）。</summary>
    void OnCaught(Drop d)
    {
        if (d.def.isBomb) return;   // 炸弹无奖励（纯风险物）

        switch (d.def.id)
        {
            case "coin":
                _coinCount++;
                ResourceWallet.Add(ResourceWallet.ResourceType.Gold, 1L, false, false);
                break;
            case "diamond":
                _diamondCount++;
                ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, 1L, false, false);
                break;
            case "gift":
                GrantGift();
                break;
        }

        // 连击：连续接住普通道具 +1（§二-9 纯表现）
        _combo++;
        if (_combo > _comboBest) _comboBest = _combo;
        if (_combo >= 3)
        {
            _comboText.text = "x" + _combo + " 连击!";
            _comboText.gameObject.SetActive(true);
            _comboHideTimer = 1f;
        }
    }

    /// <summary>礼物按品质几率抽 1 件进好感礼物仓（普 70 / 稀 25 / 传奇 5，§六草版）。</summary>
    void GrantGift()
    {
        int roll = Random.Range(0, 100);
        var q = roll < 5 ? MercFavorDefs.GiftQuality.Legendary
              : roll < 30 ? MercFavorDefs.GiftQuality.Rare
              : MercFavorDefs.GiftQuality.Common;

        MercFavorDefs.GiftDef picked = default;
        int count = 0;
        for (int i = 0; i < MercFavorDefs.Gifts.Length; i++)
        {
            if (MercFavorDefs.Gifts[i].Quality != q) continue;
            count++;
            if (Random.Range(0, count) == 0) picked = MercFavorDefs.Gifts[i];   // 蓄水池抽样（单遍等概率）
        }
        if (count == 0) return;

        SaveSystem.Instance?.Data?.AddFavorGift(picked.Id, 1);   // 唯一发放出口（不存盘，结算统一存）
        switch (q)
        {
            case MercFavorDefs.GiftQuality.Legendary: _giftLegend++; break;
            case MercFavorDefs.GiftQuality.Rare: _giftRare++; break;
            default: _giftCommon++; break;
        }
    }

    /// <summary>§八-5：每帧限速——下落封顶 + 横漂封顶，堆叠不弹跳不乱窜的第一道闸。</summary>
    void ClampStackVelocity()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            var d = _active[i];
            if (!d.inPhysics) continue;
            var v = d.body.velocity;
            float vy = Mathf.Clamp(v.y, -MAX_FALL_U, 10f);
            float vx = Mathf.Clamp(v.x, -MAX_DRIFT_U, MAX_DRIFT_U);
            if (vx != v.x || vy != v.y) d.body.velocity = new Vector2(vx, vy);
        }
    }

    /// <summary>
    /// 炸弹机制（§二-5 / §八-6）：进堆叠后每帧检测接触——碰到谁炸谁、一次碰多个炸多个；
    /// 不炸其它炸弹（无双弹合爆）、被炸的直接消失（不炸相邻链）。炸到东西炸弹自己也爆。
    /// </summary>
    void UpdateBombContacts()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var b = _active[i];
            if (!b.inPhysics || b.def == null || !b.def.isBomb) continue;

            float rWorld = b.col.radius * b.sr.transform.localScale.x + 0.02f;   // 碰撞半径 + 微余量
            var hits = Physics2D.OverlapCircleAll(b.body.transform.localPosition, rWorld, 1 << _worldLayer);
            bool touched = false;
            for (int h = 0; h < hits.Length; h++)
            {
                var d = DropByCollider(hits[h]);
                if (d == null || !d.inPhysics || d.exploding || d.def.isBomb) continue;
                BlowUp(d);
                touched = true;
            }
            if (touched) Detonate(b, i);
        }
    }

    Drop DropByCollider(Collider2D c)
    {
        for (int i = 0; i < _active.Count; i++)
            if (_active[i].col == c) return _active[i];
        return null;   // 容器壁等非池 collider 走这里
    }

    /// <summary>被炸：物理立即消失（先关 collider 防二次检测），视觉放大淡出后回收。</summary>
    void BlowUp(Drop d)
    {
        d.exploding = true;
        d.blowT = 0f;
        d.blowBaseScale = d.sr.transform.localScale;
        d.col.enabled = false;
        d.body.simulated = false;
        _blownCount++;
    }

    /// <summary>炸弹自爆：相机轻震 + 闪光，然后直接回收。</summary>
    void Detonate(Drop b, int activeIndex)
    {
        SpawnFlash(b.body.transform.localPosition);
        _shakeTimer = SHAKE_SECONDS;
        ReleaseDrop(b);
        _active.RemoveAt(activeIndex);
    }

    void UpdateBlowing()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var d = _active[i];
            if (!d.exploding) continue;
            d.blowT += Time.deltaTime;
            float k = Mathf.Clamp01(d.blowT / BLOW_DURATION);
            d.sr.transform.localScale = d.blowBaseScale * (1f + (BLOW_SCALE_TO - 1f) * k);   // 放大
            var c = d.sr.color; c.a = 1f - k; d.sr.color = c;                                // 淡出
            if (k >= 1f)
            {
                ReleaseDrop(d);
                _active.RemoveAt(i);
            }
        }
    }

    void SpawnFlash(Vector3 posU)
    {
        int idx = -1;
        for (int i = 0; i < _flashPool.Count; i++)
            if (!_flashLive.Contains(i)) { idx = i; break; }
        if (idx < 0) return;   // 池满：放弃本次闪光（表现层，可丢）

        var sr = _flashPool[idx];
        sr.transform.localPosition = posU;
        sr.transform.localScale = BlockScale(40f, 40f);
        sr.color = new Color(1f, 0.85f, 0.4f, 1f);
        sr.gameObject.SetActive(true);
        _flashLive.Add(idx);
        _flashT.Add(0f);
    }

    void UpdateFlashes()
    {
        for (int i = _flashLive.Count - 1; i >= 0; i--)
        {
            int idx = _flashLive[i];
            _flashT[i] += Time.deltaTime;
            float k = Mathf.Clamp01(_flashT[i] / FLASH_DURATION);
            var sr = _flashPool[idx];
            sr.transform.localScale = BlockScale(40f + 260f * k, 40f + 260f * k);   // 40→300px
            var c = sr.color; c.a = 1f - k; sr.color = c;
            if (k >= 1f)
            {
                sr.gameObject.SetActive(false);
                _flashLive.RemoveAt(i);
                _flashT.RemoveAt(i);
            }
        }
    }

    void ClearFlashes()
    {
        for (int i = 0; i < _flashLive.Count; i++)
        {
            var sr = _flashPool[_flashLive[i]];
            sr.gameObject.SetActive(false);
            sr.color = new Color(1f, 0.85f, 0.4f, 0f);
        }
        _flashLive.Clear();
        _flashT.Clear();
    }

    /// <summary>§二-11 轻震 2px：抖相机（纯视觉，物理零风险——抖容器会连物理 transform 一起挪）。</summary>
    void UpdateShake()
    {
        if (_playCam == null) return;
        if (_shakeTimer <= 0f) return;
        _shakeTimer -= Time.deltaTime;
        if (_shakeTimer <= 0f)
        {
            _playCam.transform.localPosition = new Vector3(0f, 0f, -10f);
            return;
        }
        float a = SHAKE_AMPLITUDE;
        _playCam.transform.localPosition = new Vector3(Random.Range(-a, a), Random.Range(-a, a), -10f);
    }

    /// <summary>§二-10：炸弹下落全程红色轻闪（tint 闪；红描边等美术到位再换）。</summary>
    void UpdateBombFlash()
    {
        _flashClock += Time.deltaTime;
        float k = 0.5f + 0.5f * Mathf.Sin(_flashClock * 10f);
        var c = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.25f), k * 0.8f);
        for (int i = 0; i < _active.Count; i++)
        {
            var d = _active[i];
            if (d.exploding || d.def == null || !d.def.isBomb) continue;
            d.sr.color = c;
        }
    }

    void UpdateComboFade()
    {
        if (_comboHideTimer <= 0f) return;
        _comboHideTimer -= Time.deltaTime;
        if (_comboHideTimer <= 0f) _comboText.gameObject.SetActive(false);
    }

    /// <summary>§八-5：堆到警戒线触发结束。每 0.3s 扫一次，要求物体近乎静止（velocity² < 1）防堆叠瞬间误判。</summary>
    void UpdateStackCheck()
    {
        _stackCheckTimer -= Time.deltaTime;
        if (_stackCheckTimer > 0f) return;
        _stackCheckTimer = 0.3f;

        float warnY = WARN_Y * INV_PX2U;
        for (int i = 0; i < _active.Count; i++)
        {
            var d = _active[i];
            if (!d.inPhysics || d.exploding) continue;
            // col.radius 是本地半径（除过渲染缩放），判世界高度要乘回来
            float rWorld = d.col.radius * d.sr.transform.localScale.x;
            float top = d.body.transform.localPosition.y + rWorld;
            if (top >= warnY && d.body.velocity.sqrMagnitude < SETTLE_SPEED_SQR)
            {
                Settle("堆满啦！");
                return;
            }
        }
    }

    /// <summary>主角控制（§二-2）：按住屏幕左右滑动，按相对位移跟手；松开即停。</summary>
    void UpdateDrag()
    {
        bool down = Input.GetMouseButton(0);   // 单指触摸在 Unity 里映射为鼠标
        // 按在按钮/UI 上不启动拖动（否则点「收摊」会把主角拖走一下）
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (down && !_dragging && !overUi)
        {
            Vector2 startLocal;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, Input.mousePosition,
                    _canvas != null ? _canvas.worldCamera : null, out startLocal))
            {
                _dragging = true;
                _dragStartFingerX = startLocal.x;
                _dragStartPlayerX = _playerRt.anchoredPosition.x;
            }
        }
        else if (!down && _dragging)
        {
            _dragging = false;
        }
        if (!_dragging) return;

        // 屏幕坐标 → 界面本地坐标（跟随 CanvasScaler，不手写比例换算，换机型不会错位）
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, Input.mousePosition,
                _canvas != null ? _canvas.worldCamera : null, out local))
            return;

        float delta = local.x - _dragStartFingerX;
        SetPlayerX(_dragStartPlayerX + delta);
    }

    void SetPlayerX(float x)
    {
        x = Mathf.Clamp(x, -MOVE_X_LIMIT, MOVE_X_LIMIT);
        _playerRt.anchoredPosition = new Vector2(x, PLAYER_Y);
        _basketRt.anchoredPosition = new Vector2(x, BASKET_Y);   // 竹篓绑定主角，不可独立拖（§二-2）
    }

    void RefreshHud()
    {
        int total = _coinCount + _diamondCount + _giftCommon + _giftRare + _giftLegend;
        _scoreText.text = "接住 " + total;
        int sec = Mathf.CeilToInt(_timeLeft);
        _timeText.text = sec.ToString();
        _timeText.color = sec <= 5 ? new Color(1f, 0.25f, 0.25f) : Color.white;
    }

    // ============================================================
    // 工具
    // ============================================================

    void ApplyLogicPos(Drop d)
    {
        d.go.transform.localPosition = new Vector3(d.logicPos.x * INV_PX2U, d.logicPos.y * INV_PX2U, 0f);
    }

    /// <summary>渲染槽位适配（§四原则 1）：按最长边等比缩放、不拉伸，与 sprite 导入 PPU 无关。</summary>
    void FitRenderSize(Drop d, Sprite sp, float renderSizePx)
    {
        float wU = sp.rect.width / Mathf.Max(1f, sp.pixelsPerUnit);
        float hU = sp.rect.height / Mathf.Max(1f, sp.pixelsPerUnit);
        float maxU = Mathf.Max(wU, hU);
        float k = maxU > 0.0001f ? (renderSizePx * INV_PX2U) / maxU : 1f;
        d.sr.transform.localScale = new Vector3(k, k, 1f);
    }

    /// <summary>
    /// 掷点（§四 基础权重 + §五 动态炸弹占比）：先按时间段定炸弹概率 p（0–20s 10% / 20–50s 10→20% / 50–60s 25%），
    /// 再在"炸弹 / 普通"组内按基础权重选——普通道具等比压缩，与规格口径一致。
    /// </summary>
    DropDef RollDef()
    {
        float t = ROUND_SECONDS - _timeLeft;
        float bombP = t <= 20f ? 0.10f
                    : t <= 50f ? Mathf.Lerp(0.10f, 0.20f, (t - 20f) / 30f)
                    : 0.25f;
        bool wantBomb = Random.value < bombP;

        int total = 0;
        for (int i = 0; i < DEFS.Length; i++)
        {
            if (DEFS[i].isBomb != wantBomb) continue;
            total += DEFS[i].weight;
        }
        if (total <= 0) return DEFS[0];

        int roll = Random.Range(0, total);
        for (int i = 0; i < DEFS.Length; i++)
        {
            if (DEFS[i].isBomb != wantBomb) continue;
            roll -= DEFS[i].weight;
            if (roll < 0) return DEFS[i];
        }
        return DEFS[0];
    }

    Drop TakeDrop()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].alive) return _pool[i];
        return null;
    }

    void ReleaseDrop(Drop d)
    {
        d.alive = false;
        d.def = null;
        d.inPhysics = false;
        d.exploding = false;
        d.body.bodyType = RigidbodyType2D.Kinematic;
        d.body.simulated = false;
        d.body.velocity = Vector2.zero;
        d.col.enabled = false;
        d.sr.color = Color.white;
        d.go.SetActive(false);
        d.go.transform.localPosition = new Vector3(0f, SPAWN_Y * INV_PX2U, 0f);
    }

    /// <summary>白块 sprite（4×4 纯色纹理，pixelsPerUnit=100 → 一块 0.04 unit，用 BlockScale 放大）。</summary>
    static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            return _whiteSprite;
        }
    }

    /// <summary>把白块（0.04 unit）放大到 wPx×hPx 逻辑像素：scale = px×0.01 / 0.04 = px×0.25。</summary>
    static Vector3 BlockScale(float wPx, float hPx) => new Vector3(wPx * 0.25f, hPx * 0.25f, 1f);

    Text MakeText(string name, string content, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    RectTransform MakeBox(string name, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.transform as RectTransform;
        rt.sizeDelta = size;
        return rt;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.pivot = pivot;
    }
}
