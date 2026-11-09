using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 《跑酷小游戏》V1 —— T3 生成器（EndlessRunGame 分部，2026-11-09）。
///
/// 规格来源：Docs/《跑酷小游戏》_待办任务清单.md §2.3 / §3 / §4 / §6 / §10，
/// 数值全部走 RunTrackTable（真源 run_track.csv），key 见 ReadWorldTuning。
///
/// 【本分部职责】只做生成与逻辑数据：7 波次模板 + 洗牌袋、地形段/沟/高台、
/// 怪（只提供判定数据，碰撞结算交 T4）、金币弧线、100m 宝箱锚点、传送门黑屏切背景。
/// 【不改主文件】T2 主框架已有 Update，本分部只用 LateUpdate 作为自己的 tick；
/// 跳信号取 IsAirborne 上升沿，全文不调用输入 API。
/// 【0.7】渲染缩放只落在实体 transform.localScale；LogicX / 里程 / 判定坐标一律不乘 0.7。
/// </summary>
public partial class EndlessRunGame : MonoBehaviour
{
    // ============================================================
    // 对外契约（逐字固定，供 T4 / 后续分部引用）
    // ============================================================

    /// <summary>一只怪的判定数据。LogicX 是逻辑像素 x（不含 0.7 渲染缩放）。
    /// 敌人不会攻击，伤害只来自碰撞；碰撞判定交 T4，本类只提供数据。</summary>
    public sealed class RunEnemy
    {
        public float LogicX;
        public float Hp;
        public bool IsElite;
        public bool IsAlive;
        public float ColliderHalfW;     // 判定盒半宽：比图缩 15%

        public void TakeDamage(float dmg)
        {
            Hp -= dmg;
            if (Hp <= 0f) IsAlive = false;
        }

        // 渲染绑定（internal，非契约 public 成员）：由生成器实例池复用。
        internal GameObject viewGo;
        internal SpriteRenderer viewSr;
        internal float logicY;
    }

    /// <summary>当前存活的怪（判定数据）。</summary>
    public IReadOnlyList<RunEnemy> ActiveEnemies { get { return _enemies; } }

    /// <summary>T4 置位：新佣兵天降窗口等三种「跳」语义互斥期间不推新怪波。</summary>
    public bool SuppressWaveSpawn { get; set; }

    /// <summary>传送门黑屏期间 true：不刷危险、T4 据此跳过碰撞判定。</summary>
    public bool IsSafeWindow { get; private set; }

    // ============================================================
    // 常量 / 路径
    // ============================================================

    // 取模板只用精确 key：表内另有 6 个数值 key 以 wave_ 开头
    //（wave_group_gap / wave_5_length / wave_interval_min/max / wave_count_min/max），
    // 绝不按前缀遍历取值，只用下面 7 个精确 key。
    static readonly string[] WAVE_KEYS =
    {
        "wave_1", "wave_2", "wave_3", "wave_4", "wave_5", "wave_6", "wave_7"
    };

    const string SP_MONSTER_ROOT = "Config/MonsterSpriteRegistry/1 Forest/";
    // 普通 3 种：forest_101 走路菇 / forest_105 硬壳甲虫 / forest_103 树精守卫；
    // 精英固定 forest_104 暮影森主，单独成波（不许用蘑菇当精英）。
    static readonly string[] NORMAL_MONSTER_IDS = { "forest_101", "forest_105", "forest_103" };
    const string ELITE_MONSTER_ID = "forest_104";

    // 金币 / 宝箱 / 传送门暂无跑酷专属图（T8 只交付 1/2/草岩三张）。
    // 按约定路径尝试加载；取不到 LogError 并只保留逻辑数据，绝不建纯色 / 造资源。
    const string SP_COIN = "UI/Art/小游戏/跑酷/金币";
    const string SP_CHEST = "UI/Art/小游戏/跑酷/宝箱";
    const string SP_PORTAL = "UI/Art/小游戏/跑酷/传送门";

    const float HP_TIER_METERS = 100f;     // 文档 §2.4：敌人 HP 每 100m 一档
    const int COIN_ARC_MIN_COUNT = 3;      // 弧线金币最少枚数（保证弧线成形）

    // ============================================================
    // 从 RunTrackTable 读的标定量（def 取 §3）
    // ============================================================

