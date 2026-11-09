using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 《跑酷小游戏》V1 —— T2 主框架：可跑的空赛道（2026-11-09）。
///
/// 规格来源：`Docs/《跑酷小游戏》_待办任务清单.md`（§2.1 视角与场景 / §2.2 单指跳 /
/// §3 首版标定量 / §6 验收总纲 / §10 施工红线）。
///
/// 【架构】玩法世界 = 独立正交相机 + 专属层 MiniGameWorld（TagManager layer 8）：
///   - 相机始终不动，只滚场景（每帧改 chunk 的 logicX，绝不触碰相机 transform）；
///   - 判定 / 世界坐标换算 1 unit = 100 逻辑像素（INV_PX2U = 0.01）；
///   - 两层滚动：背景地图 ×0.30（按高度 cover 铺满 + 左右镜像拼接循环）、
///     前景平台 ×1.00（横向平铺，同层同速，站上去不打滑）；
///   - 玩家 x 固定，只有 y 随单指跳变化；
///   - 渲染缩放 0.7 只作用 SpriteRenderer 所属 transform 的 localScale，判定 / 世界坐标一个都不动。
///
/// 【本期范围】只交付空赛道：不含怪 / 波次 / 佣兵队列 / HUD / 结算 / 入口 / 传送门 / 宝箱。
/// 攻击与碰撞判定留给后续任务；本框架只维护公开的只读状态 IsAirborne，
/// 后续攻击 / 碰撞 / 伤害结算须以 !IsAirborne 为前置闸门（空中一律跳过）。
/// </summary>
public partial class EndlessRunGame : MonoBehaviour
{
    public static EndlessRunGame Instance { get; private set; }

    // ---- 常量 ----
    const int SortOrder = GameConfig.UiSort.TownPopup;   // 900，与竹篓 / 城镇弹窗同级
    const float INV_PX2U = 0.01f;                        // 1 unit = 100 逻辑像素
    const string WORLD_LAYER = "MiniGameWorld";          // TagManager.layers[8]
    const float CAM_ORTHO_SIZE = 640f * INV_PX2U;        // 6.4 unit（半高 640 逻辑 px）
    const float CAM_BASE_DEPTH = 90f;
    const float COVER_H_U = 1280f * INV_PX2U;            // 12.8 unit，背景 cover 目标全高

    // ---- 资源路径（三张图，按路径加载，不兜底）----
    const string SP_BG_1 = "UI/Art/小游戏/跑酷/1";
    const string SP_BG_2 = "UI/Art/小游戏/跑酷/2";
    const string SP_GROUND = "UI/Art/小游戏/跑酷/草岩平台地面";

    /// <summary>分段 Chunk：一层里的一个可复用片段。logicX 是左端的判定 / 世界坐标（逻辑 px）。</summary>
    class Chunk
    {
        public GameObject go;
        public readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
        public float logicX;     // 左端逻辑 x，绝不乘 RENDER_SCALE
        public float width;      // 逻辑宽度（构建期常量，用于回收 / 补充）
        public bool isBg;
        public bool alive;
    }

    // ---- 数值（RunTrackTable 契约，Ensure 里 EnsureLoaded，Build 里读取）----
    float _worldSpeedPx, _bgFactor, _fgFactor, _pxPerMeter, _renderScale;
    float _jumpHeightPx, _jumpAirSec, _landStunSec, _inputBufferSec;
    float _chunkWidthPx, _groundHPx, _playerXPx, _groundTopYPx, _viewMarginPx;
    int _chunkPoolSize;

    // ---- 相机 / 层 ----
    int _worldLayer = -1;
    Camera _playCam;
    GameObject _worldRoot;
    Canvas _canvas;

    // ---- 资源 ----
    Sprite _bgSprite1, _bgSprite2, _groundSprite;
    bool _bgLayerReady, _fgLayerReady;

