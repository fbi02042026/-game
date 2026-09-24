using UnityEngine;

/// <summary>
/// 把 FocusMarkSystem 选中的攻击目标可视化：头顶箭头（上下浮动）+ 脚下贴地光圈。
/// 职责单一：只负责显示，不改任何战斗逻辑、不动 FocusMarkSystem 本身。
/// 指示器永远只显示 1 个目标（就是 FocusMarkSystem 选中的那个）。
/// </summary>
public class TargetIndicator : MonoBehaviour
{
    public static TargetIndicator Instance { get; private set; }

    [Header("箭头")]
    [Tooltip("箭头图方向未知，默认翻转 Y 让悬在头顶的箭头指向下方的目标")]
    public bool arrowFlipY = true;
    public float arrowFloatFreq = 2.8f;   // 上下浮动频率
    public float arrowFloatAmp = 0.12f;    // 上下浮动幅度（世界单位）
    public float arrowHeadOffset = 0.25f;  // 头顶再往上抬一点

    [Header("脚下光圈")]
    public float ringScaleX = 1.9f;        // 目标约 1.2 世界单位宽 → 64px@PPU100 缩放
    public float ringScaleY = 0.8f;        // 目标约 0.5 世界单位高（扁椭圆）

    [Header("总开关（临时关掉看对比）")]
    public bool Enabled = true;

    SpriteRenderer _arrowSr;
    SpriteRenderer _ringSr;
    Sprite _ringSprite;

    // 攻击目标箭头三张备选贴图：普通怪(PT) / 精英怪(JY) / 兜底(箭头01)
    Sprite _sprNormal;
    Sprite _sprElite;
    Sprite _sprFallback;
    Sprite _appliedArrowSpr;       // 当前已应用的箭头 sprite，避免每帧重复赋值

    UnitBase _currentTarget;       // 当前跟随的目标，用于检测切换
    SpriteRenderer _targetBodySr;  // 缓存目标身体 Sprite，取头顶高度
    bool _shown;                   // 上一帧是否处于显示态
    bool _snap;                    // 本帧需要直接定位（首帧/目标切换，避免从旧位置 lerp 拖影）

    public static TargetIndicator Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TargetIndicator");
        DontDestroyOnLoad(go);
        return go.AddComponent<TargetIndicator>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildChildren();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>创建 Arrow / Ring 两个世界空间子节点（挂在自己 transform 下）。</summary>
    void BuildChildren()
    {
        // ---- Arrow（头顶箭头）----
        var arrowGo = new GameObject("Arrow");
        arrowGo.transform.SetParent(transform, false);
        _arrowSr = arrowGo.AddComponent<SpriteRenderer>();
        _arrowSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        _arrowSr.sortingOrder = GameConfig.SORT_VFX + 5; // 55，画在怪物与伤害数字之上
        _arrowSr.enabled = false;                         // 默认隐藏，等加载到 sprite

        LoadArrowSprites();
        // 初始用兜底图占位（LateUpdate 会按目标类型换成 PT/JY）
        if (_sprFallback != null)
        {
            _arrowSr.sprite = _sprFallback;
            _arrowSr.flipY = arrowFlipY;
            _arrowSr.enabled = true;
            _appliedArrowSpr = _sprFallback;
        }
        else
        {
            Debug.LogWarning("[TargetIndicator] 未找到 Resources/UI/Common/箭头01.png，箭头已隐藏（不报错、不空引用）");
        }

        // ---- Ring（脚下光圈，代码生成，不依赖新美术）----
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(transform, false);
        _ringSr = ringGo.AddComponent<SpriteRenderer>();
        _ringSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        _ringSr.sortingOrder = GameConfig.SORT_VFX + 4;  // 54，略低于箭头免得盖住
        _ringSprite = GenerateRingSprite();
        _ringSr.sprite = _ringSprite;
        _ringSr.color = new Color(1f, 0.95f, 0.7f, 0.45f); // 暖白淡色，草地上不刺眼
        ringGo.transform.localScale = new Vector3(ringScaleX, ringScaleY, 1f);

        arrowGo.SetActive(false);
        ringGo.SetActive(false);
    }

    /// <summary>
    /// 加载箭头三张备选 sprite 并缓存。PT=普通怪，JY=精英怪，箭头01=兜底。
    /// 三张都加载失败时才打一条警告，不刷屏。
    /// </summary>
    void LoadArrowSprites()
    {
        _sprNormal = Resources.Load<Sprite>("UI/Common/PT");
        _sprElite = Resources.Load<Sprite>("UI/Common/JY");
        _sprFallback = Resources.Load<Sprite>("UI/Common/箭头01");
        if (_sprNormal == null && _sprElite == null && _sprFallback == null)
            Debug.LogWarning("[TargetIndicator] 未找到箭头 sprite（PT/JY/箭头01），攻击目标箭头将不可用");
    }