    float _wScrollSpeed, _wMonsterMoveSpeed, _wRelativeSpeed;
    float _wWaveGroupGap, _wWave5Length;
    float _wWaveIntervalMin, _wWaveIntervalMax;
    int _wWaveCountMin, _wWaveCountMax;
    float _wPlatformLenMin, _wPlatformLenMax, _wGapMin, _wGapMax, _wHighPlatformHeight;
    float _wCoinDensity, _wChestInterval;
    float _wPortalIntervalMin, _wPortalIntervalMax, _wPortalBlackout;
    float _wEnemyHpBase, _wEnemyHpPer100m;
    float _wPxPerMeter, _wRenderScale;

    // ============================================================
    // 资源
    // ============================================================

    Sprite[] _normalMonsterSprites;     // 已过滤 null
    Sprite _eliteMonsterSprite;
    Sprite _coinSprite, _chestSprite, _portalSprite;   // 可选：无专属图时为 null

    // ============================================================
    // 内嵌数据类型
    // ============================================================

    enum StepKind { Normal, Elite, CoinArc }
    enum SegKind { Platform, Gap, High }
    enum PropKind { Coin, Chest, Portal }

    class WaveStep
    {
        public StepKind kind;
        public int count;         // Normal 只数
        public float waitBefore;  // 执行前等待秒（模板内「间隔秒」）
    }

    class WaveTemplate
    {
        public readonly List<WaveStep> steps = new List<WaveStep>();
    }

    class Segment
    {
        public float left, right;
        public SegKind kind;
        public bool hasGround;    // Platform / High = true；Gap = false
        public float groundTopY;  // 逻辑 y
    }

    class WorldProp
    {
        public PropKind kind;
        public float logicX, logicY;
        public int anchorM;       // 宝箱里程锚点
        public bool alive;
        public GameObject go;     // 无专属图时为 null（纯逻辑数据）
        public SpriteRenderer sr;
    }

    // ============================================================
    // 运行时状态
    // ============================================================

    readonly List<RunEnemy> _enemies = new List<RunEnemy>();     // 活跃（ActiveEnemies 后端）
    readonly List<RunEnemy> _enemyPool = new List<RunEnemy>();   // 全量（含回收）
    readonly List<WorldProp> _props = new List<WorldProp>();
    readonly List<WorldProp> _propPool = new List<WorldProp>();
    readonly List<Segment> _segments = new List<Segment>();

    readonly HashSet<int> _chestAnchors = new HashSet<int>();

    WaveTemplate[] _templates;          // 7 个预解析模板
    readonly List<int> _waveBag = new List<int>();
    bool _lastWasHard;

    List<WaveStep> _curSteps;
    int _stepIndex;
    float _waitTimer;
    bool _betweenWaves;
    float _betweenWaveTimer;

    int _segPhase;
    float _nextPortalMeters;
    bool _portalApproaching, _portalLatched;
    WorldProp _portalProp;
    Sprite _curBgSprite;
    float _blackoutT;

    bool _wasAirborne;
    float _prevMeters;
    bool _worldReady;

    // ============================================================
    // 初始化 / 重开
    // ============================================================

    void InitWorld()
    {
        ReadWorldTuning();
        ValidateSpeedKeys();
        LoadMonsterSprites();
        LoadPropSprites();
        BuildWaveTemplates();
        _worldReady = true;
        ResetWorldState();
    }