    // ---- 构建期几何 ----
    float _bgChunkWidthPx, _fgChunkWidthPx;
    Vector3 _bgTileScale, _groundTileScale;   // _groundTileScale 含 0.7；_bgTileScale 为 cover（不缩）
    float _bgTileWU, _groundTileWU, _groundTileLocalYU;
    int _groundTileCount;

    // ---- 池（内嵌实例池，不使用 PoolManager）----
    readonly List<Chunk> _pool = new List<Chunk>();
    readonly List<Chunk> _bgActive = new List<Chunk>();
    readonly List<Chunk> _fgActive = new List<Chunk>();
    int _bgSpawnIndex;

    // ---- 玩家 / 跳跃 ----
    GameObject _playerGo;
    float _playerY, _jumpT, _stunT, _bufferT;

    /// <summary>是否在空中：后续攻击 / 碰撞 / 伤害判定须以 !IsAirborne 为前置闸门（空中不攻击不碰撞）。</summary>
    public bool IsAirborne { get; private set; }

    /// <summary>里程（独立 float 累加，1 m = _pxPerMeter 逻辑像素）。</summary>
    public float Meters { get; private set; }

    bool _playing, _appPaused, _built;

    // ============================================================
    // 生命周期 / 对外
    // ============================================================

    public static void Show() => Ensure().Open();

    public static EndlessRunGame Ensure()
    {
        RunTrackTable.EnsureLoaded();
        if (Instance != null) return Instance;
        var root = new GameObject("EndlessRunGame", typeof(RectTransform));
        DontDestroyOnLoad(root);
        var game = root.AddComponent<EndlessRunGame>();
        game.Build();
        return game;
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
        // 切后台冻结主循环（Update 早退）；回来按原状态继续，不重置里程 / 跳跃 / chunk。
        _appPaused = paused;
    }

    // ============================================================
    // 建树
    // ============================================================

    void Build()
    {
        _worldLayer = LayerMask.NameToLayer(WORLD_LAYER);
        if (_worldLayer < 0)
        {
            Debug.LogError("[跑酷] 找不到层 " + WORLD_LAYER + "（TagManager.layers[8]），玩法无法渲染，检查 ProjectSettings/TagManager.asset");
            return;
        }

        ReadTuning();
        BuildCamera();
        BuildUi();

        // 三张图全按路径加载；取不到 LogError 并退出该层构建（不建几何 / 不建池 / 不滚动），
        // 绝不写纯色 / 占位图兜底。
        _bgSprite1 = Resources.Load<Sprite>(SP_BG_1);
        _bgSprite2 = Resources.Load<Sprite>(SP_BG_2);
        _groundSprite = Resources.Load<Sprite>(SP_GROUND);
        if (_bgSprite1 == null) Debug.LogError("[跑酷] 背景图 1 加载失败，退出背景层构建。Resources 路径：" + SP_BG_1);
        if (_bgSprite2 == null) Debug.LogError("[跑酷] 背景图 2 加载失败，退出背景层构建。Resources 路径：" + SP_BG_2);
        if (_groundSprite == null) Debug.LogError("[跑酷] 平台图加载失败，退出前景层构建。Resources 路径：" + SP_GROUND);
        // 1.jpg 是背景层底图；2.jpg 本期只加载不切换（传送门切图是后续任务）。
        // 两张背景图任一缺失都不建背景层。
        _bgLayerReady = _bgSprite1 != null && _bgSprite2 != null;
        _fgLayerReady = _groundSprite != null;

        if (_bgLayerReady) ComputeBgGeometry();
        if (_fgLayerReady) ComputeFgGeometry();
        BuildPlayer();
        BuildPool();

        gameObject.SetActive(false);
        _built = true;
    }