    /// <summary>
    /// 代码生成一个 64×64 的椭圆贴图（中心不透明、边缘透明），缓存复用，不每帧新建。
    /// </summary>
    static Sprite GenerateRingSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float rx = size * 0.5f;
        float ry = size * 0.5f;

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 归一化椭圆距离：0=中心，1=边缘
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 中心不透明，向边缘平滑淡出到透明
                float a = d <= 1f ? 1f - Mathf.SmoothStep(0.55f, 1.0f, d) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return sp;
    }

    void LateUpdate()
    {
        if (!Enabled)
        {
            HideAll();
            return;
        }

        var target = FocusMarkSystem.MarkedEnemy;

        // BOSS 直接隐藏整个指示器（头顶箭头 + 脚下光圈），Boss 已有屏幕血条
        if (target is Monster bossMon && bossMon.IsBossUnit)
        {
            HideAll();
            return;
        }

        var bm = BattleManager.InstanceQuiet;   // 战斗外为 null 且不打 LogError
        bool show = target != null
                    && !target.isDead
                    && bm != null
                    && bm.isInBattle
                    && bm.UnitsCanAct;

        if (!show)
        {
            HideAll();
            return;
        }

        // 目标切换时刷新缓存（避免每帧 GetComponent 产生 GC）
        if (_currentTarget != target)
        {
            _currentTarget = target;
            _targetBodySr = target.GetComponentInChildren<SpriteRenderer>(true);
            _snap = true; // 目标变了，下一处直接定位
        }

        // GetBodyTransform 只定义在 Monster 上，UnitBase 没有 → 必须做类型判断再取
        Transform bodyTf = target is Monster mon ? mon.GetBodyTransform() : target.transform;
        Vector3 bodyPos = bodyTf != null ? bodyTf.position : target.transform.position;

        // 头顶高度：优先用身体 Sprite 的 bounds.max.y，拿不到退回 position.y + 1
        float headY = bodyPos.y + 1f;
        if (_targetBodySr != null && _targetBodySr.sprite != null)
            headY = _targetBodySr.bounds.max.y;

        // 箭头目标位置：头顶 + 偏移 + 上下浮动
        float floatY = Mathf.Sin(Time.unscaledTime * arrowFloatFreq) * arrowFloatAmp;
        Vector3 arrowTarget = new Vector3(bodyPos.x, headY + arrowHeadOffset + floatY, bodyPos.z);

        // 光圈目标位置：脚下贴地（X 跟目标走，Y 用 FootY）
        float footY = target.FootY;
        Vector3 ringTarget = new Vector3(bodyPos.x, footY, bodyPos.z);

        if (!_shown || _snap)
        {
            // 首次出现 / 目标切换：直接定位，避免从旧位置 lerp 拖影
            SnapTo(arrowTarget, ringTarget);
        }
        else
        {
            // 平滑跟随（Vector3.Lerp 返回 struct，无堆分配）
            _arrowSr.transform.position = Vector3.Lerp(_arrowSr.transform.position, arrowTarget, 0.25f);
            _ringSr.transform.position = Vector3.Lerp(_ringSr.transform.position, ringTarget, 0.25f);
        }
        _snap = false;

        // 按目标类型选择箭头贴图：精英=JY，普通/其他=PT，缺则回退箭头01
        Sprite wantSpr;
        if (target is Monster elMon && elMon.IsEliteWave)
            wantSpr = _sprElite;
        else
            wantSpr = _sprNormal;
        if (wantSpr == null) wantSpr = _sprFallback;

        if (wantSpr == null)
        {
            // 三张都缺：只隐藏箭头，光圈照常显示
            _arrowSr.gameObject.SetActive(false);
        }
        else
        {
            // 只在 sprite 真正变化时赋值，避免每帧重复设置
            if (_appliedArrowSpr != wantSpr)
            {
                _arrowSr.sprite = wantSpr;
                _arrowSr.flipY = arrowFlipY;
                _appliedArrowSpr = wantSpr;
            }
            _arrowSr.gameObject.SetActive(true);
            _arrowSr.enabled = true;   // 缺陷4：兜底图加载失败时 SpriteRenderer 仍 disabled，需补启用
        }
        _ringSr.gameObject.SetActive(true);
        _shown = true;
    }

    /// <summary>直接把两个指示器放到目标位置（切换/首帧用）。</summary>
    void SnapTo(Vector3 arrowPos, Vector3 ringPos)
    {
        _arrowSr.transform.position = arrowPos;
        _ringSr.transform.position = ringPos;
    }

    void HideAll()
    {
        _currentTarget = null;
        _targetBodySr = null;
        _shown = false;
        if (_arrowSr != null) _arrowSr.gameObject.SetActive(false);
        if (_ringSr != null) _ringSr.gameObject.SetActive(false);
    }

    /// <summary>
    /// 切场景 / 重开战斗时清状态并隐藏。照 FocusMarkSystem.ResetForBattle 的风格。
    /// 不强制要求被调用，方法存在且安全即可。
    /// </summary>
    public void ResetForBattle()
    {
        HideAll();
    }
}