    void ReadWorldTuning()
    {
        _wScrollSpeed = RunTrackTable.GetFloat("world_scroll_speed", 300f);
        _wMonsterMoveSpeed = RunTrackTable.GetFloat("monster_move_speed", 120f);
        _wRelativeSpeed = RunTrackTable.GetFloat("relative_speed", 420f);
        _wWaveGroupGap = RunTrackTable.GetFloat("wave_group_gap", 130f);
        _wWave5Length = RunTrackTable.GetFloat("wave_5_length", 520f);
        _wWaveIntervalMin = RunTrackTable.GetFloat("wave_interval_min", 1.0f);
        _wWaveIntervalMax = RunTrackTable.GetFloat("wave_interval_max", 2.0f);
        _wWaveCountMin = RunTrackTable.GetInt("wave_count_min", 1);
        _wWaveCountMax = RunTrackTable.GetInt("wave_count_max", 5);
        _wPlatformLenMin = RunTrackTable.GetFloat("platform_len_min", 400f);
        _wPlatformLenMax = RunTrackTable.GetFloat("platform_len_max", 900f);
        _wGapMin = RunTrackTable.GetFloat("gap_min", 120f);
        _wGapMax = RunTrackTable.GetFloat("gap_max", 200f);
        _wHighPlatformHeight = RunTrackTable.GetFloat("high_platform_height", 180f);
        _wCoinDensity = RunTrackTable.GetFloat("coin_density", 100f);
        _wChestInterval = RunTrackTable.GetFloat("chest_interval", 100f);
        _wPortalIntervalMin = RunTrackTable.GetFloat("portal_interval_min", 200f);
        _wPortalIntervalMax = RunTrackTable.GetFloat("portal_interval_max", 300f);
        _wPortalBlackout = RunTrackTable.GetFloat("portal_blackout", 0.25f);
        _wEnemyHpBase = RunTrackTable.GetFloat("enemy_hp_base", 40f);
        _wEnemyHpPer100m = RunTrackTable.GetFloat("enemy_hp_per_100m", 40f);
        _wPxPerMeter = RunTrackTable.GetFloat("pixels_per_meter", 100f);
        _wRenderScale = RunTrackTable.GetFloat("render_scale", 0.7f);
    }

    /// <summary>三个速度 key 一致性自检：相对速度应等于世界卷速 + 怪左行（420 = 300 + 120）。</summary>
    void ValidateSpeedKeys()
    {
        float sum = _wScrollSpeed + _wMonsterMoveSpeed;
        if (Mathf.Abs(_wRelativeSpeed - sum) > 0.01f)
            Debug.LogWarning("[跑酷] relative_speed 与 world_scroll_speed + monster_move_speed 不一致："
                + _wRelativeSpeed + " vs " + sum + "，怪相对屏幕位移以表内 relative_speed 为准。");
    }

    void LoadMonsterSprites()
    {
        var ok = new List<Sprite>();
        for (int i = 0; i < NORMAL_MONSTER_IDS.Length; i++)
        {
            string path = SP_MONSTER_ROOT + NORMAL_MONSTER_IDS[i];
            var sp = Resources.Load<Sprite>(path);
            if (sp == null) Debug.LogError("[跑酷] 普通怪图加载失败（该种不出怪）：" + path);
            else ok.Add(sp);
        }
        _normalMonsterSprites = ok.ToArray();

        string elitePath = SP_MONSTER_ROOT + ELITE_MONSTER_ID;
        _eliteMonsterSprite = Resources.Load<Sprite>(elitePath);
        if (_eliteMonsterSprite == null)
            Debug.LogError("[跑酷] 精英怪图加载失败（精英波跳过）：" + elitePath);
    }

    void LoadPropSprites()
    {
        _coinSprite = Resources.Load<Sprite>(SP_COIN);
        _chestSprite = Resources.Load<Sprite>(SP_CHEST);
        _portalSprite = Resources.Load<Sprite>(SP_PORTAL);
        if (_coinSprite == null) Debug.LogError("[跑酷] 金币专属图缺失（本期只保留逻辑数据，不建纯色/造资源）：" + SP_COIN);
        if (_chestSprite == null) Debug.LogError("[跑酷] 宝箱专属图缺失（本期只建里程锚点占位数据）：" + SP_CHEST);
        if (_portalSprite == null) Debug.LogError("[跑酷] 传送门专属图缺失（切背景/黑屏逻辑保留，门体渲染待补）：" + SP_PORTAL);
    }

    // ============================================================
    // 波次模板解析（精确 key；elite / coin_arc 字符串段不做数值解析）
    // ============================================================

    void BuildWaveTemplates()
    {
        _templates = new WaveTemplate[WAVE_KEYS.Length];
        for (int i = 0; i < WAVE_KEYS.Length; i++)
            _templates[i] = ParseTemplate(WAVE_KEYS[i]);
    }