    /// <summary>全部可调数值走 RunTrackTable，默认值取文档 §3 / 本框架结构常量。</summary>
    void ReadTuning()
    {
        _worldSpeedPx   = RunTrackTable.GetFloat("world_scroll_speed", 300f);
        _bgFactor       = RunTrackTable.GetFloat("bg_scroll_multiplier", 0.30f);
        _fgFactor       = RunTrackTable.GetFloat("fg_scroll_multiplier", 1.00f);
        _pxPerMeter     = RunTrackTable.GetFloat("pixels_per_meter", 100f);
        _renderScale    = RunTrackTable.GetFloat("render_scale", 0.7f);
        _jumpHeightPx   = RunTrackTable.GetFloat("jump_height", 180f);
        _jumpAirSec     = RunTrackTable.GetFloat("jump_air_time", 0.8f);
        _landStunSec    = RunTrackTable.GetFloat("jump_land_stun", 0.2f);
        _inputBufferSec = RunTrackTable.GetFloat("input_buffer", 0.15f);
        _chunkWidthPx   = RunTrackTable.GetFloat("chunk_width_px", 720f);
        _groundHPx      = RunTrackTable.GetFloat("ground_height_px", 160f);
        _playerXPx      = RunTrackTable.GetFloat("player_x_px", -120f);
        _groundTopYPx   = RunTrackTable.GetFloat("ground_top_y_px", -400f);
        _viewMarginPx   = RunTrackTable.GetFloat("view_margin_px", 480f);
        _chunkPoolSize  = RunTrackTable.GetInt("chunk_pool_size", 24);
    }

