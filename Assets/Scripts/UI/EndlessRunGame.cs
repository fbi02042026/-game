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
    // 玩家 / 佣兵已改 SPUM 预制体（2026-11-11，T3），原 PORTRAIT_TARGET_H（单张立绘目标高度）
    // 随之废弃；SPUM 的显示高度见 EndlessRunGame.Spum.cs 的 SPUM_UNIT_SCALE。

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
    float _coyoteTime, _jumpCutSpeed, _fallGravityMul;   // 跳跃手感（视频 8 设置：土狼 / 截断 / 下落实沉）
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
    float _playerY;                 // 当前 y（逻辑像素，地面 = _groundTopYPx）
    float _playerVy;                // 当前垂直速度（逻辑像素/秒，正=上升，负=下落）
    float _gravityPx;               // 上升时重力（逻辑像素/秒²）
    float _jumpStunT, _bufferT, _coyoteT, _peakHangT;
    bool _jumpHeld;                 // 跳跃键是否仍按住（可变高度：按住继续上升，松开截断）
    bool _peakReached;              // 本次跳跃是否已过最高点（用于下落实沉感切换重力）

    // ---- 跳跃轨迹历史（佣兵跟跳延迟复现用；每帧存 _playerY，环形缓冲，10 秒 @60fps = 600 帧）----
    const int JUMP_HISTORY_LEN = 600;
    readonly float[] _jumpHistory = new float[JUMP_HISTORY_LEN];
    int _jumpHistoryIdx;

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
        // 视频"8 隐藏跳跃手感"：土狼 / 截断 / 下落实沉
        _coyoteTime     = RunTrackTable.GetFloat("coyote_time", 0.12f);
        _jumpCutSpeed   = RunTrackTable.GetFloat("jump_cut_speed", 600f);      // 截断后立即进入的下落速度（px/s，正绝对值）
        _fallGravityMul = RunTrackTable.GetFloat("fall_gravity_mult", 1.5f);   // 下落重力倍率（比上升重，落得稳）
        // 重力：从 jump_height 和 jump_air_time 反推（抛物线顶点 = _jumpHeightPx，到顶时间 = _jumpAirSec/2）
        // h = ½ g t² → g = 2h / t²，t = _jumpAirSec * 0.5
        float tTop = Mathf.Max(0.05f, _jumpAirSec * 0.5f);
        _gravityPx = 2f * _jumpHeightPx / (tTop * tTop);   // 上升重力（px/s²）
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

        // 【2026-11-11 T3】玩家形象改用 SPUM 预制体（Units/wanjia），替掉 11-10 的单张立绘。
        // 空间分工：_playerGo 只做位置驱动（x 固定 / y 随跳跃，见 ApplyPlayerPos），
        // SPUM 作为子节点局部归零 —— SPUM 根原点在脚底，正好落在 _playerGo 的地面顶面高度。
        var spum = CreateSpumUnit(SPUM_PLAYER_ID, SPUM_SORT_BASE_PLAYER, "PlayerSpum");
        if (spum == null)
        {
            Debug.LogError("[跑酷] 玩家 SPUM 未建成（Units/" + SPUM_PLAYER_ID + "），跑酷中看不到玩家形象。");
            return;
        }
        spum.transform.SetParent(_playerGo.transform, false);
        spum.transform.localPosition = Vector3.zero;
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
        CompensateStageScale();
    }

    /// <summary>
    /// 玩法世界挂在 Canvas 根下，会继承项目统一的 0.01 缩放（720 逻辑 px = 7.2 unit）；
    /// 但相机 orthographicSize=6.4 不吃 transform 缩放，1 unit = 100 逻辑 px 的世界坐标
    /// 就全挤到屏幕中心 1%，画面一片空白（2026-11-10 实测 worldLossyScale=0.01）。
    /// 这里把世界层的缩放抵消回 1，并把相机 z=-10 按同倍率还原，保证近平面裁不到 sprite。
    /// 只在 Open() 调用：此时 Canvas 已 ApplyPopup，lossyScale 是最终值；幂等，重复调用结果不变。
    /// </summary>
    void CompensateStageScale()
    {
        float s = transform.lossyScale.x;
        if (s <= 0.0001f || Mathf.Approximately(s, 1f)) return;
        if (_worldRoot != null)
            _worldRoot.transform.localScale = Vector3.one * (1f / s);
        if (_playCam != null)
            _playCam.transform.localPosition = new Vector3(0f, 0f, -10f / s);
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
        _playerVy = 0f;
        _jumpStunT = _bufferT = _coyoteT = _peakHangT = 0f;
        _jumpHeld = false;
        _peakReached = false;
        _jumpHistoryIdx = 0;
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
        float dt = Time.deltaTime * _runTimeScale;
        UpdateInput();         // 单指跳：唯一输入
        UpdateJump(dt);        // 跳跃 y / IsAirborne / 硬直 / 输入缓冲
        ScrollWorld(dt);       // 两层滚动 + 里程累加；不触碰 _playCam.transform
        RecycleChunks();       // 出视野回收复用
        ApplyPlayerPos();      // 玩家 x 固定，仅 y 随跳跃
    }

    /// <summary>
    /// 单指点屏幕任意处 = 跳；其余输入一律不处理。
    /// 【视频 8 设置】记录按键按住/松开状态（可变高度 + 提前截断）。
    /// </summary>
    void UpdateInput()
    {
        // 天降二选一窗口内：全局单指跳禁用，避免点按钮同时触发跳跃。
        if (_choiceOpen) return;

        // 按住状态（可变跳跃高度 + 提前截断）
        _jumpHeld = Input.GetMouseButton(0);

        // 单指触摸在 Unity 映射为鼠标左键按下（与竹篓 UpdateDrag 同口径）。
        if (Input.GetMouseButtonDown(0))
        {
            // 【视频 8 设置 - 土狼时间 + 跳跃缓冲】
            // ① 站地硬直过了 → 立刻起跳；
            // ② 土狼窗口内（刚离开地面还能跳）→ 立刻起跳；
            // ③ 否则记入输入缓冲（落地后自动消费）。
            bool canGroundJump = !IsAirborne && _jumpStunT <= 0f;
            bool canCoyoteJump = IsAirborne && !_peakReached && _coyoteT > 0f && _playerVy <= 0f;
            if (canGroundJump || canCoyoteJump)
            {
                _bufferT = 0f;
                StartJump();
            }
            else
            {
                _bufferT = _inputBufferSec;   // 空中 / 硬直 / 没土狼窗口 → 记缓冲
            }
        }
    }

    /// <summary>
    /// 跳跃物理：抛物线（初速度 + 重力）。
    /// 【视频 8 设置 — 完整手感链】
    ///   ① 跳跃缓冲：提前按跳、落地自动起跳；
    ///   ② 土狼时间：离开地面后 _coyoteTime 内还能跳；
    ///   ③ 可变跳跃高度：按住 = 正常上升，松开 = 立即截断下落（jump cut）；
    ///   ④ 下落实沉感：过顶后下落重力 = 上升 × _fallGravityMul（落得稳不飘）。
    /// 空中不攻击、不碰撞 —— 本框架只维护 IsAirborne 作为后续判定的前置闸门。
    /// </summary>
    void UpdateJump(float dt)
    {
        // ---- 计时器 ----
        if (_jumpStunT > 0f) _jumpStunT = Mathf.Max(0f, _jumpStunT - dt);
        if (_bufferT > 0f) _bufferT = Mathf.Max(0f, _bufferT - dt);
        if (_coyoteT > 0f)  _coyoteT  = Mathf.Max(0f, _coyoteT - dt);

        // ---- 缓冲消费：可起跳（站地 + 硬直过了）且缓冲未过期 → 立刻起跳 ----
        if (!IsAirborne && _jumpStunT <= 0f && _bufferT > 0f)
        {
            _bufferT = 0f;
            StartJump();
        }

        // ---- 空中物理 ----
        if (!IsAirborne)
        {
            // 站地时维持地面高度（防止滚动抖动造成 y 偏移）
            _playerY = _groundTopYPx;
            _playerVy = 0f;
            return;
        }

        // 截断触发：上升中松开跳跃键 → 立即进入快速下落（jump cut，视频设置 3）
        if (_playerVy > 0f && !_jumpHeld)
        {
            _playerVy = -_jumpCutSpeed;
        }

        // 重力：上升 = _gravityPx（正常），下落 = _fallGravityMul × _gravityPx（落得实）
        float grav = _playerVy > 0f ? _gravityPx : _gravityPx * _fallGravityMul;
        _playerVy -= grav * dt;
        _playerY += _playerVy * dt;

        // 过顶检测（从上升变下落 → 启用下落实沉感 + 启动土狼窗口给补救跳）
        if (!_peakReached && _playerVy < 0f)
        {
            _peakReached = true;
            _coyoteT = _coyoteTime;   // 下落开始那一瞬间启动土狼窗口（视频设置 2）
        }

        // 触地判定
        if (_playerY <= _groundTopYPx)
        {
            _playerY = _groundTopYPx;
            _playerVy = 0f;
            IsAirborne = false;
            _peakReached = false;
            _coyoteT = 0f;
            _jumpStunT = _landStunSec;
        }

        // 每帧存跳跃轨迹历史（佣兵跟跳延迟复现用）
        _jumpHistory[_jumpHistoryIdx] = _playerY;
        _jumpHistoryIdx = (_jumpHistoryIdx + 1) % JUMP_HISTORY_LEN;
    }

    void StartJump()
    {
        // 抛物线初速度：从 _jumpHeightPx 和上升重力反推
        // v0 = sqrt(2 × g × h)
        float v0 = Mathf.Sqrt(2f * _gravityPx * _jumpHeightPx);
        _playerVy = v0;
        _playerY = _groundTopYPx;
        IsAirborne = true;
        _peakReached = false;
        _jumpStunT = 0f;
        _coyoteT = 0f;    // 起跳瞬间清零（刚离开就有土狼窗口，避免双算）
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