    WaveTemplate ParseTemplate(string key)
    {
        var t = new WaveTemplate();
        string raw = RunTrackTable.GetRaw(key, "");
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogError("[跑酷] 波次模板缺失：" + key);
            return t;
        }
        string[] parts = raw.Split('|');
        // 格式：「段|间隔秒|段|间隔秒|段」——偶数位为段，奇数位为间隔秒。
        for (int i = 0; i < parts.Length; i += 2)
        {
            string seg = parts[i].Trim();
            float waitBefore = 0f;
            if (i > 0)
                float.TryParse(parts[i - 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out waitBefore);

            var step = new WaveStep { waitBefore = Mathf.Max(0f, waitBefore) };
            if (int.TryParse(seg, out int n))
            {
                step.kind = StepKind.Normal;
                step.count = Mathf.Clamp(n, _wWaveCountMin, _wWaveCountMax);
            }
            else if (seg == "elite")
            {
                step.kind = StepKind.Elite;    // 精英单独成波（1 只）
            }
            else if (seg == "coin_arc")
            {
                step.kind = StepKind.CoinArc; // 跨沟金币弧线预告（非怪波）
            }
            else
            {
                Debug.LogError("[跑酷] 未识别的波次段（跳过）：" + key + " -> " + seg);
                continue;
            }
            t.steps.Add(step);
        }
        return t;
    }

    // ============================================================
    // 洗牌袋
    // ============================================================

    // 生成器内部把含 4 只段 / 极限连跳 / 精英段的模板视为「难模板」，
    // 洗牌袋用于防连续出难模板（纯生成侧实现，非文档定稿口径）。
    static bool IsHardTemplate(int id) { return id == 4 || id == 5 || id == 6; }

    void RefillWaveBag()
    {
        _waveBag.Clear();
        for (int i = 1; i <= WAVE_KEYS.Length; i++) _waveBag.Add(i);
        for (int i = _waveBag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = _waveBag[i]; _waveBag[i] = _waveBag[j]; _waveBag[j] = tmp;
        }
    }

    int DrawWaveTemplate()
    {
        if (_waveBag.Count == 0) RefillWaveBag();
        int pick = 0;
        if (_lastWasHard)
            for (int i = 0; i < _waveBag.Count; i++)
                if (!IsHardTemplate(_waveBag[i])) { pick = i; break; }
        int id = _waveBag[pick];
        _waveBag.RemoveAt(pick);
        _lastWasHard = IsHardTemplate(id);
        return id;
    }

    // ============================================================
    // 状态复位（首次 / 重开）
    // ============================================================

    void ResetWorldState()
    {
        for (int i = 0; i < _enemies.Count; i++) ReleaseEnemy(_enemies[i]);
        _enemies.Clear();
        for (int i = 0; i < _props.Count; i++) ReleaseProp(_props[i]);
        _props.Clear();
        _segments.Clear();
        _segPhase = 0;
        _chestAnchors.Clear();

        _waveBag.Clear();
        RefillWaveBag();
        _lastWasHard = false;
        _curSteps = null;
        _stepIndex = 0;
        _waitTimer = 0f;
        _betweenWaves = true;
        _betweenWaveTimer = Random.Range(_wWaveIntervalMin, _wWaveIntervalMax);

        _nextPortalMeters = Random.Range(_wPortalIntervalMin, _wPortalIntervalMax);
        _portalApproaching = false;
        _portalLatched = false;
        _portalProp = null;
        _curBgSprite = _bgSprite1;
        IsSafeWindow = false;
        _blackoutT = 0f;

        _wasAirborne = false;
        _prevMeters = 0f;
    }

    // ============================================================
    // 每帧 tick（只挂 LateUpdate；T2 主框架已有 Update）
    // ============================================================

    void LateUpdate()
    {
        if (!_built || !_playing || _appPaused) return;

        if (!_worldReady) InitWorld();

        // 重开信号：里程回落（ResetRun 把 Meters 归零，同帧 ScrollWorld 会加回一个 dt 小量，
        // 故用「回落」而非严格 ==0 判定）。T2 未提供 Open/ResetRun 钩子，零改主文件。
        if (_prevMeters > 0f && Meters < _prevMeters) ResetWorldState();
        _prevMeters = Meters;

        // 跳信号：IsAirborne 上升沿（唯一跳来源；不调用输入 API）。
        if (IsAirborne && !_wasAirborne) OnJumpEdge();
        _wasAirborne = IsAirborne;

        float dt = Time.deltaTime;
        float sceneDx = _wScrollSpeed * dt;        // 场景（前景/段/金币/宝箱/门）左移
        float monsterDx = _wRelativeSpeed * dt;    // 怪相对屏幕左移（= 世界卷速 + 怪左行，经自检）

        TickBlackout(dt);
        SyncBackground();
        TickSegments(sceneDx);
        TickEntities(sceneDx, monsterDx);
        TickChests();
        TickPortals();

        // 黑屏 / 天降窗口：不刷危险（怪波）。金币/宝箱/门为非危险，照常。
        if (!IsSafeWindow && !SuppressWaveSpawn) TickWaves(dt);
    }