    /// <summary>独立正交相机：只渲染玩法层，参数固定；Update 里绝不触碰它的 transform。</summary>
    void BuildCamera()
    {
        _worldRoot = new GameObject("EndlessRunWorld");
        _worldRoot.transform.SetParent(transform, false);
        _worldRoot.layer = _worldLayer;

        var camGo = new GameObject("EndlessRunCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
        camGo.layer = _worldLayer;
        _playCam = camGo.AddComponent<Camera>();
        _playCam.orthographic = true;
        _playCam.orthographicSize = CAM_ORTHO_SIZE;                 // 640f * 0.01f = 6.4
        _playCam.clearFlags = CameraClearFlags.SolidColor;
        _playCam.cullingMask = 1 << _worldLayer;
        _playCam.depth = Mathf.Max(CAM_BASE_DEPTH, Camera.main != null ? Camera.main.depth + 1f : CAM_BASE_DEPTH);
        _playCam.stereoTargetEye = StereoTargetEyeMask.None;
        // 不设 backgroundColor：不写任何纯色 / 占位图兜底，画面由 cover 背景 sprite 铺满。
    }

    void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        _canvas = canvas;
        UICanvasSetup.ApplyPopup(canvas, SortOrder);

        // 全屏 raycast 底图（§6 验收总纲）：挡住下层 UI 点击；本玩法唯一输入是全局单指跳，
        // 底图不挂 Button，跳跃靠 Input 全局检测，不受 EventSystem 拦截。
        var backdrop = new GameObject("RaycastBackdrop", typeof(RectTransform));
        backdrop.transform.SetParent(transform, false);
        var img = backdrop.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;
        var rt = backdrop.transform as RectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>背景 cover：按高度铺满 12.8 unit、宽超出裁掉，不变形（照竹篓 BuildWorld 的换算）。</summary>
    void ComputeBgGeometry()
    {
        float hU = _bgSprite1.rect.height / Mathf.Max(1f, _bgSprite1.pixelsPerUnit);
        float wU = _bgSprite1.rect.width / Mathf.Max(1f, _bgSprite1.pixelsPerUnit);
        float k = hU > 0.001f ? COVER_H_U / hU : 1f;
        // 背景是铺满的地图层，不参与实体 0.7 缩放（§2.1 只缩人物 / 平台 / 怪）。
        _bgTileScale = new Vector3(k, k, 1f);
        _bgTileWU = wU * k;
        _bgChunkWidthPx = _bgTileWU / INV_PX2U;   // 一个 chunk = 一张背景图
    }

    /// <summary>前景横向平铺：tile 按目标带高等比缩放，localScale 乘 0.7（只作用渲染）。</summary>
    void ComputeFgGeometry()
    {
        float hU = _groundSprite.rect.height / Mathf.Max(1f, _groundSprite.pixelsPerUnit);
        float wU = _groundSprite.rect.width / Mathf.Max(1f, _groundSprite.pixelsPerUnit);
        float s = hU > 0.001f ? (_groundHPx * INV_PX2U) / hU : 1f;
        s *= _renderScale;                         // ← 0.7 只落在这里（渲染 localScale）
        _groundTileScale = new Vector3(s, s, 1f);
        _groundTileWU = wU * s;                    // 单张 tile 渲染宽度（unit）
        float tileLogicW = _groundTileWU / INV_PX2U;
        _groundTileCount = Mathf.Max(1, Mathf.RoundToInt(_chunkWidthPx / Mathf.Max(1f, tileLogicW)));
        _fgChunkWidthPx = _groundTileCount * tileLogicW;   // chunk 逻辑宽度 = 整数张 tile，边界无缝
        _groundTileLocalYU = -_groundHPx * 0.5f * INV_PX2U;  // chunk 原点 = 地面顶面，tile 中心下移半带高
    }

    void BuildPlayer()
    {
        _playerGo = new GameObject("Player");
        _playerGo.transform.SetParent(_worldRoot.transform, false);
        _playerGo.layer = _worldLayer;
        var sr = _playerGo.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;
        // 角色立绘由后续队列任务接入（本框架无角色图，也不写占位兜底）；
        // 这里只建渲染缩放槽位：0.7 只作用 localScale，判定坐标不动。
        _playerGo.transform.localScale = new Vector3(_renderScale, _renderScale, 1f);
    }

    void BuildPool()
    {
        int half = Mathf.Max(1, _chunkPoolSize / 2);
        if (_bgLayerReady)
            for (int i = 0; i < half; i++) _pool.Add(CreateChunk(true));
        if (_fgLayerReady)
            for (int i = 0; i < half; i++) _pool.Add(CreateChunk(false));
    }

    // ============================================================
    // 开局 / 关闭
    // ============================================================

    public void Open()
    {
        gameObject.SetActive(true);
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (_canvas != null) UICanvasSetup.RefreshPopup(_canvas, SortOrder);
        if (_built) ResetRun();
        _playing = true;
    }

    public void Close()
    {
        _playing = false;
        gameObject.SetActive(false);
    }

    void ResetRun()
    {
        for (int i = 0; i < _bgActive.Count; i++) ReleaseChunk(_bgActive[i]);
        for (int i = 0; i < _fgActive.Count; i++) ReleaseChunk(_fgActive[i]);
        _bgActive.Clear();
        _fgActive.Clear();
        _bgSpawnIndex = 0;

        Meters = 0f;
        _playerY = _groundTopYPx;
        _jumpT = _stunT = _bufferT = 0f;
        IsAirborne = false;
        ApplyPlayerPos();

        float halfW = ViewHalfWPx();
        float left = -halfW - _viewMarginPx;
        float right = halfW + _viewMarginPx;
        // 复用补充逻辑铺初始赛道（list 空 → 从 left 铺到 right）。
        if (_bgLayerReady) RecycleLayer(_bgActive, true, left, right);
        if (_fgLayerReady) RecycleLayer(_fgActive, false, left, right);
    }

    // ============================================================
    // 每帧主循环（相机始终不动）
    // ============================================================

    void Update()
    {
        if (!_built || !_playing || _appPaused) return;
        float dt = Time.deltaTime;
        UpdateInput();         // 单指跳：唯一输入
        UpdateJump(dt);        // 跳跃 y / IsAirborne / 硬直 / 输入缓冲
        ScrollWorld(dt);       // 两层滚动 + 里程累加；不触碰 _playCam.transform
        RecycleChunks();       // 出视野回收复用
        ApplyPlayerPos();      // 玩家 x 固定，仅 y 随跳跃
    }

    /// <summary>单指点屏幕任意处 = 跳；其余输入（键盘 / 多指 / 滑动 / 右键等）一律不处理。</summary>
    void UpdateInput()
    {
        // 单指触摸在 Unity 映射为鼠标左键按下（与竹篓 UpdateDrag 同口径）。
        if (!Input.GetMouseButtonDown(0)) return;
        if (!IsAirborne && _stunT <= 0f)
            StartJump();
        else
            _bufferT = _inputBufferSec;   // 空中 / 落地硬直期间记输入缓冲
    }

    /// <summary>
    /// 跳跃：高 _jumpHeightPx / 滞空 _jumpAirSec / 落地硬直 _landStunSec / 输入缓冲 _inputBufferSec。
    /// 空中不攻击、不碰撞 —— 本框架不实现攻击 / 碰撞判定（后续任务范围），
    /// 只维护公开的 IsAirborne 只读状态作为后续判定的前置闸门。
    /// </summary>
    void UpdateJump(float dt)
    {
        if (_stunT > 0f) _stunT = Mathf.Max(0f, _stunT - dt);
        if (_bufferT > 0f) _bufferT = Mathf.Max(0f, _bufferT - dt);

        // 缓冲消费：可起跳且缓冲未过期 → 立刻起跳。
        if (!IsAirborne && _stunT <= 0f && _bufferT > 0f)
        {
            _bufferT = 0f;
            StartJump();
        }

        if (!IsAirborne) return;
        _jumpT += dt;
        if (_jumpT >= _jumpAirSec)
        {
            _jumpT = 0f;
            IsAirborne = false;
            _playerY = _groundTopYPx;
            _stunT = _landStunSec;
        }
        else
        {
            float k = _jumpT / Mathf.Max(0.0001f, _jumpAirSec);
            _playerY = _groundTopYPx + _jumpHeightPx * Mathf.Sin(Mathf.PI * k);
        }
    }

    void StartJump()
    {
        IsAirborne = true;
        _jumpT = 0f;
        _playerY = _groundTopYPx;
    }

    /// <summary>两层同层不同速：背景 ×_bgFactor(0.30) / 前景 ×_fgFactor(1.00)，前景内部同速不打滑。</summary>
    void ScrollWorld(float dt)
    {
        float bgDx = _worldSpeedPx * _bgFactor * dt;
        float fgDx = _worldSpeedPx * _fgFactor * dt;
        if (_bgLayerReady)
            for (int i = 0; i < _bgActive.Count; i++) { var c = _bgActive[i]; c.logicX -= bgDx; ApplyChunkPos(c); }
        if (_fgLayerReady)
            for (int i = 0; i < _fgActive.Count; i++) { var c = _fgActive[i]; c.logicX -= fgDx; ApplyChunkPos(c); }
        // 里程独立 float 累加，1 m = _pxPerMeter 逻辑像素，按 ×1.00 层的卷速计。
        Meters += fgDx / Mathf.Max(1f, _pxPerMeter);
    }

    void RecycleChunks()
    {
        float halfW = ViewHalfWPx();
        float leftEdge = -halfW - _viewMarginPx;
        float rightEdge = halfW + _viewMarginPx;
        if (_bgLayerReady) RecycleLayer(_bgActive, true, leftEdge, rightEdge);
        if (_fgLayerReady) RecycleLayer(_fgActive, false, leftEdge, rightEdge);
    }

    /// <summary>出视野回收 + 右端补足；背景按 _bgSpawnIndex 奇偶做左右镜像拼接循环。</summary>
    void RecycleLayer(List<Chunk> list, bool isBg, float leftEdge, float rightEdge)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var c = list[i];
            if (c.logicX + c.width < leftEdge)
            {
                ReleaseChunk(c);
                list.RemoveAt(i);
            }
        }