    void OnJumpEdge()
    {
        // 三种「跳」语义时间互斥：门迎面 wave_group_gap×2 内起跳 = 进传送门（换人语义交 T4）。
        if (_portalApproaching && _portalProp != null &&
            _portalProp.logicX <= _playerXPx + _wWaveGroupGap * 2f)
            _portalLatched = true;
    }

    // ============================================================
    // 黑屏 / 背景切换
    // ============================================================

    void TickBlackout(float dt)
    {
        if (!IsSafeWindow) return;
        _blackoutT -= dt;
        if (_blackoutT <= 0f) IsSafeWindow = false;   // 恢复白色（显示已切换的新背景）
    }

    /// <summary>幂等纠正背景层：切图只用 T2 已加载的两张 Sprite（不重新 Resources.Load）；
    /// 黑屏期间把背景 tile 染黑。只动背景层，前景与生成参数不动。</summary>
    void SyncBackground()
    {
        if (!_bgLayerReady || _curBgSprite == null) return;
        Color c = IsSafeWindow ? Color.black : Color.white;
        for (int i = 0; i < _bgActive.Count; i++)
        {
            var chunk = _bgActive[i];
            if (chunk.tiles.Count == 0) continue;
            chunk.tiles[0].sprite = _curBgSprite;
            chunk.tiles[0].color = c;
        }
    }

    void EnterPortal()
    {
        IsSafeWindow = true;
        _blackoutT = _wPortalBlackout;
        // 基础版只换背景：在 bg1 / bg2 间切换；不跳则 _curBgSprite 不变（留原图）。
        _curBgSprite = (_curBgSprite == _bgSprite1) ? _bgSprite2 : _bgSprite1;
    }

    // ============================================================
    // 地形段 + 金币
    // ============================================================

    void TickSegments(float sceneDx)
    {
        float leftEdge = -ViewHalfWPx() - _viewMarginPx;
        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            var s = _segments[i];
            s.left -= sceneDx;
            s.right -= sceneDx;
            if (s.right < leftEdge) _segments.RemoveAt(i);
        }

        float rightEdge = ViewHalfWPx() + _viewMarginPx;
        int guard = 0;
        while (guard++ < 64)
        {
            float rightMost = _segments.Count > 0 ? _segments[_segments.Count - 1].right : leftEdge;
            if (rightMost >= rightEdge) break;
            AppendSegment(rightMost);
        }
    }

    void AppendSegment(float left)
    {
        SegKind kind;
        switch (_segPhase)
        {
            case 0: kind = SegKind.Platform; break;
            case 1: kind = SegKind.Gap; break;      // 断沟，无地面
            case 2: kind = SegKind.Platform; break; // 沟后必接平台（怪不落沟里）
            default: kind = (Random.value < 0.5f) ? SegKind.High : SegKind.Platform; break;
        }
        _segPhase = (_segPhase + 1) % 4;

        float len = (kind == SegKind.Gap)
            ? Random.Range(_wGapMin, _wGapMax)
            : Random.Range(_wPlatformLenMin, _wPlatformLenMax);
        var s = new Segment
        {
            left = left,
            right = left + len,
            kind = kind,
            hasGround = kind != SegKind.Gap,
            groundTopY = _groundTopYPx + (kind == SegKind.High ? _wHighPlatformHeight : 0f)
        };
        _segments.Add(s);

        if (kind == SegKind.Gap) SpawnCoinArc(s.left, len, s.groundTopY);
        else SpawnGroundCoins(s);
    }

    float CoinSpacingPx()
    {
        // coin_density 枚 / 100m；1m = pixels_per_meter 逻辑 px。
        return Mathf.Max(1f, (_wPxPerMeter * 100f) / Mathf.Max(1f, _wCoinDensity));
    }

    void SpawnGroundCoins(Segment s)
    {
        float spacing = CoinSpacingPx();
        for (float x = s.left + spacing * 0.5f; x < s.right - spacing * 0.5f; x += spacing)
            SpawnProp(PropKind.Coin, x, s.groundTopY, -1);
    }

    /// <summary>跨沟金币弧线：u∈[0,1] 沿沟宽，y = 地面 + high_platform_height·sin(π·u)（跳跃预告）。
    /// 弧线峰值无专属表 key，复用 high_platform_height 标定量。</summary>
    void SpawnCoinArc(float left, float width, float baseY)
    {
        int n = Mathf.Max(COIN_ARC_MIN_COUNT, Mathf.RoundToInt(width / CoinSpacingPx()));
        for (int i = 0; i < n; i++)
        {
            float u = n > 1 ? (float)i / (n - 1) : 0.5f;
            float x = left + u * width;
            float y = baseY + _wHighPlatformHeight * Mathf.Sin(Mathf.PI * u);
            SpawnProp(PropKind.Coin, x, y, -1);
        }
    }

    // ============================================================
    // 波次调度
    // ============================================================

    void TickWaves(float dt)
    {
        if (_betweenWaves)
        {
            _betweenWaveTimer -= dt;
            if (_betweenWaveTimer > 0f) return;
            int id = DrawWaveTemplate();
            _curSteps = _templates[id - 1].steps;
            if (_curSteps.Count == 0)   // 空模板不进执行分支，避免越界
            {
                _betweenWaves = true;
                _betweenWaveTimer = Random.Range(_wWaveIntervalMin, _wWaveIntervalMax);
                return;
            }
            _stepIndex = 0;
            _waitTimer = _curSteps[0].waitBefore;
            _betweenWaves = false;
            return;
        }

        _waitTimer -= dt;
        if (_waitTimer > 0f) return;
        ExecuteStep(_curSteps[_stepIndex]);
        _stepIndex++;
        if (_stepIndex >= _curSteps.Count)
        {
            _betweenWaves = true;
            _betweenWaveTimer = Random.Range(_wWaveIntervalMin, _wWaveIntervalMax);
        }
        else
        {
            _waitTimer = _curSteps[_stepIndex].waitBefore;
        }
    }

    void ExecuteStep(WaveStep step)
    {
        switch (step.kind)
        {
            case StepKind.Normal: SpawnMonsterWave(step.count, false); break;
            case StepKind.Elite: SpawnMonsterWave(1, true); break;   // 精英单独成波
            case StepKind.CoinArc: SpawnCoinArcPreview(); break;      // 非怪波
        }
    }

    /// <summary>wave_7 的 coin_arc：非怪波，在最近一段沟上再铺一条弧线金币预告。</summary>
    void SpawnCoinArcPreview()
    {
        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            var s = _segments[i];
            if (s.kind == SegKind.Gap)
            {
                SpawnCoinArc(s.left, s.right - s.left, s.groundTopY);
                return;
            }
        }
        // 无沟时，在右边缘外铺一段预告（不改地形）。
        float x = ViewHalfWPx() + _viewMarginPx;
        SpawnCoinArc(x, _wGapMin, _groundTopYPx);
    }

    void SpawnMonsterWave(int count, bool elite)
    {
        // 硬上限：≤ wave_count_max，且 ≤ wave_5_length 能容纳的等间距只数。
        int cap = Mathf.Min(_wWaveCountMax,
            Mathf.FloorToInt(_wWave5Length / Mathf.Max(1f, _wWaveGroupGap)) + 1);
        count = Mathf.Clamp(count, _wWaveCountMin, cap);

        // 图缺失 → 整波跳过生成（绝不占位 / 纯色兜底）。
        if (elite && _eliteMonsterSprite == null) return;
        if (!elite && _normalMonsterSprites.Length == 0) return;

        Segment seg = FindWaveSegment();
        if (seg == null) return;

        float pad = _wWaveGroupGap * 0.5f;
        float usable = (seg.right - pad) - (seg.left + pad);
        int fit = Mathf.Max(1, Mathf.FloorToInt(usable / Mathf.Max(1f, _wWaveGroupGap)) + 1);
        count = Mathf.Min(count, fit);
        float span = (count - 1) * _wWaveGroupGap;
        float startX = seg.right - pad - span;   // 整波落在单段内：不跨段、不跨沟

        float hp = _wEnemyHpBase * (1f + Mathf.Floor(Meters / HP_TIER_METERS))
                 * (_wEnemyHpPer100m / Mathf.Max(1f, _wEnemyHpBase));
        for (int i = 0; i < count; i++)
        {
            Sprite sp = elite ? _eliteMonsterSprite
                              : _normalMonsterSprites[Random.Range(0, _normalMonsterSprites.Length)];
            SpawnMonster(startX + i * _wWaveGroupGap, seg.groundTopY, sp, elite, hp);
        }
    }

    /// <summary>选最靠右的有地面段（生成点始终在玩家右侧，怪只从右进）。</summary>
    Segment FindWaveSegment()
    {
        float bestRight = float.NegativeInfinity;
        Segment best = null;
        for (int i = 0; i < _segments.Count; i++)
        {
            var s = _segments[i];
            if (!s.hasGround) continue;
            if (s.left < _playerXPx + _wWaveGroupGap) continue;
            if (s.right > bestRight) { bestRight = s.right; best = s; }
        }
        return best;
    }

    void SpawnMonster(float x, float y, Sprite sp, bool elite, float hp)
    {
        RunEnemy e = TakeEnemy();
        e.LogicX = x;
        e.logicY = y;
        e.Hp = hp;
        e.IsElite = elite;
        e.IsAlive = true;
        // 判定盒半宽：图逻辑宽度的一半再缩 15%；不含 0.7（渲染缩放只进 localScale）。
        e.ColliderHalfW = (sp.rect.width / Mathf.Max(1f, sp.pixelsPerUnit)) / INV_PX2U * 0.5f * 0.85f;

        EnsureEnemyView(e);
        e.viewSr.sprite = sp;          // 真图，绝不纯色
        e.viewGo.SetActive(true);
        SyncEnemyPos(e);
        _enemies.Add(e);
    }

    // ============================================================
    // 宝箱 / 传送门
    // ============================================================

    void TickChests()
    {
        int anchor = Mathf.FloorToInt(Meters / Mathf.Max(1f, _wChestInterval));
        if (_chestAnchors.Contains(anchor)) return;
        _chestAnchors.Add(anchor);   // HashSet<int> 里程锚点去重
        // 本期只建锚点 / 占位数据；道具效果（回心/加攻/招人/吸金/护心）交后续任务。
        SpawnProp(PropKind.Chest, ViewHalfWPx() + _viewMarginPx + _wWaveGroupGap, _groundTopYPx, anchor);
    }

    void TickPortals()
    {
        if (Meters < _nextPortalMeters) return;
        SpawnPortal();
        _nextPortalMeters += Random.Range(_wPortalIntervalMin, _wPortalIntervalMax);
    }

    void SpawnPortal()
    {
        float x = ViewHalfWPx() + _viewMarginPx + _wWaveGroupGap;
        WorldProp p = SpawnProp(PropKind.Portal, x, _groundTopYPx, -1);
        _portalProp = p;
        _portalApproaching = true;
        _portalLatched = false;
        // 门前金币弧线预告（迎面侧 = 门左侧一段）。
        SpawnCoinArc(x - _wGapMin, _wGapMin, _groundTopYPx);
    }

    // ============================================================
    // 实体位移 / 回收 / 门结算
    // ============================================================

    void TickEntities(float sceneDx, float monsterDx)
    {
        float leftEdge = -ViewHalfWPx() - _viewMarginPx;
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            RunEnemy e = _enemies[i];
            if (!e.IsAlive) { ReleaseEnemy(e); _enemies.RemoveAt(i); continue; }
            e.LogicX -= monsterDx;
            SyncEnemyPos(e);
            if (e.LogicX < leftEdge - _wWaveGroupGap)
            { ReleaseEnemy(e); _enemies.RemoveAt(i); }
        }

        for (int i = _props.Count - 1; i >= 0; i--)
        {
            WorldProp p = _props[i];
            p.logicX -= sceneDx;
            if (p.go != null) SyncPropPos(p);
            if (p.logicX < leftEdge - _wWaveGroupGap)
            { ReleaseProp(p); _props.RemoveAt(i); }
        }

        // 门越过玩家后结算：有跳上升沿 = 进门（黑屏 + 切背景）；无跳 = 留原图。
        if (_portalApproaching && _portalProp != null && _portalProp.logicX <= _playerXPx - _wWaveGroupGap)
        {
            if (_portalLatched) EnterPortal();
            _portalApproaching = false;
            _portalLatched = false;
            _portalProp = null;
        }
    }

    // ============================================================
    // 实例池（照 T2 TakeChunk / ReleaseChunk 与竹篓 TakeDrop / ReleaseDrop 思路；不用 PoolManager）
    // ============================================================

    RunEnemy TakeEnemy()
    {
        for (int i = 0; i < _enemyPool.Count; i++)
            if (!_enemyPool[i].IsAlive) return _enemyPool[i];
        var e = new RunEnemy();
        _enemyPool.Add(e);
        return e;
    }

    void ReleaseEnemy(RunEnemy e)
    {
        e.IsAlive = false;
        if (e.viewGo != null) e.viewGo.SetActive(false);
        if (e.viewSr != null) e.viewSr.sprite = null;
    }

    void EnsureEnemyView(RunEnemy e)
    {
        if (e.viewGo != null) return;
        var go = new GameObject("RunEnemy");
        go.transform.SetParent(_worldRoot.transform, false);
        go.layer = _worldLayer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 4;
        // 0.7 唯一落点（渲染）：判定 / 世界坐标一个都不乘 0.7。
        go.transform.localScale = new Vector3(_wRenderScale, _wRenderScale, 1f);
        e.viewGo = go;
        e.viewSr = sr;
    }

    void SyncEnemyPos(RunEnemy e)
    {
        // 逻辑像素只乘 INV_PX2U 转 unit；不含 0.7。
        e.viewGo.transform.localPosition = new Vector3(e.LogicX * INV_PX2U, e.logicY * INV_PX2U, 0f);
    }

    WorldProp SpawnProp(PropKind kind, float x, float y, int anchorM)
    {
        WorldProp p = TakeProp();
        p.kind = kind;
        p.logicX = x;
        p.logicY = y;
        p.anchorM = anchorM;
        p.alive = true;

        Sprite sp = SpriteFor(kind);
        if (sp != null)
        {
            EnsurePropView(p);
            p.sr.sprite = sp;         // 真图；无专属图时下列分支不执行（不建纯色）
            p.go.SetActive(true);
            SyncPropPos(p);
        }
        else if (p.go != null)
        {
            p.go.SetActive(false);    // 复用过的旧视图仅在无图时隐藏，本体仍为纯逻辑数据
        }
        _props.Add(p);
        return p;
    }

    Sprite SpriteFor(PropKind kind)
    {
        switch (kind)
        {
            case PropKind.Coin: return _coinSprite;
            case PropKind.Chest: return _chestSprite;
            case PropKind.Portal: return _portalSprite;
            default: return null;
        }
    }

    WorldProp TakeProp()
    {
        for (int i = 0; i < _propPool.Count; i++)
            if (!_propPool[i].alive) return _propPool[i];
        var p = new WorldProp();
        _propPool.Add(p);
        return p;
    }

    void ReleaseProp(WorldProp p)
    {
        p.alive = false;
        if (p.go != null) p.go.SetActive(false);
        if (p.sr != null) p.sr.sprite = null;
    }

    void EnsurePropView(WorldProp p)
    {
        if (p.go != null) return;
        var go = new GameObject("RunProp");
        go.transform.SetParent(_worldRoot.transform, false);
        go.layer = _worldLayer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 3;
        // 0.7 只进 localScale（渲染）。
        go.transform.localScale = new Vector3(_wRenderScale, _wRenderScale, 1f);
        p.go = go;
        p.sr = sr;
    }

    void SyncPropPos(WorldProp p)
    {
        // 逻辑像素只乘 INV_PX2U 转 unit；不含 0.7。
        p.go.transform.localPosition = new Vector3(p.logicX * INV_PX2U, p.logicY * INV_PX2U, 0f);
    }
}