        float rightMost = leftEdge;
        for (int i = 0; i < list.Count; i++)
            rightMost = Mathf.Max(rightMost, list[i].logicX + list[i].width);
        while (rightMost < rightEdge)
        {
            var c = TakeChunk(isBg);
            if (c == null) break;
            bool flip = isBg && (_bgSpawnIndex++ % 2 == 1);
            ConfigureChunk(c, rightMost, flip);
            list.Add(c);
            rightMost += c.width;
        }
    }

    // ============================================================
    // Chunk 池（内嵌实例池，照竹篓 TakeDrop / ReleaseDrop 思路；不使用 PoolManager）
    // ============================================================

    Chunk TakeChunk(bool isBg)
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            var c = _pool[i];
            if (!c.alive && c.isBg == isBg) { c.alive = true; return c; }
        }
        if (isBg && !_bgLayerReady) return null;
        if (!isBg && !_fgLayerReady) return null;
        var n = CreateChunk(isBg);
        n.alive = true;
        _pool.Add(n);
        return n;
    }

    void ReleaseChunk(Chunk c)
    {
        c.alive = false;
        c.go.SetActive(false);
    }

    Chunk CreateChunk(bool isBg)
    {
        var go = new GameObject(isBg ? "BgChunk" : "FgChunk");
        go.transform.SetParent(_worldRoot.transform, false);
        go.layer = _worldLayer;
        var c = new Chunk { go = go, isBg = isBg };

        if (isBg)
        {
            var t = NewTile(go, "BgTile", _bgSprite1, -20);
            t.transform.localScale = _bgTileScale;                       // cover，不含 0.7
            t.transform.localPosition = new Vector3(_bgTileWU * 0.5f, 0f, 0f);
            c.tiles.Add(t);
            c.width = _bgChunkWidthPx;
        }
        else
        {
            for (int i = 0; i < _groundTileCount; i++)
            {
                var t = NewTile(go, "GroundTile_" + i, _groundSprite, 1);
                t.transform.localScale = _groundTileScale;               // 含 0.7（只作用渲染）
                t.transform.localPosition = new Vector3((i + 0.5f) * _groundTileWU, _groundTileLocalYU, 0f);
                c.tiles.Add(t);
            }
            c.width = _fgChunkWidthPx;
        }
        go.SetActive(false);
        return c;
    }

    SpriteRenderer NewTile(GameObject parent, string name, Sprite sp, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.layer = _worldLayer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = order;
        return sr;
    }

    void ConfigureChunk(Chunk c, float logicX, bool flip)
    {
        c.logicX = logicX;
        if (c.isBg && c.tiles.Count > 0) c.tiles[0].flipX = flip;   // 左右镜像拼接
        ApplyChunkPos(c);
        c.go.SetActive(true);
    }

    /// <summary>
    /// 判定 / 世界坐标落位：logicX 是逻辑像素，只乘 INV_PX2U 转 unit，绝不乘 0.7。
    /// 0.7 只出现在 tile / 玩家 transform 的 localScale 上（渲染层），此处一行即证据。
    /// </summary>
    void ApplyChunkPos(Chunk c)
    {
        float y = c.isBg ? 0f : _groundTopYPx * INV_PX2U;
        c.go.transform.localPosition = new Vector3(c.logicX * INV_PX2U, y, 0f);
    }

    void ApplyPlayerPos()
    {
        if (_playerGo == null) return;
        // 玩家 x 固定 _playerXPx；判定 / 世界坐标不含 0.7（0.7 只在 _playerGo.localScale）。
        _playerGo.transform.localPosition = new Vector3(_playerXPx * INV_PX2U, _playerY * INV_PX2U, 0f);
    }

    // ============================================================
    // 工具
    // ============================================================

    float ViewHalfWPx()
    {
        if (_playCam == null) return 480f;
        float aspect = _playCam.aspect > 0.01f ? _playCam.aspect : (720f / 1280f);
        return _playCam.orthographicSize * aspect / INV_PX2U;
    }
}
